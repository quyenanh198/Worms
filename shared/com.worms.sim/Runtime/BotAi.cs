using System;
using System.Collections.Generic;

namespace Worms.Sim
{
    /// <summary>What a bot decided to do on its turn.</summary>
    public sealed class BotPlan
    {
        public WeaponId Weapon;
        /// <summary>-1 or 1: which way the worm must face before firing.</summary>
        public int Facing;
        /// <summary>Elevation relative to facing, radians (-PI/2 .. PI/2).</summary>
        public float Angle;
        /// <summary>Ballistic launch power, 0..1.</summary>
        public float Power;
        /// <summary>Fuse seconds for timer weapons.</summary>
        public int Fuse;
        public float Score;
        /// <summary>Where the bot expects the blast (null when it only swings or misses).</summary>
        public Vec2? Expected;
    }

    /// <summary>
    /// A computer player. It tries shots against a frozen copy of the match with the real
    /// projectile physics (<see cref="Physics.Step"/>, wind included) and picks the one
    /// that hurts enemies most without hurting its own team, then misses by a little on
    /// purpose so it plays like a person rather than an aimbot.
    /// Deterministic for a given <see cref="Rng"/>; safe to run off the match loop because
    /// it only reads its own <see cref="View"/>.
    /// </summary>
    public static class BotAi
    {
        /// <summary>A worm as the bot sees it when it starts thinking.</summary>
        public struct Target
        {
            public int Id;
            public int Team;
            public Vec2 Pos;
            /// <summary>Health left once this turn's pending damage lands.</summary>
            public int Hp;
        }

        /// <summary>Everything the bot needs, copied out of the live world.</summary>
        public sealed class View
        {
            public Terrain Terrain;
            public float Wind;
            public float WaterLevel;
            public int Team;
            public int ShooterId;
            public Vec2 From;
            public int Facing;
            public int[] Ammo;
            public readonly List<Target> Worms = new List<Target>();
        }

        const float Deg = (float)(Math.PI / 180.0);
        const int MaxFlightTicks = (int)(8 * C.TicksPerSecond / C.ProjectileTimeScale);
        /// <summary>Hurting its own team costs twice what hurting an enemy earns.</summary>
        const float FriendlyFireWeight = 2f;
        const float KillBonus = 25f;
        const float OwnKillPenalty = 60f;

        public static View Capture(World w)
        {
            var active = w.Active;
            var v = new View
            {
                Terrain = w.Terrain.Clone(),
                Wind = w.Wind,
                WaterLevel = w.WaterLevel,
                Team = w.ActiveTeam,
                ShooterId = active != null ? active.Id : -1,
                From = active != null ? active.Pos : Vec2.Zero,
                Facing = active != null ? active.Facing : 1,
                Ammo = (int[])w.Ammo[w.ActiveTeam].Clone(),
            };
            foreach (var worm in w.Worms)
            {
                if (!worm.Alive) continue;
                v.Worms.Add(new Target { Id = worm.Id, Team = worm.Team, Pos = worm.Pos, Hp = Math.Max(1, worm.Hp - worm.PendingDamage) });
            }
            return v;
        }

        /// <summary>
        /// The bot's move for this turn. <paramref name="aimError"/> is the most it may miss
        /// by, in radians (0 = perfect).
        /// </summary>
        public static BotPlan Plan(View v, Rng rng, float aimError = 2f * Deg)
        {
            var candidates = new List<BotPlan>();
            if (HasAmmo(v, WeaponId.BaseballBat)) AddMelee(v, candidates);
            AddShotgun(v, candidates);
            AddBallistic(v, WeaponId.Bazooka, 0, candidates);
            AddBallistic(v, WeaponId.Grenade, 3, candidates);

            BotPlan best = null;
            foreach (var c in candidates) if (best == null || c.Score > best.Score) best = c;
            if (best == null)
            {
                // Nothing reachable at all (boxed in): lob a rocket straight up-forward rather than stand still.
                return new BotPlan { Weapon = WeaponId.Bazooka, Facing = v.Facing, Angle = 45f * Deg, Power = 0.4f, Score = 0 };
            }

            // Vary play a little: pick among the moves nearly as good as the best one.
            var close = candidates.FindAll(c => c.Score >= best.Score - Math.Max(3f, Math.Abs(best.Score) * 0.1f));
            var pick = close[rng.Range(0, close.Count)];

            // Miss on purpose, but never enough to put a far shot onto the bot itself: the
            // error only applies to shots that land well away from its own worms.
            if (aimError > 0 && pick.Weapon != WeaponId.BaseballBat && FarFromOwnTeam(v, pick))
            {
                pick.Angle = ClampAim(pick.Angle + rng.Range(-aimError, aimError));
                if (pick.Power > 0) pick.Power = Clamp01(pick.Power + rng.Range(-0.02f, 0.02f));
            }
            return pick;
        }

