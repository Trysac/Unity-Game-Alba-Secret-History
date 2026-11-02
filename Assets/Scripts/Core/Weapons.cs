using System;

namespace Alba.Core
{
    /// <summary>The four slots of the 2021 game (keys 1-4): a dirk for close work, then three guns.</summary>
    public enum WeaponKind
    {
        Dirk,
        Pistol,
        Shotgun,
        Rifle,
    }

    /// <summary>How one weapon behaves.</summary>
    public sealed class WeaponSpec
    {
        public WeaponKind Kind;
        public string Name;
        public int Damage;
        public int Pellets = 1;
        public float Range;
        /// <summary>Seconds between shots or swings.</summary>
        public float Cooldown;
        /// <summary>Random spread of each pellet, in degrees either side.</summary>
        public float Spread;
        /// <summary>For the dirk: half the angle of the swing, in degrees.</summary>
        public float Arc;
        /// <summary>Fires while the trigger is held; otherwise once per press.</summary>
        public bool Automatic;
        public bool UsesAmmo = true;
        public int StartAmmo;
        public int MaxAmmo;

        public static readonly WeaponSpec[] All =
        {
            new WeaponSpec { Kind = WeaponKind.Dirk, Name = "Dirk", Damage = 34, Range = 2.3f, Cooldown = 0.42f, Arc = 40f, UsesAmmo = false },
            new WeaponSpec { Kind = WeaponKind.Pistol, Name = "Pistol", Damage = 26, Range = 40f, Cooldown = 0.26f, Spread = 0.8f, StartAmmo = 24, MaxAmmo = 90 },
            new WeaponSpec { Kind = WeaponKind.Shotgun, Name = "Shotgun", Damage = 11, Pellets = 8, Range = 17f, Cooldown = 0.85f, Spread = 7f, StartAmmo = 6, MaxAmmo = 40 },
            new WeaponSpec { Kind = WeaponKind.Rifle, Name = "Rifle", Damage = 13, Range = 45f, Cooldown = 0.095f, Spread = 1.6f, Automatic = true, StartAmmo = 0, MaxAmmo = 240 },
        };

        public static WeaponSpec Of(WeaponKind kind) => All[(int)kind];
    }

    public enum FireResult
    {
        /// <summary>Still cooling down, or a semi-automatic trigger that was not pressed again.</summary>
        NotReady,
        /// <summary>The trigger clicked on an empty weapon.</summary>
        Empty,
        Fired,
    }

    /// <summary>The weapon in hand, the ammunition for each, and the trigger timing.</summary>
    public sealed class Arsenal
    {
        readonly int[] ammo = new int[WeaponSpec.All.Length];
        float cooldown;

        public Arsenal()
        {
            foreach (WeaponSpec spec in WeaponSpec.All) ammo[(int)spec.Kind] = spec.StartAmmo;
            Current = WeaponKind.Pistol;
        }

        public WeaponKind Current { get; private set; }

        public WeaponSpec Spec => WeaponSpec.Of(Current);

        public float Cooldown => cooldown;

        public int Ammo(WeaponKind kind) => ammo[(int)kind];

        /// <summary>Adds ammunition up to the weapon's limit; returns how much was taken.</summary>
        public int AddAmmo(WeaponKind kind, int amount)
        {
            WeaponSpec spec = WeaponSpec.Of(kind);
            int before = ammo[(int)kind];
            ammo[(int)kind] = Math.Min(spec.MaxAmmo, before + Math.Max(0, amount));
            return ammo[(int)kind] - before;
        }

        public bool IsFull(WeaponKind kind) => WeaponSpec.Of(kind).UsesAmmo && ammo[(int)kind] >= WeaponSpec.Of(kind).MaxAmmo;

        /// <summary>Switches weapon; returns false if it is already in hand.</summary>
        public bool Select(WeaponKind kind)
        {
            if (kind == Current) return false;
            Current = kind;
            cooldown = Math.Max(cooldown, 0.18f);
            return true;
        }

        /// <summary>Steps to the next (+1) or previous (-1) weapon.</summary>
        public void Cycle(int step)
        {
            int count = WeaponSpec.All.Length;
            Select((WeaponKind)(((int)Current + step % count + count) % count));
        }

        public void Tick(float dt) => cooldown = Math.Max(0f, cooldown - dt);

        /// <summary>Pulls the trigger. Spends one round when the weapon fires.</summary>
        public FireResult TryFire(bool pressed, bool held)
        {
            WeaponSpec spec = Spec;
            bool trigger = spec.Automatic ? held || pressed : pressed;
            if (!trigger || cooldown > 0f) return FireResult.NotReady;
            if (spec.UsesAmmo && ammo[(int)spec.Kind] <= 0)
            {
                cooldown = 0.3f;
                return FireResult.Empty;
            }
            if (spec.UsesAmmo) ammo[(int)spec.Kind]--;
            cooldown = spec.Cooldown;
            return FireResult.Fired;
        }

        /// <summary>Copies another arsenal (for checkpoints and chapter starts).</summary>
        public void CopyFrom(Arsenal other)
        {
            Array.Copy(other.ammo, ammo, ammo.Length);
            Current = other.Current;
            cooldown = 0f;
        }
    }
}
