using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Audio;
using Worms.Game.Core;
using Worms.Game.Render;
using Worms.Protocol;
using Worms.Sim;
using SimTerrain = Worms.Sim.Terrain;

namespace Worms.Game.Play
{
    /// <summary>
    /// Draws a match from snapshots and events, wherever they come from (local
    /// sandbox now, the server from P3 on). Owns the scene objects.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public TerrainView Terrain { get; private set; }
        public Vfx Vfx { get; private set; }
        public ActorViews Actors { get; private set; }
        public CameraRig Rig { get; private set; }
        public Light Sun { get; private set; }
        public Snapshot Previous { get; private set; }
        public Snapshot Current { get; private set; }
        public float WaterLevel { get; private set; }
        public int TeamCount { get; private set; }
        /// <summary>The team this client plays, or -1 (sandbox, spectator): picks the victory or defeat tune.</summary>
        public int LocalTeam = -1;

        /// <summary>What each team wears (from the server, or the player's own look offline).</summary>
        public void SetLoadouts(IReadOnlyList<Loadout> loadouts)
        {
            Actors.Loadouts = loadouts;
        }
        /// <summary>Time.time of the last kamikaze blast (the shooter and an enemy both done for), for the HUD.</summary>
        public float KamikazeAt { get; private set; } = -100f;

        // Damage taken this turn, per worm. HP in snapshots only drops at the end of the turn,
        // so "damage this turn >= HP" means the worm will not survive it.
        readonly Dictionary<int, int> _turnDamage = new Dictionary<int, int>();
        bool _kamikazeThisTurn;
        SimTerrain _sourceTerrain;
        uint _surfaceSeed;
        bool _cratePropsCreated;

        public void Init(SimTerrain terrain, float waterLevel, uint seed, int teamCount)
        {
            _sourceTerrain = terrain;
            _surfaceSeed = seed;
            WaterLevel = waterLevel;
            TeamCount = teamCount;
            var theme = Theme.ForSeed(seed);
            float mapWidth = terrain.Width * WorldSpace.Scale;
            float waterY = -waterLevel * WorldSpace.Scale;

            Sun = SceneBuilder.CreateSun(transform, theme);
            SceneBuilder.ConfigureEnvironment(theme, Sun);
            SceneBuilder.CreateBackdrop(transform, theme, seed, mapWidth, waterY);
            SceneBuilder.CreateWater(transform, theme, mapWidth, waterY);
            SceneBuilder.CreateSurfaceProps(transform, terrain);
            bool clayOnly = seed % 4u == 3u;
            if (!clayOnly)
            {
                SceneBuilder.CreateGroundFoliage(transform, terrain, seed);
                SceneBuilder.CreateCliffRocks(transform, terrain);
            }

            Terrain = new GameObject("Terrain").AddComponent<TerrainView>();
            Terrain.transform.SetParent(transform, false);
            Terrain.Init(terrain, Materials.Terrain(theme, seed));

            Vfx = new GameObject("Vfx").AddComponent<Vfx>();
            Vfx.transform.SetParent(transform, false);
            Vfx.Init(theme.Dirt);

            Actors = new GameObject("Actors").AddComponent<ActorViews>();
            Actors.transform.SetParent(transform, false);
            Actors.Vfx = Vfx;

            var camGo = new GameObject("Match Camera");
            camGo.transform.SetParent(transform, false);
            Rig = camGo.AddComponent<CameraRig>();
            // Focus stays between the sea and the top of the map.
            float focusBottom = waterY + 4f;
            Rig.Init(new Rect(0, focusBottom, mapWidth, -2f - focusBottom));
            UrpSetup.ConfigureCamera(Rig.Camera);
            UrpSetup.CreateVolume(transform);
            QualitySettingsManager.Apply(this);
        }

        public void SetSnapshots(Snapshot previous, Snapshot current)
        {
            Previous = previous;
            Current = current;
            if (!_cratePropsCreated && current != null && current.Worms.Count > 0)
            {
                // Crate cutouts include stones; leave the clay-only variant clear.
                if (_surfaceSeed % 4u != 3u)
                    SceneBuilder.CreateCrateProps(transform, _sourceTerrain, current.Worms);
                _cratePropsCreated = true;
            }
        }

