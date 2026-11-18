using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Brindlemoor's street and rooms. Building numbers: 0 forge, 1 ropewalk, 2 apothecary, 3 map room,
    /// 4 Pip's Oddments, 5 lamp house, 6 village hall, 7 the player's house.
    ///  - Fronts ("Sprites/Village/front_N_0" dark, "_1" lit): 112 x 128 px, a tall town house seven tiles wide,
    ///    made to stand shoulder to shoulder with its neighbours. Door in the middle at street level.
    ///  - Rooms ("Sprites/Village/room_N"): 544 x 304 px - the inside of the house, two storeys joined by a ladder
    ///    (16 x 15 tiles with its walls), in the middle of a dark surround as large as the camera's view.
    /// </summary>
    public static partial class ArtGenerator
    {
        const int FrontW = 112, FrontH = 128;

        static readonly Color32 VSnow = C(198, 208, 232), VSnowD = C(150, 164, 200), VBeam = C(58, 38, 30), VBeamL = C(92, 62, 44),
            VStone = C(104, 100, 112), VStoneD = C(72, 70, 84), VGlassOff = C(26, 30, 50), VGlassOffL = C(40, 46, 72),
            VGlow = C(232, 164, 86), VGlowL = C(246, 198, 120), VGlowD = C(172, 100, 58), VInk = C(232, 212, 156),
            VNight = C(52, 58, 96);       // everything on the street is seen by night: colours are pulled towards this

        static void VillageArt()
        {
            const string d = "Sprites/Village/";
            for (int k = 0; k < 8; k++)
            {
                Save(Front(k, false), d + "front_" + k + "_0", new Vector2(0.5f, 0f));
                Save(Front(k, true), d + "front_" + k + "_1", new Vector2(0.5f, 0f));
                Save(Room(k), d + "room_" + k, PCenter);
            }
            StreetArt(d);
            for (int f = 0; f < 4; f++) Save(FlameFrame(f), d + "fire_" + f, new Vector2(0.5f, 0f));
            foreach (bool icon in new[] { false, true })
            {
                // an ember of the Sunstone: a shard that still burns (the pickup, and its picture on the HUD)
                int S = icon ? 16 : 12, o = icon ? 2 : 0;
                var p = new Px(S, S);
                Color32 hot = C(255, 244, 200), mid = C(255, 176, 70), rim = C(226, 96, 40), dark = C(120, 40, 30);
                int[] half = { 1, 2, 3, 4, 4, 5, 5, 4, 3, 2, 2, 1 };
                for (int y = 0; y < 12; y++)
                {
                    int h = half[y], cx = 6 + (y > 7 ? 1 : 0);
                    p.Rect(o + cx - h, o + y, h * 2, 1, rim);
                    if (h > 1) p.Rect(o + cx - h + 1, o + y, h * 2 - 2, 1, mid);
                    if (h > 3) p.Rect(o + cx - 1, o + y, 2, 1, hot);
                    p.Set(o + cx - h, o + y, dark);
                }
                p.Set(o + 6, o + 5, C(255, 255, 240));
                Save(p, icon ? "UI/icon_ember" : "Sprites/Items/ember", PCenter);
            }
            {
                // the Recall Charm: a small stone on a cord with a spiral cut into it that glows
                var p = new Px(16, 16);
                Color32 cord = C(150, 110, 70), st = C(110, 112, 128), stD = C(74, 76, 94), stL = C(150, 152, 170), rune = C(130, 240, 255), runeL = C(220, 252, 255);
                p.Rect(4, 13, 8, 1, cord); p.Rect(3, 11, 1, 2, cord); p.Rect(12, 11, 1, 2, cord); p.Rect(7, 14, 2, 1, cord);
                p.Rect(4, 1, 8, 10, st); p.Rect(3, 3, 10, 6, st); p.Rect(4, 1, 8, 1, stD); p.Rect(11, 3, 2, 6, stD); p.Rect(4, 9, 3, 1, stL); p.Rect(3, 5, 1, 3, stL);
                p.Rect(5, 8, 6, 1, rune); p.Rect(5, 4, 1, 4, rune); p.Rect(5, 3, 5, 1, rune); p.Rect(10, 4, 1, 3, rune); p.Rect(7, 6, 3, 1, rune);
                p.Set(7, 6, runeL); p.Set(5, 8, runeL);
                Save(p, "UI/icon_recall", PCenter);
            }
            {
                // the moon over Brindlemoor: a crescent, the rest of the disc only just showing, a thin halo round it
                var p = new Px(48, 48);
                Color32 bright = C(238, 240, 250), mid = C(206, 212, 234), dim = C(74, 84, 130);
                for (int y = 0; y < 48; y++)
                    for (int x = 0; x < 48; x++)
                    {
                        float dx = x - 23.5f, dy = y - 23.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                        float sx = x - 30.5f, sy = y - 26.5f, rs = Mathf.Sqrt(sx * sx + sy * sy);        // the shadow that cuts the crescent
                        if (r <= 13f)
                        {
                            if (rs <= 12.2f) p.Set(x, y, new Color32(dim.r, dim.g, dim.b, 110));
                            else p.Set(x, y, r > 11.6f ? mid : ((x * 5 + y * 3) % 17 == 0 ? mid : bright));
                        }
                        else if (r <= 21f) p.Set(x, y, new Color32(190, 204, 240, (byte)Mathf.RoundToInt(34f * (1f - (r - 13f) / 8f))));
                    }
                Save(p, "Sky/moon", PCenter);
            }
            {
                // stars: scattered points, a few larger ones with a little cross of light
                var p = new Px(480, 270);
                var rnd = new System.Random(777);
                for (int i = 0; i < 230; i++)
                {
                    int x = rnd.Next(480), y = 60 + (int)(210 * System.Math.Sqrt(rnd.NextDouble()));       // more of them high up
                    if (y >= 270) continue;
                    byte a = (byte)(90 + rnd.Next(150));
                    var c = rnd.Next(5) == 0 ? new Color32(255, 236, 200, a) : new Color32(214, 226, 255, a);
                    p.Set(x, y, c);
                    if (rnd.Next(14) == 0) { var c2 = new Color32(c.r, c.g, c.b, (byte)(a / 2)); p.Set(x - 1, y, c2); p.Set(x + 1, y, c2); p.Set(x, y - 1, c2); p.Set(x, y + 1, c2); }
                }
                Save(p, "Sky/stars", PCenter);
            }
        }

        // =============================================================================== fronts

        static void TradeIcon(Px p, int x, int y, int kind)
        {
            Color32 red = C(230, 70, 80), gold = C(250, 200, 70);
            switch (kind)
            {
                case 0: p.Rect(x + 1, y + 6, 7, 1, VInk); p.Rect(x + 2, y + 4, 5, 2, VInk); p.Rect(x + 3, y + 2, 3, 2, VInk); p.Rect(x + 1, y, 7, 2, VInk); break;               // anvil
                case 1: p.Rect(x + 1, y + 1, 7, 7, VInk); p.Rect(x + 3, y + 3, 3, 3, C(120, 84, 52)); p.Rect(x + 8, y, 1, 3, VInk); break;                                       // rope coil
                case 2: p.Rect(x + 1, y + 5, 3, 3, red); p.Rect(x + 5, y + 5, 3, 3, red); p.Rect(x + 1, y + 4, 7, 2, red); p.Rect(x + 2, y + 2, 5, 2, red); p.Rect(x + 4, y, 1, 2, red); break;   // heart
                case 3: p.Rect(x + 4, y, 1, 9, VInk); p.Rect(x, y + 4, 9, 1, VInk); p.Rect(x + 3, y + 3, 3, 3, VInk); p.Set(x + 4, y + 7, red); p.Set(x + 4, y + 8, red); break;   // compass
                case 4: p.Rect(x + 2, y, 5, 9, gold); p.Rect(x, y + 2, 9, 5, gold); p.Rect(x + 4, y + 2, 1, 5, C(160, 110, 30)); break;                                          // coin
                case 5: p.Rect(x + 2, y + 1, 5, 5, VGlowL); p.Rect(x + 2, y, 5, 1, VInk); p.Rect(x + 2, y + 6, 5, 1, VInk); p.Rect(x + 4, y + 7, 1, 2, VInk); break;            // lantern
                case 6: p.Rect(x + 1, y + 1, 7, 4, gold); p.Rect(x + 1, y + 5, 1, 3, gold); p.Rect(x + 4, y + 5, 1, 3, gold); p.Rect(x + 7, y + 5, 1, 3, gold); break;           // crown
                default: p.Rect(x + 1, y, 7, 5, VInk); for (int i = 0; i < 5; i++) p.Rect(x + i, y + 5 + i, 9 - i * 2, 1, VInk); break;                                         // house
            }
        }

        static void VWindow(Px p, int x, int y, int w, int h, bool lit, Color32 shutter, bool shutters = true, bool arch = false)
        {
            p.Rect(x - 2, y - 2, w + 4, h + 4, VBeam);
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    if (arch && yy >= h - 2 && (xx < 2 - (h - 1 - yy) + 1 || xx >= w - (2 - (h - 1 - yy)) - 1)) continue;
                    float cx = Mathf.Abs(xx - (w - 1) * 0.5f) / (w * 0.5f), cy = Mathf.Abs(yy - (h - 1) * 0.5f) / (h * 0.5f);
                    Color32 c = lit ? Lerp(VGlowL, VGlowD, Mathf.Clamp01(Mathf.Max(cx, cy) * 1.1f - 0.1f)) : ((xx + yy) % 7 == 0 ? VGlassOffL : VGlassOff);
                    p.Set(x + xx, y + yy, c);
                }
            if (lit && w >= 14)
            {
                // curtains drawn half back, and the dark shape of something on the sill
                Color32 curtain = C(150, 78, 60);
                for (int yy = 0; yy < h; yy++) { int cw = 2 + (h - 1 - yy) * 3 / h; p.Rect(x, y + yy, cw, 1, curtain); p.Rect(x + w - cw, y + yy, cw, 1, curtain); }
                p.Rect(x + w / 2 + 2, y, 3, 4, C(120, 70, 50));
            }
            p.Rect(x + w / 2 - 1, y, 2, h, VBeam); p.Rect(x, y + h / 2, w, 1, VBeam); p.Rect(x, y + h / 2 + h / 4, w, 1, Mul(VBeam, 1.2f));
            if (shutters)
            {
                p.Rect(x - 8, y - 1, 6, h + 2, shutter); p.Rect(x + w + 2, y - 1, 6, h + 2, shutter);
                for (int yy = 1; yy < h; yy += 3) { p.Rect(x - 7, y + yy, 4, 1, Mul(shutter, 0.7f)); p.Rect(x + w + 3, y + yy, 4, 1, Mul(shutter, 0.7f)); }
            }
            // sill with snow on it and an icicle or two
            p.Rect(x - 3, y - 4, w + 6, 2, VBeamL);
            p.Rect(x - 3, y - 2, w + 6, 1, VSnow);
            p.Set(x - 1, y - 5, VSnowD); p.Set(x + w - 2, y - 5, VSnowD); p.Set(x + w - 2, y - 6, VSnowD);
        }

        static void VLantern(Px p, int x, int y, bool lit)
        {
            p.Rect(x - 3, y + 8, 5, 1, VBeam); p.Rect(x, y + 6, 1, 2, VBeam);                 // bracket
            p.Rect(x - 2, y, 5, 6, VBeam);
            p.Rect(x - 1, y + 1, 3, 4, lit ? VGlowL : VGlassOff);
            if (lit) p.Set(x, y + 2, C(255, 226, 170));
            p.Rect(x - 2, y + 6, 5, 1, C(40, 28, 24));
        }

        static Px Front(int kind, bool lit)
        {
            var p = new Px(FrontW, FrontH);
            // what makes each house its own
            Color32[] walls = { C(118, 116, 128), C(226, 204, 160), C(236, 230, 214), C(112, 136, 176), C(222, 132, 112), C(92, 96, 124), C(214, 178, 122), C(150, 172, 150) };
            Color32[] roofs = { C(74, 76, 92), C(72, 122, 100), C(190, 78, 64), C(64, 80, 128), C(198, 96, 60), C(60, 66, 100), C(150, 60, 58), C(96, 104, 150) };
            Color32[] trims = { C(60, 62, 76), C(64, 104, 96), C(170, 60, 70), C(232, 220, 190), C(90, 150, 140), C(230, 190, 90), C(120, 44, 48), C(84, 110, 150) };
            int[] bodyHs = { 84, 90, 82, 94, 86, 96, 90, 80 };
            int[] roofKinds = { 2, 1, 0, 1, 0, 1, 0, 1 };          // 0 front gable, 1 ridge with a dormer, 2 stepped gable
            bool[] timbered = { false, true, false, false, true, false, true, true };
            Color32 wall = Lerp(walls[kind], VNight, 0.34f), wallD = Mul(wall, 0.8f), roof = Lerp(roofs[kind], VNight, 0.3f), roofD = Mul(roof, 0.72f), trim = Lerp(trims[kind], VNight, 0.25f);
            int bodyH = bodyHs[kind], split = 44;
            bool lights = lit;

            // ---- body
            p.Rect(1, 0, FrontW - 2, bodyH, wall);
            p.Rect(FrontW - 7, 0, 6, bodyH, wallD);
            for (int y = 9; y < bodyH; y += 6)                                             // a hint of plaster / stone courses
                for (int x = 4 + (y % 12 == 3 ? 3 : 0); x < FrontW - 8; x += 13) p.Rect(x, y, 4, 1, wallD);
            p.Rect(1, 0, FrontW - 2, 5, VStone); p.Rect(1, 4, FrontW - 2, 1, VStoneD);
            for (int x = 6; x < FrontW; x += 12) p.Rect(x, 0, 1, 4, VStoneD);
            if (timbered[kind])
            {
                foreach (int bx in new[] { 1, 37, 72, FrontW - 4 }) p.Rect(bx, split, 3, bodyH - split, VBeam);
                p.Rect(1, bodyH - 4, FrontW - 2, 3, VBeam);
                for (int i = 0; i < 14; i++) { p.Rect(41 + i * 2, split + 4 + i * 2, 2, 2, VBeam); p.Rect(69 - i * 2, split + 4 + i * 2, 2, 2, VBeam); }
            }
            p.Rect(1, split, FrontW - 2, 4, VBeam); p.Rect(1, split + 3, FrontW - 2, 1, VBeamL);       // the floor between the storeys
            p.Rect(0, 0, 1, bodyH, C(30, 22, 24)); p.Rect(FrontW - 1, 0, 1, bodyH, C(30, 22, 24));     // party walls

            // ---- upstairs
            if (kind == 3) VWindow(p, 44, split + 14, 24, 26, lights, trim, false, true);                  // the map room's one great window
            else
            {
                VWindow(p, 16, split + 13, 18, 22, lights, trim);
                VWindow(p, 78, split + 13, 18, 22, lights, trim);
            }
            if (kind == 5) for (int i = 0; i < 3; i++) VLantern(p, 46 + i * 10, split + 16 + (i == 1 ? 6 : 0), lights);   // the lamp house hangs lamps upstairs too
            if (kind == 6) { p.Rect(50, split + 10, 12, 30, C(150, 44, 50)); p.Rect(50, split + 10, 12, 2, VBeam); p.Set(50, split + 10, Clear); TradeIcon(p, 52, split + 22, 6); p.Rect(50, split + 40, 12, 1, C(250, 200, 70)); }

            // a balcony across the upper storey on some houses: a wooden rail with snow on it
            if (kind == 1 || kind == 2 || kind == 4 || kind == 7)
            {
                int by = split + 4;
                p.Rect(6, by, FrontW - 12, 2, VBeamL); p.Rect(6, by - 1, FrontW - 12, 1, VBeam);
                for (int x = 8; x < FrontW - 8; x += 5) p.Rect(x, by + 2, 2, 8, VBeam);
                p.Rect(6, by + 10, FrontW - 12, 2, VBeamL); p.Rect(6, by + 12, FrontW - 12, 1, VSnow);
                p.Rect(5, by - 1, 2, 14, VBeam); p.Rect(FrontW - 7, by - 1, 2, 14, VBeam);
                foreach (int bx in new[] { 10, FrontW - 14 }) { p.Rect(bx, by - 5, 2, 4, VBeam); p.Rect(bx + 2, by - 4, 1, 1, VBeam); }     // brackets under it
                // a blanket left out to air, stiff with frost
                p.Rect(52, by + 3, 12, 9, Lerp(C(214, 190, 170), VNight, 0.25f)); p.Rect(52, by + 5, 12, 1, Lerp(C(190, 90, 90), VNight, 0.2f)); p.Rect(52, by + 9, 12, 1, Lerp(C(190, 90, 90), VNight, 0.2f));
            }
            // a garland of fir along the floor beam on every house, with berries - and small lights when someone is home
            for (int x = 3; x < FrontW - 3; x++)
            {
                int sag = (int)(Mathf.Abs(Mathf.Sin((x - 3) / 17.7f * Mathf.PI)) * 3f);
                Color32 fir = (x % 3 == 0) ? C(44, 92, 66) : C(58, 116, 80);
                p.Set(x, split - 1 - sag, fir); p.Set(x, split - 2 - sag, Mul(fir, 0.8f));
                if (x % 9 == 4) p.Set(x, split - 2 - sag, C(200, 60, 64));
                if (lit && x % 9 == 0) p.Set(x, split - 3 - sag, x % 18 == 0 ? VGlowL : C(240, 150, 120));
            }

            // ---- street level: door in the middle, shop window to the left, a small window to the right
            int dx = 48, dw = 16, dh = 30;
            p.Rect(dx - 2, 0, dw + 4, dh + 3, VBeam);
            Color32 doorC = kind == 2 ? C(160, 54, 62) : kind == 6 ? C(120, 76, 44) : kind == 7 ? C(70, 100, 130) : C(98, 62, 40);
            p.Rect(dx, 0, dw, dh, doorC);
            for (int x = dx + 3; x < dx + dw; x += 4) p.Rect(x, 0, 1, dh, Mul(doorC, 0.75f));
            p.Rect(dx, dh - 1, dw, 1, Mul(doorC, 0.6f)); p.Set(dx, dh - 1, VBeam); p.Set(dx + dw - 1, dh - 1, VBeam);
            p.Rect(dx + 4, dh - 11, 8, 7, VBeam); p.Rect(dx + 5, dh - 10, 6, 5, lights ? VGlow : VGlassOff);    // a pane in the door
            p.Set(dx + dw - 3, 13, C(236, 200, 110)); p.Set(dx + dw - 3, 12, C(170, 130, 60));
            p.Rect(dx - 3, 0, dw + 6, 2, VStone); p.Rect(dx - 3, 2, dw + 6, 1, VSnow);                          // doorstep
            if (kind == 7) { p.Rect(dx + 5, dh - 18, 6, 6, C(60, 130, 70)); p.Rect(dx + 7, dh - 16, 2, 2, doorC); p.Set(dx + 8, dh - 19, C(220, 60, 60)); }   // a wreath at home
            VLantern(p, dx - 6, 26, lights);

            if (kind == 6) { VWindow(p, 12, 12, 22, 22, lights, trim, false, true); VWindow(p, 78, 12, 22, 22, lights, trim, false, true); }
            else if (kind == 7) { VWindow(p, 14, 12, 18, 20, lights, trim); VWindow(p, 80, 12, 18, 20, lights, trim); }
            else
            {
                // the shop window: goods on show
                p.Rect(6, 7, 36, 28, VBeam);
                for (int y = 0; y < 24; y++) for (int x = 0; x < 32; x++) p.Set(8 + x, 9 + y, lights ? Lerp(VGlow, VGlowD, 0.25f + y / 40f + Mathf.Abs(x - 16) / 50f) : VGlassOff);
                p.Rect(8, 20, 32, 1, VBeam);
                Color32 g1 = lights ? C(90, 60, 40) : C(18, 20, 34), g2 = lights ? C(140, 60, 60) : C(22, 24, 40), g3 = lights ? C(60, 100, 120) : C(22, 26, 42);
                switch (kind)
                {
                    case 0: p.Rect(12, 10, 2, 9, g3); p.Rect(10, 16, 6, 1, g3); p.Rect(20, 10, 8, 8, g1); p.Rect(22, 12, 4, 4, g3); p.Rect(32, 10, 2, 9, g3); p.Rect(31, 17, 4, 2, g1); p.Rect(12, 22, 12, 2, g3); p.Rect(28, 22, 9, 3, g1); break;
                    case 1: foreach (int cx in new[] { 11, 20, 30 }) { p.Rect(cx, 10, 7, 7, g1); p.Rect(cx + 2, 12, 3, 3, lights ? VGlowD : VGlassOff); } p.Rect(12, 22, 8, 6, g1); p.Rect(26, 22, 5, 8, g2); break;
                    case 2: for (int i = 0; i < 6; i++) { p.Rect(10 + i * 5, 10, 3, 6 + i % 2, i % 3 == 0 ? g2 : i % 3 == 1 ? g3 : C(90, 150, 90)); p.Rect(10 + i * 5, 22, 3, 5 + (i + 1) % 2, i % 2 == 0 ? g3 : g2); } break;
                    case 3: p.Rect(11, 22, 14, 9, lights ? C(236, 222, 190) : g1); p.Rect(13, 26, 8, 1, g1); p.Rect(13, 24, 5, 1, g1); p.Rect(28, 10, 8, 8, g3); p.Rect(31, 9, 2, 1, g1); p.Rect(12, 10, 10, 3, g1); break;
                    case 4: p.Rect(10, 10, 8, 7, g1); p.Rect(20, 10, 6, 9, g2); p.Rect(28, 10, 9, 5, g3); p.Rect(12, 22, 6, 8, g3); p.Rect(22, 22, 5, 5, g1); p.Rect(30, 22, 6, 7, g2); break;
                    case 5: for (int i = 0; i < 4; i++) { p.Rect(11 + i * 7, 22, 5, 7, VBeam); p.Rect(12 + i * 7, 23, 3, 5, lights ? VGlowL : VGlassOffL); p.Rect(11 + i * 7, 10, 5, 7, g1); p.Rect(12 + i * 7, 11, 3, 4, lights ? VGlowL : VGlassOffL); } break;
                }
                VWindow(p, 82, 12, 16, 18, lights, trim, false);
                // a striped awning over the shop window, snow on it
                Color32 aw = trim, aw2 = C(184, 180, 190);
                for (int x = 3; x < 45; x++)
                {
                    Color32 c = ((x - 3) / 5) % 2 == 0 ? aw : aw2;
                    p.Rect(x, 36, 1, 7, c); p.Rect(x, 36, 1, 1, Mul(c, 0.75f));
                    if ((x - 3) % 5 != 4 && (x - 3) % 5 != 0) p.Set(x, 35, c);
                }
                p.Rect(3, 43, 42, 2, VSnow); p.Rect(3, 43, 42, 1, VSnowD);
            }
            // the hanging sign with the trade on it
            p.Rect(66, 40, 16, 1, VBeam); p.Rect(66, 36, 1, 5, VBeam); p.Rect(70, 37, 1, 3, VBeam); p.Rect(78, 37, 1, 3, VBeam);
            p.Rect(67, 22, 15, 15, VBeam); p.Rect(68, 23, 13, 13, C(126, 88, 54)); p.Rect(68, 35, 13, 1, C(160, 116, 74));
            TradeIcon(p, 70, 25, kind);
            p.Rect(67, 37, 15, 1, VSnow);

            // ---- roof
            p.Rect(0, bodyH - 1, FrontW, 3, roofD);
            for (int x = 2; x < FrontW; x += 9) { p.Set(x, bodyH - 2, VSnowD); p.Set(x + 4, bodyH - 2, VSnowD); p.Set(x + 4, bodyH - 3, VSnowD); }   // icicles
            int top = bodyH + 2;
            if (roofKinds[kind] == 0)
            {
                int rh = FrontH - top - 4;
                for (int y = 0; y < rh; y++)
                {
                    int half = 56 - y * 56 / rh;
                    if (half > 8) p.Rect(56 - half + 7, top + y, half * 2 - 14, 1, y % 6 == 5 ? wallD : wall);
                    p.Rect(56 - half, top + y, Mathf.Min(8, half), 1, y % 4 == 3 ? roofD : roof);
                    p.Rect(56 + half - Mathf.Min(8, half), top + y, Mathf.Min(8, half), 1, y % 4 == 3 ? roofD : roof);
                    p.Rect(56 - half, top + y, Mathf.Min(3, half), 1, VSnow); p.Rect(56 + half - Mathf.Min(3, half), top + y, Mathf.Min(3, half), 1, VSnow);
                }
                if (timbered[kind]) { p.Rect(24, top + 6, 64, 2, VBeam); p.Rect(55, top + 6, 2, rh - 14, VBeam); }
                VWindow(p, 49, top + 10, 14, 14, lights, trim, false, true);
            }
            else if (roofKinds[kind] == 1)
            {
                int rh = Mathf.Min(30, FrontH - top - 6);
                for (int y = 0; y < rh; y++)
                {
                    int inset = y * 2 / 5;
                    for (int x = inset; x < FrontW - inset; x++)
                    {
                        bool snow = y >= rh - 7 - ((x * 7 + kind * 3) % 5 == 0 ? 2 : 0);
                        Color32 c = snow ? (y == rh - 7 ? VSnowD : VSnow) : (y % 5 == 4 ? roofD : ((x + (y / 5) * 4) % 8 == 0 ? roofD : roof));
                        p.Set(x, top + y, c);
                    }
                }
                // a dormer window in the roof
                p.Rect(42, top + 2, 28, 18, wall); p.Rect(42, top + 2, 2, 18, VBeam); p.Rect(68, top + 2, 2, 18, VBeam);
                VWindow(p, 49, top + 6, 14, 11, lights, trim, false);
                for (int y = 0; y < 9; y++) { int half = 17 - y * 2; if (half < 1) half = 1; p.Rect(56 - half, top + 20 + y, half * 2, 1, y >= 3 ? VSnow : roof); }
            }
            else
            {
                // a stepped gable of stone
                for (int s = 0; s < 5; s++)
                {
                    int x0 = 6 + s * 10, w = FrontW - 12 - s * 20, y0 = top + s * 7;
                    p.Rect(x0, y0, w, 7, wall); p.Rect(x0, y0, w, 1, wallD);
                    p.Rect(x0, y0 + 7, Mathf.Min(10, w), 2, VSnow); p.Rect(x0 + w - Mathf.Min(10, w), y0 + 7, Mathf.Min(10, w), 2, VSnow);
                }
                p.Rect(6 + 50, top + 35, 0, 0, wall);
                VWindow(p, 50, top + 8, 12, 14, lights, trim, false, true);
            }
            // chimney: a broad one for the forge, glowing when the fire is in
            int chx = kind == 0 ? 82 : kind % 2 == 0 ? 20 : 84, chw = kind == 0 ? 14 : 9, chTop = Mathf.Min(FrontH - 3, top + (kind == 0 ? 36 : 30));
            p.Rect(chx, top + 4, chw, chTop - top - 4, C(132, 96, 84)); p.Rect(chx, top + 4, 2, chTop - top - 4, C(96, 68, 62));
            for (int y = top + 8; y < chTop; y += 5) p.Rect(chx, y, chw, 1, C(96, 68, 62));
            p.Rect(chx - 1, chTop, chw + 2, 2, VSnow);
            if (lit && kind == 0) { p.Rect(chx + 2, chTop, chw - 4, 2, C(255, 150, 60)); p.Rect(chx + 4, chTop + 2, chw - 8, 1, C(255, 220, 130)); }
            return p;
        }

        // =============================================================================== the street itself

        /// <summary>
        /// What stands in the street: lamp posts with a wreath, the strings of small lights slung from post to post
        /// (112 px: exactly one house wide), a bench, a decorated fir, a snowman, a wood pile and a stack of crates.
        /// </summary>
        static void StreetArt(string d)
        {
            Color32 iron = C(40, 42, 56), ironL = C(70, 74, 92), fir = C(52, 108, 76), firD = C(36, 78, 58), berry = C(206, 62, 66);
            {
                // The street itself: cobbles under a kerb, laid over the ground tiles for the whole width of the
                // village (1088 px = 68 tiles; 48 px deep). Snow has drifted onto the kerb and settled between the stones.
                const int PW = 1088, PH = 48;
                var p = new Px(PW, PH);
                Color32 mortar = C(34, 36, 54), s1 = C(92, 94, 118), s2 = C(78, 80, 104), s3 = C(104, 100, 116), kerb = C(116, 116, 136), kerbD = C(70, 72, 94);
                p.Rect(0, 0, PW, PH, mortar);
                int row = 0;
                for (int y = PH - 8; y > -8; y -= 7, row++)
                {
                    int x = -(row % 2) * 5 - (row * 3) % 4;
                    int i = 0;
                    while (x < PW)
                    {
                        int w = 8 + ((i * 7 + row * 5) % 5);
                        Color32 c = ((i * 3 + row) % 5 == 0) ? s3 : ((i + row) % 2 == 0 ? s1 : s2);
                        c = Mul(c, 1f - row * 0.07f);                       // deeper rows fall into shadow
                        p.Rect(x, y, w - 1, 6, c);
                        p.Rect(x, y + 5, w - 1, 1, Lerp(c, C(255, 255, 255), 0.12f));
                        p.Rect(x, y, w - 1, 1, Mul(c, 0.8f));
                        p.Set(x, y, mortar); p.Set(x + w - 2, y, mortar); p.Set(x, y + 5, mortar); p.Set(x + w - 2, y + 5, mortar);
                        if ((i * 11 + row * 7) % 6 == 0 && row < 3) p.Rect(x + 1, y + 5, Mathf.Max(2, w - 4), 1, VSnowD);      // snow caught on a stone
                        x += w; i++;
                    }
                }
                // the kerb along the houses, and the snow on it: thick in places, trodden thin in others
                p.Rect(0, PH - 4, PW, 3, kerb); p.Rect(0, PH - 4, PW, 1, kerbD);
                for (int x = 0; x < PW; x += 16) p.Rect(x, PH - 4, 1, 3, kerbD);
                for (int x = 0; x < PW; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.045f, 3.7f) + Mathf.PerlinNoise(x * 0.17f, 9.1f) * 0.45f;
                    int depth = n > 0.72f ? 4 : n > 0.55f ? 3 : n > 0.38f ? 2 : 1;
                    p.Rect(x, PH - depth, 1, depth, VSnow);
                    if (depth > 1) p.Set(x, PH - depth, VSnowD);
                    if (n > 0.8f) p.Set(x, PH - 5, VSnow);
                }
                Save(p, d + "street_ground", new Vector2(0f, 1f));
            }
            {
                var p = new Px(24, 80);
                p.Rect(8, 0, 8, 3, iron); p.Rect(9, 3, 6, 2, ironL); p.Rect(11, 5, 2, 56, iron); p.Rect(11, 5, 1, 56, ironL);
                p.Rect(9, 30, 6, 2, iron); p.Rect(8, 61, 8, 2, iron);
                p.Rect(7, 63, 10, 11, iron); p.Rect(8, 64, 8, 9, VGlow); p.Rect(10, 66, 4, 5, VGlowL); p.Rect(11, 64, 2, 9, iron);
                p.Rect(6, 74, 12, 2, iron); p.Rect(8, 76, 8, 2, VSnow); p.Rect(10, 78, 4, 1, VSnow);
                // a wreath with a red bow
                for (int a = 0; a < 16; a++) { float t = a / 16f * Mathf.PI * 2f; int x = 12 + Mathf.RoundToInt(Mathf.Cos(t) * 5f), y = 44 + Mathf.RoundToInt(Mathf.Sin(t) * 5f); p.Rect(x - 1, y - 1, 2, 2, a % 3 == 0 ? firD : fir); }
                p.Rect(10, 38, 4, 2, berry); p.Set(9, 37, berry); p.Set(14, 37, berry);
                p.Rect(2, 0, 20, 1, VSnow); p.Rect(5, 1, 14, 1, VSnow);                               // snow heaped at its foot
                Save(p, d + "lamp_post", new Vector2(0.5f, 0f));
            }
            {
                // lights on a wire, sagging between two posts; the bulbs are the brightest thing in the street
                var p = new Px(112, 32);
                Color32[] bulbs = { C(255, 214, 130), C(240, 120, 110), C(255, 214, 130), C(130, 210, 150), C(255, 214, 130), C(130, 170, 240) };
                for (int x = 0; x < 112; x++)
                {
                    int y = 30 - Mathf.RoundToInt(Mathf.Sin(x / 111f * Mathf.PI) * 16f);
                    p.Set(x, y, C(30, 30, 44));
                    if (x % 8 == 4)
                    {
                        var c = bulbs[(x / 8) % bulbs.Length];
                        p.Rect(x - 1, y - 3, 3, 3, c); p.Set(x, y - 2, C(255, 250, 230)); p.Set(x, y - 4, Mul(c, 0.7f));
                    }
                }
                Save(p, d + "string_lights", new Vector2(0f, 1f));
            }
            {
                var p = new Px(32, 16);     // a bench with snow on the seat
                Color32 w = C(110, 76, 50), wD = C(74, 48, 32);
                p.Rect(3, 0, 2, 7, wD); p.Rect(27, 0, 2, 7, wD); p.Rect(2, 7, 28, 2, w); p.Rect(2, 6, 28, 1, wD);
                p.Rect(3, 9, 2, 6, wD); p.Rect(27, 9, 2, 6, wD); p.Rect(2, 11, 28, 2, w); p.Rect(2, 14, 28, 1, w);
                p.Rect(4, 9, 22, 1, VSnow); p.Rect(8, 10, 10, 1, VSnow); p.Rect(2, 15, 28, 1, VSnow);
                Save(p, d + "bench", new Vector2(0.5f, 0f));
            }
            {
                var p = new Px(40, 60);     // a fir dressed for the winter feast: candles, baubles, a star
                p.Rect(18, 0, 4, 8, C(84, 54, 34)); p.Rect(12, 0, 16, 2, VSnow);
                for (int tier = 0; tier < 4; tier++)
                {
                    int y0 = 6 + tier * 12, half0 = 18 - tier * 4;
                    for (int y = 0; y < 14; y++)
                    {
                        int half = Mathf.Max(1, half0 * (14 - y) / 14);
                        p.Rect(20 - half, y0 + y, half * 2, 1, (y + tier) % 3 == 0 ? firD : fir);
                        if (y == 0) { p.Rect(20 - half, y0, 5, 1, VSnow); p.Rect(20 + half - 6, y0, 6, 1, VSnow); p.Rect(20 - half + 6, y0 + 1, 4, 1, VSnowD); }
                    }
                    Color32[] bb = { C(240, 120, 110), C(255, 214, 130), C(130, 170, 240) };
                    for (int i = 0; i < 3 + (3 - tier); i++) { int bx = 20 - half0 + 4 + i * 6 + tier, by = y0 + 3 + (i * 5) % 5; if (Mathf.Abs(bx - 20) < half0 * (14 - (by - y0)) / 14 - 1) p.Rect(bx, by, 2, 2, bb[(i + tier) % 3]); }
                }
                p.Rect(19, 56, 3, 3, C(255, 220, 120)); p.Set(20, 59, C(255, 244, 190)); p.Set(18, 57, C(255, 220, 120)); p.Set(22, 57, C(255, 220, 120));
                Save(p, d + "fir_tree", new Vector2(0.5f, 0f));
            }
            {
                var p = new Px(18, 26);     // a snowman some child built, wearing a scarf like the player's
                Color32 s1 = C(214, 222, 240), s2 = C(170, 184, 214);
                p.Rect(3, 0, 12, 10, s1); p.Rect(2, 2, 14, 6, s1); p.Rect(3, 0, 12, 1, s2); p.Rect(13, 2, 2, 6, s2);
                p.Rect(5, 9, 8, 7, s1); p.Rect(4, 10, 10, 5, s1); p.Rect(12, 10, 2, 5, s2);
                p.Rect(6, 16, 6, 6, s1); p.Rect(11, 17, 1, 4, s2);
                p.Rect(4, 15, 10, 2, berry); p.Rect(12, 11, 2, 5, berry);
                p.Set(7, 19, iron); p.Set(10, 19, iron); p.Rect(8, 18, 3, 1, C(236, 140, 60));
                p.Rect(5, 22, 8, 1, iron); p.Rect(6, 23, 6, 3, iron);
                p.Set(8, 12, iron); p.Set(8, 6, iron); p.Rect(0, 12, 4, 1, C(84, 54, 34)); p.Rect(14, 13, 4, 1, C(84, 54, 34));
                Save(p, d + "snowman", new Vector2(0.5f, 0f));
            }
            {
                var p = new Px(28, 18);     // split logs stacked against the wall, snow on top
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 5 - row; i++)
                    {
                        int x = 1 + row * 2 + i * 5, y = row * 5;
                        p.Rect(x, y, 5, 5, C(110, 74, 46)); p.Rect(x + 1, y + 1, 3, 3, C(190, 150, 100)); p.Set(x + 2, y + 2, C(140, 100, 64));
                    }
                p.Rect(5, 15, 12, 1, VSnow); p.Rect(7, 16, 8, 1, VSnow);
                Save(p, d + "wood_pile", new Vector2(0.5f, 0f));
            }
            {
                var p = new Px(30, 26);     // crates and a barrel put out by a shop
                Color32 w = C(124, 86, 54), wD = C(84, 56, 36), wL = C(160, 118, 76);
                void Crate(int x, int y, int sz) { p.Rect(x, y, sz, sz, w); p.Rect(x, y, sz, 1, wD); p.Rect(x, y + sz - 1, sz, 1, wL); p.Rect(x, y, 1, sz, wD); p.Rect(x + sz - 1, y, 1, sz, wD); for (int i = 2; i < sz - 2; i++) { p.Set(x + i, y + i, wD); p.Set(x + sz - 1 - i, y + i, wD); } }
                Crate(0, 0, 14); Crate(3, 14, 10);
                p.Rect(16, 0, 12, 16, C(134, 92, 56)); p.Rect(16, 3, 12, 2, C(84, 86, 100)); p.Rect(16, 11, 12, 2, C(84, 86, 100)); p.Rect(16, 0, 1, 16, wD); p.Rect(27, 0, 1, 16, wD);
                p.Rect(3, 24, 10, 1, VSnow); p.Rect(17, 16, 10, 1, VSnow); p.Rect(19, 17, 6, 1, VSnow);
                Save(p, d + "crates", new Vector2(0.5f, 0f));
            }
        }

        // =============================================================================== rooms

        // Rooms: two storeys behind every front - the shop (or the living room) at street level, the keeper's own
        // room upstairs, a ladder between them. 544 x 304 px: the house (16 x 15 tiles with its walls) in the middle
        // of a dark surround. Floor space of each storey is 224 x 96 px; the upper floor is 112 px above the lower.
        const int RoomPxW = 544, RoomPxH = 304, RX = 144, RY = 32, UpperY = 112;
        /// <summary>Where the ladder stands, in floor-space pixels (the fourth cell).</summary>
        const int LadderX = 48;

        static Px Room(int kind)
        {
            var p = new Px(RoomPxW, RoomPxH);
            p.Rect(0, 0, RoomPxW, RoomPxH, C(5, 5, 10));
            Color32[] papers = { C(92, 84, 88), C(150, 118, 84), C(206, 214, 196), C(86, 108, 148), C(170, 112, 104), C(84, 86, 116), C(150, 100, 76), C(140, 128, 108) };
            Color32 paper = papers[kind], paperD = Mul(paper, 0.78f), wood = C(120, 82, 52), woodD = C(84, 54, 34), woodL = C(160, 116, 74);
            int ix = RX + 16, iy = RY + 16;                                   // bottom-left of the ground floor's floor space
            int oy = 0;                                                       // 0 while drawing the ground floor, UpperY for upstairs

            p.Rect(RX, RY, 256, 240, VBeam);
            for (int storey = 0; storey < 2; storey++)
            {
                int by = iy + storey * UpperY;
                Color32 pw = storey == 0 ? paper : Lerp(paper, C(120, 110, 100), 0.35f), pwD = Mul(pw, 0.78f);
                p.Rect(ix, by, 224, 96, pw);
                for (int x = 0; x < 224; x += 16) p.Rect(ix + x, by + 30, 1, 66, pwD);
                for (int x = 0; x < 224; x++) if ((x / 8) % 2 == 0) p.Set(ix + x, by + 62, pwD);
                p.Rect(ix, by, 224, 28, Mul(wood, 0.9f)); p.Rect(ix, by + 28, 224, 2, woodL);             // wainscot
                for (int x = 0; x < 224; x += 12) p.Rect(ix + x, by, 1, 28, woodD);
            }
            // the roof beams, the floor between the storeys (with the hatch for the ladder), the ground floor's boards
            p.Rect(RX, RY + 224, 256, 16, woodD); for (int x = 0; x < 256; x += 32) p.Rect(RX + x, RY + 218, 8, 6, woodD);
            p.Rect(RX, iy + 96, 256, 16, wood); p.Rect(RX, iy + 111, 256, 1, woodL); p.Rect(RX, iy + 96, 256, 2, woodD);
            for (int x = 0; x < 256; x += 24) p.Rect(RX + x, iy + 98, 1, 13, woodD);
            for (int x = 0; x < 256; x += 32) p.Rect(RX + x, iy + 90, 6, 6, woodD);
            p.Rect(ix + LadderX - 2, iy + 96, 20, 16, C(30, 22, 22));                                    // the hatch
            p.Rect(RX, RY, 256, 16, wood); p.Rect(RX, RY + 15, 256, 1, woodL); p.Rect(RX, RY, 256, 1, woodD);
            for (int x = 0; x < 256; x += 24) p.Rect(RX + x, RY, 1, 15, woodD);
            p.Rect(RX, RY, 16, 240, woodD); p.Rect(RX + 240, RY, 16, 240, woodD); p.Rect(RX + 15, RY + 16, 1, 208, wood); p.Rect(RX + 240, RY + 16, 1, 208, wood);

            // helpers: coordinates are floor-space pixels of the storey being drawn (0..224 across, 0 = that floor)
            void R(int x, int y, int w, int h, Color32 c) => p.Rect(ix + x, iy + oy + y, w, h, c);
            void Dot(int x, int y, Color32 c) => p.Set(ix + x, iy + oy + y, c);
            void Shelf(int x, int y, int w) { R(x, y, w, 2, woodD); R(x, y + 2, w, 1, woodL); R(x, y - 3, 2, 3, woodD); R(x + w - 2, y - 3, 2, 3, woodD); }
            void Bottles(int x, int y, int n, int seed)
            {
                Color32[] cs = { C(214, 70, 80), C(90, 170, 110), C(90, 140, 210), C(236, 196, 90), C(170, 100, 190) };
                for (int i = 0; i < n; i++) { int h = 5 + (i * 3 + seed) % 4; R(x + i * 6, y, 4, h, cs[(i + seed) % cs.Length]); R(x + i * 6 + 1, y + h, 2, 2, C(200, 220, 235)); R(x + i * 6, y + h - 1, 1, 1, C(255, 255, 255)); }
            }
            void Crate(int x, int y, int s) { R(x, y, s, s, wood); R(x, y, s, 1, woodD); R(x, y + s - 1, s, 1, woodL); R(x, y, 1, s, woodD); R(x + s - 1, y, 1, s, woodD); for (int i = 2; i < s - 2; i++) { Dot(x + i, y + i, woodD); Dot(x + s - 1 - i, y + i, woodD); } }
            void Barrel(int x, int y) { R(x, y, 12, 16, C(134, 90, 54)); R(x + 1, y + 16, 10, 1, woodD); R(x, y + 3, 12, 2, C(90, 92, 104)); R(x, y + 11, 12, 2, C(90, 92, 104)); R(x, y, 1, 16, woodD); R(x + 11, y, 1, 16, woodD); }
            void Lamp(int x, int y) { R(x, y + 7, 1, 96 - y - 7, VBeam); R(x - 3, y, 7, 7, VBeam); R(x - 2, y + 1, 5, 5, VGlow); R(x - 1, y + 2, 3, 3, VGlowL); }
            void NightWindow(int x, int y, int w, int h) { R(x - 2, y - 2, w + 4, h + 4, VBeam); R(x, y, w, h, C(30, 40, 84)); R(x + w / 2, y, 1, h, VBeam); R(x, y + h / 2, w, 1, VBeam); Dot(x + 3, y + h - 4, VSnow); Dot(x + w - 4, y + 3, VSnow); Dot(x + 6, y + 5, VSnowD); R(x - 2, y - 2, w + 4, 1, VSnow); }
            void Counter(int x, int w) { R(x, 0, w, 14, wood); R(x, 14, w, 2, woodL); R(x, 0, w, 1, woodD); for (int i = 6; i < w; i += 10) R(x + i, 2, 1, 11, woodD); }
            // the hearth: stone, with logs in it - the flames are a moving picture laid over it in the game
            void Fireplace(int x)
            {
                R(x, 0, 40, 44, VStone); R(x, 44, 40, 4, VStoneD); R(x - 2, 44, 44, 2, VStone);
                for (int y = 6; y < 44; y += 8) R(x, y, 40, 1, VStoneD);
                R(x + 8, 0, 24, 26, C(26, 20, 22));
                R(x + 11, 0, 18, 3, woodD); R(x + 13, 3, 14, 2, C(110, 60, 40)); R(x + 12, 1, 16, 1, C(190, 90, 50));
                R(x + 10, 48, 20, 48, VStoneD); R(x + 10, 48, 2, 48, VStone);
            }
            void Table(int x, int w) { R(x, 12, w, 3, woodL); R(x, 11, w, 1, woodD); R(x + 2, 0, 3, 11, woodD); R(x + w - 5, 0, 3, 11, woodD); }
            void Rug(int x, int w, Color32 c) { R(x, 0, w, 2, c); for (int i = 2; i < w - 2; i += 4) R(x + i, 0, 2, 1, Mul(c, 1.3f)); R(x, 0, 1, 2, Mul(c, 0.7f)); R(x + w - 1, 0, 1, 2, Mul(c, 0.7f)); }
            void Bed(int x, Color32 blanket)
            {
                R(x, 0, 4, 22, woodD); R(x + 4, 0, 54, 8, wood); R(x + 56, 0, 4, 14, woodD);
                R(x + 4, 8, 52, 5, C(226, 226, 236)); R(x + 20, 8, 36, 7, blanket); R(x + 20, 14, 36, 1, Lerp(blanket, C(255, 255, 255), 0.25f)); R(x + 6, 11, 12, 5, C(248, 248, 252));
                for (int bx = x + 24; bx < x + 54; bx += 8) R(bx, 9, 4, 4, Mul(blanket, 0.8f));
            }
            void Wardrobe(int x) { R(x, 0, 28, 54, woodD); R(x + 2, 2, 11, 48, wood); R(x + 15, 2, 11, 48, wood); R(x + 11, 24, 2, 4, C(214, 170, 80)); R(x + 15, 24, 2, 4, C(214, 170, 80)); R(x - 1, 54, 30, 2, woodL); }
            void Chest(int x) { R(x, 0, 22, 12, wood); R(x, 12, 22, 4, woodL); R(x, 11, 22, 1, woodD); R(x + 9, 8, 4, 5, C(214, 170, 80)); R(x, 0, 1, 16, woodD); R(x + 21, 0, 1, 16, woodD); }

            // ---------------------------------------------------------------- ground floor
            oy = 0;
            // the door back to the street (second cell from the left)
            R(14, 0, 20, 34, VBeam); R(16, 0, 16, 31, C(98, 62, 40)); for (int x = 19; x < 32; x += 4) R(x, 0, 1, 31, C(72, 46, 30));
            R(20, 18, 8, 8, VBeam); R(21, 19, 6, 6, C(30, 40, 84)); Dot(29, 14, C(236, 200, 110));
            Rug(12, 24, C(120, 60, 60));

            switch (kind)
            {
                case 0:     // the forge: furnace, anvil, a rack of blades
                    R(168, 0, 50, 58, VStone); R(168, 58, 50, 4, VStoneD); for (int y = 8; y < 58; y += 9) R(168, y, 50, 1, VStoneD);
                    R(178, 6, 30, 26, C(26, 20, 22)); R(182, 6, 22, 4, C(190, 80, 40)); R(186, 6, 14, 2, C(240, 150, 70));          // a bed of coals; the fire burns over it
                    R(186, 62, 16, 34, VStoneD);
                    Counter(76, 92);
                    R(74, 52, 60, 2, woodD); for (int i = 0; i < 6; i++) { R(78 + i * 9, 54, 2, 20, C(190, 196, 210)); R(76 + i * 9, 54, 6, 2, C(120, 84, 52)); R(78 + i * 9, 50, 2, 4, C(90, 60, 40)); }
                    R(142, 0, 14, 5, VStoneD); R(140, 5, 18, 4, C(70, 72, 84)); R(144, 9, 10, 3, C(70, 72, 84)); R(138, 12, 22, 4, C(96, 98, 112));       // anvil
                    R(150, 40, 10, 14, C(60, 62, 76)); R(152, 54, 6, 6, C(120, 84, 52));
                    Lamp(100, 80);
                    break;
                case 1:     // the ropewalk: spools and coils everywhere
                    Shelf(80, 58, 70); for (int i = 0; i < 5; i++) { R(84 + i * 13, 61, 10, 12, C(206, 176, 112)); R(87 + i * 13, 64, 4, 6, C(150, 120, 70)); }
                    Shelf(160, 44, 50); for (int i = 0; i < 3; i++) { R(164 + i * 15, 47, 9, 8, C(110, 72, 44)); R(164 + i * 15, 47, 4, 3, C(84, 54, 34)); }
                    Counter(76, 92);
                    for (int i = 0; i < 3; i++) { R(180 + i * 12, 0, 10, 4 + i * 3, C(206, 176, 112)); R(183 + i * 12, 1, 4, 2, C(150, 120, 70)); }
                    for (int x = 70; x < 220; x += 3) Dot(x, 92 - (int)(Mathf.Sin((x - 70) / 150f * Mathf.PI) * 9f), C(206, 176, 112));
                    Lamp(150, 80);
                    break;
                case 2:     // the apothecary: shelves of bottles, a cauldron, herbs drying
                    Shelf(74, 72, 116); Bottles(78, 75, 18, 1);
                    Shelf(74, 52, 116); Bottles(80, 55, 17, 3);
                    Counter(76, 92); Bottles(140, 16, 4, 2);
                    R(186, 4, 30, 18, C(40, 40, 50)); R(184, 20, 34, 3, C(60, 60, 72)); R(190, 22, 22, 2, C(120, 220, 140)); R(190, 0, 3, 4, C(40, 40, 50)); R(210, 0, 3, 4, C(40, 40, 50));
                    R(196, 26, 2, 3, C(160, 240, 170)); R(204, 29, 2, 3, C(160, 240, 170));
                    for (int i = 0; i < 7; i++) { R(76 + i * 21, 84, 2, 12, C(90, 150, 80)); R(75 + i * 21, 80, 4, 5, i % 2 == 0 ? C(120, 170, 90) : C(190, 150, 200)); }
                    Lamp(200, 76);
                    break;
                case 3:     // the map room: a great map on the wall, a desk with a globe
                    R(82, 44, 84, 46, woodD); R(84, 46, 80, 42, C(232, 220, 190));
                    for (int i = 0; i < 9; i++) R(90 + i * 8, 52 + (i * 7) % 20, 5 + i % 3, 3 + i % 2, C(150, 170, 120));
                    for (int x = 86; x < 162; x += 4) Dot(x, 60 + (int)(Mathf.Sin(x * 0.3f) * 6f), C(120, 96, 70));
                    R(150, 78, 3, 3, C(214, 70, 80)); R(100, 58, 3, 3, C(214, 70, 80));
                    Counter(76, 92);
                    Table(170, 46); R(176, 15, 16, 2, C(232, 220, 190)); R(198, 15, 2, 8, woodD); R(193, 23, 12, 12, C(80, 130, 180)); R(196, 26, 5, 4, C(120, 170, 110));
                    Lamp(190, 78);
                    break;
                case 4:     // Pip's Oddments: crates, barrels, rugs and far too many lamps
                    Crate(170, 0, 20); Crate(192, 0, 16); Crate(176, 20, 16); Barrel(210, 0);
                    Counter(76, 92); R(84, 16, 8, 6, C(214, 70, 80)); R(100, 16, 6, 9, C(90, 140, 210)); R(150, 16, 10, 5, C(236, 196, 90));
                    R(84, 50, 26, 34, C(150, 70, 110)); for (int y = 54; y < 84; y += 6) R(86, y, 22, 2, C(236, 196, 90));
                    R(120, 46, 22, 38, C(70, 130, 140)); for (int y = 50; y < 84; y += 8) R(122, y, 18, 3, C(240, 232, 214));
                    Shelf(150, 60, 60); Bottles(154, 63, 4, 4); R(182, 63, 8, 8, C(200, 170, 90)); R(194, 63, 10, 5, C(170, 100, 190));
                    Lamp(112, 86); Lamp(170, 82); Lamp(204, 78);
                    break;
                case 5:     // the lamp house: lamps in rows, oil in barrels
                    for (int i = 0; i < 7; i++) Lamp(74 + i * 22, 74 + (i % 2) * 8);
                    Shelf(80, 46, 80); for (int i = 0; i < 7; i++) { R(84 + i * 11, 49, 7, 9, VBeam); R(85 + i * 11, 50, 5, 6, i % 3 == 0 ? VGlassOffL : VGlowL); }
                    Counter(76, 92);
                    Barrel(176, 0); Barrel(190, 0); Barrel(183, 16); R(206, 0, 10, 14, C(214, 170, 80)); R(208, 14, 6, 3, C(150, 108, 44));
                    break;
                case 6:     // the village hall: the hearth, a long table, the banner of Brindlemoor
                    Fireplace(176);
                    R(96, 40, 24, 50, C(150, 44, 50)); R(96, 88, 24, 2, VBeam);
                    R(102, 62, 12, 5, C(250, 200, 70)); R(102, 67, 2, 4, C(250, 200, 70)); R(107, 67, 2, 4, C(250, 200, 70)); R(112, 67, 2, 4, C(250, 200, 70));
                    Table(72, 92); R(80, 15, 6, 5, C(214, 170, 80)); R(100, 15, 10, 3, C(232, 220, 190)); R(134, 15, 4, 8, C(190, 196, 210)); R(135, 23, 2, 3, VGlowL);
                    R(164, 0, 2, 22, woodD); R(164, 10, 10, 2, wood); R(172, 0, 2, 10, woodD);
                    R(136, 60, 26, 2, woodD); R(140, 62, 3, 10, C(232, 220, 190)); R(154, 62, 3, 10, C(232, 220, 190)); R(146, 62, 5, 6, C(150, 110, 70));
                    Lamp(128, 84);
                    Rug(72, 92, C(150, 44, 50));
                    break;
                default:    // home, downstairs: the fire, a table laid for supper, an armchair, boots by the door
                    Fireplace(178);
                    Table(84, 44); R(92, 15, 8, 3, C(232, 220, 190)); R(106, 15, 4, 6, C(150, 110, 70)); R(114, 15, 3, 7, VGlowL);
                    R(140, 0, 26, 12, C(96, 110, 150)); R(138, 0, 4, 24, C(70, 84, 120)); R(142, 12, 22, 4, C(120, 134, 176)); R(162, 0, 4, 16, C(70, 84, 120));       // an armchair by the fire
                    R(100, 52, 22, 26, woodD); R(102, 54, 18, 22, C(120, 150, 130)); R(106, 58, 10, 8, C(232, 220, 190));
                    Shelf(134, 60, 30); R(137, 63, 5, 8, C(170, 60, 60)); R(144, 63, 4, 9, C(70, 110, 160)); R(150, 63, 5, 6, C(200, 170, 90)); R(157, 63, 4, 7, C(90, 140, 90));
                    Rug(84, 86, C(84, 110, 150)); Lamp(128, 82);
                    R(36, 0, 6, 7, C(96, 58, 32));
                    break;
            }

            // ---------------------------------------------------------------- upstairs: where the keeper lives
            oy = UpperY;
            switch (kind)
            {
                case 0: Bed(150, C(110, 110, 124)); Crate(90, 0, 18); Crate(110, 0, 14); Barrel(128, 0); NightWindow(100, 46, 18, 20); Shelf(160, 56, 44); R(164, 59, 3, 12, C(190, 196, 210)); R(172, 59, 3, 10, C(190, 196, 210)); R(182, 59, 8, 6, C(96, 98, 112)); Lamp(130, 82); break;
                case 1: Bed(150, C(96, 140, 104)); R(86, 0, 22, 30, woodD); R(88, 2, 18, 26, wood); for (int y = 6; y < 28; y += 7) R(88, y, 18, 1, C(206, 176, 112)); NightWindow(118, 46, 18, 20); for (int i = 0; i < 3; i++) R(180 + i * 11, 56, 9, 9, C(206, 176, 112)); Shelf(176, 53, 40); Lamp(140, 82); break;
                case 2: Bed(150, C(190, 70, 80)); R(84, 0, 26, 56, woodD); for (int y = 4; y < 54; y += 13) { R(86, y, 22, 10, C(40, 28, 24)); Bottles(87, y, 3, y); } NightWindow(120, 46, 18, 20); for (int i = 0; i < 5; i++) { R(150 + i * 14, 84, 2, 12, C(90, 150, 80)); R(149 + i * 14, 80, 4, 5, C(190, 150, 200)); } Lamp(134, 80); break;
                case 3: Bed(84, C(84, 110, 170)); NightWindow(160, 40, 28, 30); R(196, 0, 3, 34, woodD); R(190, 0, 14, 2, woodD); for (int i = 0; i < 16; i++) Dot(197 - i, 34 + i / 2, C(214, 170, 80)); R(180, 40, 4, 4, C(214, 170, 80));       // a telescope at the big window
                    Table(100, 44); R(104, 15, 30, 2, C(232, 220, 190)); Lamp(150, 82); break;
                case 4: Bed(150, C(170, 100, 170)); Crate(84, 0, 20); Crate(106, 0, 16); Crate(90, 20, 16); Crate(124, 0, 14); NightWindow(128, 50, 16, 18); Lamp(110, 84); Lamp(190, 80); break;
                case 5:     // the great lamp of the tower, behind its glass
                    R(100, 0, 60, 6, VStoneD); R(108, 6, 44, 60, VBeam); R(112, 10, 36, 52, VGlow); R(120, 18, 20, 36, VGlowL); R(129, 6, 2, 60, VBeam); R(108, 34, 44, 2, VBeam);
                    R(104, 66, 52, 4, VStoneD); Bed(164 - 4, C(96, 100, 150)); Barrel(84, 0); break;
                case 6: Bed(150, C(150, 60, 64)); R(84, 0, 30, 60, woodD); for (int y = 4; y < 58; y += 14) { R(86, y, 26, 11, C(40, 28, 24)); for (int i = 0; i < 7; i++) R(87 + i * 3 + i / 3, y, 3, 8 + (i * 5 + y) % 3, new[] { C(170, 60, 60), C(70, 110, 160), C(90, 140, 90), C(200, 170, 90) }[(i + y) % 4]); }
                    Table(118, 28); R(122, 15, 14, 2, C(232, 220, 190)); R(138, 15, 3, 7, VGlowL); NightWindow(124, 50, 16, 18); Rug(118, 90, C(150, 44, 50)); Lamp(170, 84); break;
                default:    // home, upstairs: the bed the player wakes up in
                    Bed(110, C(176, 62, 64)); Wardrobe(184); Chest(84);
                    NightWindow(128, 46, 22, 24); Rug(104, 72, C(84, 110, 150)); Lamp(96, 82);
                    R(176, 60, 5, 5, VBeam);
                    break;
            }

            // ---------------------------------------------------------------- the ladder, from the ground floor up through the hatch
            oy = 0;
            R(LadderX, 0, 2, UpperY + 6, woodD); R(LadderX + 14, 0, 2, UpperY + 6, woodD);
            R(LadderX + 1, 0, 1, UpperY + 6, woodL); R(LadderX + 15, 0, 1, UpperY + 6, woodL);
            for (int y = 3; y < UpperY + 4; y += 5) { R(LadderX + 2, y, 12, 2, wood); R(LadderX + 2, y, 12, 1, woodD); }
            return p;
        }

        /// <summary>Flames for a hearth or the forge: four pictures to play in a loop (unlit, so they shine).</summary>
        static Px FlameFrame(int f)
        {
            var p = new Px(22, 22);
            Color32 outer = C(232, 96, 40), mid = C(255, 164, 60), core = C(255, 232, 150), spark = C(255, 250, 220);
            for (int x = 0; x < 22; x++)
            {
                float tongue = Mathf.Abs(Mathf.Sin(x * 0.55f + f * 1.7f)) * 0.55f + Mathf.Abs(Mathf.Sin(x * 1.3f - f * 2.3f)) * 0.3f;
                float body = 1f - Mathf.Abs(x - 10.5f) / 11f;                  // tallest in the middle
                int h = Mathf.Clamp(Mathf.RoundToInt((0.25f + tongue) * body * 22f), 0, 21);
                for (int y = 0; y < h; y++)
                {
                    float t = y / (float)Mathf.Max(1, h);
                    p.Set(x, y, t < 0.3f && body > 0.45f ? core : t < 0.62f ? mid : outer);
                }
                if (h > 6 && (x + f) % 5 == 0) p.Set(x, 1, spark);
            }
            p.Set(4 + (f * 5) % 14, 18 + f % 3, mid);                           // a spark flying up
            return p;
        }
    }
}
