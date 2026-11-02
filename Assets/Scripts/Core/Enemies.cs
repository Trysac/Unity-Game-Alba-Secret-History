using System;

namespace Alba.Core
{
    /// <summary>How one kind of creature fights.</summary>
    public sealed class EnemySpec
    {
        public EnemyKind Kind;
        public string Name;
        public int Health;
        public float Radius;
        public float WanderSpeed;
        public float ChaseSpeed;
        /// <summary>How close it must be to start an attack.</summary>
        public float Reach;
        public int Damage;
        /// <summary>The tell before a strike lands, so the player can back off.</summary>
        public float Windup;
        public float Recover;
        public float Aggro;
        public int Score;
        /// <summary>Drifts through trees and rocks instead of walking round them.</summary>
        public bool Flies;
        /// <summary>Only takes damage while caught in the torch beam.</summary>
        public bool NeedsLight;

        public static readonly EnemySpec[] All =
        {
            new EnemySpec
            {
                Kind = EnemyKind.Crawler, Name = "Crawler", Health = 40, Radius = 0.7f, WanderSpeed = 1.2f, ChaseSpeed = 3.7f,
                Reach = 1.7f, Damage = 12, Windup = 0.38f, Recover = 0.75f, Aggro = 15f, Score = 25,
            },
            new EnemySpec
            {
                Kind = EnemyKind.Wisp, Name = "Wisp", Health = 32, Radius = 0.5f, WanderSpeed = 1f, ChaseSpeed = 2.7f,
                Reach = 1.4f, Damage = 9, Windup = 0.3f, Recover = 1.1f, Aggro = 20f, Score = 40, Flies = true, NeedsLight = true,
            },
            new EnemySpec
            {
                Kind = EnemyKind.Brute, Name = "Am Fomhair", Health = 720, Radius = 1.4f, WanderSpeed = 1.1f, ChaseSpeed = 2.3f,
                Reach = 3.3f, Damage = 30, Windup = 0.95f, Recover = 1.5f, Aggro = 17f, Score = 600,
            },
        };

        public static EnemySpec Of(EnemyKind kind) => All[(int)kind];
    }

    public enum EnemyState
    {
        Idle,
        Chase,
        Windup,
        Recover,
        Stagger,
        Dead,
    }

    /// <summary>One creature and its simple mind: wander near home, chase what it sees, wind up, strike, recover.</summary>
    public sealed class Enemy
    {
        public readonly int Id;
        public readonly EnemySpec Spec;
        public readonly Vec2 Home;
        public Vec2 Position;
        public float Yaw;
        public int Health;
        public EnemyState State = EnemyState.Idle;
        /// <summary>Seconds spent in the current state.</summary>
        public float StateTime;
        /// <summary>True this frame while the torch beam is on it.</summary>
        public bool Lit;
        /// <summary>The brute's second phase has begun.</summary>
        public bool Enraged;
        /// <summary>Distance walked, for leg animation.</summary>
        public float Stride;
        /// <summary>Never gives up a chase (the dawn waves).</summary>
        public bool Relentless;
        Vec2 wanderTarget;
        float calmTimer;
        float wanderTimer;
        float lostTimer;

        public Enemy(int id, EnemyKind kind, Vec2 position)
        {
            Id = id;
            Spec = EnemySpec.Of(kind);
            Home = position;
            Position = position;
            wanderTarget = position;
            Health = Spec.Health;
        }

        public EnemyKind Kind => Spec.Kind;

        public bool Alive => State != EnemyState.Dead;

        public bool Hostile => State == EnemyState.Chase || State == EnemyState.Windup || State == EnemyState.Recover;

        float Speed(bool chasing)
        {
            float speed = chasing ? Spec.ChaseSpeed : Spec.WanderSpeed;
            if (Enraged) speed *= 1.35f;
            if (Spec.NeedsLight && Lit) speed *= 0.35f;
            return speed;
        }

        void Enter(EnemyState state)
        {
            State = state;
            StateTime = 0f;
        }

        /// <summary>Applies damage; returns true if this blow killed it.</summary>
        public bool TakeHit(int damage, Vec2 from)
        {
            if (!Alive) return false;
            Health -= damage;
            if (Health <= 0)
            {
                Health = 0;
                Enter(EnemyState.Dead);
                return true;
            }
            // Being shot always wakes a creature up; small ones flinch.
            lostTimer = 0f;
            if (State == EnemyState.Idle) Enter(EnemyState.Chase);
            if (Kind != EnemyKind.Brute && State != EnemyState.Windup) Enter(EnemyState.Stagger);
            return false;
        }

