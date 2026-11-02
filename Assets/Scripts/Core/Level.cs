using System;
using System.Collections.Generic;

namespace Alba.Core
{
    public enum ObstacleKind
    {
        Pine,
        Oak,
        DeadTree,
        Rock,
        StandingStone,
        Wall,
        Tower,
    }

    /// <summary>Something solid on the ground: a circle (trees, rocks, stones, towers) or an axis-aligned box (walls).</summary>
    public sealed class Obstacle
    {
        public ObstacleKind Kind;
        public Vec2 Center;
        /// <summary>Collision radius of a circle obstacle.</summary>
        public float Radius;
        /// <summary>Half the width (X) and depth (Z) of a box obstacle; zero for circles.</summary>
        public Vec2 HalfSize;
        /// <summary>How tall it looks, in metres (view only).</summary>
        public float Height;
        /// <summary>A turn around the vertical axis, in degrees (view only).</summary>
        public float Yaw;
        /// <summary>A small random number for look variations (view only).</summary>
        public int Variant;

        public bool IsBox => HalfSize.X > 0f;
    }

    public enum PickupKind
    {
        Orb,
        Gem,
        Potion,
        PistolAmmo,
        ShotgunAmmo,
        RifleAmmo,
        Page,
    }

    public enum EnemyKind
    {
        Crawler,
        Wisp,
        Brute,
    }

