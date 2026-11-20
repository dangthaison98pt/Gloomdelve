using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Makes sure the UI input module has actions (keyboard/gamepad navigation, mouse), and keeps the menu focus:
    /// a mouse click on empty space clears the EventSystem's selection, which would leave the gamepad / keyboard with
    /// nothing to move from. When that happens the last entry in focus is chosen again, as long as it is still on
    /// screen and usable (a closed panel's buttons are not brought back).
    /// It also decides which button of a pad says "yes" to the entry in focus. The module's own Submit takes the
    /// bottom face button of every pad; on a Nintendo pad that is B, which goes back, and A - the right-hand one -
    /// confirms. So Submit is left to the keyboard here, and a pad's confirm button
    /// (InputDeviceTracker.ConfirmButton: known pads and plain joysticks alike) is passed on by hand.
    /// </summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class UIInputBootstrap : MonoBehaviour
    {
        GameObject last;
        InputActionAsset own;

        void Awake()
        {
            var m = GetComponent<InputSystemUIInputModule>();
            if (m.actionsAsset == null || m.move == null || m.move.action == null) m.AssignDefaultActions();
            own = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = own.AddActionMap("MenuConfirm");
            var submit = map.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            m.submit = InputActionReference.Create(submit);
            map.Enable();
        }

        void OnDestroy() { if (own != null) Destroy(own); }

        void Update()
        {
            if (KeyBindings.Busy || !InputDeviceTracker.PadConfirmPressed) return;
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (Usable(sel)) ExecuteEvents.Execute(sel, new BaseEventData(es), ExecuteEvents.submitHandler);
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
