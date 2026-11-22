using UnityEngine;
using UnityEngine.InputSystem;

namespace Gloomdelve
{
    /// <summary>One simulation step worth of player input.</summary>
    public struct FrameInput
    {
        public int x;
        public bool up, down, walk;
        public bool jumpPressed, jumpHeld;
        public bool attackPressed, usePressed, interactPressed, upPressed, dashPressed;
        /// <summary>The use button is down (gear that is drawn and let go: the slingshot).</summary>
        public bool useHeld;
    }

    public interface IFrameInputSource
    {
        FrameInput Next();
    }

    /// <summary>
    /// Reads the "Player" action map of the New Input System. Button presses are latched in Update and consumed
    /// by the next fixed simulation step, so no press is ever lost between frames.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public InputActionAsset actions;

        InputActionMap map;
        InputAction move, jump, attack, use, swap, interact, walk, pause, inventory, dash;
        bool jumpL, attackL, useL, interactL, upL, prevUp, dashL;

        public InputAction PauseAction => pause;

        void OnEnable()
        {
            if (actions == null) { Debug.LogError("[Input] No InputActionAsset assigned"); return; }
            map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            jump = map.FindAction("Jump", true);
            attack = map.FindAction("Attack", true);
            use = map.FindAction("Use", true);
            swap = map.FindAction("Swap", true);
            interact = map.FindAction("Interact", true);
            walk = map.FindAction("Walk", true);
            pause = map.FindAction("Pause", true);
            inventory = map.FindAction("Inventory", false);
            dash = map.FindAction("Dash", false);
            KeyBindings.Apply(actions);             // the keyboard keys the player chose
            KeyBindings.Changed += ApplyKeys;
            map.Enable();
        }

        void OnDisable() { KeyBindings.Changed -= ApplyKeys; map?.Disable(); }

        void ApplyKeys() { KeyBindings.Apply(actions); }

        Vector2 MoveValue => move != null ? move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Gear-wheel button (R1 / D) currently held. Works while the game is frozen.</summary>
        public bool SwapHeld => swap != null && swap.IsPressed();
        /// <summary>-1 / 0 / 1 horizontal direction (stick or arrows), readable while frozen.</summary>
        public int MoveX { get { var v = MoveValue; return v.x > 0.5f ? 1 : (v.x < -0.5f ? -1 : 0); } }

        void Update()
        {
            if (map == null || Time.timeScale == 0f) return;
            // a press that has just been used for something else - putting a card away, closing a screen - is not
            // also a press for the player: whoever cleared the latches in this frame meant this frame's presses too
            // (this Update may run after theirs, and the button "was pressed this frame" until the frame is over)
            if (Time.frameCount == mutedFrame) return;
            if (jump.WasPressedThisFrame()) jumpL = true;
            if (attack.WasPressedThisFrame()) attackL = true;
            if (use.WasPressedThisFrame()) useL = true;
            if (interact.WasPressedThisFrame()) interactL = true;
            if (dash != null && dash.WasPressedThisFrame()) dashL = true;
            bool up = MoveValue.y > 0.5f;
            if (up && !prevUp) upL = true;
            prevUp = up;
        }

        int mutedFrame = -1;

        /// <summary>Forgets every press not yet acted on - and those of the frame it is called in, wherever in that frame they are read.</summary>
        public void ClearLatches()
        {
            jumpL = attackL = useL = interactL = upL = dashL = false;
            mutedFrame = Time.frameCount;
        }

        // KeyBindings.Busy: the change-keys screen is waiting for a key, so that key press must not pause or confirm anything
        public bool PausePressed => !KeyBindings.Busy && pause != null && pause.WasPressedThisFrame();
        /// <summary>Tab to the left in a screen with tabs (the bag): the Walk button (L1 / S). Works while the game is frozen.</summary>
        public bool TabLeftPressed => !KeyBindings.Busy && walk != null && walk.WasPressedThisFrame();
        /// <summary>Tab to the right in a screen with tabs (the bag): the gear button (R1 / Tab). Works while the game is frozen.</summary>
        public bool TabRightPressed => !KeyBindings.Busy && swap != null && swap.WasPressedThisFrame();
        /// <summary>The "back" button of a gamepad (the right-hand face button): leaves a screen such as the bag.</summary>
        public bool BackPressed => !KeyBindings.Busy && InputDeviceTracker.PadBackPressed;      // (the right-hand face button; on a Nintendo pad the bottom one, B)
        /// <summary>Equipment screen button (I / Start). Works while the game is frozen.</summary>
        public bool InventoryPressed => !KeyBindings.Busy && inventory != null && inventory.WasPressedThisFrame();
        /// <summary>Jump or Interact pressed this frame (menu confirm), readable while frozen.</summary>
        public bool ConfirmPressed => !KeyBindings.Busy && ((jump != null && jump.WasPressedThisFrame()) || (interact != null && interact.WasPressedThisFrame()));
        /// <summary>-1 / 0 / 1 vertical direction (stick or arrows), readable while frozen.</summary>
        public int MoveY { get { var v = MoveValue; return v.y > 0.5f ? 1 : (v.y < -0.5f ? -1 : 0); } }

        public FrameInput Consume()
        {
            var f = new FrameInput();
            if (map == null) return f;
            var v = MoveValue;
            f.x = v.x > 0.5f ? 1 : (v.x < -0.5f ? -1 : 0);
            f.up = v.y > 0.5f;
            f.down = v.y < -0.5f;
            f.walk = walk.IsPressed();
            f.jumpHeld = jump.IsPressed();
            f.jumpPressed = jumpL;
            f.attackPressed = attackL;
            f.usePressed = useL;
            f.useHeld = use != null && use.IsPressed();
            f.interactPressed = interactL;
            f.upPressed = upL;
            f.dashPressed = dashL;
            jumpL = attackL = useL = interactL = upL = dashL = false;       // (taken: not ClearLatches, which also shuts the frame)
            return f;
        }
    }
}
