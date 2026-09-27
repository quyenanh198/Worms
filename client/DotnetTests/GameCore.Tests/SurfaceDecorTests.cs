using Worms.Game.Core;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class SurfaceDecorTests
    {
        [Fact]
        public void GrassOnlyAppearsOnExposedSurfaceAndUpdatesAfterCarve()
        {
            var terrain = new Terrain(64, 64);
            terrain.FillRect(0, 32, 64, 64, true);
            var decor = new SurfaceDecorMesher(terrain, 0.05f, -0.06f);
            var mesh = new MeshBuffers();

            decor.BuildChunk(0, 0, mesh);
            Assert.True(mesh.VertexCount > 0);
            for (int i = 0; i < mesh.VertexCount; i++)
                Assert.True(mesh.Positions[i * 3 + 1] >= -32f * 0.05f);

            terrain.FillRect(0, 0, 64, 64, false);
            decor.BuildChunk(0, 0, mesh);
            Assert.Equal(0, mesh.VertexCount);
        }
    }
}
