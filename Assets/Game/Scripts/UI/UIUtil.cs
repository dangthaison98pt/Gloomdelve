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

    public static class ControlsText
    {
        public const string Body =
@"[move]  -  move  (hold [walk] to walk slowly near edges)
[jump]  -  jump  (hold longer to jump higher)
[whip]  -  blade;  while holding something: throw it  ([up] + [whip] throws high)
[down] + [whip]  -  pick up an item / put the held item down
[use]  -  use the equipped GEAR:  bombs ([down] + [use] places, [up] + [use] throws high)
         or rope (thrown up, hooks onto a ceiling or wall; [down] + [use] at a ledge drops it)
Hold [gear]  -  GEAR WHEEL: the game freezes, [left] [right] choose, release to equip  (tap [gear]: back to the previous gear)
[enter]  or  [up]  -  enter a door, buy a shop item
[up] / [down]  -  climb ladders and ropes;  [down]  -  crouch, crawl off a ledge to hang
Ledges are grabbed automatically when you fall past them:  [up] or [jump] climbs, [down] lets go
[down] + [jump]  -  drop through a wooden platform;  hold [up] / [down] standing still  -  look
[inventory]  -  equipment: wear or take off what you own (boots, coat, charms...)
[pause]  -  pause          F11  -  fullscreen
The buttons shown follow the device you used last: keyboard or gamepad.";
    }
}
