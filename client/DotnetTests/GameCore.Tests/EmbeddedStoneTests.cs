using Worms.Game.Core;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class EmbeddedStoneTests
    {
        [Fact]
        public void StonesStayInsideSolidAndDisappearAfterCarve()
        {
            var terrain = new Terrain(128, 128);
            terrain.FillRect(0, 0, 128, 128, true);
            var mesher = new EmbeddedStoneMesher(terrain, 0.05f, -0.07f);
            var mesh = new MeshBuffers();

            mesher.BuildChunk(1, 0, mesh);
            Assert.True(mesh.VertexCount > 0);
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                int x = (int)(mesh.Positions[i * 3] / 0.05f);
                int y = (int)(-mesh.Positions[i * 3 + 1] / 0.05f);
                Assert.True(terrain.IsSolid(x, y));
            }

            terrain.FillRect(0, 0, 128, 128, false);
            mesher.BuildChunk(1, 0, mesh);
            Assert.Equal(0, mesh.VertexCount);
        }
    }
}
