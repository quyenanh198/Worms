using System;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>Sparse, non-interactive stone inlays on the destructible front face.</summary>
    public sealed class EmbeddedStoneMesher
    {
        readonly Terrain _terrain;
        readonly float _scale, _frontZ;

        public EmbeddedStoneMesher(Terrain terrain, float scale, float frontZ)
        {
            _terrain = terrain;
            _scale = scale;
            _frontZ = frontZ;
        }

        public void BuildChunk(int cx, int cy, MeshBuffers mesh)
        {
            mesh.Clear();
            int x0 = cx * TerrainMesher.ChunkSize;
            int y0 = cy * TerrainMesher.ChunkSize;
            int x1 = Math.Min(x0 + TerrainMesher.ChunkSize, _terrain.Width);
            int y1 = Math.Min(y0 + TerrainMesher.ChunkSize, _terrain.Height);
            if (x1 - x0 < 48 || y1 - y0 < 48) return;
            uint hash = Hash(x0 + 32, y0 + 32);
            if (hash % 2 == 0) return;
            int px = x0 + 32 + (int)((hash >> 8) % 33) - 16;
            int py = y0 + 32 + (int)((hash >> 13) % 33) - 16;
            int rx = 8 + (int)((hash >> 18) % 7);
            int ry = 5 + (int)((hash >> 22) % 4);
            if (InsideSolid(px, py, rx, ry)) AddStone(mesh, px, py, rx, ry);
        }

        bool InsideSolid(int x, int y, int rx, int ry)
        {
            // A conservative check keeps the entire polygon off crater edges.
            for (int yy = y - ry; yy <= y + ry; yy++)
                for (int xx = x - rx; xx <= x + rx; xx++)
                    if (!_terrain.IsSolid(xx, yy)) return false;
            return true;
        }

        void AddStone(MeshBuffers mesh, int x, int y, int rx, int ry)
        {
            float cx = x * _scale, cy = -y * _scale;
            float w = rx * _scale, h = ry * _scale;
            int first = mesh.VertexCount;
            // Clockwise in screen coordinates; UVs drive broad top/side facets.
            Add(mesh, cx - w, cy - h * 0.18f, 0f, 0.42f);
            Add(mesh, cx - w * 0.66f, cy + h * 0.76f, 0.17f, 0.88f);
            Add(mesh, cx + w * 0.08f, cy + h, 0.54f, 1f);
            Add(mesh, cx + w * 0.79f, cy + h * 0.54f, 0.90f, 0.77f);
            Add(mesh, cx + w, cy - h * 0.35f, 1f, 0.33f);
            Add(mesh, cx + w * 0.32f, cy - h, 0.66f, 0f);
            Add(mesh, cx - w * 0.53f, cy - h * 0.77f, 0.24f, 0.11f);
            for (int i = 1; i < 6; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        void Add(MeshBuffers mesh, float x, float y, float u, float v)
        {
            mesh.AddVertex(x, y, _frontZ, 0, 0, -1, u, v);
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 73856093u ^ (uint)y * 19349663u;
                h ^= h >> 13;
                return h * 1274126177u;
            }
        }
    }
}
