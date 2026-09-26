using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>Colors of one map theme (docs/PLAN.md §3.9). Everything is procedural, no textures.</summary>
    public sealed class Theme
    {
        public string Name;
        public Color Grass, Dirt, Deep, Rock;
        public Color SkyTop, SkyHorizon, SkyBottom;
        public Color Fog;
        public Color[] HillsNear, HillsFar;
        public Color WaterShallow, WaterDeep;
        public Color SunLight;
        public Color AmbientSky, AmbientEquator, AmbientGround;

        public static readonly Theme Meadow = new Theme
        {
            Name = "meadow",
            Grass = new Color(0.36f, 0.64f, 0.22f), Dirt = new Color(0.56f, 0.38f, 0.22f),
            Deep = new Color(0.28f, 0.19f, 0.12f), Rock = new Color(0.47f, 0.45f, 0.42f),
            SkyTop = new Color(0.22f, 0.47f, 0.85f), SkyHorizon = new Color(0.78f, 0.89f, 0.97f), SkyBottom = new Color(0.55f, 0.72f, 0.82f),
            Fog = new Color(0.72f, 0.84f, 0.93f),
            HillsNear = new[] { new Color(0.30f, 0.52f, 0.28f), new Color(0.22f, 0.40f, 0.24f) },
            HillsFar = new[] { new Color(0.47f, 0.62f, 0.62f), new Color(0.40f, 0.55f, 0.58f) },
            WaterShallow = new Color(0.18f, 0.56f, 0.62f, 0.78f), WaterDeep = new Color(0.05f, 0.18f, 0.32f, 0.92f),
            SunLight = new Color(1f, 0.95f, 0.86f),
            AmbientSky = new Color(0.55f, 0.65f, 0.8f), AmbientEquator = new Color(0.45f, 0.48f, 0.45f), AmbientGround = new Color(0.25f, 0.22f, 0.18f),
        };

        public static readonly Theme Beach = new Theme
        {
            Name = "beach",
            Grass = new Color(0.46f, 0.70f, 0.30f), Dirt = new Color(0.86f, 0.76f, 0.52f),
            Deep = new Color(0.62f, 0.50f, 0.34f), Rock = new Color(0.60f, 0.56f, 0.50f),
            SkyTop = new Color(0.18f, 0.52f, 0.92f), SkyHorizon = new Color(0.86f, 0.94f, 0.98f), SkyBottom = new Color(0.60f, 0.80f, 0.88f),
            Fog = new Color(0.80f, 0.90f, 0.96f),
            HillsNear = new[] { new Color(0.30f, 0.58f, 0.36f), new Color(0.24f, 0.46f, 0.30f) },
            HillsFar = new[] { new Color(0.55f, 0.70f, 0.74f), new Color(0.48f, 0.64f, 0.70f) },
            WaterShallow = new Color(0.20f, 0.70f, 0.72f, 0.75f), WaterDeep = new Color(0.04f, 0.28f, 0.45f, 0.9f),
            SunLight = new Color(1f, 0.97f, 0.9f),
            AmbientSky = new Color(0.6f, 0.7f, 0.85f), AmbientEquator = new Color(0.55f, 0.55f, 0.5f), AmbientGround = new Color(0.35f, 0.3f, 0.22f),
        };

        public static Theme ForSeed(uint seed)
        {
            return (seed & 1) == 0 ? Meadow : Beach;
        }
    }

    public static class Materials
    {
        public static Material Create(string shader, string name)
        {
            var s = Shader.Find(shader);
            if (s == null)
            {
                Debug.LogError("Shader not found: " + shader);
                s = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(s) { name = name };
        }

        public static Material Terrain(Theme t)
        {
            var m = Create("Worms/Terrain", "Terrain");
            m.SetColor("_GrassColor", t.Grass);
            m.SetColor("_DirtColor", t.Dirt);
            m.SetColor("_DeepColor", t.Deep);
            m.SetColor("_RockColor", t.Rock);
            return m;
        }

        public static Material Toon(Color color)
        {
            var m = Create("Worms/Toon", "Toon");
            m.SetColor("_BaseColor", color);
            return m;
        }

        public static Material Water(Theme t)
        {
            var m = Create("Worms/Water", "Water");
            m.SetColor("_ShallowColor", t.WaterShallow);
            m.SetColor("_DeepColor", t.WaterDeep);
            return m;
        }

        public static Material Backdrop(Color top, Color bottom, float height)
        {
            var m = Create("Worms/Backdrop", "Backdrop");
            m.SetColor("_TopColor", top);
            m.SetColor("_BottomColor", bottom);
            m.SetFloat("_Height", height);
            return m;
        }

        public static Material Sky(Theme t, Vector3 sunDirection)
        {
            var m = Create("Worms/Sky", "Sky");
            m.SetColor("_TopColor", t.SkyTop);
            m.SetColor("_HorizonColor", t.SkyHorizon);
            m.SetColor("_BottomColor", t.SkyBottom);
            m.SetVector("_SunDirection", sunDirection);
            return m;
        }
    }
}
