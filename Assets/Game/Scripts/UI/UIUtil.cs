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
