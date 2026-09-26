using System.Linq;
using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class OnlineRulesTests
    {
        [Fact]
        public void SkipTurnEndsOnlyTheActiveTeamsTurn()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            w.SkipTurn(1);
            Assert.Equal(Phase.Aiming, w.Phase);
            w.SkipTurn(0);
            Assert.Equal(Phase.Settling, w.Phase);
            TestWorlds.RunUntil(w, x => x.Phase == Phase.Aiming);
            Assert.Equal(1, w.ActiveTeam);
        }

        [Fact]
        public void ForfeitedTeamLosesAndIsSkipped()
        {
            var t = TestWorlds.Flat();
            var w = new World(new MatchSetup
            {
                Seed = 1, Teams = 3, WormsPerTeam = 1, Terrain = t,
                Spawns = new[] { TestWorlds.OnGround(100, t), TestWorlds.OnGround(300, t), TestWorlds.OnGround(500, t) },
            });
            w.Step(null);
            w.Forfeit(1);
            TestWorlds.RunUntil(w, x => x.ActiveTeam != 0 && x.Phase == Phase.Aiming);
            Assert.Equal(2, w.ActiveTeam);
            w.Forfeit(2);
            TestWorlds.RunUntil(w, x => x.Phase == Phase.GameOver);
            Assert.Equal(0, w.Winner);
        }

        [Fact]
        public void ClientCanRebuildTerrainFromExplodeEvents()
        {
            var server = new World(new MatchSetup { Seed = 21, Teams = 2, WormsPerTeam = 2 });
            var client = MapGenerator.Generate(21);
            var ai = new Rng(5);
            for (int turn = 0; turn < 6 && server.Phase != Phase.GameOver; turn++)
            {
                TestWorlds.RunUntil(server, x => x.Phase == Phase.Aiming || x.Phase == Phase.GameOver);
                var events = server.Step(new[] { new SimInput { Team = server.ActiveTeam, Kind = InputKind.Fire, Angle = ai.Range(0f, 1.3f), Power = ai.Range(0.4f, 1f) } }).ToList();
                events.AddRange(TestWorlds.RunUntil(server, x => x.Phase == Phase.Aiming || x.Phase == Phase.GameOver));
                foreach (var e in events.Where(e => e.Type == SimEventType.Explode))
                {
                    var op = CarveOp.FromExplosion(e.X, e.Y, e.Value);
                    client.CarveCircle(op.X, op.Y, op.R);
                }
            }
            Assert.True(server.TerrainOps.Count > 0);
            Assert.Equal(server.Terrain.CopyCells(), client.CopyCells());
        }
    }
}
