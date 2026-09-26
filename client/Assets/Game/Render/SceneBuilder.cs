using UnityEngine;
using UnityEngine.Rendering;
using Worms.Game.Core;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>Builds the static scenery around the playing field: light, sky, fog, distant hills and the sea.</summary>
    public static class SceneBuilder
    {
        public static Light CreateSun(Transform parent, Theme theme)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            // Light comes from above-left and from the camera side, so the front faces are lit.
            go.transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = theme.SunLight;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            RenderSettings.sun = light;
            return light;
        }

        public static void ConfigureEnvironment(Theme theme, Light sun)
        {
            RenderSettings.skybox = Materials.Sky(theme, -sun.transform.forward);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = theme.Fog;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 230f;
        }

        /// <summary>Layers of hills behind the map; the perspective camera gives them parallax.</summary>
        public static void CreateBackdrop(Transform parent, Theme theme, uint seed, float mapWidth, float waterY)
        {
            var rng = new Rng(seed ^ 0xB4CDu);
            var layers = new[]
            {
                (z: 12f, height: 9f, color: theme.HillsNear[0], dark: theme.HillsNear[1]),
                (z: 35f, height: 16f, color: theme.HillsFar[0], dark: theme.HillsFar[1]),
                (z: 80f, height: 28f, color: Color.Lerp(theme.HillsFar[0], theme.Fog, 0.45f), dark: Color.Lerp(theme.HillsFar[1], theme.Fog, 0.45f)),
            };
            foreach (var layer in layers)
            {
                float margin = layer.z * 1.2f + 30f;
                var mesh = HillStrip(rng, -margin, mapWidth + margin, waterY - 2f, layer.height, layer.z);
                var go = new GameObject("Hills z=" + layer.z);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Materials.Backdrop(layer.color, layer.dark, layer.height * 2f);
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static Mesh HillStrip(Rng rng, float x0, float x1, float baseY, float height, float z)
        {
            int n = 160;
            float p1 = rng.Range(0, 6.28f), p2 = rng.Range(0, 6.28f), p3 = rng.Range(0, 6.28f);
            float f1 = rng.Range(0.02f, 0.04f), f2 = rng.Range(0.06f, 0.1f), f3 = rng.Range(0.15f, 0.25f);
            var verts = new Vector3[(n + 1) * 2];
            var tris = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / n);
                float h = height * (0.55f + 0.3f * Mathf.Sin(x * f1 + p1) + 0.12f * Mathf.Sin(x * f2 + p2) + 0.05f * Mathf.Sin(x * f3 + p3));
                verts[i * 2] = new Vector3(x, baseY, z);
                verts[i * 2 + 1] = new Vector3(x, baseY + h, z);
                if (i < n)
                {
                    int a = i * 2, t = i * 6;
                    tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 3;
                    tris[t + 3] = a; tris[t + 4] = a + 3; tris[t + 5] = a + 2;
                }
            }
            var mesh = new Mesh { name = "Hills", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Transform CreateWater(Transform parent, Theme theme, float mapWidth, float waterY)
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, waterY, 0);
            var mesh = MeshUtil.Create(Shapes.Grid(-150f, -30f, mapWidth + 150f, 140f, 220, 60), "Sea");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Materials.Water(theme);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
