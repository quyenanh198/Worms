using System;
using System.Linq;
using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class CombatTests
    {
        [Fact]
        public void BazookaDoesNotTunnelThroughThinWall()
        {
            var t = TestWorlds.Flat();
            t.FillRect(300, 100, 302, TestWorlds.Ground, true); // 2-cell wall
            var w = TestWorlds.Create(t, TestWorlds.OnGround(200, t), TestWorlds.OnGround(600, t));
            w.Step(null);
            var events = TestWorlds.Run(w, 120, TestWorlds.Input(w, InputKind.Fire, angle: 0.05f, power: 1f));
            var blast = events.First(e => e.Type == SimEventType.Explode);
            Assert.InRange(blast.X, 290, 302);
        }

        [Fact]
        public void DamageFallsOffLinearlyWithDistance()
        {
            var t = TestWorlds.Flat();
            var w = TestWorlds.Create(t, TestWorlds.OnGround(200, t), TestWorlds.OnGround(600, t));
            var target = w.Worms[1];
            var center = target.Pos + new Vec2(-25f - C.WormRadius * 0.5f, 0);
            Explosion.Detonate(w, center, 50, 50);
            Assert.Equal(25, target.PendingDamage);
        }

        [Fact]
        public void DirectHitDealsFullDamage()
        {
            var w = TestWorlds.TwoWorms();
            Explosion.Detonate(w, w.Worms[1].Pos, 50, 50);
            Assert.Equal(50, w.Worms[1].PendingDamage);
        }

        [Fact]
        public void ExplosionThrowsWormUpThenItRests()
        {
            var w = TestWorlds.TwoWorms();
            var target = w.Worms[1];
            float y0 = target.Pos.Y;
            Explosion.Detonate(w, target.Pos + new Vec2(-20, 5), 50, 50);
            Assert.True(target.Body.Vel.Y < 0, "knockback must point upward");
            Assert.Equal(WormState.Tumbling, target.State);

            float minY = y0;
            for (int i = 0; i < 600; i++)
            {
                w.Step(null);
                minY = Math.Min(minY, target.Pos.Y);
            }
            Assert.True(minY < y0 - 10, "worm should fly up");
            Assert.True(target.IsGrounded, "worm should come to rest");
        }

        [Fact]
        public void BazookaIsBlownByWindGrenadeIsNot()
        {
            float Drift(WeaponId weapon, float wind)
            {
                var t = new Terrain(4000, 2000);
                t.FillRect(0, 1900, 4000, 2000, true);
                var w = TestWorlds.Create(t, new Vec2(2000, 1900 - C.WormRadius), new Vec2(3900, 1900 - C.WormRadius));
                w.Step(null);
                w.Wind = wind;
                w.Step(TestWorlds.Input(w, InputKind.Select, weapon: weapon));
                w.Step(TestWorlds.Input(w, InputKind.Fire, angle: (float)Math.PI / 2, power: 0.5f, fuse: 5));
                // Two thirds of a second of flight, in (slow-motion) projectile time.
                for (int i = 0; i < (int)(40 / C.ProjectileTimeScale); i++) w.Step(null);
                return w.Projectiles[0].Pos.X - 2000;
            }
            Assert.True(Drift(WeaponId.Bazooka, 100) > 10);
            Assert.InRange(Drift(WeaponId.Grenade, 100), -0.5f, 0.5f);
        }

        [Fact]
        public void ProjectilesFlyInSlowMotionButFusesKeepRealTime()
        {
            // A flat full-power rocket covers half its launch speed in one real second, so eyes can follow it.
            var t = new Terrain(4000, 2000);
            t.FillRect(0, 1900, 4000, 2000, true);
            var w = TestWorlds.Create(t, new Vec2(200, 1900 - C.WormRadius), new Vec2(3900, 1900 - C.WormRadius));
            w.Step(null);
            w.Wind = 0;
            w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0.3f, power: 1f));
            float x0 = w.Projectiles[0].Pos.X;
            for (int i = 0; i < C.TicksPerSecond; i++) w.Step(null);
            float perSecond = w.Projectiles[0].Pos.X - x0;
            float full = Weapons.Get(WeaponId.Bazooka).MaxSpeed * (float)System.Math.Cos(0.3);
            Assert.InRange(perSecond, full * C.ProjectileTimeScale * 0.95f, full * C.ProjectileTimeScale * 1.05f);
        }

        [Fact]
        public void GrenadeExplodesAfterFuse()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            w.Step(TestWorlds.Input(w, InputKind.Select, weapon: WeaponId.Grenade));
            int fired = w.Tick + 1;
            var events = TestWorlds.Run(w, 400, TestWorlds.Input(w, InputKind.Fire, angle: 0.8f, power: 0.3f, fuse: 2));
            var blast = events.First(e => e.Type == SimEventType.Explode);
            Assert.Equal(fired + 2 * C.TicksPerSecond, blast.Tick);
            Assert.Contains(events, e => e.Type == SimEventType.Bounce);
        }

        [Fact]
        public void TurnTimerIsFortyFiveSeconds()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            Assert.Equal(Phase.Aiming, w.Phase);
            TestWorlds.Run(w, C.TurnTicks - 2);
            Assert.Equal(Phase.Aiming, w.Phase);
            TestWorlds.Run(w, 2);
            Assert.Equal(Phase.Settling, w.Phase);
        }

        [Fact]
        public void TurnCyclesThroughPhasesAndTeams()
        {
            var w = TestWorlds.TwoWorms();
            var events = TestWorlds.Run(w, 1);
            Assert.Equal(0, w.ActiveTeam);
            Assert.Contains(events, e => e.Type == SimEventType.Turn && e.Team == 0);

            w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 1.2f, power: 0.4f));
            Assert.Equal(Phase.Retreat, w.Phase); // retreat starts while the shot flies
            Assert.Equal(C.RetreatTicks - 1, w.RetreatTicksLeft); // the firing tick already counts
            TestWorlds.RunUntil(w, x => x.Phase != Phase.Retreat);
            Assert.True(w.Phase == Phase.Flying || w.Phase == Phase.Settling);
            TestWorlds.RunUntil(w, x => x.Phase != Phase.Flying && x.Phase != Phase.Settling);
            Assert.Equal(Phase.EndOfTurn, w.Phase);
            TestWorlds.RunUntil(w, x => x.Phase == Phase.Aiming);
            Assert.Equal(1, w.ActiveTeam);
            Assert.Equal(1, w.ActiveWorm);
        }

        [Fact]
        public void InputsFromInactiveTeamAreIgnored()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            float x = w.Worms[1].Pos.X;
            w.Step(new[] { new SimInput { Team = 1, Kind = InputKind.Fire, Angle = 0.5f, Power = 1 } });
            TestWorlds.Run(w, 60, new[] { new SimInput { Team = 1, Kind = InputKind.Move, Dir = -1 } });
            Assert.Equal(Phase.Aiming, w.Phase);
            Assert.Equal(x, w.Worms[1].Pos.X);
        }

        [Fact]
        public void HpDropsOnlyAtEndOfTurn()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            Explosion.Detonate(w, w.Worms[1].Pos, 50, 30);
            Assert.Equal(C.StartHp, w.Worms[1].Hp);
            var events = TestWorlds.RunUntil(w, x => x.ActiveTeam == 1);
            int fall = events.Where(e => e.Type == SimEventType.Land && e.Worm == 1).Sum(e => e.Amount);
            Assert.Equal(C.StartHp - 30 - fall, w.Worms[1].Hp);
            Assert.Equal(0, w.Worms[1].PendingDamage);
        }

        [Fact]
        public void DeathChainTerminatesAndDecidesWinner()
        {
            // Team 1 has three worms on top of each other. When the first one dies
            // at the end of the turn, its death blast must kill the other two.
            var t = TestWorlds.Flat();
            var w = TestWorlds.Create(t,
                TestWorlds.OnGround(100, t), TestWorlds.OnGround(120, t), TestWorlds.OnGround(140, t),
                TestWorlds.OnGround(600, t), TestWorlds.OnGround(604, t), TestWorlds.OnGround(596, t));
            w.Step(null);
            foreach (var worm in w.Worms.Where(x => x.Team == 1)) worm.Hp = 1;
            w.Worms[3].Hp = 0;
            var events = TestWorlds.RunUntil(w, x => x.Phase == Phase.GameOver);
            Assert.Equal(Phase.GameOver, w.Phase);
            Assert.Equal(0, w.Winner);
            Assert.Equal(3, events.Count(e => e.Type == SimEventType.Death));
            Assert.Contains(events, e => e.Type == SimEventType.GameOver && e.Team == 0);
        }

        [Fact]
        public void SameSeedAndInputsReproduceMatch()
        {
            World Play()
            {
                var w = new World(new MatchSetup { Seed = 99, Teams = 2, WormsPerTeam = 2 });
                w.Step(null);
                w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0.7f, power: 0.8f));
                TestWorlds.RunUntil(w, x => x.ActiveTeam == 1 && x.Phase == Phase.Aiming);
                return w;
            }
            var a = Play();
            var b = Play();
            Assert.Equal(a.Tick, b.Tick);
            Assert.Equal(a.Terrain.CopyCells(), b.Terrain.CopyCells());
            for (int i = 0; i < a.Worms.Count; i++) Assert.Equal(a.Worms[i].Pos, b.Worms[i].Pos);
        }

        [Fact]
        public void GeneratedMatchSpawnsEveryWormOnLand()
        {
            var w = new World(new MatchSetup { Seed = 7, Teams = 4, WormsPerTeam = 4 });
            Assert.Equal(16, w.Worms.Count);
            foreach (var worm in w.Worms)
            {
                Assert.True(Worm.IsStanding(w.Terrain, worm.Pos), "worm " + worm.Id + " at " + worm.Pos);
                Assert.True(worm.Pos.Y < w.WaterLevel);
            }
        }
    }
}
