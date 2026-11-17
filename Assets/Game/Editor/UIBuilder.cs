using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gloomdelve.EditorTools
{
    /// <summary>Builds the uGUI hierarchies (HUD, pause, results, transition, main menu) inside scenes.</summary>
    public static class UIBuilder
    {
        public static Font font;
        public static Sprite panelSprite, buttonSprite, slotSprite;
        /// <summary>When set, every label can show button pictures for input tokens such as "[jump]" (GlyphText).</summary>
        public static InputGlyphSet glyphSet;

        static readonly Color Gold = new Color(1f, 0.84f, 0.38f);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);

        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        public static GameObject Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 box, bool shadow = true, FontStyle style = FontStyle.Normal)
        {
            var go = Node(parent, name);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Place(Rect(go), anchor, pos, box);
            if (glyphSet != null) go.AddComponent<GlyphText>().set = glyphSet;
            if (shadow)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Image Img(Transform parent, string name, Sprite s, Color c, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced = false)
        {
            var go = Node(parent, name);
            var i = go.AddComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = false;
            if (sliced) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = s == panelSprite ? 1.7f : 2.5f; }
            Place(Rect(go), anchor, pos, size);
            return i;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
            => Img(parent, name, panelSprite, Color.white, anchor, pos, size, true);

        public static Image Dimmer(Transform parent, string name)
        {
            var i = Img(parent, name, null, Dim, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(i.rectTransform);
            i.raycastTarget = true;
            return i;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, out Text text)
        {
            var go = Node(parent, name);
            var img = go.AddComponent<Image>();
            img.sprite = buttonSprite; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2.5f;
            var b = go.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = new Color(0.22f, 0.18f, 0.24f);
            cb.highlightedColor = new Color(0.62f, 0.44f, 0.22f);
            cb.selectedColor = new Color(0.78f, 0.56f, 0.24f);
            cb.pressedColor = new Color(1f, 0.8f, 0.4f);
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f;
            b.colors = cb;
            Place(Rect(go), anchor, pos, size);
            text = Label(go.transform, "Label", label, 30, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return b;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size)
            => Button(parent, name, label, anchor, pos, size, out _);

        public static Slider Slider(Transform parent, string name, string label, Vector2 pos, out Text valueText)
        {
            var row = Node(parent, name);
            Place(Rect(row), new Vector2(0.5f, 1f), pos, new Vector2(620, 56));
            Label(row.transform, "Label", label, 28, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(220, 50));
            var sgo = Node(row.transform, "Slider");
            Place(Rect(sgo), new Vector2(0f, 0.5f), new Vector2(230, 0), new Vector2(280, 30));
            var bg = Img(sgo.transform, "Background", buttonSprite, new Color(0.15f, 0.12f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            Stretch(bg.rectTransform, 6);
            var fillArea = Node(sgo.transform, "Fill Area"); Stretch(Rect(fillArea), 6);
            var fill = Img(fillArea.transform, "Fill", buttonSprite, new Color(0.85f, 0.62f, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            Stretch(fill.rectTransform);
            var handleArea = Node(sgo.transform, "Handle Area"); Stretch(Rect(handleArea), 10);
            var handle = Img(handleArea.transform, "Handle", buttonSprite, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 40), true);
            var s = sgo.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = 0; s.maxValue = 1; s.value = 0.8f;
            var cb = s.colors; cb.selectedColor = Gold; cb.highlightedColor = Gold; s.colors = cb;
            valueText = Label(row.transform, "Value", "80%", 26, Gold, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(0, 0), new Vector2(100, 50));
            return s;
        }

        public static Toggle Toggle(Transform parent, string name, string label, Vector2 pos)
        {
            var row = Node(parent, name);
            Place(Rect(row), new Vector2(0.5f, 1f), pos, new Vector2(620, 56));
            var box = Img(row.transform, "Box", buttonSprite, new Color(0.15f, 0.12f, 0.16f), new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(44, 44), true);
            var check = Img(box.transform, "Check", buttonSprite, new Color(0.9f, 0.68f, 0.28f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26), true);
            Label(row.transform, "Label", label, 28, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(64, 0), new Vector2(520, 50));
            var t = row.AddComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = box; t.graphic = check; t.isOn = true;
            var cb = t.colors; cb.selectedColor = Gold; cb.highlightedColor = Gold; t.colors = cb;
            return t;
        }

        public static InputField Input(Transform parent, string name, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = Node(parent, name);
            var img = go.AddComponent<Image>();
            img.sprite = buttonSprite; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2.5f;
            img.color = new Color(0.12f, 0.1f, 0.14f);
            Place(Rect(go), anchor, pos, size);
            var ph = Label(go.transform, "Placeholder", placeholder, 26, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20, 0), false, FontStyle.Italic);
            var tx = Label(go.transform, "Text", "", 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20, 0), false);
            tx.supportRichText = false;
            var f = go.AddComponent<InputField>();
            f.textComponent = tx; f.placeholder = ph; f.contentType = InputField.ContentType.IntegerNumber; f.characterLimit = 9;
            f.targetGraphic = img;
            var cb = f.colors; cb.selectedColor = new Color(0.5f, 0.4f, 0.25f); cb.highlightedColor = new Color(0.35f, 0.28f, 0.2f); f.colors = cb;
            return f;
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            go.AddComponent<UIInputBootstrap>();
        }

        // ------------------------------------------------------------------ settings panel (shared)

        public static SettingsPanel Settings(Transform parent)
        {
            var p = Panel(parent, "SettingsPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 700));
            Label(p.transform, "Title", "SETTINGS", 46, Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(700, 70));
            var sp = p.gameObject.AddComponent<SettingsPanel>();
            sp.master = Slider(p.transform, "Master", "Master", new Vector2(0, -120), out sp.masterValue);
            sp.music = Slider(p.transform, "Music", "Music", new Vector2(0, -190), out sp.musicValue);
            sp.sfx = Slider(p.transform, "Sfx", "Effects", new Vector2(0, -260), out sp.sfxValue);
            sp.fullscreen = Toggle(p.transform, "Fullscreen", "Fullscreen  (F11)", new Vector2(0, -340));
            sp.shake = Toggle(p.transform, "Shake", "Screen shake", new Vector2(0, -410));
            sp.keys = Button(p.transform, "Keys", "CONTROLS / KEYS", new Vector2(0.5f, 1f), new Vector2(0, -486), new Vector2(420, 64));
            sp.back = Button(p.transform, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(320, 70));
            return sp;
        }

        /// <summary>Settings > Keys: one row per KeyBindings.Slots entry - action, keyboard key (can be changed), gamepad button (fixed).</summary>
        public static KeyBindPanel KeysPanel(Transform parent)
        {
            var slots = KeyBindings.Slots;
            const float rowH = 48f, step = 54f, top = 150f, keyX = -230f, padX = -80f;
            var p = Panel(parent, "KeysPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, top + slots.Length * step + 205f));
            Label(p.transform, "Title", "CONTROLS", 46, Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(700, 70));
            var head = Node(p.transform, "Head");
            Place(Rect(head), new Vector2(0.5f, 1f), new Vector2(0, -top + 34f), new Vector2(700, 30));
            Color dim = new Color(1f, 1f, 1f, 0.6f);
            Label(head.transform, "Keyboard", "KEYBOARD", 18, dim, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(keyX + 80f, 0), new Vector2(160, 30), false);
            Label(head.transform, "Gamepad", "GAMEPAD", 18, dim, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(padX + 70f, 0), new Vector2(140, 30), false);
            var kp = p.gameObject.AddComponent<KeyBindPanel>();
            kp.rows = new Button[slots.Length];
            kp.keyTexts = new Text[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var b = Button(p.transform, "Row_" + slots[i].id, "", new Vector2(0.5f, 1f), new Vector2(0, -top - i * step), new Vector2(700, rowH));
                Label(b.transform, "Name", slots[i].title, 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(24, 0), new Vector2(330, rowH));
                string token = "[" + slots[i].token + "]";
                var key = Label(b.transform, "Key", token, 24, Gold, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(keyX + 80f, 0), new Vector2(160, rowH), false);
                var g = key.GetComponent<GlyphText>();
                if (g != null) g.keyboardOnly = true;       // this column is the keyboard even while a pad is in use
                var pad = Label(b.transform, "Pad", token, 24, Color.white, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(padX + 70f, 0), new Vector2(140, rowH), false);
                g = pad.GetComponent<GlyphText>();
                if (g != null) g.gamepadOnly = true;        // and this one the pad even while the keyboard is in use
                kp.rows[i] = b;
                kp.keyTexts[i] = key;
            }
            kp.hint = Label(p.transform, "Hint", "", 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 124), new Vector2(760, 64));
            kp.resetButton = Button(p.transform, "Reset", "DEFAULT KEYS", new Vector2(0.5f, 0f), new Vector2(-175, 45), new Vector2(330, 70));
            kp.back = Button(p.transform, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(175, 45), new Vector2(300, 70));
            return kp;
        }
    }
}
