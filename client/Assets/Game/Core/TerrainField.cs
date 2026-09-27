using System;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>
    /// Per-cell data the terrain shader paints with, uploaded as an RG8 texture (one texel per
    /// cell, row = simulation y, so v grows downward in the map like the simulation):
    /// R = distance to the nearest open cell (0..<see cref="MaxEdge"/> cells), which draws the
    /// Worms-style outline along every edge and crater rim plus the shading inside it;
    /// G = solid cells straight above (0..<see cref="MaxDepth"/>), which puts grass on top
    /// surfaces only. Carving updates just the neighbourhood of the hole.
    /// </summary>
    public sealed class TerrainField
    {
        public const int MaxEdge = 8;
        public const int MaxDepth = 24;
        const int Straight = 3, Diagonal = 4; // chamfer distance weights (x3 cell units)

        readonly Terrain _t;
        public readonly int Width, Height;
        /// <summary>R, G per cell; index (y * Width + x) * 2.</summary>
        public readonly byte[] Data;
        int[] _dist = new int[0];

        public TerrainField(Terrain terrain)
        {
            _t = terrain;
            Width = terrain.Width;
            Height = terrain.Height;
            Data = new byte[Width * Height * 2];
            Update(new CellRect(0, 0, Width, Height));
        }

        /// <summary>Recomputes everything the cells in <paramref name="dirty"/> can affect.</summary>
        public void Update(CellRect dirty)
        {
            if (dirty.IsEmpty) return;
            // Distances change within MaxEdge of the change; computing them right there needs
            // the open cells up to MaxEdge beyond that.
            int wx0 = Math.Max(0, dirty.XMin - MaxEdge), wx1 = Math.Min(Width, dirty.XMax + MaxEdge);
            int wy0 = Math.Max(0, dirty.YMin - MaxEdge), wy1 = Math.Min(Height, dirty.YMax + MaxEdge);
            Distances(wx0, wy0, wx1, wy1);
            // Depth changes below the change, up to MaxDepth rows down.
            Depths(Math.Max(0, dirty.XMin), Math.Max(0, dirty.YMin), Math.Min(Width, dirty.XMax), Math.Min(Height, dirty.YMax + MaxDepth));
        }

        void Distances(int wx0, int wy0, int wx1, int wy1)
        {
            int x0 = Math.Max(0, wx0 - MaxEdge), x1 = Math.Min(Width, wx1 + MaxEdge);
            int y0 = Math.Max(0, wy0 - MaxEdge), y1 = Math.Min(Height, wy1 + MaxEdge);
            int w = x1 - x0, h = y1 - y0;
            if (_dist.Length < w * h) _dist = new int[w * h];
            const int Far = (MaxEdge + 2) * Straight;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    _dist[y * w + x] = _t.IsSolid(x0 + x, y0 + y) ? Far : 0;

            // Outside the map counts as open (the land has an edge there too).
            int At(int x, int y) { return x < 0 || y < 0 || x >= w || y >= h ? (Inside(x + x0, y + y0) ? Far : 0) : _dist[y * w + x]; }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = _dist[y * w + x];
                    if (d == 0) continue;
                    d = Math.Min(d, At(x - 1, y) + Straight);
                    d = Math.Min(d, At(x, y - 1) + Straight);
                    d = Math.Min(d, At(x - 1, y - 1) + Diagonal);
                    d = Math.Min(d, At(x + 1, y - 1) + Diagonal);
                    _dist[y * w + x] = d;
                }
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int d = _dist[y * w + x];
                    if (d == 0) continue;
                    d = Math.Min(d, At(x + 1, y) + Straight);
                    d = Math.Min(d, At(x, y + 1) + Straight);
                    d = Math.Min(d, At(x + 1, y + 1) + Diagonal);
                    d = Math.Min(d, At(x - 1, y + 1) + Diagonal);
                    _dist[y * w + x] = d;
                }

            for (int y = wy0; y < wy1; y++)
                for (int x = wx0; x < wx1; x++)
                {
                    int d = _dist[(y - y0) * w + (x - x0)];
                    // A solid cell next to air is 1 cell from it.
                    float cells = d / (float)Straight;
                    Data[(y * Width + x) * 2] = (byte)Math.Min(255, (int)(cells * 255f / MaxEdge + 0.5f));
                }
        }

        /// <summary>Solid, and inside a region we did not load (treated as solid, so no false edge at a window border).</summary>
        bool Inside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && _t.IsSolid(x, y);
        }

        void Depths(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x < x1; x++)
            {
                // Count the solid run above y0 (capped), then walk down.
                int run = 0;
                for (int y = Math.Max(0, y0 - MaxDepth); y < y0; y++) run = _t.IsSolid(x, y) ? Math.Min(MaxDepth, run + 1) : 0;
                for (int y = y0; y < y1; y++)
                {
                    int above = run;
                    run = _t.IsSolid(x, y) ? Math.Min(MaxDepth, run + 1) : 0;
                    Data[(y * Width + x) * 2 + 1] = (byte)(above * 255 / MaxDepth);
                }
            }
        }

        public float EdgeCells(int x, int y) { return Data[(y * Width + x) * 2] * MaxEdge / 255f; }
        public float CellsAbove(int x, int y) { return Data[(y * Width + x) * 2 + 1] * MaxDepth / 255f; }
    }
}
