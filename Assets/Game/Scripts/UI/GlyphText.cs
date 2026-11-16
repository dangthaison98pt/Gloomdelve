using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Sits next to a UI Text and turns input tokens in it ("[jump]", "[use]", "[gear]"...) into buttons that
    /// follow the device in use (keyboard or gamepad). Scripts just assign text.text with tokens.
    /// How it works: each token is replaced by a run of non-breaking spaces as wide as its button, and the
    /// button (a blank shape with its label in the same font as the text) is placed over that gap, so wrapping
    /// and alignment keep working and the lettering is as sharp as the rest of the text.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class GlyphText : MonoBehaviour
    {
        public InputGlyphSet set;
        [Tooltip("Button height relative to the font size.")] public float scale = 1.35f;
        [Tooltip("Label size relative to the button height.")] public float labelScale = 0.58f;

        struct Spot { public int index, count; public Sprite sprite; public string label; public Color ink; public float width; }
        class Icon { public Image image; public Text label; }

        Text text;
        string raw, shown;
        int device;
        Vector2 lastSize;
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Icon> icons = new List<Icon>();
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
            if (device != InputDeviceTracker.Kind) { device = InputDeviceTracker.Kind; dirty = true; }
            var size = text.rectTransform.rect.size;
            if (dirty) Rebuild();
            else if (size != lastSize && spots.Count > 0) Place();
            lastSize = size;
        }

        float Height => text.fontSize * scale;
        int LabelSize => Mathf.Max(8, Mathf.RoundToInt(Height * labelScale));

        float Advance(string s, int size, FontStyle style)
        {
            var f = text.font;
            if (f == null || string.IsNullOrEmpty(s)) return 0f;
            f.RequestCharactersInTexture(s, size, style);
            float w = 0f;
            foreach (char ch in s)
                w += f.GetCharacterInfo(ch, out var ci, size, style) ? ci.advance : size * 0.5f;
            return w;
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
            float gapW = Advance(Gap.ToString(), text.fontSize, text.fontStyle);
            if (gapW <= 0f) gapW = text.fontSize * 0.28f;
            float h = Height;
            var sb = new StringBuilder(raw.Length + 16);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                int end = ch == '[' ? raw.IndexOf(']', i + 1) : -1;
                Sprite s = null; string label = ""; Color ink = Color.white;
                if (end <= i + 1 || !set.Find(raw.Substring(i + 1, end - i - 1), device, out s, out label, out ink)) { sb.Append(ch); continue; }
                // a 9-slice shape (key, pill) grows with its label; a fixed shape (round button) keeps its proportions
                float w = s.border.sqrMagnitude > 0f
                    ? Mathf.Max(h, Advance(label, LabelSize, FontStyle.Bold) + h * 0.62f)
                    : h * s.rect.width / s.rect.height;
                int n = Mathf.Max(1, Mathf.CeilToInt((w + text.fontSize * 0.1f) / gapW));
                spots.Add(new Spot { index = sb.Length, count = n, sprite = s, label = label, ink = ink, width = w });
                sb.Append(Gap, n);
                i = end;
            }
            shown = sb.ToString();
            text.text = shown;
            Place();
        }

        void Place()
        {
            for (int i = spots.Count; i < icons.Count; i++) icons[i].image.gameObject.SetActive(false);
            if (spots.Count == 0) return;
            gen.Populate(shown, text.GetGenerationSettings(text.rectTransform.rect.size));
            var chars = gen.characters;
            float k = 1f / text.pixelsPerUnit;
            float h = Height;
            float refPpu = text.canvas != null ? text.canvas.referencePixelsPerUnit : 100f;
            for (int i = 0; i < spots.Count; i++)
            {
                var sp = spots[i];
                var icon = IconAt(i);
                int last = sp.index + sp.count - 1;
                if (last >= chars.Count) { icon.image.gameObject.SetActive(false); continue; }
                var a = chars[sp.index];
                var b = chars[last];
                float x0 = a.cursorPos.x * k;
                float x1 = Mathf.Approximately(a.cursorPos.y, b.cursorPos.y) ? (b.cursorPos.x + b.charWidth) * k : x0 + sp.width;
                icon.image.gameObject.SetActive(true);
                icon.image.sprite = sp.sprite;
                icon.image.color = new Color(1f, 1f, 1f, text.color.a);
                bool sliced = sp.sprite.border.sqrMagnitude > 0f;
                icon.image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                if (sliced)   // border drawn at about a third of the button height
                    icon.image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, sp.sprite.border.x * refPpu / sp.sprite.pixelsPerUnit / (h * 0.34f));
                var rt = icon.image.rectTransform;
                rt.sizeDelta = new Vector2(sp.width, h);
                rt.anchoredPosition = new Vector2((x0 + x1) * 0.5f, a.cursorPos.y * k - text.fontSize * 0.58f);
                icon.label.font = text.font;
                icon.label.fontSize = LabelSize;
                icon.label.text = sp.label;
                icon.label.color = new Color(sp.ink.r, sp.ink.g, sp.ink.b, text.color.a);
                icon.label.rectTransform.anchoredPosition = new Vector2(0f, sliced ? h * 0.05f : 0f);
            }
        }

        Icon IconAt(int i)
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

                var lgo = new GameObject("Label", typeof(RectTransform));
                lgo.transform.SetParent(go.transform, false);
                var lt = lgo.AddComponent<Text>();
                lt.raycastTarget = false;
                lt.alignment = TextAnchor.MiddleCenter;
                lt.fontStyle = FontStyle.Bold;
                lt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lt.verticalOverflow = VerticalWrapMode.Overflow;
                var lrt = lt.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                icons.Add(new Icon { image = img, label = lt });
            }
            return icons[i];
        }
    }
}
