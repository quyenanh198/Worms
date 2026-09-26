using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class TerrainTests
    {
        [Fact]
        public void SameSeedGivesSameMap()
        {
            var a = MapGenerator.Generate(123).CopyCells();
            var b = MapGenerator.Generate(123).CopyCells();
            Assert.Equal(a, b);
            Assert.NotEqual(a, MapGenerator.Generate(124).CopyCells());
        }

        [Fact]
        public void GeneratedMapHasLandAndSea()
        {
            var t = MapGenerator.Generate(5);
            int solid = t.CountSolid();
            Assert.InRange(solid, t.Width * t.Height / 5, t.Width * t.Height * 4 / 5);
            Assert.False(t.IsSolid(0, t.Height / 3)); // edges taper into the sea
        }

        [Fact]
        public void CarveRemovesExactlyTheDisc()
        {
            var t = TestWorlds.Flat();
            var rect = t.CarveCircle(400, 350, 20);
            for (int y = 320; y < 381; y++)
                for (int x = 370; x < 431; x++)
                {
                    bool inside = (x - 400) * (x - 400) + (y - 350) * (y - 350) <= 400;
                    Assert.Equal(!inside, t.IsSolid(x, y));
                }
            Assert.Equal(380, rect.XMin);
            Assert.Equal(421, rect.XMax);
        }

        [Fact]
        public void CarveIsClampedToMap()
        {
            var t = TestWorlds.Flat();
            var rect = t.CarveCircle(5, 395, 30);
            Assert.Equal(0, rect.XMin);
            Assert.Equal(TestWorlds.H, rect.YMax);
        }
    }
}
