using System.Collections.Generic;

namespace Alba.Core
{
    /// <summary>A ring of standing stones.</summary>
    public readonly struct StoneCircle
    {
        public readonly Vec2 Center;
        public readonly float Radius;
        public readonly int Count;

        public StoneCircle(Vec2 center, float radius, int count)
        {
            Center = center;
            Radius = radius;
            Count = count;
        }
    }

    /// <summary>
    /// Everything that defines a chapter: story text, the hand-placed things (start, sealstones,
    /// lanterns, enemies, supplies, portal), how many trees and rocks to scatter, and the mood
    /// (colours are 0xRRGGBB so the rules stay free of the engine).
    /// </summary>
    public sealed class ChapterDef
    {
        public int Index;
        public string Title;
        public string Place;
        public string Intro;
        public string PageTitle;
        public string PageText;
        public int Seed;
        public float Half = 60f;
        public float Hills = 3f;

        public Vec2 Start;
        public float StartYaw;
        public Vec2 Portal;
        public Vec2[] Path = new Vec2[0];
        public Vec2[] Gems = new Vec2[0];
        public Vec2[] Lanterns = new Vec2[0];
        public Vec2 Page;
        public Placement<PickupKind>[] Supplies = new Placement<PickupKind>[0];
        public Placement<EnemyKind>[] Foes = new Placement<EnemyKind>[0];
        public StoneCircle[] StoneCircles = new StoneCircle[0];
        public Flat[] Flats = new Flat[0];
        /// <summary>Wall boxes as (centre, half size) pairs.</summary>
        public (Vec2 Center, Vec2 Half)[] Walls = new (Vec2, Vec2)[0];
        public (Vec2 Center, float Radius)[] Towers = new (Vec2, float)[0];
        public Vec2[] SpawnPoints = new Vec2[0];

        public int TreeCount;
        public float OakShare;
        public float DeadShare;
        public int RockCount;
        public int OrbCount;
        /// <summary>For the last chapter: seconds to hold the circle until dawn; zero elsewhere.</summary>
        public float SurvivalSeconds;
        public bool HasBoss;

        public bool Rain;
        public float FogDensity;
        public int FogColor;
        public int SkyColor;
        public int AmbientColor;
        public int MoonColor;
        public float MoonIntensity;
        public int GrassColor;
        public int EarthColor;
        public int MusicTheme;
    }

    /// <summary>The four chapters of the night, from the stones on the moor to dawn on the cliffs.</summary>
    public static class Chapters
    {
        public const string Prologue =
            "Samhain night. Folklorist Eilidh Ross follows her grandfather's journal to the Isle of Lewis. " +
            "He wrote that the old stones of Alba are not monuments but locks, and that on this one night " +
            "the door behind them swings open. He never came home.";

        public const string EndingTitle = "You Escaped";

        public const string Ending =
            "The sun clears the sea and the door folds shut behind you. In your pocket, the last page of the " +
            "journal is warm. The stones of Alba will keep their secret for another year.";

        public static readonly ChapterDef[] All = { Calanais(), BlackWood(), Dunadd(), Dawn() };

        static Vec2 V(float x, float z) => new Vec2(x, z);

        static Placement<PickupKind> P(PickupKind kind, float x, float z) => new Placement<PickupKind>(kind, V(x, z));

        static Placement<EnemyKind> E(EnemyKind kind, float x, float z) => new Placement<EnemyKind>(kind, V(x, z));

