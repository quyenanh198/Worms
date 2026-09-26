using System;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>What the procedural animation needs to know about a worm this frame.</summary>
    public struct WormAnimInput
    {
        public WormState State;
        /// <summary>Elevation of the aim, -PI/2..PI/2 (relative to facing).</summary>
        public float Aim;
        /// <summary>True while this worm has the turn and a weapon out.</summary>
        public bool Holding;
        public float Vx, Vy;
        public float Time;
    }

    /// <summary>
    /// Procedural worm (docs/PLAN.md §3.10): a spine of points, each with a
    /// radius, posed per state (idle, crawl, jump, aim, tumble) and blended
    /// smoothly toward the target pose. Coordinates are simulation units in
    /// the worm's local frame: +X forward (the way it faces), +Y up, origin at
    /// the collision circle's center (ground is at Y = -WormRadius).
    /// </summary>
    public sealed class WormRig
    {
        public const int Points = 10;
        const float BlendRate = 14f;

        public readonly float[] X = new float[Points];
        public readonly float[] Y = new float[Points];
        public readonly float[] R = new float[Points];
        readonly float[] _tx = new float[Points], _ty = new float[Points], _tr = new float[Points];
        bool _initialized;

        public float HeadX => X[Points - 1];
        public float HeadY => Y[Points - 1];
        public float HeadR => R[Points - 1];

        /// <summary>Unit direction the head points (from the previous spine point).</summary>
        public void HeadDirection(out float dx, out float dy)
        {
            dx = X[Points - 1] - X[Points - 2];
            dy = Y[Points - 1] - Y[Points - 2];
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) { dx = 0; dy = 1; return; }
            dx /= len;
            dy /= len;
        }

        public void Update(WormAnimInput a, float dt)
        {
            Pose(a);
            if (!_initialized)
            {
                Array.Copy(_tx, X, Points);
                Array.Copy(_ty, Y, Points);
                Array.Copy(_tr, R, Points);
                _initialized = true;
                return;
            }
            float k = 1f - (float)Math.Exp(-BlendRate * dt);
            for (int i = 0; i < Points; i++)
            {
                X[i] += (_tx[i] - X[i]) * k;
                Y[i] += (_ty[i] - Y[i]) * k;
                R[i] += (_tr[i] - R[i]) * k;
            }
        }

        void Pose(WormAnimInput a)
        {
            float ground = -C.WormRadius;
            float breathe = 1f + 0.035f * (float)Math.Sin(a.Time * 2.2f);
            for (int i = 0; i < Points; i++)
            {
                float s = (float)i / (Points - 1);
                // Radius: thin tail, fat body, round head.
                _tr[i] = 2.1f + 2.0f * Smooth(0f, 0.55f, s) + (i == Points - 1 ? 0.6f : 0f);

                if (a.State == WormState.Tumbling)
                {
                    // Curled into a ball; the view spins it.
                    double angle = -Math.PI / 2 + s * Math.PI * 1.7;
                    _tx[i] = 4.8f * (float)Math.Cos(angle);
                    _ty[i] = 4.8f * (float)Math.Sin(angle);
                    _tr[i] *= 0.85f;
                    continue;
                }

                // Idle: tail flat on the ground behind, neck rising to the head.
                float x, y;
                if (s < 0.45f)
                {
                    float t = s / 0.45f;
                    x = -10f + 8f * t;
                    y = ground + _tr[i];
                }
                else
                {
                    float t = (s - 0.45f) / 0.55f;
                    x = -2f + 2.6f * (float)Math.Sin(t * Math.PI / 2);
                    y = ground + 3.6f + 13f * t * breathe;
                }

                if (a.State == WormState.Walking)
                {
                    // Crawl: a wave runs along the body, the head bobs.
                    float wave = (float)Math.Sin(a.Time * 12f - s * 7f);
                    y += wave * 1.4f * (1f - s * 0.6f);
                    x += (float)Math.Cos(a.Time * 12f - s * 7f) * 0.6f;
                }
                else if (a.State == WormState.Airborne)
                {
                    // Stretch going up, squash coming down.
                    float stretch = Clamp(-a.Vy / 700f, -0.25f, 0.3f);
                    y = ground + (y - ground) * (1f + stretch);
                    x *= 1f - stretch * 0.5f;
                }

                if (a.Holding && s > 0.45f)
                {
                    // Lean the upper body toward the aim: vertical at +90 degrees, forward at 0.
                    float t = (s - 0.45f) / 0.55f;
                    float lean = (float)(Math.PI / 2 - a.Aim) * 0.55f * t;
                    float bx = -2f, by = ground + 3.6f;
                    float dx = x - bx, dy = y - by;
                    float cos = (float)Math.Cos(-lean), sin = (float)Math.Sin(-lean);
                    x = bx + dx * cos - dy * sin;
                    y = by + dx * sin + dy * cos;
                }
                _tx[i] = x;
                _ty[i] = y;
            }
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }

        static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>
        /// Tube mesh around the spine, in world units (sim units x <paramref name="scale"/>),
        /// with rounded head and tail caps. Rebuilt every frame; about 150 vertices.
        /// </summary>
        public void BuildMesh(MeshBuffers m, float scale, int sides = 12)
        {
            m.Clear();
            int rings = 0;
            // Tail cap, spine, head cap: a sequence of (center, tangent, radius).
            void Ring(float cx, float cy, float tx, float ty, float r)
            {
                float nx = -ty, ny = tx; // in-plane normal
                for (int k = 0; k <= sides; k++)
                {
                    double th = 2 * Math.PI * k / sides;
                    float c = (float)Math.Cos(th), s = (float)Math.Sin(th);
                    float ox = nx * c, oy = ny * c, oz = s;
                    m.AddVertex((cx + ox * r) * scale, (cy + oy * r) * scale, oz * r * scale, ox, oy, oz, (float)k / sides, rings);
                }
                rings++;
            }

            Tangent(0, out float t0x, out float t0y);
            for (int c = 3; c >= 1; c--)
            {
                float f = c / 4f;
                float back = R[0] * f;
                Ring(X[0] - t0x * back, Y[0] - t0y * back, t0x, t0y, R[0] * (float)Math.Sqrt(1 - f * f));
            }
            for (int i = 0; i < Points; i++)
            {
                Tangent(i, out float tx, out float ty);
                Ring(X[i], Y[i], tx, ty, R[i]);
            }
            Tangent(Points - 1, out float hx, out float hy);
            for (int c = 1; c <= 3; c++)
            {
                float f = c / 4f;
                float fwd = R[Points - 1] * f;
                Ring(X[Points - 1] + hx * fwd, Y[Points - 1] + hy * fwd, hx, hy, R[Points - 1] * (float)Math.Sqrt(1 - f * f));
            }

            int row = sides + 1;
            for (int r = 0; r < rings - 1; r++)
                for (int k = 0; k < sides; k++)
                {
                    int a = r * row + k, b = a + 1, c = a + row, d = c + 1;
                    m.AddTriangle(a, b, c);
                    m.AddTriangle(b, d, c);
                }
        }

        void Tangent(int i, out float tx, out float ty)
        {
            int a = Math.Max(0, i - 1), b = Math.Min(Points - 1, i + 1);
            tx = X[b] - X[a];
            ty = Y[b] - Y[a];
            float len = (float)Math.Sqrt(tx * tx + ty * ty);
            if (len < 1e-5f) { tx = 1; ty = 0; return; }
            tx /= len;
            ty /= len;
        }
    }
}
