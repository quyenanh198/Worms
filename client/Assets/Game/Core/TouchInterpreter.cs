using System;
using System.Collections.Generic;

namespace Worms.Game.Core
{
    public enum TouchPhase2
    {
        Began,
        Moved,
        Stationary,
        Ended,
    }

    public struct TouchPoint
    {
        public int Id;
        /// <summary>Screen pixels, origin bottom-left (Unity's touch coordinates).</summary>
        public float X, Y;
        public TouchPhase2 Phase;
    }

    public struct RectF
    {
        public float X, Y, W, H;

        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public bool Contains(float px, float py) { return px >= X && px <= X + W && py >= Y && py <= Y + H; }
    }

    /// <summary>On-screen buttons, in the same bottom-left pixel space as touches.</summary>
    public sealed class TouchLayout
    {
        public RectF Left, Right, Jump, Backflip, Fire;

        public static TouchLayout For(float width, float height)
        {
            float u = Math.Min(width, height) / 7f;
            float m = u * 0.3f;
            return new TouchLayout
            {
                Left = new RectF(m, m, u, u),
                Right = new RectF(m * 2 + u, m, u, u),
                Jump = new RectF(m, m * 2 + u, u, u * 0.75f),
                Backflip = new RectF(m * 2 + u, m * 2 + u, u, u * 0.75f),
                Fire = new RectF(width - m - u * 1.2f, m + u * 1.3f, u * 1.2f, u * 1.2f),
            };
        }
    }

    public struct TouchContext
    {
        public bool CanAct;
        public bool ChargeMode;
        public bool TargetMode;
        /// <summary>Active worm on screen (bottom-left pixels); NaN when not ours.</summary>
        public float WormX, WormY;
        public int Facing;
        /// <summary>Drag distance for full power.</summary>
        public float FullPowerPixels;
    }

    /// <summary>
    /// Mobile controls (docs/PLAN.md §3.12): buttons bottom-left for moving
    /// and jumping, drag from your worm to aim (direction) and set power
    /// (length), release to fire; a fire button for weapons without power;
    /// tap the map to pick an air strike target; one finger elsewhere pans,
    /// two fingers pinch-zoom. Produces the same InputFrame as the keyboard.
    /// </summary>
    public sealed class TouchInterpreter
    {
        enum Role { Left, Right, Jump, Backflip, Fire, Aim, Target, Camera }

        sealed class Tracked
        {
            public Role Role;
            public float StartX, StartY, LastX, LastY;
        }

        public const float TapSlop = 12f;
        public const float WormGrabRadius = 90f;

        readonly Dictionary<int, Tracked> _touches = new Dictionary<int, Tracked>();
        readonly List<int> _ended = new List<int>();

        public bool AnyTouch => _touches.Count > 0;

