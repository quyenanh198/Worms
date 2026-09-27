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
        public Color WaterFoam;
        public Color SunLight;
        public Color AmbientSky, AmbientEquator, AmbientGround;

        public static readonly Theme Meadow = new Theme
        {
            Name = "meadow",
            Grass = new Color(0.38f, 0.63f, 0.16f), Dirt = new Color(0.63f, 0.37f, 0.19f),
            Deep = new Color(0.34f, 0.19f, 0.12f), Rock = new Color(0.50f, 0.48f, 0.44f),
            SkyTop = new Color(0.11f, 0.43f, 0.89f), SkyHorizon = new Color(0.70f, 0.88f, 0.98f), SkyBottom = new Color(0.54f, 0.77f, 0.92f),
            Fog = new Color(0.69f, 0.83f, 0.93f),
            HillsNear = new[] { new Color(0.30f, 0.57f, 0.49f), new Color(0.19f, 0.43f, 0.43f) },
            HillsFar = new[] { new Color(0.51f, 0.69f, 0.79f), new Color(0.35f, 0.56f, 0.69f) },
            WaterShallow = new Color(0.16f, 0.64f, 0.84f, 0.80f), WaterDeep = new Color(0.04f, 0.29f, 0.58f, 0.94f), WaterFoam = new Color(0.88f, 0.97f, 1f),
            SunLight = new Color(1f, 0.96f, 0.86f),
            AmbientSky = new Color(0.57f, 0.70f, 0.85f), AmbientEquator = new Color(0.49f, 0.54f, 0.49f), AmbientGround = new Color(0.28f, 0.24f, 0.20f),
        };

        public static readonly Theme Beach = new Theme
        {
            Name = "beach",
            Grass = new Color(0.50f, 0.68f, 0.23f), Dirt = new Color(0.73f, 0.49f, 0.27f),
            Deep = new Color(0.47f, 0.29f, 0.17f), Rock = new Color(0.59f, 0.55f, 0.48f),
            SkyTop = new Color(0.11f, 0.48f, 0.91f), SkyHorizon = new Color(0.74f, 0.92f, 0.99f), SkyBottom = new Color(0.59f, 0.82f, 0.94f),
            Fog = new Color(0.76f, 0.89f, 0.95f),
            HillsNear = new[] { new Color(0.41f, 0.64f, 0.53f), new Color(0.27f, 0.51f, 0.46f) },
            HillsFar = new[] { new Color(0.59f, 0.76f, 0.82f), new Color(0.42f, 0.64f, 0.74f) },
            WaterShallow = new Color(0.22f, 0.75f, 0.87f, 0.78f), WaterDeep = new Color(0.04f, 0.36f, 0.65f, 0.92f), WaterFoam = new Color(0.94f, 0.99f, 1f),
            SunLight = new Color(1f, 0.97f, 0.88f),
            AmbientSky = new Color(0.62f, 0.75f, 0.88f), AmbientEquator = new Color(0.57f, 0.58f, 0.52f), AmbientGround = new Color(0.39f, 0.31f, 0.24f),
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
            var paintedSoil = Resources.Load<Texture2D>("Terrain/painted-soil");
            if (paintedSoil != null) m.SetTexture("_PaintedSoil", paintedSoil);
            m.SetFloat("_PaintStrength", paintedSoil != null ? 0.65f : 0f);
            return m;
        }

        public static Material Toon(Color color, bool segments = false)
        {
            var m = Create("Worms/Toon", "Toon");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Segments", segments ? 1f : 0f);
            return m;
        }

        public static Material Water(Theme t)
        {
            var m = Create("Worms/Water", "Water");
            m.SetColor("_ShallowColor", t.WaterShallow);
            m.SetColor("_DeepColor", t.WaterDeep);
            m.SetColor("_FoamColor", t.WaterFoam);
            return m;
        }

        public static Material Backdrop(Color top, Color bottom, float height, float haze)
        {
            var m = Create("Worms/Backdrop", "Backdrop");
            m.SetColor("_TopColor", top);
            m.SetColor("_BottomColor", bottom);
            m.SetFloat("_Height", height);
            m.SetFloat("_Haze", haze);
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

        public static Material BackdropSprite(Texture texture, string name, float opacity, float edgeFade = 0f,
            float alphaThreshold = 0f)
        {
            var m = Create("Worms/BackdropSprite", name);
            m.SetTexture("_MainTex", texture);
            m.SetFloat("_Opacity", opacity);
            m.SetFloat("_EdgeFade", edgeFade);
            m.SetFloat("_AlphaThreshold", alphaThreshold);
            return m;
        }
    }
}
