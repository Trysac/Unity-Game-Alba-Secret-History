using System;
using System.Collections.Generic;

namespace Alba.Core
{
    public enum StagePhase
    {
        Playing,
        Dying,
        Escaped,
    }

    public sealed class Pickup
    {
        public int Id;
        public PickupKind Kind;
        public Vec2 Position;
        public bool Taken;
    }

    public sealed class Lantern
    {
        public int Index;
        public Vec2 Position;
        public bool Lit;
    }

    /// <summary>
    /// One chapter being played: the player, the creatures, what lies on the ground, the lanterns
    /// (checkpoints) and the portal. <see cref="Tick"/> advances it by one step and fills
    /// <see cref="Events"/> with what happened.
    /// </summary>
    public sealed class Stage
    {
        public readonly Level Level;
        public readonly Player Player = new Player();
        public readonly Rng Rng;
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<Lantern> Lanterns = new List<Lantern>();
        public readonly List<StageEvent> Events = new List<StageEvent>();

        public StagePhase Phase { get; private set; } = StagePhase.Playing;
        /// <summary>Seconds since the phase began.</summary>
        public float PhaseTime { get; private set; }
        /// <summary>Seconds of play in this chapter (not counting deaths).</summary>
        public float Elapsed { get; private set; }
        public int GemsTotal { get; }
        public int GemsFound { get; private set; }
        public int OrbsFound { get; private set; }
        public bool PageFound { get; private set; }
        public int Kills { get; private set; }
        public int KillScore { get; private set; }
        public int Deaths { get; private set; }
        public bool PortalOpen { get; private set; }
        public Vec2 RespawnPoint { get; private set; }
        public float RespawnYaw { get; private set; }

        public float SurvivalTotal => Level.Chapter.SurvivalSeconds;
        public float SurvivalLeft { get; private set; }
        public bool DawnBroke { get; private set; }
        public int Wave { get; private set; }
        float waveTimer;

        int nextId;
        bool torchWasLit;

        public Stage(Level level, int seed, Player kit = null)
        {
            Level = level;
            Rng = new Rng(seed);
            if (kit != null) Player.CopyKit(kit);
            Player.Position = level.Start;
            Player.Yaw = level.StartYaw;
            RespawnPoint = level.Start;
            RespawnYaw = level.StartYaw;
            foreach (Placement<PickupKind> p in level.Pickups) AddPickup(p.Kind, p.Position);
            foreach (Placement<EnemyKind> e in level.Enemies) Enemies.Add(new Enemy(nextId++, e.Kind, e.Position));
            for (int i = 0; i < level.Lanterns.Count; i++) Lanterns.Add(new Lantern { Index = i, Position = level.Lanterns[i] });
            GemsTotal = level.GemCount;
            SurvivalLeft = SurvivalTotal;
            waveTimer = 3f;
            torchWasLit = Player.Lit;
            UpdatePortal();
        }

        public Enemy Boss
        {
            get
            {
                foreach (Enemy e in Enemies) if (e.Kind == EnemyKind.Brute) return e;
                return null;
            }
        }

        /// <summary>Points for this chapter so far: orbs, sealstones, the journal page and creatures.</summary>
        public int Score => OrbsFound * Tuning.OrbScore + GemsFound * Tuning.GemScore + (PageFound ? Tuning.PageScore : 0) + KillScore;

        /// <summary>0 at nightfall, 1 at sunrise (the last chapter only).</summary>
        public float DawnProgress => SurvivalTotal > 0f ? 1f - SurvivalLeft / SurvivalTotal : 0f;

        public int EnemiesAlive
        {
            get
            {
                int count = 0;
                foreach (Enemy e in Enemies) if (e.Alive) count++;
                return count;
            }
        }

        public void Emit(StageEventKind kind, Vec2 position, Enemy enemy = null, PickupKind pickup = default, int amount = 0) =>
            Events.Add(new StageEvent(kind, position, enemy, pickup, amount));

