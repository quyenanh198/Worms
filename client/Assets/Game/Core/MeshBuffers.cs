using System.Collections.Generic;

namespace Worms.Game.Core
{
    /// <summary>
    /// Engine-free mesh data (Unity converts it to a Mesh). Positions and
    /// normals are in Unity world space: X right, Y up, camera looking +Z.
    /// </summary>
    public sealed class MeshBuffers
    {
        public readonly List<float> Positions = new List<float>();
        public readonly List<float> Normals = new List<float>();
        /// <summary>UV0.x = cells of rock above this point (0 at the surface), UV0.y = 1 on side walls.</summary>
        public readonly List<float> Uvs = new List<float>();
        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Positions.Count / 3;

        public void Clear()
        {
            Positions.Clear();
            Normals.Clear();
            Uvs.Clear();
            Triangles.Clear();
        }

        public int AddVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v)
        {
            Positions.Add(x); Positions.Add(y); Positions.Add(z);
            Normals.Add(nx); Normals.Add(ny); Normals.Add(nz);
            Uvs.Add(u); Uvs.Add(v);
            return VertexCount - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
        }
    }
}
