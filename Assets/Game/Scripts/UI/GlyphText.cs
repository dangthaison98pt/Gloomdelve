using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Sits next to a TextMesh Pro text and turns input tokens in it ("[jump]", "[use]", "[gear]"...) into buttons
    /// that follow the device in use (keyboard or gamepad). Scripts just assign text.text with tokens.
    /// How it works: each token is replaced by one blank character made exactly as wide as its button, and the
    /// button (a blank shape with its label in the same font as the text) is placed over that gap, so wrapping
    /// and alignment keep working and the lettering is as sharp as the rest of the text.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GlyphText : MonoBehaviour
    {
        public InputGlyphSet set;
        [Tooltip("Button height relative to the font size.")] public float scale = 1.35f;
        [Tooltip("Label size relative to the button height.")] public float labelScale = 0.58f;
        [Tooltip("Always show the keyboard key, even while a gamepad is in use (the change-keys screen).")] public bool keyboardOnly;
        [Tooltip("Always show the gamepad button, even while the keyboard is in use (the gamepad column of that screen).")] public bool gamepadOnly;

        struct Spot { public Sprite sprite; public string label; public Color ink; public float width, gap; }
        class Icon { public Image image; public TMP_Text label; }

        TMP_Text text;
        string raw, shown;
        int device, keysVersion;
        Vector2 lastSize;
        float lastFontSize;
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Icon> icons = new List<Icon>();
        const char Mark = (char)0xA0;      // the blank that stands in for a button (texts never use it themselves)

        void Awake() { text = GetComponent<TMP_Text>(); }

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
            int kind = keyboardOnly ? 0 : gamepadOnly ? Mathf.Max(1, InputDeviceTracker.Kind) : InputDeviceTracker.Kind;
            if (device != kind) { device = kind; dirty = true; }
            if (keysVersion != KeyBindings.Version) { keysVersion = KeyBindings.Version; dirty = true; }   // the player changed a key
            var size = text.rectTransform.rect.size;
            if (dirty) Rebuild();
            else if ((size != lastSize || !Mathf.Approximately(lastFontSize, text.fontSize)) && spots.Count > 0) Place();
            lastSize = size;
        }

        /// <summary>The size the buttons are measured in: the size written on the text (a text that shrinks to fit shrinks its buttons with it in Place).</summary>
        float BaseSize => text.enableAutoSizing ? text.fontSizeMax : text.fontSize;

        /// <summary>Width of a button label set in bold at the given size.</summary>
        float LabelWidth(string s, float size)
        {
            if (string.IsNullOrEmpty(s) || text.font == null) return 0f;
            var f = text.font;
            float w = 0f, k = size / f.faceInfo.pointSize * f.faceInfo.scale;
            foreach (char ch in s)
            {
                if (f.characterLookupTable.TryGetValue(ch, out var c) || (f.TryAddCharacters(ch.ToString()) && f.characterLookupTable.TryGetValue(ch, out c)))
                    w += c.glyph.metrics.horizontalAdvance * k;
                else w += size * 0.8f;          // a sign from the fallback font (arrows): about square
            }
            return w * 1.06f;                   // bold runs a little wider
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
            float fs = BaseSize;
            float h = fs * scale;
            float labelSize = Mathf.Max(8f, h * labelScale);
            var sb = new StringBuilder(raw.Length + 32);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                int end = ch == '[' ? raw.IndexOf(']', i + 1) : -1;
                Sprite s = null; string label = ""; Color ink = Color.white;
                if (end <= i + 1 || !set.Find(raw.Substring(i + 1, end - i - 1), device, out s, out label, out ink)) { sb.Append(ch); continue; }
                // a 9-slice shape (key, pill) grows with its label; a fixed shape (round button) keeps its proportions
                float w = s.border.sqrMagnitude > 0f
                    ? Mathf.Max(h, LabelWidth(label, labelSize) + h * 0.62f)
                    : h * s.rect.width / s.rect.height;
                float gap = w + fs * 0.12f;
                spots.Add(new Spot { sprite = s, label = label, ink = ink, width = w, gap = gap });
                // one blank, exactly as wide as the button (in em, so it follows the font size)
                sb.Append("<mspace=").Append((gap / fs).ToString("0.###", CultureInfo.InvariantCulture)).Append("em>").Append(Mark).Append("</mspace>");
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
            text.ForceMeshUpdate();
            lastFontSize = text.fontSize;
            var info = text.textInfo;
            float k = text.fontSize / Mathf.Max(1f, BaseSize);      // < 1 when the text had to shrink to fit
            float h = BaseSize * scale * k;
            var canvas = text.canvas;
            float refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            int n = 0;
            for (int ci = 0; ci < info.characterCount && n < spots.Count; ci++)
            {
                var c = info.characterInfo[ci];
                if (c.character != Mark) continue;
                var sp = spots[n];
                var icon = IconAt(n);
                n++;
                icon.image.gameObject.SetActive(true);
                icon.image.sprite = sp.sprite;
                icon.image.color = new Color(1f, 1f, 1f, text.color.a);
                bool sliced = sp.sprite.border.sqrMagnitude > 0f;
                icon.image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                if (sliced)   // border drawn at about a third of the button height
                    icon.image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, sp.sprite.border.x * refPpu / sp.sprite.pixelsPerUnit / (h * 0.34f));
                var rt = icon.image.rectTransform;
                rt.sizeDelta = new Vector2(sp.width * k, h);
                // xAdvance is where the next character starts: the button sits in the middle of the gap before it,
                // centred on the height of the capitals
                rt.anchoredPosition = new Vector2(c.xAdvance - sp.gap * k * 0.5f, c.baseLine + text.fontSize * 0.31f);
                icon.label.font = text.font;
                icon.label.fontSharedMaterial = text.font.material;
                icon.label.fontSize = Mathf.Max(8f, h * labelScale);
                icon.label.text = sp.label;
                icon.label.color = new Color(sp.ink.r, sp.ink.g, sp.ink.b, text.color.a);
                icon.label.rectTransform.anchoredPosition = new Vector2(0f, sliced ? h * 0.05f : 0f);
            }
            for (int i = n; i < spots.Count && i < icons.Count; i++) icons[i].image.gameObject.SetActive(false);
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
                var lt = lgo.AddComponent<TextMeshProUGUI>();
                lt.raycastTarget = false;
                lt.alignment = TextAlignmentOptions.Center;
                lt.fontStyle = FontStyles.Bold;
                lt.textWrappingMode = TextWrappingModes.NoWrap;
                lt.overflowMode = TextOverflowModes.Overflow;
                var lrt = lt.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                icons.Add(new Icon { image = img, label = lt });
            }
            return icons[i];
        }
    }
}
