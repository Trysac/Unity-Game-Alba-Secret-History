using System;
using Alba.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alba.Game
{
    /// <summary>
    /// Keyboard and mouse or a gamepad through the Input System, read once per frame into an
    /// <see cref="InputFrame"/> for the rules, plus the menu keys. The 2021 controls are kept:
    /// WASD to move, 1-4 for weapons, F for the torch, Q for a potion, left click to shoot and P to pause.
    /// </summary>
    public sealed class GameInput
    {
        const float StickStep = 0.6f;
        const float StickDead = 0.15f;

        int stickVertical;
        int stickHorizontal;
        bool padFireHeld;

        static Keyboard Keys => Keyboard.current;

        public bool UsingGamepad { get; private set; }

        /// <summary>Raw look movement this frame (degrees), for the weapon sway.</summary>
        public Vector2 LastLook { get; private set; }

        /// <summary>Reads every device. <paramref name="lookSpeed"/> is the 1-10 setting.</summary>
        public InputFrame Read(int lookSpeed, bool invertY, float dt)
        {
            float x = 0f, z = 0f, yaw = 0f, pitch = 0f;
            bool firePressed = false, fireHeld = false, sprint = false, light = false, potion = false;
            int select = -1, cycle = 0;
            float sensitivity = 0.05f + lookSpeed * 0.022f;

            Keyboard keys = Keys;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) z += 1f;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) z -= 1f;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) x += 1f;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) x -= 1f;
                sprint |= keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
                light |= keys.fKey.wasPressedThisFrame;
                potion |= keys.qKey.wasPressedThisFrame;
                if (keys.digit1Key.wasPressedThisFrame) select = 0;
                if (keys.digit2Key.wasPressedThisFrame) select = 1;
                if (keys.digit3Key.wasPressedThisFrame) select = 2;
                if (keys.digit4Key.wasPressedThisFrame) select = 3;
                if (keys.anyKey.wasPressedThisFrame) UsingGamepad = false;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch += delta.y * sensitivity * (invertY ? -1f : 1f);
                firePressed |= mouse.leftButton.wasPressedThisFrame;
                fireHeld |= mouse.leftButton.isPressed;
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) cycle = -1;
                if (scroll < -0.1f) cycle = 1;
                if (delta.sqrMagnitude > 0.01f || mouse.leftButton.wasPressedThisFrame) UsingGamepad = false;
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 move = pad.leftStick.ReadValue();
                if (move.magnitude > StickDead)
                {
                    x += move.x;
                    z += move.y;
                    UsingGamepad = true;
                }
                Vector2 look = pad.rightStick.ReadValue();
                if (look.magnitude > StickDead)
                {
                    // A gentle curve keeps small corrections precise.
                    look *= look.magnitude;
                    yaw += look.x * (60f + lookSpeed * 22f) * dt;
                    pitch += look.y * (40f + lookSpeed * 14f) * dt * (invertY ? -1f : 1f);
                    UsingGamepad = true;
                }
                bool trigger = pad.rightTrigger.ReadValue() > 0.4f;
                firePressed |= trigger && !padFireHeld;
                fireHeld |= trigger;
                padFireHeld = trigger;
                sprint |= pad.leftStickButton.isPressed || pad.leftTrigger.ReadValue() > 0.4f;
                light |= pad.buttonNorth.wasPressedThisFrame;
                potion |= pad.buttonWest.wasPressedThisFrame;
                if (pad.rightShoulder.wasPressedThisFrame) cycle = 1;
                if (pad.leftShoulder.wasPressedThisFrame) cycle = -1;
                if (pad.dpad.up.wasPressedThisFrame) select = 1;
                if (pad.dpad.right.wasPressedThisFrame) select = 2;
                if (pad.dpad.down.wasPressedThisFrame) select = 3;
                if (pad.dpad.left.wasPressedThisFrame) select = 0;
                if (trigger || pad.buttonSouth.wasPressedThisFrame) UsingGamepad = true;
            }

            LastLook = new Vector2(yaw, pitch);
            var direction = new Vec2(x, z);
            return new InputFrame(direction.ClampLength(1f), yaw, pitch, firePressed, fireHeld, sprint, light, potion, select, cycle);
        }

        // ---- Menus and system keys ----

        public bool Pause => (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.pKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.startButton.wasPressedThisFrame);

        public bool Confirm =>
            (Keys != null && (Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonSouth.wasPressedThisFrame);

        public bool Back =>
            (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.backspaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonEast.wasPressedThisFrame);

        /// <summary>A mouse click anywhere (re-captures the cursor in the browser).</summary>
        public bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        /// <summary>-1 for a step up a menu, +1 for a step down, 0 otherwise.</summary>
        public int VerticalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.upArrowKey.wasPressedThisFrame || Keys.wKey.wasPressedThisFrame) step = -1;
                if (Keys.downArrowKey.wasPressedThisFrame || Keys.sKey.wasPressedThisFrame) step = 1;
            }
            return step != 0 ? step : PadStep(true, ref stickVertical);
        }

        /// <summary>-1 for a step left through a setting, +1 for a step right, 0 otherwise.</summary>
        public int HorizontalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.leftArrowKey.wasPressedThisFrame || Keys.aKey.wasPressedThisFrame) step = -1;
                if (Keys.rightArrowKey.wasPressedThisFrame || Keys.dKey.wasPressedThisFrame) step = 1;
            }
            return step != 0 ? step : PadStep(false, ref stickHorizontal);
        }

        int PadStep(bool vertical, ref int held)
        {
            int step = 0;
            int stickSign = 0;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (vertical)
                {
                    if (pad.dpad.up.wasPressedThisFrame) step = -1;
                    if (pad.dpad.down.wasPressedThisFrame) step = 1;
                }
                else
                {
                    if (pad.dpad.left.wasPressedThisFrame) step = -1;
                    if (pad.dpad.right.wasPressedThisFrame) step = 1;
                }
                Vector2 stick = pad.leftStick.ReadValue();
                float axis = vertical ? -stick.y : stick.x;
                if (Mathf.Abs(axis) > StickStep) stickSign = axis > 0f ? 1 : -1;
            }
            if (step == 0 && stickSign != 0 && stickSign != held) step = stickSign;
            held = stickSign;
            if (step != 0) UsingGamepad = true;
            return step;
        }

        static bool AnyPad(Func<Gamepad, bool> test)
        {
            foreach (Gamepad pad in Gamepad.all) if (test(pad)) return true;
            return false;
        }
    }
}
