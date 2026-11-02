using Alba.Core;
using UnityEngine;

namespace Alba.Game
{
    /// <summary>The surface textures, painted in code from seeded noise: ground, bark, stone, wall masonry and parchment.</summary>
    public static class TextureFactory
    {
        /// <summary>The whole chapter's ground in one picture: grass and heather, a muddy trail, worn earth on the flats.</summary>
        public static Texture2D Ground(Level level, int size)
        {
            ChapterDef chapter = level.Chapter;
            var noise = new Noise(chapter.Seed + 17);
            var speckle = new Rng(chapter.Seed + 5);
            Color grass = Palette.Hex(chapter.GrassColor);
            Color earth = Palette.Hex(chapter.EarthColor);
            Color heather = Palette.Hex("#4a2f45");
            Color moss = Color.Lerp(grass, Palette.Hex("#6b7a3a"), 0.5f);
            var pixels = new Color[size * size];
            float span = level.Half * 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = -level.Half + (x + 0.5f) / size * span;
                    float wz = -level.Half + (y + 0.5f) / size * span;
                    float broad = noise.Fractal(wx * 0.05f, wz * 0.05f, 3);
                    float fine = noise.Fractal(wx * 0.6f + 40f, wz * 0.6f + 40f, 2);
                    Color c = Color.Lerp(grass, moss, Mathf.Clamp01(0.5f + broad));
                    float heatherAmount = Mathf.Clamp01((noise.Fractal(wx * 0.09f + 90f, wz * 0.09f, 2) - 0.1f) * 3f);
                    c = Color.Lerp(c, heather, heatherAmount * 0.65f);
                    var p = new Vec2(wx, wz);
                    float path = level.DistanceToPath(p);
                    float trail = Mathf.Clamp01(1f - (path - 1.2f + fine * 0.8f) / 1.4f);
                    c = Color.Lerp(c, earth, trail * 0.85f);
                    foreach (Flat flat in level.Flats)
                    {
                        float d = Vec2.Distance(p, flat.Center);
                        float worn = Mathf.Clamp01(1f - (d - flat.Radius * 0.6f + fine * 1.5f) / 3f);
                        c = Color.Lerp(c, Color.Lerp(earth, Palette.Stone * 0.7f, 0.35f), worn * 0.7f);
                    }
                    c *= 0.85f + 0.3f * (fine * 0.5f + 0.5f);
                    if (speckle.Chance(0.04f)) c *= 0.75f;
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Make("Ground", size, pixels, TextureWrapMode.Clamp);
        }

        /// <summary>A seamless noise texture between two colours; <paramref name="stretch"/> elongates it vertically (bark).</summary>
        public static Texture2D Noisy(string name, int size, int seed, Color dark, Color light, float scale, float stretch = 1f)
        {
            var noise = new Noise(seed);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Tiled(noise, x, y, size, scale, stretch);
                    Color c = Color.Lerp(dark, light, Mathf.Clamp01(0.5f + n));
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Make(name, size, pixels, TextureWrapMode.Repeat);
        }

        /// <summary>Rough masonry: offset rows of stones with dark mortar, for the walls of Dunadd.</summary>
        public static Texture2D Masonry(int size, int seed)
        {
            var noise = new Noise(seed);
            var rng = new Rng(seed);
            var pixels = new Color[size * size];
            const int rows = 4;
            float rowHeight = size / (float)rows;
            var tints = new float[rows * 4];
            for (int i = 0; i < tints.Length; i++) tints[i] = rng.Range(0.75f, 1.15f);
            for (int y = 0; y < size; y++)
            {
                int row = (int)(y / rowHeight);
                float shift = row % 2 == 0 ? 0f : size / 4f;
                for (int x = 0; x < size; x++)
                {
                    float sx = (x + shift) % size;
                    int column = (int)(sx / (size / 2f));
                    float inRow = y - row * rowHeight;
                    float inColumn = sx - column * (size / 2f);
                    float edge = Mathf.Min(Mathf.Min(inRow, rowHeight - inRow), Mathf.Min(inColumn, size / 2f - inColumn));
                    float n = Tiled(noise, x, y, size, 6f, 1f);
                    Color stone = Palette.WallStone * tints[(row * 2 + column) % tints.Length] * (0.85f + 0.3f * n);
                    Color mortar = Palette.WallStone * 0.35f;
                    Color c = Color.Lerp(mortar, stone, Mathf.Clamp01((edge - 1.5f + n * 2f) / 2f));
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Make("Masonry", size, pixels, TextureWrapMode.Repeat);
        }

        /// <summary>Old paper with ruled ink lines, for the journal pages.</summary>
        public static Texture2D Parchment(int size)
        {
            var noise = new Noise(77);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Tiled(noise, x, y, size, 5f, 1f);
                    Color c = Palette.Parchment * (0.85f + 0.2f * n);
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (float)size;
                    c *= Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(edge * 8f));
                    bool line = y % 8 == 0 && x > size * 0.12f && x < size * (0.82f + 0.08f * Mathf.Sin(y)) && y > size * 0.15f && y < size * 0.88f;
                    if (line) c = Color.Lerp(c, Palette.Hex("#2b1d14"), 0.75f);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Make("Parchment", size, pixels, TextureWrapMode.Clamp);
        }

        static float Tiled(Noise noise, int x, int y, int size, float scale, float stretch)
        {
            // Sample on a torus so the texture repeats without seams.
            float u = (float)x / size * Mathf.PI * 2f, v = (float)y / size * Mathf.PI * 2f;
            float r = scale / (Mathf.PI * 2f);
            return noise.Fractal(Mathf.Cos(u) * r + 50f, Mathf.Sin(u) * r + Mathf.Cos(v) * r / stretch * 0.5f + 50f, 3) * 0.7f +
                noise.Fractal(Mathf.Sin(v) * r / stretch + 80f, Mathf.Cos(v) * r / stretch + Mathf.Sin(u) * r * 0.3f + 80f, 3) * 0.6f;
        }

        static Texture2D Make(string name, int size, Color[] pixels, TextureWrapMode wrap)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
