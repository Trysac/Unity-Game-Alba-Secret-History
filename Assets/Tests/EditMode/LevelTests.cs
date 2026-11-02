using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class LevelTests
    {
        static IEnumerable<int> ChapterIndices()
        {
            for (int i = 0; i < Chapters.All.Length; i++) yield return i;
        }

        [Test]
        public void ThereAreFourChaptersWithTheirSealstoneCounts()
        {
            Assert.That(Chapters.All.Length, Is.EqualTo(4));
            int[] gems = { 3, 4, 3, 0 };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Chapters.All[i].Index, Is.EqualTo(i));
                Assert.That(LevelBuilder.Build(Chapters.All[i]).GemCount, Is.EqualTo(gems[i]), Chapters.All[i].Title);
            }
            Assert.That(Chapters.All[2].HasBoss, Is.True);
            Assert.That(Chapters.All[3].SurvivalSeconds, Is.GreaterThan(0f));
        }

        [TestCaseSource(nameof(ChapterIndices))]
        public void BuildingAChapterTwiceGivesTheSameLevel(int chapter)
        {
            Level a = LevelBuilder.Build(Chapters.All[chapter]);
            Level b = LevelBuilder.Build(Chapters.All[chapter]);
            Assert.That(a.Obstacles.Count, Is.EqualTo(b.Obstacles.Count));
            for (int i = 0; i < a.Obstacles.Count; i++)
            {
                Assert.That(a.Obstacles[i].Center, Is.EqualTo(b.Obstacles[i].Center));
                Assert.That(a.Obstacles[i].Kind, Is.EqualTo(b.Obstacles[i].Kind));
            }
            Assert.That(a.Pickups.Count, Is.EqualTo(b.Pickups.Count));
            Assert.That(a.HeightAt(12.3f, -4.5f), Is.EqualTo(b.HeightAt(12.3f, -4.5f)));
        }

        [TestCaseSource(nameof(ChapterIndices))]
        public void ScatteringFillsTheChapterWithTreesRocksAndOrbs(int chapter)
        {
            ChapterDef def = Chapters.All[chapter];
            Level level = LevelBuilder.Build(def);
            int trees = level.Obstacles.FindAll(o => o.Kind == ObstacleKind.Pine || o.Kind == ObstacleKind.Oak || o.Kind == ObstacleKind.DeadTree).Count;
            int rocks = level.Obstacles.FindAll(o => o.Kind == ObstacleKind.Rock).Count;
            int orbs = level.Pickups.FindAll(p => p.Kind == PickupKind.Orb).Count;
            Assert.That(trees, Is.GreaterThanOrEqualTo(def.TreeCount * 9 / 10), "trees");
            Assert.That(rocks, Is.GreaterThanOrEqualTo(def.RockCount * 9 / 10), "rocks");
            Assert.That(orbs, Is.EqualTo(def.OrbCount), "orbs");
        }

        [TestCaseSource(nameof(ChapterIndices))]
        public void NothingImportantIsInsideAnObstacle(int chapter)
        {
            Level level = LevelBuilder.Build(Chapters.All[chapter]);
            Assert.That(level.Blocked(level.Start, Tuning.PlayerRadius), Is.False, "start");
            Assert.That(level.Blocked(level.Portal, Tuning.PlayerRadius), Is.False, "portal");
            foreach (Vec2 lantern in level.Lanterns) Assert.That(level.Blocked(lantern, Tuning.PlayerRadius), Is.False, $"lantern {lantern}");
            foreach (Placement<PickupKind> p in level.Pickups) Assert.That(level.Blocked(p.Position, 0.2f), Is.False, $"{p.Kind} {p.Position}");
            foreach (Placement<EnemyKind> e in level.Enemies) Assert.That(level.Blocked(e.Position, EnemySpec.Of(e.Kind).Radius), Is.False, $"{e.Kind} {e.Position}");
        }

        [TestCaseSource(nameof(ChapterIndices))]
        public void EverySealstoneLanternPageAndThePortalCanBeReachedOnFoot(int chapter)
        {
            Level level = LevelBuilder.Build(Chapters.All[chapter]);
            bool[,] reached = Flood(level, out Func<Vec2, (int, int)> cell);
            var targets = new List<(string, Vec2)> { ("portal", level.Portal) };
            foreach (Vec2 lantern in level.Lanterns) targets.Add(("lantern", lantern));
            foreach (Placement<PickupKind> p in level.Pickups) if (p.Kind != PickupKind.Orb) targets.Add((p.Kind.ToString(), p.Position));
            foreach ((string name, Vec2 at) in targets)
            {
                (int x, int z) = cell(at);
                bool near = false;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (x + dx >= 0 && z + dz >= 0 && x + dx < reached.GetLength(0) && z + dz < reached.GetLength(1)) near |= reached[x + dx, z + dz];
                Assert.That(near, Is.True, $"{name} at {at} cannot be reached");
            }
        }

        /// <summary>Flood-fills half-metre cells a player fits in, starting where the chapter starts.</summary>
        static bool[,] Flood(Level level, out Func<Vec2, (int, int)> cell)
        {
            const float size = 0.5f;
            int n = (int)(level.Half * 2f / size);
            float half = level.Half;
            cell = p => ((int)((p.X + half) / size), (int)((p.Z + half) / size));
            var open = new bool[n, n];
            var seen = new bool[n, n];
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    open[x, z] = !level.Blocked(new Vec2(-half + (x + 0.5f) * size, -half + (z + 0.5f) * size), Tuning.PlayerRadius);
            var queue = new Queue<(int, int)>();
            (int sx, int sz) = cell(level.Start);
            seen[sx, sz] = true;
            queue.Enqueue((sx, sz));
            while (queue.Count > 0)
            {
                (int x, int z) = queue.Dequeue();
                foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n || seen[nx, nz] || !open[nx, nz]) continue;
                    seen[nx, nz] = true;
                    queue.Enqueue((nx, nz));
                }
            }
            return seen;
        }

        [Test]
        public void FlatsAreLevelAndTheGroundRollsElsewhere()
        {
            Level level = LevelBuilder.Build(Chapters.All[0]);
            Flat circle = level.Flats[0];
            Assert.That(level.HeightAt(circle.Center), Is.EqualTo(circle.Height).Within(0.001f));
            Assert.That(level.HeightAt(circle.Center + new Vec2(4f, 3f)), Is.EqualTo(circle.Height).Within(0.001f));
            float min = float.MaxValue, max = float.MinValue;
            for (int i = -40; i <= 40; i += 4)
            {
                float h = level.HeightAt(i, -30f);
                min = Math.Min(min, h);
                max = Math.Max(max, h);
            }
            Assert.That(max - min, Is.GreaterThan(0.5f), "hills");
        }

        [Test]
        public void RaysStopAtTheFirstObstacle()
        {
            Level level = LevelBuilder.Build(Sim.Field());
            level.Obstacles.Add(Sim.Tree(0f, 10f, 1f));
            level.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Wall, Center = new Vec2(10f, 0f), HalfSize = new Vec2(1f, 5f) });
            Assert.That(level.Raycast(Vec2.Zero, new Vec2(0f, 1f), 50f), Is.EqualTo(9f).Within(0.001f));
            Assert.That(level.Raycast(Vec2.Zero, new Vec2(1f, 0f), 50f), Is.EqualTo(9f).Within(0.001f));
            Assert.That(level.Raycast(Vec2.Zero, new Vec2(-1f, 0f), 50f), Is.EqualTo(50f));
            Assert.That(level.CanSee(Vec2.Zero, new Vec2(0f, 20f)), Is.False);
            Assert.That(level.CanSee(Vec2.Zero, new Vec2(0f, 8f)), Is.True);
        }

        [Test]
        public void WallsPushThePlayerOutByTheNearestSide()
        {
            Level level = LevelBuilder.Build(Sim.Field());
            level.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Wall, Center = new Vec2(10f, 0f), HalfSize = new Vec2(1f, 5f) });
            Vec2 pushed = level.Resolve(new Vec2(9.2f, 0f), Tuning.PlayerRadius);
            Assert.That(pushed.X, Is.EqualTo(9f - Tuning.PlayerRadius).Within(0.001f));
            Vec2 corner = level.Resolve(new Vec2(8.8f, 5.1f), Tuning.PlayerRadius);
            Assert.That(Vec2.Distance(corner, new Vec2(9f, 5f)), Is.EqualTo(Tuning.PlayerRadius).Within(0.001f));
        }
    }
}
