using System;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>Small grass shapes rebuilt with the terrain after a crater is carved.</summary>
    public sealed class SurfaceDecorMesher
    {
        readonly Terrain _terrain;
        readonly float _scale, _frontZ;

        public SurfaceDecorMesher(Terrain terrain, float scale, float frontZ)
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
            for (int y = y0; y < Math.Min(y0 + TerrainMesher.ChunkSize, _terrain.Height); y++)
                for (int x = x0; x < Math.Min(x0 + TerrainMesher.ChunkSize, _terrain.Width); x++)
                {
                    if (!_terrain.IsSolid(x, y) || _terrain.IsSolid(x, y - 1)) continue;
                    uint hash = Hash(x, y);
                    if (hash % 7 != 0) continue;
                    float px = (x + 0.5f) * _scale;
                    float py = -y * _scale;
                    float height = (5f + hash % 4) * _scale;
                    Blade(mesh, px - 1.4f * _scale, py, height * 0.72f, -1.2f * _scale, 2.2f * _scale);
                    Blade(mesh, px, py, height, 0.5f * _scale, 2.6f * _scale);
                    Blade(mesh, px + 1.4f * _scale, py, height * 0.82f, 1.2f * _scale, 2.2f * _scale);
                }
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

        void Blade(MeshBuffers mesh, float x, float y, float height, float lean, float width)
        {
            int a = mesh.AddVertex(x - width / 2, y, _frontZ, 0, 0, -1, 0, 0);
            int b = mesh.AddVertex(x + lean, y + height, _frontZ, 0, 0, -1, 0.5f, 1);
            int c = mesh.AddVertex(x + width / 2, y, _frontZ, 0, 0, -1, 1, 0);
            mesh.AddTriangle(a, b, c);
        }
    }
}