        static ChapterDef Calanais() => new ChapterDef
        {
            Index = 0,
            Title = "The Stones of Calanais",
            Place = "Isle of Lewis, dusk",
            Intro = "The circle on the moor hums under the rain. Three sealstones have been pulled from it and " +
                "scattered across the heather. Set them back and the stones will open a road onward.",
            PageTitle = "Journal, page I",
            PageText = "The stones are not graves or calendars. Calanais was built as a lock, and every lock " +
                "has keys: the sealstones. Something under the moor has been prising them loose.",
            Seed = 1101,
            Hills = 3.2f,
            Start = V(0f, -50f),
            StartYaw = 0f,
            Portal = V(6f, 24f),
            Path = new[] { V(0f, -50f), V(-8f, -34f), V(-20f, -18f), V(-14f, 0f), V(0f, 10f), V(6f, 24f) },
            Gems = new[] { V(34f, -8f), V(-40f, 14f), V(-6f, 46f) },
            Lanterns = new[] { V(-10f, -30f), V(-14f, 4f) },
            Page = V(30f, -40f),
            Supplies = new[]
            {
                P(PickupKind.Potion, -22f, -20f), P(PickupKind.Potion, 26f, 10f),
                P(PickupKind.PistolAmmo, -6f, -38f), P(PickupKind.PistolAmmo, -30f, 10f), P(PickupKind.PistolAmmo, 12f, 42f),
                P(PickupKind.ShotgunAmmo, -18f, -6f), P(PickupKind.ShotgunAmmo, 30f, -20f),
            },
            Foes = new[]
            {
                E(EnemyKind.Crawler, 30f, -2f), E(EnemyKind.Crawler, 38f, -14f), E(EnemyKind.Crawler, -36f, 20f),
                E(EnemyKind.Crawler, -44f, 6f), E(EnemyKind.Crawler, -2f, 42f), E(EnemyKind.Crawler, 16f, 40f),
                E(EnemyKind.Crawler, 20f, -26f),
            },
            StoneCircles = new[] { new StoneCircle(V(6f, 24f), 9f, 13), new StoneCircle(V(-40f, 14f), 4f, 5) },
            Flats = new[] { new Flat(V(6f, 24f), 11f, 1.5f), new Flat(V(-40f, 14f), 5f, 0.5f) },
            TreeCount = 40,
            OakShare = 0.15f,
            DeadShare = 0.45f,
            RockCount = 45,
            OrbCount = 26,
            Rain = true,
            FogDensity = 0.028f,
            FogColor = 0x3a3f4f,
            SkyColor = 0x2b3044,
            AmbientColor = 0x2a2d3a,
            MoonColor = 0x8d9cc8,
            MoonIntensity = 0.45f,
            GrassColor = 0x4c5a32,
            EarthColor = 0x5a4630,
            MusicTheme = 0,
        };

