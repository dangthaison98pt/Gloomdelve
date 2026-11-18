using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// A quick fade through black for stepping through a door: the screen darkens, the change happens while
    /// nothing can be seen, the screen clears again. ScreenFade.Through(() => ...). Makes its own overlay the
    /// first time it is used; runs on unscaled time.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        static ScreenFade instance;
        Image cover;
        bool busy;

        /// <summary>A fade is running: doors do not react to another press meanwhile.</summary>
        public static bool Busy => instance != null && instance.busy;

        static ScreenFade Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenFade");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var img = new GameObject("Cover", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            img.color = new Color(0.02f, 0.02f, 0.05f, 0f);
            img.raycastTarget = false;
            instance = go.AddComponent<ScreenFade>();
            instance.cover = img;
            return instance;
        }

        /// <summary>Fades out, runs 'change' in the dark, fades back in. Does the change at once when a fade is already running.</summary>
        public static void Through(Action change, float outTime = 0.16f, float hold = 0.06f, float inTime = 0.24f)
        {
            var f = Get();
            if (f.busy) { change?.Invoke(); return; }
            f.StartCoroutine(f.Run(change, outTime, hold, inTime));
        }

        IEnumerator Run(Action change, float outTime, float hold, float inTime)
        {
            busy = true;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime) { SetAlpha(t / outTime); yield return null; }
            SetAlpha(1f);
            change?.Invoke();
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime) { SetAlpha(1f - t / inTime); yield return null; }
            SetAlpha(0f);
            busy = false;
        }

        void SetAlpha(float a)
        {
            var c = cover.color;
            c.a = Mathf.Clamp01(a);
            cover.color = c;
        }
    }
}
