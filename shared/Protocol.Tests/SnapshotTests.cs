using Worms.Protocol;
using Worms.Sim;
using Xunit;

namespace Worms.Protocol.Tests
{
    public class SnapshotTests
    {
        [Fact]
        public void SnapshotOfWorldRoundTrips()
        {
            var w = new World(new MatchSetup { Seed = 3, Teams = 2, WormsPerTeam = 3 });
            w.Step(null);
            w.Step(new[] { new SimInput { Team = w.ActiveTeam, Kind = InputKind.Fire, Angle = 1f, Power = 0.5f } });
            w.Step(null);

            var a = Snapshot.FromWorld(w);
            var b = Snapshot.Decode(new MsgReader(a.Encode()));

            Assert.Equal(a.Tick, b.Tick);
            Assert.Equal(Phase.Flying, b.Phase);
            Assert.Equal(a.ActiveWorm, b.ActiveWorm);
            Assert.Equal(a.Wind, b.Wind);
            Assert.Equal(a.Worms.Count, b.Worms.Count);
            for (int i = 0; i < a.Worms.Count; i++) Assert.Equal(a.Worms[i], b.Worms[i]);
            Assert.Single(b.Projectiles);
            Assert.Equal(a.Projectiles[0], b.Projectiles[0]);
        }

        [Fact]
        public void SnapshotIsSmall()
        {
            var w = new World(new MatchSetup { Seed = 3, Teams = 4, WormsPerTeam = 4 });
            w.Step(null);
            Assert.True(Snapshot.FromWorld(w).Encode().Length < 600);
        }
    }
}
