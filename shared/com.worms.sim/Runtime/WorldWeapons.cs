using System;

namespace Worms.Sim
{
    /// <summary>The five weapon behaviors (docs/PLAN.md §3.8).</summary>
    public sealed partial class World
    {
        /// <summary>Shotgun shots left in this turn's attack; the player may re-aim between them.</summary>
        int _shotsLeft;
        /// <summary>Uzi bullets left in the current automatic burst.</summary>
        int _burstLeft;
        int _burstTimer;
        float _burstAim;

        /// <summary>A multi-shot weapon was fired and the attack is not over yet (no weapon switching).</summary>
        public bool AttackInProgress => _shotsLeft > 0 || _burstLeft > 0;

        void Fire(Worm worm, SimInput input)
        {
            var team = ActiveTeam;
            if (!Weapons.TryGet(SelectedWeapon[team], out var def)) return;
            if (Ammo[team][(int)def.Id] == 0 && _shotsLeft == 0) return;
            if (def.Targets && !ValidTarget(input)) return;

            if (def.Aims) worm.Aim = ClampAngle(input.Angle);
            MoveDir = 0;
            worm.StopWalking();

            bool firstShot = _shotsLeft == 0;
            if (firstShot)
            {
                if (Ammo[team][(int)def.Id] > 0) Ammo[team][(int)def.Id]--;
                _shotsLeft = def.Shots;
            }
            _shotsLeft--;
            Emit(new SimEvent { Type = SimEventType.Fire, Worm = worm.Id, Weapon = def.Id });

            switch (def.Behavior)
            {
                case Behavior.Ballistic:
                    LaunchBallistic(worm, def, input);
                    break;
                case Behavior.Placed:
                    PlaceCharge(worm, def);
                    break;
                case Behavior.Hitscan:
                    if (def.Burst > 1)
                    {
                        _burstLeft = def.Burst;
                        _burstTimer = 0;
                        _burstAim = worm.Aim;
                        return; // bullets come out over the next ticks (StepBurst)
                    }
                    FireBullet(worm, def, worm.Aim);
                    break;
                case Behavior.Melee:
                    Swing(worm, def);
                    break;
                case Behavior.Airstrike:
                    CallAirstrike(worm, def, input.TargetX);
                    break;
            }
            if (_shotsLeft <= 0) EndAttack();
        }

        bool ValidTarget(SimInput input)
        {
            return !float.IsNaN(input.TargetX) && !float.IsInfinity(input.TargetX) && input.TargetX >= 0 && input.TargetX <= Terrain.Width;
        }

        /// <summary>The attack is over: the worm gets its retreat time while shots are still flying.</summary>
        void EndAttack()
        {
            _shotsLeft = 0;
            _burstLeft = 0;
            RetreatTicksLeft = C.RetreatTicks;
            SetPhase(Phase.Retreat);
        }

        Vec2 AimDirection(Worm worm, float aim)
        {
            return new Vec2(worm.Facing * (float)Math.Cos(aim), -(float)Math.Sin(aim));
        }

        Projectile Spawn(WeaponDef def, int owner, Vec2 pos, Vec2 vel, int fuseSeconds)
        {
            var p = new Projectile
            {
                Id = NewEntityId(),
                Weapon = def,
                OwnerWorm = owner,
                FuseTicks = def.TimerFuse ? fuseSeconds * C.TicksPerSecond : -1,
                Age = -1, // the firing tick does not count toward the fuse
                Body = new Body(pos, def.ProjectileRadius, def.Restitution, def.Friction, def.WindFactor),
            };
            p.Body.Vel = vel;
            Projectiles.Add(p);
            return p;
        }

        void LaunchBallistic(Worm worm, WeaponDef def, SimInput input)
        {
            float power = float.IsNaN(input.Power) ? 0 : Math.Max(0f, Math.Min(1f, input.Power));
            var dir = AimDirection(worm, worm.Aim);
            int fuse = Math.Max(1, Math.Min(5, input.Fuse == 0 ? 3 : input.Fuse));
            var p = Spawn(def, worm.Id, worm.Pos + dir * C.MuzzleOffset, dir * (def.MaxSpeed * power), fuse);
            // Muzzle already inside rock: point-blank explosion.
            if (Terrain.OverlapsCircle(p.Pos, p.Body.Radius)) ExplodeProjectile(p);
        }