        /// <summary>Fills touch-driven fields of <paramref name="f"/> and returns camera pan (pixels) and zoom factor.</summary>
        public void Update(IReadOnlyList<TouchPoint> touches, TouchLayout layout, TouchContext ctx, ref InputFrame f,
                           out float panX, out float panY, out float zoom)
        {
            panX = panY = 0;
            zoom = 1;
            _ended.Clear();
            int cameraTouches = 0;
            float prevMidX = 0, prevMidY = 0, midX = 0, midY = 0;
            float prevA = 0, prevB = 0, nowA = 0, nowB = 0;
            int ci = 0;

            foreach (var t in touches)
            {
                if (!_touches.TryGetValue(t.Id, out var tr))
                {
                    if (t.Phase != TouchPhase2.Began) continue;
                    tr = new Tracked { Role = Classify(t, layout, ctx), StartX = t.X, StartY = t.Y, LastX = t.X, LastY = t.Y };
                    _touches[t.Id] = tr;
                    if (tr.Role == Role.Jump) f.JumpPressed = true;
                    if (tr.Role == Role.Backflip) f.BackflipPressed = true;
                }
                bool ended = t.Phase == TouchPhase2.Ended;

                switch (tr.Role)
                {
                    case Role.Left: f.Left |= !ended; break;
                    case Role.Right: f.Right |= !ended; break;
                    case Role.Fire: f.FireHeld |= !ended; break;
                    case Role.Aim:
                    {
                        float dx = t.X - ctx.WormX, dy = t.Y - ctx.WormY;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        // Pull back like a slingshot is confusing; point where you want to shoot.
                        f.DragAngle = (float)Math.Atan2(dy, Math.Max(1f, Math.Abs(dx)));
                        f.DragPower = Math.Min(1f, dist / Math.Max(1f, ctx.FullPowerPixels));
                        if (ended) f.DragReleased = f.DragPower >= 0.1f;
                        break;
                    }
                    case Role.Target:
                        if (ended && Moved(tr, t) < TapSlop)
                        {
                            f.TargetPicked = true;
                            f.TargetX = t.X;
                            f.TargetY = t.Y;
                        }
                        else if (Moved(tr, t) >= TapSlop)
                        {
                            tr.Role = Role.Camera; // it was a pan after all
                        }
                        break;
                }

                if (tr.Role == Role.Camera)
                {
                    cameraTouches++;
                    if (ci == 0) { prevA = tr.LastX; prevB = tr.LastY; nowA = t.X; nowB = t.Y; }
                    else if (ci == 1)
                    {
                        float pd = Dist(prevA, prevB, tr.LastX, tr.LastY), nd = Dist(nowA, nowB, t.X, t.Y);
                        if (pd > 1 && nd > 1) zoom = pd / nd;
                    }
                    prevMidX += tr.LastX; prevMidY += tr.LastY;
                    midX += t.X; midY += t.Y;
                    ci++;
                }

                tr.LastX = t.X;
                tr.LastY = t.Y;
                if (ended) _ended.Add(t.Id);
            }

            if (cameraTouches > 0)
            {
                panX = (midX - prevMidX) / cameraTouches;
                panY = (midY - prevMidY) / cameraTouches;
            }
            foreach (var id in _ended) _touches.Remove(id);
            // Touches that vanished without an Ended phase (focus loss).
            if (touches.Count == 0) _touches.Clear();
        }

        Role Classify(TouchPoint t, TouchLayout layout, TouchContext ctx)
        {
            if (layout.Left.Contains(t.X, t.Y)) return Role.Left;
            if (layout.Right.Contains(t.X, t.Y)) return Role.Right;
            if (layout.Jump.Contains(t.X, t.Y)) return Role.Jump;
            if (layout.Backflip.Contains(t.X, t.Y)) return Role.Backflip;
            if (ctx.CanAct && !ctx.ChargeMode && !ctx.TargetMode && layout.Fire.Contains(t.X, t.Y)) return Role.Fire;
            if (ctx.CanAct && ctx.TargetMode) return Role.Target;
            if (ctx.CanAct && ctx.ChargeMode && !float.IsNaN(ctx.WormX) && Dist(t.X, t.Y, ctx.WormX, ctx.WormY) <= WormGrabRadius) return Role.Aim;
            return Role.Camera;
        }

        static float Moved(Tracked tr, TouchPoint t) { return Dist(tr.StartX, tr.StartY, t.X, t.Y); }
        static float Dist(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return (float)Math.Sqrt(dx * dx + dy * dy); }
    }

    /// <summary>Picks a quality tier from measured frame times (docs/PLAN.md §3.14).</summary>
    public sealed class TierPicker
    {
        public const int WarmupFrames = 30;
        public const int SampleFrames = 180;

        int _frames;
        double _sum;

        public bool Done => _frames >= WarmupFrames + SampleFrames;
        public float AverageMs => _frames <= WarmupFrames ? 0 : (float)(_sum / (_frames - WarmupFrames) * 1000.0);

        public void Add(float dt)
        {
            if (Done) return;
            _frames++;
            if (_frames > WarmupFrames) _sum += dt;
        }

        /// <summary>0 = Low, 1 = Medium, 2 = High. Mobile devices never start at High (heat, battery).</summary>
        public static int Pick(float averageMs, bool mobile)
        {
            int tier = averageMs <= 18f ? 2 : averageMs <= 26f ? 1 : 0;
            return mobile ? Math.Min(tier, 1) : tier;
        }
    }
}
