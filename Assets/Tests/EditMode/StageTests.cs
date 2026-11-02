using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class StageTests
    {
        static Pickup Find(Stage stage, PickupKind kind) => stage.Pickups.Find(p => p.Kind == kind);

        static void StandOn(Stage stage, Vec2 point)
        {
            stage.Player.Position = point;
            stage.Tick(Sim.Step, InputFrame.None);
        }

        [Test]
        public void SealstonesOpenThePortalAndWalkingInEscapes()
        {
            Stage stage = Sim.Stage(Sim.Field(new Vec2(5f, 0f), new Vec2(-5f, 0f)));
            Assert.That(stage.GemsTotal, Is.EqualTo(2));
            Assert.That(stage.PortalOpen, Is.False);

            StandOn(stage, stage.Level.Portal);
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Playing), "a closed portal does nothing");

            StandOn(stage, new Vec2(5f, 0f));
            Assert.That(Sim.Happened(stage, StageEventKind.PickedUp), Is.True);
            Assert.That(stage.GemsFound, Is.EqualTo(1));
            Assert.That(stage.PortalOpen, Is.False);
            StandOn(stage, new Vec2(-5f, 0f));
            Assert.That(stage.GemsFound, Is.EqualTo(2));
            Assert.That(Sim.Happened(stage, StageEventKind.PortalOpened), Is.True);
            Assert.That(stage.PortalOpen, Is.True);
            Assert.That(stage.Score, Is.EqualTo(2 * Tuning.GemScore));

            StandOn(stage, stage.Level.Portal);
            Assert.That(Sim.Happened(stage, StageEventKind.Escaped), Is.True);
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Escaped));
        }

        [Test]
        public void OrbsAndThePageAddToTheScore()
        {
            ChapterDef def = Sim.Field();
            def.Supplies = new[] { new Placement<PickupKind>(PickupKind.Orb, new Vec2(3f, 3f)) };
            Stage stage = Sim.Stage(def);
            StandOn(stage, new Vec2(3f, 3f));
            StandOn(stage, def.Page);
            Assert.That(stage.OrbsFound, Is.EqualTo(1));
            Assert.That(stage.PageFound, Is.True);
            Assert.That(stage.Score, Is.EqualTo(Tuning.OrbScore + Tuning.PageScore));
            Assert.That(Find(stage, PickupKind.Page).Taken, Is.True);
        }

        [Test]
        public void AmmunitionAndPotionsStayOnTheGroundWhenTheyCannotBeCarried()
        {
            ChapterDef def = Sim.Field();
            def.Supplies = new[]
            {
                new Placement<PickupKind>(PickupKind.ShotgunAmmo, new Vec2(3f, 0f)),
                new Placement<PickupKind>(PickupKind.Potion, new Vec2(-3f, 0f)),
            };
            Stage stage = Sim.Stage(def);
            Arsenal arsenal = stage.Player.Arsenal;
            arsenal.AddAmmo(WeaponKind.Shotgun, 999);
            stage.Player.Potions = Tuning.MaxPotions;

            StandOn(stage, new Vec2(3f, 0f));
            StandOn(stage, new Vec2(-3f, 0f));
            Assert.That(Find(stage, PickupKind.ShotgunAmmo).Taken, Is.False);
            Assert.That(Find(stage, PickupKind.Potion).Taken, Is.False);

            stage.Player.Potions = 2;
            arsenal.Select(WeaponKind.Shotgun);
            arsenal.Tick(1f);
            arsenal.TryFire(true, true);
            StandOn(stage, new Vec2(3f, 0f));
            StandOn(stage, new Vec2(-3f, 0f));
            Assert.That(Find(stage, PickupKind.ShotgunAmmo).Taken, Is.True);
            Assert.That(arsenal.Ammo(WeaponKind.Shotgun), Is.EqualTo(WeaponSpec.Of(WeaponKind.Shotgun).MaxAmmo), "only the one missing shell is taken");
            Assert.That(stage.Player.Potions, Is.EqualTo(3));
        }

        [Test]
        public void ALanternBecomesTheRespawnPointAndRefillsTheTorch()
        {
            ChapterDef def = Sim.Field();
            def.Lanterns = new[] { new Vec2(10f, 10f) };
            Stage stage = Sim.Stage(def);
            stage.Player.Battery = 0.2f;
            StandOn(stage, new Vec2(10f, 9f));
            Assert.That(Sim.Happened(stage, StageEventKind.LanternLit), Is.True);
            Assert.That(stage.Lanterns[0].Lit, Is.True);
            Assert.That(stage.Player.Battery, Is.EqualTo(1f).Within(0.01f));
            Assert.That(Vec2.Distance(stage.RespawnPoint, new Vec2(10f, 10f)), Is.LessThan(2f));

            stage.Player.Position = new Vec2(-20f, -20f);
            stage.HurtPlayer(500, stage.Player.Position);
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Dying));
            Assert.That(stage.Deaths, Is.EqualTo(1));
            stage.Respawn();
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Playing));
            Assert.That(stage.Player.Position, Is.EqualTo(stage.RespawnPoint));
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth));
        }

        [Test]
        public void WhileDyingNothingIsCollectedAndNoMoreDamageIsTaken()
        {
            ChapterDef def = Sim.Field();
            def.Supplies = new[] { new Placement<PickupKind>(PickupKind.Orb, new Vec2(0f, 0.5f)) };
            Stage stage = Sim.Stage(def);
            stage.HurtPlayer(500, Vec2.Zero);
            stage.Tick(Sim.Step, InputFrame.None);
            Assert.That(stage.OrbsFound, Is.Zero);
            stage.Player.HurtTimer = 0f;
            stage.HurtPlayer(5, Vec2.Zero);
            Assert.That(stage.Deaths, Is.EqualTo(1));
            Assert.That(Sim.Happened(stage, StageEventKind.PlayerHurt), Is.False);
        }

        [Test]
        public void ThePortalStaysShutWhileTheGiantLives()
        {
            Level level = LevelBuilder.Build(Chapters.All[2]);
            var stage = new Stage(level, 5);
            Assert.That(stage.Boss, Is.Not.Null);
            foreach (Pickup p in stage.Pickups) if (p.Kind == PickupKind.Gem) StandOn(stage, p.Position);
            Assert.That(stage.GemsFound, Is.EqualTo(stage.GemsTotal));
            Assert.That(stage.PortalOpen, Is.False);

            stage.Boss.TakeHit(10000, Vec2.Zero);
            stage.Player.Position = level.Start;
            stage.Tick(Sim.Step, InputFrame.None);
            Assert.That(stage.PortalOpen, Is.True);
            Assert.That(Sim.Happened(stage, StageEventKind.PortalOpened), Is.True);
        }

        [Test]
        public void TheLastChapterSendsWavesUntilDawnThenOpensThePortal()
        {
            Level level = LevelBuilder.Build(Chapters.All[3]);
            var stage = new Stage(level, 5);
            stage.Player.HurtTimer = 1000f;
            Assert.That(stage.PortalOpen, Is.False);
            Assert.That(Sim.RunUntil(stage, StageEventKind.WaveStarted, 4f, InputFrame.None), Is.True);
            Assert.That(stage.Wave, Is.EqualTo(1));
            Assert.That(stage.EnemiesAlive, Is.EqualTo(2));
            Assert.That(stage.Enemies.TrueForAll(e => e.Relentless && e.State == EnemyState.Chase), Is.True);

            for (float t = 0f; t < 120f && !stage.DawnBroke; t += Sim.Step)
            {
                stage.Player.HurtTimer = 1000f;
                stage.Tick(Sim.Step, InputFrame.None);
            }
            Assert.That(stage.DawnBroke, Is.True);
            Assert.That(stage.Elapsed, Is.EqualTo(Chapters.All[3].SurvivalSeconds).Within(0.1f));
            Assert.That(stage.DawnProgress, Is.EqualTo(1f));
            Assert.That(stage.Wave, Is.GreaterThan(8));
            Assert.That(stage.EnemiesAlive, Is.Zero, "dawn burns every creature away");
            Assert.That(stage.KillScore, Is.Zero, "and they give no points");
            Assert.That(stage.PortalOpen, Is.True);
        }
    }
}
