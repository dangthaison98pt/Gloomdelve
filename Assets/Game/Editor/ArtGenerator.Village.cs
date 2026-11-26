using System.Collections.Generic;
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
                // (only the house itself is kept of each picture: the dark round it is one plain sprite, room_dark,
                //  stretched behind the room - 32 pictures of 544 x 304 were 15 MB of the build, most of it black)
                Save(House(Room(k)), d + "room_" + k, PCenter);
                // the same room at the other hours of the village clock: what the windows show, and the light they let in
                Save(House(Room(k, 1)), d + "room_" + k + "_dawn", PCenter);
                Save(House(Room(k, 2)), d + "room_" + k + "_day", PCenter);
                Save(House(Room(k, 3)), d + "room_" + k + "_dusk", PCenter);
                Save(House(RoomForeground(k)), d + "room_fg_" + k, PCenter);        // what hangs close to the eye, drawn in front of everyone
            }
            // home with its lamps put out (the lamps can be put out there): the windows and the fire alone light it
            roomLampsOut = true;
            try
            {
                Save(House(Room(7)), d + "room_7_out", PCenter);
                Save(House(Room(7, 1)), d + "room_7_out_dawn", PCenter);
                Save(House(Room(7, 2)), d + "room_7_out_day", PCenter);
                Save(House(Room(7, 3)), d + "room_7_out_dusk", PCenter);
            }
            finally { roomLampsOut = false; }
            RoomToyArt(d);
            ReadableArt(d);
            TreeAndOcarinaArt(d);
            {
                // the dark that surrounds a room, hiding the rock the house is cut into: stretched to RoomPxW x RoomPxH
                var p = new Px(4, 4);
                p.Fill(C(5, 5, 10));
                Save(p, d + "room_dark", PCenter);
            }
            StreetArt(d);
            {
                // the glint that marks something worth a closer look
                var p = new Px(7, 7);
                Color32 g = C(255, 240, 190), gD = C(255, 210, 130, 170);
                p.Rect(3, 0, 1, 7, gD); p.Rect(0, 3, 7, 1, gD); p.Rect(3, 2, 1, 3, g); p.Rect(2, 3, 3, 1, g);
                Save(p, d + "glint", PCenter);
            }
            for (int f = 0; f < 4; f++) Save(FlameFrame(f), d + "fire_" + f, new Vector2(0.5f, 0f));
            foreach (bool icon in new[] { false, true })
            {
                // an ember of the Sunstone: a shard that still burns (the pickup, and its picture on the HUD)
                // (the pickup is a small shard, 8 px, standing on the ground like a gem; the HUD picture is the big one)
                int S = icon ? 16 : 8, o = icon ? 2 : 0;
                var p = new Px(S, S);
                Color32 hot = C(255, 244, 200), mid = C(255, 176, 70), rim = C(226, 96, 40), dark = C(120, 40, 30);
                int[] half = icon ? new[] { 1, 2, 3, 4, 4, 5, 5, 4, 3, 2, 2, 1 } : new[] { 1, 2, 3, 3, 3, 2, 2, 1 };
                int mid0 = icon ? 6 : 4, lean = icon ? 7 : 5;
                for (int y = 0; y < half.Length; y++)
                {
                    int h = half[y], cx = mid0 + (y > lean ? 1 : 0);
                    p.Rect(o + cx - h, o + y, h * 2, 1, rim);
                    if (h > 1) p.Rect(o + cx - h + 1, o + y, h * 2 - 2, 1, mid);
                    if (h > (icon ? 3 : 2)) p.Rect(o + cx - 1, o + y, 2, 1, hot);
                    p.Set(o + cx - h, o + y, dark);
                }
                if (icon) p.Set(o + 6, o + 5, C(255, 255, 240));
                Save(p, icon ? "UI/icon_ember" : "Sprites/Items/ember", icon ? PCenter : new Vector2(0.5f, 0f));
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
                // the Ember Pouch (the elder's gift, and the bag that holds the embers): not the ember itself
                var p = new Px(16, 16);
                Color32 lea = C(150, 78, 50), leaD = C(104, 50, 36), leaL = C(190, 110, 70), cord = C(226, 186, 96), cordD = C(160, 120, 50);
                Color32 hot = C(255, 244, 200), mid = C(255, 176, 70), rim = C(226, 96, 40), ol = C(30, 14, 16);
                p.Ellipse(8f, 5.5f, 5.5f, 4.5f, lea);                                  // the belly of the pouch
                p.Rect(3, 2, 10, 2, leaD); p.Rect(3, 4, 2, 4, leaD);                   // its shadow: underneath, and down the left
                p.Rect(10, 6, 2, 3, leaL);                                             // light on its right shoulder
                p.Rect(6, 10, 5, 1, lea); p.Rect(5, 11, 7, 1, cord); p.Set(5, 11, cordD); p.Set(12, 10, cord); p.Set(13, 9, cord);       // the neck and its drawstring, one end hanging
                p.Rect(5, 12, 7, 2, lea); p.Rect(5, 12, 2, 2, leaD); p.Rect(4, 13, 1, 1, lea); p.Rect(12, 13, 1, 1, lea);              // the gathered mouth
                p.Rect(7, 13, 3, 1, rim); p.Rect(7, 14, 2, 1, mid); p.Set(8, 14, hot);                                                    // an ember showing in it
                p.Set(8, 7, rim); p.Rect(7, 6, 3, 1, mid); p.Set(8, 6, hot); p.Set(8, 5, rim);                                           // the ember's mark on the leather
                p.Outline(ol);
                Save(p, "UI/icon_pouch", PCenter);
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
                const int PW = 1616, PH = 48;      // (as wide as the village: 101 cells)
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
                var p = new Px(176, 32);      // (from one lamp post to the next: eleven cells)
                Color32[] bulbs = { C(255, 214, 130), C(240, 120, 110), C(255, 214, 130), C(130, 210, 150), C(255, 214, 130), C(130, 170, 240) };
                for (int x = 0; x < 176; x++)
                {
                    int y = 30 - Mathf.RoundToInt(Mathf.Sin(x / 175f * Mathf.PI) * 18f);
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
                var firNow = Current(p, d + "fir_tree");
                Save(RingOf(Padded(firNow, 1)), d + "fir_tree_hl", new Vector2(0.5f, 1f / (firNow.h + 2f)));       // its outline (the one that can be shaken)
            }
            {
                // a young fir nobody has dressed: snow on its boughs
                var p = new Px(26, 38);
                p.Rect(12, 0, 3, 6, C(84, 54, 34)); p.Rect(8, 0, 11, 1, VSnow);
                for (int tier = 0; tier < 3; tier++)
                {
                    int y0 = 5 + tier * 10, half0 = 11 - tier * 3;
                    for (int y = 0; y < 13; y++)
                    {
                        int half = Mathf.Max(1, half0 * (13 - y) / 13);
                        p.Rect(13 - half, y0 + y, half * 2, 1, (y + tier) % 3 == 0 ? firD : fir);
                        if (y == 0) { p.Rect(13 - half, y0, 4, 1, VSnow); p.Rect(13 + half - 5, y0, 5, 1, VSnow); }
                        if (y == 5) p.Rect(13 - half + 1, y0 + y, 3, 1, VSnowD);
                    }
                }
                p.Rect(12, 36, 2, 2, VSnow);
                Save(p, d + "fir_small", new Vector2(0.5f, 0f));
            }
            {
                // a tree that has lost its leaves: bare boughs, snow lying along them
                var p = new Px(46, 66);
                Color32 bark = C(86, 62, 50), barkD = C(58, 40, 34);
                p.Rect(21, 0, 5, 30, bark); p.Rect(21, 0, 2, 30, barkD); p.Rect(17, 0, 13, 2, VSnow);
                int[,] boughs = { { 23, 22, 8, 40 }, { 23, 26, 38, 42 }, { 23, 30, 14, 56 }, { 24, 32, 33, 58 }, { 23, 28, 23, 64 },
                                  { 13, 34, 4, 46 }, { 32, 36, 42, 50 }, { 17, 46, 9, 60 }, { 29, 48, 37, 62 }, { 23, 46, 19, 60 }, { 24, 50, 29, 62 } };
                for (int i = 0; i < boughs.GetLength(0); i++)
                {
                    int ax = boughs[i, 0], ay = boughs[i, 1], bx = boughs[i, 2], by = boughs[i, 3];
                    p.Line(ax, ay, bx, by, bark);
                    if (i < 5) p.Line(ax + 1, ay, bx + 1, by, i % 2 == 0 ? bark : barkD);
                }
                // snow on whatever lies open to the sky
                for (int x = 0; x < 46; x++)
                    for (int y = 64; y > 20; y--)
                        if (p.Opaque(x, y) && !p.Opaque(x, y + 1) && (x * 7 + y * 3) % 5 != 0) p.Set(x, y + 1, VSnow);
                Save(p, d + "bare_tree", new Vector2(0.5f, 0f));
            }
            {
                // a bush under the snow
                var p = new Px(24, 13);
                p.Ellipse(8f, 5f, 7f, 5f, firD); p.Ellipse(16f, 5f, 7f, 6f, fir); p.Ellipse(12f, 4f, 6f, 4f, firD);
                for (int x = 0; x < 24; x++)
                    for (int y = 12; y > 4; y--)
                        if (p.Opaque(x, y) && !p.Opaque(x, y + 1)) { p.Set(x, y, VSnow); if (x % 3 != 0) p.Set(x, y - 1, VSnowD); break; }
                p.Rect(2, 0, 20, 1, VSnow);
                Save(p, d + "bush", new Vector2(0.5f, 0f));
            }
            {
                // the soft shadow a thing throws on the snow where it stands (stretched to the thing's width)
                var p = new Px(24, 6);
                for (int y = 0; y < 6; y++)
                    for (int x = 0; x < 24; x++)
                    {
                        float nx = (x - 11.5f) / 12f, ny = (y - 2.5f) / 3f, dd = nx * nx + ny * ny;
                        if (dd <= 1f) p.Set(x, y, new Color32(10, 14, 30, (byte)(dd < 0.5f ? 120 : 70)));
                    }
                Save(p, d + "shadow", PCenter);
            }
            {
                // the kennel: a little house of boards, snow on its roof, a bowl by the door
                var p = new Px(34, 28);
                Color32 wd = C(150, 104, 66), wdD = C(104, 70, 46), wdL = C(186, 136, 90), roof = C(120, 60, 54), roofD = C(84, 40, 40), dark = C(26, 18, 22);
                p.Rect(4, 0, 24, 16, wd); p.Rect(4, 0, 3, 16, wdD); p.Rect(25, 0, 3, 16, wdL);
                for (int y = 4; y < 16; y += 4) p.Rect(4, y, 24, 1, wdD);
                for (int i = 0; i < 9; i++) { p.Rect(2 + i, 16 + i, 28 - i * 2, 1, i % 3 == 2 ? roofD : roof); }
                p.Rect(2, 16, 28, 1, roofD);
                p.Rect(11, 0, 10, 9, dark); p.Rect(12, 9, 8, 2, dark); p.Rect(13, 11, 6, 1, dark);        // the way in
                p.Rect(3, 17, 10, 1, VSnow); p.Rect(6, 20, 14, 1, VSnow); p.Rect(9, 23, 10, 1, VSnow); p.Rect(12, 25, 6, 1, VSnow); p.Rect(18, 18, 9, 1, VSnowD);
                p.Rect(29, 0, 5, 2, C(150, 150, 165)); p.Rect(30, 2, 3, 1, C(196, 160, 110));               // its bowl
                p.Rect(0, 0, 5, 1, VSnow);
                Save(p, d + "kennel", new Vector2(0.5f, 0f));
            }
            {
                // the dog: 0 standing, 1 / 2 running, 3 sitting, 4 asleep (it faces right)
                Color32 fur = C(176, 124, 72), furD = C(124, 82, 48), white = C(238, 232, 220), ear = C(84, 54, 36), ol = C(26, 18, 22), eye = C(20, 14, 16);
                for (int f = 0; f < 5; f++)
                {
                    var p = new Px(22, 16);
                    if (f == 0)
                    {
                        p.Rect(5, 2, 2, 4, furD); p.Rect(13, 2, 2, 4, fur); p.Rect(7, 2, 2, 4, fur); p.Rect(11, 2, 2, 4, furD);
                        p.Rect(4, 6, 12, 5, fur); p.Rect(4, 6, 12, 1, furD); p.Rect(12, 6, 4, 2, white);
                        p.Rect(14, 9, 5, 5, fur); p.Rect(18, 9, 2, 2, white); p.Set(19, 10, eye); p.Set(17, 12, eye); p.Rect(14, 13, 2, 2, ear);
                        p.Rect(2, 9, 2, 4, fur); p.Set(2, 13, white);
                    }
                    else if (f == 1 || f == 2)
                    {
                        if (f == 1) { p.Rect(2, 3, 3, 2, furD); p.Rect(16, 3, 3, 2, fur); p.Rect(5, 4, 2, 2, fur); p.Rect(14, 4, 2, 2, furD); }
                        else { p.Rect(7, 1, 2, 4, furD); p.Rect(9, 2, 2, 3, fur); p.Rect(11, 1, 2, 4, fur); }
                        int by = f == 1 ? 1 : 0;
                        p.Rect(4, 5 + by, 12, 5, fur); p.Rect(4, 5 + by, 12, 1, furD); p.Rect(12, 5 + by, 4, 2, white);
                        p.Rect(14, 8 + by, 5, 5, fur); p.Rect(18, 8 + by, 2, 2, white); p.Set(19, 9 + by, eye); p.Set(17, 11 + by, eye); p.Rect(13, 12 + by, 2, 2, ear);
                        p.Rect(1, 9 + by, 3, 2, fur); p.Set(1, 11 + by, white);
                    }
                    else if (f == 3)
                    {
                        p.Rect(6, 1, 8, 3, furD); p.Rect(7, 1, 7, 9, fur); p.Rect(11, 3, 3, 6, white); p.Rect(12, 1, 2, 5, fur);
                        p.Rect(10, 9, 6, 5, fur); p.Rect(15, 9, 2, 2, white); p.Set(16, 10, eye); p.Set(14, 12, eye); p.Rect(10, 13, 2, 2, ear);
                        p.Rect(3, 1, 4, 2, fur); p.Set(3, 3, white);
                    }
                    else
                    {
                        p.Ellipse(10f, 3.5f, 8f, 3.4f, fur); p.Rect(4, 1, 12, 1, furD);
                        p.Rect(13, 1, 6, 4, fur); p.Rect(18, 1, 2, 2, white); p.Rect(15, 3, 2, 1, eye); p.Rect(13, 5, 2, 1, ear);
                        p.Rect(2, 1, 4, 2, furD);
                    }
                    p.Outline(ol);
                    Save(p, d + "dog_" + f, new Vector2(0.5f, 1f / 16f));
                }
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
        /// <summary>Paint the rooms with their lamps put out (unlit glass, no lamplight).</summary>
        static bool roomLampsOut;
        /// <summary>Leave one painted thing out of the room ("cauldron", "lamp"): the difference is its shape (RoomToyArt, the outlines).</summary>
        static string roomSkip;
        /// <summary>Where the ladder stands, in floor-space pixels (the fourth cell).</summary>
        const int LadderX = 48;

        /// <summary>The house out of a room picture, without its dark surround (the house stands in the middle, so the centre stays the centre).</summary>
        static Px House(Px room)
        {
            var q = new Px(256, 240);
            for (int y = 0; y < 240; y++)
                for (int x = 0; x < 256; x++) q.Set(x, y, room.Get(RX + x, RY + y));
            return q;
        }

        /// <summary>light: the hour outside, as VillageClock.Part counts it - 0 night (the room as first painted), 1 dawn, 2 day, 3 dusk.</summary>
        static Px Room(int kind, int light = 0)
        {
            var p = new Px(RoomPxW, RoomPxH);
            p.Rect(0, 0, RoomPxW, RoomPxH, C(5, 5, 10));
            Color32[] papers = { C(92, 84, 88), C(150, 118, 84), C(206, 214, 196), C(86, 108, 148), C(170, 112, 104), C(84, 86, 116), C(150, 100, 76), C(140, 128, 108) };
            // The rooms belong to the same winter night as the street: muted walls, and all the colour comes from the
            // lamps and the fire. Everything is painted in plain daylight colours first; the last step of this
            // function lights it - warm where a lamp or the hearth reaches, blue and dim in the corners.
            Color32 paper = Lerp(papers[kind], C(104, 86, 78), 0.42f), paperD = Mul(paper, 0.78f), wood = C(120, 82, 52), woodD = C(84, 54, 34), woodL = C(160, 116, 74);
            Color32[] cloths = { C(96, 100, 120), C(84, 130, 104), C(176, 70, 78), C(214, 200, 160), C(150, 90, 150), C(210, 170, 80), C(150, 54, 58), C(96, 116, 160) };
            Color32 cloth = cloths[kind];
            var lights = new List<Vector4>();                   // x, y (picture pixels), reach, strength
            var panes = new List<RectInt>();                    // the glass of every window (picture pixels): what it shows depends on the hour
            var beams = new List<Vector4>();                    // light falling in through a window: x, y (the bottom of the glass), width, down to which y
            int ix = RX + 16, iy = RY + 16;                                   // bottom-left of the ground floor's floor space
            int oy = 0;                                                       // 0 while drawing the ground floor, UpperY for upstairs

            p.Rect(RX, RY, 256, 240, VBeam);
            for (int storey = 0; storey < 2; storey++)
            {
                int by = iy + storey * UpperY;
                Color32 pw = storey == 0 ? paper : Lerp(paper, C(120, 110, 100), 0.35f), pwD = Mul(pw, 0.78f);
                p.Rect(ix, by, 224, 96, pw);
                if (kind == 1)          // (only the ropewalk is boarded: the others are plaster, stone or paper)
                {
                    for (int x = 0; x < 224; x += 16) p.Rect(ix + x, by + 30, 1, 66, pwD);
                    for (int x = 0; x < 224; x++) if ((x / 8) % 2 == 0) p.Set(ix + x, by + 62, pwD);
                }
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

            // the walls above the panelling, by trade: lime plaster blotched by the years (most houses), stone
            // blocks (the forge, the village hall), printed paper in stripes (the map room), boards with their
            // grain (the ropewalk); a crack here and there, and a dark skirting along every floor
            for (int storey = 0; storey < 2; storey++)
            {
                int by = iy + storey * UpperY;
                bool stone = kind == 0 || kind == 6, stripes = kind == 3, boards = kind == 1;
                for (int y = by + 30; y < by + 96; y++)
                    for (int x = ix; x < ix + 224; x++)
                    {
                        var c0 = p.Get(x, y);
                        float n = Mathf.PerlinNoise(x * 0.06f + storey * 17f + kind * 5f, y * 0.06f) * 0.7f + Mathf.PerlinNoise(x * 0.21f, y * 0.21f + 9f + kind) * 0.3f;
                        float m = 1f;
                        if (stone)
                        {
                            int row = (y - by - 30) / 11, off = (row % 2) * 11;
                            bool joint = (y - by - 30) % 11 == 0 || (x - ix + off) % 22 == 0;
                            m = joint ? 0.7f : 0.92f + (Mathf.PerlinNoise(((x - ix + off) / 22) * 3.1f + row, row * 1.7f) - 0.5f) * 0.25f;
                            if (kind == 0) m *= 0.85f + (y - by - 30) / 66f * 0.1f;            // the forge: soot gathers up the wall
                        }
                        else if (stripes)
                        {
                            int sx = (x - ix) % 14;
                            m = sx < 7 ? 1f : 0.9f;
                            if (sx == 3 && (y - by) % 6 == 0) m = 1.12f;                         // a little printed flower
                            m *= n < 0.33f ? 0.93f : 1f;
                        }
                        else if (boards)
                        {
                            m = ((x - ix) % 16 == 0) ? 0.78f : 0.94f + Mathf.PerlinNoise((x - ix) / 16 * 7.3f, y * 0.4f) * 0.1f;
                        }
                        else
                        {
                            m = n < 0.35f ? 0.9f : n > 0.68f ? 1.06f : 1f;
                            if (((x * 31 + y * 17) ^ (x * y)) % 37 == 0) m *= 0.95f;
                        }
                        p.Set(x, y, Mul(c0, m));
                    }
                if (!stone && !boards)
                {
                    int cx0 = ix + (storey == 0 ? 60 + kind * 9 : 196 - kind * 7), cy0 = by + 95;
                    for (int k = 0; k < 18; k++) p.Set(cx0 + (int)(Mathf.Sin(k * 1.3f) * 2f) + k / 6, cy0 - k, Mul(paper, 0.62f));
                }
                p.Rect(ix, by, 224, 2, C(58, 40, 30));
            }

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
            void Lamp(int x, int y) { if ((roomSkip == "lamp" && kind == 7 && oy == 0 && x == 128) || (roomSkip == "lamprow" && kind == 5 && oy == 0)) { lights.Add(new Vector4(ix + x, iy + oy + y + 3, 112f, 1f)); return; } R(x, y + 7, 1, 96 - y - 7, VBeam); R(x - 3, y, 7, 7, VBeam); if (roomLampsOut) { R(x - 2, y + 1, 5, 5, C(58, 52, 50)); R(x - 1, y + 3, 3, 1, C(84, 78, 74)); return; } R(x - 2, y + 1, 5, 5, VGlow); R(x - 1, y + 2, 3, 3, VGlowL); lights.Add(new Vector4(ix + x, iy + oy + y + 3, 112f, 1f)); }
            void NightWindow(int x, int y, int w, int h)
            {
                // curtains drawn back to either side, a rail above them
                beams.Add(new Vector4(ix + x, iy + oy + y, w, iy + oy));                      // its light falls in, down to the floor
                R(x - 8, y + h + 3, w + 16, 2, woodD);
                for (int yy = -3; yy < h + 3; yy++) { int cw = 4 + (yy < 2 ? 1 : 0) + ((yy + 3) % 6 == 0 ? 1 : 0); R(x - 8, y + yy, cw, 1, yy % 3 == 0 ? Mul(cloth, 0.8f) : cloth); R(x + w + 8 - cw, y + yy, cw, 1, yy % 3 == 0 ? Mul(cloth, 0.8f) : cloth); }
                Pane(x, y, w, h);
            }
            void Pane(int x, int y, int w, int h) { panes.Add(new RectInt(ix + x, iy + oy + y, w, h)); if (light != 0) lights.Add(new Vector4(ix + x + w * 0.5f, iy + oy + y + h * 0.4f, 84f, light == 2 ? 0.42f : 0.3f)); R(x - 2, y - 2, w + 4, h + 4, VBeam); R(x, y, w, h, C(30, 40, 84)); R(x + w / 2, y, 1, h, VBeam); R(x, y + h / 2, w, 1, VBeam); Dot(x + 3, y + h - 4, VSnow); Dot(x + w - 4, y + 3, VSnow); Dot(x + 6, y + 5, VSnowD); R(x - 2, y - 2, w + 4, 1, VSnow); }
            void Counter(int x, int w) { R(x, 0, w, 14, wood); R(x, 14, w, 2, woodL); R(x, 0, w, 1, woodD); for (int i = 6; i < w; i += 10) R(x + i, 2, 1, 11, woodD); }
            // the hearth: stone, with logs in it - the flames are a moving picture laid over it in the game
            void Fireplace(int x)
            {
                R(x, 0, 40, 44, VStone); R(x, 44, 40, 4, VStoneD); R(x - 2, 44, 44, 2, VStone);
                for (int y = 6; y < 44; y += 8) R(x, y, 40, 1, VStoneD);
                R(x + 8, 0, 24, 26, C(26, 20, 22));
                R(x + 11, 0, 18, 3, woodD); R(x + 13, 3, 14, 2, C(110, 60, 40)); R(x + 12, 1, 16, 1, C(190, 90, 50));
                R(x + 10, 48, 20, 48, VStoneD); R(x + 10, 48, 2, 48, VStone);
                R(x - 4, 0, 48, 2, C(60, 60, 70));                                           // hearthstone
                lights.Add(new Vector4(ix + x + 20, iy + oy + 12, 150f, 1.15f));
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
            // the apothecary's things
            Color32 glassC = C(196, 222, 226), cream = C(236, 224, 190), creamD = C(196, 180, 140);
            void Flask(int x, int y, int r, Color32 c)      // a round-bellied bottle with a neck and a cork
            {
                p.Disc(ix + x + r + 0.5f, iy + oy + y + r + 0.5f, r + 0.6f, Mul(c, 0.75f));
                p.Disc(ix + x + r + 0.5f, iy + oy + y + r + 0.5f, r - 0.4f, c);
                R(x + r - 1, y + 2 * r, 3, 3, glassC); R(x + r - 1, y + 2 * r + 3, 3, 2, C(150, 104, 62));
                Dot(x + r - 2, y + r + 1, C(255, 255, 255)); Dot(x + r - 1, y + r + 2, Lerp(c, C(255, 255, 255), 0.6f));
            }
            void Tall(int x, int y, int h, Color32 c)       // a tall bottle
            {
                R(x, y, 5, h, Mul(c, 0.8f)); R(x + 1, y + 1, 3, h - 2, c); R(x + 1, y + h, 3, 3, glassC); R(x + 1, y + h + 3, 3, 1, C(150, 104, 62));
                Dot(x + 1, y + h - 3, C(255, 255, 255));
            }
            void Jar(int x, int y, int w, int h, Color32 inside)      // a lidded jar, a herb inside, a paper label
            {
                R(x, y, w, h, glassC); R(x + 1, y + 1, w - 2, h * 2 / 3, inside); R(x - 1, y + h, w + 2, 2, C(110, 76, 48)); R(x + 1, y + h + 2, w - 2, 1, C(150, 104, 62));
                R(x + 1, y + h / 3, w - 2, 3, cream); R(x + 2, y + h / 3 + 1, w - 4, 1, creamD); Dot(x + w - 2, y + h - 2, C(255, 255, 255));
            }
            void Herbs(int x, int top, int len, Color32 c, Color32 flower)      // a bundle hung upside down to dry, on a string from the beam
            {
                R(x, top, 1, 96 - top, C(120, 100, 80));
                R(x - 1, top - 2, 3, 2, C(180, 150, 100));                                       // the tie
                for (int k = 0; k < len; k++)
                {
                    int w = 1 + k / 3;
                    for (int dx = -w; dx <= w; dx++)
                        if (((dx + k * 3) & 3) != 0 || Mathf.Abs(dx) < w) Dot(x + dx, top - 2 - k, ((dx + k) % 3 == 0) ? Mul(c, 0.75f) : c);
                    if (k > len / 2 && (k * 7) % 3 == 0) { Dot(x - w, top - 2 - k, flower); Dot(x + w, top - 3 - k, flower); }
                }
            }

            // ---------------------------------------------------------------- ground floor
            oy = 0;
            // the door back to the street (second cell from the left)
            R(14, 0, 20, 34, VBeam); R(16, 0, 16, 31, C(98, 62, 40)); for (int x = 19; x < 32; x += 4) R(x, 0, 1, 31, C(72, 46, 30));
            R(20, 18, 8, 8, VBeam); R(21, 19, 6, 6, C(30, 40, 84)); Dot(29, 14, C(236, 200, 110));
            panes.Add(new RectInt(ix + 21, iy + oy + 19, 6, 6));              // (the glass in the door shows the hour too)
            Rug(12, 24, C(120, 60, 60));

            switch (kind)
            {
                case 0:     // the forge: furnace, anvil, a rack of blades
                    R(168, 0, 50, 58, VStone); R(168, 58, 50, 4, VStoneD); for (int y = 8; y < 58; y += 9) R(168, y, 50, 1, VStoneD);
                    R(178, 6, 30, 26, C(26, 20, 22)); R(182, 6, 22, 4, C(190, 80, 40)); R(186, 6, 14, 2, C(240, 150, 70));          // a bed of coals; the fire burns over it
                    R(186, 62, 16, 34, VStoneD);
                    lights.Add(new Vector4(ix + 193, iy + 16, 150f, 1.15f));
                    Counter(76, 92);
                    R(74, 52, 60, 2, woodD); for (int i = 0; i < 6; i++) { R(78 + i * 9, 54, 2, 20, C(190, 196, 210)); R(76 + i * 9, 54, 6, 2, C(120, 84, 52)); R(78 + i * 9, 50, 2, 4, C(90, 60, 40)); }
                    if (roomSkip != "anvil") { R(142, 0, 14, 5, VStoneD); R(140, 5, 18, 4, C(70, 72, 84)); R(144, 9, 10, 3, C(70, 72, 84)); R(138, 12, 22, 4, C(96, 98, 112)); }       // anvil
                    R(150, 40, 10, 14, C(60, 62, 76)); R(152, 54, 6, 6, C(120, 84, 52));
                    // hammers and tongs on the wall over the counter, horseshoes nailed up for luck
                    for (int i = 0; i < 4; i++) { R(80 + i * 11, 28, 2, 14, wood); R(77 + i * 11, 40, 8, 4, C(70, 72, 84)); R(77 + i * 11, 43, 8, 1, C(120, 122, 136)); }
                    R(126, 28, 1, 16, C(90, 92, 104)); R(130, 28, 1, 16, C(90, 92, 104)); R(126, 42, 5, 2, C(70, 72, 84));
                    foreach (int hx in new[] { 138, 148 }) { R(hx, 76, 2, 8, C(130, 132, 146)); R(hx + 6, 76, 2, 8, C(130, 132, 146)); R(hx, 76, 8, 2, C(130, 132, 146)); }
                    // the bellows by the furnace, a heap of coal before it
                    if (roomSkip != "bellows") { R(156, 30, 10, 10, C(110, 70, 44)); R(158, 40, 6, 3, woodD); R(160, 26, 2, 4, C(70, 72, 84)); for (int k = 0; k < 10; k++) Dot(156 + k, 30 + (k % 2), C(84, 52, 32)); }
                    for (int k = 0; k < 15; k++) R(170 + k, 0, 1, Mathf.Max(1, 4 - Mathf.Abs(k - 7) / 2), (k % 3 == 0) ? C(52, 48, 50) : C(30, 28, 30));
                    Lamp(100, 80);
                    break;
                case 1:     // the ropewalk: spools and coils everywhere
                    Shelf(80, 58, 70); for (int i = 0; i < 5; i++) { R(84 + i * 13, 61, 10, 12, C(206, 176, 112)); R(87 + i * 13, 64, 4, 6, C(150, 120, 70)); }
                    Shelf(160, 44, 50); for (int i = 0; i < 3; i++) { R(164 + i * 15, 47, 9, 8, C(110, 72, 44)); R(164 + i * 15, 47, 4, 3, C(84, 54, 34)); }
                    Counter(76, 92);
                    for (int i = 0; i < 3; i++) { R(180 + i * 12, 0, 10, 4 + i * 3, C(206, 176, 112)); R(183 + i * 12, 1, 4, 2, C(150, 120, 70)); }
                    for (int x = 70; x < 220; x += 3) Dot(x, 92 - (int)(Mathf.Sin((x - 70) / 150f * Mathf.PI) * 9f), C(206, 176, 112));
                    // coils hanging from pegs under the shelf, a pulley block, and the ledger on its stand
                    for (int i = 0; i < 3; i++) { int cx = 88 + i * 22; R(cx, 52, 2, 4, woodD); p.Ellipse(ix + cx + 1, iy + oy + 44, 7f, 6f, C(206, 176, 112)); p.Ellipse(ix + cx + 1, iy + oy + 44, 4f, 3f, C(150, 120, 70)); p.Ellipse(ix + cx + 1, iy + oy + 44, 2f, 1.5f, C(70, 50, 38)); }
                    p.Disc(ix + 158.5f, iy + oy + 76.5f, 4f, woodD); p.Disc(ix + 158.5f, iy + oy + 76.5f, 1.5f, C(190, 196, 210)); R(158, 81, 1, 15, C(150, 120, 70));
                    R(183, 0, 3, 20, woodD); R(176, 20, 17, 3, woodL);          // the ledger's stand (the ledger on it is a Curio)
                    // the stand of the great wheel the strands are twisted on (the wheel itself turns: RoomToy)
                    {
                        R(198, 44, 4, 26, woodD); R(192, 44, 16, 3, woodD);
                        for (int k = 0; k < 18; k++) Dot(186 - k, 70 - k / 3, C(206, 176, 112));      // three strands running from it
                    }
                    // lengths of finished rope hanging from hooks by the stair, a board of sample knots
                    for (int i = 0; i < 4; i++) { R(68 + i * 3, 90, 2, 3, C(90, 92, 104)); for (int y = 30 + i * 4; y < 90; y++) Dot(68 + i * 3 + ((y / 3) % 2), y, (y + i) % 4 == 0 ? C(170, 140, 90) : C(206, 176, 112)); }
                    R(112, 74, 30, 20, woodD); R(114, 76, 26, 16, C(150, 104, 62));
                    for (int k = 0; k < 3; k++) for (int j = 0; j < 2; j++) { int kx = 118 + k * 8, ky = 80 + j * 7; p.Disc(ix + kx + 0.5f, iy + oy + ky + 0.5f, 2.2f, C(206, 176, 112)); Dot(kx, ky, C(150, 120, 70)); R(kx, ky - 4, 1, 3, C(206, 176, 112)); }
                    // (the great coil on the floor by the counter is RoomToy's)
                    Lamp(150, 80);
                    break;
                case 2:     // the apothecary: a great cabinet of bottles and jars, a cauldron on its tripod, herbs drying from the beams
                {
                    Color32 green = C(110, 180, 90), sage = C(150, 170, 120), lav = C(170, 130, 200), red = C(200, 70, 80), teal = C(80, 210, 200), amber = C(230, 160, 70), blue = C(90, 130, 210);
                    // the cabinet behind the counter: dark back, three shelves, a cornice
                    R(70, 26, 100, 68, woodD); R(72, 28, 96, 64, C(46, 32, 26));
                    R(70, 26, 3, 68, wood); R(167, 26, 3, 68, wood); R(118, 28, 2, 64, wood); R(70, 92, 100, 2, woodL); R(68, 94, 104, 2, woodD);
                    foreach (int sy in new[] { 44, 60, 76 }) { R(72, sy, 96, 2, woodL); R(72, sy - 1, 96, 1, woodD); R(72, sy + 2, 96, 1, Mul(woodD, 0.7f)); }
                    // the lowest shelf: jars of herbs, and two round flasks
                    Jar(75, 29, 10, 10, green); Jar(88, 29, 10, 12, lav); Jar(101, 29, 10, 9, sage);
                    Flask(122, 28, 5, red); Flask(134, 28, 4, blue); Tall(146, 28, 12, amber); Tall(154, 28, 10, green); Jar(161, 29, 6, 8, red);
                    // the middle shelf: a row of bottles, and one that glows
                    Tall(75, 46, 10, blue); Tall(82, 46, 12, red); Flask(89, 46, 4, amber); Jar(100, 46, 8, 9, sage); Tall(110, 46, 9, lav);
                    Flask(124, 46, 5, teal); lights.Add(new Vector4(ix + 129.5f, iy + oy + 52, 34f, 0.5f));
                    Jar(137, 47, 9, 8, amber); Tall(149, 46, 11, green); Tall(156, 46, 8, blue); Tall(162, 46, 10, red);
                    // the top shelf: small vials in a rack, a mortar, a stack of books
                    for (int i = 0; i < 6; i++) { R(76 + i * 5, 62, 3, 8, new[] { red, green, blue, amber, lav, teal }[i]); R(76 + i * 5, 70, 3, 1, glassC); }
                    R(75, 62, 30, 2, woodD);
                    R(108, 62, 9, 5, C(150, 146, 140)); R(109, 66, 7, 1, C(96, 92, 88));                                   // a stone bowl
                    R(122, 62, 18, 3, C(120, 60, 50)); R(123, 65, 16, 3, C(60, 90, 120)); R(121, 68, 18, 3, C(150, 120, 70));      // books, lying
                    Flask(144, 62, 5, lav); Tall(157, 62, 11, teal); lights.Add(new Vector4(ix + 159.5f, iy + oy + 68, 28f, 0.42f));
                    // the very top: big jars and a vase of dried flowers
                    Jar(78, 78, 12, 13, green); Jar(94, 78, 10, 11, amber); R(128, 78, 8, 8, C(150, 104, 62)); for (int i = 0; i < 5; i++) Dot(129 + i, 87 + i % 3, i % 2 == 0 ? lav : red);
                    Jar(146, 78, 12, 12, lav);
                    // the counter, and what is on it: a mortar and pestle, an open book, a brass scale, a candle
                    Counter(76, 92);
                    R(84, 16, 11, 6, C(150, 146, 140)); R(85, 21, 9, 1, C(96, 92, 88)); for (int k = 0; k < 7; k++) Dot(92 + k / 2, 20 + k, C(170, 140, 100));
                    R(100, 16, 20, 2, woodD); R(101, 18, 8, 2, cream); R(111, 18, 8, 2, cream); R(109, 17, 2, 2, creamD);
                    for (int k = 0; k < 3; k++) { Dot(102 + k * 2, 19, creamD); Dot(113 + k * 2, 19, creamD); }
                    Color32 brass = C(214, 170, 80), brassD = C(160, 118, 44);
                    R(133, 16, 2, 15, brassD); R(126, 30, 16, 1, brass); R(124, 24, 7, 2, brass); R(137, 26, 7, 2, brass);
                    for (int k = 0; k < 5; k++) { Dot(126, 25 + k, brassD); Dot(141, 27 + k * 3 / 4, brassD); } R(131, 16, 6, 1, brassD);
                    R(152, 16, 3, 7, cream); R(151, 16, 5, 1, creamD); Dot(153, 23, C(255, 220, 120)); Dot(153, 24, C(255, 250, 220));
                    lights.Add(new Vector4(ix + 153.5f, iy + oy + 24, 46f, 0.75f));
                    // the cauldron on its tripod, over a little fire on a hearthstone; a copper hood and its pipe above
                    R(176, 0, 46, 5, VStone); R(176, 4, 46, 1, VStoneD); for (int x = 180; x < 222; x += 9) R(x, 0, 1, 4, VStoneD);
                    R(180, 5, 40, 44, C(52, 44, 44)); R(180, 5, 40, 2, C(70, 60, 58));                                  // the sooted wall behind it
                    Color32 iron = C(54, 54, 64), ironL = C(96, 98, 112);
                    if (roomSkip != "cauldron") {
                    R(186, 5, 2, 12, iron); R(212, 5, 2, 12, iron); R(199, 5, 2, 9, iron);
                    p.Disc(ix + 200.5f, iy + oy + 22.5f, 12.6f, iron); p.Disc(ix + 200.5f, iy + oy + 22.5f, 11.4f, C(40, 40, 48));
                    p.Rect(ix + 186, iy + oy + 26, 30, 10, C(52, 44, 44));                                             // the pot is open at the top
                    // its mouth seen from a little above: the near rim, the brew inside, the far rim
                    R(186, 25, 29, 2, ironL); R(185, 25, 1, 2, iron); R(215, 25, 1, 2, iron);
                    R(188, 27, 25, 2, C(110, 220, 130)); R(191, 28, 7, 1, C(190, 255, 200)); R(203, 28, 4, 1, C(190, 255, 200));
                    R(188, 29, 25, 1, iron);
                    for (int k = 0; k < 4; k++) Dot(195 + k * 4, 31 + k % 2, C(170, 250, 180));
                    Dot(189, 18, ironL); Dot(190, 15, ironL); Dot(191, 13, ironL);
                    }
                    lights.Add(new Vector4(ix + 200, iy + oy + 34, 58f, 0.55f));                                      // the brew glows
                    lights.Add(new Vector4(ix + 200, iy + oy + 6, 92f, 0.9f));                                        // and the fire under it
                    Color32 copper = C(196, 116, 64), copperD = C(140, 76, 40), copperL = C(232, 160, 100);
                    for (int k = 0; k < 14; k++) { int w = 40 - k * 2; R(200 - w / 2, 54 + k, w, 1, k == 0 ? copperD : copper); Dot(200 - w / 2, 54 + k, copperD); Dot(200 + w / 2 - 1, 54 + k, copperD); }
                    R(181, 54, 38, 1, copperL); R(194, 68, 12, 28, copperD); R(196, 68, 3, 28, copper); for (int y = 74; y < 96; y += 8) R(194, y, 12, 1, C(110, 60, 30));
                    // her recipe, pinned to the wall beside the hood
                    // (her recipe, pinned to the wall beside the hood, is a Curio)
                    // herbs drying from the beam, all along the room; a cord of flowers across
                    Herbs(76, 88, 11, green, lav); Herbs(88, 86, 13, sage, C(240, 220, 120)); Herbs(101, 89, 10, C(120, 150, 70), red);
                    Herbs(114, 85, 14, green, lav); Herbs(128, 88, 11, sage, lav); Herbs(142, 86, 13, C(100, 160, 100), C(240, 220, 120)); Herbs(156, 89, 10, green, red);
                    for (int x = 66; x < 176; x++) Dot(x, 93 - (int)(Mathf.Sin((x - 66) / 110f * Mathf.PI) * 3f), C(150, 120, 80));
                    for (int x = 70; x < 174; x += 7) Dot(x, 92 - (int)(Mathf.Sin((x - 66) / 110f * Mathf.PI) * 3f), (x / 7) % 2 == 0 ? lav : C(240, 220, 120));
                    // a pot of mint by the cabinet, a basket of roots by the hearth
                    R(64, 0, 8, 6, C(176, 96, 60)); R(63, 6, 10, 2, C(196, 116, 74)); for (int k = 0; k < 9; k++) Dot(64 + (k * 5) % 9, 8 + k % 4, k % 2 == 0 ? green : C(80, 150, 70));
                    R(168, 0, 10, 6, C(170, 130, 70)); for (int x = 168; x < 178; x += 2) R(x, 1, 1, 4, C(130, 96, 50)); Dot(170, 7, C(200, 160, 110)); Dot(173, 7, C(180, 120, 90)); Dot(175, 6, C(200, 160, 110));
                    // a lantern on a chain near the door
                    Lamp(40, 74); lights[lights.Count - 1] = new Vector4(lights[lights.Count - 1].x, lights[lights.Count - 1].y, 70f, 0.7f);       // (a small lantern: it lights the door)
                    break;
                }
                case 3:     // the map room: a great map on the wall, a desk with a globe
                    R(82, 44, 84, 46, woodD); R(84, 46, 80, 42, C(232, 220, 190));
                    for (int i = 0; i < 9; i++) R(90 + i * 8, 52 + (i * 7) % 20, 5 + i % 3, 3 + i % 2, C(150, 170, 120));
                    for (int x = 86; x < 162; x += 4) Dot(x, 60 + (int)(Mathf.Sin(x * 0.3f) * 6f), C(120, 96, 70));
                    R(150, 78, 3, 3, C(214, 70, 80)); R(100, 58, 3, 3, C(214, 70, 80));
                    Counter(76, 92);
                    Table(170, 46); R(176, 15, 16, 2, C(232, 220, 190)); R(198, 15, 2, 8, woodD); /* the globe on its stand is RoomToy's */
                    // rolled maps standing in a basket, a candle and a quill on the counter, a sea chart pinned beside the map
                    R(64, 0, 10, 10, C(170, 130, 70)); for (int x = 64; x < 74; x += 2) R(x, 1, 1, 8, C(130, 96, 50));
                    for (int i = 0; i < 4; i++) { R(65 + i * 2, 10, 2, 8 + (i * 3) % 5, cream); Dot(65 + i * 2, 10 + 8 + (i * 3) % 5, creamD); }
                    R(150, 16, 3, 7, cream); Dot(151, 23, C(255, 220, 120)); Dot(151, 24, C(255, 250, 220)); lights.Add(new Vector4(ix + 151.5f, iy + oy + 24, 46f, 0.7f));
                    R(120, 16, 14, 2, cream); for (int k = 0; k < 6; k++) Dot(128 + k, 18 + k, C(230, 230, 236));
                    R(170, 50, 18, 22, cream); R(170, 50, 18, 1, creamD); for (int k = 0; k < 12; k++) Dot(172 + k, 56 + (int)(Mathf.Sin(k) * 3f), C(90, 130, 180)); Dot(179, 70, C(200, 70, 80));
                    Lamp(190, 78);
                    break;
                case 4:     // Pip's Oddments: crates, barrels, rugs and far too many lamps
                    Crate(170, 0, 20); Crate(192, 0, 16); Crate(176, 20, 16); Barrel(210, 0);
                    Counter(76, 92); R(84, 16, 8, 6, C(214, 70, 80)); R(100, 16, 6, 9, C(90, 140, 210)); R(150, 16, 10, 5, C(236, 196, 90));
                    R(84, 50, 26, 34, C(150, 70, 110)); for (int y = 54; y < 84; y += 6) R(86, y, 22, 2, C(236, 196, 90));
                    R(120, 46, 22, 38, C(70, 130, 140)); for (int y = 50; y < 84; y += 8) R(122, y, 18, 3, C(240, 232, 214));
                    Shelf(150, 60, 60); Bottles(154, 63, 4, 4); R(182, 63, 8, 8, C(200, 170, 90)); R(194, 63, 10, 5, C(170, 100, 190));
                    // a clock on the wall, a stuffed owl on the crates, a birdcage, a pile of books by the stair
                    R(159, 35, 3, 4, woodD);          // (the clock over it is RoomToy's)
                    /* the owl on the crates is RoomToy's */
                    if (roomSkip != "cage") { for (int k = 0; k < 9; k++) { Dot(136 + k, 70, C(214, 170, 80)); Dot(136 + k, 82, C(214, 170, 80)); } for (int x = 136; x < 145; x += 2) R(x, 70, 1, 12, C(214, 170, 80)); R(139, 83, 3, 13, C(150, 120, 70)); }      // (the bird in it is RoomToy's)
                    R(64, 0, 12, 3, C(150, 70, 70)); R(65, 3, 10, 3, C(70, 110, 150)); R(64, 6, 11, 2, C(214, 170, 80));
                    Lamp(112, 86); Lamp(170, 82); Lamp(204, 78);
                    break;
                case 5:     // the lamp house: lamps in rows, oil in barrels
                    for (int i = 0; i < 7; i++) Lamp(74 + i * 22, 74 + (i % 2) * 8);
                    Shelf(80, 46, 80); for (int i = 0; i < 7; i++) { R(84 + i * 11, 49, 7, 9, VBeam); R(85 + i * 11, 50, 5, 6, i % 3 == 0 ? VGlassOffL : VGlowL); }
                    Counter(76, 92);
                    Barrel(176, 0); Barrel(190, 0); Barrel(183, 16); R(206, 0, 10, 14, C(214, 170, 80)); R(208, 14, 6, 3, C(150, 108, 44));
                    // oil cans and spools of wick on the counter, a long snuffer on the wall
                    R(84, 16, 6, 8, C(170, 140, 70)); R(85, 24, 2, 3, C(150, 120, 60)); R(96, 16, 6, 6, C(170, 140, 70)); R(103, 21, 3, 1, C(150, 120, 60));
                    for (int i = 0; i < 3; i++) { R(116 + i * 9, 16, 7, 5, C(232, 226, 214)); R(116 + i * 9, 18, 7, 1, C(200, 190, 170)); }
                    R(150, 30, 1, 30, C(160, 118, 44)); R(148, 60, 5, 3, C(160, 118, 44));
                    // glass chimneys drying in a rack, rags, the lighter's pole leaning by the stair
                    R(156, 26, 18, 2, woodD); for (int i = 0; i < 3; i++) { R(158 + i * 6, 28, 4, 9, C(200, 225, 230)); R(159 + i * 6, 29, 2, 7, C(230, 245, 250)); }
                    R(66, 0, 1, 70, C(150, 104, 62)); R(64, 70, 5, 3, C(160, 118, 44)); Dot(66, 73, C(255, 220, 120));
                    R(132, 16, 10, 3, C(200, 190, 170)); R(134, 19, 6, 2, C(180, 170, 150));
                    break;
                case 6:     // the village hall: the hearth, a long table, the banner of Brindlemoor
                    Fireplace(176);
                    // (the banner of Brindlemoor is RoomToy's: it stirs when touched)
                    Table(72, 92); if (roomSkip != "cup") R(80, 15, 6, 5, C(214, 170, 80)); R(100, 15, 10, 3, C(232, 220, 190)); R(134, 15, 4, 8, C(190, 196, 210)); R(135, 23, 2, 3, VGlowL);
                    R(164, 0, 2, 22, woodD); R(164, 10, 10, 2, wood); R(172, 0, 2, 10, woodD);
                    R(136, 60, 26, 2, woodD); R(140, 62, 3, 10, C(232, 220, 190)); R(154, 62, 3, 10, C(232, 220, 190)); R(146, 62, 5, 6, C(150, 110, 70));
                    // portraits of the elders before him, and antlers over the hearth
                    foreach (int px in new[] { 72, 136 }) { R(px, 50, 18, 22, C(150, 110, 50)); R(px + 2, 52, 14, 18, C(70, 60, 70)); p.Disc(ix + px + 9.5f, iy + oy + 63.5f, 3.5f, C(200, 170, 140)); R(px + 4, 52, 10, 6, C(60, 60, 90)); }
                    // over the hearth: the board of names, everyone who went up the mountain for the Sunstone
                    R(178, 58, 40, 24, woodD); R(180, 60, 36, 20, C(150, 104, 62)); R(178, 81, 40, 2, woodL);
                    for (int row = 0; row < 5; row++)
                        for (int x = 183; x < 213; x += 1)
                            if (((x * 7 + row * 13) % 9) < 6 && !((x - 183 + row * 3) % 11 == 10)) Dot(x, 77 - row * 4, C(84, 54, 34));
                    Dot(197, 63, C(230, 190, 100)); Dot(198, 63, C(230, 190, 100));          // the newest name, still pale where it was cut
                    R(196, 82, 4, 3, C(214, 170, 80));
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
                    // a clock over the table, a basket of logs by the fire, a jug and cups on the table
                    p.Disc(ix + 106.5f, iy + oy + 86.5f, 5f, woodD); p.Disc(ix + 106.5f, iy + oy + 86.5f, 3.5f, cream); R(106, 86, 1, 3, C(40, 30, 30)); R(106, 86, 2, 1, C(40, 30, 30));
                    R(166, 0, 12, 8, C(170, 130, 70)); for (int i = 0; i < 4; i++) R(167 + i * 3, 8, 3, 3, C(120, 80, 50));
                    R(92, 15, 5, 7, C(176, 96, 60)); R(98, 15, 3, 3, cream); R(120, 15, 3, 3, cream);
                    // a coat and a scarf on hooks by the door (the cat asleep by the fire is RoomToy's)
                    R(38, 40, 2, 3, woodD); R(34, 18, 10, 22, C(90, 70, 60)); R(34, 18, 10, 1, C(60, 46, 40)); R(44, 40, 2, 3, woodD); R(44, 28, 3, 12, C(176, 62, 64)); R(43, 26, 5, 2, C(176, 62, 64));
                    break;
            }

            // ---------------------------------------------------------------- upstairs: where the keeper lives
            oy = UpperY;
            switch (kind)
            {
                case 0: Bed(150, C(110, 110, 124)); Crate(90, 0, 18); Crate(110, 0, 14); Barrel(128, 0); NightWindow(100, 46, 18, 20); Shelf(160, 56, 44); R(164, 59, 3, 12, C(190, 196, 210)); R(172, 59, 3, 10, C(190, 196, 210)); R(182, 59, 8, 6, C(96, 98, 112));
                    // a round shield and two crossed blades on the wall, an apron on a peg
                    p.Disc(ix + 80.5f, iy + oy + 64.5f, 9f, C(110, 70, 44)); p.Disc(ix + 80.5f, iy + oy + 64.5f, 7f, C(130, 86, 54)); p.Disc(ix + 80.5f, iy + oy + 64.5f, 2.5f, C(190, 196, 210));
                    p.Line(ix + 132, iy + oy + 56, ix + 148, iy + oy + 74, C(190, 196, 210)); p.Line(ix + 148, iy + oy + 56, ix + 132, iy + oy + 74, C(190, 196, 210));
                    R(131, 54, 4, 3, C(120, 84, 52)); R(145, 54, 4, 3, C(120, 84, 52));
                    R(212, 40, 2, 3, woodD); R(208, 22, 10, 18, C(96, 70, 52)); R(208, 22, 10, 1, C(70, 50, 38));
                    Lamp(130, 82); break;
                case 1: Bed(150, C(96, 140, 104)); R(86, 0, 22, 30, woodD); R(88, 2, 18, 26, wood); for (int y = 6; y < 28; y += 7) R(88, y, 18, 1, C(206, 176, 112)); NightWindow(118, 46, 18, 20); for (int i = 0; i < 3; i++) R(180 + i * 11, 56, 9, 9, C(206, 176, 112)); Shelf(176, 53, 40);
                    // the cut harness on the loom, a coil on the floor, a pair of climbing boots
                    for (int k = 0; k < 12; k++) { Dot(90 + k, 50 - k / 2, C(150, 110, 60)); Dot(104 - k / 2, 50 - k, C(150, 110, 60)); } R(95, 40, 4, 3, C(190, 196, 210));
                    p.Ellipse(ix + 128.5f, iy + oy + 3.5f, 8f, 3.5f, C(206, 176, 112)); p.Ellipse(ix + 128.5f, iy + oy + 3.5f, 4f, 1.5f, C(150, 120, 70));
                    R(214, 0, 4, 9, C(96, 62, 40)); R(214, 0, 7, 3, C(96, 62, 40));
                    // a fishing net hung to dry along the wall, a hammock rolled up, a chest of tools
                    for (int y = 64; y < 92; y++) for (int x = 150; x < 176; x++) if ((x + y) % 6 == 0 || (x - y + 300) % 6 == 0) Dot(x, y, C(190, 170, 120));
                    R(150, 91, 26, 2, woodD);
                    R(59, 42, 2, 2, woodD); R(81, 42, 2, 2, woodD);          // (the hammock between the hooks is RoomToy's)
                    // (Tam's chest, by the bed, is a Curio)
                    Lamp(140, 82); break;
                case 2:     // Maren's own room: the cabinet of jars kept by night, the window with its candle stubs, her bed and a coat on it
                {
                    Color32 red = C(200, 70, 80), lav = C(170, 130, 200), green = C(110, 180, 90), amber = C(230, 160, 70);
                    // the cabinet: glass doors, labelled jars behind them
                    R(82, 0, 30, 60, woodD); R(84, 2, 26, 56, C(46, 32, 26)); R(81, 60, 32, 3, woodL);
                    for (int y = 4; y < 56; y += 13)
                    {
                        R(84, y - 1, 26, 1, woodL);
                        for (int i = 0; i < 3; i++) Jar(86 + i * 8, y, 6, 8, new[] { red, lav, green, amber }[(i + y / 13) % 4]);
                    }
                    R(96, 2, 2, 56, wood); for (int y = 6; y < 56; y += 9) { Dot(88, y, C(230, 240, 245)); Dot(102, y + 3, C(230, 240, 245)); }       // the glass doors catch the light
                    // the window: curtains, a sill, three candle stubs burned to the same height
                    NightWindow(120, 46, 18, 20);
                    R(116, 43, 26, 3, woodL); R(116, 42, 26, 1, woodD);
                    foreach (int cxs in new[] { 121, 127, 133 }) { R(cxs, 46, 3, 3, cream); Dot(cxs + 1, 49, C(60, 50, 46)); R(cxs - 1, 46, 5, 1, creamD); }
                    // the bed under a patchwork quilt; a man's coat folded at its foot
                    Bed(150, C(190, 70, 80));
                    for (int i = 0; i < 4; i++) for (int j = 0; j < 2; j++) R(171 + i * 9, 8 + j * 4, 8, 3, new[] { C(170, 60, 70), C(214, 170, 80), C(120, 150, 110), C(150, 100, 160) }[(i + j) % 4]);
                    R(194, 13, 14, 4, C(58, 60, 76)); R(194, 16, 14, 1, C(84, 86, 104)); R(196, 14, 3, 2, C(40, 40, 52)); Dot(205, 15, C(214, 170, 80));
                    Rug(142, 74, C(120, 70, 90));
                    // a stool with a candle by the bed, a basket of linen
                    R(140, 0, 2, 10, woodD); R(146, 0, 2, 10, woodD); R(139, 10, 10, 2, woodL);
                    R(142, 12, 3, 6, cream); Dot(143, 18, C(255, 220, 120)); Dot(143, 19, C(255, 250, 220)); lights.Add(new Vector4(ix + 143.5f, iy + oy + 19, 46f, 0.75f));
                    R(114, 0, 14, 8, C(170, 130, 70)); for (int x = 114; x < 128; x += 2) R(x, 1, 1, 6, C(130, 96, 50)); R(115, 8, 12, 2, C(232, 226, 214));
                    // dried lavender over the bed
                    Herbs(166, 88, 9, lav, C(220, 200, 240)); Herbs(180, 86, 11, C(140, 170, 110), lav); Herbs(194, 88, 9, lav, C(220, 200, 240));
                    Lamp(58, 80); lights[lights.Count - 1] = new Vector4(lights[lights.Count - 1].x, lights[lights.Count - 1].y, 64f, 0.6f);
                    break;
                }
                case 3: Bed(84, C(84, 110, 170)); NightWindow(160, 40, 28, 30); if (roomSkip != "telescope") { R(196, 0, 3, 34, woodD); R(190, 0, 14, 2, woodD); for (int i = 0; i < 16; i++) Dot(197 - i, 34 + i / 2, C(214, 170, 80)); R(180, 40, 4, 4, C(214, 170, 80)); }       // a telescope at the big window
                    Table(100, 44); R(104, 15, 30, 2, C(232, 220, 190));
                    // charts pinned over the table, books stacked on the floor, a candle
                    R(100, 48, 16, 14, cream); R(120, 52, 18, 12, Lerp(cream, C(200, 180, 140), 0.4f)); R(104, 66, 14, 10, cream);
                    for (int k = 0; k < 10; k++) { Dot(102 + k, 54 + k % 3, C(120, 96, 76)); Dot(122 + k, 57 + (k / 2) % 3, C(90, 130, 180)); }
                    Dot(108, 61, C(200, 70, 80)); Dot(128, 63, C(200, 70, 80)); Dot(110, 75, C(200, 70, 80));
                    R(150, 0, 12, 3, C(120, 60, 50)); R(151, 3, 10, 3, C(60, 90, 120)); R(150, 6, 12, 3, C(150, 120, 70)); R(152, 9, 8, 2, C(90, 120, 80));
                    R(132, 15, 3, 6, cream); Dot(133, 21, C(255, 220, 120)); lights.Add(new Vector4(ix + 133.5f, iy + oy + 21, 44f, 0.7f));
                    Lamp(150, 82); break;
                case 4: Bed(150, C(170, 100, 170)); Crate(84, 0, 20); Crate(106, 0, 16); Crate(90, 20, 16); Crate(124, 0, 14); NightWindow(128, 50, 16, 18);
                    // the packed bag by the bed, a hat on the stand, paintings leaning on the crates
                    R(212, 0, 10, 9, C(120, 84, 52)); R(214, 9, 6, 2, C(90, 60, 40)); R(213, 4, 8, 1, C(214, 170, 80));
                    R(146, 0, 2, 30, woodD); R(141, 30, 12, 3, C(60, 50, 70)); R(144, 33, 6, 3, C(60, 50, 70));
                    R(108, 16, 14, 12, woodD); R(110, 18, 10, 8, C(90, 130, 110)); R(92, 36, 12, 10, woodD); R(94, 38, 8, 6, C(170, 110, 90));
                    Lamp(110, 84); Lamp(190, 80); break;
                case 5:     // the great lamp of the tower, behind its glass
                    if (roomSkip != "greatlamp") { R(100, 0, 60, 6, VStoneD); R(108, 6, 44, 60, VBeam); R(112, 10, 36, 52, VGlow); R(120, 18, 20, 36, VGlowL); R(129, 6, 2, 60, VBeam); R(108, 34, 44, 2, VBeam); }
                    lights.Add(new Vector4(ix + 130, iy + oy + 36, 150f, 1.2f));
                    R(104, 66, 52, 4, VStoneD); Bed(164 - 4, C(96, 100, 150)); Barrel(84, 0);
                    // the night's logbook on a stand, oil-stained boards, a chair by the lamp
                    R(70, 0, 3, 22, woodD); R(64, 22, 15, 3, woodL);            // (the logbook on the stand is a Curio)
                    p.Ellipse(ix + 130.5f, iy + oy + 1.5f, 10f, 1.5f, C(60, 46, 36));
                    R(100, 0, 2, 14, woodD); R(110, 0, 2, 8, woodD); R(100, 8, 12, 2, woodL); R(100, 14, 2, 10, woodD);
                    break;
                case 6: Bed(150, C(150, 60, 64)); R(84, 0, 30, 60, woodD); for (int y = 4; y < 58; y += 14) { R(86, y, 26, 11, C(40, 28, 24)); for (int i = 0; i < 7; i++) R(87 + i * 3 + i / 3, y, 3, 8 + (i * 5 + y) % 3, new[] { C(170, 60, 60), C(70, 110, 160), C(90, 140, 90), C(200, 170, 90) }[(i + y) % 4]); }
                    Table(118, 28); R(122, 15, 14, 2, C(232, 220, 190)); R(138, 15, 3, 7, VGlowL); NightWindow(124, 50, 16, 18); Rug(118, 90, C(150, 44, 50));
                    // a candlestand with three candles, a cloak on a peg
                    if (roomSkip != "candles") { R(212, 0, 2, 26, C(160, 118, 44)); R(207, 26, 12, 2, C(160, 118, 44)); foreach (int cxs in new[] { 207, 212, 217 }) R(cxs, 28, 2, 6, cream); }      // (their flames are RoomToy's)
                    lights.Add(new Vector4(ix + 212, iy + oy + 34, 52f, 0.75f));
                    R(150, 44, 2, 3, woodD); R(146, 22, 12, 22, C(110, 40, 50)); R(146, 22, 12, 1, C(80, 28, 36));
                    Lamp(170, 84); break;
                default:    // home, upstairs: the bed the player wakes up in
                    if (roomSkip != "bed") Bed(110, C(176, 62, 64)); Wardrobe(184);                    // (the chest at the foot of the bed is a Curio)
                    NightWindow(128, 46, 22, 24); Rug(104, 72, C(84, 110, 150)); Lamp(96, 82);
                    R(176, 60, 5, 5, VBeam);
                    // a child's drawing pinned over the bed
                    R(112, 44, 14, 11, cream); p.Disc(ix + 119.5f, iy + oy + 51.5f, 2.5f, C(240, 190, 70)); R(113, 45, 12, 2, C(240, 240, 250)); for (int k = 0; k < 4; k++) Dot(115 + k * 2, 47, C(60, 60, 60));
                    break;
            }

            // ---------------------------------------------------------------- the ladder, from the ground floor up through the hatch
            oy = 0;
            R(LadderX, 0, 2, UpperY + 6, woodD); R(LadderX + 14, 0, 2, UpperY + 6, woodD);
            R(LadderX + 1, 0, 1, UpperY + 6, woodL); R(LadderX + 15, 0, 1, UpperY + 6, woodL);
            for (int y = 3; y < UpperY + 4; y += 5) { R(LadderX + 2, y, 12, 2, wood); R(LadderX + 2, y, 12, 1, woodD); }

            // ---------------------------------------------------------------- shadow and light
            // soft shadow under the ceilings and along the walls, a little grain in every surface
            for (int y = RY; y < RY + 240; y++)
                for (int x = RX; x < RX + 256; x++)
                {
                    var c = p.Get(x, y);
                    if (c.a == 0) continue;
                    float k = 1f;
                    if (((x * 7 + y * 13) ^ (x * y)) % 11 == 0) k *= 0.93f;
                    int fromLeft = x - (RX + 16), fromRight = RX + 240 - x;
                    if (fromLeft >= 0 && fromLeft < 10) k *= 0.8f + fromLeft * 0.02f;
                    if (fromRight > 0 && fromRight <= 10) k *= 0.8f + (fromRight - 1) * 0.02f;
                    foreach (int ceil in new[] { iy + 96, iy + 208 })
                    {
                        int below = ceil - y;
                        if (below > 0 && below <= 8) k *= 0.78f + below * 0.0275f;
                    }
                    // the light of the lamps and the fire: warm where it reaches, cold blue dusk where it does not
                    float lit = 0f;
                    foreach (var l in lights)
                    {
                        float dx = x - l.x, dy = (y - l.y) * 1.15f;
                        float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / l.z);
                        lit = Mathf.Max(lit, f * f * (3f - 2f * f) * l.w);
                    }
                    lit = Mathf.Clamp01((light == 0 ? 0.32f : 0.26f) + lit);
                    float r = c.r * k, g = c.g * k, b = c.b * k;
                    float dr = r * 0.34f, dg = g * 0.4f, db = b * 0.58f + 6f;                    // unlit: dim and blue
                    // by day the corners are not dark: the room stands in plain daylight, a little cool; at dawn and at dusk
                    // it is dim still, but the dimness has the colour of the sky outside
                    if (light == 2) { dr = r * 0.8f; dg = g * 0.83f; db = b * 0.88f + 3f; }
                    else if (light == 1) { dr = r * 0.56f; dg = g * 0.5f; db = b * 0.56f + 4f; }
                    else if (light == 3) { dr = r * 0.6f; dg = g * 0.48f; db = b * 0.46f + 3f; }
                    float wr = r * 1.1f + 6f, wg = g * 1.0f + 2f, wb = b * 0.84f;                 // lit: warm
                    float or = Mathf.Lerp(dr, wr, lit), og = Mathf.Lerp(dg, wg, lit), ob = Mathf.Lerp(db, wb, lit);
                    // a shaft of light falling in through a window: moonlight by night (cold), the sun by day (warm)
                    foreach (var bm in beams)
                    {
                        float down = bm.y - y;
                        if (down < 0f || y < bm.w) continue;
                        float x0 = bm.x + down * 0.55f;
                        if (x < x0 || x >= x0 + bm.z) continue;
                        float edge = Mathf.Min(x - x0, x0 + bm.z - 1 - x) / 3f;
                        float f = Mathf.Clamp01(edge) * (1f - down / Mathf.Max(1f, bm.y - bm.w + 6f)) * (light == 0 ? 0.26f : light == 2 ? 0.3f : 0.22f);
                        Color32 tint = light == 0 ? C(170, 196, 255) : light == 2 ? C(255, 240, 200) : C(255, 200, 160);
                        or += (tint.r - or) * f; og += (tint.g - og) * f; ob += (tint.b - ob) * f;
                    }
                    p.Set(x, y, new Color32((byte)Mathf.Clamp(or, 0, 255), (byte)Mathf.Clamp(og, 0, 255), (byte)Mathf.Clamp(ob, 0, 255), c.a));
                }
            // What the windows show at this hour (the night's glass is the dark blue painted above). Laid in after the
            // light of the room: the sky is not lit by the lamps. Sky above, the snow of the roofs across the street
            // below it, then the glazing bars and the snow on the sill again.
            if (light != 0)
            {
                Color32 skyTop = light == 2 ? C(120, 176, 232) : light == 1 ? C(150, 150, 204) : C(214, 120, 96);
                Color32 skyLow = light == 2 ? C(196, 224, 246) : light == 1 ? C(244, 190, 176) : C(250, 190, 110);
                Color32 snow = light == 2 ? C(240, 246, 252) : light == 1 ? C(226, 206, 214) : C(232, 196, 170);
                Color32 snowD = Mul(snow, 0.86f), bar = Lerp(VBeam, skyTop, 0.25f);
                foreach (var w in panes)
                {
                    bool small = w.height < 10; int roofs = small ? 2 : Mathf.Max(3, w.height / 4);
                    for (int yy = 0; yy < w.height; yy++)
                        for (int xx = 0; xx < w.width; xx++)
                        {
                            Color32 c;
                            int rise = roofs + ((xx * 5 / Mathf.Max(1, w.width)) % 2 == 0 ? 0 : small ? 1 : 2) - (!small && xx % 7 == 3 ? 1 : 0);      // two roof heights across the street
                            if (yy < rise) c = yy == rise - 1 ? snow : snowD;
                            else c = Lerp(skyLow, skyTop, Mathf.Clamp01((yy - rise) / (float)Mathf.Max(1, w.height - rise - 1)));
                            p.Set(w.x + xx, w.y + yy, c);
                        }
                    if (small) continue;
                    p.Rect(w.x + w.width / 2, w.y, 1, w.height, bar); p.Rect(w.x, w.y + w.height / 2, w.width, 1, bar);
                    p.Set(w.x + 3, w.y + w.height - 4, C(255, 255, 255));                                    // a glint on the glass
                    p.Set(w.x + 4, w.y + w.height - 5, C(255, 255, 255));
                }
            }
            return p;
        }

        /// <summary>
        /// The things in the houses that answer when touched (RoomToy), painted the way the room around them is lit:
        /// the cat (asleep, stretching, sitting up, walking; it faces left), Tam's rope wheel, his hammock rolled up
        /// and hung (hung: its top row is the line of the hooks).
        /// </summary>
        static void RoomToyArt(string d)
        {
            // a colour as the room's lamps light it (lit 0..1), the way Room lights its picture by night
            Color32 W(Color32 c, float lit) => new Color32(
                (byte)Mathf.Clamp(Mathf.Lerp(c.r * 0.34f, c.r * 1.1f + 6f, lit), 0, 255),
                (byte)Mathf.Clamp(Mathf.Lerp(c.g * 0.4f, c.g + 2f, lit), 0, 255),
                (byte)Mathf.Clamp(Mathf.Lerp(c.b * 0.58f + 6f, c.b * 0.84f, lit), 0, 255), 255);
            // the cat is drawn with a dark line round it (it has to be seen against the dark of the room), 2 pixels of room all round
            void SaveCat(Px cat, string name)
            {
                var q = Padded(cat, 2);
                q.Outline(C(24, 16, 18));
                SaveWithRing(q, d + "toy_cat_" + name, new Vector2(0.5f, 2f / q.h));
            }
            {
                // the cat: a ginger tabby, by the fire - drawn pixel by pixel (top row first; the dark line round it
                // is added after): f fur, d its stripes, l the pale muzzle, chest and paws, p nose and ears, e eyes, k a dark line
                var pal = new Dictionary<char, Color32> { { 'f', W(C(226, 140, 62), 0.95f) }, { 'd', W(C(176, 94, 40), 0.95f) }, { 'l', W(C(250, 214, 160), 0.95f) },
                    { 'p', W(C(232, 140, 150), 0.95f) }, { 'e', C(120, 200, 90) }, { 'k', C(34, 22, 20) } };
                Px Map(string[] rows)
                {
                    int mw = 0; foreach (var r in rows) mw = Mathf.Max(mw, r.Length);
                    var m = new Px(mw, rows.Length);
                    for (int y = 0; y < rows.Length; y++)
                        for (int x = 0; x < rows[y].Length; x++)
                            if (pal.TryGetValue(rows[y][x], out var col)) m.Set(x, rows.Length - 1 - y, col);
                    return m;
                }
                SaveCat(Map(new[]
                {
                    "..f...f.............",
                    ".fpf.fpf............",
                    ".fffffff..dffdff....",
                    "ffffffffffffffffdf..",
                    "fkkffkkfffdffffffff.",
                    "fffpfffffffdffffffff",
                    ".lllllfffffffdffffff",
                    "..llldfdfffffffffff.",
                    "....ffdfdfdfdfdfdl..",
                }), "sleep");
                SaveCat(Map(new[]
                {
                    "...................f",
                    "..................fd",
                    "..................f.",
                    "...............fffd.",
                    ".f...f.......ffdfff.",
                    ".fp.fp.....ffdffdff.",
                    ".ffffff..ffdffdffff.",
                    "fkkffkkfffffffffff..",
                    "fffpfffdffdffffff...",
                    ".lllllllfffff.ff....",
                    "llllll.......lff....",
                    "lll..........ll.ll..",
                }), "stretch");
                SaveCat(Map(new[]
                {
                    ".f....f.......",
                    ".pf..fp.......",
                    ".ffffff.......",
                    "ffdffdff......",
                    "fefffeff......",
                    "fffpffff......",
                    ".lkkkll.......",
                    "..llllff......",
                    "..llfffff.....",
                    "..llffdfff....",
                    "..llfffdff....",
                    "..llffffdff...",
                    "..lkfffffff...",
                    "..lkffffffffd.",
                    ".llkffffffdfff",
                    ".ll.llffffffl.",
                }), "sit");
                SaveCat(Map(new[]
                {
                    ".f....f.............",
                    ".pf..fp...........f.",
                    ".ffffff...........fd",
                    "ffdffdff..........f.",
                    "fefffefffffffffffdf.",
                    "fffpfffffdffffdffff.",
                    ".lkkllffffdffffdfff.",
                    "..llllffffffffffff..",
                    "...lff.lff..ff.ff...",
                    "..ff....ff..ff..ff..",
                    ".ff......ff.ff...ff.",
                    ".ll......ll.ll...ll.",
                }), "walk_0");
                SaveCat(Map(new[]
                {
                    ".f....f.............",
                    ".pf..fp...........f.",
                    ".ffffff...........fd",
                    "ffdffdff..........f.",
                    "fefffefffffffffffdf.",
                    "fffpfffffdffffdffff.",
                    ".lkkllffffdffffdfff.",
                    "..llllffffffffffff..",
                    "....lffff....ffff...",
                    "....ff.ff....ff.ff..",
                    "....ff.ff....ff.ff..",
                    "....ll.ll....ll.ll..",
                }), "walk_1");
            }
            {
                // Tam's great wheel: rim, spokes (one lighter, so the turning shows), the hub
                Color32 wd = W(C(84, 54, 34), 0.9f), wo = W(C(120, 82, 52), 0.9f), wl = W(C(160, 116, 74), 0.9f), metal = W(C(190, 196, 210), 0.9f), rope = W(C(206, 176, 112), 0.9f);
                var p = new Px(31, 31);
                for (int a = 0; a < 360; a++)
                {
                    float an = a * Mathf.Deg2Rad;
                    p.Set(15 + Mathf.RoundToInt(Mathf.Cos(an) * 14.5f), 15 + Mathf.RoundToInt(Mathf.Sin(an) * 14.5f), wd);
                    p.Set(15 + Mathf.RoundToInt(Mathf.Cos(an) * 13.5f), 15 + Mathf.RoundToInt(Mathf.Sin(an) * 13.5f), a % 40 < 20 ? wo : rope);
                }
                for (int k = 0; k < 8; k++) { float an = k * 45f * Mathf.Deg2Rad; p.Line(15, 15, 15 + Mathf.RoundToInt(Mathf.Cos(an) * 12.5f), 15 + Mathf.RoundToInt(Mathf.Sin(an) * 12.5f), k == 0 ? wl : wd); }
                p.Disc(15.5f, 15.5f, 3f, wl); p.Set(15, 15, metal);
                SaveWithRing(Padded(p, 1), d + "toy_wheel", PCenter);
            }
            {
                // the hammock: rolled up between its hooks; hung, sagging, its ropes to the hooks
                Color32 cv = W(C(176, 156, 116), 0.62f), cvD = W(C(140, 120, 84), 0.62f), cvL = W(C(206, 190, 150), 0.62f), rope = W(C(150, 120, 70), 0.62f);
                var r = new Px(24, 7);
                r.Rect(1, 1, 22, 5, cv); r.Rect(1, 5, 22, 1, cvL); r.Rect(1, 1, 22, 1, cvD);
                r.Rect(6, 0, 1, 7, rope); r.Rect(17, 0, 1, 7, rope);
                SaveWithRing(Padded(r, 1), d + "toy_hammock_rolled", PCenter);
                var h = new Px(27, 18);
                for (int x = 3; x <= 23; x++)
                {
                    float t = (x - 13f) / 10f;
                    int bot = 2 + Mathf.RoundToInt(6f * t * t);
                    for (int y = bot; y <= Mathf.Min(16, bot + 5); y++)
                        h.Set(x, y, y == bot ? cvD : y >= bot + 5 ? cvL : (x % 4 == 0 ? cvD : cv));
                }
                h.Line(2, 17, 3, 15, rope); h.Line(24, 17, 23, 15, rope);
                Save(Padded(h, 1), d + "toy_hammock", new Vector2(14.5f / 29f, 18.5f / 20f));
            }
            // the outlines of things painted into the room (Maren's cauldron, the lamp at home): the room painted with
            // and without it, and the line round where they differ - a sheet the size of the room picture
            BakedRing(2, "cauldron", d + "toy_hl_cauldron");
            BakedRing(7, "lamp", d + "toy_hl_lamp");
            foreach (var (k, id) in new[] { (0, "bellows"), (0, "anvil"), (3, "telescope"), (4, "cage"), (5, "lamprow"), (5, "greatlamp"), (6, "cup"), (6, "candles"), (7, "bed") })
                BakedRing(k, id, d + "toy_hl_" + id);
            MoreToyArt(d);
        }

        /// <summary>
        /// The pictures of the other things in the houses (RoomToy.More): Oswin's globe turning (8 steps; the first
        /// shows the burned hole), Pip's owl (front, head round, blinking), clock and hands, the bird of the cage,
        /// an unlit lamp, a candle flame, the hall's banner stirring, Tam's coil of rope, and what the telescope shows.
        /// </summary>
        static void MoreToyArt(string d)
        {
            Color32 W(Color32 c, float lit) => new Color32(
                (byte)Mathf.Clamp(Mathf.Lerp(c.r * 0.34f, c.r * 1.1f + 6f, lit), 0, 255),
                (byte)Mathf.Clamp(Mathf.Lerp(c.g * 0.4f, c.g + 2f, lit), 0, 255),
                (byte)Mathf.Clamp(Mathf.Lerp(c.b * 0.58f + 6f, c.b * 0.84f, lit), 0, 255), 255);
            Px Map(string[] rows, System.Collections.Generic.Dictionary<char, Color32> pal)
            {
                int mw = 0; foreach (var r in rows) mw = Mathf.Max(mw, r.Length);
                var m = new Px(mw, rows.Length);
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        if (pal.TryGetValue(rows[y][x], out var col)) m.Set(x, rows.Length - 1 - y, col);
                return m;
            }
            Color32 ink = C(34, 22, 20);
            // the globe: seas and lands turning; the hole burned through where Brindlemoor should be
            for (int f = 0; f < 8; f++)
            {
                var p = new Px(13, 13);
                float phase = f * 45f * Mathf.Deg2Rad;
                for (int y = 0; y < 13; y++)
                    for (int x = 0; x < 13; x++)
                    {
                        float dx = x - 6f, dy = y - 6f;
                        if (dx * dx + dy * dy > 40f) continue;
                        if (dx * dx + dy * dy > 30f) { p.Set(x, y, W(C(60, 80, 110), 0.9f)); continue; }
                        float u = Mathf.Clamp(dx / 6.2f, -1f, 1f), lat = dy / 6.2f;
                        float lon = Mathf.Asin(u) + phase;
                        float wl = Mathf.Repeat(lon + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                        bool land = Mathf.PerlinNoise(Mathf.Cos(lon) * 1.4f + 5f, Mathf.Sin(lon) * 1.4f + lat * 1.8f + 3f) > 0.52f;
                        Color32 c = land ? C(110, 170, 100) : C(80, 130, 190);
                        float shade = 1f - Mathf.Abs(u) * 0.35f - (dy < -3f ? 0.1f : 0f);
                        c = Mul(c, shade);
                        float hx = wl * 3.2f, hy = (lat - 0.25f) * 3.2f;
                        if (Mathf.Abs(wl) < 1.4f && hx * hx + hy * hy < 1.1f) c = C(26, 18, 16);
                        else if (Mathf.Abs(wl) < 1.4f && hx * hx + hy * hy < 2.4f) c = C(140, 80, 44);
                        p.Set(x, y, W(c, 0.9f));
                    }
                p.Set(4, 9, W(C(236, 240, 250), 0.9f));                   // a glint
                SaveWithRing(Padded(p, 1), d + "toy_globe_" + f, PCenter);
            }
            {
                // Pip's stuffed owl: front, the head turned right round, a blink
                var pal = new System.Collections.Generic.Dictionary<char, Color32> { { 'b', W(C(150, 110, 74), 0.9f) }, { 'd', W(C(100, 70, 46), 0.9f) }, { 'l', W(C(222, 200, 160), 0.9f) },
                    { 'y', C(250, 210, 80) }, { 'k', ink }, { 'o', W(C(230, 150, 60), 0.9f) } };
                string[] body = { ".bblllbb.", ".bldldlb.", ".blllllb.", ".bldldlb.", "..blllb..", "..o...o.." };
                string[] Owl(params string[] head) { var r = new System.Collections.Generic.List<string>(head); r.AddRange(body); return r.ToArray(); }
                var heads = new[]
                {
                    Owl(".d.....d.", ".dbbbbbd.", "bbbbbbbbb", "byybbbyyb", "bykbbbkyb", "bbbbobbbb"),
                    Owl(".d.....d.", ".dbbbbbd.", "bbbbbbbbb", "bbdbbbdbb", "bbbdbdbbb", "bbbbdbbbb"),
                    Owl(".d.....d.", ".dbbbbbd.", "bbbbbbbbb", "bkkbbbkkb", "bbbbbbbbb", "bbbbobbbb"),
                };
                for (int i = 0; i < 3; i++) { var q = Padded(Map(heads[i], pal), 2); q.Outline(ink); SaveWithRing(q, d + "toy_owl_" + i, new Vector2(0.5f, 2f / q.h)); }
            }
            {
                // the clock: a round face in a wooden rim, the hours marked; the hands are pictures of their own
                var p = new Px(13, 13);
                p.Disc(6.5f, 6.5f, 6.4f, W(C(96, 62, 40), 0.85f)); p.Disc(6.5f, 6.5f, 5f, W(C(236, 224, 190), 0.85f));
                p.Set(6, 11, ink); p.Set(6, 1, ink); p.Set(1, 6, ink); p.Set(11, 6, ink);
                p.Set(6, 6, ink);
                SaveWithRing(Padded(p, 1), d + "toy_clock", PCenter);
                var mh = new Px(1, 6); mh.Rect(0, 0, 1, 6, ink); Save(mh, d + "toy_hand_min", new Vector2(0.5f, 0.5f / 6f));
                var hh = new Px(1, 4); hh.Rect(0, 0, 1, 4, W(C(120, 40, 40), 0.85f)); Save(hh, d + "toy_hand_hour", new Vector2(0.5f, 0.5f / 4f));
            }
            {
                // the bird of the cage, wings up and down (it faces right)
                var pal = new System.Collections.Generic.Dictionary<char, Color32> { { 'b', W(C(90, 140, 220), 0.9f) }, { 'y', W(C(240, 210, 90), 0.9f) }, { 'o', W(C(240, 150, 60), 0.9f) }, { 'k', ink } };
                Save(Map(new[] { "b.b...", ".bbbk.", ".byybo", "..bb.." }, pal), d + "toy_bird_0", PCenter);
                Save(Map(new[] { "......", ".bbbk.", "bbyybo", "b.bb.." }, pal), d + "toy_bird_1", PCenter);
            }
            {
                // a lamp put out (its glass dark), a candle flame (two flickers), plain white
                var o = new Px(5, 5); o.Rect(0, 0, 5, 5, C(58, 52, 50)); o.Rect(1, 2, 3, 1, C(84, 78, 74)); Save(o, d + "toy_lamp_off", PCenter);
                var pal = new System.Collections.Generic.Dictionary<char, Color32> { { 'y', C(255, 226, 130) }, { 'o', C(255, 170, 60) }, { 'w', C(255, 250, 230) } };
                Save(Map(new[] { ".y", "yy", "yw", "oo" }, pal), d + "toy_flame_0", new Vector2(0.5f, 0f));
                Save(Map(new[] { "y.", "yy", "wy", "oo" }, pal), d + "toy_flame_1", new Vector2(0.5f, 0f));
                var wh = new Px(4, 4); wh.Rect(0, 0, 4, 4, C(255, 255, 255)); Save(wh, d + "toy_white", PCenter);
            }
            {
                // the banner of Brindlemoor on its rod: red, a gold crown, a swallowtail; stirring as if in a wind
                Color32 red = W(C(150, 44, 50), 0.8f), redD = W(C(110, 30, 36), 0.8f), gold = W(C(250, 200, 70), 0.8f), rod = W(C(90, 64, 44), 0.8f);
                for (int f = 0; f < 3; f++)
                {
                    var p = new Px(28, 50);
                    p.Rect(0, 48, 28, 2, rod);
                    for (int y = 0; y < 48; y++)
                    {
                        float down = (47 - y) / 47f;
                        int sh = Mathf.RoundToInt(Mathf.Sin(y * 0.22f + f * 2.1f) * 2f * down);
                        for (int x = 0; x < 24; x++)
                        {
                            if (y < 6 && Mathf.Abs(x - 11.5f) < 6 - y) continue;          // the swallowtail
                            int gx = 2 + x + sh, ey = y;      // (the emblem where it was painted: 22..30 up the cloth)
                            Color32 c = (x == 0 || x == 23) ? redD : red;
                            if (ey >= 22 && ey < 27 && x >= 6 && x < 18) c = gold;
                            if (ey >= 27 && ey < 31 && (x == 6 || x == 7 || x == 11 || x == 12 || x == 16 || x == 17)) c = gold;
                            if (x % 6 == 3 && (y + f) % 9 < 2) c = redD;               // folds
                            p.Set(gx, y, c);
                        }
                    }
                    SaveWithRing(Padded(p, 1), d + "toy_banner_" + f, new Vector2(0.5f, 51f / 52f));
                }
            }
            {
                // Tam's great coil of rope, lying on the floor
                var p = new Px(19, 10);
                Color32 rope = W(C(206, 176, 112), 0.85f), ropeD = W(C(170, 140, 90), 0.85f), dark = W(C(110, 84, 50), 0.85f);
                p.Ellipse(9.5f, 4.5f, 9f, 4.5f, rope); p.Ellipse(9.5f, 4.5f, 6f, 3f, ropeD); p.Ellipse(9.5f, 4.5f, 3f, 1.5f, dark);
                for (int x = 1; x < 18; x += 3) p.Set(x, 4, ropeD);
                SaveWithRing(Padded(p, 1), d + "toy_coil", new Vector2(0.5f, 1f / 12f));
            }
            {
                // through the telescope: a round view of the night, the mountain's peak in the middle of it; the light on
                // the peak and a climber with a lantern are pictures of their own
                var p = new Px(128, 128);
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float dx = x - 63.5f, dy = y - 63.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                        if (r > 60f) { p.Set(x, y, C(0, 0, 0)); continue; }
                        Color32 sky = Lerp(C(40, 50, 96), C(8, 10, 30), y / 127f);
                        float ridge = 96f - Mathf.Abs(x - 64f) * 1.15f + Mathf.Sin(x * 0.5f) * 2f + Mathf.PerlinNoise(x * 0.08f, 3f) * 6f;
                        Color32 c = sky;
                        if (y < ridge) c = y > ridge - 4f - Mathf.Max(0f, 30f - Mathf.Abs(x - 64f)) * 0.4f ? C(200, 210, 236) : Lerp(C(30, 34, 52), C(52, 58, 86), y / 100f);
                        else if (((x * 37 + y * 91) % 97) == 0) c = C(230, 236, 255);                          // stars
                        if (r > 57f) c = Mul(c, 0.5f);                                                       // the rim of the lens
                        p.Set(x, y, c);
                    }
                Save(p, d + "toy_scope_view", PCenter);
                var l = new Px(5, 5); l.Disc(2.5f, 2.5f, 2.4f, C(255, 190, 90, 120)); l.Rect(1, 2, 3, 1, C(255, 230, 150)); l.Rect(2, 1, 1, 3, C(255, 230, 150)); l.Set(2, 2, C(255, 255, 230));
                Save(l, d + "toy_scope_light", PCenter);
                var cl = new Px(3, 5); cl.Rect(1, 0, 1, 4, C(16, 16, 24)); cl.Set(0, 1, C(16, 16, 24)); cl.Set(2, 0, C(16, 16, 24)); cl.Set(1, 4, C(16, 16, 24)); cl.Set(2, 3, C(255, 210, 120));
                Save(cl, d + "toy_scope_climber", PCenter);
            }
        }

        static void BakedRing(int kind, string skip, string path)
        {
            var with = House(Room(kind));
            roomSkip = skip;
            Px without;
            try { without = House(Room(kind)); }
            finally { roomSkip = null; }
            var mask = new Px(with.w, with.h);
            for (int i = 0; i < with.c.Length; i++)
                if (!with.c[i].Equals(without.c[i])) mask.c[i] = C(255, 255, 255);
            Save(RingOf(mask), path, PCenter);
        }

        /// <summary>
        /// The Blue Ocarina that falls out of the shaken fir, and the pictures of the achievement it earns (with the
        /// lock the list shows over one not earned, and the tile of a secret one).
        /// </summary>
        static void TreeAndOcarinaArt(string d)
        {
            // a chunky pixel ocarina: the mouthpiece standing up at the left, a plump body lying along with its tail
            // tapering to the right, holes in two staggered rows, pale glaze across the top (no band on the mouthpiece)
            Px Ocarina()
            {
                string[] rows =
                {
                    "...mbd............",
                    "...mbd............",
                    "...mbd............",
                    "..lmbbdd..........",
                    ".lwwllbbbbdd......",
                    "bbbbbllllbbbbdd...",
                    "bbbkbbbbkbbbbbkbd.",
                    "bbbbbbkbbbbkbbbbbd",
                    "dbbbbbbbbbbbbbbbdd",
                    ".ddbbbbbbbbbbbddd.",
                    "...ddddbbbbdddd...",
                };
                var pal = new Dictionary<char, Color32> { { 'b', C(24, 112, 206) }, { 'l', C(92, 190, 240) }, { 'w', C(236, 242, 250) }, { 'd', C(34, 52, 128) }, { 'k', C(12, 14, 30) }, { 'm', C(70, 160, 230) } };
                var o = new Px(rows[0].Length, rows.Length);
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        if (pal.TryGetValue(rows[y][x], out var col)) o.Set(x, rows.Length - 1 - y, col);
                return o;
            }
            {
                var q = Padded(Ocarina(), 2);
                q.Outline(C(12, 14, 30));
                SaveWithRing(q, d + "ocarina", new Vector2(0.5f, 2f / q.h));
            }
            {
                // the achievement's picture, drawn at its own small size (24 px: shown at three times, 72, so every pixel
                // is the same size on screen): the ocarina on a dark tile, a gold edge, two sparkles
                var p = new Px(24, 24);
                p.Rect(0, 0, 24, 24, C(196, 156, 70)); p.Rect(1, 1, 22, 22, C(22, 26, 48)); p.Rect(1, 22, 22, 1, C(40, 46, 80));
                var o = Padded(Ocarina(), 1); o.Outline(C(12, 14, 30));
                int ox = (24 - o.w) / 2, oy = (24 - o.h) / 2 - 1;
                for (int y = 0; y < o.h; y++)
                    for (int x = 0; x < o.w; x++) { var c = o.Get(x, y); if (c.a != 0) p.Set(ox + x, oy + y, c); }
                p.Set(20, 20, C(255, 236, 170)); p.Set(3, 20, C(255, 236, 170));
                Save(p, "UI/ach_" + Achievements.OcarinaOfGoat, PCenter);
                // the lock over one not earned (8 x 10: shown at three times)
                var l = new Px(8, 10);
                l.Rect(1, 5, 1, 4, C(150, 156, 170)); l.Rect(6, 5, 1, 4, C(150, 156, 170)); l.Rect(2, 8, 4, 2, C(150, 156, 170)); l.Rect(2, 8, 4, 1, C(150, 156, 170));
                l.Rect(0, 0, 8, 6, C(196, 156, 70)); l.Rect(0, 0, 8, 1, C(140, 110, 50)); l.Rect(3, 2, 2, 2, C(60, 44, 24));
                Save(l, "UI/ach_lock", PCenter);
                // a secret one not yet earned: a dim tile with a question mark (24 px, like the others)
                var q = new Px(24, 24);
                q.Rect(0, 0, 24, 24, C(110, 112, 130)); q.Rect(1, 1, 22, 22, C(46, 50, 76)); q.Rect(1, 22, 22, 1, C(64, 70, 104));
                string[] mark = { ".####.", "##..##", "....##", "...##.", "..##..", "..##..", "......", "..##.." };
                for (int y = 0; y < mark.Length; y++)
                    for (int x = 0; x < mark[y].Length; x++)
                        if (mark[y][x] == '#') q.Rect(6 + x * 2, 19 - y * 2, 2, 2, C(196, 200, 220));
                Save(q, "UI/ach_secret", PCenter);
                // the frame of a tile on the Achievements screen (26 px, shown at four times over a 24 px picture: its
                // ring covers the picture's own edge): gold round one earned, grey round one not yet earned
                void Frame(string rel, Color32 light, Color32 mid, Color32 dark)
                {
                    var f = new Px(26, 26);
                    f.Rect(0, 0, 26, 26, C(12, 14, 30));
                    f.Rect(1, 1, 24, 24, mid);
                    f.Rect(1, 24, 24, 1, light); f.Rect(1, 1, 1, 24, light);          // light from the top left
                    f.Rect(1, 1, 24, 1, dark); f.Rect(24, 1, 1, 24, dark);
                    f.Rect(1, 24, 1, 1, light); f.Rect(24, 1, 1, 1, dark);
                    for (int y = 2; y < 24; y++) for (int x = 2; x < 24; x++) f.Set(x, y, new Color32(0, 0, 0, 0));
                    // a stud in each corner
                    foreach (var (cx, cy) in new[] { (0, 0), (24, 0), (0, 24), (24, 24) }) { f.Rect(cx, cy, 2, 2, light); f.Set(cx + 1, cy, dark); }
                    Save(f, rel, PCenter);
                }
                Frame("UI/ach_frame_gold", C(255, 230, 150), C(214, 170, 76), C(140, 98, 40));
                // First Light and the bosses' achievements: their pictures are files of their own (UI/ach_<id>.png,
                // a symbol for each, drawn by hand); a plain tile stands in only for one that is missing
                {
                    var ids = new List<string> { Achievements.FirstVictory };
                    foreach (var bl in Achievements.BossList) ids.Add(bl.id);
                    foreach (var id in ids)
                    {
                        var v = new Px(24, 24);
                        v.Rect(0, 0, 24, 24, C(170, 60, 50)); v.Rect(1, 1, 22, 22, C(30, 22, 40));
                        Save(v, "UI/ach_" + id, PCenter);
                    }
                }
                Frame("UI/ach_frame", C(176, 182, 198), C(118, 124, 142), C(70, 74, 92));
            }
        }

        /// <summary>The same drawing with n empty pixels all round it.</summary>
        static Px Padded(Px p, int n)
        {
            var q = new Px(p.w + n * 2, p.h + n * 2);
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++) q.c[(y + n) * q.w + x + n] = p.c[y * p.w + x];
            return q;
        }

        /// <summary>Only the line just outside a drawing, in white: shown round a thing while the player can use it.</summary>
        static Px RingOf(Px p)
        {
            var q = new Px(p.w, p.h);
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    if (p.c[y * p.w + x].a > 0) continue;
                    bool n = (x > 0 && p.c[y * p.w + x - 1].a > 0) || (x < p.w - 1 && p.c[y * p.w + x + 1].a > 0) ||
                             (y > 0 && p.c[(y - 1) * p.w + x].a > 0) || (y < p.h - 1 && p.c[(y + 1) * p.w + x].a > 0);
                    if (n) q.c[y * p.w + x] = C(255, 255, 255);
                }
            return q;
        }

        /// <summary>A drawing, and its outline as name_hl (same size and pivot) - the outline of the picture as it is
        /// now, so a hand-edited file gets an outline that fits it.</summary>
        static void SaveWithRing(Px p, string path, Vector2 pivot)
        {
            Save(p, path, pivot);
            Save(RingOf(Current(p, path)), path + "_hl", pivot);
        }

        /// <summary>
        /// The things to read in the houses (Curio), each with its outline: a note lying flat, a note pinned to the
        /// wall, an open book, a sealed letter, a chest shut and open (a scrap of paper inside). Painted the way the
        /// rooms are lit; the foot of each is its pivot (1 pixel of room all round).
        /// </summary>
        static void ReadableArt(string d)
        {
            Color32 W(Color32 c) => new Color32((byte)Mathf.Clamp(c.r * 1.02f + 4f, 0, 255), (byte)Mathf.Clamp(c.g * 0.96f + 2f, 0, 255), (byte)Mathf.Clamp(c.b * 0.84f, 0, 255), 255);
            Color32 paper = W(C(240, 230, 204)), paperD = W(C(196, 180, 146)), paperL = W(C(252, 246, 228)), ink = W(C(84, 64, 56)), red = W(C(190, 54, 54)), redD = W(C(130, 34, 36));
            Color32 wood = W(C(132, 90, 56)), woodD = W(C(88, 58, 36)), woodL = W(C(172, 124, 80)), gold = W(C(222, 178, 84)), dark = W(C(40, 28, 24));
            void Out(Px p, string name) { var q = Padded(p, 1); SaveWithRing(q, d + "read_" + name, new Vector2(0.5f, 1f / q.h)); }
            {
                // a note lying flat (seen a little from above), two lines of writing, a corner turned
                var p = new Px(12, 4);
                p.Rect(0, 0, 12, 4, paper); p.Rect(0, 0, 12, 1, paperD);
                for (int x = 1; x < 9; x++) if (x != 5) p.Set(x, 2, ink);
                for (int x = 1; x < 6; x++) p.Set(x, 1, ink);
                p.Set(11, 3, paperD);
                Out(p, "note");
            }
            {
                // a note pinned to the wall: lines of writing, a red pin, the foot curling
                var p = new Px(10, 12);
                p.Rect(0, 0, 10, 12, paper); p.Rect(0, 0, 1, 12, paperD); p.Rect(0, 0, 10, 1, paperD);
                for (int y = 2; y < 10; y += 2) for (int x = 2; x < 8; x++) if ((x + y) % 5 != 0) p.Set(x, y, ink);
                p.Set(5, 11, red); p.Set(5, 10, redD); p.Set(9, 0, Px.Clear); p.Set(8, 0, paperD);
                Out(p, "pinned");
            }
            {
                // an open book: the cover, two pages with writing, the spine, a ribbon hanging out
                var p = new Px(14, 6);
                p.Rect(0, 0, 14, 2, redD); p.Rect(0, 1, 14, 1, red);
                p.Rect(1, 1, 6, 4, paper); p.Rect(7, 1, 6, 4, paper); p.Rect(1, 5, 5, 1, paperL); p.Rect(8, 5, 5, 1, paperL);
                p.Rect(6, 1, 2, 5, paperD);
                for (int x = 2; x < 6; x++) { p.Set(x, 3, ink); if (x < 5) p.Set(x, 2, ink); }
                for (int x = 9; x < 12; x++) { p.Set(x, 3, ink); p.Set(x, 2, ink); }
                p.Set(10, 0, gold);
                Out(p, "book");
            }
            {
                // a sealed letter
                var p = new Px(11, 7);
                p.Rect(0, 0, 11, 7, paperL); p.Rect(0, 0, 11, 1, paperD);
                p.Line(0, 6, 5, 3, paperD); p.Line(10, 6, 5, 3, paperD);
                p.Disc(5.5f, 3f, 1.6f, red); p.Set(5, 3, redD);
                Out(p, "letter");
            }
            {
                // a chest, shut: boards, a band, a brass lock
                var p = new Px(22, 16);
                p.Rect(0, 0, 22, 12, wood); for (int x = 5; x < 22; x += 6) p.Rect(x, 1, 1, 10, woodD);
                p.Rect(0, 12, 22, 4, woodL); p.Rect(0, 15, 22, 1, woodD); p.Rect(0, 11, 22, 1, woodD);
                p.Rect(0, 0, 1, 16, woodD); p.Rect(21, 0, 1, 16, woodD); p.Rect(0, 0, 22, 1, woodD);
                p.Rect(9, 8, 4, 5, gold); p.Set(10, 9, dark); p.Set(11, 9, dark);
                Out(p, "chest");
                // open: the lid thrown back, the dark inside, a scrap of paper in it
                var o = new Px(22, 22);
                o.Rect(0, 0, 22, 12, wood); for (int x = 5; x < 22; x += 6) o.Rect(x, 1, 1, 10, woodD);
                o.Rect(0, 0, 1, 12, woodD); o.Rect(21, 0, 1, 12, woodD); o.Rect(0, 0, 22, 1, woodD);
                o.Rect(1, 10, 20, 3, dark); o.Rect(0, 12, 22, 1, woodL);
                o.Rect(1, 13, 20, 8, woodD); o.Rect(1, 20, 20, 1, woodL); o.Rect(0, 13, 1, 9, wood); o.Rect(21, 13, 1, 9, wood);
                o.Rect(8, 11, 7, 3, paperL); o.Set(9, 12, ink); o.Set(10, 12, ink); o.Set(12, 12, ink);
                o.Rect(9, 7, 4, 3, gold);
                Out(o, "chest_open");
            }
        }

        /// <summary>
        /// What hangs close to the eye in a room, drawn in front of the people in it: dark, a little larger than the
        /// room, at the top edge of each storey - it gives the picture its depth. (Only the apothecary has one yet.)
        /// </summary>
        static Px RoomForeground(int kind)
        {
            var p = new Px(RoomPxW, RoomPxH);
            int ix = RX + 16, iy = RY + 16;
            Color32 sil = C(16, 12, 14, 235), silL = C(34, 26, 28, 235);
            void Bundle(int x, int ceil, int len, int spread)           // dried herbs, a bunch of garlic
            {
                p.Rect(ix + x - 1, iy + ceil - 4, 3, 5, sil);
                for (int k = 0; k < len; k++)
                {
                    float t = k / (float)Mathf.Max(1, len - 1);
                    int w = Mathf.RoundToInt(2 + Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.PI * 0.5f) * spread * (1f - t * 0.35f));
                    int jag = (k * 7 + x) % 3;
                    for (int dx = -w - jag; dx <= w + (2 - jag); dx++) p.Set(ix + x + dx, iy + ceil - 5 - k, sil);
                    p.Set(ix + x + w + (2 - jag), iy + ceil - 5 - k, silL);
                }
            }
            void Chain(int x, int ceil, int len)                         // a chain with a hook at its end
            {
                for (int k = 0; k < len; k++) { p.Set(ix + x + (k % 4 < 2 ? 0 : 1), iy + ceil - k, sil); if (k % 4 == 0) { p.Set(ix + x - 1, iy + ceil - k, sil); p.Set(ix + x + 2, iy + ceil - k, sil); } }
                int b = iy + ceil - len;
                p.Rect(ix + x - 1, b - 2, 4, 3, sil); p.Rect(ix + x + 2, b - 5, 2, 4, sil); p.Rect(ix + x - 2, b - 6, 5, 2, sil); p.Set(ix + x + 3, b - 1, silL);
            }
            void Coil(int x, int ceil, int len, int r)                    // a coil of rope hung from a peg
            {
                p.Rect(ix + x, iy + ceil - len, 2, len, sil);
                int cy = iy + ceil - len - r;
                for (int a = 0; a < 360; a += 4)
                    for (int ring = 0; ring < 3; ring++)
                    {
                        float rr = r - ring * 2.2f, an = a * Mathf.Deg2Rad;
                        p.Set(ix + x + Mathf.RoundToInt(Mathf.Cos(an) * rr), cy + Mathf.RoundToInt(Mathf.Sin(an) * rr * 0.9f), ring == 0 && a > 20 && a < 70 ? silL : sil);
                    }
            }
            void Lantern(int x, int ceil, int len)                       // a hanging lantern
            {
                p.Rect(ix + x, iy + ceil - len, 1, len, sil);
                int top = iy + ceil - len;
                p.Rect(ix + x - 4, top - 2, 9, 2, sil); p.Rect(ix + x - 3, top - 12, 7, 10, sil); p.Rect(ix + x - 4, top - 14, 9, 2, sil);
                p.Rect(ix + x - 1, top - 10, 3, 6, C(120, 70, 30, 220));
            }
            void Banner(int x, int ceil, int len, int w)                 // the end of a hanging banner, notched
            {
                p.Rect(ix + x - 1, iy + ceil - 3, w + 2, 3, sil);
                for (int k = 0; k < len; k++)
                    for (int dx = 0; dx < w; dx++)
                    {
                        bool notch = k > len - 6 && Mathf.Abs(dx - w / 2) < (k - (len - 6)) * 0.8f;
                        if (!notch) p.Set(ix + x + dx, iy + ceil - 3 - k, dx == 0 ? silL : sil);
                    }
            }
            foreach (int ceil in new[] { 96, 96 + UpperY })
            {
                p.Rect(ix - 16, iy + ceil - 1, 256, 3, sil);                       // the near edge of the beam
                bool up = ceil > 96;
                switch (kind)
                {
                    case 0: Chain(8, ceil, 18); Chain(20, ceil, 10); Chain(206, ceil, 14); Chain(216, ceil, 22); break;                         // the forge
                    case 1: Coil(10, ceil, 6, 9); Coil(212, ceil, 4, 8); if (!up) Coil(28, ceil, 3, 6); break;                                 // the ropewalk
                    case 2: Bundle(6, ceil, 22, 9); Bundle(22, ceil, 14, 6); Bundle(214, ceil, 20, 8); Bundle(200, ceil, 12, 5); break;         // the apothecary
                    case 3: Lantern(12, ceil, 8); Banner(204, ceil, 20, 14); break;                                                           // the map room: a lamp, a hanging chart
                    case 4: Lantern(8, ceil, 6); Lantern(22, ceil, 14); Bundle(210, ceil, 12, 5); Lantern(214, ceil, 20); break;               // Pip's: lamps and odd things
                    case 5: Lantern(10, ceil, 10); Lantern(212, ceil, 6); if (!up) Lantern(200, ceil, 16); break;                              // the lamp house
                    case 6: Banner(4, ceil, 26, 16); Banner(206, ceil, 26, 16); break;                                                         // the village hall
                    default: Bundle(8, ceil, 12, 5); Bundle(20, ceil, 9, 4); Bundle(212, ceil, 14, 6); break;                                 // home: garlic and herbs
                }
            }
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
