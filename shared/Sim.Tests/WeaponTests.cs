using System;
using System.Linq;
using Worms.Sim;
using Xunit;

namespace Worms.Sim.Tests
{
    public class WeaponTests
    {
        static World Duel(float x0 = 200, float x1 = 260)
        {
            var w = TestWorlds.TwoWorms(null, x0, x1);
            w.Step(null);
            return w;
        }

        static void Select(World w, WeaponId id) { w.Step(TestWorlds.Input(w, InputKind.Select, weapon: id)); }

        [Fact]
        public void EveryWeaponIsDefinedOnce()
        {
            var ids = Weapons.All.Select(d => d.Id).ToArray();
            Assert.Equal(Weapons.Count, ids.Length);
            Assert.Equal(ids.Distinct().Count(), ids.Length);
            for (int i = 0; i < Weapons.Count; i++) Assert.Equal((WeaponId)i, Weapons.Get((WeaponId)i).Id);
        }

        [Fact]
        public void AmmoIsLimitedPerTeamAndSelectionRefusesEmptyWeapons()
        {
            var w = Duel();
            Assert.Equal(1, w.Ammo[0][(int)WeaponId.AirStrike]);
            Assert.Equal(-1, w.Ammo[0][(int)WeaponId.Bazooka]);
            Select(w, WeaponId.Dynamite);
            Assert.Equal(WeaponId.Dynamite, w.SelectedWeapon[0]);
            w.Step(TestWorlds.Input(w, InputKind.Fire));
            Assert.Equal(0, w.Ammo[0][(int)WeaponId.Dynamite]);
            Assert.Equal(1, w.Ammo[1][(int)WeaponId.Dynamite]);
            TestWorlds.RunUntil(w, x => x.ActiveTeam == 0 && x.Phase == Phase.Aiming && x.Tick > 10 && x.Worms[0].Alive, 60 * 200);
            if (w.Phase == Phase.Aiming && w.ActiveTeam == 0)
            {
                Assert.Equal(WeaponId.Bazooka, w.SelectedWeapon[0]); // empty weapon is deselected at turn start
                Select(w, WeaponId.Dynamite);
                Assert.NotEqual(WeaponId.Dynamite, w.SelectedWeapon[0]);
            }
        }

