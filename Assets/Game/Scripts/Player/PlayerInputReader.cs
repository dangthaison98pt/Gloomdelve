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
        public bool attackPressed, usePressed, interactPressed, upPressed;
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
        InputAction move, jump, attack, use, swap, interact, walk, pause, inventory;
        bool jumpL, attackL, useL, interactL, upL, prevUp;

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
            map.Enable();
        }

        void OnDisable() { map?.Disable(); }

        Vector2 MoveValue => move != null ? move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Gear-wheel button (R1 / D) currently held. Works while the game is frozen.</summary>
        public bool SwapHeld => swap != null && swap.IsPressed();
        /// <summary>-1 / 0 / 1 horizontal direction (stick or arrows), readable while frozen.</summary>
        public int MoveX { get { var v = MoveValue; return v.x > 0.5f ? 1 : (v.x < -0.5f ? -1 : 0); } }

        void Update()
        {
            if (map == null || Time.timeScale == 0f) return;
            if (jump.WasPressedThisFrame()) jumpL = true;
            if (attack.WasPressedThisFrame()) attackL = true;
            if (use.WasPressedThisFrame()) useL = true;
            if (interact.WasPressedThisFrame()) interactL = true;
            bool up = MoveValue.y > 0.5f;
            if (up && !prevUp) upL = true;
            prevUp = up;
        }

        public void ClearLatches()
        {
            jumpL = attackL = useL = interactL = upL = false;
        }

        public bool PausePressed => pause != null && pause.WasPressedThisFrame();
        /// <summary>Equipment screen button (Tab / Select). Works while the game is frozen.</summary>
        public bool InventoryPressed => inventory != null && inventory.WasPressedThisFrame();
        /// <summary>Jump or Interact pressed this frame (menu confirm), readable while frozen.</summary>
        public bool ConfirmPressed => (jump != null && jump.WasPressedThisFrame()) || (interact != null && interact.WasPressedThisFrame());
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
            f.interactPressed = interactL;
            f.upPressed = upL;
            ClearLatches();
            return f;
        }
    }
}