        static ChapterDef BlackWood() => new ChapterDef
        {
            Index = 1,
            Title = "The Black Wood",
            Place = "Rannoch, midnight",
            Intro = "The road leads into the old pinewood, where lights that are not lanterns drift between the " +
                "trunks. Wisps cannot be hurt in the dark: catch them in your torch beam first. Four sealstones " +
                "lie somewhere in the wood.",
            PageTitle = "Journal, page II",
            PageText = "The wisps are what is left of travellers who stayed too long. They fear honest light. " +
                "I lost my lamp at the burn and I have heard them laughing ever since.",
            Seed = 2207,
            Hills = 2.6f,
            Start = V(-48f, -48f),
            StartYaw = 45f,
            Portal = V(44f, 44f),
            Path = new[] { V(-48f, -48f), V(-30f, -36f), V(-26f, -14f), V(-6f, -6f), V(4f, 14f), V(24f, 18f), V(30f, 36f), V(44f, 44f) },
            Gems = new[] { V(-46f, 10f), V(12f, -40f), V(-10f, 40f), V(46f, -6f) },
            Lanterns = new[] { V(-28f, -26f), V(2f, 6f), V(28f, 30f) },
            Page = V(-38f, 46f),
            Supplies = new[]
            {
                P(PickupKind.Potion, -34f, -30f), P(PickupKind.Potion, 6f, 10f), P(PickupKind.Potion, 20f, -20f),
                P(PickupKind.PistolAmmo, -24f, -12f), P(PickupKind.PistolAmmo, 10f, -36f), P(PickupKind.PistolAmmo, 34f, 24f),
                P(PickupKind.ShotgunAmmo, -44f, 6f), P(PickupKind.ShotgunAmmo, 40f, -4f),
                P(PickupKind.RifleAmmo, -8f, -8f), P(PickupKind.RifleAmmo, 26f, 34f),
            },
            Foes = new[]
            {
                E(EnemyKind.Wisp, -30f, 0f), E(EnemyKind.Wisp, 10f, -30f), E(EnemyKind.Wisp, -14f, 30f),
                E(EnemyKind.Wisp, 36f, 0f), E(EnemyKind.Wisp, 20f, 40f),
                E(EnemyKind.Crawler, -40f, 14f), E(EnemyKind.Crawler, 16f, -42f), E(EnemyKind.Crawler, -4f, 44f),
                E(EnemyKind.Crawler, 48f, -12f), E(EnemyKind.Crawler, 0f, 22f), E(EnemyKind.Crawler, -18f, -42f),
                E(EnemyKind.Crawler, 32f, 18f), E(EnemyKind.Crawler, 40f, 34f),
            },
            Flats = new[] { new Flat(V(44f, 44f), 7f, 0.6f) },
            TreeCount = 230,
            OakShare = 0.3f,
            DeadShare = 0.1f,
            RockCount = 24,
            OrbCount = 30,
            Rain = false,
            FogDensity = 0.05f,
            FogColor = 0x232a26,
            SkyColor = 0x101418,
            AmbientColor = 0x1e2420,
            MoonColor = 0x7f93a8,
            MoonIntensity = 0.3f,
            GrassColor = 0x2f3d24,
            EarthColor = 0x3f3122,
            MusicTheme = 1,
        };

        static ChapterDef Dunadd() => new ChapterDef
        {
            Index = 2,
            Title = "Dunadd Keep",
            Place = "Kilmartin Glen, the small hours",
            Intro = "A ruined hill fort, its gate still guarded. Something huge paces the courtyard: Am Famhair, " +
                "the giant of the old stories. Gather the three sealstones and bring it down to open the way.",
            PageTitle = "Journal, page III",
            PageText = "The Fomorians were here before Alba had a name. One of them was chained in the keep at " +
                "Dunadd to guard the gate. The chain has rusted through.",
            Seed = 3313,
            Hills = 3.6f,
            Start = V(0f, -52f),
            StartYaw = 0f,
            Portal = V(0f, 28f),
            Path = new[] { V(0f, -52f), V(-12f, -34f), V(-4f, -16f), V(0f, -4f), V(0f, 26f) },
            Gems = new[] { V(-42f, 30f), V(42f, -22f), V(12f, 22f) },
            Lanterns = new[] { V(-14f, -36f), V(0f, -9f) },
            Page = V(44f, 40f),
            Supplies = new[]
            {
                P(PickupKind.Potion, -6f, -18f), P(PickupKind.Potion, -14f, 28f), P(PickupKind.Potion, 30f, -36f),
                P(PickupKind.RifleAmmo, -36f, 26f), P(PickupKind.RifleAmmo, 38f, -14f), P(PickupKind.RifleAmmo, 4f, -12f),
                P(PickupKind.ShotgunAmmo, -10f, -42f), P(PickupKind.ShotgunAmmo, 16f, 6f),
                P(PickupKind.PistolAmmo, -30f, -10f), P(PickupKind.PistolAmmo, 26f, 40f),
            },
            Foes = new[]
            {
                E(EnemyKind.Brute, 0f, 18f), E(EnemyKind.Crawler, -12f, 10f), E(EnemyKind.Crawler, 12f, 8f),
                E(EnemyKind.Crawler, -40f, 22f), E(EnemyKind.Crawler, -30f, 38f), E(EnemyKind.Crawler, 36f, -28f),
                E(EnemyKind.Crawler, 48f, -14f), E(EnemyKind.Crawler, -26f, -24f), E(EnemyKind.Crawler, 12f, -38f),
                E(EnemyKind.Wisp, 30f, 42f), E(EnemyKind.Wisp, -44f, -10f), E(EnemyKind.Wisp, 26f, -6f),
            },
            Walls = new[]
            {
                (V(0f, 32.6f), V(21.2f, 0.6f)),
                (V(-12.1f, -0.6f), V(9.1f, 0.6f)),
                (V(12.1f, -0.6f), V(9.1f, 0.6f)),
                (V(-20.6f, 16f), V(0.6f, 17.2f)),
                (V(20.6f, 16f), V(0.6f, 17.2f)),
            },
            Towers = new[]
            {
                (V(-20.6f, -0.6f), 2.4f), (V(20.6f, -0.6f), 2.4f), (V(-20.6f, 32.6f), 2.4f), (V(20.6f, 32.6f), 2.4f),
                (V(-4.2f, -0.6f), 1.3f), (V(4.2f, -0.6f), 1.3f),
            },
            Flats = new[] { new Flat(V(0f, 16f), 24f, 1f) },
            HasBoss = true,
            TreeCount = 90,
            OakShare = 0.45f,
            DeadShare = 0.35f,
            RockCount = 34,
            OrbCount = 28,
            Rain = true,
            FogDensity = 0.03f,
            FogColor = 0x2b2633,
            SkyColor = 0x15121f,
            AmbientColor = 0x26222e,
            MoonColor = 0x9a92c0,
            MoonIntensity = 0.42f,
            GrassColor = 0x3b432a,
            EarthColor = 0x4d3c2c,
            MusicTheme = 2,
        };

