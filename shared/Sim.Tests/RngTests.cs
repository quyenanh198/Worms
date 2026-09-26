using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class RngTests
    {
        [Fact]
        public void SameSeedGivesSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void DifferentSeedsDiverge()
        {
            Assert.NotEqual(new Rng(1).NextUInt(), new Rng(2).NextUInt());
        }

        [Fact]
        public void RangesStayInBounds()
        {
            var r = new Rng(7);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat();
                Assert.InRange(f, 0f, 0.9999999f);
                int n = r.Range(-3, 5);
                Assert.InRange(n, -3, 4);
            }
        }
    }
}
