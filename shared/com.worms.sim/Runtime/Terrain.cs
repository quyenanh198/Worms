using System;

namespace Worms.Sim
{
    /// <summary>Axis-aligned cell rectangle, max exclusive.</summary>
    public struct CellRect
    {
        public int XMin, YMin, XMax, YMax;

        public CellRect(int xMin, int yMin, int xMax, int yMax)
        {
            XMin = xMin; YMin = yMin; XMax = xMax; YMax = yMax;
        }

        public bool IsEmpty => XMax <= XMin || YMax <= YMin;
    }

    /// <summary>
    /// Destructible terrain as one byte per cell (1 = solid). Cells outside the
    /// map are air. The only way the map changes is <see cref="CarveCircle"/>,
    /// so the seed plus the list of carves reproduces it exactly.
    /// </summary>
    public sealed class Terrain
    {
        readonly byte[] _cells;

        public int Width { get; }
        public int Height { get; }

        public Terrain(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new byte[width * height];
        }

        public bool IsSolid(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return false;
            return _cells[y * Width + x] != 0;
        }

        public void SetSolid(int x, int y, bool solid)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            _cells[y * Width + x] = solid ? (byte)1 : (byte)0;
        }

        public void FillRect(int x0, int y0, int x1, int y1, bool solid)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(Height, y1); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(Width, x1); x++)
                    _cells[y * Width + x] = solid ? (byte)1 : (byte)0;
        }

        /// <summary>Clears every cell whose center is within <paramref name="r"/> of (cx, cy).</summary>
        /// <returns>The rectangle that may have changed (clamped to the map).</returns>
        public CellRect CarveCircle(int cx, int cy, int r)
        {
            var rect = new CellRect(
                Math.Max(0, cx - r), Math.Max(0, cy - r),
                Math.Min(Width, cx + r + 1), Math.Min(Height, cy + r + 1));
            long r2 = (long)r * r;
            for (int y = rect.YMin; y < rect.YMax; y++)
            {
                long dy = y - cy;
                for (int x = rect.XMin; x < rect.XMax; x++)
                {
                    long dx = x - cx;
                    if (dx * dx + dy * dy <= r2) _cells[y * Width + x] = 0;
                }
            }
            return rect;
        }

        /// <summary>True if any solid cell lies within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public bool OverlapsCircle(Vec2 p, float radius)
        {
            int x0 = (int)Math.Floor(p.X - radius), x1 = (int)Math.Ceiling(p.X + radius);
            int y0 = (int)Math.Floor(p.Y - radius), y1 = (int)Math.Ceiling(p.Y + radius);
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - p.Y;
                for (int x = x0; x <= x1; x++)
                {
                    if (!IsSolid(x, y)) continue;
                    float dx = x + 0.5f - p.X;
                    if (dx * dx + dy * dy <= r2) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Direction pointing away from the terrain around <paramref name="p"/>
        /// (sum of offsets from nearby solid cells). Up if nothing is nearby.
        /// </summary>
        public Vec2 NormalAt(Vec2 p, float radius)
        {
            int x0 = (int)Math.Floor(p.X - radius), x1 = (int)Math.Ceiling(p.X + radius);
            int y0 = (int)Math.Floor(p.Y - radius), y1 = (int)Math.Ceiling(p.Y + radius);
            float r2 = radius * radius;
            float sx = 0, sy = 0;
            for (int y = y0; y <= y1; y++)
            {
                float dy = p.Y - (y + 0.5f);
                for (int x = x0; x <= x1; x++)
                {
                    if (!IsSolid(x, y)) continue;
                    float dx = p.X - (x + 0.5f);
                    if (dx * dx + dy * dy <= r2) { sx += dx; sy += dy; }
                }
            }
            var n = new Vec2(sx, sy).Normalized;
            return n.LengthSq > 0 ? n : Vec2.Up;
        }

        public int CountSolid()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++) n += _cells[i];
            return n;
        }

        /// <summary>Copy of the raw cells (row-major, 1 = solid) for renderers.</summary>
        public byte[] CopyCells()
        {
            return (byte[])_cells.Clone();
        }
    }
}
