using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class MovementTests
    {
        [Fact]
        public void WormStandsStillOnFlatGround()
        {
            var w = TestWorlds.TwoWorms();
            var start = w.Worms[0].Pos;
            TestWorlds.Run(w, 300);
            Assert.Equal(start, w.Worms[0].Pos);
            Assert.Equal(WormState.Idle, w.Worms[0].State);
        }

        [Fact]
        public void WalkingMovesAtWalkSpeed()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null); // turn starts, team 0 active
            float x0 = w.Active.Pos.X;
            TestWorlds.Run(w, 60, TestWorlds.Input(w, InputKind.Move, dir: 1));
            Assert.InRange(w.Active.Pos.X - x0, C.WalkSpeed - 2, C.WalkSpeed + 1);
            Assert.Equal(1, w.Active.Facing);
        }

        [Fact]
        public void CannotClimbStepHigherThanMaxClimb()
        {
            var t = TestWorlds.Flat();
            t.FillRect(260, TestWorlds.Ground - 6, TestWorlds.W, TestWorlds.Ground, true); // 6-cell ledge
            var w = TestWorlds.Create(t, TestWorlds.OnGround(200, t), TestWorlds.OnGround(600, t));
            w.Step(null);
            TestWorlds.Run(w, 120, TestWorlds.Input(w, InputKind.Move, dir: 1));
            Assert.True(w.Active.Pos.X < 260, "worm walked up a 6-cell step");
        }

        [Fact]
        public void CanClimbSmallStep()
        {
            var t = TestWorlds.Flat();
            t.FillRect(220, TestWorlds.Ground - 3, TestWorlds.W, TestWorlds.Ground, true); // 3-cell step
            var w = TestWorlds.Create(t, TestWorlds.OnGround(200, t), TestWorlds.OnGround(600, t));
            w.Step(null);
            TestWorlds.Run(w, 60, TestWorlds.Input(w, InputKind.Move, dir: 1));
            Assert.True(w.Active.Pos.X > 240);
            Assert.True(Worm.IsStanding(t, w.Active.Pos));
        }

        [Fact]
        public void JumpLandsWithoutDamage()
        {
            var w = TestWorlds.TwoWorms();
            w.Step(null);
            float x0 = w.Active.Pos.X;
            TestWorlds.Run(w, 120, TestWorlds.Input(w, InputKind.Jump));
            Assert.Equal(WormState.Idle, w.Active.State);
            Assert.Equal(0, w.Active.PendingDamage);
            Assert.True(w.Active.Pos.X - x0 > 60, "a forward jump should move the worm forward");
        }

        [Fact]
        public void HighFallCausesDamageAndEndsTurn()
        {
            var t = TestWorlds.Flat();
            var high = new Vec2(200, 20);
            var w = TestWorlds.Create(t, high, TestWorlds.OnGround(600, t));
            var events = TestWorlds.Run(w, 200);
            Assert.True(w.Worms[0].PendingDamage > 0 || w.Worms[0].Hp < C.StartHp);
            Assert.Contains(events, e => e.Type == SimEventType.Land && e.Worm == 0);
        }

        [Fact]
        public void ShortFallIsSafe()
        {
            var t = TestWorlds.Flat();
            var w = TestWorlds.Create(t, new Vec2(200, TestWorlds.Ground - C.WormRadius - 40), TestWorlds.OnGround(600, t));
            TestWorlds.Run(w, 120);
            Assert.Equal(0, w.Worms[0].PendingDamage);
            Assert.Equal(C.StartHp, w.Worms[0].Hp);
            Assert.Equal(WormState.Idle, w.Worms[0].State);
        }

        [Fact]
        public void FallingIntoWaterDrowns()
        {
            var t = new Terrain(TestWorlds.W, TestWorlds.H);
            t.FillRect(500, TestWorlds.Ground, TestWorlds.W, TestWorlds.H, true); // no ground under x = 200
            var w = TestWorlds.Create(t, new Vec2(200, 100), TestWorlds.OnGround(600, t));
            var events = TestWorlds.Run(w, 300);
            Assert.False(w.Worms[0].Alive);
            Assert.Contains(events, e => e.Type == SimEventType.Death && e.Worm == 0 && e.Cause == DeathCause.Water);
        }
    }
}
