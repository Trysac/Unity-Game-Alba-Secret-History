using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class RecordsTests
    {
        [Test]
        public void OnlyAHigherScoreBecomesTheBestAndItIsSaved()
        {
            var store = new MemoryStore();
            var records = new Records(store);
            Assert.That(records.Best, Is.Zero);
            Assert.That(records.Submit(800), Is.True);
            Assert.That(records.Submit(500), Is.False);
            Assert.That(records.Submit(800), Is.False);
            Assert.That(new Records(store).Best, Is.EqualTo(800), "kept in the store");
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ChaptersUnlockOnlyForwardsAndStayInRange()
        {
            var records = new Records(new MemoryStore());
            Assert.That(records.Reached, Is.Zero);
            records.Unlock(2);
            records.Unlock(1);
            Assert.That(records.Reached, Is.EqualTo(2));
            records.Unlock(99);
            Assert.That(records.Reached, Is.EqualTo(Chapters.All.Length - 1));
        }

        [Test]
        public void JournalPagesAreRememberedOnce()
        {
            var records = new Records(new MemoryStore());
            Assert.That(records.FindPage(1), Is.True);
            Assert.That(records.FindPage(1), Is.False);
            Assert.That(records.FindPage(3), Is.True);
            Assert.That(records.HasPage(1), Is.True);
            Assert.That(records.HasPage(0), Is.False);
            Assert.That(records.PagesFound, Is.EqualTo(2));
        }

        [Test]
        public void SettingsHaveDefaultsAreClampedAndPersist()
        {
            var store = new MemoryStore();
            var records = new Records(store);
            Assert.That(records.MusicVolume, Is.EqualTo(6));
            Assert.That(records.SoundVolume, Is.EqualTo(8));
            Assert.That(records.LookSpeed, Is.EqualTo(5));
            Assert.That(records.InvertY, Is.False);
            Assert.That(records.Brightness, Is.EqualTo(2));

            records.MusicVolume = 40;
            records.SoundVolume = -3;
            records.LookSpeed = 0;
            records.InvertY = true;
            records.Brightness = 9;
            var reloaded = new Records(store);
            Assert.That(reloaded.MusicVolume, Is.EqualTo(Records.MaxVolume));
            Assert.That(reloaded.SoundVolume, Is.Zero);
            Assert.That(reloaded.LookSpeed, Is.EqualTo(Records.MinLook));
            Assert.That(reloaded.InvertY, Is.True);
            Assert.That(reloaded.Brightness, Is.EqualTo(Records.MaxBrightness));
        }

        [Test]
        public void EscapesAreCounted()
        {
            var records = new Records(new MemoryStore());
            records.AddEscape();
            records.AddEscape();
            Assert.That(records.Escapes, Is.EqualTo(2));
        }
    }
}
