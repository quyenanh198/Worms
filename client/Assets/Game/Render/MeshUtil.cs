using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Worms.Game.Core;

namespace Worms.Game.Render
{
    /// <summary>Copies engine-free <see cref="MeshBuffers"/> into a Unity Mesh, reusing scratch lists.</summary>
    public sealed class MeshUtil
    {
        readonly List<Vector3> _positions = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();

        public void Apply(MeshBuffers src, Mesh dst)
        {
            _positions.Clear();
            _normals.Clear();
            _uvs.Clear();
            for (int i = 0; i < src.VertexCount; i++)
            {
                _positions.Add(new Vector3(src.Positions[i * 3], src.Positions[i * 3 + 1], src.Positions[i * 3 + 2]));
                _normals.Add(new Vector3(src.Normals[i * 3], src.Normals[i * 3 + 1], src.Normals[i * 3 + 2]));
                _uvs.Add(new Vector2(src.Uvs[i * 2], src.Uvs[i * 2 + 1]));
            }
            dst.Clear();
            dst.indexFormat = src.VertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            dst.SetVertices(_positions);
            dst.SetNormals(_normals);
            dst.SetUVs(0, _uvs);
            dst.SetTriangles(src.Triangles, 0);
            dst.RecalculateBounds();
        }

        public static Mesh Create(MeshBuffers src, string name)
        {
            var mesh = new Mesh { name = name };
            new MeshUtil().Apply(src, mesh);
            return mesh;
        }
    }
}