        /// <summary>Advances the chapter by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt, in InputFrame input)
        {
            Events.Clear();
            PhaseTime += dt;
            if (Phase == StagePhase.Escaped) return;

            if (Phase == StagePhase.Playing)
            {
                Elapsed += dt;
                Player.HurtTimer = Math.Max(0f, Player.HurtTimer - dt);
                Player.Move(Level, input, dt);
                Player.Arsenal.Tick(dt);
                HandleTools(input);
                Player.TickTorch(dt);
                if (torchWasLit && !Player.Lit && Player.LightOn) Emit(StageEventKind.TorchEmpty, Player.Position);
                torchWasLit = Player.Lit;
            }

            foreach (Enemy e in Enemies) e.Lit = e.Alive && Player.Illuminates(Level, e.Position, e.Spec.Radius);

            if (Phase == StagePhase.Playing) Shoot(input);

            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Tick(this, dt);
            SeparateEnemies();

            if (Phase == StagePhase.Playing)
            {
                Collect();
                LightLanterns();
                if (SurvivalTotal > 0f) Survive(dt);
                UpdatePortal();
                if (PortalOpen && Vec2.Distance(Player.Position, Level.Portal) < Tuning.PortalRadius)
                {
                    Phase = StagePhase.Escaped;
                    PhaseTime = 0f;
                    Emit(StageEventKind.Escaped, Level.Portal);
                }
            }
        }

        // ---- Player tools ----

        void HandleTools(in InputFrame input)
        {
            Arsenal arsenal = Player.Arsenal;
            if (input.SelectWeapon >= 0 && input.SelectWeapon < WeaponSpec.All.Length && arsenal.Select((WeaponKind)input.SelectWeapon))
            {
                Emit(StageEventKind.WeaponSwitched, Player.Position, amount: input.SelectWeapon);
            }
            else if (input.CycleWeapon != 0)
            {
                arsenal.Cycle(input.CycleWeapon);
                Emit(StageEventKind.WeaponSwitched, Player.Position, amount: (int)arsenal.Current);
            }

            if (input.ToggleLight)
            {
                Player.LightOn = !Player.LightOn;
                Emit(StageEventKind.TorchToggled, Player.Position, amount: Player.LightOn ? 1 : 0);
            }

            if (input.UsePotion)
            {
                if (Player.Potions > 0 && Player.Health < Tuning.MaxHealth)
                {
                    Player.Potions--;
                    int before = Player.Health;
                    Player.Health = Math.Min(Tuning.MaxHealth, Player.Health + Tuning.PotionHeal);
                    Emit(StageEventKind.PotionDrunk, Player.Position, amount: Player.Health - before);
                }
                else
                {
                    Emit(StageEventKind.PotionRefused, Player.Position, amount: Player.Potions);
                }
            }
        }

        void Shoot(in InputFrame input)
        {
            Arsenal arsenal = Player.Arsenal;
            FireResult result = arsenal.TryFire(input.FirePressed, input.FireHeld);
            if (result == FireResult.Empty)
            {
                Emit(StageEventKind.DryFire, Player.Position, amount: (int)arsenal.Current);
                return;
            }
            if (result != FireResult.Fired) return;

            WeaponSpec spec = arsenal.Spec;
            if (spec.Kind == WeaponKind.Dirk)
            {
                Emit(StageEventKind.Swung, Player.Position, amount: (int)spec.Kind);
                // Index loop: a blow that enrages the giant adds creatures to the list.
                for (int i = 0, count = Enemies.Count; i < count; i++)
                {
                    Enemy e = Enemies[i];
                    if (!e.Alive) continue;
                    Vec2 offset = e.Position - Player.Position;
                    if (offset.Length > spec.Range + e.Spec.Radius) continue;
                    if (Math.Abs(Vec2.DeltaYaw(Player.Yaw, offset.Yaw)) > spec.Arc) continue;
                    Strike(e, spec.Damage);
                }
                return;
            }

            Emit(StageEventKind.Fired, Player.Position, amount: (int)spec.Kind);
            for (int pellet = 0; pellet < spec.Pellets; pellet++)
            {
                Vec2 direction = Vec2.FromYaw(Player.Yaw + Rng.Range(-spec.Spread, spec.Spread));
                float wall = Level.Raycast(Player.Position, direction, spec.Range);
                Enemy target = null;
                float nearest = wall;
                foreach (Enemy e in Enemies)
                {
                    if (!e.Alive) continue;
                    float hit = Level.RayCircle(Player.Position, direction, e.Position, e.Spec.Radius);
                    if (hit < 0f || hit >= nearest) continue;
                    if (e.Spec.NeedsLight && !e.Lit)
                    {
                        Emit(StageEventKind.EnemyImmune, e.Position, e);
                        continue;
                    }
                    nearest = hit;
                    target = e;
                }
                Emit(StageEventKind.Impact, Player.Position + direction * nearest, target, amount: wall < spec.Range && target == null ? 1 : 0);
                if (target != null) Strike(target, spec.Damage);
            }
        }

