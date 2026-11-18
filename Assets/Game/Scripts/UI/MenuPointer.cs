using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The little marker that sits to the left of the menu entry in focus (keyboard, pad or mouse), like a finger on
    /// a list. Only shows for entries inside 'scope'.
    /// </summary>
    public class MenuPointer : MonoBehaviour
    {
        public RectTransform pointer;
        [Tooltip("Only entries under this object get the marker (the main list).")] public Transform scope;
        public float gap = 34f;

        void LateUpdate()
        {
            if (pointer == null) return;
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            bool show = sel != null && scope != null && sel.activeInHierarchy && sel.transform.IsChildOf(scope);
            if (pointer.gameObject.activeSelf != show) pointer.gameObject.SetActive(show);
            if (!show) return;
            var text = sel.GetComponentInChildren<TMPro.TMP_Text>();
            float half = text != null ? text.preferredWidth * 0.5f : 80f;
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 4f;
            var centre = ((RectTransform)sel.transform).rect.center;       // the entry's own middle, wherever its pivot is
            pointer.position = sel.transform.TransformPoint(new Vector3(centre.x - (half + gap) + bob, centre.y, 0f));
        }
    }
}
