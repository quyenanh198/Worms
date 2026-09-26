using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>
    /// Weapon models built from primitives (no model files, docs/PLAN.md D4).
    /// Each prop points along +X from its grip at the origin; sizes in meters.
    /// </summary>
    public static class WeaponProps
    {
        static Mesh _cyl, _box, _sphere;
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        static Material Mat(Color c)
        {
            string key = ColorUtility.ToHtmlStringRGB(c);
            if (!Mats.TryGetValue(key, out var m)) Mats[key] = m = Materials.Toon(c);
            return m;
        }

        static void Part(Transform parent, Mesh mesh, Color color, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(color);
        }

        public static Transform Build(WeaponId id, Transform parent)
        {
            if (_cyl == null)
            {
                _cyl = MeshUtil.Create(Shapes.Cylinder(0.5f, 1f), "Cylinder");
                _box = MeshUtil.Create(Shapes.Box(1f, 1f, 1f), "Box");
                _sphere = MeshUtil.Create(Shapes.Sphere(0.5f, 8, 12), "Sphere");
            }
            var root = new GameObject("Prop " + id).transform;
            root.SetParent(parent, false);
            var olive = new Color(0.36f, 0.42f, 0.24f);
            var steel = new Color(0.35f, 0.37f, 0.4f);
            var wood = new Color(0.55f, 0.36f, 0.2f);
            var along = new Vector3(0, 0, 90); // cylinders are built along Y; turn them to +X
            switch (id)
            {
                case WeaponId.Bazooka:
                    Part(root, _cyl, olive, new Vector3(0.1f, 0, 0), new Vector3(0.16f, 0.9f, 0.16f), along);
                    Part(root, _cyl, steel, new Vector3(0.55f, 0, 0), new Vector3(0.2f, 0.08f, 0.2f), along);
                    Part(root, _box, steel, new Vector3(0.05f, -0.1f, 0), new Vector3(0.06f, 0.14f, 0.05f), Vector3.zero);
                    break;
                case WeaponId.Grenade:
                    Part(root, _sphere, olive, new Vector3(0.05f, 0, 0), Vector3.one * 0.2f, Vector3.zero);
                    Part(root, _cyl, steel, new Vector3(0.05f, 0.11f, 0), new Vector3(0.06f, 0.05f, 0.06f), Vector3.zero);
                    break;
                case WeaponId.ClusterBomb:
                    Part(root, _sphere, new Color(0.75f, 0.2f, 0.18f), new Vector3(0.05f, 0, 0), Vector3.one * 0.22f, Vector3.zero);
                    Part(root, _cyl, steel, new Vector3(0.05f, 0.12f, 0), new Vector3(0.06f, 0.05f, 0.06f), Vector3.zero);
                    break;
                case WeaponId.Shotgun:
                    Part(root, _cyl, steel, new Vector3(0.3f, 0.02f, 0), new Vector3(0.06f, 0.6f, 0.06f), along);
                    Part(root, _cyl, steel, new Vector3(0.3f, -0.04f, 0), new Vector3(0.06f, 0.6f, 0.06f), along);
                    Part(root, _box, wood, new Vector3(-0.08f, -0.03f, 0), new Vector3(0.25f, 0.1f, 0.07f), new Vector3(0, 0, -10));
                    break;
                case WeaponId.Uzi:
                    Part(root, _box, new Color(0.18f, 0.18f, 0.2f), new Vector3(0.1f, 0, 0), new Vector3(0.28f, 0.1f, 0.07f), Vector3.zero);
                    Part(root, _cyl, steel, new Vector3(0.3f, 0.01f, 0), new Vector3(0.04f, 0.14f, 0.04f), along);
                    Part(root, _box, new Color(0.18f, 0.18f, 0.2f), new Vector3(0.05f, -0.1f, 0), new Vector3(0.05f, 0.16f, 0.05f), Vector3.zero);
                    break;
                case WeaponId.Dynamite:
                    Part(root, _cyl, new Color(0.85f, 0.15f, 0.12f), new Vector3(0.05f, 0, 0), new Vector3(0.1f, 0.32f, 0.1f), Vector3.zero);
                    Part(root, _cyl, new Color(0.9f, 0.85f, 0.7f), new Vector3(0.05f, 0.2f, 0), new Vector3(0.015f, 0.08f, 0.015f), Vector3.zero);
                    break;
                case WeaponId.BaseballBat:
                    Part(root, _cyl, wood, new Vector3(0.3f, 0, 0), new Vector3(0.08f, 0.6f, 0.08f), along);
                    Part(root, _cyl, wood, new Vector3(0.55f, 0, 0), new Vector3(0.12f, 0.2f, 0.12f), along);
                    break;
                case WeaponId.AirStrike:
                    Part(root, _box, new Color(0.25f, 0.3f, 0.25f), new Vector3(0.05f, 0, 0), new Vector3(0.1f, 0.18f, 0.06f), Vector3.zero);
                    Part(root, _cyl, steel, new Vector3(0.08f, 0.17f, 0), new Vector3(0.015f, 0.16f, 0.015f), Vector3.zero);
                    break;
            }
            return root;
        }

        /// <summary>Flying projectile model, pointing along +X.</summary>
        public static Transform BuildProjectile(WeaponId id, bool fragment, Transform parent)
        {
            if (_cyl == null) Build(WeaponId.Bazooka, parent).gameObject.SetActive(false);
            var root = new GameObject("Projectile " + id).transform;
            root.SetParent(parent, false);
            var along = new Vector3(0, 0, 90);
            if (fragment)
            {
                Part(root, _sphere, new Color(0.3f, 0.1f, 0.08f), Vector3.zero, Vector3.one * 0.14f, Vector3.zero);
                return root;
            }
            switch (id)
            {
                case WeaponId.Bazooka:
                case WeaponId.AirStrike:
                    Part(root, _cyl, new Color(0.4f, 0.45f, 0.3f), Vector3.zero, new Vector3(0.12f, 0.36f, 0.12f), along);
                    Part(root, _sphere, new Color(0.8f, 0.2f, 0.15f), new Vector3(0.18f, 0, 0), Vector3.one * 0.12f, Vector3.zero);
                    Part(root, _box, new Color(0.3f, 0.3f, 0.3f), new Vector3(-0.16f, 0, 0), new Vector3(0.08f, 0.2f, 0.02f), Vector3.zero);
                    break;
                case WeaponId.Grenade:
                    Part(root, _sphere, new Color(0.36f, 0.42f, 0.24f), Vector3.zero, Vector3.one * 0.2f, Vector3.zero);
                    break;
                case WeaponId.ClusterBomb:
                    Part(root, _sphere, new Color(0.75f, 0.2f, 0.18f), Vector3.zero, Vector3.one * 0.22f, Vector3.zero);
                    break;
                case WeaponId.Dynamite:
                    Part(root, _cyl, new Color(0.85f, 0.15f, 0.12f), Vector3.zero, new Vector3(0.12f, 0.36f, 0.12f), Vector3.zero);
                    break;
                default:
                    Part(root, _sphere, new Color(0.25f, 0.25f, 0.28f), Vector3.zero, Vector3.one * 0.15f, Vector3.zero);
                    break;
            }
            return root;
        }

        public static Transform BuildGravestone(Transform parent)
        {
            if (_cyl == null) Build(WeaponId.Bazooka, parent).gameObject.SetActive(false);
            var root = new GameObject("Gravestone").transform;
            root.SetParent(parent, false);
            var stone = new Color(0.62f, 0.62f, 0.64f);
            Part(root, _box, stone, new Vector3(0, 0.22f, 0), new Vector3(0.42f, 0.45f, 0.15f), Vector3.zero);
            Part(root, _cyl, stone, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.075f, 0.42f), new Vector3(90, 0, 0));
            Part(root, _box, new Color(0.45f, 0.45f, 0.48f), new Vector3(0, 0.3f, -0.08f), new Vector3(0.05f, 0.22f, 0.02f), Vector3.zero);
            Part(root, _box, new Color(0.45f, 0.45f, 0.48f), new Vector3(0, 0.34f, -0.08f), new Vector3(0.16f, 0.05f, 0.02f), Vector3.zero);
            return root;
        }
    }
}
