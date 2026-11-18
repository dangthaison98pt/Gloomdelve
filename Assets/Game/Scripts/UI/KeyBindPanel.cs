using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Settings > Keys: the controls list. One row per action (KeyBindings.Slots) with its keyboard key and its
    /// gamepad button. Choosing a row waits for a keyboard key and gives it to that action; a key already in use
    /// swaps with it. Esc cancels. Only the keyboard can be changed: the gamepad column is there to be read.
    /// Shared by the main menu and the pause menu.
    /// </summary>
    public class KeyBindPanel : MonoBehaviour
    {
        [Tooltip("One per KeyBindings.Slots entry, same order.")] public Button[] rows;
        [Tooltip("The key shown on the right of each row.")] public TMPro.TMP_Text[] keyTexts;
        public Button resetButton, back;
        public TMPro.TMP_Text hint;

        public Action onBack;

        const string DefaultHint = "Choose an action, then press the new key.   Esc cancels.\nGamepad buttons cannot be changed.";

        enum State { Idle, WaitRelease, Listen, Settle, Done }
        State state;
        int row = -1;

        public Selectable First => rows != null && rows.Length > 0 ? rows[0] : back;
        public bool Listening => state == State.WaitRelease || state == State.Listen;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int n = i;
                UIUtil.Wire(rows[i], () => Begin(n));
            }
            UIUtil.Wire(resetButton, () => { Finish(); KeyBindings.ResetAll(); Refresh(); Say("All keys are back to default."); });
            UIUtil.Wire(back, () => onBack?.Invoke());
        }

        void OnEnable()
        {
            state = State.Idle; row = -1;
            Say(DefaultHint);
            Refresh();
        }

        void OnDisable() { Finish(); }

        void Say(string s) { if (hint != null) hint.text = s; }

        void Refresh()
        {
            for (int i = 0; i < keyTexts.Length && i < KeyBindings.Slots.Length; i++)
                keyTexts[i].text = i == row && Listening ? "press a key..." : "[" + KeyBindings.Slots[i].token + "]";
        }

        /// <summary>Start waiting for a key for this row.</summary>
        public void Begin(int index)
        {
            if (index < 0 || index >= KeyBindings.Slots.Length) return;
            row = index;
            state = State.WaitRelease;          // the Enter / click that chose the row must be let go first
            KeyBindings.Busy = true;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            Say("Press the new key for  " + KeyBindings.Slots[index].title.ToUpperInvariant() + ".   Esc cancels.");
            Refresh();
        }

        void Finish()
        {
            if (state == State.Idle && !KeyBindings.Busy) return;
            state = State.Idle;
            KeyBindings.Busy = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        }

        void Update()
        {
            if (state == State.Idle) return;
            var kb = Keyboard.current;
            bool held = kb != null && kb.anyKey.isPressed;
            switch (state)
            {
                case State.WaitRelease:
                    if (!held) state = State.Listen;
                    break;
                case State.Listen:
                    var gp = Gamepad.current;
                    if (kb == null || kb.escapeKey.wasPressedThisFrame || (gp != null && gp.buttonEast.wasPressedThisFrame))
                    {
                        Say(DefaultHint);
                        state = State.Settle;
                        Refresh();
                        break;
                    }
                    foreach (var k in kb.allKeys)
                    {
                        if (k == null || !k.wasPressedThisFrame) continue;
                        var slot = KeyBindings.Slots[row];
                        if (!KeyBindings.Allowed(k.name)) { Say("That key cannot be used. Press another one.   Esc cancels."); break; }
                        var swapped = KeyBindings.Assign(slot.id, k.name);
                        Sfx.PlayUI("menu_select");
                        Say(swapped != null
                            ? slot.title + " is now " + KeyBindings.Label(k.name) + ".   " + swapped.title + " took the old key: " + KeyBindings.Label(KeyBindings.KeyOf(swapped.id)) + "."
                            : slot.title + " is now " + KeyBindings.Label(k.name) + ".");
                        state = State.Settle;
                        Refresh();
                        break;
                    }
                    break;
                case State.Settle:          // until the key is let go, so it does not also move the selection or pause
                    if (!held) state = State.Done;
                    break;
                case State.Done:
                    int r = row;
                    Finish();
                    if (r >= 0 && r < rows.Length) UIUtil.Select(rows[r]);
                    break;
            }
        }
    }
}
