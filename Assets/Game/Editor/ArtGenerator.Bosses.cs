using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The bosses that used to be their area's creature at double size, drawn for themselves: the Forge Brute,
    /// the Yeti King, the Storm Hawk, the Rot Heart and the Hollow Warden. (The Broodmother is in
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
            GearWyrmArt();
            WardenArt();
            EclipseKingArt();
        }

        /// <summary>
        /// The Eclipse King, drawn the way the Rot Heart is: small, seen from the front, a few round shapes in one
        /// tone with a rim of shadow, a handful of large marks, and one thing that shines - the slit down its middle.
        /// First a shell: a boulder of a beast with two shoulders, two fists, two feet, two horns, the Sunstone's
        /// light in the slit of its chest (two sets of pictures: the slit narrow, and wide with two veins of light
        /// once it is half broken). 0 standing, 1 fists raised, 2 head down in the charge, 3 / 4 the two steps of
        /// its walk; and "open": the slit gaping (horns stuck, and coming apart).
        /// Then the core: a small covered sun - a ball of light with a dark disc on it and a slit of white in that,
        /// flares round it, threads of light under it (0 / 1 burning, "cast", "open") - and its two hands (open, fist).
        /// Also what it throws: the stroke of its arm (three frames), the wall of light, the column of light, an ember.
        /// </summary>
        static void EclipseKingArt()
        {
            Color32 ol = C(22, 14, 34), m = C(98, 100, 150), d = C(62, 62, 108), l = C(156, 162, 212), dD = C(36, 32, 66);
            Color32 mf = C(98, 100, 151), ma = C(98, 100, 152);      // feet, fists: the stone's tone, shapes of their own
            Color32 horn = C(236, 226, 204), hot = C(255, 246, 206), mid = C(255, 196, 84), rim = C(240, 124, 40);
            Color32 sun = C(255, 190, 70), sunD = C(232, 120, 40), sunL = C(255, 240, 190), dark = C(26, 18, 40);

            // ---- the shell: frames 0..4, and 5 = "open"; twice (the slit narrow / wide)
            for (int wide = 0; wide < 2; wide++)
                for (int f = 0; f < 6; f++)
                {
                    bool open = f == 5;
                    if (open && wide == 0) continue;
                    var p = new Px(76, 72);
                    int lean = f == 2 ? 7 : 0, sq = f == 2 ? -6 : open ? -4 : 0, cx = 38;
                    int stepL = f == 3 ? 3 : f == 4 ? -1 : 0, stepR = f == 4 ? 3 : f == 3 ? -1 : 0;      // a foot lifted, turn about
                    int fistY = f == 1 ? 52 : open ? 12 : 22;
                    // two feet
                    p.Ellipse(cx - 13, 5 + stepL, 9f, 5f, mf); p.Ellipse(cx + 13, 5 + stepR, 9f, 5f, mf);
                    Shade(p, mf, d, l, 2, 2, 0);
                    // two fists, hanging at its sides (raised for the swipe and the leap, on the floor when it is spent)
                    p.Disc(cx - 28 + (f == 2 ? 4 : 0), fistY + (f == 3 ? 2 : 0), 8f, ma); p.Disc(cx + 28 + (f == 2 ? 4 : 0), fistY + (f == 4 ? 2 : 0), 8f, ma);
                    Shade(p, ma, d, l, 3, 2, 2);
                    // the body: one great boulder, two shoulders above it, a low head between them
                    p.Ellipse(cx - 16 + lean, 44 + sq, 12f, 10f, m); p.Ellipse(cx + 16 + lean, 44 + sq, 12f, 10f, m);
                    p.Ellipse(cx + lean, 51 + sq, 11f, 9f, m);
                    p.Ellipse(cx + lean * 0.5f, 28 + sq, 24f, 21f, m);
                    Shade(p, m, d, l, 5, 3, 2);
                    // four pale stones in it
                    int[,] studs = { { -22, 30 }, { 22, 30 }, { -15, 14 }, { 15, 14 } };
                    for (int k = 0; k < 4; k++) p.Disc(cx + studs[k, 0] + lean * 0.5f + 0.5f, studs[k, 1] + sq + 0.5f, 2.6f, l);
                    // two horns: up and out - both levelled forward in the charge
                    if (f == 2) { Bar(p, cx + 6 + lean, 56 + sq, cx + 22 + lean, 60 + sq, 2, horn); Bar(p, cx - 6 + lean, 58 + sq, cx + 8 + lean, 66 + sq, 2, horn); }
                    else
                    {
                        Bar(p, cx - 9 + lean, 57 + sq, cx - 16 + lean, 64 + sq, 2, horn); Bar(p, cx - 16 + lean, 64 + sq, cx - 15 + lean, 69 + sq, 1, horn);
                        Bar(p, cx + 9 + lean, 57 + sq, cx + 16 + lean, 64 + sq, 2, horn); Bar(p, cx + 16 + lean, 64 + sq, cx + 15 + lean, 69 + sq, 1, horn);
                    }
                    // half broken: two broad veins of light to either side of the slit
                    int sx = Mathf.RoundToInt(cx + lean * 0.5f), sy = 27 + sq;
                    if (wide == 1 || open)
                        for (int k = 0; k < 2; k++)
                        {
                            p.Line(sx - 5 + k, sy + 11, sx - 11 + k, sy + 5, mid); p.Line(sx - 11 + k, sy + 5, sx - 13 + k, sy - 7, mid); p.Line(sx - 13 + k, sy - 7, sx - 8 + k, sy - 14, mid);
                            p.Line(sx + 5 + k, sy + 11, sx + 11 + k, sy + 5, mid); p.Line(sx + 11 + k, sy + 5, sx + 13 + k, sy - 7, mid); p.Line(sx + 13 + k, sy - 7, sx + 8 + k, sy - 14, mid);
                        }
                    // the slit down its middle: dark lips, the Sunstone's light inside (gaping when it is spent)
                    float gap = open ? 4.2f : wide == 1 ? 2.2f : 1.4f;
                    p.Ellipse(sx, sy, gap + 2f, 11f, dD); p.Ellipse(sx, sy, gap, 9f, mid); p.Ellipse(sx, sy, gap * 0.5f, 6.5f, hot);
                    p.Outline(ol);
                    // two lights for eyes, under a brow
                    if (!open) { p.Rect(cx - 6 + lean, 51 + sq, 3, 2, hot); p.Rect(cx + 4 + lean, 51 + sq, 3, 2, hot); }
                    p.Rect(cx - 7 + lean, 54 + sq, 5, 1, dD); p.Rect(cx + 3 + lean, 54 + sq, 5, 1, dD);
                    Save(p, open ? "Sprites/Bosses/eclipseshell_open" : $"Sprites/Bosses/eclipseshell_{wide}_{f}", new Vector2(0.5f, 0f));
                }

            // ---- the core: 0 / 1 burning, 2 "cast" (its flares stand out), 3 "open" (the dark disc split wide, the stone bare)
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(64, 70);
                int fl = f == 1 ? 1 : 0, cx = 32, cy = 38;
                bool cast = f == 2, open = f == 3;
                float big = cast ? 2f : 0f;
                // threads of light hanging under it
                p.Rect(cx - 12, 10 + fl, 2, 12, sunD); p.Rect(cx - 1, 6 - fl, 2, 14, sunD); p.Rect(cx + 10, 10 - fl + 1, 2, 12, sunD);
                // the ball of light, five flares round it
                p.Ellipse(cx - 18, cy + 9, 8f + big, 7f + big, sun); p.Ellipse(cx + 18, cy + 9, 8f + big, 7f + big, sun);
                p.Ellipse(cx, cy + 20, 9f + big, 7f + big, sun);
                p.Ellipse(cx - 15, cy - 14 - fl, 7f + big, 6f + big, sun); p.Ellipse(cx + 15, cy - 14 + fl, 7f + big, 6f + big, sun);
                p.Ellipse(cx, cy, 21f, 20f, sun);
                Shade(p, sun, sunD, sunL, 5, 3, 2);
                // four sparks on it
                int[,] sparks = { { -21, 10 }, { 21, 10 }, { -16, -13 }, { 16, -13 } };
                for (int k = 0; k < 4; k++) p.Disc(cx + sparks[k, 0] + 0.5f, cy + sparks[k, 1] + 0.5f, 2.2f, sunL);
                // the covered sun: a dark disc on it, a slit of white in that - split wide when its light has sunk
                p.Disc(cx, cy, 12.5f, dark);
                float gap = open ? 5f : cast ? 2.2f : 1.3f;
                p.Ellipse(cx, cy, gap + 1.5f, 9.5f, sunD); p.Ellipse(cx, cy, gap, 8f, mid); p.Ellipse(cx, cy, gap * 0.55f, 6f, hot);
                p.Outline(ol);
                Save(p, f < 2 ? $"Sprites/Bosses/eclipsecore_{f}" : cast ? "Sprites/Bosses/eclipsecore_cast" : "Sprites/Bosses/eclipsecore_open", new Vector2(0.5f, 0f));
            }
            // ---- its hands: open (three round fingers), and a fist
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(26, 24);
                if (f == 0) { p.Disc(6f, 17f, 3.5f, sun); p.Disc(13f, 19f, 3.5f, sun); p.Disc(20f, 17f, 3.5f, sun); }
                p.Ellipse(13f, 9f, 10f, f == 0 ? 8f : 9f, sun);
                Shade(p, sun, sunD, sunL, 3, 2, 2);
                if (f == 1) { p.Rect(8, 12, 1, 4, sunD); p.Rect(13, 13, 1, 4, sunD); p.Rect(18, 12, 1, 4, sunD); }
                else p.Disc(13f, 9f, 2.5f, sunL);
                p.Outline(ol);
                Save(p, f == 0 ? "Sprites/Bosses/sunhand_open" : "Sprites/Bosses/sunhand_fist", new Vector2(0.5f, 0f));
            }
            {
                // the wave his landing throws along the floor: a wall of the stone's light as tall as he is, leaning the
                // way it runs (drawn running right). Two frames: its tongues flicker.
                for (int f = 0; f < 2; f++)
                {
                    var p = new Px(18, 66);
                    for (int y = 0; y < 66; y++)
                    {
                        float u = y / 65f;
                        float half = Mathf.Lerp(6.5f, 1.5f, u * u) + (((y / 5) + f) % 2 == 0 ? 0.8f : -0.4f) * u * 2f;
                        float xc = 9f - 3f * u + ((y / 7 + f) % 2 == 0 ? 1f : -1f) * u * 1.5f;
                        for (int x = 0; x < 18; x++)
                        {
                            float off = Mathf.Abs(x - xc);
                            if (off > half) continue;
                            p.Set(x, y, off < half * 0.35f ? hot : off < half * 0.7f ? mid : rim);
                        }
                    }
                    Save(p, $"Sprites/Bosses/sunwave_{f}", new Vector2(0.5f, 0f));
                }
            }
            // the stroke of the blade: a long low crescent, its bright edge leading (drawn running right)
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(108, 22);
                int from = f == 2 ? 30 : 2, to = f == 0 ? 64 : 105;
                for (int x = from; x <= to; x++)
                {
                    float u = (x - 2) / 103f;
                    float thick = (2f + 7f * Mathf.Sin(u * Mathf.PI)) * (f == 2 ? 0.45f : 1f);
                    float yc = 5f + 9f * u * u;                                     // it rises a little towards its tip
                    for (int y = 0; y < 22; y++)
                    {
                        float dist = Mathf.Abs(y - yc);
                        if (dist > thick) continue;
                        p.Set(x, y, f == 2 ? rim : dist < thick * 0.4f ? hot : dist < thick * 0.75f ? mid : rim);
                    }
                }
                Save(p, $"Sprites/Bosses/sunslash_{f}", PCenter);
            }
            {
                // the column of light (stretched to the height of the hall): a white core, gold to either side, fading out
                var p = new Px(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    float off = Mathf.Abs(x - 7.5f);
                    Color32 c = off < 2.2f ? hot : off < 4.5f ? mid : off < 6.5f ? C(240, 124, 40, 170) : C(240, 124, 40, 70);
                    p.Rect(x, 0, 1, 16, c);
                }
                Save(p, "Sprites/Bosses/sunbeam", PCenter);
            }
            {
                // an ember off the stone, falling
                var p = new Px(8, 11);
                p.Rect(3, 6, 2, 4, rim); p.Set(3, 10, rim);
                p.Disc(3.5f, 3.5f, 3.2f, rim); p.Disc(3.5f, 3.5f, 2.2f, mid); p.Disc(3.5f, 3.5f, 1f, hot);
                Save(p, "Sprites/Bosses/sunember", PCenter);
            }
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
        /// The Cogwyrm: a centipede of gears. Its head (0 jaws shut, 1 jaws wide and a glow in its throat) faces
        /// right; a body segment (a copper gear), and the tail (a smaller gear with a spike). All drawn about
        /// their middle: the game turns them. Flat, as the small creatures are.
        /// </summary>
        static void GearWyrmArt()
        {
            Color32 ol = C(34, 22, 20), bD = C(138, 90, 40), b = C(198, 142, 60), bL = C(242, 198, 112);
            Color32 st = C(168, 174, 196), stD = C(92, 96, 122), glow = C(255, 150, 50), glowL = C(255, 232, 130), dark = C(40, 28, 30);
            Color32 cD = C(120, 62, 34), c = C(186, 106, 54), cL = C(240, 166, 90);
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(42, 34);
                int open = f == 1 ? 4 : 0;
                // feelers swept back, the collar its first segment hangs on
                p.Line(14, 27, 6, 32, stD); p.Line(15, 27, 7, 32, stD); p.Line(12, 6, 5, 2, stD); p.Line(13, 6, 6, 2, stD);
                p.Rect(2, 9, 5, 16, stD); p.Rect(3, 11, 3, 12, st);
                // the skull: a brass dome
                p.Ellipse(19f, 17f, 14f, 12f, b);
                Shade(p, b, bD, bL, 4, 2, 2);
                p.Rect(9, 7, 3, 20, bD);      // a band of plate behind the eye
                // jaws: two steel pincers
                Bar(p, 28, 21, 36, 23 + open, 1, st); p.Line(36, 22 + open, 40, 18 + open / 2, st); p.Line(37, 23 + open, 41, 19 + open / 2, st);
                Bar(p, 28, 12, 36, 10 - open, 1, stD); p.Line(36, 11 - open, 40, 15 - open / 2, stD); p.Line(37, 10 - open, 41, 14 - open / 2, stD);
                if (f == 1) { p.Rect(31, 14, 5, 6, glow); p.Rect(32, 15, 3, 4, glowL); }
                // the core: the one soft thing in it
                p.Disc(20f, 18f, 5.5f, dark); p.Disc(20f, 18f, 4f, glow); p.Disc(20f, 18f, 2f, glowL);
                p.Outline(ol);
                p.Disc(20f, 18f, 4f, glow); p.Disc(20f, 18f, 2f, glowL);
                Save(p, $"Sprites/Bosses/gearwyrm_{f}", PCenter);
            }
            {
                // a segment: a copper gear with eight teeth
                var p = new Px(28, 28);
                for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; p.Disc(13.5f + Mathf.Cos(a) * 10.5f, 13.5f + Mathf.Sin(a) * 10.5f, 2.4f, cD); }
                p.Disc(13.5f, 13.5f, 9.5f, c);
                Shade(p, c, cD, cL, 3, 2, 2);
                p.Disc(13.5f, 13.5f, 4f, stD); p.Disc(13.5f, 13.5f, 2f, st);
                p.Outline(ol);
                Save(p, "Sprites/Bosses/gearwyrm_seg", PCenter);
            }
            {
                // the tail: a smaller gear, a spike behind it
                var p = new Px(24, 24);
                Tri(p, new Vector2(1, 11.5f), new Vector2(9, 8), new Vector2(9, 15), st);
                for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; p.Disc(13.5f + Mathf.Cos(a) * 7.5f, 11.5f + Mathf.Sin(a) * 7.5f, 2f, cD); }
                p.Disc(13.5f, 11.5f, 6.5f, c);
                Shade(p, c, cD, cL, 2, 2, 2);
                p.Disc(13.5f, 11.5f, 2.5f, stD);
                p.Outline(ol);
                Save(p, "Sprites/Bosses/gearwyrm_tail", PCenter);
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
