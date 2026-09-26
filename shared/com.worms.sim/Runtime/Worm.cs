using System;

namespace Worms.Sim
{
    public enum WormState : byte
    {
        Idle = 0,
        Walking = 1,
        /// <summary>Jumping or falling: lands without bouncing on a floor.</summary>
        Airborne = 2,
        /// <summary>Knocked back: bounces and rolls until it slows down.</summary>
        Tumbling = 3,
        Dead = 4,
    }

    public sealed class Worm
    {
        public int Id;
        public int Team;
        public Body Body;
        public int Hp = C.StartHp;
        /// <summary>Damage taken this turn, applied to Hp at the end of the turn.</summary>
        public int PendingDamage;
        public int Facing = 1;
        public float Aim;
        public WormState State;
        public bool Alive = true;

        float _walkAccum;
        int _slowTicks;

        public Vec2 Pos => Body.Pos;
        public bool IsGrounded => State == WormState.Idle || State == WormState.Walking;

        public Worm(int id, int team, Vec2 pos)
        {
            Id = id;
            Team = team;
            Body = new Body(pos, C.WormRadius, C.WormRestitution, C.WormFriction, 0f);
        }

        public static bool IsStanding(Terrain t, Vec2 p)
        {
            return !t.OverlapsCircle(p, C.WormRadius) && t.OverlapsCircle(p + new Vec2(0, 1), C.WormRadius);
        }

        /// <summary>Walks up to one tick's worth, one cell at a time, following slopes up to MaxClimb.</summary>
        public void Walk(Terrain t, int dir)
        {
            if (dir == 0 || !IsGrounded) return;
            Facing = dir;
            _walkAccum += C.WalkSpeed * C.Dt;
            State = WormState.Walking;
            while (_walkAccum >= 1f)
            {
                _walkAccum -= 1f;
                float nx = Body.Pos.X + dir;
                bool free = false;
                for (int dy = -C.MaxClimb; dy <= C.MaxClimb; dy++)
                {
                    var p = new Vec2(nx, Body.Pos.Y + dy);
                    if (t.OverlapsCircle(p, C.WormRadius)) continue;
                    free = true;
                    if (t.OverlapsCircle(p + new Vec2(0, 1), C.WormRadius))
                    {
                        // A round body would roll up any ledge lower than its radius.
                        // Only accept the spot if the ground under the worm's middle
                        // is within MaxClimb of its underside; otherwise it is
                        // perched on a corner.
                        if (FootGap(t, p) > C.MaxClimb)
                        {
                            if (dy < 0) { _walkAccum = 0; return; }
                            break; // going down a steep drop: walk off the edge
                        }
                        Body.Pos = p;
                        goto nextStep;
                    }
                }
                if (!free)
                {
                    _walkAccum = 0; // blocked by a wall or a slope that is too steep
                    return;
                }
                // Walked off an edge.
                Body.Pos = new Vec2(nx, Body.Pos.Y);
                Body.Vel = new Vec2(dir * C.WalkSpeed, 0);
                State = WormState.Airborne;
                _walkAccum = 0;
                return;
            nextStep:;
            }
        }

        /// <summary>Empty cells between the bottom of the worm and the ground straight below its center.</summary>
        static int FootGap(Terrain t, Vec2 p)
        {
            int x = (int)Math.Floor(p.X);
            int y0 = (int)Math.Floor(p.Y + C.WormRadius);
            for (int gap = 0; gap <= C.MaxClimb + 1; gap++)
                if (t.IsSolid(x, y0 + gap)) return gap;
            return C.MaxClimb + 2;
        }

        public void StopWalking()
        {
            if (State == WormState.Walking) State = WormState.Idle;
            _walkAccum = 0;
        }

        public bool Jump(bool backflip)
        {
            if (!IsGrounded) return false;
            Body.Vel = backflip
                ? new Vec2(-Facing * C.BackflipVx, C.BackflipVy)
                : new Vec2(Facing * C.JumpVx, C.JumpVy);
            State = WormState.Airborne;
            _walkAccum = 0;
            return true;
        }

        /// <summary>Starts tumbling with an added velocity (explosions, melee).</summary>
        public void Knock(Vec2 velocity)
        {
            Body.Vel += velocity;
            State = WormState.Tumbling;
            _slowTicks = 0;
        }

        /// <summary>Per-tick physics. Returns fall damage taken on landing (0 if none).</summary>
        public int Update(World w)
        {
            if (!Alive) return 0;
            var t = w.Terrain;

            if (IsGrounded)
            {
                if (!IsStanding(t, Body.Pos))
                {
                    if (t.OverlapsCircle(Body.Pos, C.WormRadius)) Physics.Depenetrate(Body, t);
                    if (!IsStanding(t, Body.Pos))
                    {
                        State = WormState.Airborne;
                        Body.Vel = Vec2.Zero;
                    }
                }
                if (IsGrounded) return 0;
            }

            var hit = Physics.Step(Body, t, 0f);
            if (!hit.Hit) return 0;

            int fallDamage = 0;
            if (hit.ImpactSpeed > C.SafeFallSpeed)
                fallDamage = (int)Math.Ceiling((hit.ImpactSpeed - C.SafeFallSpeed) / C.FallDamageDivisor);

            bool floor = hit.Normal.Y < -0.6f;
            bool slow = Body.Vel.Length < C.LandSpeed;
            if (floor && (State == WormState.Airborne || slow))
            {
                Land(t);
            }
            else if (slow)
            {
                // Wedged in a crevice or creeping down a steep slope: after a
                // while it counts as standing there, like worms stick in Worms.
                if (++_slowTicks >= C.StuckTicks && IsStanding(t, Body.Pos)) Land(t);
            }
            else
            {
                _slowTicks = 0;
            }
            return fallDamage;
        }

        void Land(Terrain t)
        {
            Body.Vel = Vec2.Zero;
            // Settle onto the surface: the contact may have left a small gap.
            for (int i = 0; i < 4 && !IsStanding(t, Body.Pos) && !t.OverlapsCircle(Body.Pos + new Vec2(0, 1), C.WormRadius); i++)
                Body.Pos += new Vec2(0, 1);
            if (IsStanding(t, Body.Pos)) State = WormState.Idle;
            _slowTicks = 0;
        }
    }
}
