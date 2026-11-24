using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// A screen-space canvas laid out in the 16:9 frame: everything under it is moved into a child "Frame" the size
    /// of the frame, and the canvas is scaled as if the screen were the frame. Things put under the canvas later
    /// are moved in too.
    /// </summary>
    [DisallowMultipleComponent]
    public class FramedCanvas : MonoBehaviour
    {
        RectTransform frame;
        Canvas canvas;
        CanvasScaler scaler;
        Vector2 reference = new Vector2(1920f, 1080f);
        float match = 0.5f;
        bool scaled;
        int lastW = -1, lastH = -1;
        readonly System.Collections.Generic.List<Transform> moving = new System.Collections.Generic.List<Transform>();

        public RectTransform Frame => frame;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                reference = scaler.referenceResolution;
                match = scaler.matchWidthOrHeight;
                scaled = true;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;     // the scale is worked out here, from the frame
            }
            frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.gameObject.layer = gameObject.layer;
            frame.SetParent(transform, false);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            Gather();
            Fit();
        }

        void LateUpdate()
        {
            if (transform.childCount > 1) Gather();
            Fit();
        }

        void Gather()
        {
            moving.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var t = transform.GetChild(i);
                if (t != frame) moving.Add(t);
            }
            foreach (var t in moving) t.SetParent(frame, false);     // (keeps anchors and offsets: the frame stands in for the canvas)
            moving.Clear();
        }

        void Fit()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width; lastH = Screen.height;
            float w = ScreenFrame.W, h = ScreenFrame.H;
            float s;
            if (scaled)
            {
                // what CanvasScaler's "scale with screen size" works out, for a screen the size of the frame
                float lw = Mathf.Log(w / reference.x, 2f), lh = Mathf.Log(h / reference.y, 2f);
                s = Mathf.Pow(2f, Mathf.Lerp(lw, lh, match));
                scaler.scaleFactor = s;
            }
            else s = canvas != null ? canvas.scaleFactor : 1f;
            s = Mathf.Max(0.0001f, s);
            frame.sizeDelta = new Vector2(w, h) / s;
            frame.anchoredPosition = new Vector2(0f, (ScreenFrame.Y0 + h * 0.5f - Screen.height * 0.5f) / s);
        }
    }
}