        void PlaceCharge(Worm worm, WeaponDef def)
        {
            var pos = worm.Pos + new Vec2(worm.Facing * (C.WormRadius * 0.5f), C.WormRadius - def.ProjectileRadius);
            var p = Spawn(def, worm.Id, pos, new Vec2(worm.Facing * def.MaxSpeed, 0), def.FixedFuse);
            Physics.Depenetrate(p.Body, Terrain);
        }

        void StepBurst(Worm worm)
        {
            if (worm == null || !worm.Alive)
            {
                _burstLeft = 0;
                EndAttack();
                return;
            }
            if (_burstTimer-- > 0) return;
            var def = Weapons.Get(SelectedWeapon[ActiveTeam]);
            _burstTimer = def.BurstIntervalTicks - 1;
            float aim = _burstAim + Rng.Range(-def.Spread, def.Spread);
            FireBullet(worm, def, aim);
            if (--_burstLeft <= 0) EndAttack();
        }

        void FireBullet(Worm worm, WeaponDef def, float aim)
        {
            var dir = AimDirection(worm, aim);
            var from = worm.Pos + dir * C.MuzzleOffset;
            var end = from + dir * def.Range;
            Worm target = null;
            bool hit = false;
            for (float d = 0; d < def.Range; d += 1f)
            {
                var p = from + dir * d;
                if (Terrain.IsSolid((int)Math.Floor(p.X), (int)Math.Floor(p.Y)))
                {
                    end = p;
                    hit = true;
                    break;
                }
                foreach (var w in Worms)
                {
                    if (!w.Alive || w == worm) continue;
                    if ((w.Pos - p).LengthSq <= C.WormRadius * C.WormRadius)
                    {
                        target = w;
                        break;
                    }
                }
                if (target != null)
                {
                    end = p;
                    hit = true;
                    break;
                }
                if (p.Y > WaterLevel || p.X < -200 || p.X > Terrain.Width + 200) break;
            }
            Emit(new SimEvent { Type = SimEventType.Shot, Worm = worm.Id, Weapon = def.Id, X = end.X, Y = end.Y });
            if (!hit) return;

            if (def.Explodes)
            {
                // A body hit blasts from the worm's center, so it takes the full damage.
                Explosion.Detonate(this, target != null ? target.Pos : end, def.BlastRadius, def.MaxDamage);
            }
            else if (target != null)
            {
                target.PendingDamage += def.HitDamage;
                target.Knock(dir * (C.Knockback * def.HitDamage * 0.5f));
                Emit(new SimEvent { Type = SimEventType.Hit, Worm = target.Id, Amount = def.HitDamage, X = dir.X, Y = dir.Y });
            }
        }

        void Swing(Worm worm, WeaponDef def)
        {
            var dir = AimDirection(worm, worm.Aim);
            float reach = def.MeleeReach + C.WormRadius * 2f;
            foreach (var w in Worms)
            {
                if (!w.Alive || w == worm) continue;
                var delta = w.Pos - worm.Pos;
                float dist = delta.Length;
                if (dist > reach) continue;
                // 90 degree cone in the aim direction, or touching.
                if (dist > C.WormRadius && Vec2.Dot(delta / dist, dir) < 0.707f) continue;
                w.PendingDamage += def.HitDamage;
                w.Body.Vel = Vec2.Zero;
                w.Knock(dir * def.LaunchSpeed);
                Emit(new SimEvent { Type = SimEventType.Hit, Worm = w.Id, Amount = def.HitDamage, X = dir.X, Y = dir.Y });
            }
        }

        void CallAirstrike(Worm worm, WeaponDef def, float targetX)
        {
            // Missiles come in from the side the worm faces and drift onto the target.
            float drift = worm.Facing * def.MaxSpeed * 1.4f;
            for (int i = 0; i < def.Missiles; i++)
            {
                float x = targetX + (i - (def.Missiles - 1) / 2f) * def.MissileSpacing - drift;
                Spawn(def, worm.Id, new Vec2(x, -40f - i * 6f), new Vec2(worm.Facing * def.MaxSpeed, 150f), 0);
            }
        }

        void SpawnFragments(Projectile parent)
        {
            var def = parent.Weapon;
            for (int i = 0; i < def.Fragments; i++)
            {
                float angle = Rng.Range(-2.6f, -0.55f); // upward fan (Y is down)
                float speed = Rng.Range(180f, 320f);
                var vel = new Vec2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
                var f = Spawn(def.Fragment, parent.OwnerWorm, parent.Pos + new Vec2(0, -3), vel, 0);
                f.Age = C.OwnerImmunityTicks; // fragments may hit anyone, including the thrower
            }
        }
    }
}
