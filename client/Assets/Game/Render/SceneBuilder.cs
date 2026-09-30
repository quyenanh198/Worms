using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>Builds the static scenery around the playing field: light, sky, fog, distant hills and the sea.</summary>
    public static class SceneBuilder
    {
        public static void RefreshSurfaceProps(Transform parent, Worms.Sim.Terrain terrain)
        {
            foreach (var anchor in parent.GetComponentsInChildren<SurfacePropAnchor>(true))
                anchor.Refresh(terrain);
            foreach (var anchor in parent.GetComponentsInChildren<FaceRockAnchor>(true))
                anchor.Refresh(terrain);
        }

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
                    // Keep the nearest procedural ridge below the painted island. A tall,
                    // flat strip used to cover its cliff detail in the battle overview.
                    (z: 12f, height: 4.5f, color: theme.HillsNear[0], dark: theme.HillsNear[1], haze: 0.24f),
                    // Keep this low: the painted coastal range and citadel sit just
                    // behind it and need their cliff faces visible above the water.
                    (z: 35f, height: 6.5f, color: theme.HillsFar[0], dark: theme.HillsFar[1], haze: 0.38f),
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
            if (!menuComposition)
            {
                CreateCoastalRange(parent, mapWidth, waterY);
                CreateCoastalCitadel(parent, mapWidth, waterY);
            }
            CreateMidgroundIsland(parent, mapWidth, waterY, menuComposition);
            CreateClouds(parent, seed, mapWidth, waterY, menuComposition);
        }

        static void CreateCoastalRange(Transform parent, float mapWidth, float waterY)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/coastal-range-left");
            if (texture == null) return;
            const float z = 48f;
            float width = 38f;
            float height = width * texture.height / texture.width;
            // Perspective pulls distant scenery toward the screen center.
            // Place this near the map edge to frame the left side of the bay.
            float x = mapWidth * 0.06f;
            float bottom = waterY + 2f;
            var mesh = new Mesh
            {
                name = "Coastal range left",
                vertices = new[]
                {
                    new Vector3(x - width / 2f, bottom, z),
                    new Vector3(x - width / 2f, bottom + height, z),
                    new Vector3(x + width / 2f, bottom + height, z),
                    new Vector3(x + width / 2f, bottom, z),
                },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            var go = new GameObject("Coastal range left");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.BackdropSprite(texture, "Coastal range left", 0.72f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void CreateCoastalCitadel(Transform parent, float mapWidth, float waterY)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/coastal-citadel");
            if (texture == null) return;
            const float z = 40f;
            float width = 40f;
            float height = width * texture.height / texture.width;
            float x = mapWidth * 0.96f;
            float bottom = waterY + 2f;
            var mesh = new Mesh
            {
                name = "Coastal citadel",
                vertices = new[]
                {
                    new Vector3(x - width / 2f, bottom, z),
                    new Vector3(x - width / 2f, bottom + height, z),
                    new Vector3(x + width / 2f, bottom + height, z),
                    new Vector3(x + width / 2f, bottom, z),
                },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            var go = new GameObject("Coastal citadel");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.BackdropSprite(texture, "Coastal citadel", 0.82f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
            for (int i = 0; i < 7; i++)
            {
                float x = mapWidth * (i - 2f) * 0.36f + rng.Range(-7f, 7f);
                float y = waterY + (menuComposition ? rng.Range(28f, 36f) : rng.Range(28f, 39f));
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
        public static void CreateSurfaceProps(Transform parent, Worms.Sim.Terrain terrain, uint seed)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/foreground-oak");
            if (texture == null) return;
            var rng = new Rng(seed ^ 0xD6E8FEB9u);
            float[] widths = { 5.8f, 7.5f, 5.2f, 6f };
            for (int i = 0; i < widths.Length; i++)
            {
                float fraction = (i + rng.Range(0.30f, 0.75f)) / widths.Length;
                int cellX = Mathf.Clamp(Mathf.RoundToInt((terrain.Width - 1) * fraction), 0, terrain.Width - 1);
                int top = 0;
                while (top < terrain.Height && !terrain.IsSolid(cellX, top)) top++;
                if (top >= terrain.Height - 8) continue;

                float x = cellX * WorldSpace.Scale;
                float width = widths[i];
                // The source cutout has an earth plinth in its bottom quarter. Hide it
                // inside the real terrain so trees remain grounded on sloped maps.
                const float cropBottom = 0.25f;
                float uvLeft = 0f, uvRight = 1f;
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
                var anchor = go.AddComponent<SurfacePropAnchor>();
                anchor.CellX = cellX;
                anchor.OriginalTop = top;
                anchor.MaxDropCells = 50;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.BackdropSprite(texture, "Painted oak", 0.92f);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Concept-style grass fringe layered over stable cliff tops.</summary>
        public static void CreateGrassLip(Transform parent, Worms.Sim.Terrain terrain, uint seed = 0)
        {
            var art = new[]
            {
                Resources.Load<Texture2D>("Terrain/grass-lip-a"),
                Resources.Load<Texture2D>("Terrain/grass-lip-b"),
            };
            if (art[0] == null || art[1] == null) return;
            var materials = new[]
            {
                Materials.BackdropSprite(art[0], "Grass lip A", 1f, alphaThreshold: 0.08f),
                Materials.BackdropSprite(art[1], "Grass lip B", 1f, alphaThreshold: 0.08f),
            };
            var rng = new Rng(seed ^ 0x6C8E9CF5u);
            const int spacing = 80, halfSupport = 40;
            for (int slot = 0, origin = 40; origin < terrain.Width - 40; slot++, origin += spacing)
            {
                int x = Mathf.Clamp(origin + rng.Range(-13, 14), halfSupport,
                    terrain.Width - halfSupport - 1);
                int top = SurfaceTop(terrain, x);
                if (top >= terrain.Height - 8) continue;
                bool stable = true;
                for (int dx = -halfSupport; dx <= halfSupport; dx += 8)
                    if (Mathf.Abs(SurfaceTop(terrain, x + dx) - top) > 8)
                    {
                        stable = false;
                        break;
                    }
                if (!stable) continue;

                int variant = rng.Range(0, 2);
                float width = 4.8f;
                float height = variant == 0 ? 0.75f : 0.84f;
                float center = x * WorldSpace.Scale;
                float bottom = -top * WorldSpace.Scale - 0.54f;
                // UVs trim transparent canvas padding; the painted blades keep
                // their original orientation and aspect ratio.
                float u0 = variant == 0 ? 0.015f : 0.012f;
                float u1 = variant == 0 ? 0.985f : 0.988f;
                float v0 = variant == 0 ? 0.250f : 0.225f;
                float v1 = variant == 0 ? 0.712f : 0.745f;
                var mesh = new Mesh
                {
                    name = "Grass lip " + slot,
                    vertices = new[]
                    {
                        new Vector3(center - width * 0.5f, bottom, -0.08f),
                        new Vector3(center - width * 0.5f, bottom + height, -0.08f),
                        new Vector3(center + width * 0.5f, bottom + height, -0.08f),
                        new Vector3(center + width * 0.5f, bottom, -0.08f),
                    },
                    uv = new[] { new Vector2(u0, v0), new Vector2(u0, v1),
                        new Vector2(u1, v1), new Vector2(u1, v0) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                var anchor = go.AddComponent<SurfacePropAnchor>();
                anchor.CellX = x;
                anchor.OriginalTop = top;
                anchor.HalfWidthCells = halfSupport;
                anchor.MaxDropCells = 16;
                anchor.RequireContinuousGround = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials[variant];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Painted grass and stones on stable shelves, behind the actors.</summary>
        public static void CreateGroundFoliage(Transform parent, Worms.Sim.Terrain terrain,
            uint seed = 0, bool menuComposition = false)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/grass-rock-clump");
            var bankTexture = Resources.Load<Texture2D>("Backdrop/rocky-grass-bank");
            if (texture == null && bankTexture == null) return;
            var rng = new Rng(seed ^ 0xA1B2C3D4u);
            float[] positions = menuComposition
                ? new[] { rng.Range(0.10f, 0.20f), rng.Range(0.77f, 0.89f) }
                : new float[8];
            if (!menuComposition)
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = (i + rng.Range(0.22f, 0.78f)) / positions.Length;
            float[] widths = menuComposition
                ? new[] { 2.0f, 2.0f }
                : new[] { 3.2f, 4.1f, 3.6f, 4.3f, 3.4f, 4.0f, 3.7f, 4.2f };
            var material = texture != null
                ? Materials.BackdropSprite(texture, "Grass and stones", 0.94f, alphaThreshold: 0.08f)
                : null;
            var bankMaterial = bankTexture != null
                ? Materials.BackdropSprite(bankTexture, "Rocky grass bank", 0.94f, alphaThreshold: 0.08f)
                : null;
            const float z = 1.1f;
            int[] offsets = { 0, -20, 20, -40, 40, -60, 60 };
            var occupied = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                bool rocky = bankTexture != null && (texture == null ||
                    !menuComposition && rng.Range(0, 3) == 0);
                float width = widths[i], height = width * (rocky ? 0.26f : 1.25f / 3.8f);
                int halfCells = Mathf.RoundToInt(width * (rocky ? 0.45f : 0.35f) / WorldSpace.Scale);
                int origin = Mathf.RoundToInt((terrain.Width - 1) * positions[i]);
                int cellX = -1, top = terrain.Height;
                foreach (int offset in offsets)
                {
                    int candidate = origin + offset;
                    if (candidate - halfCells < 0 || candidate + halfCells >= terrain.Width) continue;
                    bool overlaps = false;
                    foreach (int placed in occupied)
                        if (Mathf.Abs(placed - candidate) < halfCells * 2) overlaps = true;
                    if (overlaps) continue;
                    int surface = SurfaceTop(terrain, candidate);
                    if (surface >= terrain.Height - 8 ||
                        Mathf.Abs(SurfaceTop(terrain, candidate - halfCells) - surface) > 5 ||
                        Mathf.Abs(SurfaceTop(terrain, candidate + halfCells) - surface) > 5) continue;
                    cellX = candidate;
                    top = surface;
                    break;
                }
                if (cellX < 0) continue;
                occupied.Add(cellX);

                float x = cellX * WorldSpace.Scale;
                float bottom = -top * WorldSpace.Scale - 0.08f;
                float u0 = 0f, u1 = 1f;
                // Each cutout has different transparent padding around its baseline.
                float vBottom = rocky ? 0.11f : 0.07f;
                float vTop = rocky ? 0.84f : 0.64f;
                var mesh = new Mesh
                {
                    name = rocky ? "Rocky grass bank" : "Grass and stones",
                    vertices = new[]
                    {
                        new Vector3(x - width / 2f, bottom, z),
                        new Vector3(x - width / 2f, bottom + height, z),
                        new Vector3(x + width / 2f, bottom + height, z),
                        new Vector3(x + width / 2f, bottom, z),
                    },
                    uv = new[] { new Vector2(u0, vBottom), new Vector2(u0, vTop),
                        new Vector2(u1, vTop), new Vector2(u1, vBottom) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject((rocky ? "Rocky grass bank " : "Grass and stones ") + i);
                go.transform.SetParent(parent, false);
                var anchor = go.AddComponent<SurfacePropAnchor>();
                anchor.CellX = cellX;
                anchor.OriginalTop = top;
                anchor.MaxDropCells = 30;
                anchor.HalfWidthCells = halfCells;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rocky ? bankMaterial : material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Painted stones embedded inside broad dirt faces, without collision.</summary>
        public static void CreateCliffRocks(Transform parent, Worms.Sim.Terrain terrain, uint seed = 0)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/cliff-rock-inlay");
            if (texture == null) return;
            var material = Materials.BackdropSprite(texture, "Cliff rock inlay", 0.86f);
            var granite = Resources.Load<Texture2D>("Terrain/granite-boulder");
            var sandstone = Resources.Load<Texture2D>("Terrain/sandstone-boulder");
            var basalt = Resources.Load<Texture2D>("Terrain/basalt-boulder");
            var graniteMaterial = granite != null ? Materials.BackdropSprite(granite, "Granite face rock", 0.95f, alphaThreshold: 0.08f) : material;
            var sandstoneMaterial = sandstone != null ? Materials.BackdropSprite(sandstone, "Sandstone face rock", 0.95f, alphaThreshold: 0.25f) : material;
            var basaltMaterial = basalt != null ? Materials.BackdropSprite(basalt, "Basalt face rock", 0.95f, alphaThreshold: 0.08f) : material;
            var rng = new Rng(seed ^ 0x8D12E93Bu);
            int[] offsets = { 0, -48, 48, -96, 96 };
            const int count = 6;
            for (int i = 0; i < count; i++)
            {
                int rockKind = rng.Range(0, 4);
                float width = rng.Range(4.1f, 5.9f);
                float height = width * (rockKind == 1 ? 0.82f : rockKind == 2 ? 0.62f : rockKind == 3 ? 0.67f : 3.1f / 5.2f);
                // The visible art spans roughly 74% by 58% of the PNG; do not
                // reject a face because its transparent padding crosses a rim.
                int halfX = Mathf.CeilToInt(width * 0.37f / WorldSpace.Scale);
                int halfY = Mathf.CeilToInt(height * 0.29f / WorldSpace.Scale);
                int origin = Mathf.RoundToInt((terrain.Width - 1) *
                    (i + rng.Range(0.22f, 0.78f)) / count);
                float depth = rng.Range(2.8f, 4.5f);
                int cellX = -1, cellY = -1;
                foreach (int offset in offsets)
                {
                    int x = origin + offset;
                    if (x - halfX < 0 || x + halfX >= terrain.Width) continue;
                    int y = SurfaceTop(terrain, x) + Mathf.CeilToInt(depth / WorldSpace.Scale);
                    if (y - halfY < 0 || y + halfY >= terrain.Height) continue;
                    if (!terrain.IsSolid(x, y) || !terrain.IsSolid(x - halfX, y) ||
                        !terrain.IsSolid(x + halfX, y) || !terrain.IsSolid(x, y - halfY) ||
                        !terrain.IsSolid(x, y + halfY)) continue;
                    cellX = x;
                    cellY = y;
                    break;
                }
                if (cellX < 0) continue;
                float cx = cellX * WorldSpace.Scale, cy = -cellY * WorldSpace.Scale;
                float leftUv = 0f, rightUv = 1f;
                var mesh = new Mesh
                {
                    name = "Cliff rock inlay",
                    vertices = new[]
                    {
                        new Vector3(cx - width / 2f, cy - height / 2f, -0.14f),
                        new Vector3(cx - width / 2f, cy + height / 2f, -0.14f),
                        new Vector3(cx + width / 2f, cy + height / 2f, -0.14f),
                        new Vector3(cx + width / 2f, cy - height / 2f, -0.14f),
                    },
                    uv = new[] { new Vector2(leftUv, 0), new Vector2(leftUv, 1),
                        new Vector2(rightUv, 1), new Vector2(rightUv, 0) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject("Cliff rock " + i);
                go.transform.SetParent(parent, false);
                var anchor = go.AddComponent<FaceRockAnchor>();
                anchor.CellX = cellX;
                anchor.CellY = cellY;
                anchor.HalfWidthCells = halfX;
                anchor.HalfHeightCells = halfY;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rockKind == 1 ? graniteMaterial : rockKind == 2 ? sandstoneMaterial : rockKind == 3 ? basaltMaterial : material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static int SurfaceTop(Worms.Sim.Terrain terrain, int x)
        {
            int top = 0;
            while (top < terrain.Height && !terrain.IsSolid(x, top)) top++;
            return top;
        }

        public static void CreateCrateProps(Transform parent, Worms.Sim.Terrain terrain,
            IReadOnlyList<WormSnap> worms, uint seed)
        {
            var texture = Resources.Load<Texture2D>("Backdrop/crate-rocks");
            if (texture == null) return;
            var rng = new Rng(seed ^ 0xC6D5A4B3u);
            const int count = 5;
            var material = Materials.BackdropSprite(texture, "Crate and rocks", 1f, alphaThreshold: 0.25f);
            // Crop the generator's transparent padding and keep the props below worm height.
            const float u0 = 112f / 1536f, u1 = 1452f / 1536f;
            const float v0 = 141f / 1024f, v1 = 895f / 1024f;
            for (int i = 0; i < count; i++)
            {
                float width = rng.Range(2.35f, 2.9f);
                float height = width * (1.65f / 2.9f);
                int originX = Mathf.RoundToInt((terrain.Width - 1) *
                    (i + rng.Range(0.18f, 0.82f)) / count);
                int cellX = originX, top = terrain.Height;
                int[] offsets = { 0, -48, 48, -96, 96 };
                bool found = false;
                foreach (int offset in offsets)
                {
                    int candidate = Mathf.Clamp(originX + offset, 0, terrain.Width - 1);
                    int surface = 0;
                    while (surface < terrain.Height && !terrain.IsSolid(candidate, surface)) surface++;
                    if (surface >= terrain.Height - 8) continue;
                    float candidateX = candidate * WorldSpace.Scale;
                    float y = -surface * WorldSpace.Scale;
                    bool nearWorm = false;
                    foreach (var worm in worms)
                    {
                        if (!worm.Alive) continue;
                        if (Mathf.Abs(worm.X * WorldSpace.Scale - candidateX) < width * 0.5f + 0.8f &&
                            Mathf.Abs(-worm.Y * WorldSpace.Scale - y) < 2.3f)
                        {
                            nearWorm = true;
                            break;
                        }
                    }
                    if (nearWorm) continue;
                    cellX = candidate;
                    top = surface;
                    found = true;
                    break;
                }
                if (!found) continue;
                float x = cellX * WorldSpace.Scale, bottom = -top * WorldSpace.Scale - 0.12f;
                float leftUv = u0, rightUv = u1;
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
                    uv = new[] { new Vector2(leftUv, v0), new Vector2(leftUv, v1),
                        new Vector2(rightUv, v1), new Vector2(rightUv, v0) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                mesh.RecalculateBounds();
                var go = new GameObject("Crate and rocks " + i);
                go.transform.SetParent(parent, false);
                var anchor = go.AddComponent<SurfacePropAnchor>();
                anchor.CellX = cellX;
                anchor.OriginalTop = top;
                anchor.MaxDropCells = 35;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = -10;
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
