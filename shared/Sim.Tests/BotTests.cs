using System.Collections.Generic;
using System.Linq;
using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class BotTests
    {
        /// <summary>Plays <paramref name="plan"/> through the real world the way the server's bot driver does.</summary>
        static void Execute(World w, BotPlan plan)
        {
            int team = w.ActiveTeam;
            if (w.Active.Facing != plan.Facing)
            {
                w.Step(new[] { new SimInput { Team = team, Kind = InputKind.Move, Dir = plan.Facing } });
                w.Step(new[] { new SimInput { Team = team, Kind = InputKind.Move, Dir = 0 } });
            }
            w.Step(new[] { new SimInput { Team = team, Kind = InputKind.Select, Weapon = plan.Weapon } });
            var fire = new SimInput { Team = team, Kind = InputKind.Fire, Angle = plan.Angle, Power = plan.Power, Fuse = plan.Fuse, Weapon = plan.Weapon };
            w.Step(new[] { fire });
            if (plan.Weapon == WeaponId.Shotgun) w.Step(new[] { fire });
            TestWorlds.RunUntil(w, x => x.Phase == Phase.TurnStart || x.Phase == Phase.GameOver);
        }

        static World Ready(World w, float wind = 0)
        {
            w.Step(null); // TurnStart -> Aiming, team 0 active
            w.Wind = wind;
            return w;
        }

        static int Lost(World w, int wormId)
        {
            var worm = w.Worms[wormId];
            return worm.Alive ? C.StartHp - worm.Hp : C.StartHp;
        }

        [Fact]
        public void HitsAnEnemyAcrossFlatGround()
        {
            var w = Ready(TestWorlds.TwoWorms(x0: 200, x1: 600));
            var plan = BotAi.Plan(BotAi.Capture(w), new Rng(7), aimError: 0);
            Assert.True(plan.Score > 20, $"expected a damaging shot, got {plan.Weapon} score {plan.Score}");
            Execute(w, plan);
            Assert.True(Lost(w, 1) >= 20, $"enemy only lost {Lost(w, 1)} HP");
            Assert.Equal(0, Lost(w, 0));
        }

        [Fact]
        public void AllowsForWind()
        {
            foreach (float wind in new[] { -80f, 80f })
            {
                var w = Ready(TestWorlds.TwoWorms(x0: 200, x1: 600), wind);
                var plan = BotAi.Plan(BotAi.Capture(w), new Rng(3), aimError: 0);
                Execute(w, plan);
                Assert.True(Lost(w, 1) >= 15, $"wind {wind}: enemy only lost {Lost(w, 1)} HP with {plan.Weapon}");
                Assert.Equal(0, Lost(w, 0));
            }
        }

        [Fact]
        public void TurnsAroundForAnEnemyBehindIt()
        {
            var t = TestWorlds.Flat();
            // The bot starts on the left half, so it spawns facing right; its target is to the left.
            var w = Ready(TestWorlds.Create(t, TestWorlds.OnGround(380, t), TestWorlds.OnGround(60, t)));
            Assert.Equal(1, w.Active.Facing);
            var plan = BotAi.Plan(BotAi.Capture(w), new Rng(1), aimError: 0);
            Assert.Equal(-1, plan.Facing);
            Execute(w, plan);
            Assert.True(Lost(w, 1) >= 15, $"enemy only lost {Lost(w, 1)} HP");
        }

        [Fact]
        public void PrefersATargetItCanHitWithoutHurtingItsOwnTeam()
        {
            var t = TestWorlds.Flat();
            // Team 0: the bot at 150 and a teammate hugging the far enemy. Team 1: enemies at 600 and 400.
            var w = Ready(new World(new MatchSetup
            {
                Seed = 1, Teams = 2, WormsPerTeam = 2, Terrain = t,
                Spawns = new[] { TestWorlds.OnGround(150, t), TestWorlds.OnGround(612, t), TestWorlds.OnGround(600, t), TestWorlds.OnGround(400, t) },
            }));
            var plan = BotAi.Plan(BotAi.Capture(w), new Rng(5), aimError: 0);
            Execute(w, plan);
            Assert.Equal(0, Lost(w, 1)); // the teammate next to the far enemy is untouched
            Assert.True(Lost(w, 3) >= 15, $"the lone enemy only lost {Lost(w, 3)} HP");
        }

        [Fact]
        public void SwingsTheBatAtAnAdjacentEnemy()
        {
            var w = Ready(TestWorlds.TwoWorms(x0: 300, x1: 322));
            var plan = BotAi.Plan(BotAi.Capture(w), new Rng(2), aimError: 0);
            Assert.Equal(WeaponId.BaseballBat, plan.Weapon);
            Execute(w, plan);
            Assert.True(Lost(w, 1) >= 30);
            Assert.Equal(0, Lost(w, 0));
        }

        [Fact]
        public void SamePlanForTheSameSeed()
        {
            var w = Ready(TestWorlds.TwoWorms(x0: 200, x1: 600), wind: 35);
            var a = BotAi.Plan(BotAi.Capture(w), new Rng(9));
            var b = BotAi.Plan(BotAi.Capture(w), new Rng(9));
            Assert.Equal((a.Weapon, a.Facing, a.Angle, a.Power, a.Fuse), (b.Weapon, b.Facing, b.Angle, b.Power, b.Fuse));
        }

        [Fact]
        public void PlanningDoesNotTouchTheLiveWorld()
        {
            var w = Ready(TestWorlds.TwoWorms(x0: 200, x1: 600));
            var cells = Enumerable.Range(0, TestWorlds.W).Select(x => w.Terrain.IsSolid(x, TestWorlds.Ground)).ToArray();
            var view = BotAi.Capture(w);
            view.Terrain.CarveCircle(400, TestWorlds.Ground, 50);
            BotAi.Plan(view, new Rng(1));
            Assert.Equal(cells, Enumerable.Range(0, TestWorlds.W).Select(x => w.Terrain.IsSolid(x, TestWorlds.Ground)).ToArray());
            Assert.Empty(w.Projectiles);
        }

        [Fact]
        public void NeverFiresWithWeaponsItHasNoAmmoFor()
        {
            var w = Ready(TestWorlds.TwoWorms(x0: 300, x1: 322));
            w.Ammo[0][(int)WeaponId.BaseballBat] = 0;
            var plan = BotAi.Plan(BotAi.Capture(w), new Rng(2), aimError: 0);
            Assert.NotEqual(WeaponId.BaseballBat, plan.Weapon);
        }
    }
}
