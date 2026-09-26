using UnityEngine;
using UnityEngine.Rendering;
using Worms.Game.Core;
using SimTerrain = Worms.Sim.Terrain;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>One MeshRenderer per 64x64 chunk; only chunks touched by a carve are rebuilt.</summary>
    public sealed class TerrainView : MonoBehaviour
    {
        TerrainMesher _mesher;
        Mesh[,] _meshes;
        MeshRenderer[,] _renderers;
        readonly MeshBuffers _buffers = new MeshBuffers();
        readonly MeshUtil _util = new MeshUtil();

        public void Init(SimTerrain terrain, Material material)
        {
            _mesher = new TerrainMesher(terrain, WorldSpace.Scale, WorldSpace.TerrainFrontZ, WorldSpace.TerrainBackZ);
            _meshes = new Mesh[_mesher.ChunksX, _mesher.ChunksY];
            _renderers = new MeshRenderer[_mesher.ChunksX, _mesher.ChunksY];
            for (int cy = 0; cy < _mesher.ChunksY; cy++)
                for (int cx = 0; cx < _mesher.ChunksX; cx++)
                {
                    var go = new GameObject("Chunk " + cx + "," + cy);
                    go.transform.SetParent(transform, false);
                    var mesh = new Mesh { name = go.name };
                    mesh.MarkDynamic();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    _meshes[cx, cy] = mesh;
                    _renderers[cx, cy] = r;
                    Rebuild(cx, cy);
                }
        }

        public void Refresh(CellRect dirty)
        {
            foreach (var (cx, cy) in _mesher.ChunksTouching(dirty)) Rebuild(cx, cy);
        }

        public void RefreshCircle(float x, float y, float r)
        {
            int ri = Mathf.CeilToInt(r) + 1;
            Refresh(new CellRect((int)x - ri, (int)y - ri, (int)x + ri + 1, (int)y + ri + 1));
        }

        void Rebuild(int cx, int cy)
        {
            _mesher.BuildChunk(cx, cy, _buffers);
            _util.Apply(_buffers, _meshes[cx, cy]);
            _renderers[cx, cy].enabled = _buffers.VertexCount > 0;
        }
    }
}
