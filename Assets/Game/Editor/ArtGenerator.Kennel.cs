using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The room inside the dog's kennel ("Sprites/Village/kennel_room"): 192 x 112 px - one low room of ten by five
    /// cells, built like the rooms of the houses (ArtGenerator.Village, Room): a post at each side, roof beams with
    /// their joists, thick floor boards, a wall of old lime plaster over wooden panelling. The little arched door is
    /// in the back wall at the left; a lantern hangs from the ridge, a stove stands on its stone hearth at the right,
    /// the dog's basket in the middle with its bowls, things on the wall. Floor space begins at (16, 16).
    /// </summary>
    public static partial class ArtGenerator
    {
        public const int KennelRoomW = 192, KennelRoomH = 112;

        static void KennelRoomArt(string d)
        {
            var p = new Px(KennelRoomW, KennelRoomH);
            Color32 wood = C(120, 82, 52), woodD = C(84, 54, 34), woodL = C(160, 116, 74),
                plaster = Lerp(C(176, 150, 112), C(104, 86, 78), 0.42f),
                straw = C(214, 176, 96), strawD = C(168, 130, 64), red = C(176, 62, 66), redD = C(124, 40, 48), cream = C(232, 214, 176),
                iron = C(48, 46, 56), ironL = C(78, 76, 90), glow = C(255, 196, 110), glowL = C(255, 236, 170),
                stone = C(118, 112, 120), stoneD = C(84, 80, 92);
            const int fx = 16, fy = 16, fw = 160, fh = 80;           // floor space

            void Shade(int x, int y, int w, int h, float f)
            {
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++) { var c = p.Get(xx, yy); if (c.a > 0) p.Set(xx, yy, Mul(c, f)); }
            }

            // the frame of the room: posts, roof beams, floor boards
            p.Rect(0, 0, KennelRoomW, KennelRoomH, C(58, 38, 30));
            // the wall: lime plaster blotched by the years over a band of wooden panelling
            for (int y = fy; y < fy + fh; y++)
                for (int x = fx; x < fx + fw; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.06f + 40f, y * 0.06f) * 0.7f + Mathf.PerlinNoise(x * 0.21f, y * 0.21f + 11f) * 0.3f;
                    p.Set(x, y, Mul(plaster, 0.84f + n * 0.3f));
                }
            for (int i = 0; i < 3; i++) p.Line(fx + 30 + i * 2, fy + 74 - i * 5, fx + 33 + i * 2, fy + 68 - i * 5, Mul(plaster, 0.7f));      // a crack
            const int wain = 26;
            p.Rect(fx, fy, fw, wain, Mul(wood, 0.9f)); p.Rect(fx, fy + wain, fw, 2, woodL);
            for (int x = 0; x < fw; x += 12) p.Rect(fx + x, fy, 1, wain, woodD);
            p.Rect(fx, fy, fw, 2, Mul(woodD, 0.8f));                                                 // the skirting
            // the roof beams and their joists; the floor boards; a post at either side
            p.Rect(0, fy + fh, KennelRoomW, 16, woodD);
            for (int x = 10; x < KennelRoomW - 10; x += 32) p.Rect(x, fy + fh - 6, 8, 6, woodD);
            p.Rect(0, 0, KennelRoomW, fy, wood); p.Rect(0, fy - 1, KennelRoomW, 1, woodL); p.Rect(0, 0, KennelRoomW, 1, woodD);
            for (int x = 0; x < KennelRoomW; x += 24) p.Rect(x, 0, 1, fy - 1, woodD);
            p.Rect(0, 0, fx, KennelRoomH, woodD); p.Rect(fx + fw, 0, 16, KennelRoomH, woodD);
            p.Rect(fx - 1, fy, 1, fh, wood); p.Rect(fx + fw, fy, 1, fh, wood);
            // the shadow under the beams and in the corners: the room has depth
            for (int i = 0; i < 10; i++) Shade(fx, fy + fh - 1 - i, fw, 1, 0.62f + i * 0.038f);
            for (int i = 0; i < 6; i++) { Shade(fx + i, fy, 1, fh, 0.7f + i * 0.05f); Shade(fx + fw - 1 - i, fy, 1, fh, 0.7f + i * 0.05f); }
            for (int x = 10; x < KennelRoomW - 10; x += 32) if (x + 8 > fx && x < fx + fw) Shade(Mathf.Max(fx, x + 2), fy + fh - 9, 8, 3, 0.8f);

            // the door: a little arched door of boards in the back wall, an iron ring, a mat before it
            {
                int dx = fx + 5, dw = 16, dh = 22;
                bool InArch(int x, int y, int w, int h)
                {
                    float r = w / 2f, top = h - r;
                    if (y < top) return true;
                    float ax = (x - w / 2f + 0.5f) / r, ay = (y - top) / r;
                    return ax * ax + ay * ay < 1f;
                }
                for (int y = -2; y < dh + 2; y++)
                    for (int x = -2; x < dw + 2; x++)
                        if (InArch(x + 2, y + 2, dw + 4, dh + 4)) p.Set(dx + x, fy + y, woodD);              // its frame
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                        if (InArch(x, y, dw, dh)) p.Set(dx + x, fy + y, x % 4 == 0 ? Mul(wood, 0.8f) : Lerp(wood, woodL, 0.2f + 0.1f * ((x / 4) % 2)));
                p.Rect(dx, fy + 5, dw, 2, iron); p.Rect(dx, fy + 15, dw, 2, iron);                         // hinges
                p.Rect(dx + dw - 5, fy + 9, 3, 3, ironL); p.Set(dx + dw - 4, fy + 10, iron);               // the ring
                Shade(dx + dw + 2, fy, 2, dh + 1, 0.8f);
                p.Rect(dx - 2, fy - 1, dw + 4, 2, C(96, 80, 120)); p.Rect(dx - 2, fy - 1, dw + 4, 1, C(70, 58, 92));      // the mat
            }

            // a framed picture: the dog and its person, side by side under a moon (with its shadow on the wall)
            {
                int px0 = 52, py0 = fy + 42;
                Shade(px0 + 2, py0 - 2, 22, 18, 0.72f);
                p.Rect(px0, py0, 22, 18, woodD); p.Rect(px0 + 1, py0 + 1, 20, 16, wood); p.Rect(px0 + 2, py0 + 2, 18, 14, C(52, 62, 104));
                p.Rect(px0 + 15, py0 + 11, 3, 3, cream);
                p.Rect(px0 + 2, py0 + 2, 18, 3, C(196, 206, 232));
                p.Rect(px0 + 5, py0 + 5, 3, 6, C(70, 110, 120)); p.Rect(px0 + 5, py0 + 11, 3, 2, C(232, 196, 160)); p.Rect(px0 + 4, py0 + 9, 5, 1, red);
                p.Rect(px0 + 10, py0 + 5, 6, 3, C(176, 124, 72)); p.Rect(px0 + 14, py0 + 7, 3, 3, C(176, 124, 72));
            }
            // a name board over the basket, a bone painted on it
            {
                int bx = 106, by = fy + 42;
                p.Line(bx + 3, by + 9, bx + 14, by + 16, iron); p.Line(bx + 25, by + 9, bx + 14, by + 16, iron); p.Rect(bx + 13, by + 16, 3, 2, ironL);
                Shade(bx + 2, by - 2, 28, 9, 0.72f);
                p.Rect(bx, by, 28, 9, woodL); p.Rect(bx, by, 28, 1, woodD); p.Rect(bx, by + 8, 28, 1, Lerp(woodL, cream, 0.3f));
                p.Rect(bx + 9, by + 4, 10, 2, cream); p.Rect(bx + 7, by + 2, 3, 2, cream); p.Rect(bx + 7, by + 6, 3, 2, cream); p.Rect(bx + 18, by + 2, 3, 2, cream); p.Rect(bx + 18, by + 6, 3, 2, cream);
            }
            // a shelf over the door, on two brackets: a jar of treats, a ball
            {
                int sy = fy + 36, sx0 = 18;
                Shade(sx0 + 1, sy - 3, 26, 3, 0.7f);
                p.Rect(sx0 + 3, sy - 5, 2, 5, woodD); p.Rect(sx0 + 21, sy - 5, 2, 5, woodD);
                p.Rect(sx0, sy, 26, 3, woodL); p.Rect(sx0, sy, 26, 1, woodD);
                p.Rect(sx0 + 4, sy + 3, 8, 10, C(150, 180, 196)); p.Rect(sx0 + 5, sy + 4, 6, 6, C(196, 150, 96)); p.Rect(sx0 + 3, sy + 12, 10, 2, red); p.Rect(sx0 + 5, sy + 5, 1, 6, C(210, 230, 240));
                p.Disc(sx0 + 19.5f, sy + 6.5f, 3.5f, red); p.Rect(sx0 + 16, sy + 6, 7, 1, cream);
            }
            // a lead and a red scarf on a peg
            {
                int gx = 80;
                Shade(gx + 1, fy + 38, 8, 22, 0.8f);
                p.Rect(gx, fy + 60, 6, 2, woodD);
                p.Rect(gx + 1, fy + 40, 1, 20, C(150, 60, 50)); p.Rect(gx, fy + 38, 3, 3, C(150, 60, 50));
                p.Rect(gx + 3, fy + 46, 5, 14, red); p.Rect(gx + 3, fy + 46, 5, 2, redD); p.Rect(gx + 5, fy + 42, 3, 4, red);
            }

            // the stove on its stone hearth, the stones of the wall behind it, its pipe up through the beams
            int sx = 148;
            {
                for (int y = fy + 2; y < fy + fh; y++)
                    for (int x = sx - 4; x < sx + 24; x++)
                    {
                        int row = (y - fy) / 7, off = (row % 2) * 5;
                        bool joint = (y - fy) % 7 == 0 || (x - sx + off + 40) % 10 == 0;
                        float n = Mathf.PerlinNoise(((x - sx + off + 40) / 10) * 2.3f + row, row * 1.3f);
                        p.Set(x, y, joint ? stoneD : Lerp(stoneD, stone, 0.55f + n * 0.4f));
                    }
                Shade(sx - 4, fy + 2, 1, fh - 2, 0.75f); Shade(sx + 23, fy + 2, 1, fh - 2, 0.75f);
                p.Rect(sx - 6, fy, 32, 3, stone); p.Rect(sx - 6, fy + 2, 32, 1, C(150, 144, 150));          // the hearth stone
                Shade(sx + 2, fy + 3, 20, 3, 0.6f);
                p.Rect(sx + 2, fy + 3, 2, 3, iron); p.Rect(sx + 16, fy + 3, 2, 3, iron);                    // its feet
                p.Rect(sx, fy + 6, 20, 16, iron); p.Rect(sx, fy + 6, 2, 16, ironL); p.Rect(sx + 18, fy + 6, 2, 16, Mul(iron, 0.7f));
                p.Rect(sx + 4, fy + 9, 12, 8, C(120, 40, 24)); p.Rect(sx + 5, fy + 10, 10, 5, glow); p.Rect(sx + 7, fy + 10, 5, 3, glowL);
                for (int x = sx + 5; x < sx + 15; x += 3) p.Rect(x, fy + 10, 1, 6, C(70, 30, 20));             // the bars of its window
                p.Rect(sx - 1, fy + 22, 22, 2, ironL);
                p.Rect(sx + 7, fy + 24, 6, fh - 24, iron); p.Rect(sx + 7, fy + 24, 1, fh - 24, ironL); p.Rect(sx + 6, fy + 48, 8, 2, ironL);
                p.Rect(sx - 13, fy, 6, 3, C(110, 72, 44)); p.Rect(sx - 13, fy + 3, 6, 3, C(132, 86, 52)); p.Rect(sx - 13, fy + 3, 6, 1, C(170, 120, 80));   // logs by it
            }

            // a lantern hanging from the ridge
            int lx = 96, ly = 66;
            p.Rect(lx, ly + 12, 1, fy + fh - ly - 12, iron);
            p.Rect(lx - 4, ly, 9, 11, iron); p.Rect(lx - 3, ly + 1, 7, 9, glow); p.Rect(lx - 1, ly + 3, 3, 5, glowL);
            p.Rect(lx - 5, ly + 10, 11, 2, ironL); p.Rect(lx - 3, ly - 2, 7, 2, ironL);

            // straw strewn on the floor, a rug, the bowls on their mat, a bone
            var rng = new System.Random(7);
            p.Rect(44, fy, 2, 1, strawD);
            p.Rect(62, fy - 1, 36, 2, C(84, 110, 100)); p.Rect(62, fy - 1, 36, 1, C(60, 80, 74));
            p.Rect(65, fy + 1, 12, 2, redD); p.Rect(64, fy + 3, 14, 3, red); p.Rect(65, fy + 6, 12, 1, C(140, 90, 50)); Shade(78, fy + 1, 2, 4, 0.8f);
            p.Rect(82, fy + 1, 12, 2, C(56, 82, 140)); p.Rect(81, fy + 3, 14, 3, C(80, 112, 176)); p.Rect(82, fy + 6, 12, 1, C(150, 190, 230)); Shade(95, fy + 1, 2, 4, 0.8f);
            p.Rect(100, fy, 6, 2, cream); p.Rect(98, fy, 2, 3, cream); p.Rect(106, fy, 2, 3, cream);

            // the basket: woven, round, a red plaid cushion in it, a blanket folded at its end - and its shadow on the panelling
            {
                float cx = 120f;
                Shade((int)cx - 17, fy + 1, 38, 12, 0.72f);
                p.Ellipse(cx, fy + 6f, 18f, 7f, C(150, 104, 60));
                p.Rect((int)cx - 18, fy, 36, 6, C(150, 104, 60));
                for (int x = (int)cx - 17; x < cx + 17; x += 3) p.Rect(x, fy, 1, 11, C(118, 80, 44));
                for (int y = fy + 2; y < fy + 11; y += 3) p.Rect((int)cx - 17, y, 34, 1, C(170, 124, 76));
                p.Rect((int)cx - 15, fy + 9, 30, 4, red);
                for (int x = (int)cx - 15; x < cx + 15; x += 5) p.Rect(x, fy + 9, 1, 4, redD);
                p.Rect((int)cx - 15, fy + 11, 30, 1, redD);
                p.Rect((int)cx - 19, fy + 10, 4, 3, C(176, 130, 80)); p.Rect((int)cx + 15, fy + 10, 4, 3, C(176, 130, 80));      // the rim
                p.Rect((int)cx + 9, fy + 12, 8, 3, cream); p.Rect((int)cx + 9, fy + 12, 8, 1, C(196, 176, 140));
            }
            for (int i = 0; i < 46; i++) { int x = rng.Next(fx + 26, fx + fw - 34), w = rng.Next(2, 5); p.Rect(x, fy - 1 + rng.Next(0, 2), w, 1, i % 2 == 0 ? straw : strawD); }

            // the light: warm round the lantern and the stove, the corners dim and blue
            for (int y = 0; y < KennelRoomH; y++)
                for (int x = 0; x < KennelRoomW; x++)
                {
                    var c = p.Get(x, y);
                    if (c.a == 0) continue;
                    float d1 = Vector2.Distance(new Vector2(x, y * 1.15f), new Vector2(lx, (ly + 5) * 1.15f)), d2 = Vector2.Distance(new Vector2(x, y), new Vector2(sx + 10, fy + 14));
                    float l = Mathf.Clamp01(Mathf.Clamp01(1.12f - d1 / 92f) * 0.95f + Mathf.Clamp01(1f - d2 / 58f) * 0.65f);
                    var darkC = Lerp(Mul(c, 0.4f), C(26, 28, 52), 0.25f);
                    var warmC = Lerp(c, C(255, 196, 130), 0.14f);
                    p.Set(x, y, Lerp(darkC, warmC, l));
                }
            Save(p, d + "kennel_room", PCenter);
        }

        /// <summary>
        /// The two little dolls in the dog's basket, hand in hand (DollsPickup): a clay one with a round peach face, a
        /// green shirt and brown trousers, and a wooden one with pegged joints, a red-brown bun and a violet dress.
        /// Their picture on a tile for the achievement "It Takes Two" (24 px, like the others).
        /// </summary>
        static void DollsArt(string d)
        {
            Px Dolls()
            {
                string[] rows =
                {
                    "..hhh....rrr..",
                    ".hhhhh..rrrrr.",
                    "hsssssh.rwwwwr",
                    "sksssksrwkwwkw",
                    ".psssp...pwwp.",
                    "..sss.....ww..",
                    ".ggggg..vvvvv.",
                    "sgggggswvvvvvw",
                    ".ggggg..vvvvv.",
                    ".ttttt.vvvvvvv",
                    ".t...t..w...w.",
                    ".n...n..n...n.",
                };
                var pal = new System.Collections.Generic.Dictionary<char, Color32>
                {
                    { 'h', C(96, 60, 40) }, { 's', C(236, 190, 150) }, { 'k', C(30, 20, 24) }, { 'g', C(80, 150, 96) },
                    { 't', C(124, 90, 62) }, { 'w', C(204, 156, 104) }, { 'r', C(150, 72, 52) }, { 'v', C(132, 96, 182) },
                    { 'p', C(240, 140, 140) }, { 'n', C(60, 40, 36) },
                };
                var o = new Px(rows[0].Length, rows.Length);
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        if (pal.TryGetValue(rows[y][x], out var col)) o.Set(x, rows.Length - 1 - y, col);
                return o;
            }
            {
                var q = Padded(Dolls(), 2);
                q.Outline(C(40, 26, 30));
                SaveWithRing(q, d + "dolls", new Vector2(0.5f, 2f / q.h));
            }
            {
                var p = new Px(24, 24);
                p.Rect(0, 0, 24, 24, C(196, 156, 70)); p.Rect(1, 1, 22, 22, C(48, 26, 44)); p.Rect(1, 22, 22, 1, C(76, 42, 70));
                var o = Padded(Dolls(), 1); o.Outline(C(40, 26, 30));
                int ox = (24 - o.w) / 2, oy = 3;
                for (int y = 0; y < o.h; y++)
                    for (int x = 0; x < o.w; x++) { var c = o.Get(x, y); if (c.a != 0) p.Set(ox + x, oy + y, c); }
                // a little heart over their joined hands
                Color32 pink = C(255, 120, 150);
                p.Rect(10, 20, 2, 1, pink); p.Rect(13, 20, 2, 1, pink); p.Rect(10, 19, 5, 1, pink); p.Rect(11, 18, 3, 1, pink); p.Set(12, 17, pink);
                Save(p, "UI/ach_" + Achievements.ItTakesTwo, PCenter);
            }
        }
    }
}
