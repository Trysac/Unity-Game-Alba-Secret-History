using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class EnemyTests
    {
        static Stage WithFoe(EnemyKind kind, float x, float z, params Obstacle[] obstacles)
        {
            ChapterDef def = Sim.Field();
            def.Foes = new[] { new Placement<EnemyKind>(kind, new Vec2(x, z)) };
            return Sim.Stage(def, obstacles);
        }

        [Test]
        public void ACrawlerWandersUntilThePlayerComesCloseThenGivesChase()
        {
            Stage stage = WithFoe(EnemyKind.Crawler, 0f, 30f);
            Enemy crawler = stage.Enemies[0];
            Sim.Run(stage, 2f);
            Assert.That(crawler.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(Vec2.Distance(crawler.Position, crawler.Home), Is.LessThan(6f), "stays near home");

            stage.Player.Position = new Vec2(crawler.Position.X, crawler.Position.Z - 10f);
            Assert.That(Sim.RunUntil(stage, StageEventKind.EnemyAlerted, 0.5f, InputFrame.None), Is.True);
            Assert.That(crawler.State, Is.EqualTo(EnemyState.Chase));
            float before = Vec2.Distance(crawler.Position, stage.Player.Position);
            Sim.Run(stage, 0.5f);
            Assert.That(Vec2.Distance(crawler.Position, stage.Player.Position), Is.LessThan(before - 1f));
        }

        [Test]
        public void ARockBetweenThemHidesThePlayer()
        {
            var rock = new Obstacle { Kind = ObstacleKind.Rock, Center = new Vec2(0f, 5f), Radius = 2f };
            Stage stage = WithFoe(EnemyKind.Crawler, 0f, 10f, rock);
            Sim.Run(stage, 0.5f);
            Assert.That(stage.Enemies[0].State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void AStrikeLandsOnlyAfterTheWindupWhenThePlayerStaysClose()
        {
            Stage stage = WithFoe(EnemyKind.Crawler, 0f, 1.5f);
            Enemy crawler = stage.Enemies[0];
            Assert.That(Sim.RunUntil(stage, StageEventKind.EnemyWindup, 1f, InputFrame.None), Is.True);
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth), "nothing yet: this is the tell");
            Assert.That(Sim.RunUntil(stage, StageEventKind.PlayerHurt, crawler.Spec.Windup + 0.1f, InputFrame.None), Is.True);
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth - crawler.Spec.Damage));
            Assert.That(stage.Player.HurtTimer, Is.GreaterThan(0f));
        }

        [Test]
        public void BackingOffDuringTheWindupMakesTheStrikeMiss()
        {
            Stage stage = WithFoe(EnemyKind.Crawler, 0f, 1.5f);
            Assert.That(Sim.RunUntil(stage, StageEventKind.EnemyWindup, 1f, InputFrame.None), Is.True);
            var back = new InputFrame(new Vec2(0f, -1f), sprint: false);
            Assert.That(Sim.RunUntil(stage, StageEventKind.EnemyMissed, 1f, back), Is.True);
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth));
        }

        [Test]
        public void HurtGraceStopsTwoHitsInARow()
        {
            Stage stage = Sim.Stage(Sim.Field());
            stage.HurtPlayer(10, Vec2.Zero);
            stage.HurtPlayer(10, Vec2.Zero);
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth - 10));
            Sim.Run(stage, Tuning.HurtGrace + 0.05f);
            stage.HurtPlayer(10, Vec2.Zero);
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth - 20));
        }

        [Test]
        public void ShootingACrawlerDownAwardsItsScore()
        {
            Stage stage = WithFoe(EnemyKind.Crawler, 0f, 8f);
            Enemy crawler = stage.Enemies[0];
            int shots = 0;
            while (crawler.Alive && shots < 10)
            {
                stage.Tick(Sim.Step, Sim.Fire);
                Assert.That(Sim.Happened(stage, StageEventKind.Fired), Is.True);
                shots++;
                Sim.Run(stage, WeaponSpec.Of(WeaponKind.Pistol).Cooldown);
                stage.Player.Position = Vec2.Zero;
                stage.Player.Yaw = (crawler.Position - stage.Player.Position).Yaw;
            }
            Assert.That(crawler.Alive, Is.False);
            Assert.That(shots, Is.EqualTo(2), "40 health, 26 per pistol round");
            Assert.That(stage.Kills, Is.EqualTo(1));
            Assert.That(stage.KillScore, Is.EqualTo(crawler.Spec.Score));
            Assert.That(stage.Score, Is.EqualTo(crawler.Spec.Score));
        }

        [Test]
        public void AWispShrugsOffBulletsInTheDarkButDiesInTheTorchBeam()
        {
            Stage stage = WithFoe(EnemyKind.Wisp, 0f, 6f);
            Enemy wisp = stage.Enemies[0];
            stage.Player.LightOn = false;
            stage.Tick(Sim.Step, Sim.Fire);
            Assert.That(Sim.Happened(stage, StageEventKind.EnemyImmune), Is.True);
            Assert.That(wisp.Health, Is.EqualTo(wisp.Spec.Health));

            stage.Player.LightOn = true;
            Sim.Run(stage, 0.3f);
            stage.Player.Yaw = (wisp.Position - stage.Player.Position).Yaw;
            stage.Tick(Sim.Step, Sim.Fire);
            Assert.That(wisp.Lit, Is.True);
            Assert.That(Sim.Happened(stage, StageEventKind.EnemyHit), Is.True);
            Assert.That(wisp.Health, Is.EqualTo(wisp.Spec.Health - WeaponSpec.Of(WeaponKind.Pistol).Damage));
        }

        [Test]
        public void TheTorchBeamSlowsAWisp()
        {
            Stage dark = WithFoe(EnemyKind.Wisp, 0f, 12f);
            Stage lit = WithFoe(EnemyKind.Wisp, 0f, 12f);
            dark.Player.LightOn = false;
            Sim.Run(dark, 1f);
            Sim.Run(lit, 1f);
            Assert.That(lit.Enemies[0].Position.Z, Is.GreaterThan(dark.Enemies[0].Position.Z + 1f), "the lit wisp is held back");
        }

        [Test]
        public void TheGiantRagesAtHalfHealthAndCallsCrawlers()
        {
            Stage stage = WithFoe(EnemyKind.Brute, 0f, 10f);
            Enemy brute = stage.Enemies[0];
            stage.Player.Arsenal.Select(WeaponKind.Shotgun);
            stage.Player.Arsenal.AddAmmo(WeaponKind.Shotgun, 40);
            bool enraged = false;
            for (int i = 0; i < 40 && !enraged; i++)
            {
                stage.Player.Position = Vec2.Zero;
                stage.Player.HurtTimer = 10f;
                stage.Player.Yaw = (brute.Position - stage.Player.Position).Yaw;
                enraged = Sim.RunUntil(stage, StageEventKind.BossEnraged, 1f, Sim.Fire);
            }
            Assert.That(enraged, Is.True);
            Assert.That(brute.Enraged, Is.True);
            Assert.That(brute.Health, Is.LessThanOrEqualTo(brute.Spec.Health / 2));
            Assert.That(stage.Enemies.FindAll(e => e.Kind == EnemyKind.Crawler && e.Alive).Count, Is.EqualTo(Tuning.BossSummons));
        }

        [Test]
        public void RespawningSendsNearbyHuntersHome()
        {
            Stage stage = WithFoe(EnemyKind.Crawler, 3f, 3f);
            Sim.Run(stage, 0.2f);
            Enemy crawler = stage.Enemies[0];
            Assert.That(crawler.State, Is.Not.EqualTo(EnemyState.Idle));
            stage.Player.HurtTimer = 0f;
            stage.HurtPlayer(500, crawler.Position, crawler);
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Dying));
            stage.Respawn();
            Assert.That(crawler.State, Is.EqualTo(EnemyState.Idle));
            Sim.Run(stage, 1f);
            Assert.That(crawler.State, Is.EqualTo(EnemyState.Idle), "calm for a moment after the respawn");
        }
    }
}
