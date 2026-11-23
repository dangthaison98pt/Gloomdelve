using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gloomdelve
{
    public static class UIUtil
    {
        public static void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        /// <summary>The same words with their first letter a capital (descriptions are written small in the data: "points to the exit").</summary>
        public static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            // (only a description that begins with a letter: "+3 bombs", a button picture or a tag are left as they are)
            int at = 0;
            while (at < s.Length && char.IsWhiteSpace(s[at])) at++;
            if (at < s.Length && char.IsLower(s[at])) return s.Substring(0, at) + char.ToUpperInvariant(s[at]) + s.Substring(at + 1);
            return s;
        }

        public static string FormatTime(float t)
        {
            if (t < 0f) return "--:--";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:00}:{s:00.0}";
        }

        public static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { Sfx.PlayUI("menu_select"); a(); });
        }
    }
}
