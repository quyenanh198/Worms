using System;

namespace Worms.Sim
{
    /// <summary>A moving circle: worms, projectiles and gravestones are all bodies.</summary>
    public sealed class Body
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public float Radius;
        public float Restitution;
        public float Friction;
        /// <summary>How much of the wind acceleration applies (1 = full, 0 = none).</summary>
        public float WindFactor;

        public Body(Vec2 pos, float radius, float restitution, float friction, float windFactor)
        {
            Pos = pos;
            Radius = radius;
            Restitution = restitution;
            Friction = friction;
            WindFactor = windFactor;
        }
    }

    public struct StepResult
    {
        public bool Hit;
        public Vec2 Normal;
        /// <summary>Speed along the contact normal just before the hit (&gt;= 0).</summary>
        public float ImpactSpeed;
    }

    public static class Physics
    {
        /// <summary>
        /// Advances a body by one tick: semi-implicit Euler, sub-stepped so it
        /// never moves more than 1 u at a time (no tunnelling). On contact the
        /// body stays at its last free position and its velocity is reflected:
        /// v' = -e * vn + (1 - mu) * vt.
        /// </summary>
        public static StepResult Step(Body b, Terrain t, float wind)
        {
            var result = new StepResult();
            b.Vel += new Vec2(wind * b.WindFactor, C.Gravity) * C.Dt;
            Vec2 delta = b.Vel * C.Dt;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length));
            Vec2 step = delta / steps;

            for (int i = 0; i < steps; i++)
            {
                Vec2 next = b.Pos + step;
                if (!t.OverlapsCircle(next, b.Radius))
                {
                    b.Pos = next;
                    continue;
                }
                Vec2 n = t.NormalAt(next, b.Radius + 1f);
                float vn = Vec2.Dot(b.Vel, n);
                result.Hit = true;
                result.Normal = n;
                result.ImpactSpeed = Math.Max(0f, -vn);
                if (vn < 0)
                {
                    Vec2 normalPart = n * vn;
                    Vec2 tangentPart = b.Vel - normalPart;
                    b.Vel = -b.Restitution * normalPart + (1f - b.Friction) * tangentPart;
                }
                Depenetrate(b, t);
                return result;
            }
            return result;
        }

        /// <summary>Pushes a body out of the terrain along the local normal.</summary>
        public static void Depenetrate(Body b, Terrain t)
        {
            for (int i = 0; i < 64 && t.OverlapsCircle(b.Pos, b.Radius); i++)
                b.Pos += t.NormalAt(b.Pos, b.Radius + 1f) * 0.5f;
        }
    }
}