        /// <summary>Turns simulation events into terrain updates, effects and popups.</summary>
        public void HandleEvents(List<SimEvent> events)
        {
            foreach (var e in events)
            {
                var at = WorldSpace.ToWorld(e.X, e.Y);
                switch (e.Type)
                {
                    case SimEventType.Explode:
                    {
                        // Value is the crater, Amount the blast (weapons dig and hurt by different amounts).
                        float blast = e.Amount > 0 ? e.Amount : e.Value;
                        Terrain.RefreshCircle(e.X, e.Y, e.Value);
                        SceneBuilder.RefreshSurfaceProps(transform, _sourceTerrain);
                        Vfx.Explosion(at, blast * WorldSpace.Scale);
                        ShakeFrom(at, blast);
                        Sound(blast < 25 ? Sfx.ExplosionSmall : blast < 60 ? Sfx.ExplosionMedium : Sfx.ExplosionLarge, at.x);
                        break;
                    }
                    case SimEventType.Burn:
                    {
                        var pos = Actors.WormPosition(e.Worm);
                        if (pos.HasValue)
                        {
                            Sound(Sfx.Burn, pos.Value.x, 0.7f, 0.1f);
                            Vfx.AddPopup(pos.Value + Vector3.up * 0.8f, "-" + e.Amount, new Color(1f, 0.6f, 0.2f));
                            _turnDamage.TryGetValue(e.Worm, out int burnt);
                            _turnDamage[e.Worm] = burnt + e.Amount;
                        }
                        break;
                    }
                    case SimEventType.Turn:
                        Sound(Sfx.TurnBell, CameraX, 0.7f);
                        _turnDamage.Clear();
                        _kamikazeThisTurn = false;
                        break;
                    case SimEventType.GameOver:
                    {
                        // e.Team is the winner (-1: draw).
                        bool won = e.Team >= 0 && (LocalTeam < 0 || e.Team == LocalTeam);
                        Sound(won ? Sfx.Victory : Sfx.Defeat, CameraX, 1f, 0f);
                        break;
                    }
                    case SimEventType.Fire:
                    {
                        var shooter = Actors.WormPosition(e.Worm);
                        var worm = Current?.FindWorm(e.Worm);
                        if (shooter.HasValue) FireSound(e.Weapon, shooter.Value.x);
                        if (shooter.HasValue && worm.HasValue && Weapons.Get(e.Weapon).Aims)
                        {
                            var dir = new Vector3(worm.Value.Facing * Mathf.Cos(worm.Value.Aim), Mathf.Sin(worm.Value.Aim), 0);
                            if (e.Weapon == WeaponId.Bazooka || e.Weapon == WeaponId.Shotgun) Vfx.Muzzle(shooter.Value + dir * 0.9f, dir);
                        }
                        break;
                    }
                    case SimEventType.Shot:
                    {
                        var shooter = Actors.WormPosition(e.Worm);
                        if (shooter.HasValue)
                        {
                            var dir = (at - shooter.Value).normalized;
                            Vfx.Tracer(shooter.Value + dir * 0.7f, at);
                            Sound(e.Weapon == WeaponId.Uzi ? Sfx.UziShot : Sfx.Shotgun, shooter.Value.x, e.Weapon == WeaponId.Uzi ? 0.7f : 1f);
                            if (e.Weapon == WeaponId.Uzi) Vfx.Muzzle(shooter.Value + dir * 0.7f, dir);
                        }
                        break;
                    }
                    case SimEventType.Hit:
                    {
                        Actors.OnHit(e.Worm);
                        _turnDamage.TryGetValue(e.Worm, out int taken);
                        _turnDamage[e.Worm] = taken + e.Amount;
                        var pos = Actors.WormPosition(e.Worm);
                        if (pos.HasValue) Sound(e.Amount >= 35 ? Sfx.OuchBig : Sfx.Hurt, pos.Value.x, 0.8f, 0.12f);
                        if (pos.HasValue) Vfx.AddPopup(pos.Value + Vector3.up * 0.8f, "-" + e.Amount, new Color(1f, 0.45f, 0.35f));
                        Rig.Shake(0.05f + e.Amount * 0.004f);
                        break;
                    }
                    case SimEventType.Land:
                    {
                        var pos = Actors.WormPosition(e.Worm);
                        if (pos.HasValue)
                        {
                            Sound(Sfx.Land, pos.Value.x);
                            Vfx.Dust(pos.Value + Vector3.down * 0.4f, 2f);
                            if (e.Amount > 0) Vfx.AddPopup(pos.Value + Vector3.up * 0.8f, "-" + e.Amount, new Color(1f, 0.7f, 0.35f));
                        }
                        break;
                    }
                    case SimEventType.Bounce:
                        if (e.Value > 120f) Vfx.Dust(at, 1f);
                        Sound(Sfx.Bounce, at.x, Mathf.Clamp01(e.Value / 400f));
                        break;
                    case SimEventType.Splash:
                        Vfx.Splash(new Vector3(at.x, -WaterLevel * WorldSpace.Scale, WorldSpace.ActorZ));
                        Sound(Sfx.Splash, at.x);
                        break;
                    case SimEventType.Death:
                    {
                        var pos = Actors.WormPosition(e.Worm) ?? at;
                        DeathVoice(e.Worm, pos.x);
                        Actors.OnDeath(e.Worm, e.Cause, pos);
                        break;
                    }
                }
            }
            CheckKamikaze();
        }

