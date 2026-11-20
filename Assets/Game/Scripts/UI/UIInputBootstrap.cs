using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Makes sure the UI input module has actions (keyboard/gamepad navigation, mouse), and keeps the menu focus:
    /// a mouse click on empty space clears the EventSystem's selection, which would leave the gamepad / keyboard with
    /// nothing to move from. When that happens the last entry in focus is chosen again, as long as it is still on
    /// screen and usable (a closed panel's buttons are not brought back).
    /// </summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class UIInputBootstrap : MonoBehaviour
    {
        GameObject last;

        void Awake()
        {
            var m = GetComponent<InputSystemUIInputModule>();
            if (m.actionsAsset == null || m.move == null || m.move.action == null) m.AssignDefaultActions();
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var cur = es.currentSelectedGameObject;
            if (Usable(cur)) { last = cur; return; }
            if (Usable(last)) es.SetSelectedGameObject(last);
            else last = null;
        }

        static bool Usable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            var s = go.GetComponent<Selectable>();
            return s == null || s.IsInteractable();
        }
    }
}