        static ChapterDef Dawn()
        {
            var spawns = new Vec2[8];
            for (int i = 0; i < spawns.Length; i++)
            {
                Vec2 direction = Vec2.FromYaw(i * 45f + 22.5f);
                spawns[i] = direction * 28f;
            }
            return new ChapterDef
            {
                Index = 3,
                Title = "Alba",
                Place = "Neist Point, before dawn",
                Intro = "The last circle stands on the cliffs at the edge of the land. The door closes at first light. " +
                    "Hold the circle until dawn, then step through.",
                PageTitle = "Journal, page IV",
                PageText = "If you are reading this, you followed me. Do not wait for me at the cliffs. Hold the last " +
                    "circle until the sun comes up over the sea: dawn closes every door. Alba is the old name of this " +
                    "land; in the south, it means dawn.",
                Seed = 4421,
                Half = 40f,
                Hills = 1.4f,
                Start = V(0f, -6f),
                StartYaw = 0f,
                Portal = V(0f, 3f),
                Path = new[] { V(0f, -34f), V(0f, 0f) },
                Lanterns = new[] { V(0f, -3f) },
                Page = V(0f, 8f),
                Supplies = new[]
                {
                    P(PickupKind.Potion, 6f, 4f), P(PickupKind.Potion, -6f, 4f),
                    P(PickupKind.ShotgunAmmo, -8f, -5f), P(PickupKind.RifleAmmo, 8f, -5f), P(PickupKind.PistolAmmo, -3f, -9f),
                    P(PickupKind.RifleAmmo, 4f, 9f),
                },
                StoneCircles = new[] { new StoneCircle(V(0f, 0f), 11f, 15) },
                Flats = new[] { new Flat(V(0f, 0f), 13f, 0.8f) },
                SpawnPoints = spawns,
                SurvivalSeconds = 100f,
                TreeCount = 18,
                OakShare = 0f,
                DeadShare = 0.8f,
                RockCount = 30,
                OrbCount = 12,
                Rain = false,
                FogDensity = 0.026f,
                FogColor = 0x3d3048,
                SkyColor = 0x2a2140,
                AmbientColor = 0x2e2838,
                MoonColor = 0xb0a0d0,
                MoonIntensity = 0.4f,
                GrassColor = 0x4a5a34,
                EarthColor = 0x5d4a36,
                MusicTheme = 3,
            };
        }
    }

