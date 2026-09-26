using System.Collections.Generic;
using Worms.Sim;

namespace Worms.Sim.Tests
{
    static class TestWorlds
    {
        public const int W = 800;
        public const int H = 400;
        public const int Ground = 300;

        /// <summary>Flat ground at y = 300 across the whole map.</summary>
        public static Terrain Flat()
        {
            var t = new Terrain(W, H);
            t.FillRect(0, Ground, W, H, true);
            return t;
        }

        /// <summary>Standing position on flat ground at column x.</summary>
        public static Vec2 OnGround(float x, Terrain t = null)
        {
            t = t ?? Flat();
            for (int y = 0; y < H; y++)
            {
                var p = new Vec2(x, y);
                if (Worm.IsStanding(t, p)) return p;
            }
            return new Vec2(x, Ground - C.WormRadius);
        }

        public static World Create(Terrain t, params Vec2[] spawns)
        {
            return new World(new MatchSetup { Seed = 1, Teams = 2, WormsPerTeam = spawns.Length / 2, Terrain = t, Spawns = spawns });
        }

        public static World TwoWorms(Terrain t = null, float x0 = 200, float x1 = 600)
        {
            t = t ?? Flat();
            return Create(t, OnGround(x0, t), OnGround(x1, t));
        }

        public static List<SimEvent> Run(World w, int ticks, IReadOnlyList<SimInput> first = null)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                all.AddRange(w.Step(i == 0 ? first : null));
            }
            return all;
        }

        public static List<SimEvent> RunUntil(World w, System.Func<World, bool> done, int maxTicks = 60 * 120)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < maxTicks && !done(w); i++) all.AddRange(w.Step(null));
            return all;
        }

        public static SimInput[] Input(World w, InputKind kind, int dir = 0, float angle = 0, float power = 0, int fuse = 0, WeaponId weapon = WeaponId.Bazooka)
        {
            return new[] { new SimInput { Team = w.ActiveTeam, Kind = kind, Dir = dir, Angle = angle, Power = power, Fuse = fuse, Weapon = weapon } };
        }
    }
}
