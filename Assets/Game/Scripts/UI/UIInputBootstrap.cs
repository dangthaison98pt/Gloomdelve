using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Gloomdelve
{
    /// <summary>Makes sure the UI input module has actions (keyboard/gamepad navigation, mouse).</summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class UIInputBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var m = GetComponent<InputSystemUIInputModule>();
            if (m.actionsAsset == null || m.move == null || m.move.action == null) m.AssignDefaultActions();
        }
    }
}