    /// <summary>Turns a <see cref="ChapterDef"/> into a <see cref="Level"/>: builds the stones and walls, then scatters trees, rocks and orbs with the chapter's seed.</summary>
    public static class LevelBuilder
    {
        const float PathClearance = 3.4f;

        public static Level Build(ChapterDef def)
        {
            var level = new Level(def)
            {
                Start = def.Start,
                StartYaw = def.StartYaw,
                Portal = def.Portal,
            };
            level.Path.AddRange(def.Path);
            level.Flats.AddRange(def.Flats);
            level.Lanterns.AddRange(def.Lanterns);
            level.SpawnPoints.AddRange(def.SpawnPoints);
            foreach (Vec2 gem in def.Gems) level.Pickups.Add(new Placement<PickupKind>(PickupKind.Gem, gem));
            level.Pickups.Add(new Placement<PickupKind>(PickupKind.Page, def.Page));
            level.Pickups.AddRange(def.Supplies);
            level.Enemies.AddRange(def.Foes);

            var rng = new Rng(def.Seed);
            foreach (StoneCircle circle in def.StoneCircles)
            {
                float offset = rng.Range(0f, 360f);
                for (int i = 0; i < circle.Count; i++)
                {
                    float yaw = offset + i * 360f / circle.Count;
                    level.Obstacles.Add(new Obstacle
                    {
                        Kind = ObstacleKind.StandingStone,
                        Center = circle.Center + Vec2.FromYaw(yaw) * circle.Radius,
                        Radius = 0.55f,
                        Height = rng.Range(2.6f, 4.4f),
                        Yaw = yaw + rng.Range(-15f, 15f),
                        Variant = rng.Range(0, 1000),
                    });
                }
            }
            foreach ((Vec2 center, Vec2 half) in def.Walls)
            {
                level.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Wall, Center = center, HalfSize = half, Height = 4.5f, Variant = rng.Range(0, 1000) });
            }
            foreach ((Vec2 center, float radius) in def.Towers)
            {
                level.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Tower, Center = center, Radius = radius, Height = radius > 2f ? 8f : 6f, Variant = rng.Range(0, 1000) });
            }

            Scatter(level, def, rng);
            PlaceOrbs(level, def, rng);
            return level;
        }

        static void Scatter(Level level, ChapterDef def, Rng rng)
        {
            float limit = def.Half - 3f;
            int placed = 0;
            for (int attempt = 0; attempt < def.TreeCount * 12 && placed < def.TreeCount; attempt++)
            {
                var p = new Vec2(rng.Range(-limit, limit), rng.Range(-limit, limit));
                float roll = rng.Value();
                ObstacleKind kind = roll < def.DeadShare ? ObstacleKind.DeadTree : roll < def.DeadShare + def.OakShare ? ObstacleKind.Oak : ObstacleKind.Pine;
                float radius = kind == ObstacleKind.Oak ? rng.Range(0.45f, 0.65f) : rng.Range(0.3f, 0.45f);
                if (!Free(level, p, radius, 2.4f)) continue;
                level.Obstacles.Add(new Obstacle
                {
                    Kind = kind,
                    Center = p,
                    Radius = radius,
                    Height = kind == ObstacleKind.Pine ? rng.Range(7f, 12f) : kind == ObstacleKind.Oak ? rng.Range(6f, 9f) : rng.Range(4f, 7f),
                    Yaw = rng.Range(0f, 360f),
                    Variant = rng.Range(0, 1000),
                });
                placed++;
            }

            placed = 0;
            for (int attempt = 0; attempt < def.RockCount * 12 && placed < def.RockCount; attempt++)
            {
                var p = new Vec2(rng.Range(-limit, limit), rng.Range(-limit, limit));
                float radius = rng.Chance(0.25f) ? rng.Range(1.4f, 2.2f) : rng.Range(0.6f, 1.2f);
                if (!Free(level, p, radius, 1.6f)) continue;
                level.Obstacles.Add(new Obstacle
                {
                    Kind = ObstacleKind.Rock,
                    Center = p,
                    Radius = radius,
                    Height = radius * rng.Range(0.8f, 1.4f),
                    Yaw = rng.Range(0f, 360f),
                    Variant = rng.Range(0, 1000),
                });
                placed++;
            }
        }

        /// <summary>True when a scattered tree or rock of this size would block nothing important.</summary>
        static bool Free(Level level, Vec2 p, float radius, float spacing)
        {
            if (level.DistanceToPath(p) < PathClearance + radius) return false;
            if (Vec2.Distance(p, level.Start) < 6f || Vec2.Distance(p, level.Portal) < 6f) return false;
            foreach (Flat flat in level.Flats) if (Vec2.Distance(p, flat.Center) < flat.Radius + 2.5f + radius) return false;
            foreach (Vec2 lantern in level.Lanterns) if (Vec2.Distance(p, lantern) < 4f + radius) return false;
            foreach (Vec2 spawn in level.SpawnPoints) if (Vec2.Distance(p, spawn) < 4f + radius) return false;
            foreach (Placement<PickupKind> item in level.Pickups) if (Vec2.Distance(p, item.Position) < 3f + radius) return false;
            foreach (Placement<EnemyKind> foe in level.Enemies) if (Vec2.Distance(p, foe.Position) < (foe.Kind == EnemyKind.Brute ? 6f : 2.5f) + radius) return false;
            foreach (Obstacle o in level.Obstacles)
            {
                if (o.IsBox)
                {
                    if (System.Math.Abs(p.X - o.Center.X) < o.HalfSize.X + radius + 3f && System.Math.Abs(p.Z - o.Center.Z) < o.HalfSize.Z + radius + 3f) return false;
                }
                else if (Vec2.Distance(p, o.Center) < o.Radius + radius + spacing) return false;
            }
            return true;
        }

        /// <summary>Orbs line the trail in little runs, with a few hidden off it.</summary>
        static void PlaceOrbs(Level level, ChapterDef def, Rng rng)
        {
            int placed = 0;
            float travelled = 0f;
            for (int i = 0; i + 1 < level.Path.Count && placed < def.OrbCount * 2 / 3; i++)
            {
                Vec2 a = level.Path[i], b = level.Path[i + 1];
                float length = Vec2.Distance(a, b);
                for (float s = 6f - travelled % 6f; s < length && placed < def.OrbCount * 2 / 3; s += 6f)
                {
                    Vec2 along = (b - a) / length;
                    var side = new Vec2(along.Z, -along.X);
                    Vec2 p = a + along * s + side * (1.4f * (float)System.Math.Sin(s * 0.7f + i));
                    if (Vec2.Distance(p, level.Start) < 4f || level.Blocked(p, 0.3f)) continue;
                    level.Pickups.Add(new Placement<PickupKind>(PickupKind.Orb, p));
                    placed++;
                }
                travelled += length;
            }
            float limit = def.Half - 6f;
            for (int attempt = 0; attempt < 400 && placed < def.OrbCount; attempt++)
            {
                var p = new Vec2(rng.Range(-limit, limit), rng.Range(-limit, limit));
                if (level.Blocked(p, 1f)) continue;
                bool crowded = false;
                foreach (Placement<PickupKind> item in level.Pickups) if (Vec2.Distance(p, item.Position) < 3f) crowded = true;
                if (crowded) continue;
                level.Pickups.Add(new Placement<PickupKind>(PickupKind.Orb, p));
                placed++;
            }
        }
    }
}
