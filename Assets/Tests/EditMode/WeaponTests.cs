using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class WeaponTests
    {
        [Test]
        public void ThePistolSpendsOneRoundPerShotAndWaitsForItsCooldown()
        {
            var arsenal = new Arsenal();
            Assert.That(arsenal.Current, Is.EqualTo(WeaponKind.Pistol));
            int start = arsenal.Ammo(WeaponKind.Pistol);

            Assert.That(arsenal.TryFire(true, true), Is.EqualTo(FireResult.Fired));
            Assert.That(arsenal.Ammo(WeaponKind.Pistol), Is.EqualTo(start - 1));
            Assert.That(arsenal.TryFire(true, true), Is.EqualTo(FireResult.NotReady), "still cooling down");
            Assert.That(arsenal.Ammo(WeaponKind.Pistol), Is.EqualTo(start - 1));

            arsenal.Tick(WeaponSpec.Of(WeaponKind.Pistol).Cooldown);
            Assert.That(arsenal.TryFire(true, true), Is.EqualTo(FireResult.Fired));
            Assert.That(arsenal.Ammo(WeaponKind.Pistol), Is.EqualTo(start - 2));
        }

        [Test]
        public void ThePistolNeedsAFreshPressButTheRifleFiresWhileHeld()
        {
            var arsenal = new Arsenal();
            arsenal.Tick(1f);
            Assert.That(arsenal.TryFire(false, true), Is.EqualTo(FireResult.NotReady), "holding the trigger does not fire the pistol");

            arsenal.AddAmmo(WeaponKind.Rifle, 10);
            arsenal.Select(WeaponKind.Rifle);
            arsenal.Tick(1f);
            Assert.That(arsenal.TryFire(false, true), Is.EqualTo(FireResult.Fired));
            arsenal.Tick(WeaponSpec.Of(WeaponKind.Rifle).Cooldown);
            Assert.That(arsenal.TryFire(false, true), Is.EqualTo(FireResult.Fired));
            Assert.That(arsenal.Ammo(WeaponKind.Rifle), Is.EqualTo(8));
        }

        [Test]
        public void AnEmptyWeaponClicksAndStaysAtZero()
        {
            var arsenal = new Arsenal();
            arsenal.Select(WeaponKind.Rifle);
            arsenal.Tick(1f);
            Assert.That(arsenal.Ammo(WeaponKind.Rifle), Is.Zero);
            Assert.That(arsenal.TryFire(true, true), Is.EqualTo(FireResult.Empty));
            Assert.That(arsenal.Ammo(WeaponKind.Rifle), Is.Zero);
        }

        [Test]
        public void TheDirkNeverRunsOutAndNeverChangesAnyAmmo()
        {
            var arsenal = new Arsenal();
            int pistol = arsenal.Ammo(WeaponKind.Pistol);
            arsenal.Select(WeaponKind.Dirk);
            for (int i = 0; i < 20; i++)
            {
                arsenal.Tick(1f);
                Assert.That(arsenal.TryFire(true, true), Is.EqualTo(FireResult.Fired));
            }
            Assert.That(arsenal.Ammo(WeaponKind.Pistol), Is.EqualTo(pistol));
            Assert.That(arsenal.IsFull(WeaponKind.Dirk), Is.False);
        }

        [Test]
        public void AmmunitionIsCappedAtTheWeaponMaximum()
        {
            var arsenal = new Arsenal();
            int max = WeaponSpec.Of(WeaponKind.Shotgun).MaxAmmo;
            int taken = arsenal.AddAmmo(WeaponKind.Shotgun, 1000);
            Assert.That(taken, Is.EqualTo(max - WeaponSpec.Of(WeaponKind.Shotgun).StartAmmo));
            Assert.That(arsenal.Ammo(WeaponKind.Shotgun), Is.EqualTo(max));
            Assert.That(arsenal.IsFull(WeaponKind.Shotgun), Is.True);
            Assert.That(arsenal.AddAmmo(WeaponKind.Shotgun, 5), Is.Zero);
        }

        [Test]
        public void CyclingWrapsRoundTheFourSlotsAndSelectingTheSameWeaponDoesNothing()
        {
            var arsenal = new Arsenal();
            arsenal.Cycle(1);
            Assert.That(arsenal.Current, Is.EqualTo(WeaponKind.Shotgun));
            arsenal.Cycle(1);
            arsenal.Cycle(1);
            Assert.That(arsenal.Current, Is.EqualTo(WeaponKind.Dirk));
            arsenal.Cycle(-1);
            Assert.That(arsenal.Current, Is.EqualTo(WeaponKind.Rifle));
            Assert.That(arsenal.Select(WeaponKind.Rifle), Is.False);
            Assert.That(arsenal.Select(WeaponKind.Pistol), Is.True);
        }
    }
}
