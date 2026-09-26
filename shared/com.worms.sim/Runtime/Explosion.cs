using System;

namespace Worms.Sim
{
    public struct CarveOp
    {
        public short X;
        public short Y;
        public short R;
    }

    public static class Explosion
    {
        /// <summary>
        /// Carves the terrain, damages and knocks back worms in range:
        /// dmg = round(maxDamage * (1 - d / r)), velocity += dir * Knockback * dmg,
        /// with dir tilted upward (KnockbackMinUp) so worms always get thrown up.
        /// </summary>
        public static void Detonate(World w, Vec2 c, float radius, int maxDamage, int excludeWorm = -1)
        {
            var op = new CarveOp
            {
                X = (short)Math.Round(c.X),
                Y = (short)Math.Round(c.Y),
                R = (short)Math.Round(radius),
            };
            w.ApplyCarve(op);
            w.Emit(new SimEvent { Type = SimEventType.Explode, X = c.X, Y = c.Y, Value = radius });

            foreach (var worm in w.Worms)
            {
                if (!worm.Alive || worm.Id == excludeWorm) continue;
                float d = Math.Max(0f, Vec2.Distance(worm.Pos, c) - C.WormRadius * 0.5f);
                if (d >= radius) continue;
                int dmg = (int)Math.Round(maxDamage * (1f - d / radius));
                if (dmg <= 0) continue;

                Vec2 dir = (worm.Pos - c).Normalized;
                if (dir.LengthSq == 0) dir = Vec2.Up;
                if (dir.Y > -C.KnockbackMinUp) dir = new Vec2(dir.X, -C.KnockbackMinUp).Normalized;
                worm.PendingDamage += dmg;
                worm.Knock(dir * (C.Knockback * dmg));
                w.Emit(new SimEvent { Type = SimEventType.Hit, Worm = worm.Id, Amount = dmg, X = dir.X, Y = dir.Y });
            }

            // Grenades and other loose projectiles get pushed too.
            foreach (var p in w.Projectiles)
            {
                if (!p.Alive || !p.Weapon.TimerFuse) continue;
                float d = Vec2.Distance(p.Pos, c);
                if (d >= radius) continue;
                Vec2 dir = (p.Pos - c).Normalized;
                if (dir.LengthSq == 0) dir = Vec2.Up;
                p.Body.Vel += dir * (C.Knockback * maxDamage * (1f - d / radius));
            }
        }
    }
}
