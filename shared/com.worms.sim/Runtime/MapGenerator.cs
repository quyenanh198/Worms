using System;

namespace Worms.Sim
{
    /// <summary>Procedural island map from a seed (docs/PLAN.md §3.9).</summary>
    public static class MapGenerator
    {
        public static Terrain Generate(uint seed, int width = C.MapWidth, int height = C.MapHeight)
        {
            var rng = new Rng(seed);
            var t = new Terrain(width, height);
            float twoPi = (float)(Math.PI * 2);

            float baseY = height * rng.Range(0.42f, 0.52f);
            float a1 = rng.Range(80f, 140f), f1 = twoPi / width * rng.Range(1f, 2f), p1 = rng.Range(0f, twoPi);
            float a2 = rng.Range(30f, 70f), f2 = twoPi / width * rng.Range(3f, 5f), p2 = rng.Range(0f, twoPi);
            float a3 = rng.Range(8f, 20f), f3 = twoPi / width * rng.Range(9f, 14f), p3 = rng.Range(0f, twoPi);
            int margin = 60;

            for (int x = 0; x < width; x++)
            {
                float surface = baseY + a1 * (float)Math.Sin(x * f1 + p1) + a2 * (float)Math.Sin(x * f2 + p2) + a3 * (float)Math.Sin(x * f3 + p3);
                // Taper both edges down into the sea so the map reads as islands.
                float edge = Math.Min(x, width - 1 - x);
                if (edge < margin * 3) surface += (margin * 3 - edge) * 1.6f;
                int top = Math.Max(120, (int)surface);
                t.FillRect(x, top, x + 1, height, true);
            }

            // One or two sea channels split the land into islands.
            int channels = rng.Range(1, 3);
            for (int i = 0; i < channels; i++)
            {
                int cx = rng.Range(width / 4, width * 3 / 4);
                int w = rng.Range(50, 110);
                t.FillRect(cx - w / 2, 0, cx + w / 2, height, false);
            }

            // Caves.
            int caves = rng.Range(5, 10);
            for (int i = 0; i < caves; i++)
            {
                int cx = rng.Range(150, width - 150);
                int cy = rng.Range((int)baseY + 60, height - 140);
                t.CarveCircle(cx, cy, rng.Range(22, 55));
            }
            return t;
        }
    }
}
