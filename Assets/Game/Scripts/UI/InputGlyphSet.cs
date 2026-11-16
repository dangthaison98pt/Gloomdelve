using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Gloomdelve
{
    /// <summary>
    /// Pictures of the buttons behind the input tokens used in UI texts ("[jump]", "[use]", "[gear]"...):
    /// one sprite for keyboard, one for gamepad. GlyphText swaps a token for the picture of the device in use.
    /// To add a token: add an entry here (Data/InputGlyphs.asset) and write "[name]" in any UI text.
    /// </summary>
    public class InputGlyphSet : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string token;
            public Sprite keyboard;
            public Sprite gamepad;
        }

        public Entry[] entries;

        Dictionary<string, Entry> map;

        public Sprite Find(string token, bool gamepad)
        {
            if (map == null)
            {
                map = new Dictionary<string, Entry>();
                if (entries != null) foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.token)) map[e.token] = e;
            }
            if (!map.TryGetValue(token, out var en)) return null;
            return gamepad && en.gamepad != null ? en.gamepad : en.keyboard;
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
