using System.IO;
using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Shardlight Geode: the resonant crystal and its shards, the light lens, its three creatures (shardling and
    /// its young, glint), the Facet - a colossus on the back wall - its two fists and its volley, the mimic awake (MimicArt), and the two things a shop sells for this place
    /// (Dampener, Truth Lens). All creatures face right in the art.
    /// </summary>
    public static partial class ArtGenerator
    {
        static void GeodeArt()
        {
            Color32 ol = C(20, 18, 40);
            Color32 cry = C(110, 220, 235), cryD = C(52, 130, 170), cryL = C(220, 255, 255), cryDeep = C(34, 72, 120);
            Color32 vio = C(170, 120, 230), vioD = C(104, 64, 160), vioL = C(232, 200, 255);

            // one spire of crystal: a tall, pointed prism with a lit face and a dark one
            void Spire(Px p, int x, int base0, int h, int w, Color32 mid, Color32 dark, Color32 lit)
            {
                for (int y = 0; y < h; y++)
                {
                    int ww = y > h - 4 ? Mathf.Max(1, w - (y - (h - 4)) * (w > 2 ? 1 : 0) - (y == h - 1 ? 1 : 0)) : w;
                    int off = (w - ww) / 2;
                    p.Rect(x + off, base0 + y, ww, 1, mid);
                    p.Set(x + off, base0 + y, dark);
                    if (ww > 1) p.Set(x + off + ww - 1, base0 + y, lit);
                }
            }

            // resonant crystal: 0 whole, 1 ringing (lit from within), 2 the stump it leaves
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 20);
                Color32 mid = f == 1 ? cryL : cry, dark = f == 1 ? cry : cryD, lit = f == 1 ? C(255, 255, 255) : cryL;
                if (f < 2)
                {
                    Spire(p, 2, 0, 9, 3, mid, dark, lit);
                    Spire(p, 12, 0, 11, 4, mid, dark, lit);
                    Spire(p, 6, 0, 17, 5, mid, dark, lit);
                    p.Set(8, 12, lit); p.Set(8, 13, lit);
                    if (f == 1) { p.Set(1, 12, cryL); p.Set(16, 14, cryL); p.Set(4, 17, cryL); p.Set(13, 17, cryL); }      // it rings: light off its points
                }
                else
                {
                    Spire(p, 2, 0, 3, 3, cryD, cryDeep, cry);
                    Spire(p, 12, 0, 4, 4, cryD, cryDeep, cry);
                    Spire(p, 6, 0, 5, 5, cryD, cryDeep, cry);
                }
                p.Outline(ol);
                p.Rect(1, 0, 16, 1, cryDeep);
                Save(p, $"Sprites/Traps/crystal_{f}", new Vector2(0.5f, 0f));
            }

            // a flying shard (points right)
            {
                var p = new Px(9, 5);
                p.Rect(1, 2, 5, 1, cryL); p.Rect(2, 1, 3, 1, cry); p.Rect(2, 3, 3, 1, cryD); p.Set(6, 2, C(255, 255, 255)); p.Set(7, 2, cryL); p.Set(0, 2, cryD);
                p.Outline(ol);
                Save(p, "Sprites/Traps/crystalshard", PCenter);
            }

            // the light lens: a cut crystal held in a ring of dark stone under the ceiling
            {
                var p = new Px(16, 10);
                Color32 st = C(70, 66, 96), stD = C(44, 42, 66);
                p.Rect(1, 6, 14, 4, st); p.Rect(1, 6, 14, 1, stD); p.Rect(3, 4, 10, 2, st); p.Rect(3, 4, 1, 2, stD);
                p.Rect(5, 1, 6, 4, cry); p.Rect(6, 0, 4, 1, cryL); p.Rect(5, 1, 1, 4, cryD); p.Set(9, 2, C(255, 255, 255)); p.Set(8, 3, cryL);
                p.Outline(ol);
                Save(p, "Sprites/Traps/lens", new Vector2(0.5f, 0f));
            }

            // shardling: a low crab under a back of crystal points. 0 / 1 walk, 2 tucked for the rush
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(20, 18);
                Color32 shell = C(96, 84, 150), shellD = C(60, 50, 104), claw = C(150, 130, 200);
                int dip = f == 2 ? 1 : 0;
                p.Ellipse(9.5f, 4f - dip * 0.5f, 6.5f, 3.4f, shell); p.Ellipse(9.5f, 2.6f, 6f, 1.6f, shellD);
                for (int i = 0; i < 3; i++) { int x = 4 + i * 5; p.Set(x + (f == 1 ? 1 : 0), 0, shellD); p.Set(x, 1, shellD); }      // legs
                if (f != 2) { p.Rect(15, 3, 3, 2, claw); p.Set(18, 4, claw); p.Set(18, 2, claw); }                                   // claw forward
                else { p.Rect(14, 2, 3, 2, claw); }
                p.Set(14, 5, C(255, 220, 120));                                                                                       // eye
                Spire(p, 4, 6 - dip, 5, 2, cry, cryD, cryL);
                Spire(p, 8, 6 - dip, 8, 3, cry, cryD, cryL);
                Spire(p, 12, 6 - dip, 6, 2, cry, cryD, cryL);
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/shardling_{f}", new Vector2(0.5f, 1f / 18f));
            }
            // its young: the same, half the size, one point on its back
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(12, 12);
                Color32 shell = C(116, 100, 176), shellD = C(70, 58, 120);
                p.Ellipse(5.5f, 2.6f, 3.6f, 2.2f, shell); p.Ellipse(5.5f, 1.8f, 3.2f, 1f, shellD);
                p.Set(3 + (f == 1 ? 1 : 0), 0, shellD); p.Set(7, 0, shellD);
                p.Set(9, 3, C(150, 130, 200)); p.Set(8, 4, C(255, 220, 120));
                Spire(p, 4, 4, 5, 2, cry, cryD, cryL);
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/shardlet_{f}", new Vector2(0.5f, 1f / 12f));
            }

            // glint: a bat of living prism. 0 / 1 wings, 2 hanging folded
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                if (f == 2)
                {
                    p.Rect(7, 8, 4, 8, vio); p.Rect(7, 8, 1, 8, vioD); p.Rect(10, 8, 1, 8, vioL);
                    p.Rect(6, 10, 1, 5, vioD); p.Rect(11, 10, 1, 5, vioD);
                    p.Set(8, 9, C(255, 255, 255)); p.Set(9, 16, vioD); p.Set(8, 16, vioD);
                }
                else
                {
                    int wy = f == 0 ? 10 : 6;
                    p.Ellipse(9f, 8f, 2.6f, 3.2f, vio); p.Set(8, 9, C(255, 255, 255)); p.Set(10, 9, C(255, 255, 255));
                    p.Set(8, 12, vioD); p.Set(10, 12, vioD);                                                    // ears
                    for (int i = 0; i < 6; i++)
                    {
                        int y = wy + (f == 0 ? -i / 2 : i / 2);
                        p.Rect(11 + i, y, 1, 3 - i / 3, i % 2 == 0 ? vioL : vio);
                        p.Rect(6 - i, y, 1, 3 - i / 3, i % 2 == 0 ? vio : vioD);
                    }
                }
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/glint_{f}", new Vector2(0.5f, 1f / 18f));
            }

            // --- the Facet and its fists: stone cut in facets (every cell of a jittered grid is one flat face with a tone of its own)
            Color32 FacetStone(int x, int y, int G, int seed, Color32 dark, Color32 mid, Color32 lit, Color32 seam)
            {
                int gx = Mathf.FloorToInt((float)x / G), gy = Mathf.FloorToInt((float)y / G);
                float d1 = 1e9f, d2 = 1e9f;
                int bx = 0, by = 0;
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        int qx = gx + i, qy = gy + j;
                        float sx = (qx + 0.15f + 0.7f * N(qx + 100, qy + 100, seed)) * G, sy = (qy + 0.15f + 0.7f * N(qx + 100, qy + 100, seed + 1)) * G;
                        float d = (x - sx) * (x - sx) + (y - sy) * (y - sy);
                        if (d < d1) { d2 = d1; d1 = d; bx = qx; by = qy; } else if (d < d2) d2 = d;
                    }
                if (Mathf.Sqrt(d2) - Mathf.Sqrt(d1) < 1.1f) return seam;
                float t = N(bx + 100, by + 100, seed + 2);
                return t < 0.3f ? dark : t < 0.72f ? mid : lit;
            }
            // a prism of crystal with a point: dark face, lit face, a dark edge on its left
            void Shard(Px p, float cx, int base0, int h, float w, float lean, Color32 dark, Color32 mid, Color32 lit, Color32 edge)
            {
                for (int y = 0; y < h; y++)
                {
                    float t = (float)y / h, half = Mathf.Max(0.5f, w * 0.5f * Mathf.Min(1f, (1f - t) / 0.32f)), xc = cx + lean * y;
                    int xa = Mathf.RoundToInt(xc - half), xb = Mathf.RoundToInt(xc + half);
                    for (int x = xa; x <= xb; x++)
                        p.Set(x, base0 + y, x == xa ? edge : x < xc - half * 0.2f ? dark : x < xc + half * 0.45f ? mid : lit);
                }
            }
            // a cut stone standing on its point
            void Gem(Px p, int cx, int cy, int hw0, int hh, Color32 dark, Color32 mid, Color32 lit, Color32 edge)
            {
                for (int dy = -hh; dy <= hh; dy++)
                {
                    int hw = Mathf.RoundToInt(hw0 * (1f - Mathf.Abs(dy) / (hh + 0.5f)));
                    for (int dx = -hw; dx <= hw; dx++)
                        p.Set(cx + dx, cy + dy, Mathf.Abs(dx) == hw ? edge : dx * 3 < -hw ? dark : dx * 3 <= hw ? mid : lit);
                }
            }

            // the Facet: a colossus. Its head and shoulders fill the back wall of its hall from floor to ceiling
            // (30 tiles by 9; the rest of it is below). 0 watching, 1 striking (eyes ablaze, jaws apart), 2 staggered (eyes dim)
            for (int f = 0; f < 3; f++)
            {
                const int BW = 480, BH = 144;
                var p = new Px(BW, BH);
                Color32 sD = C(40, 38, 70), sM = C(56, 54, 92), sL = C(74, 72, 116), seam = C(28, 27, 52), rim = C(116, 116, 166), pit = C(14, 12, 28), white = C(255, 255, 255);
                Color32 hD = C(52, 50, 90), hM = C(70, 68, 114), hL = C(94, 92, 144);
                Color32 pl = C(70, 150, 184), plD = C(40, 92, 134), plL = C(140, 214, 232), plE = C(24, 52, 88);
                Color32 core = C(255, 190, 110), coreL = C(255, 240, 200);
                // its outline: two great shoulders, the slope of the neck between them, and the head
                float Top(int x)
                {
                    float l = 84f * Mathf.Exp(-Mathf.Pow((x - 108) / 76f, 2f)), r = 84f * Mathf.Exp(-Mathf.Pow((x - 372) / 76f, 2f));
                    float m = Mathf.Abs(x - 240) < 150 ? 66f - Mathf.Abs(x - 240) * 0.22f : 0f;
                    return Mathf.Max(l, Mathf.Max(r, m)) * Mathf.Clamp01(Mathf.Min(x, BW - 1 - x) / 28f);      // (down to nothing at both ends: no cut edge where the hall is wider)
                }
                float HeadHalf(int y) => y < 46 || y > 126 ? -1f : y < 70 ? Mathf.Lerp(34f, 52f, (y - 46) / 24f) : y < 104 ? Mathf.Lerp(52f, 58f, (y - 70) / 34f) : Mathf.Lerp(58f, 42f, (y - 104) / 22f);
                for (int y = 0; y < BH; y++)
                    for (int x = 0; x < BW; x++)
                    {
                        bool hd = Mathf.Abs(x - 240) <= HeadHalf(y), bd = y < Top(x);
                        if (!hd && !bd) continue;
                        Color32 c0 = hd ? FacetStone(x, y, 13, 211, hD, hM, hL, seam) : FacetStone(x, y, 17, 97, sD, sM, sL, seam);
                        float lum = hd ? 0.84f + 0.24f * (y - 46) / 80f : 0.68f + 0.42f * y / 84f;
                        if (!hd && y > 14) lum *= Mathf.Lerp(0.66f, 1f, Mathf.Clamp01((Mathf.Abs(x - 240) - 40f) / 50f));                         // the shadow of its jaw on its chest
                        p.Set(x, y, Mul(c0, lum));
                    }
                // light along every upper edge
                for (int x = 0; x < BW; x++)
                    for (int y = 1; y < BH; y++)
                        if (p.Opaque(x, y) && !p.Opaque(x, y + 1)) { p.Set(x, y, rim); p.Set(x, y - 1, Mul(rim, 0.8f)); }
                // brow: a heavy ridge, low in the middle, and its shadow
                for (int x = 184; x <= 296; x++)
                {
                    int by = Mathf.RoundToInt(97f + Mathf.Abs(x - 240) * 0.13f);
                    for (int y = by; y < by + 7; y++) p.Set(x, y, y == by + 6 ? rim : Mul(p.Get(x, y), 1.22f));
                    for (int y = by - 4; y < by; y++) p.Set(x, y, Mul(p.Get(x, y), 0.5f));
                }
                // eyes: two slits, slanted down towards the middle
                Color32 eye = f == 1 ? C(190, 250, 255) : f == 2 ? C(44, 70, 104) : C(96, 214, 238);
                int glow = f == 1 ? 3 : f == 2 ? 0 : 2;
                foreach (int side in new[] { -1, 1 })
                    for (int dx = -14; dx <= 14; dx++)
                    {
                        int x = 240 + side * 30 + dx, yc = Mathf.RoundToInt(86f + side * dx * 0.2f);
                        int hh = Mathf.Abs(dx) > 11 ? 2 : 4;
                        for (int y = yc - hh; y <= yc + hh; y++) p.Set(x, y, pit);
                        if (Mathf.Abs(dx) > 9) continue;
                        for (int y = yc - glow; y <= yc + glow; y++)
                            p.Set(x, y, f != 2 && Mathf.Abs(dx) <= 4 && Mathf.Abs(y - yc) < glow ? (f == 1 ? white : C(196, 246, 255)) : eye);
                    }
                // mouth: a jagged crack (light in it when it strikes), veins of crystal down its cheeks
                for (int x = 210; x <= 270; x++)
                {
                    int my = 60 + Mathf.Abs((x - 210) % 12 - 6) - 3, th = f == 1 ? 5 : 2;
                    for (int y = my - th; y <= my + 1; y++) p.Set(x, y, pit);
                    if (f == 1) { p.Set(x, my - 2, pl); p.Set(x, my - 3, plD); }
                }
                p.Line(212, 78, 206, 64, plD); p.Line(268, 78, 274, 64, plD);
                // the stone in its forehead (the picture hangs from here)
                p.Disc(240f, 117f, 10.5f, Mul(hD, 0.55f));
                if (f == 2) Gem(p, 240, 117, 7, 9, core, core, coreL, plE); else Gem(p, 240, 117, 7, 9, plD, pl, plL, plE);
                p.Set(239, 120, white);
                // crystal: a crown, a cluster on each shoulder, a heart in its chest
                int[] crownX = { -36, -22, -8, 7, 21, 35 }, crownH = { 10, 15, 12, 17, 13, 9 };
                for (int i = 0; i < crownX.Length; i++) Shard(p, 240 + crownX[i], 122, crownH[i], 7f, crownX[i] * 0.004f, plD, pl, plL, plE);
                foreach (int side in new[] { -1, 1 })
                {
                    int at = 240 + side * 132;
                    Shard(p, at + side * 34, 56, 16, 7f, side * 0.3f, plD, pl, plL, plE);
                    Shard(p, at - side * 14, 70, 22, 9f, -side * 0.08f, plD, pl, plL, plE);
                    Shard(p, at + side * 16, 68, 24, 10f, side * 0.22f, plD, pl, plL, plE);
                    Shard(p, at, 74, 34, 13f, side * 0.1f, plD, pl, plL, plE);
                    Shard(p, 240 + side * 196, 30, 14, 6f, side * 0.25f, plD, pl, plL, plE);
                    Shard(p, 240 + side * 84, 44, 12, 6f, -side * 0.1f, plD, pl, plL, plE);
                }
                p.Disc(240f, 22f, 19f, seam); p.Disc(240f, 22f, 16f, pit);
                p.Line(221, 22, 202, 30, plD); p.Line(259, 22, 278, 30, plD);
                Gem(p, 240, 22, 9, 13, plD, pl, plL, plE);
                p.Set(238, 27, white);
                p.Outline(ol);
                Save(p, $"Sprites/Bosses/facet_{f}", new Vector2(0.5f, 112f / BH));
            }

            // one of its fists: the same cut stone, crystal over the knuckles. 0 whole, 1 broken open (the core showing)
            for (int f = 0; f < 2; f++)
            {
                // (3.5 tiles by 2.9: a fist to go with a body that fills the wall)
                var p = new Px(56, 46);
                Color32 fD = C(64, 60, 104), fM = C(90, 86, 136), fL = C(120, 114, 168), fSeam = C(44, 42, 78), core = C(255, 190, 110), coreL = C(255, 240, 200);
                for (int y = 1; y <= 33; y++)
                    for (int x = 4; x <= 51; x++)
                    {
                        if (Mathf.Min(x - 4, 51 - x) + Mathf.Min(y - 1, 33 - y) < 3) continue;      // corners knocked off
                        p.Set(x, y, Mul(FacetStone(x, y, 9, 53, fD, fM, fL, fSeam), 0.8f + 0.35f * y / 33f));
                    }
                p.Rect(1, 10, 4, 14, Mul(fM, 0.8f)); p.Rect(1, 10, 1, 14, fSeam);                          // thumb
                foreach (int x in new[] { 16, 28, 40 }) p.Rect(x, 1, 1, 15, fSeam);                        // fingers
                if (f == 0)
                {
                    p.Rect(7, 22, 42, 9, cry); p.Rect(7, 28, 42, 3, cryL); p.Rect(7, 22, 42, 1, cryD);
                    foreach (int x in new[] { 17, 28, 39 }) p.Rect(x, 22, 1, 9, cryD);
                    for (int i = 0; i < 4; i++) Shard(p, 12f + i * 10.7f, 32, 10 + (i % 2) * 3, 7f, 0f, cryD, cry, cryL, cryDeep);
                }
                else
                {
                    p.Rect(7, 22, 6, 9, cryD); p.Rect(43, 22, 6, 9, cryD);
                    for (int i = 0; i < 4; i++) Shard(p, 12f + i * 10.7f, 32, 3, 7f, 0f, cryDeep, cryD, cry, cryDeep);
                    p.Disc(28f, 18f, 10.5f, C(20, 18, 40));
                    Gem(p, 28, 18, 7, 9, core, coreL, C(255, 255, 255), C(200, 120, 60));
                    p.Line(28, 33, 22, 28, ol); p.Line(38, 12, 44, 6, ol); p.Line(18, 12, 12, 7, ol);
                }
                p.Outline(ol);
                Save(p, $"Sprites/Bosses/facetfist_{f}", new Vector2(0.5f, 1f / 46f));
            }

            // Dampener: a lump of dark felt bound in copper, a tuning fork through it
            {
                var p = ItemCanvas();
                Color32 felt = C(60, 56, 84), feltD = C(38, 36, 58), cop = C(214, 140, 80), copL = C(250, 200, 140), steel = C(200, 206, 220);
                p.Ellipse(8f, 6f, 5.2f, 4.6f, felt); p.Ellipse(8f, 4.2f, 4.8f, 2.2f, feltD);
                p.Rect(3, 6, 10, 1, cop); p.Set(4, 6, copL); p.Set(11, 6, copL);
                p.Rect(7, 9, 1, 5, steel); p.Rect(9, 9, 1, 5, steel); p.Rect(7, 9, 3, 1, steel); p.Rect(8, 7, 1, 2, steel);
                p.Outline(ol);
                Save(p, "Sprites/Items/dampener", PItem);
            }
            // Truth Lens: a round glass in a brass ring, on a short handle
            {
                var p = ItemCanvas();
                Color32 brass = C(214, 170, 80), brassD = C(140, 104, 44), glass = C(150, 220, 235), glassL = C(230, 255, 255);
                p.Disc(9f, 9f, 5.2f, brass); p.Disc(9f, 9f, 3.8f, glass); p.Set(8, 11, glassL); p.Set(7, 10, glassL); p.Set(11, 7, C(255, 80, 80));
                p.Line(5, 5, 2, 2, brassD); p.Line(4, 5, 1, 2, brass);
                p.Outline(ol);
                Save(p, "Sprites/Items/truthlens", PItem);
            }
        }
    

        /// <summary>Everything a mimic can pose as: a short name (prefab "mimic_<name>") and the picture of the real thing.</summary>
        public static readonly (string key, string sprite)[] MimicDisguises =
        {
            ("gem", "Sprites/Items/gem"), ("pot", "Sprites/Items/pot"), ("crate", "Sprites/Items/crate"),
        };

        /// <summary>
        /// The mimic awake, once for every disguise: the picture of the real thing (read back from the file just
        /// written, so this runs after the items and the props) split across the middle into two jaws, with teeth,
        /// two red eyes and feet. 0 / 1: the mouth less and more open.
        /// </summary>
        static void MimicArt()
        {
            Color32 mouth = C(90, 20, 40), tooth = C(250, 250, 235), eye = C(255, 60, 60), foot = C(34, 26, 44);
            foreach (var kv in MimicDisguises)
            {
                string file = Path.Combine(Directory.GetCurrentDirectory(), Root + kv.sprite + ".png");
                if (!File.Exists(file)) { Debug.LogWarning("[Art] mimic: no picture " + file); continue; }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(file));
                int w = tex.width, h = tex.height;
                var src = tex.GetPixels32();
                UnityEngine.Object.DestroyImmediate(tex);
                int x0 = w, x1 = -1, y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (src[y * w + x].a > 0) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                if (y1 < 0) continue;
                int split = y0 + Mathf.Max(2, (y1 - y0 + 1) * 45 / 100), mid = (x0 + x1) / 2;
                for (int f = 0; f < 2; f++)
                {
                    int open = f == 0 ? 3 : 5;
                    var p = new Px(w + 2, h + open + 2);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (src[y * w + x].a > 0) p.Set(x + 1, y + 1 + (y >= split ? open : 0), src[y * w + x]);
                    for (int x = x0; x <= x1; x++)
                    {
                        if (src[(split - 1) * w + x].a == 0 || split > y1 || src[split * w + x].a == 0) continue;
                        for (int g = 0; g < open; g++) p.Set(x + 1, split + 1 + g, mouth);
                        if ((x - x0) % 2 == 0) p.Set(x + 1, split + 1, tooth); else p.Set(x + 1, split + open, tooth);
                    }
                    int ey = Mathf.Min(y1 - 1, split + 2) + 1 + open;
                    foreach (int ex in new[] { mid - 2, mid + 3 }) if (p.Opaque(ex + 1, ey)) p.Set(ex + 1, ey, eye);
                    p.Set(x0 + 2, y0, foot); p.Set(x0 + 3, y0, foot); p.Set(x1 - 1, y0, foot); p.Set(x1, y0, foot);
                    Save(p, $"Sprites/Mimic/{kv.key}_{f}", new Vector2(0.5f, (float)y0 / p.h));
                }
            }
        }
    }
}
