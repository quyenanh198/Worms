using System.Collections.Generic;
using UnityEngine;
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

        public void Init(SimTerrain terrain, float waterLevel, uint seed, int teamCount)
        {
            WaterLevel = waterLevel;
            TeamCount = teamCount;
            var theme = Theme.ForSeed(seed);
            float mapWidth = terrain.Width * WorldSpace.Scale;
            float waterY = -waterLevel * WorldSpace.Scale;

            Sun = SceneBuilder.CreateSun(transform, theme);
            SceneBuilder.ConfigureEnvironment(theme, Sun);
            SceneBuilder.CreateBackdrop(transform, theme, seed, mapWidth, waterY);
            SceneBuilder.CreateWater(transform, theme, mapWidth, waterY);

            Terrain = new GameObject("Terrain").AddComponent<TerrainView>();
            Terrain.transform.SetParent(transform, false);
            Terrain.Init(terrain, Materials.Terrain(theme));

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
            UrpSetup.ApplyTier(QualityTier.High, Sun);
        }

        public void SetSnapshots(Snapshot previous, Snapshot current)
        {
            Previous = previous;
            Current = current;
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
                        Terrain.RefreshCircle(e.X, e.Y, e.Value);
                        Vfx.Explosion(at, e.Value * WorldSpace.Scale);
                        ShakeFrom(at, e.Value);
                        break;
                    case SimEventType.Fire:
                    {
                        var shooter = Actors.WormPosition(e.Worm);
                        var worm = Current?.FindWorm(e.Worm);
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
                            if (e.Weapon == WeaponId.Uzi) Vfx.Muzzle(shooter.Value + dir * 0.7f, dir);
                        }
                        break;
                    }
                    case SimEventType.Hit:
                    {
                        Actors.OnHit(e.Worm);
                        var pos = Actors.WormPosition(e.Worm);
                        if (pos.HasValue) Vfx.AddPopup(pos.Value + Vector3.up * 0.8f, "-" + e.Amount, new Color(1f, 0.45f, 0.35f));
                        Rig.Shake(0.05f + e.Amount * 0.004f);
                        break;
                    }
                    case SimEventType.Land:
                    {
                        var pos = Actors.WormPosition(e.Worm);
                        if (pos.HasValue)
                        {
                            Vfx.Dust(pos.Value + Vector3.down * 0.4f, 2f);
                            if (e.Amount > 0) Vfx.AddPopup(pos.Value + Vector3.up * 0.8f, "-" + e.Amount, new Color(1f, 0.7f, 0.35f));
                        }
                        break;
                    }
                    case SimEventType.Bounce:
                        if (e.Value > 120f) Vfx.Dust(at, 1f);
                        break;
                    case SimEventType.Splash:
                        Vfx.Splash(new Vector3(at.x, -WaterLevel * WorldSpace.Scale, WorldSpace.ActorZ));
                        break;
                    case SimEventType.Death:
                    {
                        var pos = Actors.WormPosition(e.Worm) ?? at;
                        Actors.OnDeath(e.Worm, e.Cause, pos);
                        break;
                    }
                }
            }
        }

        void ShakeFrom(Vector3 at, float radius)
        {
            float distance = Vector2.Distance(at, Rig.transform.position);
            Rig.Shake(radius / 50f * 0.35f * Mathf.Clamp01(1.5f - distance / 40f));
        }

        /// <summary>Called every frame after the snapshots are up to date.</summary>
        public void Render(float alpha, bool localTurn, float localAim)
        {
            if (Current == null) return;
            Actors.Render(Previous, Current, alpha, localTurn, localAim);
            Rig.Follow(FollowPoint(alpha));
        }

        Vector3? FollowPoint(float alpha)
        {
            if (Current.Projectiles.Count > 0)
            {
                var p = Current.Projectiles[0];
                return WorldSpace.ToWorld(p.X, p.Y, 0);
            }
            var worm = Current.FindWorm(Current.ActiveWorm);
            if (worm.HasValue) return WorldSpace.ToWorld(worm.Value.X, worm.Value.Y, 0);
            return null;
        }
    }
}
