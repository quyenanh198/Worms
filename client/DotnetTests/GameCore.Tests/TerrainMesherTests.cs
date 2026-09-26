using System;
using System.Diagnostics;
using Worms.Game.Core;
using Worms.Sim;
using Xunit;
using Xunit.Abstractions;

namespace Worms.Game.Core.Tests
{
    public class TerrainMesherTests
    {
        readonly ITestOutputHelper _out;

        public TerrainMesherTests(ITestOutputHelper output)
        {
            _out = output;
        }

        static float CrossZ(MeshBuffers m, int t, out float nx, out float ny)
        {
            int a = m.Triangles[t] * 3, b = m.Triangles[t + 1] * 3, c = m.Triangles[t + 2] * 3;
            float abx = m.Positions[b] - m.Positions[a], aby = m.Positions[b + 1] - m.Positions[a + 1], abz = m.Positions[b + 2] - m.Positions[a + 2];
            float acx = m.Positions[c] - m.Positions[a], acy = m.Positions[c + 1] - m.Positions[a + 1], acz = m.Positions[c + 2] - m.Positions[a + 2];
            nx = aby * acz - abz * acy;
            ny = abz * acx - abx * acz;
            return abx * acy - aby * acx;
        }

        [Fact]
        public void EveryCaseFacesTheCameraAndWallsFaceOutward()
        {
            // All 16 marching-squares cases appear in a noisy mask.
            var t = new Terrain(64, 64);
            var rng = new Rng(4);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    t.SetSolid(x, y, rng.NextFloat() < 0.5f);
            var mesher = new TerrainMesher(t, 1f, -1f, 1f);
            var mesh = new MeshBuffers();
            mesher.BuildChunk(0, 0, mesh);
            Assert.True(mesh.Triangles.Count > 0);

            for (int i = 0; i < mesh.Triangles.Count; i += 3)
            {
                int v = mesh.Triangles[i] * 3;
                float cz = CrossZ(mesh, i, out float cx, out float cy);
                bool isWall = mesh.Normals[v + 2] == 0;
                if (!isWall)
                {
                    Assert.True(cz < 0, "front triangle must point -Z (toward the camera)");
                }
                else
                {
                    // Geometric normal agrees with the stored outward normal.
                    Assert.True(cx * mesh.Normals[v] + cy * mesh.Normals[v + 1] > 0);
                }
            }
        }

        [Fact]
        public void FlatGroundTopWallFacesUp()
        {
            var t = new Terrain(64, 64);
            t.FillRect(0, 32, 64, 64, true);
            var mesh = new MeshBuffers();
            new TerrainMesher(t, 1f, -1f, 1f).BuildChunk(0, 0, mesh);
            bool foundUp = false;
            for (int v = 0; v < mesh.VertexCount; v++)
                if (mesh.Normals[v * 3 + 2] == 0 && mesh.Normals[v * 3 + 1] > 0.99f) foundUp = true;
            Assert.True(foundUp);
            // Full rows are merged: far fewer vertices than one quad per solid cell.
            Assert.True(mesh.VertexCount < 64 * 32, "got " + mesh.VertexCount);
        }

        [Fact]
        public void SurfaceIsAtTheCellEdge()
        {
            var t = new Terrain(64, 64);
            t.FillRect(0, 32, 64, 64, true); // first solid row y = 32 -> surface at world y = -32
            var mesh = new MeshBuffers();
            new TerrainMesher(t, 1f, -1f, 1f).BuildChunk(0, 0, mesh);
            float maxY = float.MinValue;
            for (int v = 0; v < mesh.VertexCount; v++) maxY = Math.Max(maxY, mesh.Positions[v * 3 + 1]);
            Assert.Equal(-32f, maxY, 3);
        }

        [Fact]
        public void DepthGrowsBelowTheSurface()
        {
            var t = new Terrain(64, 64);
            t.FillRect(0, 10, 64, 64, true);
            var mesh = new MeshBuffers();
            new TerrainMesher(t, 1f, -1f, 1f).BuildChunk(0, 0, mesh);
            float deepest = 0;
            for (int v = 0; v < mesh.VertexCount; v++) deepest = Math.Max(deepest, mesh.Uvs[v * 2]);
            Assert.Equal(TerrainMesher.MaxDepth, deepest);
        }

        [Fact]
        public void CarveRebuildsOnlyTouchedChunks()
        {
            var t = MapGenerator.Generate(11);
            var mesher = new TerrainMesher(t, 0.05f, 0f, 2f);
            Assert.Single(mesher.ChunksTouching(t.CarveCircle(100, 100, 20)));
            Assert.Equal(4, mesher.ChunksTouching(t.CarveCircle(128, 128, 20)).Count);
            // A carve ending right at a chunk edge still refreshes the neighbour that samples it.
            var edge = mesher.ChunksTouching(new CellRect(60, 10, 64, 20));
            Assert.Contains((1, 0), edge);
        }

        [Fact]
        public void ChunkRebuildIsFast()
        {
            var t = MapGenerator.Generate(11);
            var mesher = new TerrainMesher(t, 0.05f, 0f, 2f);
            var mesh = new MeshBuffers();
            int tris = 0;
            var sw = Stopwatch.StartNew();
            for (int cy = 0; cy < mesher.ChunksY; cy++)
                for (int cx = 0; cx < mesher.ChunksX; cx++)
                {
                    mesher.BuildChunk(cx, cy, mesh);
                    tris += mesh.Triangles.Count / 3;
                }
            sw.Stop();
            double perChunk = sw.Elapsed.TotalMilliseconds / (mesher.ChunksX * mesher.ChunksY);
            _out.WriteLine($"{perChunk:0.000} ms/chunk, {tris} triangles for the whole map");
            Assert.True(perChunk < 2, "chunk rebuild too slow: " + perChunk);
            Assert.True(tris < 400_000, "too many triangles: " + tris);
        }
    }
}
