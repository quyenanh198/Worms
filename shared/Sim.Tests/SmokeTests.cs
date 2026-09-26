using System;
using System.Diagnostics;
using Worms.Sim;
using Xunit;
using Xunit.Abstractions;

namespace Worms.Sim.Tests
{
    public class SmokeTests
    {
        readonly ITestOutputHelper _out;

        public SmokeTests(ITestOutputHelper output)
        {
            _out = output;
        }

        /// <summary>Random players on generated maps: no exceptions, the game always ends, ticks stay cheap.</summary>
        [Theory]
        [InlineData(1u)]
        [InlineData(2u)]
        [InlineData(3u)]
        public void RandomMatchFinishes(uint seed)
        {
            var w = new World(new MatchSetup { Seed = seed, Teams = 2, WormsPerTeam = 2 });
            var ai = new Rng(seed * 31);
            var sw = Stopwatch.StartNew();
            int ticks = 0;
            const int maxTicks = 60 * 60 * 30; // 30 minutes of game time
            while (w.Phase != Phase.GameOver && ticks < maxTicks)
            {
                SimInput[] input = null;
                if (w.Phase == Phase.Aiming && w.PhaseTicks == 30)
                {
                    var weapon = (WeaponId)ai.Range(0, Weapons.Count);
                    w.Step(new[] { new SimInput { Team = w.ActiveTeam, Kind = InputKind.Select, Weapon = weapon } });
                    ticks++;
                    input = new[]
                    {
                        new SimInput
                        {
                            Team = w.ActiveTeam, Kind = InputKind.Fire,
                            Angle = ai.Range(-0.3f, 1.4f), Power = ai.Range(0.3f, 1f), Fuse = ai.Range(1, 6),
                            TargetX = ai.Range(100f, w.Terrain.Width - 100f),
                        },
                    };
                }
                else if (w.Phase == Phase.Aiming && w.AttackInProgress && w.PhaseTicks % 20 == 0)
                {
                    input = new[] { new SimInput { Team = w.ActiveTeam, Kind = InputKind.Fire, Angle = ai.Range(-0.3f, 1.4f) } };
                }
                else if (w.Phase == Phase.Aiming && w.PhaseTicks == 5)
                {
                    input = new[] { new SimInput { Team = w.ActiveTeam, Kind = InputKind.Move, Dir = ai.Range(-1, 2) } };
                }
                w.Step(input);
                ticks++;
            }
            sw.Stop();
            double usPerTick = sw.Elapsed.TotalMilliseconds * 1000 / ticks;
            _out.WriteLine($"seed {seed}: {ticks} ticks, winner {w.Winner}, {usPerTick:0.0} us/tick, {w.TerrainOps.Count} carves");
            Assert.Equal(Phase.GameOver, w.Phase);
            Assert.True(usPerTick < 1000, "a tick must cost well under 1 ms");
        }
    }
}
