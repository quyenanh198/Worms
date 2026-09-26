using System;
using System.Collections.Generic;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>
    /// Turns the terrain mask into chunked 3D meshes (docs/PLAN.md §3.9):
    /// a front face from marching squares (full squares merged into row runs)
    /// plus side walls extruded along Z from every boundary segment.
    /// Samples sit at cell centers, so boundaries fall on cell edges, matching
    /// the simulation's collision exactly.
    /// </summary>
    public sealed class TerrainMesher
    {
        public const int ChunkSize = 64;
        /// <summary>Rock deeper than this is shaded the same.</summary>
        public const int MaxDepth = 24;

        readonly Terrain _t;
        /// <summary>World units per simulation unit.</summary>
        public readonly float Scale;
        /// <summary>Front face Z (toward the camera) and back Z of the walls, in world units.</summary>
        public readonly float FrontZ, BackZ;

        public int ChunksX => (_t.Width + ChunkSize - 1) / ChunkSize;
        public int ChunksY => (_t.Height + ChunkSize - 1) / ChunkSize;

        public TerrainMesher(Terrain terrain, float scale, float frontZ, float backZ)
        {
            _t = terrain;
            Scale = scale;
            FrontZ = frontZ;
            BackZ = backZ;
        }

        /// <summary>Chunks whose mesh may change when the cells in <paramref name="dirty"/> change.</summary>
        public List<(int cx, int cy)> ChunksTouching(CellRect dirty)
        {
            var list = new List<(int, int)>();
            if (dirty.IsEmpty) return list;
            // A cell is a sample of the squares on both sides of it.
            int x0 = Math.Max(0, (dirty.XMin - 1) / ChunkSize), x1 = Math.Min(ChunksX - 1, dirty.XMax / ChunkSize);
            int y0 = Math.Max(0, (dirty.YMin - 1) / ChunkSize), y1 = Math.Min(ChunksY - 1, dirty.YMax / ChunkSize);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                    list.Add((cx, cy));
            return list;
        }

        bool S(int i, int j) { return _t.IsSolid(i, j); }

        int CaseAt(int i, int j)
        {
            return (S(i, j) ? 1 : 0) | (S(i + 1, j) ? 2 : 0) | (S(i + 1, j + 1) ? 4 : 0) | (S(i, j + 1) ? 8 : 0);
        }

        // Point ids inside a square: 0..3 corners TL, TR, BR, BL; 4..7 edge midpoints top, right, bottom, left.
        // Polygons are listed clockwise on screen, which is Unity's front-facing winding.
        static readonly int[][] Polygons =
        {
            null,
            new[] { 0, 4, 7 },
            new[] { 4, 1, 5 },
            new[] { 0, 1, 5, 7 },
            new[] { 5, 2, 6 },
            new[] { 0, 4, 5, 2, 6, 7 },
            new[] { 4, 1, 2, 6 },
            new[] { 0, 1, 2, 6, 7 },
            new[] { 7, 6, 3 },
            new[] { 0, 4, 6, 3 },
            new[] { 4, 1, 5, 6, 3, 7 },
            new[] { 0, 1, 5, 6, 3 },
            new[] { 7, 5, 2, 3 },
            new[] { 0, 4, 5, 2, 3 },
            new[] { 4, 1, 2, 3, 7 },
            new[] { 0, 1, 2, 3 },
        };

        static readonly float[] PointX = { 0, 1, 1, 0, 0.5f, 1, 0.5f, 0 };
        static readonly float[] PointY = { 0, 0, 1, 1, 0, 0.5f, 1, 0.5f };

        public void BuildChunk(int cx, int cy, MeshBuffers mesh)
        {
            mesh.Clear();
            int i0 = cx * ChunkSize, j0 = cy * ChunkSize;
            // The first chunk row/column also owns the squares that close the map edge.
            int iStart = cx == 0 ? -1 : i0, jStart = cy == 0 ? -1 : j0;
            int iEnd = Math.Min(i0 + ChunkSize, _t.Width), jEnd = Math.Min(j0 + ChunkSize, _t.Height);

            for (int j = jStart; j < jEnd; j++)
            {
                int runStart = int.MinValue;
                for (int i = iStart; i <= iEnd; i++)
                {
                    int c = i < iEnd ? CaseAt(i, j) : 0;
                    if (c == 15)
                    {
                        if (runStart == int.MinValue) runStart = i;
                        continue;
                    }
                    if (runStart != int.MinValue)
                    {
                        AddRun(mesh, runStart, i, j);
                        runStart = int.MinValue;
                    }
                    if (c != 0 && i < iEnd) AddSquare(mesh, i, j, c);
                }
            }
        }

        // Sample (i, j) sits at cell center (i + 0.5, j + 0.5).
        float Wx(float sx) { return (sx + 0.5f) * Scale; }
        float Wy(float sy) { return -(sy + 0.5f) * Scale; }

        float DepthAt(float sx, float sy)
        {
            int x = (int)Math.Floor(sx + 0.5f), y = (int)Math.Floor(sy + 0.5f);
            if (!_t.IsSolid(x, y)) return 0;
            int d = 0;
            while (d < MaxDepth && _t.IsSolid(x, y - d - 1)) d++;
            return d;
        }

        void AddRun(MeshBuffers mesh, int iFrom, int iTo, int j)
        {
            // Full squares from sample column iFrom to iTo (exclusive) in sample row j: one quad.
            float x0 = iFrom, x1 = iTo, y0 = j, y1 = j + 1;
            int a = FrontVertex(mesh, x0, y0);
            int b = FrontVertex(mesh, x1, y0);
            int c = FrontVertex(mesh, x1, y1);
            int d = FrontVertex(mesh, x0, y1);
            mesh.AddTriangle(a, b, c);
            mesh.AddTriangle(a, c, d);
        }

        int FrontVertex(MeshBuffers mesh, float sx, float sy)
        {
            return mesh.AddVertex(Wx(sx), Wy(sy), FrontZ, 0, 0, -1, DepthAt(sx, sy), 0);
        }

        void AddSquare(MeshBuffers mesh, int i, int j, int c)
        {
            var poly = Polygons[c];
            int first = -1, prev = -1;
            for (int k = 0; k < poly.Length; k++)
            {
                int v = FrontVertex(mesh, i + PointX[poly[k]], j + PointY[poly[k]]);
                if (k == 0) first = v;
                else if (k >= 2) mesh.AddTriangle(first, prev, v);
                prev = v;
            }
            // Boundary segments run between two consecutive edge midpoints.
            for (int k = 0; k < poly.Length; k++)
            {
                int p = poly[k], q = poly[(k + 1) % poly.Length];
                if (p >= 4 && q >= 4) AddWall(mesh, i + PointX[p], j + PointY[p], i + PointX[q], j + PointY[q]);
            }
        }

        void AddWall(MeshBuffers mesh, float sx0, float sy0, float sx1, float sy1)
        {
            float ax = Wx(sx0), ay = Wy(sy0), bx = Wx(sx1), by = Wy(sy1);
            // The polygon is clockwise on screen, so solid is on the right of a->b
            // and the outward normal is on the left: (-dy, dx) in Y-up space.
            float dx = bx - ax, dy = by - ay;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float nx = -dy / len, ny = dx / len;
            int a0 = mesh.AddVertex(ax, ay, FrontZ, nx, ny, 0, 0, 1);
            int b0 = mesh.AddVertex(bx, by, FrontZ, nx, ny, 0, 0, 1);
            int b1 = mesh.AddVertex(bx, by, BackZ, nx, ny, 0, 0, 1);
            int a1 = mesh.AddVertex(ax, ay, BackZ, nx, ny, 0, 0, 1);
            mesh.AddTriangle(a0, a1, b1);
            mesh.AddTriangle(a0, b1, b0);
        }
    }
}
