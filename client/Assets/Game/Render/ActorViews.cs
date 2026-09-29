using System.Collections.Generic;
using UnityEngine;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Render
{
    public static class TeamColors
    {
        public static readonly Color[] All =
        {
            new Color(0.93f, 0.28f, 0.35f),
            new Color(0.22f, 0.50f, 0.95f),
            new Color(0.97f, 0.71f, 0.16f),
            new Color(0.37f, 0.76f, 0.25f),
        };

        public static Color Of(int team) { return All[((team % All.Length) + All.Length) % All.Length]; }
    }

    /// <summary>
    /// Worms, projectiles and gravestones, drawn from two snapshots
    /// interpolated by <c>alpha</c>.
    /// </summary>
    public sealed class ActorViews : MonoBehaviour
    {
        sealed class ProjectileView
        {
            public Transform Root;
            public Vector3 LastPos;
            public bool Smokes;
            public bool Burns;
        }

        public Vfx Vfx;
        /// <summary>Cosmetics per team (store loadouts); missing teams wear nothing.</summary>
        public IReadOnlyList<Loadout> Loadouts;

        Loadout LoadoutOf(int team)
        {
            return Loadouts != null && team >= 0 && team < Loadouts.Count ? Loadouts[team] : default;
        }
        readonly Dictionary<int, WormView> _worms = new Dictionary<int, WormView>();
        readonly Dictionary<int, ProjectileView> _projectiles = new Dictionary<int, ProjectileView>();
        readonly Dictionary<int, Vector3> _wormPos = new Dictionary<int, Vector3>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _remove = new List<int>();
        Transform _crosshair;
        Transform _activeMarker;

        void Awake()
        {
            _crosshair = new GameObject("Crosshair").transform;
            _crosshair.SetParent(transform, false);
            _crosshair.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshUtil.Create(Core.Shapes.Sphere(0.5f, 8, 12), "Crosshair");
            _crosshair.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials.Toon(new Color(1f, 0.25f, 0.2f));
            _crosshair.localScale = Vector3.one * 0.22f;

            _activeMarker = new GameObject("Active worm marker").transform;
            _activeMarker.SetParent(transform, false);
            var marker = new Mesh
            {
                name = "Active worm arrow",
                vertices = new[] { new Vector3(-0.39f, 0.27f, 0), new Vector3(0.39f, 0.27f, 0), new Vector3(0, -0.33f, 0) },
                triangles = new[] { 0, 1, 2 },
            };
            marker.RecalculateNormals();
            _activeMarker.gameObject.AddComponent<MeshFilter>().sharedMesh = marker;
            _activeMarker.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials.Toon(new Color(1f, 0.96f, 0.86f));
            var inner = new GameObject("Red center");
            inner.transform.SetParent(_activeMarker, false);
            inner.transform.localPosition = new Vector3(0, 0.015f, -0.01f);
            inner.transform.localScale = new Vector3(0.77f, 0.72f, 1f);
            inner.AddComponent<MeshFilter>().sharedMesh = marker;
            inner.AddComponent<MeshRenderer>().sharedMaterial = Materials.Toon(new Color(1f, 0.24f, 0.22f));
        }

        public Vector3? WormPosition(int id)
        {
            return _wormPos.TryGetValue(id, out var p) ? p : (Vector3?)null;
        }

        public void Render(Snapshot prev, Snapshot cur, float alpha, bool localTurn, float localAim)
        {
            float dt = Time.deltaTime;
            _seen.Clear();
            bool aimingPhase = cur.Phase == Phase.Aiming;
            var weapon = Weapons.Get(cur.ActiveWeapon);
            _crosshair.gameObject.SetActive(false);
            _activeMarker.gameObject.SetActive(false);

            foreach (var w in cur.Worms)
            {
                if (!_worms.TryGetValue(w.Id, out var view))
                {
                    _worms[w.Id] = view = new WormView(transform, w.Id, w.Team, TeamColors.Of(w.Team), 1.4f);
                    view.SetLoadout(LoadoutOf(w.Team));
                }
                _seen.Add(w.Id);
                if (!w.Alive)
                {
                    if (view.Sinking) view.Update(w, Vector3.zero, false, cur.ActiveWeapon, 0, dt);
                    else if (view.ShowingKamikaze)
                    {
                        view.Root.gameObject.SetActive(true);
                        view.Update(w, WorldSpace.ToWorld(w.X, w.Y), false, cur.ActiveWeapon, 0, dt);
                    }
                    else view.Root.gameObject.SetActive(false);
                    _wormPos.Remove(w.Id);
                    continue;
                }
                view.Root.gameObject.SetActive(true);
                var p = prev?.FindWorm(w.Id) ?? w;
                var pos = WorldSpace.ToWorld(Mathf.Lerp(p.X, w.X, alpha), Mathf.Lerp(p.Y, w.Y, alpha));
                _wormPos[w.Id] = pos;

                bool active = w.Id == cur.ActiveWorm && aimingPhase;
                float aim = active && localTurn ? localAim : w.Aim;
                var shown = w;
                if (cur.Phase == Phase.GameOver && w.Team == cur.Winner)
                {
                    // Victory: hop on the spot.
                    if (!QualitySettingsManager.ReducedMotion)
                        pos += Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 7f + w.Id)) * 0.35f;
                    shown.State = QualitySettingsManager.ReducedMotion ? WormState.Idle : WormState.Airborne;
                    shown.Vy = QualitySettingsManager.ReducedMotion ? 0 : Mathf.Cos(Time.time * 7f + w.Id) * -300f;
                }
                view.Update(shown, pos, active, cur.ActiveWeapon, weapon.Aims ? aim : 0.2f, dt);

                if (w.Id == cur.ActiveWorm && cur.Phase != Phase.GameOver)
                {
                    _activeMarker.gameObject.SetActive(true);
                    _activeMarker.position = pos + new Vector3(0,
                        (view.UsesPaintedArt ? 2.28f : 1.35f) +
                        (QualitySettingsManager.ReducedMotion ? 0f : Mathf.Sin(Time.time * 4f) * 0.08f), -0.65f);
                    if (Camera.main != null)
                    {
                        float distance = Vector3.Distance(Camera.main.transform.position, _activeMarker.position);
                        _activeMarker.localScale = Vector3.one * Mathf.Clamp(distance / 34f, 0.35f, 1.8f);
                    }
                }

                if (active && weapon.Aims)
                {
                    _crosshair.gameObject.SetActive(true);
                    _crosshair.position = pos + new Vector3(w.Facing * Mathf.Cos(aim), Mathf.Sin(aim), 0) * 1.7f;
                }
            }
            RemoveMissing(_worms, v => v.Destroy());

            _seen.Clear();
            foreach (var pr in cur.Projectiles)
            {
                if (!_projectiles.TryGetValue(pr.Id, out var view))
                {
                    bool fragment = pr.Weapon == WeaponId.ClusterBomb && prev != null && !prev.Projectiles.Exists(x => x.Id == pr.Id) && cur.Projectiles.Count > 1;
                    view = new ProjectileView
                    {
                        Root = WeaponProps.BuildProjectile(pr.Weapon, fragment, transform, ProjectileSkin(cur, pr.Weapon)),
                        Smokes = pr.Weapon == WeaponId.Bazooka || pr.Weapon == WeaponId.AirStrike || pr.Weapon == WeaponId.Napalm,
                        Burns = pr.Weapon == WeaponId.Fire,
                    };
                    view.LastPos = WorldSpace.ToWorld(pr.X, pr.Y);
                    _projectiles[pr.Id] = view;
                }
                _seen.Add(pr.Id);
                float x = pr.X, y = pr.Y;
                if (prev != null)
                    foreach (var pp in prev.Projectiles)
                        if (pp.Id == pr.Id) { x = Mathf.Lerp(pp.X, pr.X, alpha); y = Mathf.Lerp(pp.Y, pr.Y, alpha); }
                var pos = WorldSpace.ToWorld(x, y);
                var delta = pos - view.LastPos;
                if (delta.sqrMagnitude > 1e-6f)
                {
                    // Rockets point where they fly; round things just roll.
                    if (view.Smokes) view.Root.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    else view.Root.Rotate(0, 0, -delta.x * 400f, Space.World);
                    if (view.Smokes && Vfx != null) Vfx.Trail(pos - delta.normalized * 0.2f);
                }
                view.Root.position = pos;
                view.LastPos = pos;
                if (view.Burns && Vfx != null) Vfx.Flame(pos);
            }
            RemoveMissing(_projectiles, v => Destroy(v.Root.gameObject));
        }

        /// <summary>Shots fly in the skin of the team that fired them (the team whose turn it is).</summary>
        byte ProjectileSkin(Snapshot cur, WeaponId weapon)
        {
            var l = LoadoutOf(cur.ActiveTeam);
            return weapon == WeaponId.Bazooka ? l.Bazooka : weapon == WeaponId.Grenade ? l.Grenade : (byte)0;
        }

        public void OnHit(int wormId)
        {
            if (_worms.TryGetValue(wormId, out var v)) v.Flash();
        }

        public void OnFire(int wormId, WeaponId weapon)
        {
            if (_worms.TryGetValue(wormId, out var v)) v.TriggerFire(weapon);
        }

        public void OnBurn(int wormId)
        {
            if (_worms.TryGetValue(wormId, out var v)) v.TriggerBurn();
        }

        public void OnKamikaze(int wormId)
        {
            if (_worms.TryGetValue(wormId, out var v)) v.TriggerKamikaze();
        }

        public void OnDeath(int wormId, DeathCause cause, Vector3 at)
        {
            if (cause == DeathCause.Water)
            {
                if (_worms.TryGetValue(wormId, out var v)) v.StartSinking(at);
                return;
            }
            var stone = WeaponProps.BuildGravestone(transform);
            stone.position = at + Vector3.down * (C.WormRadius * WorldSpace.Scale) + new Vector3(0, 0, 0.1f);
        }

        void RemoveMissing<T>(Dictionary<int, T> map, System.Action<T> destroy)
        {
            _remove.Clear();
            foreach (var kv in map) if (!_seen.Contains(kv.Key)) _remove.Add(kv.Key);
            foreach (var id in _remove)
            {
                destroy(map[id]);
                map.Remove(id);
            }
        }

        /// <summary>Screen-space anchor above a worm for name and HP labels.</summary>
        public bool TryGetLabelPoint(int wormId, Camera cam, out Vector3 screen)
        {
            screen = default;
            if (!_wormPos.TryGetValue(wormId, out var p)) return false;
            float height = _worms.TryGetValue(wormId, out var view) && view.UsesPaintedArt ? 2.75f : 1.25f;
            screen = cam.WorldToScreenPoint(p + Vector3.up * height);
            return screen.z > 0;
        }
    }
}