        void Strike(Enemy e, int damage)
        {
            if (!e.Alive) return;
            if (e.Spec.NeedsLight && !e.Lit)
            {
                Emit(StageEventKind.EnemyImmune, e.Position, e);
                return;
            }
            bool killed = e.TakeHit(damage, Player.Position);
            Emit(StageEventKind.EnemyHit, e.Position, e, amount: damage);
            if (killed)
            {
                Kills++;
                KillScore += e.Spec.Score;
                Emit(StageEventKind.EnemyKilled, e.Position, e, amount: e.Spec.Score);
                if (e.Kind == EnemyKind.Brute) Emit(StageEventKind.BossDefeated, e.Position, e);
                else Drop(e.Position);
                return;
            }
            if (e.Kind == EnemyKind.Brute && !e.Enraged && e.Health <= e.Spec.Health * Tuning.BossSecondPhase)
            {
                e.Enraged = true;
                Emit(StageEventKind.BossEnraged, e.Position, e);
                for (int i = 0; i < Tuning.BossSummons; i++)
                {
                    Vec2 at = Level.Resolve(e.Position + Vec2.FromYaw(e.Yaw + 120f * i + 60f) * 4f, EnemySpec.Of(EnemyKind.Crawler).Radius);
                    Spawn(EnemyKind.Crawler, at);
                }
            }
        }

        void Drop(Vec2 at)
        {
            if (!Rng.Chance(Tuning.DropChance)) return;
            PickupKind kind = Rng.Chance(0.55f) ? PickupKind.Orb : (PickupKind)Rng.Range((int)PickupKind.PistolAmmo, (int)PickupKind.RifleAmmo + 1);
            Pickup p = AddPickup(kind, at);
            Emit(StageEventKind.Dropped, at, pickup: kind, amount: p.Id);
        }

        Pickup AddPickup(PickupKind kind, Vec2 at)
        {
            var p = new Pickup { Id = Pickups.Count, Kind = kind, Position = at };
            Pickups.Add(p);
            return p;
        }

        /// <summary>Adds a creature that already knows where the player is.</summary>
        public Enemy Spawn(EnemyKind kind, Vec2 at)
        {
            var e = new Enemy(nextId++, kind, at) { State = EnemyState.Chase, Relentless = true };
            Enemies.Add(e);
            Emit(StageEventKind.EnemySpawned, at, e);
            return e;
        }

        /// <summary>Hurts the player unless a moment of grace after the last hit is still running.</summary>
        public void HurtPlayer(int damage, Vec2 from, Enemy source = null)
        {
            if (Phase != StagePhase.Playing || Player.HurtTimer > 0f || damage <= 0) return;
            Player.Health = Math.Max(0, Player.Health - damage);
            Player.HurtTimer = Tuning.HurtGrace;
            Emit(StageEventKind.PlayerHurt, from, source, amount: damage);
            if (Player.Health == 0)
            {
                Phase = StagePhase.Dying;
                PhaseTime = 0f;
                Deaths++;
                Emit(StageEventKind.PlayerDied, Player.Position, source);
            }
        }

        /// <summary>Brings the player back at the last lit lantern with full health; nearby hunters go home.</summary>
        public void Respawn()
        {
            Phase = StagePhase.Playing;
            PhaseTime = 0f;
            Player.Position = RespawnPoint;
            Player.Yaw = RespawnYaw;
            Player.Pitch = 0f;
            Player.Velocity = Vec2.Zero;
            Player.Health = Tuning.MaxHealth;
            Player.Stamina = 1f;
            Player.Winded = false;
            Player.Battery = 1f;
            Player.LightOn = true;
            Player.HurtTimer = 2f;
            foreach (Enemy e in Enemies)
            {
                if (e.Alive && Vec2.Distance(e.Position, RespawnPoint) < 14f) e.SendHome();
            }
            Emit(StageEventKind.Respawned, RespawnPoint);
        }

        // ---- World ----