        // ---- scoring ----------------------------------------------------------------

        /// <summary>
        /// Value of a blast at <paramref name="c"/>, using the same falloff as
        /// <see cref="Explosion.Detonate"/>. A small bonus for landing near an enemy breaks
        /// ties between complete misses, so the bot keeps pressing instead of idling.
        /// </summary>
        static float BlastScore(View v, Vec2 c, float radius, int maxDamage)
        {
            float score = 0;
            float nearestEnemy = float.MaxValue;
            foreach (var t in v.Worms)
            {
                float dist = Vec2.Distance(t.Pos, c);
                if (t.Team != v.Team) nearestEnemy = Math.Min(nearestEnemy, dist);
                float d = Math.Max(0f, dist - C.WormRadius * 0.5f);
                if (d >= radius) continue;
                int dmg = (int)Math.Round(maxDamage * (1f - d / radius));
                if (dmg <= 0) continue;
                float dealt = Math.Min(dmg, t.Hp);
                if (t.Team != v.Team)
                {
                    score += dealt;
                    if (dmg >= t.Hp) score += KillBonus;
                }
                else
                {
                    score -= dealt * FriendlyFireWeight;
                    if (dmg >= t.Hp) score -= OwnKillPenalty;
                }
            }
            if (nearestEnemy < 400f) score += 3f * (1f - nearestEnemy / 400f);
            return score;
        }

        static float HitScore(View v, Target t, int damage)
        {
            if (t.Team == v.Team) return -damage * FriendlyFireWeight - (damage >= t.Hp ? OwnKillPenalty : 0);
            return Math.Min(damage, t.Hp) + (damage >= t.Hp ? KillBonus : 0);
        }

        // ---- ballistic: bazooka, grenade --------------------------------------------

        static void AddBallistic(View v, WeaponId id, int fuse, List<BotPlan> into)
        {
            if (!HasAmmo(v, id)) return;
            var def = Weapons.Get(id);
            var coarse = new List<BotPlan>();
            foreach (int facing in new[] { -1, 1 })
            {
                for (float a = -60f; a <= 85f; a += 5f)
                    for (float p = 0.3f; p <= 1.001f; p += 0.1f)
                        Try(v, def, facing, a * Deg, p, fuse, coarse);
            }
            if (coarse.Count == 0) return;

            // Refine around the best few coarse shots: a finer grid only where it pays.
            coarse.Sort((x, y) => y.Score.CompareTo(x.Score));
            var refined = new List<BotPlan>(coarse.GetRange(0, Math.Min(12, coarse.Count)));
            for (int i = 0; i < Math.Min(5, coarse.Count); i++)
            {
                var c = coarse[i];
                for (float da = -4f; da <= 4.01f; da += 1f)
                    for (float dp = -0.08f; dp <= 0.081f; dp += 0.02f)
                        Try(v, def, c.Facing, ClampAim(c.Angle + da * Deg), Clamp01(c.Power + dp), fuse, refined);
            }
            into.AddRange(refined);
        }

        static void Try(View v, WeaponDef def, int facing, float angle, float power, int fuse, List<BotPlan> into)
        {
            var landing = Fly(v, def, facing, angle, power, fuse);
            if (landing == null) return; // lost in the water or off the map
            into.Add(new BotPlan
            {
                Weapon = def.Id, Facing = facing, Angle = angle, Power = power, Fuse = fuse,
                Score = BlastScore(v, landing.Value, def.BlastRadius, def.MaxDamage), Expected = landing,
            });
        }

        /// <summary>
        /// Where a shot explodes, replaying <c>World.LaunchBallistic</c> and
        /// <c>World.StepProjectiles</c> against the frozen terrain. Null when it is lost.
        /// </summary>
        public static Vec2? Fly(View v, WeaponDef def, int facing, float angle, float power, int fuse)
        {
            var dir = new Vec2(facing * (float)Math.Cos(angle), -(float)Math.Sin(angle));
            var start = v.From + dir * C.MuzzleOffset;
            if (v.Terrain.OverlapsCircle(start, def.ProjectileRadius)) return start; // point blank
            var body = new Body(start, def.ProjectileRadius, def.Restitution, def.Friction, def.WindFactor) { Vel = dir * (def.MaxSpeed * power) };
            int fuseTicks = def.TimerFuse ? Math.Max(1, Math.Min(5, fuse == 0 ? 3 : fuse)) * C.TicksPerSecond : -1;

            for (int age = 0; age < MaxFlightTicks; age++)
            {
                var hit = Physics.Step(body, v.Terrain, v.Wind, C.ProjectileDt);
                if (fuseTicks >= 0)
                {
                    if (age >= fuseTicks) return body.Pos;
                }
                else if (hit.Hit || HitsWorm(v, body, age))
                {
                    return body.Pos;
                }
                if (body.Pos.Y > v.WaterLevel || body.Pos.X < -500 || body.Pos.X > v.Terrain.Width + 500) return null;
            }
            return null;
        }

