using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>Engine-independent 2D vector used by the simulation (X = east, Y = north, in map cells).</summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 One = new Vec2(1, 1);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float SqrLength => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec2(X / l, Y / l) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float SqrDistance(Vec2 a, Vec2 b) => (a - b).SqrLength;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        public static Vec2 FromAngle(float radians) => new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians));
        public float Angle => (float)Math.Atan2(Y, X);
        public Vec2 Perp => new Vec2(-Y, X);

        public Vec2 Rotate(float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        public Vec2 ClampLength(float max)
        {
            float l = Length;
            return l > max && l > 0 ? this * (max / l) : this;
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.##},{Y:0.##})";
    }

    [Serializable]
    public struct Int2 : IEquatable<Int2>
    {
        public int X;
        public int Y;
        public Int2(int x, int y) { X = x; Y = y; }
        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Int2 a, Int2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Int2 a, Int2 b) => !(a == b);
        public Vec2 Center => new Vec2(X + 0.5f, Y + 0.5f);
        public bool Equals(Int2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Int2 i && Equals(i);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
        public override string ToString() => $"[{X},{Y}]";
        public static Int2 Floor(Vec2 v) => new Int2((int)Math.Floor(v.X), (int)Math.Floor(v.Y));
    }

    public static class SMath
    {
        public const float PI = (float)Math.PI;
        public const float TwoPI = (float)(Math.PI * 2);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Abs(float v) => v < 0 ? -v : v;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);

        /// <summary>Wraps an angle to (-PI, PI].</summary>
        public static float WrapAngle(float a)
        {
            while (a > PI) a -= TwoPI;
            while (a <= -PI) a += TwoPI;
            return a;
        }

        /// <summary>Rotates current toward target by at most maxDelta radians.</summary>
        public static float RotateTowards(float current, float target, float maxDelta)
        {
            float d = WrapAngle(target - current);
            if (Abs(d) <= maxDelta) return target;
            return WrapAngle(current + Math.Sign(d) * maxDelta);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }

    /// <summary>Small, fast, serializable PRNG (xorshift) so simulation randomness can be saved and restored.</summary>
    [Serializable]
    public class SimRandom
    {
        public uint State;

        public SimRandom(int seed) { State = (uint)seed ^ 0x9E3779B9u; if (State == 0) State = 1; }

        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State = x;
            return x;
        }

        public float NextFloat() => (NextUInt() & 0xFFFFFF) / (float)0x1000000;
        public float Range(float a, float b) => a + (b - a) * NextFloat();
        public int Range(int a, int bExclusive) => bExclusive <= a ? a : a + (int)(NextUInt() % (uint)(bExclusive - a));
        public bool Chance(float p) => NextFloat() < p;
        public Vec2 InsideCircle(float r)
        {
            float a = Range(0, SMath.TwoPI);
            float d = SMath.Sqrt(NextFloat()) * r;
            return new Vec2((float)Math.Cos(a) * d, (float)Math.Sin(a) * d);
        }
        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];
    }

    /// <summary>Deterministic value noise used by the map generator.</summary>
    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        public static float Value(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return SMath.Lerp(SMath.Lerp(a, b, fx), SMath.Lerp(c, d, fx), fy);
        }

        public static float Fractal(float x, float y, int seed, int octaves = 4)
        {
            float sum = 0, amp = 1, norm = 0, f = 1;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x * f, y * f, seed + i * 31) * amp;
                norm += amp; amp *= 0.5f; f *= 2f;
            }
            return sum / norm;
        }
    }
}