    public readonly struct Placement<T>
    {
        public readonly T Kind;
        public readonly Vec2 Position;

        public Placement(T kind, Vec2 position)
        {
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>A flat patch of ground: a stone circle, a courtyard, a clearing.</summary>
    public readonly struct Flat
    {
        public readonly Vec2 Center;
        public readonly float Radius;
        public readonly float Height;

        public Flat(Vec2 center, float radius, float height)
        {
            Center = center;
            Radius = radius;
            Height = height;
        }
    }

    /// <summary>
    /// The ground, solid things and placements of one chapter, built by <see cref="LevelBuilder"/>.
    /// Movement is on the ground plane; the height only places things for the view.
    /// </summary>
    public sealed class Level
    {
        public readonly ChapterDef Chapter;
        public readonly float Half;
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        public readonly List<Placement<PickupKind>> Pickups = new List<Placement<PickupKind>>();
        public readonly List<Placement<EnemyKind>> Enemies = new List<Placement<EnemyKind>>();
        public readonly List<Vec2> Lanterns = new List<Vec2>();
        public readonly List<Vec2> Path = new List<Vec2>();
        public readonly List<Flat> Flats = new List<Flat>();
        public readonly List<Vec2> SpawnPoints = new List<Vec2>();
        public Vec2 Start;
        public float StartYaw;
        public Vec2 Portal;
        readonly Noise noise;

        public Level(ChapterDef chapter)
        {
            Chapter = chapter;
            Half = chapter.Half;
            noise = new Noise(chapter.Seed);
        }

        public int GemCount
        {
            get
            {
                int count = 0;
                foreach (Placement<PickupKind> p in Pickups) if (p.Kind == PickupKind.Gem) count++;
                return count;
            }
        }

        // ---- Ground ----

        /// <summary>The ground height at a point, in metres: rolling hills, flattened on paths and flats.</summary>
        public float HeightAt(float x, float z)
        {
            float hills = noise.Fractal(x * 0.035f, z * 0.035f, 4) * Chapter.Hills;
            // The edge rises into banks so the arena feels closed in.
            float edge = Math.Max(Math.Abs(x), Math.Abs(z)) - (Half - 8f);
            if (edge > 0f) hills += edge * edge * 0.12f;

            var point = new Vec2(x, z);
            float pathDistance = DistanceToPath(point);
            if (pathDistance < 5f) hills *= 0.45f + 0.55f * Smooth(pathDistance / 5f);

            foreach (Flat flat in Flats)
            {
                float d = Vec2.Distance(point, flat.Center);
                float t = Smooth(Clamp01((d - flat.Radius) / 6f));
                hills = flat.Height + (hills - flat.Height) * t;
            }
            return hills;
        }

        public float HeightAt(Vec2 p) => HeightAt(p.X, p.Z);

        /// <summary>Shortest distance from a point to the trail through the chapter.</summary>
        public float DistanceToPath(Vec2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < Path.Count; i++)
            {
                best = Math.Min(best, DistanceToSegment(point, Path[i], Path[i + 1]));
            }
            return best;
        }

        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float lengthSquared = ab.LengthSquared;
            float t = lengthSquared > 0f ? Clamp01(Vec2.Dot(p - a, ab) / lengthSquared) : 0f;
            return Vec2.Distance(p, a + ab * t);
        }

        // ---- Collision ----

        public bool InBounds(Vec2 p, float margin = 0f) => Math.Abs(p.X) <= Half - margin && Math.Abs(p.Z) <= Half - margin;

        /// <summary>Pushes a circle of <paramref name="radius"/> out of every obstacle and back inside the arena.</summary>
        public Vec2 Resolve(Vec2 position, float radius)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                foreach (Obstacle o in Obstacles)
                {
                    if (Push(ref position, radius, o)) moved = true;
                }
                if (!moved) break;
            }
            float limit = Half - 2f - radius;
            return new Vec2(Math.Clamp(position.X, -limit, limit), Math.Clamp(position.Z, -limit, limit));
        }

        /// <summary>True when a circle overlaps any obstacle.</summary>
        public bool Blocked(Vec2 position, float radius)
        {
            foreach (Obstacle o in Obstacles)
            {
                Vec2 p = position;
                if (Push(ref p, radius, o)) return true;
            }
            return !InBounds(position, 2f + radius);
        }

        static bool Push(ref Vec2 position, float radius, Obstacle o)
        {
            if (!o.IsBox)
            {
                Vec2 offset = position - o.Center;
                float minimum = radius + o.Radius;
                float distanceSquared = offset.LengthSquared;
                if (distanceSquared >= minimum * minimum) return false;
                float distance = MathF.Sqrt(distanceSquared);
                Vec2 normal = distance > 1e-4f ? offset / distance : new Vec2(1f, 0f);
                position = o.Center + normal * minimum;
                return true;
            }

            float dx = position.X - o.Center.X;
            float dz = position.Z - o.Center.Z;
            float cx = Math.Clamp(dx, -o.HalfSize.X, o.HalfSize.X);
            float cz = Math.Clamp(dz, -o.HalfSize.Z, o.HalfSize.Z);
            float ox = dx - cx, oz = dz - cz;
            float d2 = ox * ox + oz * oz;
            if (d2 >= radius * radius) return false;
            if (d2 > 1e-8f)
            {
                float d = MathF.Sqrt(d2);
                position = new Vec2(o.Center.X + cx + ox / d * radius, o.Center.Z + cz + oz / d * radius);
                return true;
            }
            // The centre is inside the box: leave by the nearest face.
            float px = o.HalfSize.X - Math.Abs(dx), pz = o.HalfSize.Z - Math.Abs(dz);
            if (px < pz) position = new Vec2(o.Center.X + Math.Sign(dx == 0 ? 1 : dx) * (o.HalfSize.X + radius), position.Z);
            else position = new Vec2(position.X, o.Center.Z + Math.Sign(dz == 0 ? 1 : dz) * (o.HalfSize.Z + radius));
            return true;
        }

        // ---- Rays ----

        /// <summary>Distance along a ray to the first obstacle, or <paramref name="max"/> when nothing is in the way.</summary>
        public float Raycast(Vec2 origin, Vec2 direction, float max)
        {
            float best = max;
            foreach (Obstacle o in Obstacles)
            {
                float hit = o.IsBox ? RayBox(origin, direction, o) : RayCircle(origin, direction, o.Center, o.Radius);
                if (hit >= 0f && hit < best) best = hit;
            }
            return best;
        }

        /// <summary>True when nothing solid stands between two points.</summary>
        public bool CanSee(Vec2 from, Vec2 to)
        {
            Vec2 offset = to - from;
            float distance = offset.Length;
            if (distance < 1e-3f) return true;
            return Raycast(from, offset / distance, distance) >= distance - 0.01f;
        }

        /// <summary>Distance along a unit ray to a circle, or -1 when it misses. A ray that starts inside hits at 0.</summary>
        public static float RayCircle(Vec2 origin, Vec2 direction, Vec2 center, float radius)
        {
            Vec2 m = origin - center;
            float c = m.LengthSquared - radius * radius;
            if (c <= 0f) return 0f;
            float b = Vec2.Dot(m, direction);
            if (b > 0f) return -1f;
            float discriminant = b * b - c;
            if (discriminant < 0f) return -1f;
            return -b - MathF.Sqrt(discriminant);
        }

        static float RayBox(Vec2 origin, Vec2 direction, Obstacle o)
        {
            float tMin = 0f, tMax = float.MaxValue;
            if (!Slab(origin.X, direction.X, o.Center.X - o.HalfSize.X, o.Center.X + o.HalfSize.X, ref tMin, ref tMax)) return -1f;
            if (!Slab(origin.Z, direction.Z, o.Center.Z - o.HalfSize.Z, o.Center.Z + o.HalfSize.Z, ref tMin, ref tMax)) return -1f;
            return tMin;
        }

        static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (Math.Abs(direction) < 1e-6f) return origin >= min && origin <= max;
            float t1 = (min - origin) / direction;
            float t2 = (max - origin) / direction;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            return tMin <= tMax;
        }

        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
