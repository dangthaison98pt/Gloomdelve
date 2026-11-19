using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Sandsunk Tombs: quicksand, the spear plate, its three creatures (scarab, mummy, sand worm), the Dune
    /// Queen and her spit, and the two things a shop sells for this place (Sand Striders, Warding Scarab).
    /// All creatures face right in the art.
    /// </summary>
    public static partial class ArtGenerator
    {
        static void SandsunkArt()
        {
            Color32 ol = C(34, 24, 18);
            Color32 sand = C(226, 190, 118), sandD = C(176, 136, 78), sandL = C(250, 228, 168), sandDeep = C(128, 92, 52);

            // quicksand: a pool of loose sand let into the floor; slow ripples run across it
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(16, 10);
                p.Rect(0, 0, 16, 9, sandD); p.Rect(0, 0, 16, 2, sandDeep);
                p.Rect(0, 7, 16, 2, sand);
                for (int x = (f == 0 ? 1 : 5); x < 16; x += 8) { p.Rect(x, 8, 4, 1, sandL); p.Rect(x + 1, 5, 3, 1, sand); p.Set(x + 2, 3, sand); }
                p.Set(f == 0 ? 11 : 3, 9, sandL);
                Save(p, $"Sprites/Props/quicksand_{f}", new Vector2(0.5f, 0.7f));
            }

            // the slab of stone that rises out of the sand in the Queen's chamber: two tiles wide, one high
            {
                var p = new Px(32, 16);
                Color32 st = C(168, 142, 104), stD = C(116, 94, 66), stL = C(214, 190, 146), carve = C(92, 72, 50);
                p.Rect(0, 0, 32, 16, st); p.Rect(0, 0, 32, 3, stD); p.Rect(0, 13, 32, 3, stL); p.Rect(0, 15, 32, 1, C(240, 222, 180));
                p.Rect(0, 0, 1, 16, stD); p.Rect(31, 0, 1, 16, stD); p.Rect(15, 3, 1, 10, stD); p.Rect(16, 3, 1, 10, stL);
                for (int i = 0; i < 2; i++)
                {
                    int x = 4 + i * 16;
                    p.Rect(x, 5, 8, 1, carve); p.Rect(x, 10, 8, 1, carve); p.Rect(x, 5, 1, 6, carve); p.Rect(x + 7, 5, 1, 6, carve);
                    p.Disc(x + 3.5f, 7.6f, 1.4f, carve);                       // a worn sun in each panel
                }
                p.Outline(ol);
                p.Rect(0, 0, 32, 1, sandD); for (int x = 2; x < 32; x += 5) p.Set(x, 1, sand);          // sand clinging to its foot
                Save(p, "Sprites/Props/sandpillar", new Vector2(0.5f, 0f));
                // coming up out of the sand: its top first, two rows more in each picture (the eighth is the whole block)
                for (int n = 1; n <= 8; n++)
                {
                    int h = n * 2;
                    var q = new Px(32, h);
                    for (int y = 0; y < h; y++) for (int x = 0; x < 32; x++) q.Set(x, y, p.Get(x, 16 - h + y));
                    if (n < 8) { q.Rect(0, 0, 32, 1, sandD); for (int x = 2; x < 32; x += 5) q.Set(x, 1, sand); }
                    Save(q, $"Sprites/Props/sandpillar_r{n}", new Vector2(0.5f, 0f));
                }
            }

            // spear plate: 0 down (only the slotted plate), 1 the tips show, 2 the spears stand
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(16, 22);
                Color32 stone = C(150, 124, 88), stoneD = C(98, 78, 54), steel = C(206, 210, 220), steelD = C(120, 126, 140), shaft = C(126, 88, 50);
                p.Rect(0, 0, 16, 2, stone); p.Rect(0, 0, 16, 1, stoneD);
                for (int i = 0; i < 4; i++)
                {
                    int x = 2 + i * 4;
                    p.Rect(x, 1, 2, 1, C(30, 22, 18));                       // the slot
                    int h = f == 0 ? 0 : f == 1 ? 4 : 19;
                    if (h == 0) continue;
                    if (h > 5) p.Rect(x, 2, 2, h - 5, shaft);
                    int top = 2 + Mathf.Max(0, h - 5);
                    p.Rect(x, top, 2, 3, steel); p.Set(x, top, steelD); p.Rect(x, top + 3, 1, 2, steel); p.Set(x + 1, top + 3, steelD);
                }
                Save(p, $"Sprites/Traps/spear_{f}", new Vector2(0.5f, 0f));
            }

            // scarab: a low, glossy beetle. 0 / 1 legs
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(18, 18);
                Color32 shell = C(40, 92, 112), shellD = C(22, 54, 72), shellL = C(110, 196, 206), gold = C(236, 190, 80);
                p.Ellipse(8.5f, 4f, 5.2f, 3.2f, shell); p.Ellipse(8.5f, 3f, 5f, 1.6f, shellD);
                p.Line(8, 2, 8, 7, shellD);
                p.Rect(13, 3, 2, 3, shellD); p.Set(15, 4, gold); p.Set(15, 5, gold);       // head and horns
                p.Set(6, 6, shellL); p.Set(7, 6, shellL); p.Set(10, 5, shellL);
                for (int i = 0; i < 3; i++) { int x = 5 + i * 3; p.Set(x + (f == 0 ? 0 : 1), 0, shellD); p.Set(x, 1, shellD); }
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/scarab_{f}", new Vector2(0.5f, 1f / 18f));
            }

            // mummy: wrapped head to foot, one eye glowing. 0 / 1 walk, 2 grab (arms out)
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(20, 22);
                Color32 wrap = C(214, 204, 172), wrapD = C(154, 142, 112), wrapL = C(242, 236, 214), eye = C(120, 255, 190);
                int step = f == 1 ? 1 : 0;
                p.Rect(6, 0, 3, 5 - step, wrap); p.Rect(10, 0, 3, 4 + step, wrap);                  // legs
                p.Rect(5, 5, 9, 9, wrap); p.Rect(5, 5, 2, 9, wrapD);                                // body
                p.Rect(6, 14, 7, 6, wrap); p.Rect(6, 14, 2, 6, wrapD);                              // head
                for (int y = 2; y < 20; y += 3) p.Line(5, y, 13, y + 1, wrapD);                     // the windings
                p.Rect(10, 16, 2, 2, C(20, 16, 20)); p.Set(11, 17, eye);
                if (f == 2) { p.Rect(13, 10, 6, 2, wrap); p.Rect(13, 7, 5, 2, wrapD); p.Set(19, 10, wrapL); p.Set(18, 7, wrapL); }
                else { p.Rect(13, 6 + step, 2, 6, wrap); p.Rect(3, 7 - step, 2, 5, wrapD); }
                p.Line(4, 4, 2, 1 + step, wrapD);                                                   // a loose end trailing
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/mummy_{f}", new Vector2(0.5f, 1f / 22f));
            }

            // the length of wrapping a mummy casts: a strip of linen, its end frayed
            {
                var p = new Px(14, 5);
                Color32 wrap = C(222, 212, 180), wrapD = C(160, 148, 118), wrapL = C(246, 240, 220);
                p.Rect(0, 1, 12, 3, wrap); p.Rect(0, 1, 12, 1, wrapD); p.Rect(2, 3, 8, 1, wrapL);
                for (int x = 2; x < 12; x += 3) p.Set(x, 2, wrapD);
                p.Set(12, 3, wrap); p.Set(13, 2, wrap); p.Set(12, 1, wrapD);
                p.Outline(ol);
                Save(p, "Sprites/Enemies/mummywrap", PCenter);
            }

            // sand worm: 0 the mound (all that shows while it waits), 1 / 2 out of the ground, swaying
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 28);
                Color32 hide = C(196, 132, 84), hideD = C(136, 84, 52), hideL = C(238, 188, 130), mouth = C(120, 30, 40), tooth = C(250, 244, 220);
                if (f == 0)
                {
                    p.Ellipse(9f, 1.5f, 6f, 2.6f, sand); p.Ellipse(9f, 0.8f, 6f, 1.2f, sandD);
                    p.Set(8, 4, sandL); p.Set(11, 3, sandL);
                    p.Outline(ol);
                }
                else
                {
                    int lean = f == 1 ? 0 : 1;
                    for (int y = 0; y < 21; y++)
                    {
                        int cx = 8 + (y > 12 ? lean : 0) + (y > 17 ? lean : 0);
                        p.Rect(cx - 3, y, 7, 1, y % 4 == 3 ? hideD : hide);
                        p.Set(cx - 3, y, hideD); p.Set(cx + 2, y, hideL);
                    }
                    int hx = 8 + lean * 2;
                    p.Ellipse(hx + 0.5f, 22f, 4.6f, 3.6f, hide);
                    p.Rect(hx + 1, 21, 4, 3, mouth); p.Set(hx + 2, 24, tooth); p.Set(hx + 4, 24, tooth); p.Set(hx + 2, 20, tooth); p.Set(hx + 4, 20, tooth);
                    p.Set(hx - 1, 24, C(20, 16, 20));
                    p.Outline(ol);
                    p.Ellipse(9f, 0.8f, 6.5f, 1.4f, sandD); p.Rect(3, 0, 12, 1, sandDeep);        // the sand it stands in
                }
                Save(p, $"Sprites/Enemies/sandworm_{f}", new Vector2(0.5f, 1f / 28f));
            }

            // the Dune Queen: 0 risen, 1 rearing (jaws open), 2 her mound. A worm as thick as a pillar: armour and
            // spines down her back, a crest of spines tipped with gold, two eyes, jaws with long fangs.
            for (int f = 0; f < 3; f++)
            {
                var p = Framed(62, 66);
                Color32 hide = C(206, 142, 82), hideD = C(140, 88, 50), hideL = C(246, 200, 130), plate = C(112, 70, 60), plateL = C(164, 108, 86), plateD = C(74, 44, 40),
                        mouth = C(110, 22, 36), mouthD = C(60, 10, 22), tooth = C(252, 246, 222), eye = C(255, 214, 80), gold = C(250, 204, 70);
                if (f == 2)
                {
                    // under the sand: only the spines of her back cut through it, a little wake of sand to either side
                    // (nothing broad or flat - it must never be taken for something to stand on)
                    p.Rect(14, 0, 36, 1, sandD); p.Rect(17, 1, 30, 2, sand); p.Rect(20, 3, 24, 1, sandL); p.Set(12, 2, sandL); p.Set(50, 2, sandL); p.Set(10, 1, sandL); p.Set(53, 1, sandL);
                    int[] sx = { 19, 27, 36 }, sh = { 12, 19, 13 };
                    for (int i = 0; i < 3; i++)
                        for (int y = 0; y < sh[i]; y++)
                        {
                            int w = y < sh[i] - 8 ? 7 : y < sh[i] - 5 ? 5 : y < sh[i] - 2 ? 3 : 1;
                            p.Rect(sx[i] + y / 3, 2 + y, w, 1, y % 3 == 2 ? plateL : plate);
                            if (w > 2) p.Set(sx[i] + y / 3, 2 + y, plateD);
                        }
                    p.Outline(ol);
                }
                else
                {
                    int lean = f == 1 ? 4 : 0;
                    for (int y = 0; y < 46; y++)
                    {
                        float k = y / 46f;
                        int cx = 28 + Mathf.RoundToInt(lean * k * k);
                        int hw = 12 - Mathf.RoundToInt(k * 3f);
                        bool seam = y % 7 == 6;
                        // (flat: her hide one tone with a seam at every ring, light down her belly, armour down her back)
                        p.Rect(cx - hw, y, hw * 2 + 1, 1, seam ? hideD : hide);
                        if (!seam) p.Rect(cx + hw - 2, y, 3, 1, hideL);
                        p.Rect(cx - hw, y, 5, 1, seam ? plateD : plate);
                        if (y % 7 == 3 && y > 4) { p.Rect(cx - hw - 3, y, 3, 2, plate); p.Set(cx - hw - 4, y + 1, plateL); }     // a spine on every ring
                    }
                    int hx = 28 + lean;
                    p.Ellipse(hx + 2f, 51f, 14f, 9f, hide);
                    p.Ellipse(hx - 4f, 54f, 9f, 7f, plate);
                    // her crest: spines tipped with gold
                    Bar(p, hx - 1, 59, hx - 4, 64, 1, plate); p.Set(hx - 5, 65, gold); p.Set(hx - 4, 65, gold);
                    Bar(p, hx - 8, 58, hx - 14, 63, 1, plate); p.Set(hx - 15, 64, gold); p.Set(hx - 16, 64, gold);
                    Bar(p, hx + 5, 59, hx + 5, 63, 1, plate); p.Set(hx + 5, 65, gold); p.Set(hx + 6, 65, gold);
                    // jaws
                    int open = f == 1 ? 10 : 3;
                    int my = 46 - open / 2;
                    p.Rect(hx + 5, my, 13, open + 2, mouth); p.Rect(hx + 5, my, 4, open + 2, mouthD);
                    for (int i = 0; i < 3; i++) { p.Rect(hx + 7 + i * 3, my + open, 2, 2, tooth); p.Rect(hx + 7 + i * 3, my, 2, 2, tooth); }
                    Bar(p, hx + 16, my + open + 1, hx + 20, my + open - 2, 0, tooth); p.Line(hx + 17, my + open + 1, hx + 21, my + open - 2, tooth);       // fangs
                    Bar(p, hx + 16, my, hx + 20, my + 3, 0, tooth); p.Line(hx + 17, my, hx + 21, my + 3, tooth);
                    p.Outline(ol);
                    // eyes (after the outline: they shine)
                    p.Disc(hx + 4.5f, 54.5f, 2.4f, eye); p.Rect(hx + 4, 53, 1, 4, C(40, 20, 20)); p.Set(hx + 6, 56, C(255, 250, 220));
                    p.Disc(hx + 11.5f, 53.5f, 1.5f, eye); p.Set(hx + 11, 53, C(40, 20, 20));
                    p.Line(hx + 1, 58, hx + 8, 57, ol);
                    p.Ellipse(29f, 1.4f, 21f, 2.6f, sandD); p.Rect(9, 0, 41, 1, sandDeep);              // the sand she stands in
                    p.Set(11, 3, sandL); p.Set(47, 3, sandL); p.Set(8, 2, sand); p.Set(51, 2, sand);
                }
                Save(p, $"Sprites/Bosses/dunequeen_{f}", FramedPivot(62, 66, 31, 1));
            }

            // her spit: a clod of wet sand
            {
                var p = new Px(9, 9);
                p.Disc(4f, 4f, 3.4f, sandD); p.Disc(3.6f, 4.4f, 2.2f, sand); p.Set(3, 5, sandL); p.Set(6, 2, sandDeep);
                p.Outline(ol);
                Save(p, "Sprites/Bosses/sandshot", PCenter);
            }

            // Sand Striders: wide woven soles on a boot
            {
                var p = ItemCanvas();
                Color32 leather = C(170, 112, 62), leatherD = C(112, 70, 40), weave = C(232, 206, 140), weaveD = C(176, 146, 86);
                p.Rect(5, 4, 5, 8, leather); p.Rect(5, 4, 2, 8, leatherD); p.Rect(5, 3, 8, 3, leather); p.Rect(11, 3, 2, 2, leatherD);
                p.Rect(1, 1, 14, 2, weave); p.Rect(1, 1, 14, 1, weaveD);
                for (int x = 2; x < 15; x += 3) p.Set(x, 2, weaveD);
                p.Rect(6, 11, 3, 1, weave);
                p.Outline(ol);
                Save(p, "Sprites/Items/sandstriders", PItem);
            }
            // Warding Scarab: a blue-green scarab amulet on a cord, an eye on its back
            {
                var p = ItemCanvas();
                Color32 shell = C(46, 150, 150), shellD = C(26, 90, 100), shellL = C(150, 236, 220), gold = C(240, 196, 80), cord = C(150, 110, 70);
                p.Line(4, 14, 8, 11, cord); p.Line(12, 14, 8, 11, cord);
                p.Ellipse(8f, 6f, 5f, 4.6f, shell); p.Ellipse(8f, 4f, 4.6f, 2f, shellD);
                p.Line(8, 2, 8, 10, shellD);
                p.Rect(6, 6, 5, 2, gold); p.Set(8, 6, C(30, 30, 40)); p.Set(8, 7, C(30, 30, 40));
                p.Set(5, 9, shellL); p.Set(6, 9, shellL);
                p.Outline(ol);
                Save(p, "Sprites/Items/wardscarab", PItem);
            }
        }
    }
}
