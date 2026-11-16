using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Gloomdelve
{
    /// <summary>
    /// The buttons behind the input tokens used in UI texts ("[jump]", "[use]", "[gear]"...). Each token has a
    /// keyboard and a gamepad look: a blank button shape (sprite) plus the label written on it with the normal
    /// UI font, so the lettering stays sharp at any size. GlyphText swaps a token for the button of the device in use.
    /// To add a token: add an entry here (Data/InputGlyphs.asset) and write "[name]" in any UI text.
    /// </summary>
    public class InputGlyphSet : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string token;
            [Tooltip("Button shape. A sprite with a 9-slice border stretches to fit its label.")] public Sprite keyboard;
            public string keyboardLabel;
            public Color keyboardInk = new Color(0.17f, 0.14f, 0.19f);
            public Sprite gamepad;
            public string gamepadLabel;
            public Color gamepadInk = Color.white;
        }

        public Entry[] entries;

        Dictionary<string, Entry> map;

        public bool Find(string token, bool gamepad, out Sprite shape, out string label, out Color ink)
        {
            shape = null; label = ""; ink = Color.white;
            if (map == null)
            {
                map = new Dictionary<string, Entry>();
                if (entries != null) foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.token)) map[e.token] = e;
            }
            if (!map.TryGetValue(token, out var en)) return false;
            bool pad = gamepad && en.gamepad != null;
            shape = pad ? en.gamepad : en.keyboard;
            label = (pad ? en.gamepadLabel : en.keyboardLabel) ?? "";
            ink = pad ? en.gamepadInk : en.keyboardInk;
            return shape != null;
        }
    }

    /// <summary>Remembers which device the player touched last, so prompts show the right buttons.</summary>
    public static class InputDeviceTracker
    {
        public static bool UsingGamepad { get; private set; }
        static int frame = -1;

        /// <summary>Back to keyboard pictures (used by tests after driving a virtual gamepad).</summary>
        public static void ResetToKeyboard() { UsingGamepad = false; }

        public static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { UsingGamepad = false; return; }
            var gp = Gamepad.current;
            if (gp == null || !gp.wasUpdatedThisFrame) return;
            if (gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.dpad.ReadValue().sqrMagnitude > 0.25f) { UsingGamepad = true; return; }
            foreach (var c in gp.allControls)
                if (c is ButtonControl b && !b.synthetic && b.isPressed) { UsingGamepad = true; return; }
        }
    }
}