        /// <summary>
        /// The shooter's own blast was lethal to it and to at least one enemy: a suicide
        /// bombing. A war cry, a second fireball, a white flash, a big shake, and a banner
        /// from the HUD. Once per turn.
        /// </summary>
        void CheckKamikaze()
        {
            if (_kamikazeThisTurn || Current == null || Current.ActiveTeam < 0 || _turnDamage.Count < 2) return;
            var shooter = Current.FindWorm(Current.ActiveWorm);
            if (!shooter.HasValue || !Doomed(shooter.Value)) return;
            bool enemyDown = false;
            foreach (var w in Current.Worms)
                if (w.Team != shooter.Value.Team && Doomed(w)) enemyDown = true;
            if (!enemyDown) return;

            _kamikazeThisTurn = true;
            KamikazeAt = Time.time;
            var at = Actors.WormPosition(shooter.Value.Id) ?? WorldSpace.ToWorld(shooter.Value.X, shooter.Value.Y);
            Sound(Sfx.Kamikaze, at.x, 1f, 0f);
            Vfx.Explosion(at + Vector3.up * 0.3f, 3.2f);
            Rig.Shake(1.2f);
        }

        bool Doomed(WormSnap w)
        {
            return w.Alive && _turnDamage.TryGetValue(w.Id, out int taken) && taken >= w.Hp;
        }

        float CameraX => Rig.transform.position.x;

