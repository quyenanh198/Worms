using System;

namespace Worms.Sim
{
    /// <summary>2D vector in simulation units. +X is right, +Y is down (mask rows).</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 Up = new Vec2(0, -1);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float LengthSq => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec2(X / len, Y / len) : Zero;
            }
        }

        public static float Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }
        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }
        public static Vec2 FromAngle(float radians) { return new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians)); }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.X, -a.Y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator *(float s, Vec2 a) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator /(Vec2 a, float s) { return new Vec2(a.X / s, a.Y / s); }

        public bool Equals(Vec2 other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Vec2 v && Equals(v); }
        public override int GetHashCode() { return X.GetHashCode() * 397 ^ Y.GetHashCode(); }
        public override string ToString() { return "(" + X.ToString("0.##") + ", " + Y.ToString("0.##") + ")"; }
    }
}
