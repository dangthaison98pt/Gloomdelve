using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gloomdelve
{
    /// <summary>
    /// Makes a piece of hand-built UI (a cell of the bag, a tab) answer the mouse: the pointer coming over it and a
    /// click on it are passed on to whoever built it. For screens that are steered by the game's own input and
    /// have no Buttons of their own. The Image it sits on must be a raycast target.
    /// </summary>
    public class PointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public Action onEnter, onClick;

        public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) onClick?.Invoke();
        }

        /// <summary>Puts a relay on this object (its Image, if it has one, becomes a raycast target).</summary>
        public static PointerRelay On(GameObject go, Action enter, Action click)
        {
            var r = go.GetComponent<PointerRelay>();
            if (r == null) r = go.AddComponent<PointerRelay>();
            r.onEnter = enter; r.onClick = click;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = true;
            return r;
        }
    }
}
