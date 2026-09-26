using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;

namespace Worms.Game.Render
{
    /// <summary>
    /// Store cosmetics built from primitives, like the weapons (no model files).
    /// Hats are built around the head center with the head radius as the unit (+Y up the
    /// head, +X the way the worm faces, -Z toward the camera); armor around a spine point
    /// with the body radius as the unit (+Y along the body). The view scales and places them
    /// every frame, so they follow the worm's pose.
    /// </summary>
    public static class CosmeticProps
    {
        static Mesh _sphere, _cyl, _cone, _box;
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        public static readonly Color Gold = new Color(1f, 0.78f, 0.22f);

        static void Meshes()
        {
            if (_sphere != null) return;
            _sphere = MeshUtil.Create(Shapes.Sphere(0.5f, 10, 14), "CosSphere");
            _cyl = MeshUtil.Create(Shapes.Cylinder(0.5f, 1f, 18), "CosCylinder");
            _cone = MeshUtil.Create(Shapes.Cone(0.5f, 1f, 18), "CosCone");
            _box = MeshUtil.Create(Shapes.Box(1f, 1f, 1f), "CosBox");
        }

        static Material Mat(Color c)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c);
            if (!Mats.TryGetValue(key, out var m)) Mats[key] = m = Materials.Toon(c);
            return m;
        }

        static Transform Part(Transform parent, Mesh mesh, Color color, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Transform Root(Transform parent, string name)
        {
            Meshes();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        /// <summary>A hat, or null for "no hat" / unknown ids.</summary>
        public static Transform BuildHat(byte id, Transform parent)
        {
            if (id == 0) return null;
            var root = Root(parent, "Hat " + id);
            switch (id)
            {
                case 1: // Nón lá
                    var straw = new Color(0.93f, 0.83f, 0.55f);
                    Part(root, _cone, straw, new Vector3(0, 0.5f, 0), new Vector3(3.1f, 1.05f, 3.1f));
                    Part(root, _cyl, straw * 0.85f, new Vector3(0, 0.52f, 0), new Vector3(3.14f, 0.05f, 3.14f));
                    break;
                case 2: // Mũ cối
                    var olive = new Color(0.47f, 0.5f, 0.3f);
                    Part(root, _sphere, olive, new Vector3(0, 0.62f, 0), new Vector3(2.1f, 1.35f, 2.1f));
                    Part(root, _cyl, olive * 0.9f, new Vector3(0, 0.42f, 0), new Vector3(2.5f, 0.06f, 2.5f));
                    Part(root, _cyl, new Color(0.85f, 0.12f, 0.12f), new Vector3(0.1f, 0.85f, -0.93f), new Vector3(0.45f, 0.04f, 0.45f), new Vector3(-72f, 0, 0));
                    Part(root, _sphere, new Color(1f, 0.85f, 0.2f), new Vector3(0.1f, 0.86f, -0.97f), new Vector3(0.2f, 0.2f, 0.06f));
                    break;
                case 3: // Băng đô Rambo
                    var red = new Color(0.86f, 0.12f, 0.14f);
                    Part(root, _cyl, red, new Vector3(0, 0.38f, 0), new Vector3(2.12f, 0.3f, 2.12f));
                    Part(root, _box, red, new Vector3(-1.1f, 0.15f, 0.2f), new Vector3(0.14f, 0.7f, 0.08f), new Vector3(0, 0, 50f));
                    Part(root, _box, red, new Vector3(-1.2f, 0.05f, 0.35f), new Vector3(0.14f, 0.6f, 0.08f), new Vector3(0, 0, 70f));
                    break;
                case 4: // Mũ bảo hiểm
                    Part(root, _sphere, new Color(0.88f, 0.16f, 0.16f), new Vector3(0, 0.66f, 0), new Vector3(2.15f, 1.25f, 2.15f));
                    Part(root, _cyl, Color.white, new Vector3(0, 0.66f, 0), new Vector3(2.17f, 0.12f, 2.17f));
                    Part(root, _box, new Color(0.15f, 0.15f, 0.18f), new Vector3(0.95f, 0.68f, 0), new Vector3(0.12f, 0.2f, 1.2f));
                    break;
                case 5: // Mũ cao bồi
                    var leather = new Color(0.58f, 0.38f, 0.2f);
                    Part(root, _cyl, leather, new Vector3(0, 0.75f, 0), new Vector3(3.1f, 0.07f, 3.1f));
                    Part(root, _cyl, leather, new Vector3(0, 1.08f, 0), new Vector3(1.55f, 0.66f, 1.55f));
                    Part(root, _cyl, new Color(0.25f, 0.15f, 0.08f), new Vector3(0, 0.86f, 0), new Vector3(1.58f, 0.14f, 1.58f));
                    break;
                case 6: // Mũ phù thủy
                    var purple = new Color(0.38f, 0.2f, 0.62f);
                    Part(root, _cyl, purple, new Vector3(0, 0.72f, 0), new Vector3(2.7f, 0.06f, 2.7f));
                    var cone = Part(root, _cone, purple, new Vector3(0, 0.74f, 0), new Vector3(1.6f, 1.9f, 1.6f), new Vector3(0, 0, -14f));
                    Part(root, _cyl, new Color(1f, 0.84f, 0.3f), new Vector3(0, 0.84f, 0), new Vector3(1.62f, 0.14f, 1.62f));
                    Part(cone, _sphere, new Color(1f, 0.9f, 0.4f), new Vector3(0.05f, 0.45f, -0.26f), new Vector3(0.14f, 0.12f, 0.05f));
                    break;
                case 7: // Vương miện
                    Part(root, _cyl, Gold, new Vector3(0, 0.95f, 0), new Vector3(1.6f, 0.36f, 1.6f));
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Mathf.PI * 2f / 5f + Mathf.PI / 2f;
                        Part(root, _cone, Gold, new Vector3(Mathf.Cos(a) * 0.72f, 1.12f, -Mathf.Sin(a) * 0.72f), new Vector3(0.34f, 0.42f, 0.34f));
                    }
                    Part(root, _sphere, new Color(0.9f, 0.1f, 0.2f), new Vector3(0, 0.96f, -0.82f), new Vector3(0.26f, 0.26f, 0.1f));
                    Part(root, _sphere, new Color(0.2f, 0.45f, 1f), new Vector3(0.62f, 0.96f, -0.55f), new Vector3(0.18f, 0.18f, 0.08f));
                    Part(root, _sphere, new Color(0.2f, 0.45f, 1f), new Vector3(-0.62f, 0.96f, -0.55f), new Vector3(0.18f, 0.18f, 0.08f));
                    break;
                default:
                    Object.Destroy(root.gameObject);
                    return null;
            }
            return root;
        }

        /// <summary>Which spine point (0 = tail tip, WormRig.Points-1 = head) an armor piece sits on.</summary>
        public static int ArmorSpinePoint(byte id)
        {
            switch (id)
            {
                case 9: return 4;   // belt low on the body
                case 10: return 8;  // cape from the neck
                default: return 6;  // vests and plate on the chest
            }
        }

        /// <summary>Armor, or null for none. A cape's first child is the cloth (the view flutters it).</summary>
        public static Transform BuildArmor(byte id, Transform parent)
        {
            if (id == 0) return null;
            var root = Root(parent, "Armor " + id);
            switch (id)
            {
                case 8: // Áo phao
                    var orange = new Color(1f, 0.5f, 0.12f);
                    Part(root, _sphere, orange, Vector3.zero, new Vector3(2.35f, 2.3f, 2.35f));
                    Part(root, _cyl, new Color(0.95f, 0.95f, 0.9f), new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.14f, 2.4f));
                    Part(root, _cyl, new Color(0.95f, 0.95f, 0.9f), new Vector3(0, -0.35f, 0), new Vector3(2.37f, 0.14f, 2.37f));
                    break;
                case 9: // Đai ninja
                    var black = new Color(0.1f, 0.1f, 0.12f);
                    Part(root, _cyl, black, Vector3.zero, new Vector3(2.16f, 0.4f, 2.16f));
                    Part(root, _sphere, new Color(0.85f, 0.1f, 0.12f), new Vector3(-1.08f, 0, 0), new Vector3(0.45f, 0.4f, 0.4f));
                    Part(root, _box, new Color(0.85f, 0.1f, 0.12f), new Vector3(-1.3f, -0.35f, 0.1f), new Vector3(0.12f, 0.8f, 0.1f), new Vector3(0, 0, 25f));
                    break;
                case 10: // Áo choàng anh hùng
                    var cloth = new GameObject("Cloth").transform;
                    cloth.SetParent(root, false);
                    cloth.localPosition = new Vector3(-0.7f, 0.2f, 0.25f);
                    Part(cloth, _box, new Color(0.85f, 0.1f, 0.16f), new Vector3(0, -1.9f, 0), new Vector3(0.18f, 3.8f, 2.2f));
                    Part(root, _sphere, Gold, new Vector3(0.2f, 0.3f, -0.95f), new Vector3(0.35f, 0.35f, 0.12f));
                    break;
                case 11: // Giáp hiệp sĩ
                    var steel = new Color(0.74f, 0.76f, 0.82f);
                    Part(root, _sphere, steel, Vector3.zero, new Vector3(2.3f, 2.5f, 2.3f));
                    Part(root, _cyl, Gold, new Vector3(0, 0.95f, 0), new Vector3(2.0f, 0.12f, 2.0f));
                    Part(root, _cyl, Gold, new Vector3(0, -0.95f, 0), new Vector3(2.0f, 0.12f, 2.0f));
                    Part(root, _box, Gold, new Vector3(0.1f, 0, -1.12f), new Vector3(0.14f, 1.2f, 0.06f));
                    Part(root, _box, Gold, new Vector3(0.1f, 0.2f, -1.12f), new Vector3(0.6f, 0.14f, 0.06f));
                    break;
                default:
                    Object.Destroy(root.gameObject);
                    return null;
            }
            return root;
        }
    }
}
