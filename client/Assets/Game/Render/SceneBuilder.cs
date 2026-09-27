using System.Collections.Generic;
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
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 280f;
        }

        /// <summary>Layers of hills behind the map; the perspective camera gives them parallax.</summary>
        public static void CreateBackdrop(Transform parent, Theme theme, uint seed, float mapWidth, float waterY,
            bool menuComposition = false)
        {
            var rng = new Rng(seed ^ 0xB4CDu);
            if (!menuComposition)
            {
                var layers = new[]
                {
                    (z: 12f, height: 9f, color: theme.HillsNear[0], dark: theme.HillsNear[1], haze: 0.08f),
                    (z: 35f, height: 16f, color: theme.HillsFar[0], dark: theme.HillsFar[1], haze: 0.3f),
                    (z: 80f, height: 28f, color: theme.HillsFar[0], dark: theme.HillsFar[1], haze: 0.58f),
                };
                foreach (var layer in layers)
                {
                    float margin = layer.z * 1.2f + 30f;
                    var mesh = HillStrip(rng, -margin, mapWidth + margin, waterY - 2f, layer.height, layer.z);
                    var go = new GameObject("Hills z=" + layer.z);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = Materials.Backdrop(layer.color, layer.dark, layer.height * 2f, layer.haze);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            CreateDistantIslands(parent, seed, mapWidth, waterY);
            CreateMidgroundIsland(parent, mapWidth, waterY, menuComposition);
            CreateClouds(parent, seed, mapWidth, waterY, menuComposition);
        }

        static void CreateMidgroundIsland(Transform parent, float mapWidth, float waterY, bool menuComposition)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/midground-island");
            if (texture == null) return;
            const float z = 25f;
            float width = menuComposition ? 34f : 48f;
            float height = menuComposition ? 16f : 26f;
            float x = mapWidth * (menuComposition ? 0.65f : 0.5f);
            float baseY = waterY + 4f;
            var mesh = new Mesh
            {
                name = "Midground island",
                vertices = new[]
                {
                    new Vector3(x - width / 2, baseY, z),
                    new Vector3(x - width / 2, baseY + height, z),
                    new Vector3(x + width / 2, baseY + height, z),
                    new Vector3(x + width / 2, baseY, z),
                },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            var go = new GameObject("Midground island");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.BackdropSprite(texture, "Midground island", 0.72f,
                menuComposition ? 0.08f : 0f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void CreateDistantIslands(Transform parent, uint seed, float mapWidth, float waterY)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/distant-island");
            if (texture == null) return;
            var rng = new Rng(seed ^ 0x151Au);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            const float z = 55f;
            for (int i = 0; i < 2; i++)
            {
                float x = mapWidth * (i == 0 ? 0.18f : 0.81f) + rng.Range(-6f, 6f);
                float width = rng.Range(30f, 38f);
                float height = width * 0.5f;
                float baseY = waterY + rng.Range(4f, 7f);
                int first = vertices.Count;
                vertices.Add(new Vector3(x - width / 2, baseY, z));
                vertices.Add(new Vector3(x - width / 2, baseY + height, z));
                vertices.Add(new Vector3(x + width / 2, baseY + height, z));
                vertices.Add(new Vector3(x + width / 2, baseY, z));
                float left = i == 0 ? 0f : 1f;
                float right = 1f - left;
                uvs.Add(new Vector2(left, 0)); uvs.Add(new Vector2(left, 1));
                uvs.Add(new Vector2(right, 1)); uvs.Add(new Vector2(right, 0));
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            var mesh = new Mesh { name = "Distant islands", vertices = vertices.ToArray(), uv = uvs.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateBounds();
            var go = new GameObject("Distant islands");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.BackdropSprite(texture, "Distant islands", 0.8f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void CreateClouds(Transform parent, uint seed, float mapWidth, float waterY, bool menuComposition)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/cloud-bank");
            if (texture == null) return;
            var rng = new Rng(seed ^ 0xC10Du);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            const float z = 105f;
            for (int i = 0; i < 5; i++)
            {
                float x = mapWidth * (i - 1f) * 0.5f + rng.Range(-7f, 7f);
                float y = waterY + (menuComposition ? rng.Range(28f, 36f) : rng.Range(39f, 49f));
                float width = rng.Range(29f, 40f);
                float height = width / 3f;
                int first = vertices.Count;
                vertices.Add(new Vector3(x - width / 2, y - height / 2, z));
                vertices.Add(new Vector3(x - width / 2, y + height / 2, z));
                vertices.Add(new Vector3(x + width / 2, y + height / 2, z));
                vertices.Add(new Vector3(x + width / 2, y - height / 2, z));
                float left = i % 2 == 0 ? 0f : 1f;
                float right = 1f - left;
                uvs.Add(new Vector2(left, 0)); uvs.Add(new Vector2(left, 1));
                uvs.Add(new Vector2(right, 1)); uvs.Add(new Vector2(right, 0));
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            var mesh = new Mesh { name = "Cloud banks", vertices = vertices.ToArray(), uv = uvs.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateBounds();
            var go = new GameObject("Cloud banks");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.BackdropSprite(texture, "Cloud banks", 0.88f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Painted scenery behind the play plane. It never changes collision or blocks actors.</summary>
        public static void CreateSurfaceProps(Transform parent, Worms.Sim.Terrain terrain)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/foreground-oak");
            if (texture == null)
            {
                CreateCrateProps(parent, terrain);
                return;
            }
            float[] fractions = { 0.25f, 0.45f, 0.65f, 0.82f };
            float[] widths = { 5.8f, 7.5f, 5.2f, 6f };
            for (int i = 0; i < fractions.Length; i++)
            {
                int cellX = Mathf.Clamp(Mathf.RoundToInt((terrain.Width - 1) * fractions[i]), 0, terrain.Width - 1);
                int top = 0;
                while (top < terrain.Height && !terrain.IsSolid(cellX, top)) top++;
                if (top >= terrain.Height - 8) continue;

                float x = cellX * WorldSpace.Scale;
                float width = widths[i];
                // The source cutout has an earth plinth in its bottom quarter. Hide it
                // inside the real terrain so trees remain grounded on sloped maps.
                const float cropBottom = 0.25f;
                bool flip = i == 1; // keep the fence on the uphill side of the left tree
                float uvLeft = flip ? 1f : 0f, uvRight = 1f - uvLeft;
                float height = width * texture.height / texture.width * (1f - cropBottom);
                float bottom = -top * WorldSpace.Scale - 0.3f;
                const float z = 1.2f;
                var mesh = new Mesh
                {
                    name = "Painted oak",
                    vertices = new[]
                    {
                        new Vector3(x - width / 2, bottom, z),
                        new Vector3(x - width / 2, bottom + height, z),
                        new Vector3(x + width / 2, bottom + height, z),
                        new Vector3(x + width / 2, bottom, z),
                    },
                    uv = new[] { new Vector2(uvLeft, cropBottom), new Vector2(uvLeft, 1),
                        new Vector2(uvRight, 1), new Vector2(uvRight, cropBottom) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject("Painted oak " + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.BackdropSprite(texture, "Painted oak", 0.92f);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            CreateCrateProps(parent, terrain);
        }

        static void CreateCrateProps(Transform parent, Worms.Sim.Terrain terrain)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/crate-rocks");
            if (texture == null) return;
            float[] fractions = { 0.14f, 0.56f, 0.92f };
            var material = Materials.BackdropSprite(texture, "Crate and rocks", 1f, alphaThreshold: 0.25f);
            // Crop the generator's transparent padding and keep the props below worm height.
            const float u0 = 112f / 1536f, u1 = 1452f / 1536f;
            const float v0 = 141f / 1024f, v1 = 895f / 1024f;
            const float width = 2.9f, height = 1.65f;
            for (int i = 0; i < fractions.Length; i++)
            {
                int cellX = Mathf.RoundToInt((terrain.Width - 1) * fractions[i]);
                int top = 0;
                while (top < terrain.Height && !terrain.IsSolid(cellX, top)) top++;
                if (top >= terrain.Height - 8) continue;
                float x = cellX * WorldSpace.Scale, bottom = -top * WorldSpace.Scale - 0.12f;
                var mesh = new Mesh
                {
                    name = "Painted crate and rocks",
                    vertices = new[]
                    {
                        new Vector3(x - width / 2, bottom, -0.12f),
                        new Vector3(x - width / 2, bottom + height, -0.12f),
                        new Vector3(x + width / 2, bottom + height, -0.12f),
                        new Vector3(x + width / 2, bottom, -0.12f),
                    },
                    uv = new[] { new Vector2(u0, v0), new Vector2(u0, v1),
                        new Vector2(u1, v1), new Vector2(u1, v0) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject("Crate and rocks " + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static Mesh HillStrip(Rng rng, float x0, float x1, float baseY, float height, float z)
        {
            int n = 160;
            float p1 = rng.Range(0, 6.28f), p2 = rng.Range(0, 6.28f), p3 = rng.Range(0, 6.28f);
            float f1 = rng.Range(0.02f, 0.04f), f2 = rng.Range(0.06f, 0.1f), f3 = rng.Range(0.15f, 0.25f);
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var tris = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / n);
                float h = height * (0.55f + 0.3f * Mathf.Sin(x * f1 + p1) + 0.12f * Mathf.Sin(x * f2 + p2) + 0.05f * Mathf.Sin(x * f3 + p3));
                verts[i * 2] = new Vector3(x, baseY, z);
                verts[i * 2 + 1] = new Vector3(x, baseY + h, z);
                uvs[i * 2] = new Vector2(0, 0);
                uvs[i * 2 + 1] = new Vector2(0, 1);
                if (i < n)
                {
                    int a = i * 2, t = i * 6;
                    tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 3;
                    tris[t + 3] = a; tris[t + 4] = a + 3; tris[t + 5] = a + 2;
                }
            }
            var mesh = new Mesh { name = "Hills", vertices = verts, uv = uvs, triangles = tris };
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
