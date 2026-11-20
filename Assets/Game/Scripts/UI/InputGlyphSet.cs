using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

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
            [Tooltip("Xbox-style pads and any pad that is not recognised.")] public Sprite gamepad;
            public string gamepadLabel;
            public Color gamepadInk = Color.white;
            [Tooltip("PlayStation pads (DualShock / DualSense). Empty = use the gamepad look.")] public Sprite playstation;
            public string playstationLabel;
            public Color playstationInk = Color.white;
            [Tooltip("Nintendo Switch pads (Pro Controller). Empty = use the gamepad look.")] public Sprite nintendo;
            public string nintendoLabel;
            public Color nintendoInk = Color.white;
        }

        public Entry[] entries;

        Dictionary<string, Entry> map;

        /// <param name="device">InputDeviceTracker.Kind: 0 keyboard, 1 gamepad (Xbox style), 2 PlayStation, 3 Nintendo Switch.</param>
        public bool Find(string token, int device, out Sprite shape, out string label, out Color ink)
        {
            shape = null; label = ""; ink = Color.white;
            if (map == null)
            {
                map = new Dictionary<string, Entry>();
                if (entries != null) foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.token)) map[e.token] = e;
            }
            if (!map.TryGetValue(token, out var en)) return false;
            if (device == 3 && en.nintendo != null) { shape = en.nintendo; label = en.nintendoLabel ?? ""; ink = en.nintendoInk; }
            else if (device == 2 && en.playstation != null) { shape = en.playstation; label = en.playstationLabel ?? ""; ink = en.playstationInk; }
            else if (device >= 1 && en.gamepad != null) { shape = en.gamepad; label = en.gamepadLabel ?? ""; ink = en.gamepadInk; }
            else
            {
                shape = en.keyboard; label = en.keyboardLabel ?? ""; ink = en.keyboardInk;
                if (KeyBindings.GlyphLabel(token, out var chosen)) label = chosen;      // the key the player chose for it
            }
            return shape != null;
        }
    }

    /// <summary>
    /// Remembers which device the player touched last, so prompts show the right buttons.
    /// Listens to every button on every device (not just "the current gamepad": with several controllers plugged
    /// in, or a pad that shows up twice, the current one may be an idle device) and also watches all sticks.
    /// </summary>
    public static class InputDeviceTracker
    {
        public static bool UsingGamepad { get; private set; }
        /// <summary>The pad in use is a PlayStation one (DualShock / DualSense): show cross, circle, square, triangle.</summary>
        public static bool PlayStation { get; private set; }
        /// <summary>The pad in use is a Nintendo Switch one: letters as printed on it (A on the right, B at the bottom).</summary>
        public static bool Nintendo { get; private set; }
        /// <summary>0 keyboard, 1 gamepad (Xbox style), 2 PlayStation, 3 Nintendo Switch.</summary>
        public static int Kind => !UsingGamepad ? 0 : PlayStation ? 2 : Nintendo ? 3 : 1;

        /// <summary>
        /// The face button that says "yes" in a menu on this pad, and the one that says "back". On a Nintendo pad
        /// they are the other way round from the rest: A, the right-hand button, confirms and B, the bottom one,
        /// goes back - as on the console itself. Everywhere else the bottom button confirms and the right one goes back.
        /// </summary>
        public static UnityEngine.InputSystem.Controls.ButtonControl ConfirmButton(Gamepad g) => IsNintendo(g) ? g.buttonEast : g.buttonSouth;
        public static UnityEngine.InputSystem.Controls.ButtonControl BackButton(Gamepad g) => IsNintendo(g) ? g.buttonSouth : g.buttonEast;

        /// <summary>
        /// The same two buttons on a pad the Input System only knows as a joystick with numbered buttons: button 2 is
        /// the bottom face button and button 3 the right-hand one on most of them (see the joystick bindings in
        /// ProjectBuilder.InputActions). A stick with a trigger and nothing else confirms with its trigger.
        /// </summary>
        public static UnityEngine.InputSystem.Controls.ButtonControl ConfirmButton(Joystick j)
            => j.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("button2") ?? j.trigger;
        public static UnityEngine.InputSystem.Controls.ButtonControl BackButton(Joystick j)
            => j.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("button3");

        /// <summary>Any pad's confirm button went down this frame (whatever kind of pad it is).</summary>
        public static bool PadConfirmPressed
        {
            get
            {
                foreach (var g in Gamepad.all) if (ConfirmButton(g).wasPressedThisFrame) return true;
                foreach (var j in Joystick.all) { var b = ConfirmButton(j); if (b != null && b.wasPressedThisFrame) return true; }
                return false;
            }
        }
        /// <summary>Any pad's back button went down this frame (whatever kind of pad it is).</summary>
        public static bool PadBackPressed
        {
            get
            {
                foreach (var g in Gamepad.all) if (BackButton(g).wasPressedThisFrame) return true;
                foreach (var j in Joystick.all) { var b = BackButton(j); if (b != null && b.wasPressedThisFrame) return true; }
                return false;
            }
        }
        static int frame = -1;
        static System.IDisposable hook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { UsingGamepad = false; PlayStation = false; Nintendo = false; frame = -1; hook = null; }

        static string Describe(InputDevice d)
        {
            var desc = d.description;
            return ((desc.manufacturer ?? "") + " " + (desc.product ?? "") + " " + (desc.interfaceName ?? "")).ToLowerInvariant();
        }

        static bool IsNintendo(InputDevice d)
        {
            if (d is UnityEngine.InputSystem.Switch.SwitchProControllerHID) return true;
            string s = Describe(d);
            return s.Contains("nintendo") || s.Contains("pro controller") || s.Contains("joy-con") || s.Contains("switch");
        }

        static bool IsPlayStation(InputDevice d)
        {
            if (d is UnityEngine.InputSystem.DualShock.DualShockGamepad) return true;   // DualShock 3/4 and DualSense
            var desc = d.description;
            string s = ((desc.manufacturer ?? "") + " " + (desc.product ?? "") + " " + (desc.interfaceName ?? "")).ToLowerInvariant();
            return s.Contains("sony") || s.Contains("dualsense") || s.Contains("dualshock") || s.Contains("playstation");
        }

        static void UsePad(InputDevice d)
        {
            UsingGamepad = true;
            PlayStation = IsPlayStation(d);
            Nintendo = !PlayStation && IsNintendo(d);
        }

        /// <summary>Back to keyboard pictures (used by tests after driving a virtual gamepad).</summary>
        public static void ResetToKeyboard() { UsingGamepad = false; PlayStation = false; Nintendo = false; }

        static void OnButton(InputControl control)
        {
            var d = control.device;
            if (d is Gamepad || d is Joystick) UsePad(d);
            else if (d is Keyboard) UsingGamepad = false;
        }

        public static void Poll()
        {
            if (hook == null) hook = InputSystem.onAnyButtonPress.Call(OnButton);
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { UsingGamepad = false; return; }
            // sticks and d-pads are not "button presses": check them on every pad
            foreach (var gp in Gamepad.all)
                if (gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.dpad.ReadValue().sqrMagnitude > 0.25f || gp.rightStick.ReadValue().sqrMagnitude > 0.25f)
                { UsePad(gp); return; }
            foreach (var js in Joystick.all)
                if (js.stick != null && js.stick.ReadValue().sqrMagnitude > 0.25f) { UsePad(js); return; }
        }
    }
}
