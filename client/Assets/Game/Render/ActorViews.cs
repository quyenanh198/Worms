using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Render
{
    public static class TeamColors
    {
        public static readonly Color[] All =
        {
            new Color(0.95f, 0.45f, 0.45f),
            new Color(0.40f, 0.62f, 0.98f),
            new Color(0.98f, 0.82f, 0.32f),
            new Color(0.62f, 0.90f, 0.45f),
        };

        public static Color Of(int team) { return All[((team % All.Length) + All.Length) % All.Length]; }
    }

    /// <summary>
    /// Worms and projectiles, drawn from two snapshots interpolated by
    /// <c>alpha</c>. Placeholder shapes until the procedural worm (P5).
    /// </summary>
    public sealed class ActorViews : MonoBehaviour
    {
        sealed class WormView
        {
            public Transform Root, Body, EyeL, EyeR, Crosshair;
            public Material Material;
            public float Spin;
        }

        readonly Dictionary<int, WormView> _worms = new Dictionary<int, WormView>();
        readonly Dictionary<int, Transform> _projectiles = new Dictionary<int, Transform>();
        readonly HashSet<int> _seen = new HashSet<int>();
        Mesh _body, _sphere, _small;
        Material _white, _black, _projectileMat, _crosshairMat;

        void Awake()
        {
            _body = MeshUtil.Create(Shapes.Capsule(0.36f, 0.95f), "WormBody");
            _sphere = MeshUtil.Create(Shapes.Sphere(0.12f, 8, 12), "Eye");
            _small = MeshUtil.Create(Shapes.Sphere(0.15f, 8, 12), "Projectile");
            _white = Materials.Toon(Color.white);
            _black = Materials.Toon(new Color(0.05f, 0.05f, 0.08f));
            _projectileMat = Materials.Toon(new Color(0.25f, 0.27f, 0.3f));
            _crosshairMat = Materials.Toon(new Color(1f, 0.25f, 0.2f));
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, float scale = 1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        WormView CreateWorm(WormSnap w)
        {
            var root = new GameObject("Worm " + w.Id).transform;
            root.SetParent(transform, false);
            var v = new WormView { Root = root, Material = Materials.Toon(TeamColors.Of(w.Team)) };
            v.Body = Part(root, "Body", _body, v.Material, new Vector3(0, 0.08f, 0)).transform;
            var eyeL = Part(v.Body, "EyeL", _sphere, _white, new Vector3(-0.1f, 0.3f, -0.3f));
            var eyeR = Part(v.Body, "EyeR", _sphere, _white, new Vector3(0.1f, 0.3f, -0.3f));
            Part(eyeL.transform, "Pupil", _sphere, _black, new Vector3(0, 0, -0.07f), 0.5f);
            Part(eyeR.transform, "Pupil", _sphere, _black, new Vector3(0, 0, -0.07f), 0.5f);
            v.EyeL = eyeL.transform;
            v.EyeR = eyeR.transform;
            v.Crosshair = Part(transform, "Crosshair " + w.Id, _small, _crosshairMat, Vector3.zero, 0.8f).transform;
            return v;
        }

        public void Render(Snapshot prev, Snapshot cur, float alpha, bool showAim, float localAim)
        {
            _seen.Clear();
            foreach (var w in cur.Worms)
            {
                if (!_worms.TryGetValue(w.Id, out var view)) _worms[w.Id] = view = CreateWorm(w);
                _seen.Add(w.Id);
                bool alive = w.Alive;
                view.Root.gameObject.SetActive(alive);
                if (!alive)
                {
                    view.Crosshair.gameObject.SetActive(false);
                    continue;
                }

                var p = prev?.FindWorm(w.Id) ?? w;
                float x = Mathf.Lerp(p.X, w.X, alpha), y = Mathf.Lerp(p.Y, w.Y, alpha);
                view.Root.position = WorldSpace.ToWorld(x, y);

                // Facing turns the body; tumbling spins it (visual only, docs/PLAN.md §3.6.3).
                if (w.State == WormState.Tumbling)
                    view.Spin += -Mathf.Sign(w.Vx) * new Vector2(w.Vx, w.Vy).magnitude * Time.deltaTime * 0.12f * Mathf.Rad2Deg;
                else
                    view.Spin = Mathf.MoveTowardsAngle(view.Spin, 0, 720 * Time.deltaTime);
                float squash = w.State == WormState.Walking ? 1f + 0.06f * Mathf.Sin(Time.time * 14f) : 1f;
                view.Body.localRotation = Quaternion.Euler(0, w.Facing > 0 ? -25f : 25f, view.Spin);
                view.Body.localScale = new Vector3(1f / squash, squash, 1f / squash);

                bool aims = Weapons.Get(cur.ActiveWeapon).Aims;
                bool isActive = w.Id == cur.ActiveWorm && showAim && cur.Phase == Phase.Aiming;
                view.Crosshair.gameObject.SetActive(aims && w.Id == cur.ActiveWorm && cur.Phase == Phase.Aiming);
                float aim = isActive ? localAim : w.Aim;
                var dir = new Vector3(w.Facing * Mathf.Cos(aim), Mathf.Sin(aim), 0);
                view.Crosshair.position = view.Root.position + dir * 1.6f;
                // Eyes follow the aim.
                var look = new Vector3(dir.x * 0.04f, dir.y * 0.04f, 0);
                view.EyeL.localPosition = new Vector3(-0.1f, 0.3f, -0.3f) + look;
                view.EyeR.localPosition = new Vector3(0.1f, 0.3f, -0.3f) + look;
            }
            RemoveMissing(_worms, v => { Destroy(v.Root.gameObject); Destroy(v.Crosshair.gameObject); });

            _seen.Clear();
            foreach (var pr in cur.Projectiles)
            {
                if (!_projectiles.TryGetValue(pr.Id, out var t))
                    _projectiles[pr.Id] = t = Part(transform, "Projectile " + pr.Id, _small, _projectileMat, Vector3.zero).transform;
                _seen.Add(pr.Id);
                float x = pr.X, y = pr.Y;
                if (prev != null)
                    foreach (var pp in prev.Projectiles)
                        if (pp.Id == pr.Id) { x = Mathf.Lerp(pp.X, pr.X, alpha); y = Mathf.Lerp(pp.Y, pr.Y, alpha); }
                t.position = WorldSpace.ToWorld(x, y);
            }
            RemoveMissing(_projectiles, t => Destroy(t.gameObject));
        }

        readonly List<int> _remove = new List<int>();

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
            if (!_worms.TryGetValue(wormId, out var v) || !v.Root.gameObject.activeSelf) return false;
            screen = cam.WorldToScreenPoint(v.Root.position + Vector3.up * 1.0f);
            return screen.z > 0;
        }
    }
}
