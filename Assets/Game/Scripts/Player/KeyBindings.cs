using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Gloomdelve
{
    /// <summary>
    /// The keyboard keys the player chose for each action (Settings > Keys). Only the keyboard can be
    /// changed: gamepad buttons stay as they are.
    /// A key is stored by its Input System name ("z", "leftShift", "upArrow"). Only keys that differ from the
    /// default are saved (settings.json). PlayerInputReader applies them to the action asset as binding overrides,
    /// and the button pictures in UI texts (GlyphText) follow them.
    /// To make another action changeable: add a Slot here whose 'defaultKey' is its keyboard binding in
    /// ProjectBuilder.InputActions, and a row appears on the screen by itself after a rebuild.
    /// </summary>
    public static class KeyBindings
    {
        public class Slot
        {
            public string id, title, action, defaultKey, token;
            public Slot(string id, string title, string action, string defaultKey, string token)
            { this.id = id; this.title = title; this.action = action; this.defaultKey = defaultKey; this.token = token; }
        }

        /// <summary>id, name on the screen, input action, default key, token used in UI texts ("[jump]").</summary>
        public static readonly Slot[] Slots =
        {
            new Slot("up", "Up / climb", "Move", "upArrow", "up"),
            new Slot("down", "Down / crouch", "Move", "downArrow", "down"),
            new Slot("left", "Left", "Move", "leftArrow", "left"),
            new Slot("right", "Right", "Move", "rightArrow", "right"),
            new Slot("jump", "Jump", "Jump", "space", "jump"),
            new Slot("attack", "Blade", "Attack", "q", "whip"),
            new Slot("dash", "Dash", "Dash", "leftShift", "dash"),
            new Slot("use", "Use gear", "Use", "w", "use"),
            new Slot("swap", "Change gear", "Swap", "tab", "gear"),
            new Slot("interact", "Enter / buy", "Interact", "e", "enter"),
            new Slot("walk", "Walk slowly", "Walk", "s", "walk"),
            new Slot("inventory", "Equipment", "Inventory", "i", "inventory"),
            new Slot("map", "Map (hold)", "Map", "m", "map"),
        };

        /// <summary>Goes up on every change: texts with button pictures redraw themselves.</summary>
        public static int Version { get; private set; }
        /// <summary>The change-keys screen is waiting for a key: nothing else may react to the keyboard.</summary>
        public static bool Busy;
        public static event Action Changed;

        const string Prefix = "<Keyboard>/";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Busy = false; Changed = null; }

        static List<KeyBind> Saved
        {
            get
            {
                SaveSystem.EnsureLoaded();
                var s = SaveSystem.Settings;
                if (s.keys == null) s.keys = new List<KeyBind>();
                return s.keys;
            }
        }

        public static Slot Find(string id)
        {
            foreach (var s in Slots) if (s.id == id) return s;
            return null;
        }

        public static string KeyOf(string id)
        {
            foreach (var k in Saved) if (k != null && k.id == id && !string.IsNullOrEmpty(k.key)) return k.key;
            var s = Find(id);
            return s != null ? s.defaultKey : "";
        }

        public static bool AllDefault => Saved.Count == 0;

        static void Set(Slot s, string key)
        {
            Saved.RemoveAll(k => k == null || k.id == s.id);
            if (key != s.defaultKey) Saved.Add(new KeyBind { id = s.id, key = key });
        }

        /// <summary>Keys that keep a fixed job (Esc: pause and cancel, F11 / F4: fullscreen) or belong to the system.</summary>
        public static bool Allowed(string key)
        {
            switch (key)
            {
                case "escape": case "f11": case "f4": case "leftMeta": case "rightMeta": case "contextMenu":
                case "printScreen": case "capsLock": case "numLock": case "scrollLock": case "pause":
                    return false;
            }
            return !string.IsNullOrEmpty(key) && !key.StartsWith("OEM") && key != "IMESelected";
        }

        /// <summary>
        /// Gives the action a new key. If another action already has that key, the two swap keys, so no action is
        /// ever left without one. Returns the action that was swapped (null if none).
        /// </summary>
        public static Slot Assign(string id, string key)
        {
            var s = Find(id);
            if (s == null || !Allowed(key)) return null;
            string old = KeyOf(id);
            if (old == key) return null;
            Slot other = null;
            foreach (var o in Slots) if (o != s && KeyOf(o.id) == key) other = o;
            Set(s, key);
            if (other != null) Set(other, old);
            Commit();
            return other;
        }

        public static void ResetAll()
        {
            Saved.Clear();
            Commit();
        }

        /// <summary>Puts back a saved set (tests).</summary>
        public static void Restore(List<KeyBind> keys)
        {
            Saved.Clear();
            if (keys != null) Saved.AddRange(keys);
            Commit();
        }

        static void Commit()
        {
            SaveSystem.SaveSettings();
            Version++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Writes the chosen keys onto the action asset. Keyboard bindings that are not a Slot (P for pause)
        /// keep working as extras, unless the player gave that key to an action: then the extra is off.
        /// </summary>
        public static void Apply(InputActionAsset asset)
        {
            if (asset == null) return;
            var map = asset.FindActionMap("Player", false);
            if (map == null) return;
            var used = new HashSet<string>();
            foreach (var s in Slots) used.Add(KeyOf(s.id));
            foreach (var action in map.actions)
            {
                var bs = action.bindings;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b.isComposite || string.IsNullOrEmpty(b.path) || !b.path.StartsWith(Prefix)) continue;
                    string key = b.path.Substring(Prefix.Length);
                    Slot slot = null;
                    foreach (var s in Slots) if (s.action == action.name && s.defaultKey == key) slot = s;
                    if (slot != null)
                    {
                        string cur = KeyOf(slot.id);
                        if (cur == key) action.RemoveBindingOverride(i);
                        else action.ApplyBindingOverride(i, Prefix + cur);
                    }
                    else if (used.Contains(key)) action.ApplyBindingOverride(i, "");     // an empty path switches the binding off
                    else action.RemoveBindingOverride(i);
                }
            }
        }

        /// <summary>The key of this action went down this frame (screens that read the keyboard directly).</summary>
        public static bool Pressed(string id)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var k = kb.TryGetChildControl(KeyOf(id)) as KeyControl;
            return k != null && k.wasPressedThisFrame;
        }

        /// <summary>What is written on the key picture for an input token ("[jump]"). False: not a key the player can change.</summary>
        public static bool GlyphLabel(string token, out string label)
        {
            label = "";
            if (token == "move")
            {
                if (KeyOf("left") == "leftArrow" && KeyOf("up") == "upArrow" && KeyOf("down") == "downArrow" && KeyOf("right") == "rightArrow") return false;
                label = Label(KeyOf("left")) + " " + Label(KeyOf("up")) + " " + Label(KeyOf("down")) + " " + Label(KeyOf("right"));
                return true;
            }
            foreach (var s in Slots)
                if (s.token == token) { label = Label(KeyOf(s.id)); return true; }
            return false;
        }

        /// <summary>Short name of a key for its picture: "z" -> "Z", "leftShift" -> "Shift", "numpad4" -> "Num 4".</summary>
        public static string Label(string key)
        {
            if (string.IsNullOrEmpty(key)) return "?";
            switch (key)
            {
                case "upArrow": return "▲";
                case "downArrow": return "▼";
                case "leftArrow": return "◄";
                case "rightArrow": return "►";
                case "leftShift": return "Shift";
                case "rightShift": return "R Shift";
                case "leftCtrl": return "Ctrl";
                case "rightCtrl": return "R Ctrl";
                case "leftAlt": return "Alt";
                case "rightAlt": return "R Alt";
                case "space": return "Space";
                case "enter": return "Enter";
                case "tab": return "Tab";
                case "backspace": return "Bksp";
                case "delete": return "Del";
                case "insert": return "Ins";
                case "pageUp": return "PgUp";
                case "pageDown": return "PgDn";
                case "home": return "Home";
                case "end": return "End";
                case "minus": return "-";
                case "equals": return "=";
                case "comma": return ",";
                case "period": return ".";
                case "slash": return "/";
                case "backslash": return "\\";
                case "semicolon": return ";";
                case "quote": return "'";
                case "backquote": return "`";
                case "leftBracket": return "[";
                case "rightBracket": return "]";
                case "numpadEnter": return "Num Enter";
                case "numpadPlus": return "Num +";
                case "numpadMinus": return "Num -";
                case "numpadMultiply": return "Num *";
                case "numpadDivide": return "Num /";
                case "numpadPeriod": return "Num .";
                case "numpadEquals": return "Num =";
            }
            if (key.StartsWith("numpad")) return "Num " + key.Substring(6);
            if (key.StartsWith("digit")) return key.Substring(5);
            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }
    }
}
