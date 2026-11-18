using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Looks at every text on screen right now and reports layout faults (used by the scenario tests):
    ///   - two texts whose drawn letters run into each other,
    ///   - a text that sticks out of the panel or button it sits in,
    ///   - a text that runs off the screen.
    /// Only what is really visible counts (active, not faded out, not empty). Button pictures inside a text
    /// (GlyphText) are part of that text and are not compared with it.
    /// </summary>
    public static class UIOverlap
    {
        struct Item { public TMP_Text text; public Rect rect; public string path; }

        static readonly Vector3[] corners = new Vector3[4];

        public static List<string> Find(float tolerance = 3f)
        {
            var faults = new List<string>();
            var items = new List<Item>();
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.transform.parent != null && t.transform.parent.name == "Glyph") continue;        // the letter on a button picture
                if (Alpha(t) < 0.05f) continue;
                t.ForceMeshUpdate();
                if (t.textInfo == null || t.textInfo.characterCount == 0) continue;
                // the box around the letters as drawn (TMP's own textBounds is the line box, descender room included)
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                for (int ci = 0; ci < t.textInfo.characterCount; ci++)
                {
                    var ch = t.textInfo.characterInfo[ci];
                    if (!ch.isVisible) continue;
                    lo = Vector2.Min(lo, ch.bottomLeft); hi = Vector2.Max(hi, ch.topRight);
                }
                if (!(hi.x - lo.x > 0.5f) || !(hi.y - lo.y > 0.5f)) continue;
                Vector3 p0 = t.transform.TransformPoint(lo), p1 = t.transform.TransformPoint(hi);
                var r = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
                items.Add(new Item { text = t, rect = r, path = Path(t.transform) });
            }
            float k = Screen.height / 1080f;                 // tolerances are given in 1080p pixels
            float tol = tolerance * k;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            for (int i = 0; i < items.Count; i++)
            {
                var a = items[i];
                if (a.rect.xMin < screen.xMin - tol || a.rect.xMax > screen.xMax + tol || a.rect.yMin < screen.yMin - tol || a.rect.yMax > screen.yMax + tol)
                    faults.Add($"off screen: {a.path} \"{Short(a.text.text)}\"");
                var box = Container(a.text.transform);
                if (box != null)
                {
                    box.GetWorldCorners(corners);
                    var br = Rect.MinMaxRect(corners[0].x - tol, corners[0].y - tol, corners[2].x + tol, corners[2].y + tol);
                    if (a.rect.xMin < br.xMin || a.rect.xMax > br.xMax || a.rect.yMin < br.yMin || a.rect.yMax > br.yMax)
                        faults.Add($"out of its frame ({box.name}): {a.path} \"{Short(a.text.text)}\"");
                }
                var ar = Shrink(a.rect, tol);
                for (int j = i + 1; j < items.Count; j++)
                {
                    var c = items[j];
                    if (c.text.transform.IsChildOf(a.text.transform) || a.text.transform.IsChildOf(c.text.transform)) continue;
                    if (ar.Overlaps(Shrink(c.rect, tol)))
                        faults.Add($"overlap: {a.path} \"{Short(a.text.text)}\"  x  {c.path} \"{Short(c.text.text)}\"");
                }
            }
            return faults;
        }

        static Rect Shrink(Rect r, float d)
        {
            float dx = Mathf.Min(d, r.width * 0.45f), dy = Mathf.Min(d, r.height * 0.45f);
            return Rect.MinMaxRect(r.xMin + dx, r.yMin + dy, r.xMax - dx, r.yMax - dy);
        }

        /// <summary>How visible the text really is: its own alpha times every CanvasGroup above it.</summary>
        static float Alpha(TMP_Text t)
        {
            float a = t.color.a;
            for (var p = t.transform; p != null; p = p.parent)
            {
                var g = p.GetComponent<CanvasGroup>();
                if (g != null) a *= g.alpha;
            }
            return a;
        }

        /// <summary>The panel or button a text sits in: the nearest parent drawn with a panel / strip sprite.</summary>
        static RectTransform Container(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var img = p.GetComponent<Image>();
                if (img == null || img.sprite == null) continue;
                string n = img.sprite.name;
                if (n == "panel_soft" || n == "row_soft" || n == "panel" || n == "button") return (RectTransform)p;
            }
            return null;
        }

        static string Path(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null && p.parent != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static string Short(string s)
        {
            s = s.Replace('\n', ' ');
            int cut = s.IndexOf("<mspace");
            if (cut >= 0) s = s.Substring(0, cut) + "[key]..";
            return s.Length > 28 ? s.Substring(0, 28) + ".." : s;
        }
    }
}
