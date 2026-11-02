using System;

namespace Alba.Core
{
    /// <summary>
    /// A point or direction on the ground plane: X is east, Z is north (Unity's X and Z).
    /// Yaw angles are in degrees, clockwise from north, like a Unity camera's Y rotation.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Z;

        public Vec2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length => MathF.Sqrt(X * X + Z * Z);

        public float LengthSquared => X * X + Z * Z;

        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-6f ? new Vec2(X / length, Z / length) : Zero;
            }
        }

        /// <summary>The same direction, at most <paramref name="max"/> long.</summary>
        public Vec2 ClampLength(float max)
        {
            float length = Length;
            return length > max && length > 1e-6f ? this * (max / length) : this;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Z);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Z * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Z * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Z / s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        /// <summary>The unit vector a camera with this yaw looks along.</summary>
        public static Vec2 FromYaw(float yawDegrees)
        {
            float radians = yawDegrees * (MathF.PI / 180f);
            return new Vec2(MathF.Sin(radians), MathF.Cos(radians));
        }

        /// <summary>The yaw (degrees, 0..360) that looks along this vector.</summary>
        public float Yaw
        {
            get
            {
                float degrees = MathF.Atan2(X, Z) * (180f / MathF.PI);
                return degrees < 0f ? degrees + 360f : degrees;
            }
        }

        /// <summary>Turns a local move (X strafe right, Z forward) into a world direction for a yaw.</summary>
        public Vec2 RotatedByYaw(float yawDegrees)
        {
            Vec2 forward = FromYaw(yawDegrees);
            var right = new Vec2(forward.Z, -forward.X);
            return right * X + forward * Z;
        }

        /// <summary>The signed difference b - a between two yaws, in -180..180.</summary>
        public static float DeltaYaw(float a, float b)
        {
            float delta = (b - a) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            return delta;
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Z);

        public override string ToString() => $"({X:0.00}, {Z:0.00})";
    }
}
