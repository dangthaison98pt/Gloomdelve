using UnityEngine;
using UnityEngine.EventSystems;

namespace Gloomdelve
{
    /// <summary>
    /// One tile of the Achievements grid. In focus a soft gold glow shows behind it; pressed (a click, or confirm) it shows its name
    /// and what earns it beside it, pressed again it hides them. The box goes too when the focus moves on.
    /// </summary>
    public class AchievementTile : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public AchievementsPanel panel;
        public int index;
        [Tooltip("The glow behind it while it is in focus.")] public UnityEngine.UI.Image focus;

        void Awake()
        {
            var b = GetComponent<UnityEngine.UI.Button>();
            if (b != null) b.onClick.AddListener(() => { if (panel != null) panel.ToggleTip(index); });
        }

        void OnEnable() { if (focus != null) focus.enabled = false; }

        public void OnSelect(BaseEventData e) { if (focus != null) focus.enabled = true; }
        public void OnDeselect(BaseEventData e) { if (focus != null) focus.enabled = false; if (panel != null) panel.HideTip(index); }
    }
}
