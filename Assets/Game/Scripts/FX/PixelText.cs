using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>World-space pixel text built from glyph sprites (price tags, floating numbers).</summary>
    public class PixelText : MonoBehaviour
    {
        public PixelFont font;
        public Color color = Color.white;
        public int sortingOrder = 60;
        public Material material;
        [SerializeField] string text = "";

        readonly List<SpriteRenderer> glyphs = new List<SpriteRenderer>();

        public string Text => text;

        void Start() { Rebuild(); }

        public void Set(string t, Color c)
        {
            text = t ?? "";
            color = c;
            Rebuild();
        }

        public void SetAlpha(float a)
        {
            foreach (var g in glyphs) { var c = g.color; c.a = a; g.color = c; }
        }

        void Rebuild()
        {
            if (font == null) return;
            while (glyphs.Count < text.Length)
            {
                var go = new GameObject("g");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = sortingOrder;
                glyphs.Add(sr);
            }
            float width = text.Length * font.advance;
            float x0 = -width * 0.5f + font.advance * 0.5f;
            for (int i = 0; i < glyphs.Count; i++)
            {
                var g = glyphs[i];
                if (i >= text.Length) { g.enabled = false; continue; }
                g.enabled = true;
                g.sprite = font.Get(text[i]);
                g.color = color;
                g.sortingOrder = sortingOrder;
                g.transform.localPosition = new Vector3(x0 + i * font.advance, 0f, 0f);
            }
        }
    }
}