        /// <summary>Keeps creatures from overlapping each other, and the player from walking through them.</summary>
        void SeparateEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy a = Enemies[i];
                if (!a.Alive) continue;
                if (Phase == StagePhase.Playing)
                {
                    Vec2 away = Player.Position - a.Position;
                    float reach = a.Spec.Radius + Tuning.PlayerRadius;
                    float gap = away.Length;
                    if (gap < reach && gap > 1e-4f) Player.Position = Level.Resolve(a.Position + away / gap * reach, Tuning.PlayerRadius);
                }
                for (int j = i + 1; j < Enemies.Count; j++)
                {
                    Enemy b = Enemies[j];
                    if (!b.Alive) continue;
                    Vec2 offset = b.Position - a.Position;
                    float minimum = a.Spec.Radius + b.Spec.Radius;
                    float distance = offset.Length;
                    if (distance >= minimum || distance < 1e-4f) continue;
                    Vec2 push = offset / distance * ((minimum - distance) * 0.5f);
                    a.Position -= push;
                    b.Position += push;
                }
            }
        }

        void Collect()
        {
            Arsenal arsenal = Player.Arsenal;
            foreach (Pickup p in Pickups)
            {
                if (p.Taken || Vec2.Distance(p.Position, Player.Position) > Tuning.PickupRadius) continue;
                int amount = 1;
                switch (p.Kind)
                {
                    case PickupKind.Orb:
                        OrbsFound++;
                        break;
                    case PickupKind.Gem:
                        GemsFound++;
                        break;
                    case PickupKind.Page:
                        PageFound = true;
                        break;
                    case PickupKind.Potion:
                        if (Player.Potions >= Tuning.MaxPotions) continue;
                        Player.Potions++;
                        break;
                    case PickupKind.PistolAmmo:
                        if (arsenal.IsFull(WeaponKind.Pistol)) continue;
                        amount = arsenal.AddAmmo(WeaponKind.Pistol, Tuning.PistolPickup);
                        break;
                    case PickupKind.ShotgunAmmo:
                        if (arsenal.IsFull(WeaponKind.Shotgun)) continue;
                        amount = arsenal.AddAmmo(WeaponKind.Shotgun, Tuning.ShotgunPickup);
                        break;
                    case PickupKind.RifleAmmo:
                        if (arsenal.IsFull(WeaponKind.Rifle)) continue;
                        amount = arsenal.AddAmmo(WeaponKind.Rifle, Tuning.RiflePickup);
                        break;
                }
                p.Taken = true;
                Emit(StageEventKind.PickedUp, p.Position, pickup: p.Kind, amount: amount);
            }
        }

        void LightLanterns()
        {
            foreach (Lantern lantern in Lanterns)
            {
                if (lantern.Lit || Vec2.Distance(lantern.Position, Player.Position) > Tuning.LanternRadius) continue;
                lantern.Lit = true;
                RespawnPoint = Level.Resolve(lantern.Position + Vec2.FromYaw(Player.Yaw) * 1.2f, Tuning.PlayerRadius);
                RespawnYaw = Player.Yaw;
                Player.Battery = 1f;
                Emit(StageEventKind.LanternLit, lantern.Position, amount: lantern.Index);
            }
        }

        void Survive(float dt)
        {
            if (DawnBroke) return;
            SurvivalLeft = Math.Max(0f, SurvivalLeft - dt);
            if (SurvivalLeft <= 0f)
            {
                DawnBroke = true;
                Emit(StageEventKind.Dawn, Level.Portal);
                foreach (Enemy e in Enemies)
                {
                    if (!e.Alive) continue;
                    e.Dissolve();
                    Emit(StageEventKind.EnemyDissolved, e.Position, e);
                }
                return;
            }
            waveTimer -= dt;
            if (waveTimer > 0f || Level.SpawnPoints.Count == 0) return;
            waveTimer = Tuning.WaveInterval;
            Wave++;
            int count = Math.Min(2 + Wave / 2, 14 - EnemiesAlive);
            Emit(StageEventKind.WaveStarted, Level.Portal, amount: Wave);
            for (int i = 0; i < count; i++)
            {
                Vec2 point = Level.SpawnPoints[Rng.Range(0, Level.SpawnPoints.Count)];
                EnemyKind kind = Wave >= 3 && Rng.Chance(0.3f) ? EnemyKind.Wisp : EnemyKind.Crawler;
                Spawn(kind, Level.Resolve(Rng.InDisc(point, 3f), EnemySpec.Of(kind).Radius));
            }
        }

        void UpdatePortal()
        {
            if (PortalOpen) return;
            Enemy boss = Boss;
            bool open = GemsFound >= GemsTotal && (boss == null || !boss.Alive) && (SurvivalTotal <= 0f || DawnBroke);
            if (!open) return;
            PortalOpen = true;
            if (Phase == StagePhase.Playing && (Elapsed > 0f || GemsTotal > 0)) Emit(StageEventKind.PortalOpened, Level.Portal);
        }
    }
}
