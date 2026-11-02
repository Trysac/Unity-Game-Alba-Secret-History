using System;

namespace Alba.Core
{
    /// <summary>What the player asks for in one frame, already read from the keyboard, mouse or gamepad.</summary>
    public readonly struct InputFrame
    {
        /// <summary>Local movement: X strafes right, Z walks forward; length up to 1.</summary>
        public readonly Vec2 Move;
        public readonly float YawDelta;
        public readonly float PitchDelta;
        public readonly bool FirePressed;
        public readonly bool FireHeld;
        public readonly bool Sprint;
        public readonly bool ToggleLight;
        public readonly bool UsePotion;
        /// <summary>A weapon slot chosen directly (0-3), or -1.</summary>
        public readonly int SelectWeapon;
        /// <summary>-1 or +1 to cycle weapons, 0 otherwise.</summary>
        public readonly int CycleWeapon;

        public InputFrame(Vec2 move, float yawDelta = 0f, float pitchDelta = 0f, bool firePressed = false, bool fireHeld = false,
            bool sprint = false, bool toggleLight = false, bool usePotion = false, int selectWeapon = -1, int cycleWeapon = 0)
        {
            Move = move;
            YawDelta = yawDelta;
            PitchDelta = pitchDelta;
            FirePressed = firePressed;
            FireHeld = fireHeld;
            Sprint = sprint;
            ToggleLight = toggleLight;
            UsePotion = usePotion;
            SelectWeapon = selectWeapon;
            CycleWeapon = cycleWeapon;
        }

        public static readonly InputFrame None = new InputFrame(Vec2.Zero);
    }

    /// <summary>Eilidh: where she stands and looks, her health, flask of potions, torch and weapons.</summary>
    public sealed class Player
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public float Yaw;
        public float Pitch;
        public int Health = Tuning.MaxHealth;
        public int Potions = 1;
        public float Stamina = 1f;
        public bool Winded;
        public bool LightOn = true;
        public float Battery = 1f;
        public float HurtTimer;
        /// <summary>Distance walked, for footsteps and view bob.</summary>
        public float Stride;
        public readonly Arsenal Arsenal = new Arsenal();

        public bool Alive => Health > 0;

        /// <summary>True while the torch is on and still has charge.</summary>
        public bool Lit => LightOn && Battery > 0f;

        public Vec2 Forward => Vec2.FromYaw(Yaw);

        /// <summary>Walks, turns and spends or recovers stamina for one step.</summary>
        public void Move(Level level, in InputFrame input, float dt)
        {
            Yaw = (Yaw + input.YawDelta) % 360f;
            if (Yaw < 0f) Yaw += 360f;
            Pitch = Math.Clamp(Pitch + input.PitchDelta, -80f, 80f);

            Vec2 wish = input.Move.ClampLength(1f);
            bool moving = wish.LengthSquared > 0.01f;
            bool sprinting = input.Sprint && moving && wish.Z > 0.2f && !Winded;
            if (sprinting)
            {
                Stamina = Math.Max(0f, Stamina - Tuning.StaminaDrain * dt);
                if (Stamina <= 0f) Winded = true;
            }
            else
            {
                Stamina = Math.Min(1f, Stamina + Tuning.StaminaRegen * dt);
                if (Winded && Stamina >= Tuning.StaminaResume) Winded = false;
            }

            float speed = sprinting ? Tuning.SprintSpeed : Tuning.WalkSpeed;
            Vec2 target = wish.RotatedByYaw(Yaw) * speed;
            Vec2 change = (target - Velocity).ClampLength(Tuning.Acceleration * dt);
            Velocity += change;
            Vec2 before = Position;
            Position = level.Resolve(Position + Velocity * dt, Tuning.PlayerRadius);
            Vec2 moved = Position - before;
            // Hitting a wall or tree kills the speed into it.
            if (dt > 0f) Velocity = moved / dt;
            Stride += moved.Length;
        }

        /// <summary>Drains the torch while it is on and recharges it while it is off.</summary>
        public void TickTorch(float dt)
        {
            if (LightOn)
            {
                Battery = Math.Max(0f, Battery - Tuning.BatteryDrain * dt);
            }
            else
            {
                Battery = Math.Min(1f, Battery + Tuning.BatteryRecharge * dt);
            }
        }

        /// <summary>True when a point is inside the torch beam (on, charged, in range, inside the cone and not behind something).</summary>
        public bool Illuminates(Level level, Vec2 point, float radius)
        {
            if (!Lit) return false;
            Vec2 offset = point - Position;
            float distance = offset.Length;
            if (distance > Tuning.FlashlightRange + radius) return false;
            if (distance > radius)
            {
                float angle = Math.Abs(Vec2.DeltaYaw(Yaw, offset.Yaw));
                float allowance = (float)(Math.Atan2(radius, distance) * 180.0 / Math.PI);
                if (angle > Tuning.FlashlightHalfAngle + allowance) return false;
            }
            return level.CanSee(Position, point);
        }

        /// <summary>Copies everything carried (health, flask, ammunition) from another player.</summary>
        public void CopyKit(Player other)
        {
            Health = other.Health;
            Potions = other.Potions;
            Battery = other.Battery;
            LightOn = other.LightOn;
            Arsenal.CopyFrom(other.Arsenal);
        }
    }
}