        void Sound(Sfx sfx, float worldX, float volume = 1f, float jitter = 0.06f)
        {
            if (AudioManager.Instance == null) return;
            float half = Rig.Distance * Mathf.Tan(Rig.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Rig.Camera.aspect;
            AudioManager.Instance.Play(sfx, AudioManager.PanFor(worldX, CameraX, half), volume, jitter);
        }

        /// <summary>
        /// Deaths are settled at the end of the shooter's turn, so the active team is the one
        /// that caused them: an enemy kill gets the shooter's teasing "bye-bye~", a worm lost
        /// to its own team's shot gets "uh-oh", anything else the victim's sad "bye-bye".
        /// </summary>
        void DeathVoice(int wormId, float x)
        {
            var victim = Current?.FindWorm(wormId);
            int shooterTeam = Current?.ActiveTeam ?? -1;
            if (!victim.HasValue || shooterTeam < 0) { Sound(Sfx.Death, x); return; }
            if (victim.Value.Team == shooterTeam) { if (!_kamikazeThisTurn) Sound(Sfx.Oops, x, 1f, 0.04f); return; }
            var shooter = Actors.WormPosition(Current.ActiveWorm);
            Sound(Sfx.Taunt, shooter.HasValue ? shooter.Value.x : x, 1f, 0.04f);
        }

        void FireSound(WeaponId weapon, float x)
        {
            switch (weapon)
            {
                case WeaponId.Bazooka: Sound(Sfx.Launch, x); break;
                case WeaponId.Grenade:
                case WeaponId.ClusterBomb:
                case WeaponId.Dynamite: Sound(Sfx.Throw, x); break;
                case WeaponId.BaseballBat: Sound(Sfx.Swing, x); Sound(Sfx.Bonk, x, 0.8f); break;
                case WeaponId.AirStrike:
                case WeaponId.Napalm: Sound(Sfx.AirRaid, x); break;
            }
        }

        void ShakeFrom(Vector3 at, float radius)
        {
            float distance = Vector2.Distance(at, Rig.transform.position);
            Rig.Shake(radius / 50f * 0.35f * Mathf.Clamp01(1.5f - distance / 40f));
        }

        /// <summary>Called every frame after the snapshots are up to date.</summary>
        int _lastTickSecond = -1;
        int _framedWorm = -1;
        readonly System.Collections.Generic.Dictionary<int, WormState> _lastState = new System.Collections.Generic.Dictionary<int, WormState>();

        public void Render(float alpha, bool localTurn, float localAim)
        {
            if (Current == null) return;
            if (QualitySettingsManager.Sample(Time.unscaledDeltaTime)) QualitySettingsManager.Apply(this);
            Actors.Render(Previous, Current, alpha, localTurn, localAim);
            Rig.Follow(FollowPoint(alpha));

            // Last five seconds of our turn: tick.
            int second = Mathf.CeilToInt(Current.TurnTicksLeft / (float)C.TicksPerSecond);
            if (localTurn && Current.Phase == Phase.Aiming && second <= 5 && second > 0 && second != _lastTickSecond) Sound(Sfx.Tick, CameraX, 0.8f, 0f);
            _lastTickSecond = second;

            // Jumps are not events; hear them when a worm leaves the ground going up.
            foreach (var w in Current.Worms)
            {
                _lastState.TryGetValue(w.Id, out var before);
                if (w.State == WormState.Airborne && before != WormState.Airborne && w.Vy < -150f)
                    Sound(Sfx.Jump, WorldSpace.ToWorld(w.X, w.Y).x, 0.7f);
                _lastState[w.Id] = w.State;
            }
        }

        Vector3? FollowPoint(float alpha)
        {
            if (Current.Projectiles.Count > 0)
            {
                var p = Current.Projectiles[0];
                var projectile = WorldSpace.ToWorld(p.X, p.Y, 0);
                if (Rig.Camera.aspect >= 0.75f)
                {
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    int living = 0;
                    foreach (var actor in Current.Worms)
                    {
                        if (!actor.Alive) continue;
                        var position = WorldSpace.ToWorld(actor.X, actor.Y, 0);
                        minX = Mathf.Min(minX, position.x);
                        maxX = Mathf.Max(maxX, position.x);
                        minY = Mathf.Min(minY, position.y);
                        maxY = Mathf.Max(maxY, position.y);
                        living++;
                    }
                    // Keep the battle tableau visible while a shot crosses the
                    // playable valley. Follow the rocket once it leaves that frame.
                    if (living >= 2 && maxX - minX <= 50f && maxY - minY <= 20f &&
                        projectile.x >= minX - 2f && projectile.x <= maxX + 2f &&
                        projectile.y >= minY - 4f && projectile.y <= maxY + 8f)
                        return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f + 1f, 0);
                }
                return projectile;
            }
            var worm = Current.FindWorm(Current.ActiveWorm);
            if (worm.HasValue)
            {
                var active = WorldSpace.ToWorld(worm.Value.X, worm.Value.Y, 0);
                if (Rig.Camera.aspect < 0.75f)
                {
                    if (_framedWorm != worm.Value.Id)
                    {
                        _framedWorm = worm.Value.Id;
                        Rig.Distance = 19f;
                    }
                    return active + Vector3.down * 0.4f;
                }
                Vector3 nearest = default;
                float nearestSqr = float.MaxValue;
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                int living = 0;
                foreach (var other in Current.Worms)
                {
                    if (!other.Alive) continue;
                    var at = WorldSpace.ToWorld(other.X, other.Y, 0);
                    living++;
                    minX = Mathf.Min(minX, at.x);
                    maxX = Mathf.Max(maxX, at.x);
                    minY = Mathf.Min(minY, at.y);
                    maxY = Mathf.Max(maxY, at.y);
                    if (other.Team == worm.Value.Team) continue;
                    float sqr = (at - active).sqrMagnitude;
                    if (sqr >= nearestSqr) continue;
                    nearestSqr = sqr;
                    nearest = at;
                }

                bool overview = living >= 2 && maxX - minX <= 50f && maxY - minY <= 20f;
                bool pairVisible = nearestSqr <= 30f * 30f &&
                    Mathf.Abs(nearest.y - active.y) <= 12f;
                if (_framedWorm != worm.Value.Id)
                {
                    _framedWorm = worm.Value.Id;
                    if (overview || pairVisible)
                    {
                        float tan = Mathf.Tan(Rig.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        float aspect = Mathf.Max(1f, Rig.Camera.aspect);
                        float width = overview ? maxX - minX : Mathf.Abs(nearest.x - active.x);
                        float height = overview ? maxY - minY : Mathf.Abs(nearest.y - active.y);
                        Rig.Distance = overview
                            ? Mathf.Clamp(Mathf.Max(25f, width / (1.65f * tan * aspect),
                                height / (1.35f * tan)), 25f, 42f)
                            : Mathf.Clamp(Mathf.Max(30f, width / (1.45f * tan * aspect),
                                height / (1.30f * tan)), 30f, 35f);
                    }
                    else Rig.Distance = 25f;
                }
                if (overview)
                    return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f + 1f, 0);
                // The center of the shot stays between the current actor and the
                // nearest close opponent; distant enemies cannot displace the actor.
                return pairVisible
                    ? Vector3.Lerp(active, nearest, 0.5f)
                    : active + Vector3.up * 1.5f;
            }
            return null;
        }
    }
}
