using System.IO;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>Tiny pixel canvas used by the art generator. (0,0) is bottom-left.</summary>
    public class Px
    {
        public readonly int w, h;
        public readonly Color32[] c;
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public Px(int w, int h)
        {
            this.w = w; this.h = h;
            c = new Color32[w * h];
        }

        public static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        public static Color32 Mul(Color32 col, float f) =>
            new Color32((byte)Mathf.Clamp(col.r * f, 0, 255), (byte)Mathf.Clamp(col.g * f, 0, 255), (byte)Mathf.Clamp(col.b * f, 0, 255), col.a);

        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        /// <summary>
        /// Where the drawing's (0, 0) lies in the picture: a margin to the left and below, so that a drawing made
        /// for a w x h sheet can be given room all round without changing one of its numbers (see Framed).
        /// </summary>
        public int ox, oy;

        /// <summary>A sheet for a drawing of w x h with 'pad' empty pixels to the left, to the right and above it.</summary>
        public static Px Framed(int w, int h, int pad = 4) => new Px(w + pad * 2, h + pad) { ox = pad };

        /// <summary>The pivot (0..1) of a Framed sheet whose pivot is the drawing's pixel (px, py).</summary>
        public static Vector2 FramedPivot(int w, int h, float px, float py, int pad = 4) => new Vector2((px + pad) / (w + pad * 2), py / (h + pad));

        public bool In(int x, int y) => x + ox >= 0 && y + oy >= 0 && x + ox < w && y + oy < h;

        public void Set(int x, int y, Color32 col)
        {
            if (In(x, y)) c[(y + oy) * w + x + ox] = col;
        }

        public Color32 Get(int x, int y) => In(x, y) ? c[(y + oy) * w + x + ox] : Clear;
        public bool Opaque(int x, int y) => In(x, y) && c[(y + oy) * w + x + ox].a > 0;

        public void Rect(int x, int y, int rw, int rh, Color32 col)
        {
            for (int yy = y; yy < y + rh; yy++)
                for (int xx = x; xx < x + rw; xx++)
                    Set(xx, yy, col);
        }

        public void Fill(Color32 col) { for (int i = 0; i < c.Length; i++) c[i] = col; }

        public void Line(int x0, int y0, int x1, int y1, Color32 col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Disc(float cx, float cy, float r, Color32 col)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) Set(x, y, col);
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 col)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, col);
                }
        }

        /// <summary>Adds a 1px outline around opaque pixels (4-neighbourhood).</summary>
        public void Outline(Color32 col)
        {
            var copy = (Color32[])c.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a > 0) continue;
                    bool n = (x > 0 && copy[y * w + x - 1].a > 0) || (x < w - 1 && copy[y * w + x + 1].a > 0) ||
                             (y > 0 && copy[(y - 1) * w + x].a > 0) || (y < h - 1 && copy[(y + 1) * w + x].a > 0);
                    if (n) c[y * w + x] = col;
                }
        }

        public void FlipX()
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w / 2; x++)
                {
                    var t = c[y * w + x];
                    c[y * w + x] = c[y * w + (w - 1 - x)];
                    c[y * w + (w - 1 - x)] = t;
                }
        }

        public Px Copy()
        {
            var p = new Px(w, h);
            System.Array.Copy(c, p.c, c.Length);
            return p;
        }

        public void Blit(Px src, int ox, int oy)
        {
            for (int y = 0; y < src.h; y++)
                for (int x = 0; x < src.w; x++)
                {
                    var s = src.c[y * src.w + x];
                    if (s.a > 0) Set(ox + x, oy + y, s);
                }
        }

        public void Save(string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(c);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Deterministic hash noise in [0,1).</summary>
        public static float N(int x, int y, int s)
        {
            unchecked
            {
                uint hsh = (uint)(x * 374761393 + y * 668265263 + s * 2147483647);
                hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                hsh ^= hsh >> 16;
                return (hsh & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
