using System.Collections.Generic;
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
        TerrainField _field;
        Texture2D _fieldTex;
        Mesh[,] _meshes;
        MeshRenderer[,] _renderers;
        readonly MeshBuffers _buffers = new MeshBuffers();
        readonly MeshBuffers _decorBuffers = new MeshBuffers();
        readonly MeshUtil _util = new MeshUtil();
        SurfaceDecorMesher _decorMesher;
        Material _decorMaterial;
        Material _terrainMaterial;
        readonly Dictionary<int, (Mesh mesh, MeshRenderer renderer)> _decor = new Dictionary<int, (Mesh, MeshRenderer)>();

        public void Init(SimTerrain terrain, Material material)
        {
            _terrainMaterial = material;
            _mesher = new TerrainMesher(terrain, WorldSpace.Scale, WorldSpace.TerrainFrontZ, WorldSpace.TerrainBackZ);
            _decorMesher = new SurfaceDecorMesher(terrain, WorldSpace.Scale, WorldSpace.TerrainFrontZ - 0.06f);
            _decorMaterial = Materials.Toon(new Color(0.30f, 0.64f, 0.13f));
            // Edge distance and land-above per cell: the shader paints outlines, crater rims and grass from it.
            _field = new TerrainField(terrain);
            _fieldTex = new Texture2D(terrain.Width, terrain.Height, TextureFormat.RG16, false, true)
            {
                name = "TerrainField", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            UploadField();
            material.SetTexture("_Field", _fieldTex);
            material.SetVector("_FieldSize", new Vector4(terrain.Width, terrain.Height, 1f / WorldSpace.Scale, 0));
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

        public void SetPaintStrength(float strength)
        {
            if (_terrainMaterial != null) _terrainMaterial.SetFloat("_PaintStrength", strength);
        }

        public void Refresh(CellRect dirty)
        {
            foreach (var (cx, cy) in _mesher.ChunksTouching(dirty)) Rebuild(cx, cy);
            _field.Update(dirty);
            UploadField();
        }

        void UploadField()
        {
            _fieldTex.SetPixelData(_field.Data, 0);
            _fieldTex.Apply(false, false);
        }

        void OnDestroy()
        {
            if (_fieldTex != null) Destroy(_fieldTex);
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
            RebuildDecor(cx, cy);
        }

        void RebuildDecor(int cx, int cy)
        {
            _decorMesher.BuildChunk(cx, cy, _decorBuffers);
            int key = cy * _mesher.ChunksX + cx;
            if (_decorBuffers.VertexCount == 0)
            {
                if (_decor.TryGetValue(key, out var empty)) empty.renderer.enabled = false;
                return;
            }
            if (!_decor.TryGetValue(key, out var chunk))
            {
                var go = new GameObject("Grass " + cx + "," + cy);
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = go.name };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _decorMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                chunk = (mesh, renderer);
                _decor[key] = chunk;
            }
            _util.Apply(_decorBuffers, chunk.mesh);
            chunk.renderer.enabled = true;
        }

    }
}
