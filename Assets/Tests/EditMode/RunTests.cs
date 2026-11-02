using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class RunTests
    {
        static void Collect(Run run, PickupKind kind)
        {
            foreach (Pickup p in run.Stage.Pickups)
            {
                if (p.Kind != kind || p.Taken) continue;
                run.Stage.Player.Position = p.Position;
                run.Stage.Player.HurtTimer = 100f;
                run.Tick(Sim.Step, InputFrame.None);
            }
        }

        static void Escape(Run run)
        {
            Collect(run, PickupKind.Gem);
            if (run.Stage.Boss != null) run.Stage.Boss.TakeHit(100000, Vec2.Zero);
            run.Stage.Player.Position = run.Level.Portal;
            run.Tick(Sim.Step, InputFrame.None);
        }

        static void Die(Run run)
        {
            run.Stage.Player.HurtTimer = 0f;
            run.Stage.HurtPlayer(1000, run.Stage.Player.Position);
            run.Tick(Sim.Step, InputFrame.None);
            for (float t = 0f; t < Tuning.DyingTime + 0.1f; t += Sim.Step) run.Tick(Sim.Step, InputFrame.None);
        }

        [Test]
        public void ANewRunStartsTheFirstChapterWithThreeLives()
        {
            var run = new Run();
            Assert.That(run.ChapterIndex, Is.Zero);
            Assert.That(run.Lives, Is.EqualTo(Tuning.StartingLives));
            Assert.That(run.Score, Is.Zero);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(run.Stage.Player.Position, Is.EqualTo(Chapters.All[0].Start));
        }

        [Test]
        public void ClearingAChapterBanksItsScoreWithBonusesAndCarriesTheKitOn()
        {
            var run = new Run();
            Collect(run, PickupKind.Orb);
            int orbs = run.Stage.OrbsFound;
            Assert.That(orbs, Is.EqualTo(Chapters.All[0].OrbCount));
            run.Stage.Player.Arsenal.AddAmmo(WeaponKind.Rifle, 30);
            run.Stage.Player.Potions = 4;
            run.Stage.Player.Health = 20;
            Escape(run);

            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.ChapterClear));
            int expected = orbs * Tuning.OrbScore + 3 * Tuning.GemScore + Tuning.ChapterScore + Tuning.FlawlessScore;
            Assert.That(run.BankedScore, Is.EqualTo(expected));
            Assert.That(run.Score, Is.EqualTo(expected));
            Assert.That(run.LastFlawlessBonus, Is.EqualTo(Tuning.FlawlessScore));

            run.NextChapter();
            Assert.That(run.ChapterIndex, Is.EqualTo(1));
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(run.Stage.Player.Arsenal.Ammo(WeaponKind.Rifle), Is.EqualTo(30));
            Assert.That(run.Stage.Player.Potions, Is.EqualTo(4));
            Assert.That(run.Stage.Player.Health, Is.EqualTo(Tuning.MaxHealth), "health is restored between chapters");
            Assert.That(run.Score, Is.EqualTo(expected));
        }

        [Test]
        public void DyingCostsALifeAndTheLastOneEndsTheRun()
        {
            var run = new Run();
            Die(run);
            Assert.That(run.Lives, Is.EqualTo(Tuning.StartingLives - 1));
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(run.Stage.Phase, Is.EqualTo(StagePhase.Playing), "respawned");
            Die(run);
            Die(run);
            Assert.That(run.Lives, Is.Zero);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.GameOver));
            Assert.That(run.TotalDeaths, Is.EqualTo(3));
        }

        [Test]
        public void ADeathCostsTheFlawlessBonus()
        {
            var run = new Run();
            Die(run);
            Escape(run);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.ChapterClear));
            Assert.That(run.LastFlawlessBonus, Is.Zero);
            Assert.That(run.BankedScore, Is.EqualTo(3 * Tuning.GemScore + Tuning.ChapterScore));
        }

        [Test]
        public void RetryingAfterAGameOverRestoresTheChapterStart()
        {
            var run = new Run();
            Escape(run);
            run.NextChapter();
            int banked = run.BankedScore;
            int pistol = run.Stage.Player.Arsenal.Ammo(WeaponKind.Pistol);
            Collect(run, PickupKind.Orb);
            run.Stage.Player.Arsenal.Tick(1f);
            run.Stage.Tick(Sim.Step, Sim.Fire);
            Assert.That(run.Score, Is.GreaterThan(banked));
            Die(run);
            Die(run);
            Die(run);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.GameOver));

            run.RetryChapter();
            Assert.That(run.ChapterIndex, Is.EqualTo(1));
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Playing));
            Assert.That(run.Lives, Is.EqualTo(Tuning.StartingLives));
            Assert.That(run.Score, Is.EqualTo(banked));
            Assert.That(run.Stage.Player.Arsenal.Ammo(WeaponKind.Pistol), Is.EqualTo(pistol));
            Assert.That(run.Stage.OrbsFound, Is.Zero);
        }

        [Test]
        public void EscapingTheLastChapterWinsTheRun()
        {
            var run = new Run(3);
            Assert.That(run.IsLastChapter, Is.True);
            run.Stage.Player.HurtTimer = 1000f;
            for (float t = 0f; t < Chapters.All[3].SurvivalSeconds + 1f; t += Sim.Step)
            {
                run.Stage.Player.HurtTimer = 1000f;
                run.Tick(Sim.Step, InputFrame.None);
            }
            Assert.That(run.Stage.PortalOpen, Is.True);
            Escape(run);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Victory));
        }

        [Test]
        public void StartingLaterChaptersFromTheMenuGivesMoreSupplies()
        {
            Player first = Run.DefaultKit(0);
            Player third = Run.DefaultKit(2);
            Assert.That(third.Potions, Is.GreaterThan(first.Potions));
            Assert.That(third.Arsenal.Ammo(WeaponKind.Pistol), Is.GreaterThan(first.Arsenal.Ammo(WeaponKind.Pistol)));
            Assert.That(third.Arsenal.Ammo(WeaponKind.Rifle), Is.GreaterThan(0));
            Assert.That(first.Arsenal.Ammo(WeaponKind.Rifle), Is.Zero);
            var run = new Run(2);
            Assert.That(run.ChapterIndex, Is.EqualTo(2));
            Assert.That(run.Stage.Player.Potions, Is.EqualTo(third.Potions));
        }
    }
}
