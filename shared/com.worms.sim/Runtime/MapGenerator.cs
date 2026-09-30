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
            // The approved map is the composition guide: separate high shoulders,
            // staggered lower ledges, and an open dry basin. Each side gets its
            // own positions and heights; the sprite layers never define geometry.
            float leftHigh = height * rng.Range(.34f, .41f);
            float leftMid = leftHigh + rng.Range(110f, 160f);
            float basin = height * rng.Range(.69f, .75f);
            float rightMid = basin - rng.Range(110f, 160f);
            float rightHigh = rightMid - rng.Range(95f, 145f);
            float[] px = {
                0f, width * .075f,
                width * rng.Range(.200f, .205f), width * rng.Range(.215f, .220f),
                width * rng.Range(.30f, .33f), width * rng.Range(.410f, .415f),
                width * rng.Range(.425f, .430f), width * rng.Range(.615f, .620f),
                width * rng.Range(.625f, .630f), width * rng.Range(.785f, .790f),
                width * rng.Range(.795f, .800f), width * rng.Range(.89f, .91f),
                width * .97f, width - 1f
            };
            float[] py = {
                leftHigh + 220f, leftHigh, leftHigh, leftMid,
                leftMid, leftMid, basin, basin,
                rightMid, rightMid, rightHigh, rightHigh,
                rightHigh + 105f, rightHigh + 220f
            };
            var surfaceTops = new int[width];
            int segment = 0;
            for (int x = 0; x < width; x++)
            {
                while (segment < px.Length - 2 && x > px[segment + 1]) segment++;
                float u = (x - px[segment]) / (px[segment + 1] - px[segment]);
                float smooth = u * u * (3f - 2f * u);
                float ripple = 2.2f * (float)Math.Sin(x * .057f + seed * .011f);
                surfaceTops[x] = Math.Max(120, (int)(py[segment] +
                    (py[segment + 1] - py[segment]) * smooth + ripple));
            }
            for (int x = 0; x < width; x++)
                t.FillRect(x, surfaceTops[x], x + 1, height, true);

            // Narrow sea channels at the outer quarters reveal the backdrop while
            // leaving the central battle basin intact.
            int channels = rng.Range(0, 2);
            for (int i = 0; i < channels; i++)
            {
                bool left = (rng.NextUInt() & 1u) == 0u;
                int cx = left ? rng.Range(width / 8, width / 4)
                              : rng.Range(width * 3 / 4, width * 7 / 8);
                int w = rng.Range(50, 110);
                t.FillRect(cx - w / 2, 0, cx + w / 2, height, false);
            }

            // Caves.
            int caves = rng.Range(5, 10);
            for (int i = 0; i < caves; i++)
            {
                int cx = rng.Range(150, width - 150);
                int cy = rng.Range((int)(height * .52f), height - 140);
                t.CarveCircle(cx, cy, rng.Range(22, 55));
            }
            return t;
        }
    }
}
