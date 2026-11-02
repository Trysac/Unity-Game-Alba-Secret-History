namespace Alba.Core
{
    /// <summary>Things that happened during a step, for sound, effects and the HUD to react to.</summary>
    public enum StageEventKind
    {
        Fired,
        Swung,
        DryFire,
        /// <summary>A bullet or pellet ended here (on a tree, a rock, a creature or in the air).</summary>
        Impact,
        EnemyHit,
        /// <summary>A bullet passed through a wisp that was not in the torch beam.</summary>
        EnemyImmune,
        EnemyKilled,
        EnemyAlerted,
        EnemyWindup,
        EnemyMissed,
        EnemySpawned,
        EnemyDissolved,
        BossEnraged,
        BossDefeated,
        PlayerHurt,
        PlayerDied,
        Respawned,
        PickedUp,
        Dropped,
        PotionDrunk,
        PotionRefused,
        WeaponSwitched,
        TorchToggled,
        TorchEmpty,
        LanternLit,
        PortalOpened,
        Escaped,
        WaveStarted,
        Dawn,
    }

    public readonly struct StageEvent
    {
        public readonly StageEventKind Kind;
        public readonly Vec2 Position;
        public readonly Enemy Enemy;
        public readonly PickupKind Pickup;
        public readonly int Amount;

        public StageEvent(StageEventKind kind, Vec2 position, Enemy enemy = null, PickupKind pickup = default, int amount = 0)
        {
            Kind = kind;
            Position = position;
            Enemy = enemy;
            Pickup = pickup;
            Amount = amount;
        }

        public override string ToString() => $"{Kind} at {Position}" + (Enemy != null ? $" ({Enemy.Kind} #{Enemy.Id})" : "");
    }
}
