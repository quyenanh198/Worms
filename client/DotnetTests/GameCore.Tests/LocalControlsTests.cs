using System.Collections.Generic;
using System.Linq;
using Worms.Game.Core;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class LocalControlsTests
    {
        static InputFrame Frame() { return new InputFrame { Dt = 1f / 60, DragAngle = float.NaN, DragPower = float.NaN }; }

        [Fact]
        public void MoveIsSentOnlyWhenDirectionChanges()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.Right = true;
            for (int i = 0; i < 30; i++) c.Update(f, true, true, output);
            f.Right = false;
            c.Update(f, true, true, output);
            Assert.Equal(new[] { 1, 0 }, output.Where(x => x.Kind == InputKind.Move).Select(x => x.Dir));
        }

        [Fact]
        public void HoldingFireChargesThenReleaseFires()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.FireHeld = true;
            for (int i = 0; i < 30; i++) c.Update(f, true, true, output); // half a second
            Assert.True(c.Charging);
            f.FireHeld = false;
            c.Update(f, true, true, output);
            var shot = Assert.Single(output, x => x.Kind == InputKind.Fire);
            Assert.InRange(shot.Power, 0.49f, 0.51f);
            Assert.False(c.Charging);
        }

        [Fact]
        public void FullChargeFiresAutomatically()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.FireHeld = true;
            for (int i = 0; i < 90; i++) c.Update(f, true, true, output);
            var shot = Assert.Single(output, x => x.Kind == InputKind.Fire);
            Assert.Equal(1f, shot.Power);
        }

        [Fact]
        public void AimIsClampedAndThrottled()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.Up = true;
            for (int i = 0; i < 180; i++) c.Update(f, true, true, output);
            Assert.Equal((float)System.Math.PI / 2, c.Aim, 4);
            int aims = output.Count(x => x.Kind == InputKind.Aim);
            Assert.InRange(aims, 10, 31); // at most 10 per second over 3 s
        }

        [Fact]
        public void NoActionsOutsideOwnTurn()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.FireHeld = true;
            f.Right = true;
            f.JumpPressed = true;
            c.Update(f, false, false, output);
            Assert.Empty(output);
        }

        [Fact]
        public void DragReleaseFiresWithDragPower()
        {
            var c = new LocalControls();
            var output = new List<Intent>();
            var f = Frame();
            f.DragAngle = 0.5f;
            f.DragPower = 0.7f;
            f.DragReleased = true;
            c.Update(f, true, true, output);
            var shot = Assert.Single(output, x => x.Kind == InputKind.Fire);
            Assert.Equal(0.5f, shot.Angle);
            Assert.Equal(0.7f, shot.Power);
        }
    }
}

namespace Worms.Game.Core.Tests
{
    public class WeaponModeTests
    {
        static InputFrame Frame() { return new InputFrame { Dt = 1f / 60, DragAngle = float.NaN, DragPower = float.NaN }; }

        [Fact]
        public void InstantWeaponsFireOnceOnPress()
        {
            var c = new LocalControls();
            c.UseWeapon(WeaponId.Shotgun);
            var output = new List<Intent>();
            var f = Frame();
            f.FireHeld = true;
            for (int i = 0; i < 30; i++) c.Update(f, true, true, output);
            var shot = Assert.Single(output, x => x.Kind == InputKind.Fire);
            Assert.Equal(1f, shot.Power);
            f.FireHeld = false;
            c.Update(f, true, true, output);
            f.FireHeld = true;
            c.Update(f, true, true, output);
            Assert.Equal(2, output.Count(x => x.Kind == InputKind.Fire)); // second pull, second shot
        }

        [Fact]
        public void AirStrikeFiresAtPickedPoint()
        {
            var c = new LocalControls();
            c.UseWeapon(WeaponId.AirStrike);
            var output = new List<Intent>();
            var f = Frame();
            f.FireHeld = true;
            c.Update(f, true, true, output);
            Assert.DoesNotContain(output, x => x.Kind == InputKind.Fire); // space does nothing
            f.TargetPicked = true;
            f.TargetX = 812;
            f.TargetY = 300;
            c.Update(f, true, true, output);
            var shot = Assert.Single(output, x => x.Kind == InputKind.Fire);
            Assert.Equal(812, shot.TargetX);
        }

        [Fact]
        public void ThrownWeaponsCharge()
        {
            var c = new LocalControls();
            c.UseWeapon(WeaponId.Grenade);
            Assert.True(c.ChargeMode);
            c.UseWeapon(WeaponId.Dynamite);
            Assert.False(c.ChargeMode);
            Assert.False(c.TargetMode);
        }
    }
}
