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
        public void SelectedConceptSeedHasCentralBasinBetweenTwoHighShelves()
        {
            var t = MapGenerator.Generate(123456);
            int Surface(int x)
            {
                for (int y = 0; y < t.Height; y++)
                    if (t.IsSolid(x, y)) return y;
                return t.Height;
            }

            int basin = Surface(t.Width / 2);
            Assert.True(basin - Surface(640) >= 100, "Left shelf must stand above the basin");
            Assert.True(basin - Surface(1408) >= 100, "Right shelf must stand above the basin");
        }

        [Fact]
        public void SelectedConceptSeedHasDryCentralBattleFloor()
        {
            var t = MapGenerator.Generate(123456);
            int water = t.Height - C.WaterDepth;
            for (int x = t.Width * 45 / 100; x <= t.Width * 55 / 100; x += 16)
            {
                int top = 0;
                while (top < t.Height && !t.IsSolid(x, top)) top++;
                Assert.True(top < water - 80,
                    $"Central valley at x={x} reaches y={top}; water starts at {water}");
            }
        }

        [Fact]
        public void GeneratedSpawnsStayInTheCentralBattleArea()
        {
            foreach (uint seed in new uint[] { 0, 1, 5, 42, 123456, 123457 })
            {
                var world = new World(new MatchSetup { Seed = seed, Teams = 4, WormsPerTeam = 4 });
                foreach (var worm in world.Worms)
                    Assert.InRange(worm.Pos.X, world.Terrain.Width / 2f - 400f,
                        world.Terrain.Width / 2f + 400f);
            }
        }

        [Theory]
        [InlineData(5u)]
        [InlineData(123456u)]
        [InlineData(123457u)]
        public void GeneratedMapHasBroadTerracesAndInteriorCliffs(uint seed)
        {
            var t = MapGenerator.Generate(seed);
            var surface = new int[t.Width];
            for (int x = 0; x < t.Width; x++)
            {
                surface[x] = -1;
                for (int y = 0; y < t.Height; y++)
                    if (t.IsSolid(x, y)) { surface[x] = y; break; }
            }

            int broadTerraces = 0, interiorCliffs = 0, run = 0, steepestCell = 0, steepestX = -1;
            for (int x = 81; x < t.Width - 81; x++)
            {
                int a = surface[x - 1], b = surface[x];
                if (a < 0 || b < 0) { run = 0; continue; }
                int cellRise = System.Math.Abs(b - a);
                if (cellRise > steepestCell) { steepestCell = cellRise; steepestX = x; }
                if (cellRise <= 3) run++;
                else
                {
                    if (run >= 55) broadTerraces++;
                    run = 0;
                }
                if (x + 8 < t.Width && surface[x + 8] >= 0 &&
                    System.Math.Abs(surface[x + 8] - a) >= 40)
                {
                    interiorCliffs++;
                    x += 8;
                }
            }
            if (run >= 55) broadTerraces++;
            Assert.True(broadTerraces >= 4, $"Seed {seed}: only {broadTerraces} broad terraces");
            Assert.True(interiorCliffs >= 3, $"Seed {seed}: only {interiorCliffs} interior cliffs");
            Assert.True(steepestCell <= 55, $"Seed {seed}: a {steepestCell}-cell vertical wall at x={steepestX} ({surface[steepestX - 1]} to {surface[steepestX]}) looks like a box");
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(42u)]
        [InlineData(5u)]
        [InlineData(9999u)]
        [InlineData(123456u)]
        [InlineData(123457u)]
        public void WormsSpawnStandingOnTerracedMap(uint seed)
        {
            var world = new World(new MatchSetup { Seed = seed, Teams = 4, WormsPerTeam = 4 });
            foreach (var worm in world.Worms)
            {
                Assert.True(Worm.IsStanding(world.Terrain, worm.Pos),
                    $"Seed {seed}: worm {worm.Id} is not standing at {worm.Pos}");
                Assert.True(worm.Pos.Y < world.WaterLevel - 3 * C.WormRadius,
                    $"Seed {seed}: worm {worm.Id} spawned too close to water");
                int x = (int)worm.Pos.X;
                float? left = world.SurfaceY(x - 18), right = world.SurfaceY(x + 18);
                Assert.True(left.HasValue && right.HasValue &&
                    System.Math.Abs(left.Value - worm.Pos.Y) <= C.MaxClimb &&
                    System.Math.Abs(right.Value - worm.Pos.Y) <= C.MaxClimb,
                    $"Seed {seed}: worm {worm.Id} spawned too close to a cliff");
            }
        }

        [Fact]
        public void FourTeamSpawnsRemainSafeAcrossManySeeds()
        {
            for (uint seed = 0; seed < 32; seed++)
            {
                var world = new World(new MatchSetup { Seed = seed, Teams = 4, WormsPerTeam = 4 });
                foreach (var worm in world.Worms)
                {
                    int x = (int)worm.Pos.X;
                    float? left = world.SurfaceY(x - 18), right = world.SurfaceY(x + 18);
                    Assert.True(Worm.IsStanding(world.Terrain, worm.Pos) &&
                        left.HasValue && right.HasValue &&
                        System.Math.Abs(left.Value - worm.Pos.Y) <= C.MaxClimb &&
                        System.Math.Abs(right.Value - worm.Pos.Y) <= C.MaxClimb,
                        $"Seed {seed}: worm {worm.Id} lacks a safe shelf");
                }
            }
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
