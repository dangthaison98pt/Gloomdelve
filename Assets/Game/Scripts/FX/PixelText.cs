using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// World-space text (price tags, floating numbers, "FROZEN"...). Drawn with the normal UI font through
    /// TextMesh so it stays sharp at any zoom, with a dark copy behind it for contrast.
    /// (The name is historical: it used to be built from pixel-font sprites.)
    /// </summary>
    public class PixelText : MonoBehaviour
    {
        [Tooltip("Unused: kept so existing prefabs and scenes still load.")] public PixelFont font;
        [Tooltip("Font to draw with. Empty = the built-in UI font.")] public Font uiFont;
        public Color color = Color.white;
        public int sortingOrder = 60;
        [Tooltip("Unused: the font's own material is used.")] public Material material;
        [Tooltip("Height of a line of text in tiles.")] public float lineHeight = 0.4f;
        [SerializeField] string text = "";

        const int Raster = 48;      // glyphs are rendered at this size and scaled down, so they stay crisp
        TextMesh main, shadow;
        float alpha = 1f;

        public string Text => text;

        void Start() { Rebuild(); }

        public void Set(string t, Color c)
        {
            text = t ?? "";
            color = c;
            alpha = 1f;
            Rebuild();
        }

        public void SetAlpha(float a)
        {
            alpha = a;
            Apply();
        }

        TextMesh Make(string name, Vector3 offset, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            var f = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = f;
            tm.fontSize = Raster;
            tm.characterSize = lineHeight * 10f / Raster;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            if (f != null) mr.sharedMaterial = f.material;
            mr.sortingOrder = order;
            return tm;
        }

        void Rebuild()
        {
            if (main == null)
            {
                shadow = Make("Shadow", new Vector3(0.035f, -0.035f, 0f), sortingOrder);
                main = Make("Text", Vector3.zero, sortingOrder + 1);
            }
            main.text = text;
            shadow.text = text;
            Apply();
        }

        void Apply()
        {
            if (main == null) return;
            main.color = new Color(color.r, color.g, color.b, color.a * alpha);
            shadow.color = new Color(0f, 0f, 0f, 0.85f * color.a * alpha);
        }
    }
}
