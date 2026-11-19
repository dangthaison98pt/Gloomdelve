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
                var p = new Px(78, 56);
                bool charge = f == 2, dazed = f == 3;
                int step = f == 1 ? 1 : 0;
                int hx = charge ? 3 : 0, hy = charge ? -7 : dazed ? -3 : 0;      // where the head is
                // far legs
                p.Rect(15 + (step == 1 ? 3 : -1) - (charge ? 4 : 0), 1, 7, 20, dD);
                p.Rect(49 + (step == 1 ? -2 : 2) + (charge ? 4 : 0), 1, 7, 20, dD);
                // rump, barrel, the hump of its shoulders
                Ball(p, 17, 25, 12, 13, d, m, l, l);
                Ball(p, 33, 26, 23, 16, d, m, l, l);
                Ball(p, 44, 35, 14, 11, d, m, l, l);
                // plates of basalt along its back
                for (int i = 0; i < 5; i++) { int bx = 12 + i * 8, bh = 3 + (i % 2) * 2 + (i == 3 ? 2 : 0); Tri(p, new Vector2(bx, 38 + i), new Vector2(bx + 6, 38 + i), new Vector2(bx + 2, 41 + i + bh), dD); p.Line(bx + 2, 41 + i + bh, bx + 5, 39 + i, d); }
                // the furnace in its belly, and the lava in its cracks
                p.Ellipse(32f, 13f, 13f, 4.5f, lava); p.Ellipse(32f, 12.5f, 8f, 2.6f, lavaL);
                p.Line(16, 30, 25, 24, lava); p.Line(25, 24, 23, 17, lava); p.Line(25, 24, 36, 23, lava); p.Line(36, 23, 41, 31, lava); p.Line(36, 23, 40, 16, lava);
                p.Line(9, 24, 14, 20, lava); p.Line(44, 38, 49, 33, lava);
                p.Set(25, 24, lavaL); p.Set(36, 23, lavaL); p.Set(14, 20, lavaL); p.Set(49, 33, lavaL);
                // tail: a short lash with a coal at its end
                p.Line(5, 28, 2, 33, d); p.Line(2, 33, 3, 37, d); p.Disc(3.5f, 38.5f, 1.6f, lava);
                // near legs, with hooves
                int nf = 51 + (step == 1 ? 2 : -2) + (charge ? 5 : 0), nh = 11 + (step == 1 ? -2 : 3) - (charge ? 5 : 0);
                foreach (int lx in new[] { nh, nf })
                {
                    // (each leg runs up into a haunch on the body: no gap under the round of its belly)
                    Ball(p, lx + 4, 21, 7, 8, d, m, l, l);
                    p.Rect(lx, 1, 8, 19, d); p.Rect(lx + 5, 1, 3, 19, m); p.Rect(lx, 1, 8, 3, dD); p.Rect(lx + 1, 9, 6, 1, lava);
                }
                // the neck (the head never leaves the shoulders, however low it goes), and the horn of its far side
                // behind the head: darker, only its sweep and its point showing over the skull
                Ball(p, 52 + hx / 2, 29 + hy / 2, 11, 10, d, m, l, l);
                Bar(p, 64 + hx, 33 + hy, 67 + hx, 40 + hy, 1, hornD); Bar(p, 67 + hx, 40 + hy, 73 + hx, 42 + hy, 1, hornD); p.Set(75 + hx, 42 + hy, hornD);
                // head: a blunt skull, a heavy jaw, an ember for an eye
                Ball(p, 60 + hx, 27 + hy, 11, 10, d, m, l, l);
                p.Rect(64 + hx, 18 + hy, 9, 8, m); p.Rect(64 + hx, 25 + hy, 9, 1, l); p.Rect(72 + hx, 18 + hy, 1, 8, l);      // muzzle
                p.Rect(62 + hx, 15 + hy, 10, 3, d); p.Rect(62 + hx, 15 + hy, 10, 1, dD);                                      // jaw
                p.Set(71 + hx, 22 + hy, lava); p.Set(71 + hx, 20 + hy, dD);                                                    // nostril, breathing fire
                if (charge) { p.Set(74 + hx, 22 + hy, lavaL); p.Set(75 + hx, 23 + hy, lava); p.Set(74 + hx, 20 + hy, lava); }
                // the horn of its near side, sweeping forward
                Bar(p, 57 + hx, 35 + hy, 60 + hx, 42 + hy, 1, horn); Bar(p, 60 + hx, 42 + hy, 67 + hx, 44 + hy, 1, horn); p.Set(69 + hx, 44 + hy, horn);
                p.Line(56 + hx, 35 + hy, 59 + hx, 42 + hy, hornD);
                p.Outline(ol);
                // the eye (after the outline: it glows), a heavy brow over it
                if (dazed) { p.Line(60 + hx, 29 + hy, 63 + hx, 26 + hy, ol); p.Line(60 + hx, 26 + hy, 63 + hx, 29 + hy, ol); }
                else { p.Rect(61 + hx, 27 + hy, 3, 2, charge ? lavaL : lava); p.Set(63 + hx, 28 + hy, lavaL); p.Line(58 + hx, 31 + hy, 65 + hx, 29 + hy, ol); }
                if (dazed)
                {
                    Color32 star = C(255, 226, 110);
                    p.Set(52, 48, star); p.Set(51, 47, star); p.Set(53, 47, star); p.Set(52, 46, star);
                    p.Set(62, 52, star); p.Set(61, 51, star); p.Set(63, 51, star); p.Set(62, 50, star);
                    p.Set(71, 47, star); p.Set(70, 46, star); p.Set(72, 46, star); p.Set(71, 45, star);
                }
                Save(p, $"Sprites/Bosses/forgebrute_{f}", new Vector2(0.5f, 1f / 56f));
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
                var p = new Px(62, 68);
                // far arm
                Bar(p, 12, 40, 6, 26, 3, dD); Bar(p, 6, 26, 9, 14, 3, dD); Ball(p, 9, 11, 5, 5, skinD, skinD, skin, skinL);
                // legs and feet
                p.Rect(15, 4, 11, 10, d); p.Rect(33, 4, 11, 10, d); p.Rect(22, 4, 4, 10, dD); p.Rect(40, 4, 4, 10, m);
                p.Rect(13, 1, 15, 4, skin); p.Rect(31, 1, 16, 4, skin); p.Rect(13, 1, 15, 1, skinD); p.Rect(31, 1, 16, 1, skinD);
                foreach (int tx in new[] { 20, 24, 39, 43 }) p.Rect(tx, 1, 1, 3, skinD);
                // belly, chest, shoulders
                // (one body, lit as one: two balls laid over each other would give it stripes)
                for (int y = 4; y <= 53; y++)
                    for (int x = 6; x <= 52; x++)
                    {
                        float ax = (x - 29) / 20f, ay = (y - 24) / 19f, bx = (x - 29) / 22f, by = (y - 39) / 13f;
                        if (ax * ax + ay * ay > 1f && bx * bx + by * by > 1f) continue;
                        p.Set(x, y, Round((x - 29) / 24f, (y - 29) / 26f, d, m, l, l));
                    }
                // shag: tufts along the outline and strokes in the fur
                int[,] tufts = { { 8, 22 }, { 9, 30 }, { 7, 38 }, { 50, 20 }, { 51, 30 }, { 52, 40 }, { 18, 6 }, { 29, 5 }, { 40, 6 } };
                for (int i = 0; i < tufts.GetLength(0); i++) { int tx = tufts[i, 0], ty = tufts[i, 1]; p.Set(tx, ty, d); p.Set(tx + (tx < 30 ? -1 : 1), ty - 1, d); p.Set(tx, ty - 1, d); }
                int[,] strokes = { { 16, 30 }, { 22, 20 }, { 30, 14 }, { 36, 28 }, { 42, 20 }, { 26, 32 }, { 18, 42 }, { 38, 40 }, { 44, 32 }, { 30, 24 } };
                for (int i = 0; i < strokes.GetLength(0); i++) { int sx = strokes[i, 0], sy = strokes[i, 1]; p.Line(sx, sy, sx + 1, sy - 3, dD); p.Set(sx + 2, sy - 1, dD); }
                // head, and the bare blue face in it
                Ball(p, 33, 50, 12, 11, d, m, l, l);
                p.Ellipse(37f, 49f, 8f, 7f, skin); p.Ellipse(38f, 51f, 6f, 4f, skinL);
                p.Rect(34, 43, 9, 3, skinD);                                               // mouth
                p.Rect(35, 44, 1, 4, tusk); p.Rect(41, 44, 1, 4, tusk);                    // tusks
                // horns of ice, and the crown between them
                Bar(p, 24, 57, 20, 63, 1, ice); p.Set(19, 65, iceL); p.Set(20, 64, iceL);
                Bar(p, 43, 58, 48, 63, 1, ice); p.Set(49, 65, iceL); p.Set(49, 64, iceL);
                p.Rect(27, 60, 14, 3, gold); p.Rect(27, 60, 14, 1, goldD);
                p.Rect(27, 63, 2, 3, gold); p.Rect(33, 63, 2, 4, gold); p.Rect(39, 63, 2, 3, gold);
                p.Rect(33, 61, 2, 1, gem);
                // near arm: hanging, or raised with the hand open
                if (f == 0) { Bar(p, 47, 40, 54, 27, 4, m); Bar(p, 54, 27, 52, 15, 4, d); Ball(p, 52, 11, 6, 6, skinD, skin, skin, skinL); p.Rect(49, 6, 1, 3, skinD); p.Rect(52, 5, 1, 3, skinD); p.Rect(55, 6, 1, 3, skinD); }
                else { Bar(p, 47, 42, 55, 50, 4, m); Bar(p, 55, 50, 54, 59, 4, d); Ball(p, 54, 62, 6, 5, skinD, skin, skin, skinL); p.Rect(51, 65, 1, 3, skin); p.Rect(54, 66, 1, 2, skin); p.Rect(57, 65, 1, 3, skin); }
                p.Outline(ol);
                // eyes: cold light under a heavy brow
                p.Rect(35, 50, 2, 2, iceL); p.Rect(40, 50, 2, 2, iceL); p.Set(36, 50, C(40, 120, 220)); p.Set(41, 50, C(40, 120, 220));
                p.Line(33, 54, 37, 52, ol); p.Line(39, 52, 43, 54, ol);
                Save(p, $"Sprites/Bosses/yetiking_{f}", new Vector2(0.5f, 1f / 68f));
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
                var p = new Px(76, 46);
                int by = 20;                                    // the body's height
                bool up = f == 0;
                // far wing (dark, behind the body)
                if (up) Tri(p, new Vector2(48, by + 3), new Vector2(34, by + 2), new Vector2(46, 45), dD);
                else Tri(p, new Vector2(48, by - 2), new Vector2(34, by - 2), new Vector2(44, 1), dD);
                // tail: a fan of long feathers
                Bar(p, 26, by, 8, by - 5, 1, d); Bar(p, 26, by, 6, by, 1, m); Bar(p, 26, by, 8, by + 5, 1, d);
                p.Set(6, by - 6, w); p.Set(4, by, w); p.Set(6, by + 6, w);
                // body and the white head
                Ball(p, 38, by, 15, 8, d, m, l, l);
                Ball(p, 55, by + 3, 7, 7, wD, wD, w, w);
                p.Ellipse(47f, by + 1f, 4f, 6f, wD);            // ruff
                // the beak: heavy and hooked
                p.Rect(61, by + 2, 6, 3, beak); p.Rect(61, by + 2, 6, 1, beakD); p.Rect(66, by, 2, 3, beak); p.Set(67, by - 1, beakD);
                // talons
                foreach (int tx in new[] { 36, 42 }) { p.Rect(tx, by - 11, 2, 4, beak); p.Set(tx - 1, by - 12, beakD); p.Set(tx + 2, by - 12, beakD); p.Set(tx + 1, by - 13, beakD); }
                // near wing: long primaries, white at the tips, a streak of lightning in it
                Vector2 root0 = new Vector2(47, by + (up ? 4 : -3)), root1 = new Vector2(28, by + (up ? 3 : -2));
                Vector2 tip = up ? new Vector2(16, 45) : new Vector2(14, 1);
                Vector2 mid = up ? new Vector2(34, 44) : new Vector2(31, 2);
                Tri(p, root0, root1, tip, m); Tri(p, root0, tip, mid, m);
                Bar(p, (int)root0.x, (int)root0.y, (int)mid.x, (int)mid.y, 1, l);       // the leading edge
                for (int i = 1; i <= 4; i++)
                {
                    Vector2 a = Vector2.Lerp(root0, root1, i / 5f), b = Vector2.Lerp(mid, tip, i / 4.5f);
                    p.Line((int)a.x, (int)a.y, (int)b.x, (int)b.y, d);
                    p.Set((int)b.x, (int)b.y, w); p.Set((int)b.x, (int)b.y + (up ? -1 : 1), w);
                }
                p.Line((int)root1.x, (int)root1.y, (int)tip.x, (int)tip.y, dD);
                int s = up ? 1 : -1;
                p.Line(40, by + s * 8, 36, by + s * 13, bolt); p.Line(36, by + s * 13, 38, by + s * 15, bolt); p.Line(38, by + s * 15, 33, by + s * 20, bolt);
                p.Outline(ol);
                // the eye under its brow
                p.Rect(56, by + 4, 2, 2, bolt); p.Set(57, by + 4, ol); p.Line(54, by + 7, 59, by + 6, ol);
                Save(p, $"Sprites/Bosses/stormhawk_{f}", new Vector2(0.5f, 2f / 46f));
            }
            {
                // folded, head down
                var p = new Px(34, 60);
                Bar(p, 17, 46, 11, 58, 1, d); Bar(p, 17, 46, 17, 59, 1, m); Bar(p, 17, 46, 23, 58, 1, d);
                p.Set(10, 59, w); p.Set(17, 59, w); p.Set(24, 59, w);
                Ball(p, 17, 32, 8, 17, d, m, l, l);
                Ball(p, 8, 34, 5, 16, dD, dD, d, m); Ball(p, 26, 34, 5, 16, d, m, l, l);        // the folded wings
                p.Line(8, 22, 8, 46, dD); p.Line(26, 22, 26, 46, d);
                p.Line(27, 40, 25, 34, bolt); p.Line(25, 34, 27, 31, bolt); p.Line(27, 31, 25, 26, bolt);
                Ball(p, 17, 13, 7, 7, wD, wD, w, w);
                p.Rect(16, 3, 3, 5, beak); p.Rect(16, 3, 1, 5, beakD); p.Set(17, 2, beakD);
                p.Rect(13, 22, 2, 3, beak); p.Rect(20, 22, 2, 3, beak);
                p.Outline(ol);
                p.Rect(13, 13, 2, 2, bolt); p.Rect(20, 13, 2, 2, bolt); p.Set(13, 13, ol); p.Set(21, 13, ol);
                Save(p, "Sprites/Bosses/stormhawk_2", PCenter);
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
                // the stalk: two strands twisted about each other, roots spreading into the ceiling
                for (int y = 36; y < 70; y++)
                {
                    int sway = Mathf.RoundToInt(Mathf.Sin(y * 0.33f) * 2f);
                    p.Rect(22 + sway, y, 4, 1, stalk); p.Set(25 + sway, y, stalkL); p.Set(22 + sway, y, stalkD);
                    p.Rect(25 - sway, y, 3, 1, stalkD); p.Set(27 - sway, y, stalk);
                    if (y % 8 == 3) p.Set(21 + sway, y, vein);
                }
                Bar(p, 22, 62, 11, 69, 1, stalk); Bar(p, 28, 61, 39, 69, 1, stalk); p.Line(24, 56, 16, 63, stalkD); p.Line(27, 55, 34, 62, stalkD);
                // the heart: one great chamber, two lobes above it
                Ball(p, 15, 33, 10, 8, d, m, l, l); Ball(p, 34, 33, 10, 8, d, m, l, l);
                Ball(p, 25, 21, 19 + grow, 18 + grow * 0.6f, d, m, l, l);
                // pustules
                int[,] pods = { { 11, 14 }, { 38, 12 }, { 9, 26 }, { 40, 27 }, { 18, 6 }, { 32, 5 } };
                for (int i = 0; i < pods.GetLength(0); i++) { p.Disc(pods[i, 0] + 0.5f, pods[i, 1] + 0.5f, 2.2f, l); p.Set(pods[i, 0], pods[i, 1], dD); }
                // veins of light, branching from the slit
                p.Line(24, 35, 21, 28, vein); p.Line(21, 28, 14, 24, vein); p.Line(14, 24, 9, 18, vein); p.Line(21, 28, 19, 20, vein);
                p.Line(27, 35, 30, 28, vein); p.Line(30, 28, 37, 25, vein); p.Line(37, 25, 42, 19, vein); p.Line(30, 28, 32, 20, vein);
                p.Line(19, 20, 14, 11, vein); p.Line(32, 20, 36, 11, vein); p.Line(22, 9, 20, 4, vein); p.Line(28, 9, 31, 4, vein);
                p.Set(14, 24, veinL); p.Set(37, 25, veinL); p.Set(9, 18, veinL); p.Set(42, 19, veinL); p.Set(14, 11, veinL); p.Set(36, 11, veinL);
                // the slit down its middle: dark lips, light inside (wide open when it swells)
                float open = f == 2 ? 3.2f : f == 1 ? 2f : 1.4f;
                p.Ellipse(25f, 19f, open + 2f, 11f, dD); p.Ellipse(25f, 19f, open, 9f, vein); p.Ellipse(25f, 19f, open * 0.5f, 6.5f, veinL);
                // threads hanging from under it
                p.Line(14, 5, 13, 1, dD); p.Line(25, 2, 25, 0, dD); p.Line(36, 5, 37, 1, dD);
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
                var p = new Px(70, 62);
                bool charge = f == 2;
                int lean = charge ? 4 : 0, step = f == 1 ? 1 : 0;
                // mace arm, behind
                Bar(p, 18 + lean, 34, 8, 28 + (charge ? 6 : 0), 1, d);
                p.Disc(5.5f, 27.5f + (charge ? 6 : 0), 4.2f, dD); p.Disc(6f, 28f + (charge ? 6 : 0), 2.4f, d);
                foreach (var s in new[] { new Vector2Int(0, 28), new Vector2Int(5, 33), new Vector2Int(5, 22), new Vector2Int(10, 28) }) p.Set(s.x, s.y + (charge ? 6 : 0), l);
                // smokestack
                Plate(p, 12 + lean, 38, 7, 15, dD, d, m, false); p.Rect(11 + lean, 52, 9, 2, c); p.Rect(11 + lean, 53, 9, 1, cL);
                p.Set(15 + lean, 56, steam); p.Set(13 + lean, 58, steam); p.Set(16 + lean, 60, steam);
                if (charge) { p.Rect(12, 57, 3, 2, steam); p.Rect(8, 59, 4, 2, steam); p.Set(16, 59, steam); }
                // legs: pistons with copper knees, heavy feet
                int a = step == 1 ? 3 : 0, b = step == 1 ? 0 : 3;
                p.Rect(19 + a, 4, 8, 14, d); p.Rect(24 + a, 4, 3, 14, m); p.Rect(18 + a, 10, 10, 3, c); p.Rect(18 + a, 12, 10, 1, cL);
                p.Rect(33 + b, 4, 8, 14, d); p.Rect(38 + b, 4, 3, 14, m); p.Rect(32 + b, 10, 10, 3, c); p.Rect(32 + b, 12, 10, 1, cL);
                p.Rect(17 + a, 1, 13, 4, dD); p.Rect(17 + a, 4, 13, 1, d); p.Rect(31 + b, 1, 13, 4, dD); p.Rect(31 + b, 4, 13, 1, d);
                // the body: a boiler of riveted plate, a furnace door in it
                Plate(p, 16 + lean, 17, 28, 24, dD, d, m);
                p.Rect(17 + lean, 28, 26, 1, dD); p.Rect(38 + lean, 18, 5, 22, l);
                p.Rect(22 + lean, 21, 9, 6, dD); p.Rect(23 + lean, 22, 7, 4, red); p.Rect(25 + lean, 23, 3, 2, hot);
                // shoulder gear
                p.Disc(20.5f + lean, 40.5f, 6.4f, cD); p.Disc(20.5f + lean, 40.5f, 4.6f, c); p.Disc(20.5f + lean, 40.5f, 1.8f, dD);
                for (int i = 0; i < 8; i++) { float an = i * Mathf.PI / 4f; p.Rect(Mathf.RoundToInt(20f + lean + Mathf.Cos(an) * 7f), Mathf.RoundToInt(40f + Mathf.Sin(an) * 7f), 2, 2, c); }
                // head: a helmet with a crest, one red slit
                Ball(p, 33 + lean, 48, 10, 8, d, m, l, l);
                p.Rect(24 + lean, 41, 19, 3, d); p.Rect(24 + lean, 43, 19, 1, l);                       // collar
                p.Rect(31 + lean, 55, 4, 5, c); p.Rect(31 + lean, 59, 4, 1, cL); p.Rect(33 + lean, 55, 2, 5, cL);
                p.Rect(31 + lean, 47, 12, 3, dD); p.Rect(34 + lean, 48, 8, 1, red); p.Set(40 + lean, 48, hot); p.Set(41 + lean, 48, hot);
                // the shield arm and the tower shield before it
                int sx = 48 + lean + (charge ? 3 : 0), sy = charge ? 3 : 6;
                Bar(p, 42 + lean, 34, sx + 1, 30, 2, d);
                Plate(p, sx, sy, 15, 44, cD, c, cL, false);
                p.Rect(sx, sy, 15, 2, d); p.Rect(sx, sy + 42, 15, 2, l); p.Rect(sx, sy, 2, 44, dD); p.Rect(sx + 13, sy, 2, 44, l);       // its iron rim
                foreach (int ry in new[] { 5, 15, 28, 38 }) { p.Set(sx + 4, sy + ry, cD); p.Set(sx + 10, sy + ry, cD); }
                p.Disc(sx + 7.5f, sy + 21.5f, 5.2f, d); p.Disc(sx + 7.5f, sy + 21.5f, 3.4f, m); p.Disc(sx + 7.5f, sy + 21.5f, 1.4f, dD);     // a gear for a boss
                foreach (int ry in new[] { 8, 21, 34 }) { p.Rect(sx + 15, sy + ry, 3, 2, l); p.Set(sx + 18, sy + ry, l); }                 // spikes
                p.Outline(ol);
                Save(p, $"Sprites/Bosses/foreman_{f}", new Vector2(0.5f, 1f / 62f));
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
                var p = new Px(68, 76);
                // sleeves and hands of bone
                Bar(p, 18, 44, 8, 34 - f, 3, d); Bar(p, 50, 44, 60, 36 + f, 3, m);
                p.Line(7, 31 - f, 3, 26 - f, bone); p.Line(8, 31 - f, 7, 24 - f, bone); p.Line(9, 31 - f, 11, 25 - f, bone); p.Set(5, 29 - f, boneD);
                p.Line(61, 33 + f, 65, 28 + f, bone); p.Line(60, 33 + f, 61, 26 + f, bone); p.Line(59, 33 + f, 57, 27 + f, bone); p.Set(63, 31 + f, boneD);
                // a broken chain on each wrist
                for (int i = 0; i < 4; i++) { p.Set(10 + (i % 2), 30 - f - i * 2, boneD); p.Set(58 - (i % 2), 32 + f - i * 2, boneD); }
                // the robe: narrow at the shoulders, wide at the hem, in tatters
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
                        bool fold = ((x + Mathf.RoundToInt(Mathf.Sin(y * 0.2f + f) * 1.5f)) % 8) == 0;
                        p.Set(x, y, fold ? dD : u > 0.55f ? l : u > -0.1f ? m : u > -0.7f ? d : dD);
                    }
                }
                p.Rect(22, 30, 25, 2, dD); p.Rect(31, 28, 6, 6, gold); p.Rect(32, 29, 4, 4, goldD); p.Set(33, 31, glow); p.Set(34, 30, glow);       // a belt and its clasp
                // the mantle over its shoulders, the hood
                Ball(p, 34, 46, 19, 7, d, m, l, l);
                Ball(p, 34, 58, 14, 14, d, m, l, l);
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
                Save(p, $"Sprites/Bosses/warden_{f}", new Vector2(0.5f, 2f / 76f));
            }
        }
    }
}