        static bool HitsWorm(View v, Body b, int age)
        {
            foreach (var t in v.Worms)
            {
                if (t.Id == v.ShooterId && age < C.OwnerImmunityTicks) continue;
                float r = C.WormRadius + b.Radius;
                if ((t.Pos - b.Pos).LengthSq <= r * r) return true;
            }
            return false;
        }

        // ---- shotgun: straight line of sight ----------------------------------------

        static void AddShotgun(View v, List<BotPlan> into)
        {
            if (!HasAmmo(v, WeaponId.Shotgun)) return;
            var def = Weapons.Get(WeaponId.Shotgun);
            foreach (var t in v.Worms)
            {
                if (t.Team == v.Team) continue;
                var delta = t.Pos - v.From;
                if (delta.Length > def.Range || Math.Abs(delta.X) < 1f) continue;
                int facing = delta.X > 0 ? 1 : -1;
                float angle = (float)Math.Atan2(-delta.Y, Math.Abs(delta.X));
                if (Math.Abs(angle) > Math.PI / 2) continue;
                var victim = FirstInLine(v, facing, angle, def.Range);
                if (victim == null) continue;
                // Two shots; the second often misses because the first knocks the target away.
                float score = BlastScore(v, victim.Value.Pos, def.BlastRadius, def.MaxDamage) * 1.4f;
                into.Add(new BotPlan { Weapon = WeaponId.Shotgun, Facing = facing, Angle = angle, Score = score, Expected = victim.Value.Pos });
            }
        }

        /// <summary>The first worm a bullet would reach, replaying <c>World.FireBullet</c>'s march; null if rock comes first.</summary>
        static Target? FirstInLine(View v, int facing, float angle, float range)
        {
            var dir = new Vec2(facing * (float)Math.Cos(angle), -(float)Math.Sin(angle));
            var from = v.From + dir * C.MuzzleOffset;
            for (float d = 0; d < range; d += 1f)
            {
                var p = from + dir * d;
                if (v.Terrain.IsSolid((int)Math.Floor(p.X), (int)Math.Floor(p.Y))) return null;
                foreach (var t in v.Worms)
                {
                    if (t.Id == v.ShooterId) continue;
                    if ((t.Pos - p).LengthSq <= C.WormRadius * C.WormRadius) return t;
                }
                if (p.Y > v.WaterLevel) return null;
            }
            return null;
        }

        // ---- baseball bat -----------------------------------------------------------

        static void AddMelee(View v, List<BotPlan> into)
        {
            var def = Weapons.Get(WeaponId.BaseballBat);
            float reach = def.MeleeReach + C.WormRadius * 2f;
            foreach (var t in v.Worms)
            {
                if (t.Team == v.Team) continue;
                var delta = t.Pos - v.From;
                if (delta.Length > reach * 0.9f) continue;
                int facing = delta.X >= 0 ? 1 : -1;
                // Swing a little upward: the launch is what makes the bat worth a turn.
                float angle = ClampAim((float)Math.Atan2(-delta.Y, Math.Abs(delta.X)) + 15f * Deg);
                var dir = new Vec2(facing * (float)Math.Cos(angle), -(float)Math.Sin(angle));
                float score = 0;
                foreach (var other in v.Worms)
                {
                    if (other.Id == v.ShooterId) continue;
                    var dd = other.Pos - v.From;
                    float dist = dd.Length;
                    if (dist > reach) continue;
                    if (dist > C.WormRadius && Vec2.Dot(dd / dist, dir) < 0.707f) continue;
                    score += HitScore(v, other, def.HitDamage);
                }
                // A 700 u/s launch often ends in the water or far from cover.
                if (score > 0) score += 15f;
                into.Add(new BotPlan { Weapon = WeaponId.BaseballBat, Facing = facing, Angle = angle, Score = score });
            }
        }

        // ---- helpers ----------------------------------------------------------------

        static bool HasAmmo(View v, WeaponId id)
        {
            return v.Ammo[(int)id] != 0;
        }

        static bool FarFromOwnTeam(View v, BotPlan plan)
        {
            if (plan.Expected == null) return true;
            foreach (var t in v.Worms)
                if (t.Team == v.Team && Vec2.Distance(t.Pos, plan.Expected.Value) < 150f) return false;
            return true;
        }

        static float ClampAim(float a)
        {
            const float half = (float)(Math.PI / 2);
            return Math.Max(-half, Math.Min(half, a));
        }

        static float Clamp01(float x)
        {
            return Math.Max(0f, Math.Min(1f, x));
        }
    }
}
