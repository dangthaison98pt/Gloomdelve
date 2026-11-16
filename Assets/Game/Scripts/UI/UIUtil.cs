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
@"Arrow keys  -  move  (hold SHIFT to walk slowly near edges)
Z  or  SPACE  -  jump  (hold longer to jump higher)
X  -  whip;  while holding something: throw it  (UP + X throws high)
DOWN + X  -  pick up an item / put the held item down
C  -  use the equipped GEAR:  bombs (DOWN + C places, UP + C throws high)
         or rope (thrown up, hooks onto a ceiling or wall; DOWN + C at a ledge drops it)
Hold D  -  GEAR WHEEL: the game freezes, LEFT / RIGHT choose, release to equip  (tap D: swap back to the previous gear)
A  or  UP  -  enter a door, buy a shop item
UP / DOWN  -  climb ladders and ropes;  DOWN  -  crouch, crawl off a ledge to hang
Ledges are grabbed automatically when you fall past them:  UP or JUMP climbs, DOWN lets go
DOWN + JUMP  -  drop through a wooden platform;  hold UP / DOWN standing still  -  look
ESC  -  pause          F11  -  fullscreen
Gamepad: stick / d-pad move, A jump, X whip, B use gear, hold R1 gear wheel (stick left/right), Y enter/buy, LT walk";
    }
}
