using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The bosses that used to be their area's creature at double size, drawn for themselves: the Forge Brute,
    /// the Yeti King, the Storm Hawk, the Rot Heart, the Foreman and the Hollow Warden. (The Broodmother is in
    /// ArtGenerator.cs, the Dune Queen with her area, the Facet with the Geode.) All face right; flat tones,
    /// lit from the upper right, one dark pixel of outline.
    /// </summary>
    public static partial class ArtGenerator
    {
        /// <summary>
        /// The tone of a point on a rounded form (nx, ny: -1..1 across it), lit from the upper right as a ball is:
        /// most of it the middle tone, a crescent of shadow hugging the lower left edge, the light on the upper
        /// right and one small bright spot in it. (Not bands straight across: those read as stripes.)
        /// </summary>
        static Color32 Round(float nx, float ny, Color32 d, Color32 m, Color32 l, Color32 h)
        {
            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
            float lam = nx * 0.42f + ny * 0.56f + nz * 0.71f;
            // (the deepest of the shadow a shade darker still, the bright spot a shade brighter than the light)
            return lam > 0.955f ? Lerp(h, C(255, 255, 255), 0.3f) : lam > 0.82f ? l : lam > 0.34f ? m : lam > 0.08f ? d : Mul(d, 0.78f);
        }

        /// <summary>A round shape in four flat tones, lit from the upper right (see Round).</summary>
        static void Ball(Px p, float cx, float cy, float rx, float ry, Color32 d, Color32 m, Color32 l, Color32 h)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float nx = (x - cx) / rx, ny = (y - cy) / ry;
                    if (nx * nx + ny * ny > 1f) continue;
                    p.Set(x, y, Round(nx, ny, d, m, l, h));
                }
        }

        /// <summary>A line with some thickness (r 0 = one pixel).</summary>
        static void Bar(Px p, int x0, int y0, int x1, int y1, int r, Color32 c)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r + 1) p.Line(x0 + dx, y0 + dy, x1 + dx, y1 + dy, c);
        }

        /// <summary>A filled triangle.</summary>
        static void Tri(Px p, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 0.001f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float w0 = ((b.x - x) * (c.y - y) - (b.y - y) * (c.x - x)) / area;
                    float w1 = ((c.x - x) * (a.y - y) - (c.y - y) * (a.x - x)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= -0.01f && w1 >= -0.01f && w2 >= -0.01f) p.Set(x, y, col);
                }
        }

        /// <summary>A plate of metal: lit along its top and right edge, dark along the bottom and left, a rivet in each corner.</summary>
        static void Plate(Px p, int x, int y, int w, int h, Color32 d, Color32 m, Color32 l, bool rivets = true)
        {
            p.Rect(x, y, w, h, m);
            p.Rect(x, y, w, 1, d); p.Rect(x, y, 1, h, d);
            p.Rect(x, y + h - 1, w, 1, l); p.Rect(x + w - 1, y, 1, h, l);
            if (!rivets || w < 7 || h < 7) return;
            p.Set(x + 2, y + 2, d); p.Set(x + w - 3, y + 2, d); p.Set(x + 2, y + h - 3, d); p.Set(x + w - 3, y + h - 3, d);
        }

        /// <summary>
        /// The flat shading of the small creatures, for a shape drawn in one tone 'm': a rim of 'd' where the shape
        /// ends within 'under' pixels below or 'side' pixels to the left, a rim of 'l' where open air is within
        /// 'top' pixels above. Do it before anything else is drawn over the shape.
        /// </summary>
        static void Shade(Px p, Color32 m, Color32 d, Color32 l, int under = 4, int side = 2, int top = 2)
        {
            bool Is(int x, int y) { var c = p.Get(x, y); return c.a > 0 && c.r == m.r && c.g == m.g && c.b == m.b; }
            var dark = new System.Collections.Generic.List<Vector2Int>(); var lit = new System.Collections.Generic.List<Vector2Int>();
            for (int y = -16; y < 160; y++)
                for (int x = -16; x < 160; x++)
                {
                    if (!Is(x, y)) continue;
                    bool sh = false, hi = false;
                    for (int k = 1; k <= under && !sh; k++) sh = !Is(x, y - k);
                    for (int k = 1; k <= side && !sh; k++) sh = !Is(x - k, y);
                    for (int k = 1; k <= top && !hi; k++) hi = !p.Opaque(x, y + k);
                    if (sh) dark.Add(new Vector2Int(x, y)); else if (hi) lit.Add(new Vector2Int(x, y));
                }
            foreach (var v in dark) p.Set(v.x, v.y, d);
            foreach (var v in lit) p.Set(v.x, v.y, l);
        }

        static void OldBossesArt()
        {
            ForgeBruteArt();
            YetiKingArt();
            StormHawkArt();
            RotHeartArt();
            ForemanArt();
            WardenArt();
        }

        /// <summary>
        /// The Forge Brute: a bull of basalt with a furnace in its belly, lava in every crack, horns of pale stone.
        /// 0 / 1 walking, 2 charging (head down, horns first), 3 dazed (stars about its head).
        /// </summary>
        static void ForgeBruteArt()
        {
            Color32 ol = C(30, 12, 14), dD = C(58, 28, 32), d = C(92, 44, 42), m = C(132, 62, 52), l = C(176, 92, 66);
            Color32 lava = C(255, 150, 50), lavaL = C(255, 228, 124), horn = C(236, 220, 188), hornD = C(176, 148, 118);
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(86, 56);      // (room to the right for the horns when its head goes forward)
                bool charge = f == 2, dazed = f == 3;
                int step = f == 1 ? 1 : 0;
                int hx = charge ? 3 : 0, hy = charge ? -7 : dazed ? -3 : 0;      // where the head is
                // Drawn the way the small creatures are: one shape in one tone, a rim of shadow under it, a rim of
                // light along its top, and a few large marks. Nothing a pixel wide but the outline.
                // Its shape is a bison's: a great hump over the shoulders falling away to small hindquarters, the
                // head slung low in front of it, horns pointing where it means to go, short thick legs.
                int nf = 47 + (step == 1 ? 2 : -2) + (charge ? 5 : 0), nh = 9 + (step == 1 ? -2 : 3) - (charge ? 5 : 0);
                // far legs, and the horn of its far side behind the head
                p.Rect(16 + (step == 1 ? 3 : -1) - (charge ? 4 : 0), 1, 6, 14, dD);
                p.Rect(51 + (step == 1 ? -2 : 2) + (charge ? 4 : 0), 1, 7, 16, dD);
                Bar(p, 62 + hx, 34 + hy, 67 + hx, 40 + hy, 1, hornD); Bar(p, 67 + hx, 40 + hy, 73 + hx, 40 + hy, 1, hornD); p.Line(73 + hx, 41 + hy, 76 + hx, 43 + hy, hornD);
                // hindquarters, barrel, hump, neck, head and muzzle, the near legs
                p.Ellipse(17f, 23f, 11f, 10f, m); p.Ellipse(31f, 23f, 19f, 11f, m); p.Ellipse(45f, 30f, 15f, 16f, m);
                p.Ellipse(54f + hx / 2, 27f + hy / 2, 10f, 11f, m);
                p.Ellipse(63f + hx, 25f + hy, 9f, 9f, m);
                p.Rect(64 + hx, 15 + hy, 11, 11, m);
                p.Ellipse(nh + 4f, 18f, 6f, 7f, m); p.Rect(nh, 1, 7, 17, m);
                p.Rect(nf, 1, 9, 20, m);
                Shade(p, m, d, l);
                // the shoulder: a crescent of shadow where the hump stands out from the barrel behind it
                for (int y = 15; y <= 37; y++)
                    for (int x = 28; x <= 40; x++)
                    {
                        float ny = (y - 30f) / 16f, a = (x - 45f) / 15f, b2 = (x - 48f) / 15f;
                        var c = p.Get(x, y);
                        if (a * a + ny * ny <= 1f && b2 * b2 + ny * ny > 1f && c.r == m.r && c.g == m.g && c.b == m.b) p.Set(x, y, d);
                    }
                p.Rect(nh, 1, 7, 3, dD); p.Rect(nf, 1, 9, 3, dD);      // hooves
                // a mane of basalt slabs on the hump
                Tri(p, new Vector2(33, 39), new Vector2(41, 43), new Vector2(35, 48), dD);
                Tri(p, new Vector2(40, 43), new Vector2(50, 43), new Vector2(44, 52), dD);
                Tri(p, new Vector2(49, 43), new Vector2(57, 38), new Vector2(54, 48), dD);
                // the furnace in its belly, and two broad cracks running up from it
                p.Ellipse(30f, 12.5f, 11f, 3.5f, lava); p.Ellipse(30f, 12f, 7f, 2f, lavaL);
                for (int k = 0; k < 2; k++)
                {
                    p.Line(22 + k, 16, 20 + k, 23, lava); p.Line(20 + k, 23, 14 + k, 27, lava);
                    p.Line(37 + k, 16, 39 + k, 24, lava);
                }
                // tail: a short lash with a coal at its end
                p.Line(6, 26, 2, 31, d); p.Line(2, 31, 3, 36, d); p.Disc(3.5f, 37.5f, 1.6f, lava);
                // a heavy jaw, a nostril breathing fire
                p.Rect(64 + hx, 14 + hy, 11, 3, dD);
                p.Rect(72 + hx, 20 + hy, 2, 2, charge ? lavaL : lava);
                // the horn of its near side: up out of the skull, then straight forward to its point
                Bar(p, 59 + hx, 32 + hy, 64 + hx, 38 + hy, 1, horn); Bar(p, 64 + hx, 38 + hy, 70 + hx, 38 + hy, 1, horn); p.Line(70 + hx, 39 + hy, 73 + hx, 41 + hy, horn);
                p.Line(64 + hx, 37 + hy, 70 + hx, 37 + hy, hornD);
                p.Outline(ol);
                // the eye (after the outline: it glows), a heavy brow over it
                if (dazed) { p.Line(64 + hx, 27 + hy, 67 + hx, 24 + hy, ol); p.Line(64 + hx, 24 + hy, 67 + hx, 27 + hy, ol); }
                else { p.Rect(64 + hx, 25 + hy, 3, 2, charge ? lavaL : lava); p.Line(62 + hx, 29 + hy, 68 + hx, 27 + hy, ol); p.Line(62 + hx, 30 + hy, 68 + hx, 28 + hy, ol); }
                if (dazed)
                {
                    Color32 star = C(255, 226, 110);
                    foreach (var v in new[] { new Vector2Int(58, 46), new Vector2Int(67, 51), new Vector2Int(76, 46) })
                    { p.Set(v.x, v.y + 1, star); p.Set(v.x - 1, v.y, star); p.Set(v.x + 1, v.y, star); p.Set(v.x, v.y - 1, star); }
                }
                Save(p, $"Sprites/Bosses/forgebrute_{f}", new Vector2(39f / 86f, 1f / 56f));      // (its middle where it was)
            }
        }

        /// <summary>
        /// The Yeti King: a mountain of white fur with arms down to its knees, a blue face with tusks, horns of ice
        /// and a crown. 0 standing, 1 its arm raised to throw.
        /// </summary>
        static void YetiKingArt()
        {
            Color32 ol = C(38, 52, 92), dD = C(120, 150, 198), d = C(172, 198, 232), m = C(216, 232, 249), l = C(248, 252, 255);
            Color32 skin = C(96, 126, 184), skinD = C(60, 84, 138), skinL = C(140, 170, 220), ice = C(170, 230, 250), iceL = C(236, 252, 255);
            Color32 gold = C(250, 204, 70), goldD = C(190, 136, 36), gem = C(220, 60, 80), tusk = C(250, 248, 236);
            for (int f = 0; f < 2; f++)
            {
                var p = Framed(62, 68);
                // (flat, as the small creatures are: one tone, a rim of shadow under each shape, a few large marks)
                Color32 am = C(216, 232, 248);      // the near arm: the fur's tone, but a shape of its own to shade
                // far arm
                // (as long and as thick as the near one, with a hand of its own: it must not look like a stump)
                Bar(p, 11, 40, 4, 27, 4, d); Bar(p, 4, 27, 6, 15, 4, d); p.Disc(6f, 11f, 6f, skinD);
                p.Rect(2, 5, 2, 3, Mul(skinD, 0.7f)); p.Rect(5, 4, 2, 3, Mul(skinD, 0.7f)); p.Rect(8, 5, 2, 3, Mul(skinD, 0.7f));
                // legs and feet
                p.Rect(15, 4, 11, 10, d); p.Rect(33, 4, 11, 10, d);
                p.Rect(13, 1, 15, 4, skin); p.Rect(31, 1, 16, 4, skin); p.Rect(13, 1, 15, 1, skinD); p.Rect(31, 1, 16, 1, skinD);
                // an ape's body: shoulders wider than its belly, the head sunk between them
                p.Ellipse(29f, 22f, 19f, 18f, m); p.Ellipse(29f, 38f, 23f, 15f, m);
                p.Ellipse(33f, 50f, 12f, 11f, m);
                Shade(p, m, d, l, 5, 3, 2);
                // three locks of fur down its chest
                foreach (var v in new[] { new Vector2Int(17, 33), new Vector2Int(30, 27), new Vector2Int(22, 18) })
                    for (int k = 0; k < 2; k++) { p.Line(v.x, v.y - k, v.x + 3, v.y - 4 - k, d); p.Line(v.x + 6, v.y - k, v.x + 3, v.y - 4 - k, d); }
                // the bare blue face
                p.Ellipse(37f, 49f, 8f, 7f, skin);
                p.Rect(34, 43, 9, 3, skinD);                                               // mouth
                p.Rect(35, 44, 2, 4, tusk); p.Rect(40, 44, 2, 4, tusk);                    // tusks
                // horns of ice, and the crown between them
                Bar(p, 24, 57, 20, 63, 1, ice); p.Set(19, 65, iceL); p.Set(20, 64, iceL);
                Bar(p, 43, 58, 48, 63, 1, ice); p.Set(49, 65, iceL); p.Set(49, 64, iceL);
                p.Rect(27, 60, 14, 3, gold); p.Rect(27, 60, 14, 1, goldD);
                p.Rect(27, 63, 2, 3, gold); p.Rect(33, 63, 2, 4, gold); p.Rect(39, 63, 2, 3, gold);
                p.Rect(33, 61, 2, 1, gem);
                // near arm: hanging, or raised with the hand open
                if (f == 0) { Bar(p, 47, 40, 54, 27, 4, am); Bar(p, 54, 27, 52, 15, 4, am); Shade(p, am, d, l, 3, 2, 2); p.Disc(52f, 11f, 6f, skin); p.Rect(48, 5, 2, 3, skinD); p.Rect(51, 4, 2, 3, skinD); p.Rect(54, 5, 2, 3, skinD); }
                else { Bar(p, 47, 42, 55, 50, 4, am); Bar(p, 55, 50, 54, 59, 4, am); Shade(p, am, d, l, 3, 2, 2); p.Disc(54f, 62f, 6f, skin); p.Rect(50, 66, 2, 3, skin); p.Rect(53, 67, 2, 2, skin); p.Rect(56, 66, 2, 3, skin); }
                p.Outline(ol);
                // eyes: cold light under a heavy brow
                p.Rect(35, 50, 2, 2, iceL); p.Rect(40, 50, 2, 2, iceL); p.Set(36, 50, C(40, 120, 220)); p.Set(41, 50, C(40, 120, 220));
                p.Line(33, 54, 37, 52, ol); p.Line(39, 52, 43, 54, ol);
                Save(p, $"Sprites/Bosses/yetiking_{f}", FramedPivot(62, 68, 31, 1));
            }
            {
                // 2: down on his knuckles, head low and forward - about to throw himself on his belly
                Color32 am = C(216, 232, 248);
                var p = Framed(62, 68);
                Bar(p, 14, 30, 9, 12, 4, d); p.Disc(9f, 7f, 6f, skinD);
                p.Rect(15, 4, 11, 8, d); p.Rect(33, 4, 11, 8, d);
                p.Rect(13, 1, 15, 4, skin); p.Rect(31, 1, 16, 4, skin); p.Rect(13, 1, 15, 1, skinD); p.Rect(31, 1, 16, 1, skinD);
                p.Ellipse(30f, 19f, 20f, 15f, m); p.Ellipse(33f, 30f, 22f, 12f, m);
                p.Ellipse(42f, 38f, 12f, 11f, m);
                Shade(p, m, d, l, 5, 3, 2);
                p.Ellipse(47f, 37f, 8f, 7f, skin);
                p.Rect(44, 31, 9, 3, skinD);
                p.Rect(45, 32, 2, 4, tusk); p.Rect(50, 32, 2, 4, tusk);
                Bar(p, 33, 45, 29, 51, 1, ice); p.Set(28, 53, iceL); p.Set(29, 52, iceL);
                Bar(p, 52, 46, 57, 51, 1, ice); p.Set(58, 53, iceL); p.Set(58, 52, iceL);
                p.Rect(36, 48, 14, 3, gold); p.Rect(36, 48, 14, 1, goldD);
                p.Rect(36, 51, 2, 3, gold); p.Rect(42, 51, 2, 4, gold); p.Rect(48, 51, 2, 3, gold);
                p.Rect(42, 49, 2, 1, gem);
                Bar(p, 46, 30, 52, 13, 4, am); Shade(p, am, d, l, 3, 2, 2);
                p.Disc(53f, 7f, 6f, skin); p.Rect(57, 3, 3, 2, skinD); p.Rect(58, 7, 3, 2, skinD);
                p.Outline(ol);
                p.Rect(45, 38, 2, 2, iceL); p.Rect(50, 38, 2, 2, iceL);
                p.Line(43, 42, 47, 40, ol); p.Line(49, 40, 53, 42, ol);
                Save(p, "Sprites/Bosses/yetiking_2", FramedPivot(62, 68, 31, 1));
            }
            {
                // 3: on his belly, sliding: arm out in front, feet up behind, snow thrown up under him
                Color32 am = C(216, 232, 248);
                var p = Framed(62, 68);
                Bar(p, 13, 15, 5, 14, 3, d); p.Rect(0, 10, 5, 8, skin); p.Rect(0, 10, 1, 8, skinD);
                p.Ellipse(26f, 13f, 22f, 12f, m);
                p.Ellipse(46f, 17f, 11f, 10f, m);
                Shade(p, m, d, l, 4, 3, 2);
                p.Ellipse(51f, 16f, 7f, 6f, skin);
                p.Rect(50, 10, 7, 3, skinD);
                p.Rect(51, 11, 2, 3, tusk); p.Rect(55, 11, 2, 3, tusk);
                Bar(p, 38, 24, 34, 29, 1, ice); p.Set(33, 31, iceL); p.Set(34, 30, iceL);
                Bar(p, 54, 24, 58, 29, 1, ice); p.Set(59, 31, iceL); p.Set(59, 30, iceL);
                p.Rect(40, 26, 13, 3, gold); p.Rect(40, 26, 13, 1, goldD);
                p.Rect(40, 29, 2, 3, gold); p.Rect(46, 29, 2, 4, gold); p.Rect(51, 29, 2, 3, gold);
                p.Rect(46, 27, 2, 1, gem);
                Bar(p, 36, 7, 54, 5, 3, am); Shade(p, am, d, l, 2, 0, 2);
                p.Disc(58f, 5f, 4.5f, skin);
                p.Rect(2, 2, 3, 2, l); p.Rect(8, 4, 2, 2, l); p.Rect(-2, 5, 2, 2, l);
                p.Outline(ol);
                p.Rect(49, 18, 2, 2, iceL); p.Rect(54, 18, 2, 2, iceL);
                p.Line(47, 22, 51, 20, ol); p.Line(53, 20, 57, 22, ol);
                Save(p, "Sprites/Bosses/yetiking_3", FramedPivot(62, 68, 31, 1));
            }
        }

        /// <summary>
        /// The Storm Hawk: a great grey-blue raptor with a white head, wings as long as it is, lightning in its
        /// feathers. 0 wings up, 1 wings down, 2 folded for the dive (pointing down; turned by the game).
        /// </summary>
        static void StormHawkArt()
        {
            Color32 ol = C(20, 24, 48), dD = C(44, 52, 92), d = C(68, 84, 136), m = C(102, 126, 184), l = C(156, 182, 230);
            Color32 w = C(238, 244, 255), wD = C(186, 200, 232), beak = C(250, 200, 70), beakD = C(196, 140, 40), bolt = C(255, 240, 130);
            for (int f = 0; f < 2; f++)
            {
                var p = Framed(76, 46);
                int by = 20;                                    // the body's height
                bool up = f == 0;
                // far wing (dark, behind the body)
                if (up) Tri(p, new Vector2(48, by + 3), new Vector2(34, by + 2), new Vector2(46, 45), dD);
                else Tri(p, new Vector2(48, by - 2), new Vector2(34, by - 2), new Vector2(44, 1), dD);
                // (flat, as the small creatures are: one tone to a shape, a rim of shadow under it, a few large marks)
                // tail: a fan of three long feathers
                Bar(p, 26, by, 8, by - 5, 1, d); Bar(p, 26, by, 6, by, 1, m); Bar(p, 26, by, 8, by + 5, 1, d);
                // body and the white head
                p.Ellipse(38f, by, 15f, 8f, m);
                Shade(p, m, d, l, 3, 2, 0);
                p.Ellipse(47f, by + 1f, 4f, 6f, wD);            // ruff
                p.Disc(55f, by + 3f, 7f, w);
                Shade(p, w, wD, w, 3, 0, 0);
                // the beak: heavy and hooked
                p.Rect(61, by + 2, 6, 3, beak); p.Rect(61, by + 2, 6, 1, beakD); p.Rect(66, by, 2, 3, beak);
                // talons
                foreach (int tx in new[] { 36, 42 }) { p.Rect(tx, by - 11, 2, 4, beak); p.Rect(tx - 1, by - 13, 4, 2, beakD); }
                // near wing: one broad blade, light along its leading edge, white at the tips of its primaries,
                // a streak of lightning in it
                Vector2 root0 = new Vector2(47, by + (up ? 4 : -3)), root1 = new Vector2(28, by + (up ? 3 : -2));
                Vector2 tip = up ? new Vector2(16, 45) : new Vector2(14, 1);
                Vector2 mid = up ? new Vector2(34, 44) : new Vector2(31, 2);
                Tri(p, root0, root1, tip, m); Tri(p, root0, tip, mid, m);
                Bar(p, (int)root0.x, (int)root0.y, (int)mid.x, (int)mid.y, 1, l);       // the leading edge
                Bar(p, (int)root1.x, (int)root1.y, (int)tip.x, (int)tip.y, 1, d);       // the trailing edge
                for (int i = 0; i <= 2; i++)
                {
                    Vector2 b = Vector2.Lerp(mid, tip, i / 2f);
                    p.Rect((int)b.x - 1, (int)b.y - (up ? 2 : 0), 3, 3, w);
                }
                int s = up ? 1 : -1;
                for (int k = 0; k < 2; k++) { p.Line(40 + k, by + s * 8, 36 + k, by + s * 13, bolt); p.Line(36 + k, by + s * 13, 39 + k, by + s * 15, bolt); p.Line(39 + k, by + s * 15, 35 + k, by + s * 19, bolt); }
                p.Outline(ol);
                // the eye under its brow
                p.Rect(56, by + 4, 2, 2, bolt); p.Set(57, by + 4, ol); p.Line(54, by + 7, 59, by + 6, ol);
                Save(p, $"Sprites/Bosses/stormhawk_{f}", FramedPivot(76, 46, 38, 2));
            }
            {
                // folded, head down
                var p = Framed(34, 60);
                Bar(p, 17, 46, 11, 58, 1, d); Bar(p, 17, 46, 17, 59, 1, m); Bar(p, 17, 46, 23, 58, 1, d);
                p.Set(10, 59, w); p.Set(17, 59, w); p.Set(24, 59, w);
                p.Ellipse(8f, 34f, 5f, 16f, d); p.Ellipse(26f, 34f, 5f, 16f, d);        // the folded wings
                p.Ellipse(17f, 32f, 8f, 17f, m);
                for (int k = 0; k < 2; k++) { p.Line(18 + k, 42, 15 + k, 36, bolt); p.Line(15 + k, 36, 18 + k, 33, bolt); p.Line(18 + k, 33, 15 + k, 27, bolt); }
                p.Disc(17f, 13f, 7f, w);
                Shade(p, w, wD, w, 0, 2, 0);
                p.Rect(16, 3, 3, 5, beak); p.Rect(16, 3, 1, 5, beakD); p.Set(17, 2, beakD);
                p.Rect(13, 22, 2, 3, beak); p.Rect(20, 22, 2, 3, beak);
                p.Outline(ol);
                p.Rect(13, 13, 2, 2, bolt); p.Rect(20, 13, 2, 2, bolt); p.Set(13, 13, ol); p.Set(21, 13, ol);
                Save(p, "Sprites/Bosses/stormhawk_2", FramedPivot(34, 60, 17, 30));
            }
        }

        /// <summary>
        /// The Rot Heart: a fungus heart as big as a cart on a twisted stalk from the ceiling, veins of cold light
        /// all over it, a slit down its middle that glows. 0 / 1 the beat, 2 swollen and bright (the tell before a
        /// move). The pivot is the bottom of the heart; the picture is 70 high, like the old one, so the stalk
        /// still reaches the ceiling.
        /// </summary>
        static void RotHeartArt()
        {
            Color32 ol = C(22, 14, 34);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(50, 70);
                Color32 dD = C(70, 28, 90), d = C(108, 46, 130), m = C(152, 72, 172), l = C(204, 128, 222), vein = C(70, 214, 186), veinL = C(196, 255, 238);
                Color32 stalk = C(98, 80, 118), stalkD = C(60, 48, 78), stalkL = C(134, 114, 152);
                if (f == 2) { d = C(140, 62, 160); m = C(192, 98, 204); l = C(242, 180, 250); vein = C(150, 255, 222); }
                float grow = f == 0 ? 0f : f == 1 ? 1.2f : 2.4f;
                // (flat, as the small creatures are: one tone to a shape, a rim of shadow under it, a few large marks)
                // the stalk: two strands twisted about each other, roots spreading into the ceiling
                for (int y = 36; y < 70; y++)
                {
                    int sway = Mathf.RoundToInt(Mathf.Sin(y * 0.33f) * 2f);
                    p.Rect(25 - sway, y, 3, 1, stalkD);
                    p.Rect(22 + sway, y, 4, 1, stalk);
                }
                Bar(p, 22, 62, 11, 69, 1, stalk); Bar(p, 28, 61, 39, 69, 1, stalk);
                // the heart: one great chamber, two lobes above it
                p.Ellipse(15f, 33f, 10f, 8f, m); p.Ellipse(34f, 33f, 10f, 8f, m);
                p.Ellipse(25f, 21f, 19f + grow, 18f + grow * 0.6f, m);
                Shade(p, m, d, l, 5, 3, 2);
                // four pustules
                int[,] pods = { { 9, 15 }, { 40, 14 }, { 13, 30 }, { 37, 31 } };
                for (int i = 0; i < pods.GetLength(0); i++) p.Disc(pods[i, 0] + 0.5f, pods[i, 1] + 0.5f, 2.6f, l);
                // two broad veins of light to either side of the slit
                for (int k = 0; k < 2; k++)
                {
                    p.Line(20 + k, 30, 14 + k, 24, vein); p.Line(14 + k, 24, 12 + k, 12, vein); p.Line(12 + k, 12, 17 + k, 5, vein);
                    p.Line(30 + k, 30, 36 + k, 24, vein); p.Line(36 + k, 24, 38 + k, 12, vein); p.Line(38 + k, 12, 33 + k, 5, vein);
                }
                // the slit down its middle: dark lips, light inside (wide open when it swells)
                float open = f == 2 ? 3.2f : f == 1 ? 2f : 1.4f;
                p.Ellipse(25f, 19f, open + 2f, 11f, dD); p.Ellipse(25f, 19f, open, 9f, vein); p.Ellipse(25f, 19f, open * 0.5f, 6.5f, veinL);
                // threads hanging from under it
                p.Rect(13, 1, 2, 4, dD); p.Rect(25, 0, 2, 3, dD); p.Rect(36, 1, 2, 4, dD);
                p.Outline(ol);
                Save(p, $"Sprites/Bosses/sporeheart_{f}", new Vector2(0.5f, 0f));
            }
        }

        /// <summary>
        /// The Foreman: an iron automaton behind a tower shield of copper, a red slit for eyes, a smokestack on its
        /// back and a mace on a chain arm. 0 / 1 walking, 2 charging (leaning in behind the shield, steam up).
        /// </summary>
        static void ForemanArt()
        {
            Color32 ol = C(18, 18, 26), dD = C(38, 40, 54), d = C(66, 70, 90), m = C(102, 108, 132), l = C(152, 160, 186);
            Color32 cD = C(120, 62, 34), c = C(186, 106, 54), cL = C(240, 166, 90), red = C(255, 70, 50), hot = C(255, 220, 120), steam = C(226, 232, 240);
            for (int f = 0; f < 3; f++)
            {
                var p = Framed(70, 62, 7);
                bool charge = f == 2;
                int lean = charge ? 4 : 0, step = f == 1 ? 1 : 0;
                // (flat, as the small creatures are: one tone to a part, shadow down its left side, no rivets)
                int mo = charge ? 6 : 0;
                // mace arm, behind: a ball with four studs
                Bar(p, 18 + lean, 34, 8, 28 + mo, 1, d);
                p.Disc(5.5f, 27.5f + mo, 4.4f, dD);
                p.Rect(-1, 27 + mo, 2, 2, l); p.Rect(5, 32 + mo, 2, 2, l); p.Rect(5, 21 + mo, 2, 2, l); p.Rect(10, 27 + mo, 2, 2, l);
                // smokestack, a puff of steam over it
                p.Rect(12 + lean, 38, 7, 15, d); p.Rect(12 + lean, 38, 2, 15, dD); p.Rect(11 + lean, 52, 9, 2, c);
                p.Rect(14 + lean, 56, 3, 3, steam);
                if (charge) { p.Rect(11, 59, 4, 3, steam); p.Rect(6, 61, 4, 3, steam); }
                // legs: pistons with copper knees, heavy feet
                int a = step == 1 ? 3 : 0, b = step == 1 ? 0 : 3;
                p.Rect(19 + a, 4, 8, 14, d); p.Rect(18 + a, 10, 10, 3, c);
                p.Rect(33 + b, 4, 8, 14, d); p.Rect(32 + b, 10, 10, 3, c);
                p.Rect(17 + a, 1, 13, 4, dD); p.Rect(31 + b, 1, 13, 4, dD);
                // the body: a boiler, a furnace door in it
                p.Rect(16 + lean, 17, 28, 24, m); p.Rect(16 + lean, 17, 4, 24, d); p.Rect(16 + lean, 17, 28, 3, d); p.Rect(41 + lean, 20, 3, 21, l);
                p.Rect(23 + lean, 22, 11, 8, dD); p.Rect(24 + lean, 23, 9, 6, red); p.Rect(26 + lean, 24, 5, 3, hot);
                // shoulder gear
                p.Disc(20.5f + lean, 40.5f, 6.4f, c); p.Disc(20.5f + lean, 40.5f, 2.2f, dD);
                for (int i = 0; i < 8; i++) { float an = i * Mathf.PI / 4f; p.Rect(Mathf.RoundToInt(20f + lean + Mathf.Cos(an) * 7f), Mathf.RoundToInt(40f + Mathf.Sin(an) * 7f), 2, 2, c); }
                // head: a helmet with a crest, one red slit
                Color32 hm = C(102, 108, 133);
                p.Ellipse(33f + lean, 48f, 10f, 8f, hm);
                Shade(p, hm, d, l, 2, 3, 2);
                p.Rect(24 + lean, 41, 19, 3, d);                                                        // collar
                p.Rect(31 + lean, 55, 4, 5, c);
                p.Rect(31 + lean, 47, 12, 3, dD); p.Rect(34 + lean, 48, 8, 1, red); p.Rect(40 + lean, 48, 2, 1, hot);
                // the shield arm and the tower shield before it
                int sx = 48 + lean + (charge ? 3 : 0), sy = charge ? 3 : 6;
                Bar(p, 42 + lean, 34, sx + 1, 30, 2, d);
                p.Rect(sx, sy, 15, 44, c); p.Rect(sx, sy, 3, 44, cD); p.Rect(sx + 13, sy, 2, 44, cL);
                p.Rect(sx, sy, 15, 3, d); p.Rect(sx, sy + 41, 15, 3, d);                                  // its iron bands
                p.Disc(sx + 7.5f, sy + 21.5f, 5.2f, d); p.Disc(sx + 7.5f, sy + 21.5f, 2.2f, l);           // a boss in its middle
                foreach (int ry in new[] { 8, 21, 34 }) { p.Rect(sx + 15, sy + ry, 3, 2, l); p.Set(sx + 18, sy + ry, l); }                 // spikes
                p.Outline(ol);
                Save(p, $"Sprites/Bosses/foreman_{f}", FramedPivot(70, 62, 35, 1, 7));
            }
        }

        /// <summary>
        /// The Hollow Warden: a robe with nothing in it but two cold lights - a hood under a broken crown, sleeves
        /// that end in hands of bone, a hem in tatters that never touches the floor. 0 / 1 floating.
        /// </summary>
        static void WardenArt()
        {
            Color32 ol = C(18, 10, 34), dD = C(44, 26, 76), d = C(76, 48, 118), m = C(116, 80, 164), l = C(166, 130, 212);
            Color32 dark = C(12, 7, 24), bone = C(232, 226, 242), boneD = C(170, 160, 198), glow = C(140, 255, 230), glowL = C(240, 255, 250);
            Color32 gold = C(236, 190, 70), goldD = C(170, 120, 36);
            int[] rags = { 2, 8, 4, 9, 1, 6, 9, 3, 7, 0, 8, 5 };      // how far down each rag of the hem hangs
            for (int f = 0; f < 2; f++)
            {
                var p = Framed(68, 76);
                // (flat, as the small creatures are: one tone to a shape, a rim of shadow under it, a few large marks)
                Color32 mm = C(116, 80, 165), mh = C(116, 80, 166);      // the mantle and the hood: the robe's tone, shapes of their own
                // sleeves and hands of bone: three long fingers each
                Bar(p, 18, 44, 8, 34 - f, 3, d); Bar(p, 50, 44, 60, 36 + f, 3, d);
                p.Line(7, 31 - f, 3, 26 - f, bone); p.Line(8, 31 - f, 7, 24 - f, bone); p.Line(9, 31 - f, 11, 25 - f, bone);
                p.Line(61, 33 + f, 65, 28 + f, bone); p.Line(60, 33 + f, 61, 26 + f, bone); p.Line(59, 33 + f, 57, 27 + f, bone);
                // the robe: narrow at the shoulders, wide at the hem, in tatters; one side of it in shadow
                for (int y = 4; y <= 48; y++)
                {
                    float k = (48 - y) / 44f;
                    int half = Mathf.RoundToInt(Mathf.Lerp(15f, 25f, k));
                    for (int x = 34 - half; x <= 34 + half; x++)
                    {
                        // the hem hangs in long pointed rags, no two alike (they shift a little as it floats)
                        int strip = (x - 9 + f) / 5, into = (x - 9 + f) % 5;
                        int rag = 4 + (9 - rags[strip % rags.Length]) + Mathf.Abs(into - 2) * 2;
                        if (y < rag) continue;
                        float u = (x - 34f) / half;
                        p.Set(x, y, u < -0.45f ? d : m);
                    }
                }
                // two deep folds
                p.Rect(29, 12, 2, 16, d); p.Rect(41, 10, 2, 18, d);
                p.Rect(22, 30, 25, 2, dD); p.Rect(31, 28, 6, 6, gold); p.Rect(33, 30, 2, 2, goldD);       // a belt and its clasp
                // the mantle over its shoulders, the hood
                p.Ellipse(34f, 46f, 19f, 7f, mm);
                Shade(p, mm, d, l, 3, 3, 0);
                p.Ellipse(34f, 58f, 14f, 14f, mh);
                Shade(p, mh, d, l, 2, 3, 2);
                // nothing in the hood but two lights
                p.Ellipse(36f, 56f, 9f, 10f, dark);
                // the broken crown
                p.Rect(24, 68, 21, 2, gold); p.Rect(24, 68, 21, 1, goldD);
                p.Rect(24, 70, 2, 3, gold); p.Rect(30, 70, 2, 5, gold); p.Rect(36, 70, 2, 2, goldD); p.Rect(42, 70, 2, 4, gold);
                p.Outline(ol);
                int e = f == 1 ? 1 : 0;
                p.Rect(31, 57, 4, 2 + e, glow); p.Rect(39, 57, 4, 2 + e, glow); p.Set(33, 58, glowL); p.Set(41, 58, glowL);
                p.Set(30, 59 + e, glow); p.Set(43, 59 + e, glow);                              // the lights slant: it is not pleased
                p.Set(36, 51, C(60, 140, 130)); p.Set(37, 50, C(60, 140, 130));                // a breath of cold
                Save(p, $"Sprites/Bosses/warden_{f}", FramedPivot(68, 76, 34, 2));
            }
        }
    }
}
