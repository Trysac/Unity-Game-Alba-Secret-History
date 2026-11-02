using System;

namespace Alba.Core
{
    /// <summary>Seeded value noise with octaves, for the rolling ground of the Highlands.</summary>
    public sealed class Noise
    {
        readonly uint seed;

        public Noise(int seed)
        {
            this.seed = (uint)seed * 747796405u + 2891336453u;
        }

        /// <summary>Smooth noise in [-1, 1] at a point; <paramref name="octaves"/> layers of halving size.</summary>
        public float Fractal(float x, float z, int octaves)
        {
            float sum = 0f, amplitude = 1f, total = 0f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x * frequency, z * frequency, (uint)i) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }
            return sum / total;
        }

        float Value(float x, float z, uint layer)
        {
            int x0 = (int)MathF.Floor(x);
            int z0 = (int)MathF.Floor(z);
            float tx = Smooth(x - x0);
            float tz = Smooth(z - z0);
            float a = Lattice(x0, z0, layer);
            float b = Lattice(x0 + 1, z0, layer);
            float c = Lattice(x0, z0 + 1, layer);
            float d = Lattice(x0 + 1, z0 + 1, layer);
            float top = a + (b - a) * tx;
            float bottom = c + (d - c) * tx;
            return top + (bottom - top) * tz;
        }

        float Lattice(int x, int z, uint layer)
        {
            uint h = (uint)x * 374761393u + (uint)z * 668265263u + seed + layer * 1442695041u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 32767.5f - 1f;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
