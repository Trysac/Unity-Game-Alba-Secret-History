using System;

namespace Alba.Core
{
    /// <summary>A small seeded random generator (xorshift32), so levels and fights replay exactly.</summary>
    public sealed class Rng
    {
        uint state;

        public Rng(int seed)
        {
            state = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            if (state == 0) state = 0x6C8E9CF5u;
            for (int i = 0; i < 4; i++) Next();
        }

        public uint Next()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value() => (Next() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>An int in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min));

        public bool Chance(float probability) => Value() < probability;

        /// <summary>A point uniformly inside a disc.</summary>
        public Vec2 InDisc(Vec2 center, float radius)
        {
            float angle = Range(0f, MathF.PI * 2f);
            float distance = radius * MathF.Sqrt(Value());
            return center + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }
    }
}
