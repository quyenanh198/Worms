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
                    if (hash % 12 != 0 || !SkyExposed(x, y)) continue;
                    float px = (x + 0.5f) * _scale;
                    float py = -y * _scale;
                    float height = (8f + hash % 6) * _scale;
                    // Five broad, curved leaves form a tuft instead of three
                    // narrow spikes. All geometry still rebuilds with crater chunks.
                    Blade(mesh, px - 4f * _scale, py, height * 0.55f, -5f * _scale, 5f * _scale);
                    Blade(mesh, px - 2f * _scale, py, height * 0.8f, -2.5f * _scale, 5f * _scale);
                    Blade(mesh, px, py, height, 0.5f * _scale, 6f * _scale);
                    Blade(mesh, px + 2f * _scale, py, height * 0.78f, 3f * _scale, 5f * _scale);
                    Blade(mesh, px + 4f * _scale, py, height * 0.6f, 5f * _scale, 5f * _scale);
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

        bool SkyExposed(int x, int y)
        {
            // A cave ceiling can have air immediately above it too. Only put
            // grass where the column remains open all the way to the sky.
            for (int above = y - 2; above >= 0; above--)
                if (_terrain.IsSolid(x, above)) return false;
            return true;
        }

        void Blade(MeshBuffers mesh, float x, float y, float height, float lean, float width)
        {
            int a = mesh.AddVertex(x - width / 2, y, _frontZ, 0, 0, -1, 0, 0);
            int b = mesh.AddVertex(x + lean * 0.35f - width * 0.32f,
                y + height * 0.55f, _frontZ, 0, 0, -1, 0.2f, 0.55f);
            int c = mesh.AddVertex(x + lean, y + height, _frontZ, 0, 0, -1, 0.5f, 1);
            int d = mesh.AddVertex(x + lean * 0.35f + width * 0.32f,
                y + height * 0.55f, _frontZ, 0, 0, -1, 0.8f, 0.55f);
            int e = mesh.AddVertex(x + width / 2, y, _frontZ, 0, 0, -1, 1, 0);
            mesh.AddTriangle(a, b, e);
            mesh.AddTriangle(b, d, e);
            mesh.AddTriangle(b, c, d);
        }
    }
}