        /// <summary>Calls off the hunt for a few seconds and walks back home (after the player respawns nearby).</summary>
        public void SendHome()
        {
            if (!Alive) return;
            Enter(EnemyState.Idle);
            calmTimer = 4f;
            wanderTarget = Home;
            wanderTimer = 4f;
            lostTimer = 0f;
        }

        /// <summary>Kills it outright with no score (dawn burns the last creatures away).</summary>
        public void Dissolve()
        {
            Health = 0;
            Enter(EnemyState.Dead);
        }

        /// <summary>One step of thinking and moving. Strikes are reported through <paramref name="stage"/>.</summary>
        public void Tick(Stage stage, float dt)
        {
            if (!Alive) return;
            StateTime += dt;
            Player player = stage.Player;
            Vec2 toPlayer = player.Position - Position;
            float distance = toPlayer.Length;
            bool playerAlive = player.Alive && stage.Phase == StagePhase.Playing;

            switch (State)
            {
                case EnemyState.Idle:
                    calmTimer = Math.Max(0f, calmTimer - dt);
                    if (playerAlive && calmTimer <= 0f && distance < Spec.Aggro && (Spec.Flies || stage.Level.CanSee(Position, player.Position)))
                    {
                        Enter(EnemyState.Chase);
                        stage.Emit(StageEventKind.EnemyAlerted, Position, this);
                        break;
                    }
                    wanderTimer -= dt;
                    if (wanderTimer <= 0f || Vec2.Distance(Position, wanderTarget) < 0.5f)
                    {
                        wanderTarget = stage.Rng.InDisc(Home, 5f);
                        wanderTimer = stage.Rng.Range(2f, 5f);
                    }
                    Walk(stage, wanderTarget, Speed(false), dt);
                    break;

                case EnemyState.Chase:
                    if (!playerAlive)
                    {
                        Enter(EnemyState.Idle);
                        break;
                    }
                    if (distance > Spec.Aggro * 1.8f && !Relentless) lostTimer += dt;
                    else lostTimer = 0f;
                    if (lostTimer > Tuning.LeashTime)
                    {
                        lostTimer = 0f;
                        Enter(EnemyState.Idle);
                        break;
                    }
                    if (distance <= Spec.Reach)
                    {
                        Enter(EnemyState.Windup);
                        stage.Emit(StageEventKind.EnemyWindup, Position, this);
                        break;
                    }
                    Walk(stage, player.Position, Speed(true), dt);
                    break;

                case EnemyState.Windup:
                    Face(toPlayer, dt);
                    if (StateTime >= Spec.Windup)
                    {
                        // The strike lands only if the player is still close: backing off is the counter.
                        if (playerAlive && distance <= Spec.Reach + 0.4f + Tuning.PlayerRadius)
                        {
                            stage.HurtPlayer(Spec.Damage, Position, this);
                        }
                        else
                        {
                            stage.Emit(StageEventKind.EnemyMissed, Position, this);
                        }
                        Enter(EnemyState.Recover);
                    }
                    break;

                case EnemyState.Recover:
                    if (StateTime >= Spec.Recover / (Enraged ? 1.5f : 1f)) Enter(playerAlive ? EnemyState.Chase : EnemyState.Idle);
                    break;

                case EnemyState.Stagger:
                    if (StateTime >= Tuning.StaggerTime) Enter(EnemyState.Chase);
                    break;
            }
        }

        void Walk(Stage stage, Vec2 target, float speed, float dt)
        {
            Vec2 offset = target - Position;
            float distance = offset.Length;
            if (distance < 1e-3f) return;
            Vec2 step = offset / distance * Math.Min(speed * dt, distance);
            Vec2 before = Position;
            Vec2 next = Position + step;
            if (!Spec.Flies)
            {
                next = stage.Level.Resolve(next, Spec.Radius);
                // Slide round an obstacle that stops the creature dead.
                if (Vec2.Distance(next, before) < speed * dt * 0.25f)
                {
                    var side = new Vec2(step.Z, -step.X);
                    if (((Id + (int)(StateTime * 0.5f)) & 1) == 1) side = -side;
                    next = stage.Level.Resolve(before + side, Spec.Radius);
                }
            }
            Position = next;
            Stride += Vec2.Distance(before, Position);
            Face(offset, dt);
        }

        void Face(Vec2 direction, float dt)
        {
            if (direction.LengthSquared < 1e-6f) return;
            float target = direction.Yaw;
            float delta = Vec2.DeltaYaw(Yaw, target);
            Yaw = (Yaw + Math.Clamp(delta, -540f * dt, 540f * dt) + 360f) % 360f;
        }
    }
}