        [Fact]
        public void ShotgunFiresTwiceWithReaimBetween()
        {
            var w = Duel(200, 400);
            Select(w, WeaponId.Shotgun);
            var events = w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0.02f)).ToList();
            Assert.Equal(Phase.Aiming, w.Phase); // one shot left
            Assert.True(w.AttackInProgress);
            Select(w, WeaponId.Bazooka);
            Assert.Equal(WeaponId.Shotgun, w.SelectedWeapon[0]); // cannot switch mid-attack
            events.AddRange(w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0.02f)));
            Assert.Equal(Phase.Retreat, w.Phase);
            Assert.Equal(2, events.Count(e => e.Type == SimEventType.Shot));
            Assert.Equal(50, w.Worms[1].PendingDamage); // two direct hits of 25
        }

        [Fact]
        public void UziBurstsTenBulletsWithSmallDamage()
        {
            var w = Duel(200, 300);
            Select(w, WeaponId.Uzi);
            var events = TestWorlds.Run(w, 60, TestWorlds.Input(w, InputKind.Fire, angle: 0f));
            Assert.Equal(10, events.Count(e => e.Type == SimEventType.Shot));
            int hits = events.Count(e => e.Type == SimEventType.Hit && e.Worm == 1);
            Assert.InRange(hits, 6, 10); // spread may miss a few
            Assert.Equal(hits * 5, w.Worms[1].PendingDamage);
            Assert.DoesNotContain(events, e => e.Type == SimEventType.Explode); // uzi does not dig
        }

        [Fact]
        public void BatLaunchesTargetAlongAim()
        {
            var w = Duel(200, 215);
            Select(w, WeaponId.BaseballBat);
            w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0.6f));
            var target = w.Worms[1];
            Assert.Equal(30, target.PendingDamage);
            Assert.Equal(WormState.Tumbling, target.State);
            var v = target.Body.Vel.Normalized;
            Assert.InRange(v.X, (float)Math.Cos(0.6) - 0.05f, (float)Math.Cos(0.6) + 0.05f);
            Assert.True(target.Body.Vel.Y < 0);
            Assert.InRange(target.Body.Vel.Length, 650, 710);
        }

        [Fact]
        public void BatMissesWormsOutsideTheCone()
        {
            var w = Duel(200, 215);
            Select(w, WeaponId.BaseballBat);
            w.Worms[0].Facing = -1; // turned away
            w.Step(TestWorlds.Input(w, InputKind.Fire, angle: 0f));
            Assert.Equal(0, w.Worms[1].PendingDamage);
        }

        [Fact]
        public void DynamiteIsDroppedAndGivesTimeToRun()
        {
            var w = Duel(200, 600);
            Select(w, WeaponId.Dynamite);
            int fired = w.Tick + 1;
            var events = w.Step(TestWorlds.Input(w, InputKind.Fire)).ToList();
            Assert.Equal(Phase.Retreat, w.Phase);
            var stick = Assert.Single(w.Projectiles);
            Assert.InRange(stick.Pos.X, 195, 215);
            // Run away during the retreat.
            events.AddRange(TestWorlds.Run(w, 400, TestWorlds.Input(w, InputKind.Move, dir: -1)));
            var blast = events.First(e => e.Type == SimEventType.Explode);
            Assert.Equal(fired + 5 * C.TicksPerSecond, blast.Tick);
            Assert.Equal(75, blast.Value);
            Assert.True(w.Worms[0].Pos.X < 200 - 60, "worm should have walked away during the fuse");
        }

        [Fact]
        public void ClusterBombReleasesFiveFragments()
        {
            var w = Duel(200, 600);
            Select(w, WeaponId.ClusterBomb);
            var events = TestWorlds.Run(w, 60 * 12, TestWorlds.Input(w, InputKind.Fire, angle: 1.0f, power: 0.4f, fuse: 1));
            var blasts = events.Where(e => e.Type == SimEventType.Explode).ToList();
            Assert.Equal(1 + 5, blasts.Count);
            Assert.Equal(1, blasts.Count(b => b.Value == 20f && b.Tick == blasts[0].Tick));
        }

        [Fact]
        public void AirStrikeDropsFiveMissilesAroundTheTarget()
        {
            var w = Duel(200, 600);
            Select(w, WeaponId.AirStrike);
            var bad = TestWorlds.Input(w, InputKind.Fire);
            bad[0].TargetX = float.NaN;
            w.Step(bad);
            Assert.Equal(Phase.Aiming, w.Phase); // invalid target: nothing happens, ammo kept
            Assert.Equal(1, w.Ammo[0][(int)WeaponId.AirStrike]);

            var input = TestWorlds.Input(w, InputKind.Fire);
            input[0].TargetX = 600;
            var events = TestWorlds.Run(w, 60 * 8, input);
            var blasts = events.Where(e => e.Type == SimEventType.Explode).ToList();
            Assert.Equal(5, blasts.Count);
            Assert.All(blasts, b => Assert.InRange(b.X, 600 - 110, 600 + 110));
            Assert.True(w.Worms[1].PendingDamage > 0 || w.Worms[1].Hp < C.StartHp);
            Assert.Equal(0, w.Ammo[0][(int)WeaponId.AirStrike]);
        }

        [Fact]
        public void GrenadeCanBeCookedFromOneToFiveSeconds()
        {
            foreach (int fuse in new[] { 1, 5 })
            {
                var w = Duel(200, 600);
                Select(w, WeaponId.Grenade);
                int fired = w.Tick + 1;
                var events = TestWorlds.Run(w, 400, TestWorlds.Input(w, InputKind.Fire, angle: 0.8f, power: 0.3f, fuse: fuse));
                Assert.Equal(fired + fuse * C.TicksPerSecond, events.First(e => e.Type == SimEventType.Explode).Tick);
            }
        }
    }
}
