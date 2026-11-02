using System.Collections.Generic;
using Alba.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alba.Game
{
    /// <summary>
    /// Every mesh in the game, made in code: faceted rocks and stones, trees, walls, the creatures'
    /// parts, the portal ring and the ground. Facets are flat-shaded on purpose (a low-poly look).
    /// </summary>
    public static class MeshFactory
    {
        sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            /// <summary>Adds a flat-shaded triangle (its own three vertices) whose front faces along <paramref name="outward"/>.</summary>
            public void Facet(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
            {
                // Unity draws the side where Cross(b - a, c - a) points; flip the winding if that is inward.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, c) = (c, b);
                    (ub, uc) = (uc, ub);
                }
                int i = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Uvs.Add(ua);
                Uvs.Add(ub);
                Uvs.Add(uc);
                Triangles.Add(i);
                Triangles.Add(i + 1);
                Triangles.Add(i + 2);
            }

            public void Facet(Vector3 a, Vector3 b, Vector3 c, Vector3 outward) => Facet(a, b, c, Planar(a), Planar(b), Planar(c), outward);

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
            {
                Facet(a, b, c, ua, ub, uc, outward);
                Facet(a, c, d, ua, uc, ud, outward);
            }

            static Vector2 Planar(Vector3 p) => new Vector2(p.x + p.z * 0.7f, p.y);

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ---- Spheres and rocks ----

        static readonly Dictionary<int, (List<Vector3>, List<int>)> SphereCache = new Dictionary<int, (List<Vector3>, List<int>)>();

        /// <summary>A unit icosphere's shared vertices and triangles, subdivided <paramref name="detail"/> times.</summary>
        static (List<Vector3> points, List<int> triangles) Icosphere(int detail)
        {
            if (SphereCache.TryGetValue(detail, out var cached)) return cached;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int level = 0; level < detail; level++)
            {
                var middle = new Dictionary<long, int>();
                var next = new List<int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (middle.TryGetValue(key, out int index)) return index;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    middle[key] = points.Count - 1;
                    return points.Count - 1;
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
            SphereCache[detail] = (points, triangles);
            return (points, triangles);
        }

        /// <summary>A faceted ball; <paramref name="roughness"/> pushes each corner in or out (rocks, lumps of foliage).</summary>
        public static Mesh Ball(int detail, float roughness, int seed, Vector3 scale, string name = "Ball")
        {
            (List<Vector3> points, List<int> triangles) = Icosphere(detail);
            var noise = new Noise(seed);
            var moved = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                float n = noise.Fractal(p.x * 1.7f + 10f, p.z * 1.7f + p.y * 1.3f + 10f, 2);
                moved[i] = Vector3.Scale(p * (1f + n * roughness), scale);
            }
            var builder = new Builder();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = moved[triangles[i]], b = moved[triangles[i + 1]], c = moved[triangles[i + 2]];
                builder.Facet(a, b, c, a + b + c);
            }
            return builder.Build(name);
        }

        /// <summary>A boulder that sits on the ground: a flattened lumpy ball with its base sunk.</summary>
        public static Mesh Rock(int seed) => Ball(1, 0.35f, seed, new Vector3(1f, 0.75f, 1f), "Rock");

        // ---- Cylinders, cones and stones ----

        /// <summary>A cylinder or cone standing on the origin, <paramref name="height"/> tall.</summary>
        public static Mesh Cylinder(int sides, float bottom, float top, float height, bool caps = true, string name = "Cylinder", float lean = 0f)
        {
            var builder = new Builder();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var up = new Vector3(lean, height, 0f);
                Vector3 side = d0 + d1;
                Vector3 b0 = d0 * bottom, b1 = d1 * bottom, t0 = d0 * top + up, t1 = d1 * top + up;
                float u0 = (float)i / sides * 3f, u1 = (float)(i + 1) / sides * 3f;
                if (top > 0f)
                {
                    builder.Quad(b0, t0, t1, b1, new Vector2(u0, 0f), new Vector2(u0, height), new Vector2(u1, height), new Vector2(u1, 0f), side);
                }
                else
                {
                    builder.Facet(b0, up, b1, new Vector2(u0, 0f), new Vector2((u0 + u1) / 2f, height), new Vector2(u1, 0f), side);
                }
                if (caps)
                {
                    builder.Facet(Vector3.zero, b0, b1, Vector3.down);
                    if (top > 0f) builder.Facet(up, t1, t0, Vector3.up);
                }
            }
            return builder.Build(name);
        }

        /// <summary>A rough standing stone: a tapered, twisted slab with facets.</summary>
        public static Mesh StandingStone(int seed, float height)
        {
            var rng = new Rng(seed);
            float width = rng.Range(0.7f, 1.1f), depth = rng.Range(0.35f, 0.55f);
            const int rings = 4;
            var corners = new Vector3[rings + 1, 4];
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float taper = Mathf.Lerp(1f, rng.Range(0.45f, 0.75f), t);
                float twist = t * rng.Range(-0.3f, 0.3f);
                float lean = t * t * rng.Range(-0.25f, 0.25f);
                for (int c = 0; c < 4; c++)
                {
                    float sx = c == 0 || c == 3 ? -1f : 1f, sz = c < 2 ? -1f : 1f;
                    var p = new Vector3(sx * width * taper * rng.Range(0.85f, 1.1f), t * height, sz * depth * taper * rng.Range(0.85f, 1.1f));
                    p = Quaternion.Euler(0f, twist * Mathf.Rad2Deg, 0f) * p;
                    p.x += lean;
                    corners[r, c] = p;
                }
            }
            var builder = new Builder();
            for (int r = 0; r < rings; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int n = (c + 1) % 4;
                    Vector3 middle = (corners[r, c] + corners[r + 1, n]) * 0.5f;
                    Vector3 axis = (corners[r, 0] + corners[r, 2] + corners[r + 1, 0] + corners[r + 1, 2]) * 0.25f;
                    builder.Quad(corners[r, c], corners[r + 1, c], corners[r + 1, n], corners[r, n],
                        new Vector2(c, r), new Vector2(c, r + 1), new Vector2(c + 1, r + 1), new Vector2(c + 1, r), middle - axis);
                }
            }
            Vector3 peak = (corners[rings, 0] + corners[rings, 2]) * 0.5f + Vector3.up * rng.Range(0.1f, 0.3f);
            for (int c = 0; c < 4; c++) builder.Facet(corners[rings, c], peak, corners[rings, (c + 1) % 4], Vector3.up);
            return builder.Build("StandingStone");
        }

        // ---- Boxes and walls ----

        /// <summary>A box centred on X/Z, sitting on y = 0, with world-scaled UVs (one unit per metre).</summary>
        public static Mesh Box(Vector3 size, string name = "Box")
        {
            var builder = new Builder();
            float x = size.x / 2f, y = size.y, z = size.z / 2f;
            Vector3[] p =
            {
                new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z),
                new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z),
            };
            builder.Quad(p[0], p[4], p[5], p[1], new Vector2(0, 0), new Vector2(0, y), new Vector2(size.x, y), new Vector2(size.x, 0), Vector3.back);
            builder.Quad(p[1], p[5], p[6], p[2], new Vector2(0, 0), new Vector2(0, y), new Vector2(size.z, y), new Vector2(size.z, 0), Vector3.right);
            builder.Quad(p[2], p[6], p[7], p[3], new Vector2(0, 0), new Vector2(0, y), new Vector2(size.x, y), new Vector2(size.x, 0), Vector3.forward);
            builder.Quad(p[3], p[7], p[4], p[0], new Vector2(0, 0), new Vector2(0, y), new Vector2(size.z, y), new Vector2(size.z, 0), Vector3.left);
            builder.Quad(p[4], p[7], p[6], p[5], new Vector2(0, 0), new Vector2(0, size.z), new Vector2(size.x, size.z), new Vector2(size.x, 0), Vector3.up);
            builder.Quad(p[0], p[1], p[2], p[3], new Vector2(0, 0), new Vector2(size.x, 0), new Vector2(size.x, size.z), new Vector2(0, size.z), Vector3.down);
            return builder.Build(name);
        }

        // ---- Gems and the portal ----

        /// <summary>A long octahedron, like the sealstones of the 2021 game.</summary>
        public static Mesh Octahedron(float width, float height)
        {
            var builder = new Builder();
            var top = new Vector3(0f, height, 0f);
            var bottom = new Vector3(0f, -height, 0f);
            for (int i = 0; i < 4; i++)
            {
                float a0 = i * Mathf.PI / 2f, a1 = (i + 1) * Mathf.PI / 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * width, 0f, Mathf.Sin(a0) * width);
                var p1 = new Vector3(Mathf.Cos(a1) * width, 0f, Mathf.Sin(a1) * width);
                builder.Facet(p0, top, p1, p0 + p1 + top);
                builder.Facet(p1, bottom, p0, p0 + p1 + bottom);
            }
            return builder.Build("Octahedron");
        }

        /// <summary>An upright ring in the X/Y plane (the portal's stone hoop).</summary>
        public static Mesh Torus(float radius, float thickness, int segments, int sides)
        {
            var builder = new Builder();
            Vector3 Point(int s, int k)
            {
                float a = s * Mathf.PI * 2f / segments, b = k * Mathf.PI * 2f / sides;
                var centre = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                Vector3 outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                return centre + (outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * thickness;
            }
            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < sides; k++)
                {
                    float mid = (s + 0.5f) * Mathf.PI * 2f / segments;
                    Vector3 tube = new Vector3(Mathf.Cos(mid), Mathf.Sin(mid), 0f) * radius;
                    Vector3 middle = (Point(s, k) + Point(s + 1, k + 1)) * 0.5f;
                    builder.Quad(Point(s, k), Point(s, k + 1), Point(s + 1, k + 1), Point(s + 1, k),
                        new Vector2(s, k), new Vector2(s, k + 1), new Vector2(s + 1, k + 1), new Vector2(s + 1, k), middle - tube);
                }
            }
            return builder.Build("Torus");
        }

        /// <summary>A flat disc in the X/Y plane facing +Z and -Z.</summary>
        public static Mesh Disc(float radius, int segments)
        {
            var builder = new Builder();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                var u0 = new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f);
                var u1 = new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f);
                builder.Facet(Vector3.zero, p0, p1, new Vector2(0.5f, 0.5f), u0, u1, Vector3.forward);
                builder.Facet(Vector3.zero, p1, p0, new Vector2(0.5f, 0.5f), u1, u0, Vector3.back);
            }
            return builder.Build("Disc");
        }

        // ---- Ground ----

        /// <summary>The chapter's ground as a grid, UV 0..1 across the whole arena for the painted ground texture.</summary>
        public static Mesh Ground(Level level, float cell)
        {
            int n = Mathf.CeilToInt(level.Half * 2f / cell);
            var vertices = new Vector3[(n + 1) * (n + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    float wx = -level.Half + x * cell, wz = -level.Half + z * cell;
                    vertices[z * (n + 1) + x] = new Vector3(wx, level.HeightAt(wx, wz), wz);
                    uvs[z * (n + 1) + x] = new Vector2((float)x / n, (float)z / n);
                }
            }
            var triangles = new int[n * n * 6];
            int t = 0;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * (n + 1) + x;
                    triangles[t++] = i;
                    triangles[t++] = i + n + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + n + 1;
                    triangles[t++] = i + n + 2;
                }
            }
            var mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
