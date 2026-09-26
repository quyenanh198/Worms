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
            new Color(0.95f, 0.52f, 0.55f),
            new Color(0.45f, 0.66f, 0.98f),
            new Color(0.98f, 0.82f, 0.36f),
            new Color(0.62f, 0.90f, 0.48f),
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
        }

        public Vfx Vfx;
        readonly Dictionary<int, WormView> _worms = new Dictionary<int, WormView>();
        readonly Dictionary<int, ProjectileView> _projectiles = new Dictionary<int, ProjectileView>();
        readonly Dictionary<int, Vector3> _wormPos = new Dictionary<int, Vector3>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _remove = new List<int>();
        Transform _crosshair;

        void Awake()
        {
            _crosshair = new GameObject("Crosshair").transform;
            _crosshair.SetParent(transform, false);
            _crosshair.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshUtil.Create(Core.Shapes.Sphere(0.5f, 8, 12), "Crosshair");
            _crosshair.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Materials.Toon(new Color(1f, 0.25f, 0.2f));
            _crosshair.localScale = Vector3.one * 0.22f;
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

            foreach (var w in cur.Worms)
            {
                if (!_worms.TryGetValue(w.Id, out var view)) _worms[w.Id] = view = new WormView(transform, w.Id, TeamColors.Of(w.Team));
                _seen.Add(w.Id);
                if (!w.Alive)
                {
                    if (!view.Sinking) view.Root.gameObject.SetActive(false);
                    else view.Update(w, Vector3.zero, false, cur.ActiveWeapon, 0, dt);
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
                    pos += Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 7f + w.Id)) * 0.35f;
                    shown.State = WormState.Airborne;
                    shown.Vy = Mathf.Cos(Time.time * 7f + w.Id) * -300f;
                }
                view.Update(shown, pos, active, cur.ActiveWeapon, weapon.Aims ? aim : 0.2f, dt);

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
                        Root = WeaponProps.BuildProjectile(pr.Weapon, fragment, transform),
                        Smokes = pr.Weapon == WeaponId.Bazooka || pr.Weapon == WeaponId.AirStrike,
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
            }
            RemoveMissing(_projectiles, v => Destroy(v.Root.gameObject));
        }

        public void OnHit(int wormId)
        {
            if (_worms.TryGetValue(wormId, out var v)) v.Flash();
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
            screen = cam.WorldToScreenPoint(p + Vector3.up * 1.1f);
            return screen.z > 0;
        }
    }
}
