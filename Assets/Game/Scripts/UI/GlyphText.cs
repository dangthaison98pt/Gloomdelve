using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Sits next to a UI Text and turns input tokens in it ("[jump]", "[use]", "[gear]"...) into button pictures
    /// that follow the device in use (keyboard or gamepad). Scripts just assign text.text with tokens.
    /// How it works: each token is replaced by a run of non-breaking spaces as wide as its picture, and the
    /// picture is placed over that gap, so wrapping and alignment keep working.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class GlyphText : MonoBehaviour
    {
        public InputGlyphSet set;
        [Tooltip("Picture height relative to the font size.")] public float scale = 1.35f;

        struct Spot { public int index, count; public Sprite sprite; public float width; }

        Text text;
        string raw, shown;
        bool pad;
        Vector2 lastSize;
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Image> icons = new List<Image>();
        static readonly TextGenerator gen = new TextGenerator();
        const char Gap = ' ';

        void Awake() { text = GetComponent<Text>(); }

        void LateUpdate()
        {
            if (set == null || text == null) return;
            InputDeviceTracker.Poll();
            string cur = text.text;
            bool dirty = false;
            if (cur != shown)
            {
                if (cur == raw) text.text = shown;      // a script assigned the same tokens again: nothing to redo
                else { raw = cur; dirty = true; }
            }
            if (raw == null) return;
            if (pad != InputDeviceTracker.UsingGamepad) { pad = InputDeviceTracker.UsingGamepad; dirty = true; }
            var size = text.rectTransform.rect.size;
            if (dirty) Rebuild();
            else if (size != lastSize && spots.Count > 0) Place();
            lastSize = size;
        }

        void Rebuild()
        {
            spots.Clear();
            if (raw.IndexOf('[') < 0)
            {
                shown = raw;
                Place();
                return;
            }
            float gapW = GapWidth();
            float h = text.fontSize * scale;
            var sb = new StringBuilder(raw.Length + 16);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                int end = ch == '[' ? raw.IndexOf(']', i + 1) : -1;
                Sprite s = end > i + 1 ? set.Find(raw.Substring(i + 1, end - i - 1), pad) : null;
                if (s == null) { sb.Append(ch); continue; }
                float w = h * s.rect.width / s.rect.height;
                int n = Mathf.Max(1, Mathf.CeilToInt((w + text.fontSize * 0.1f) / gapW));
                spots.Add(new Spot { index = sb.Length, count = n, sprite = s, width = w });
                sb.Append(Gap, n);
                i = end;
            }
            shown = sb.ToString();
            text.text = shown;
            Place();
        }

        float GapWidth()
        {
            var f = text.font;
            if (f != null)
            {
                f.RequestCharactersInTexture(Gap.ToString(), text.fontSize, text.fontStyle);
                if (f.GetCharacterInfo(Gap, out var ci, text.fontSize, text.fontStyle) && ci.advance > 0) return ci.advance;
            }
            return text.fontSize * 0.28f;
        }

        void Place()
        {
            for (int i = spots.Count; i < icons.Count; i++) icons[i].enabled = false;
            if (spots.Count == 0) return;
            gen.Populate(shown, text.GetGenerationSettings(text.rectTransform.rect.size));
            var chars = gen.characters;
            float k = 1f / text.pixelsPerUnit;
            float h = text.fontSize * scale;
            for (int i = 0; i < spots.Count; i++)
            {
                var sp = spots[i];
                var icon = IconAt(i);
                int last = sp.index + sp.count - 1;
                if (last >= chars.Count) { icon.enabled = false; continue; }
                var a = chars[sp.index];
                var b = chars[last];
                float x0 = a.cursorPos.x * k;
                float x1 = Mathf.Approximately(a.cursorPos.y, b.cursorPos.y) ? (b.cursorPos.x + b.charWidth) * k : x0 + sp.width;
                icon.enabled = true;
                icon.sprite = sp.sprite;
                icon.color = new Color(1f, 1f, 1f, text.color.a);
                var rt = icon.rectTransform;
                rt.sizeDelta = new Vector2(sp.width, h);
                rt.anchoredPosition = new Vector2((x0 + x1) * 0.5f, a.cursorPos.y * k - text.fontSize * 0.58f);
            }
        }

        Image IconAt(int i)
        {
            while (icons.Count <= i)
            {
                var go = new GameObject("Glyph", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = text.rectTransform.pivot;
                rt.pivot = new Vector2(0.5f, 0.5f);
                icons.Add(img);
            }
            return icons[i];
        }
    }
}
