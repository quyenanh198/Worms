using System.Collections.Generic;
using Worms.Game.Core;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class TouchTests
    {
        static readonly TouchLayout Layout = TouchLayout.For(1600, 900);

        static InputFrame Frame() { return new InputFrame { Dt = 1f / 60, DragAngle = float.NaN, DragPower = float.NaN }; }

        static TouchContext Ctx(bool charge = true, bool target = false)
        {
            return new TouchContext { CanAct = true, ChargeMode = charge, TargetMode = target, WormX = 800, WormY = 450, Facing = 1, FullPowerPixels = 300 };
        }

        static InputFrame Step(TouchInterpreter ti, TouchContext ctx, params TouchPoint[] touches)
        {
            var f = Frame();
            ti.Update(touches, Layout, ctx, ref f, out _, out _, out _);
            return f;
        }

        static TouchPoint T(int id, float x, float y, TouchPhase2 p) { return new TouchPoint { Id = id, X = x, Y = y, Phase = p }; }

        [Fact]
        public void HoldingArrowButtonsMoves()
        {
            var ti = new TouchInterpreter();
            float x = Layout.Right.X + 5, y = Layout.Right.Y + 5;
            Assert.True(Step(ti, Ctx(), T(1, x, y, TouchPhase2.Began)).Right);
            Assert.True(Step(ti, Ctx(), T(1, x, y, TouchPhase2.Stationary)).Right);
            Assert.False(Step(ti, Ctx(), T(1, x, y, TouchPhase2.Ended)).Right);
        }

        [Fact]
        public void DragFromWormAimsAndReleaseFires()
        {
            var ti = new TouchInterpreter();
            Step(ti, Ctx(), T(1, 810, 460, TouchPhase2.Began));
            var f = Step(ti, Ctx(), T(1, 950, 600, TouchPhase2.Moved));
            Assert.InRange(f.DragAngle, 0.7f, 0.87f); // about 45 degrees up
            Assert.InRange(f.DragPower, 0.70f, 0.72f); // 212 px of 300
            Assert.False(f.DragReleased);
            f = Step(ti, Ctx(), T(1, 950, 600, TouchPhase2.Ended));
            Assert.True(f.DragReleased);
        }

        [Fact]
        public void TinyDragDoesNotFire()
        {
            var ti = new TouchInterpreter();
            Step(ti, Ctx(), T(1, 805, 452, TouchPhase2.Began));
            Assert.False(Step(ti, Ctx(), T(1, 810, 455, TouchPhase2.Ended)).DragReleased);
        }

        [Fact]
        public void FireButtonOnlyForInstantWeapons()
        {
            var ti = new TouchInterpreter();
            float x = Layout.Fire.X + 5, y = Layout.Fire.Y + 5;
            Assert.True(Step(ti, Ctx(charge: false), T(1, x, y, TouchPhase2.Began)).FireHeld);
            var ti2 = new TouchInterpreter();
            Assert.False(Step(ti2, Ctx(charge: true), T(1, x, y, TouchPhase2.Began)).FireHeld);
        }

        [Fact]
        public void TapPicksAirStrikeTargetButDragPans()
        {
            var ti = new TouchInterpreter();
            Step(ti, Ctx(target: true), T(1, 1200, 500, TouchPhase2.Began));
            var f = Step(ti, Ctx(target: true), T(1, 1203, 502, TouchPhase2.Ended));
            Assert.True(f.TargetPicked);
            Assert.Equal(1203, f.TargetX);

            var ti2 = new TouchInterpreter();
            Step(ti2, Ctx(target: true), T(1, 1200, 500, TouchPhase2.Began));
            var g = Frame();
            ti2.Update(new[] { T(1, 1300, 500, TouchPhase2.Moved) }, Layout, Ctx(target: true), ref g, out float px, out _, out _);
            Assert.Equal(100, px);
            g = Frame();
            ti2.Update(new[] { T(1, 1300, 500, TouchPhase2.Ended) }, Layout, Ctx(target: true), ref g, out _, out _, out _);
            Assert.False(g.TargetPicked);
        }

        [Fact]
        public void PinchZooms()
        {
            var ti = new TouchInterpreter();
            var f = Frame();
            ti.Update(new[] { T(1, 600, 500, TouchPhase2.Began), T(2, 1000, 500, TouchPhase2.Began) }, Layout, Ctx(), ref f, out _, out _, out _);
            ti.Update(new[] { T(1, 500, 500, TouchPhase2.Moved), T(2, 1100, 500, TouchPhase2.Moved) }, Layout, Ctx(), ref f, out _, out _, out float zoom);
            Assert.InRange(zoom, 0.66f, 0.67f); // fingers apart: zoom in (distance shrinks)
        }

        [Fact]
        public void TierPickerFollowsFrameTime()
        {
            Assert.Equal(2, TierPicker.Pick(14, false));
            Assert.Equal(1, TierPicker.Pick(14, true));
            Assert.Equal(1, TierPicker.Pick(22, false));
            Assert.Equal(0, TierPicker.Pick(40, false));
            var p = new TierPicker();
            for (int i = 0; i < TierPicker.WarmupFrames; i++) p.Add(1f);
            for (int i = 0; i < TierPicker.SampleFrames; i++) p.Add(0.02f);
            Assert.True(p.Done);
            Assert.Equal(20f, p.AverageMs, 1);
        }
    }
}
