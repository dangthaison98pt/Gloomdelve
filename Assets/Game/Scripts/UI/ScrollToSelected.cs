using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Sits on a ScrollRect whose content is a column of buttons: when the selection moves (keys, gamepad) to a
    /// button that is out of view, the list scrolls just far enough to show it. The mouse wheel and the scrollbar
    /// work by themselves (ScrollRect).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollToSelected : MonoBehaviour
    {
        [Tooltip("Room kept above and below the selected button.")] public float margin = 6f;

        ScrollRect scroll;
        GameObject last;

        void Awake() { scroll = GetComponent<ScrollRect>(); }

        void OnEnable() { last = null; }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == last) return;
            last = sel;
            if (sel == null || scroll == null || scroll.content == null) return;
            var rt = sel.transform as RectTransform;
            if (rt == null || !rt.IsChildOf(scroll.content)) return;
            var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            // the button's top and bottom edge, measured in the viewport's own space
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float top = view.InverseTransformPoint(corners[1]).y, bottom = view.InverseTransformPoint(corners[0]).y;
            var r = view.rect;
            float move = 0f;
            if (top > r.yMax - margin) move = top - (r.yMax - margin);              // above the view: the content goes down
            else if (bottom < r.yMin + margin) move = bottom - (r.yMin + margin);   // below it: the content goes up
            if (Mathf.Approximately(move, 0f)) return;
            var p = scroll.content.anchoredPosition;
            float max = Mathf.Max(0f, scroll.content.rect.height - r.height);
            p.y = Mathf.Clamp(p.y - move, 0f, max);
            scroll.content.anchoredPosition = p;
            scroll.velocity = Vector2.zero;
        }
    }
}
