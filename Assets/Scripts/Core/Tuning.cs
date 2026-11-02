namespace Alba.Core
{
    /// <summary>Every number that sets the feel of the game, in metres, seconds and hit points.</summary>
    public static class Tuning
    {
        // ---- Player ----
        public const float PlayerRadius = 0.4f;
        public const float EyeHeight = 1.65f;
        public const float WalkSpeed = 4.4f;
        public const float SprintSpeed = 7.2f;
        public const float Acceleration = 30f;
        public const float StaminaDrain = 0.22f;
        public const float StaminaRegen = 0.18f;
        /// <summary>After running dry the player must recover this much before sprinting again.</summary>
        public const float StaminaResume = 0.3f;
        public const int MaxHealth = 100;
        public const float HurtGrace = 0.6f;

        // ---- Flask and flashlight ----
        public const int PotionHeal = 45;
        public const int MaxPotions = 9;
        public const float BatteryDrain = 1f / 75f;
        public const float BatteryRecharge = 1f / 20f;
        public const float FlashlightRange = 17f;
        public const float FlashlightHalfAngle = 19f;

        // ---- Pickups ----
        public const float PickupRadius = 1.3f;
        public const float LanternRadius = 2.6f;
        public const float PortalRadius = 1.8f;
        public const int PistolPickup = 12;
        public const int ShotgunPickup = 6;
        public const int RiflePickup = 45;

        // ---- Score ----
        public const int OrbScore = 10;
        public const int GemScore = 50;
        public const int PageScore = 100;
        public const int ChapterScore = 250;
        public const int FlawlessScore = 250;

        // ---- Run ----
        public const int StartingLives = 3;
        public const float DyingTime = 2.2f;

        // ---- Enemies ----
        public const float LeashTime = 4f;
        public const float StaggerTime = 0.18f;
        public const float BossSecondPhase = 0.5f;
        public const int BossSummons = 3;
        public const float DropChance = 0.35f;

        // ---- Last chapter ----
        public const float WaveInterval = 9f;
    }
}
