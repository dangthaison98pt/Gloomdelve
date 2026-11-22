using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Generates every sprite of the game procedurally as PNG files (original art, no external assets).
    /// Artists can overwrite the PNGs in Assets/Game/Art; re-running the generator overwrites them again.
    /// </summary>
    public static partial class ArtGenerator
    {
        public const string Root = "Assets/Game/Art/";
        static readonly List<string> written = new List<string>();

        static readonly Vector2 PCenter = new Vector2(0.5f, 0.5f);
        static Vector2 PBottom(Px p, int oy) => new Vector2(0.5f, oy / (float)p.h);

        /// <summary>
        /// Gives a sprite volume with a light from above: pixels under the top outline get a warm highlight, pixels
        /// above the bottom outline a cool shadow. Outline pixels (very dark) are left alone.
        /// </summary>
        static Px AutoShade(Px src)
        {
            var p = src.Copy();
            bool Edge(int x, int y)
            {
                var c = src.Get(x, y);
                return c.a < 128 || (c.r + c.g + c.b) < 110;
            }
            for (int y = 0; y < src.h; y++)
                for (int x = 0; x < src.w; x++)
                {
                    var c = src.Get(x, y);
                    if (c.a < 255 || (c.r + c.g + c.b) < 110) continue;
                    if (Edge(x, y + 1))
                        p.Set(x, y, new Color32((byte)Mathf.Min(255, c.r * 1.14f + 10), (byte)Mathf.Min(255, c.g * 1.12f + 8), (byte)Mathf.Min(255, c.b * 1.06f + 2), 255));
                    else if (Edge(x, y - 1))
                        p.Set(x, y, new Color32((byte)(c.r * 0.8f), (byte)(c.g * 0.8f), (byte)Mathf.Min(255, c.b * 0.88f + 4), 255));
                }
            return p;
        }

        /// <summary>
        /// A thing drawn on the very bottom row of its sheet has no room for the outline under it: it shows with a
        /// dark line round its sides and top and none below. Such a picture is moved up a row and the line closed
        /// under it, in its own outline colour. (Only pictures that have an outline, and a free row at the top.)
        /// </summary>
        static Px CloseUnder(Px src)
        {
            int w = src.w, h = src.h;
            for (int x = 0; x < w; x++) if (src.c[(h - 1) * w + x].a > 0) return src;       // no free row to move up into
            // Its outline colour: what most of its edge is drawn in (the pixels with nothing beside or over them),
            // the bottom row left out - that is the row in question.
            var count = new Dictionary<int, int>();
            bool Clear(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || src.c[y * w + x].a < 128;
            for (int y = 1; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = src.c[y * w + x];
                    if (c.a < 128 || !(Clear(x - 1, y) || Clear(x + 1, y) || Clear(x, y + 1))) continue;
                    int key = c.r << 16 | c.g << 8 | c.b;
                    count[key] = count.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            int most = 0, olKey = -1;
            foreach (var kv in count) if (kv.Value > most) { most = kv.Value; olKey = kv.Key; }
            if (olKey < 0) return src;
            var ol = new Color32((byte)(olKey >> 16), (byte)(olKey >> 8 & 255), (byte)(olKey & 255), 255);
            // the bottom row: is there drawing on it that is not outline, and clearly lighter than the outline?
            int body = 0, sum = 0;
            for (int x = 0; x < w; x++)
            {
                var c = src.c[x];
                if (c.a < 128 || (c.r == ol.r && c.g == ol.g && c.b == ol.b)) continue;
                body++; sum += c.r + c.g + c.b;
            }
            if (body == 0 || (ol.r + ol.g + ol.b) > 0.75f * sum / body) return src;       // closed already, or a thing with no outline to speak of
            var p = new Px(w, h);
            for (int y = h - 1; y >= 1; y--)
                for (int x = 0; x < w; x++) p.c[y * w + x] = src.c[(y - 1) * w + x];
            for (int x = 0; x < w; x++)
            {
                var up = p.c[w + x];
                if (up.a >= 128 && !(up.r == ol.r && up.g == ol.g && up.b == ol.b)) p.c[x] = ol;
            }
            return p;
        }

        static void Save(Px p, string rel, Vector2 pivot, Vector4 border = default)
        {
            if (rel.StartsWith("Sprites/Items/")) p = CloseUnder(p);
            if (rel.StartsWith("Sprites/Player/") || rel.StartsWith("Sprites/Enemies/") || rel.StartsWith("Sprites/Items/")) p = AutoShade(p);
            string path = Root + rel + ".png";
            p.Save(Path.Combine(Directory.GetCurrentDirectory(), path));
            ArtImport.Pending[path] = new ArtImport.Settings { pivot = pivot, border = border };
            written.Add(path);
            // the part of the sheet that is drawn on (see IconFrames)
            int x0 = p.w, y0 = p.h, x1 = -1, y1 = -1;
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                    if (p.c[y * p.w + x].a > 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 >= x0) Drawn[rel] = new Rect(x0 / (float)p.w, y0 / (float)p.h, (x1 - x0 + 1) / (float)p.w, (y1 - y0 + 1) / (float)p.h);
        }

        /// <summary>Picture (as given to S) -> the part of its sheet that is drawn on, 0..1. Filled as the pictures are made.</summary>
        public static readonly Dictionary<string, Rect> Drawn = new Dictionary<string, Rect>();

        static void SaveSmooth(Px p, string rel)
        {
            string path = Root + rel + ".png";
            p.Save(Path.Combine(Directory.GetCurrentDirectory(), path));
            ArtImport.Pending[path] = new ArtImport.Settings { pivot = PCenter, smooth = true };
            written.Add(path);
        }

        /// <summary>Anti-aliased white shape (4x4 supersampled); inside(u, v) with u, v in [-1, 1], v up.</summary>
        static Px SmoothShape(int size, System.Func<float, float, bool> inside)
        {
            var p = new Px(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / 4f) / size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / 4f) / size) * 2f - 1f;
                            if (inside(u, v)) hit++;
                        }
                    if (hit > 0) p.Set(x, y, C(255, 255, 255, hit * 255 / 16));
                }
            return p;
        }

        static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // Clean vector-style waypoint pieces (tinted at runtime).
        static void WaypointArt()
        {
            const string d = "UI/wp_";
            SaveSmooth(SmoothShape(128, (u, v) => u * u + v * v <= 0.86f * 0.86f), d + "fill");
            SaveSmooth(SmoothShape(128, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return r >= 0.80f && r <= 0.95f; }), d + "ring");
            SaveSmooth(SmoothShape(128, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return r >= 0.90f && r <= 0.97f; }), d + "ping");
            // chevron pointing right
            SaveSmooth(SmoothShape(64, (u, v) => InTri(u, v, -0.6f, 0.8f, 0.7f, 0f, -0.15f, 0f) || InTri(u, v, -0.15f, 0f, 0.7f, 0f, -0.6f, -0.8f)), d + "chevron");
            // pin tail pointing down
            SaveSmooth(SmoothShape(64, (u, v) => InTri(u, v, -0.7f, 0.6f, 0.7f, 0.6f, 0f, -0.7f)), d + "tail");
            // exit icon: arched doorway with threshold
            SaveSmooth(SmoothShape(96, (u, v) =>
            {
                bool body = (Mathf.Abs(u) <= 0.42f && v >= -0.62f && v <= 0.18f) || (u * u + (v - 0.18f) * (v - 0.18f) <= 0.42f * 0.42f);
                bool inner = (Mathf.Abs(u) <= 0.26f && v >= -0.62f && v <= 0.18f) || (u * u + (v - 0.18f) * (v - 0.18f) <= 0.26f * 0.26f);
                bool floor = Mathf.Abs(u) <= 0.72f && v >= -0.8f && v <= -0.64f;
                bool knob = (u - 0.12f) * (u - 0.12f) + (v + 0.24f) * (v + 0.24f) <= 0.06f * 0.06f;
                return (body && !inner) || floor || knob;
            }), d + "exit");
            // goal icon: faceted gem
            SaveSmooth(SmoothShape(96, (u, v) =>
            {
                bool gem = Mathf.Abs(u) / 0.6f + Mathf.Abs(v + 0.05f) / 0.78f <= 1f;
                bool cut = Mathf.Abs(v - 0.2f) <= 0.035f || (Mathf.Abs(u) <= 0.035f && v < 0.2f);
                return gem && !cut;
            }), d + "goal");
        }

        public static Sprite S(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Root + rel + ".png");
            if (s == null) Debug.LogError("[Art] Missing sprite " + rel);
            return s;
        }

        public static Sprite[] Frames(string relPrefix, int count)
        {
            var a = new Sprite[count];
            for (int i = 0; i < count; i++) a[i] = S(relPrefix + "_" + i);
            return a;
        }

        public static void GenerateAll()
        {
            written.Clear();
            ArtImport.Pending.Clear();
            AllTiles();
            PlayerArt();
            EnemyArt();
            ItemArt();
            TrapAndPropArt();
            MimicArt();
            VillagerArt();
            StoryArt();
            BossArt();
            CounterFxArt();
            FxArt();
            UiArt();
            FontArt();
            AppIconArt();
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            try { foreach (var p in written) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            ArtImport.Pending.Clear();
            Debug.Log($"[Art] Generated {written.Count} sprites");
        }

        // ======================================================================================= TILES

        class Pal
        {
            public Color32 b, d, l, deep, top, topL, topD;
            public Color32 rock, rockD, rockL, rockDeep;
            public Color32 bg, bgD, bgL;
            public Color32 wood, woodD, woodL;
            public Color32 spike, spikeD, spikeL, spikeTip;
            public int style;   // 0 moss/grass, 1 ember crust, 2 vault bricks
            public bool vine;   // ladders drawn as vines (outdoor)
            public bool ashlar; // walls of big dressed blocks with sand drifted on top instead of small bricks (the tombs)
        }

        static readonly Pal Moss = new Pal
        {
            b = C(122, 82, 52), d = C(94, 62, 40), l = C(152, 106, 68), deep = C(56, 36, 25),
            top = C(84, 148, 60), topL = C(142, 198, 86), topD = C(48, 98, 44),
            rock = C(64, 64, 80), rockD = C(42, 42, 56), rockL = C(98, 100, 122), rockDeep = C(24, 24, 34),
            bg = C(40, 30, 25), bgD = C(31, 23, 19), bgL = C(52, 40, 32),
            wood = C(150, 100, 56), woodD = C(92, 58, 32), woodL = C(192, 142, 82),
            spike = C(176, 176, 186), spikeD = C(96, 96, 108), spikeL = C(230, 230, 236), spikeTip = C(160, 40, 40),
            style = 0
        };

        static readonly Pal Ember = new Pal
        {
            b = C(112, 60, 54), d = C(84, 42, 40), l = C(146, 84, 66), deep = C(52, 24, 26),
            top = C(214, 104, 40), topL = C(255, 182, 78), topD = C(140, 52, 28),
            rock = C(52, 40, 60), rockD = C(32, 24, 40), rockL = C(88, 70, 102), rockDeep = C(16, 12, 22),
            bg = C(38, 20, 22), bgD = C(27, 13, 15), bgL = C(56, 30, 28),
            wood = C(118, 76, 60), woodD = C(70, 40, 34), woodL = C(160, 108, 80),
            spike = C(70, 50, 76), spikeD = C(36, 24, 42), spikeL = C(120, 96, 130), spikeTip = C(255, 110, 50),
            style = 1
        };

        static readonly Pal Vault = new Pal
        {
            b = C(86, 92, 122), d = C(64, 68, 94), l = C(122, 130, 164), deep = C(36, 38, 58),
            top = C(196, 156, 58), topL = C(250, 220, 112), topD = C(132, 98, 40),
            rock = C(46, 46, 60), rockD = C(30, 30, 42), rockL = C(78, 80, 100), rockDeep = C(16, 16, 24),
            bg = C(26, 27, 40), bgD = C(18, 19, 30), bgL = C(38, 40, 56),
            wood = C(120, 112, 100), woodD = C(70, 66, 60), woodL = C(170, 162, 146),
            spike = C(212, 176, 80), spikeD = C(140, 104, 40), spikeL = C(255, 236, 150), spikeTip = C(255, 255, 220),
            style = 2
        };

        static readonly Pal Sunfall = new Pal
        {
            b = C(152, 104, 62), d = C(120, 80, 48), l = C(186, 132, 82), deep = C(76, 50, 32),
            top = C(98, 180, 70), topL = C(172, 228, 112), topD = C(58, 124, 52),
            rock = C(168, 156, 134), rockD = C(126, 114, 98), rockL = C(210, 200, 180), rockDeep = C(86, 76, 66),
            bg = C(64, 46, 34), bgD = C(50, 36, 27), bgL = C(80, 58, 42),
            wood = C(172, 122, 72), woodD = C(104, 68, 38), woodL = C(214, 166, 104),
            spike = C(96, 126, 60), spikeD = C(62, 84, 40), spikeL = C(146, 176, 92), spikeTip = C(236, 226, 186),
            style = 0, vine = true
        };

        // Frozen caves: pale blue ice body with a white snow cap, dark blue rock, icicle spikes.
        static readonly Pal Frost = new Pal
        {
            b = C(118, 170, 208), d = C(86, 134, 180), l = C(168, 214, 238), deep = C(44, 76, 120),
            top = C(232, 244, 252), topL = C(255, 255, 255), topD = C(170, 204, 230),
            rock = C(70, 88, 124), rockD = C(46, 60, 92), rockL = C(112, 134, 172), rockDeep = C(26, 34, 58),
            bg = C(30, 44, 70), bgD = C(22, 32, 54), bgL = C(44, 62, 94),
            wood = C(132, 104, 84), woodD = C(82, 62, 52), woodL = C(178, 148, 120),
            spike = C(170, 220, 244), spikeD = C(96, 150, 196), spikeL = C(236, 250, 255), spikeTip = C(255, 255, 255),
            style = 0
        };

        // Sporegloom: dark violet soil under a skin of glowing teal mycelium, purple rock, pale thorn spikes.
        static readonly Pal Spore = new Pal
        {
            b = C(74, 58, 96), d = C(54, 42, 74), l = C(102, 84, 126), deep = C(30, 22, 44),
            top = C(64, 170, 150), topL = C(130, 236, 206), topD = C(36, 110, 104),
            rock = C(52, 50, 78), rockD = C(34, 32, 54), rockL = C(84, 82, 118), rockDeep = C(18, 16, 30),
            bg = C(26, 20, 36), bgD = C(18, 14, 27), bgL = C(38, 30, 52),
            wood = C(120, 98, 120), woodD = C(74, 58, 78), woodL = C(164, 140, 160),
            spike = C(150, 110, 170), spikeD = C(92, 62, 112), spikeL = C(206, 170, 220), spikeTip = C(140, 255, 210),
            style = 0
        };

        // Gearworks: riveted brass plates over dark iron, copper trim, steel spikes.
        static readonly Pal Gear = new Pal
        {
            b = C(132, 100, 62), d = C(100, 74, 46), l = C(170, 134, 84), deep = C(58, 42, 28),
            top = C(196, 122, 70), topL = C(244, 180, 110), topD = C(132, 76, 44),
            rock = C(58, 62, 72), rockD = C(38, 42, 50), rockL = C(92, 98, 112), rockDeep = C(20, 22, 28),
            bg = C(34, 30, 28), bgD = C(24, 21, 20), bgL = C(48, 42, 38),
            wood = C(110, 112, 122), woodD = C(68, 70, 80), woodL = C(160, 164, 176),
            spike = C(190, 196, 206), spikeD = C(108, 114, 128), spikeL = C(236, 240, 246), spikeTip = C(255, 200, 90),
            style = 2
        };

        // Sandsunk Tombs: long courses of pale, smooth sandstone under drifted sand; dark basalt; bronze spikes.
        static readonly Pal Sand = new Pal
        {
            b = C(188, 164, 124), d = C(160, 136, 100), l = C(212, 192, 154), deep = C(104, 84, 62),
            top = C(232, 208, 150), topL = C(250, 234, 190), topD = C(190, 162, 112),
            rock = C(92, 78, 72), rockD = C(62, 52, 48), rockL = C(132, 114, 104), rockDeep = C(34, 28, 26),
            bg = C(58, 48, 40), bgD = C(46, 38, 32), bgL = C(74, 62, 50),
            wood = C(150, 112, 70), woodD = C(98, 70, 42), woodL = C(200, 160, 104),
            spike = C(202, 150, 84), spikeD = C(130, 88, 46), spikeL = C(246, 214, 150), spikeTip = C(255, 244, 200),
            style = 2, ashlar = true
        };

        // Shardlight Geode: deep indigo rock with a crust of pale crystal on every top face; violet spikes.
        static readonly Pal Geode = new Pal
        {
            b = C(72, 66, 112), d = C(52, 48, 86), l = C(100, 94, 146), deep = C(30, 26, 54),
            top = C(120, 214, 226), topL = C(214, 252, 255), topD = C(64, 138, 170),
            rock = C(48, 46, 72), rockD = C(32, 30, 52), rockL = C(78, 76, 110), rockDeep = C(16, 14, 28),
            bg = C(26, 24, 44), bgD = C(18, 16, 32), bgL = C(40, 38, 64),
            wood = C(110, 100, 140), woodD = C(70, 62, 96), woodL = C(156, 146, 190),
            spike = C(168, 130, 226), spikeD = C(104, 72, 160), spikeL = C(226, 200, 255), spikeTip = C(220, 255, 255),
            style = 1
        };

        static void AllTiles()
        {
            TileSetArt("Geode", Geode, 131);
            TileSetArt("Sandsunk", Sand, 113);
            TileSetArt("Gearworks", Gear, 97);
            TileSetArt("Sporegloom", Spore, 83);
            TileSetArt("Frost", Frost, 67);
            TileSetArt("Mossroot", Moss, 11);
            TileSetArt("Emberdeep", Ember, 23);
            TileSetArt("Vault", Vault, 37);
            TileSetArt("Sunfall", Sunfall, 51);
            SkyArt();
            AtmosphereArt();
        }

        /// <summary>Smooth value noise that repeats every 'period' lattice cells (for seamless tiling textures).</summary>
        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int W(int v) => ((v % period) + period) % period;
            float a = N(W(x0), W(y0), seed), b = N(W(x0 + 1), W(y0), seed), c = N(W(x0), W(y0 + 1), seed), d = N(W(x0 + 1), W(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Lighting / depth art shared by every area: contact shadows, parallax rock layers, wall torch.</summary>
        static void AtmosphereArt()
        {
            // Contact shadows on the back wall, by solid-neighbour mask (1=up, 2=right, 4=down, 8=left).
            for (int m = 0; m < 16; m++)
            {
                var px = new Px(16, 16);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float a = 0f;
                        const float reach = 6f;
                        if ((m & 1) != 0) a = Mathf.Max(a, 1f - (15 - y) / reach);          // ceiling casts the deepest shadow
                        if ((m & 2) != 0) a = Mathf.Max(a, 0.75f * (1f - (15 - x) / reach));
                        if ((m & 4) != 0) a = Mathf.Max(a, 0.6f * (1f - y / reach));
                        if ((m & 8) != 0) a = Mathf.Max(a, 0.75f * (1f - x / reach));
                        if (a <= 0f) continue;
                        int step = Mathf.CeilToInt(a * 3f);                                  // 3 flat bands: stays pixel art
                        px.Set(x, y, C(8, 5, 12, step * 38));
                    }
                Save(px, $"Tiles/Common/shade_{m:00}", PCenter);
            }

            // Parallax rock silhouettes (tile seamlessly in both directions).
            for (int layer = 0; layer < 2; layer++)
            {
                const int S = 96;
                var px = new Px(S, S);
                int period = layer == 0 ? 3 : 4;
                float cell = S / (float)period;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float n = TileNoise(x / cell, y / cell, period, 71 + layer * 13);
                        float n2 = TileNoise(x / (cell * 0.5f), y / (cell * 0.5f), period * 2, 91 + layer * 7);
                        float v = n * 0.75f + n2 * 0.25f;
                        float cut = layer == 0 ? 0.5f : 0.56f;
                        if (v < cut) continue;
                        int alpha = layer == 0 ? 120 : 150;
                        if (v < cut + 0.035f) alpha = alpha * 55 / 100;                      // one-step soft rim
                        px.Set(x, y, C(255, 255, 255, alpha));
                    }
                Save(px, "Sky/cave_" + layer, PCenter);
            }

            // Wall torch: bracket + 3 flame frames.
            {
                var br = new Px(8, 12);
                Color32 wood = C(110, 72, 42), woodD = C(70, 44, 26), iron = C(96, 96, 108), ironD = C(56, 56, 66), ol = C(22, 14, 20);
                br.Rect(3, 0, 2, 7, wood); br.Rect(3, 0, 1, 7, woodD);
                br.Rect(2, 7, 4, 2, iron); br.Rect(2, 7, 4, 1, ironD);
                br.Rect(1, 9, 6, 1, iron);
                br.Outline(ol);
                Save(br, "Sprites/Props/torch", new Vector2(0.5f, 0.75f));
                for (int f = 0; f < 3; f++)
                {
                    var fl = new Px(8, 10);
                    Color32 o = C(232, 84, 30), y = C(255, 176, 52), w = C(255, 244, 190);
                    int lean = f - 1;
                    fl.Rect(2, 0, 4, 3, o); fl.Rect(2 + (lean > 0 ? 1 : 0), 3, 3, 2, o); fl.Rect(3 + lean, 5, 2, 2, o); fl.Set(3 + lean + (f == 1 ? 1 : 0), 7, o);
                    fl.Rect(3, 0, 2, 3, y); fl.Rect(3, 3, 2 - (f == 2 ? 1 : 0), 2, y); fl.Set(3 + lean, 5, y);
                    fl.Rect(3, 0, 2, 2, w);
                    Save(fl, $"Sprites/Props/torch_flame_{f}", new Vector2(0.5f, 0f));
                }
            }
        }

        static void TileSetArt(string name, Pal p, int seed)
        {
            string dir = "Tiles/" + name + "/";
            for (int v = 0; v < 2; v++)
                for (int m = 0; m < 16; m++)
                    Save(p.ashlar ? Ashlar(m, v, p, seed) : p.style == 2 ? Brick(m, v, p, seed) : Earth(m, v, p, seed), $"{dir}dirt_{m:00}_{v}", PCenter);
            for (int m = 0; m < 16; m++) Save(Bedrock(m, p, seed), $"{dir}bedrock_{m:00}", PCenter);
            for (int m = 0; m < 16; m++) Save(Brick(m, 0, p, seed + 5), $"{dir}brick_{m:00}", PCenter);
            for (int v = 0; v < 4; v++) Save(Background(v, p, seed), $"{dir}bg_{v}", PCenter);
            Save(ShopBackground(), dir + "bg_shop", PCenter);
            Save(Platform(p), dir + "platform", PCenter);
            Save(Ladder(p), dir + "ladder", PCenter);
            Save(Spikes(p), dir + "spikes", PCenter);
            Save(TrapBlock(p), dir + "trapblock", PCenter);
            Save(VentBlock(), dir + "ventblock", PCenter);
            Save(Rubble(p, seed), dir + "rubble", PCenter);
            Save(GoldOverlay(seed), dir + "gold_overlay", PCenter);
            Save(GemOverlay(), dir + "gem_overlay", PCenter);
            for (int i = 0; i < 4; i++) Save(DecorFloor(p, i, seed), $"{dir}decor_floor_{i}", PCenter);
            for (int i = 0; i < 3; i++) Save(DecorCeiling(p, i, seed), $"{dir}decor_ceiling_{i}", PCenter);
        }

        static void Edges(Px px, int mask, Pal p, Color32 deep)
        {
            bool top = (mask & 1) != 0, right = (mask & 2) != 0, bottom = (mask & 4) != 0, left = (mask & 8) != 0;
            for (int i = 0; i < 16; i++)
            {
                if (right) { px.Set(15, i, deep); px.Set(14, i, Mul(px.Get(14, i), 0.82f)); }
                if (left) { px.Set(0, i, deep); px.Set(1, i, Mul(px.Get(1, i), 0.9f)); }
                if (bottom) { px.Set(i, 0, deep); px.Set(i, 1, Mul(px.Get(i, 1), 0.8f)); }
            }
            if (top && left) px.Set(0, 15, Clear);
            if (top && right) px.Set(15, 15, Clear);
            if (bottom && left) px.Set(0, 0, Clear);
            if (bottom && right) px.Set(15, 0, Clear);
        }

        static Px Earth(int mask, int v, Pal p, int seed)
        {
            var px = new Px(16, 16);
            int s = seed + v * 131;
            Color32 mid = Lerp(p.b, p.l, 0.45f), low = Lerp(p.b, p.d, 0.55f);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    // soft clumps (4 px lattice, wraps at the tile edge so neighbouring tiles join without seams)
                    float n = TileNoise(x / 4f, y / 4f, 4, s) * 0.7f + TileNoise(x / 2f, y / 2f, 8, s + 31) * 0.3f;
                    Color32 col = n < 0.36f ? low : (n > 0.66f ? mid : p.b);
                    float g = N(x, y, s);
                    if (g < 0.035f) col = p.d; else if (g > 0.975f) col = p.l;
                    px.Set(x, y, col);
                }
            // pebbles pressed into the soil: light top, dark underside
            // (only in the second variant, and low-contrast, so large walls do not show a repeating dot grid)
            for (int k = 0; k < v; k++)
            {
                int sx = 3 + (int)(N(k, v, s + 7) * 9), sy = 4 + (int)(N(v, k, s + 9) * 8);
                int wdt = 2 + (N(k, 5, s + 3) > 0.5f ? 1 : 0);
                for (int i = 0; i < wdt; i++)
                {
                    px.Set(sx + i, sy, Lerp(p.b, p.l, 0.8f));
                    px.Set(sx + i, sy - 1, Lerp(p.b, p.l, 0.3f));
                    px.Set(sx + i, sy - 2, p.d);
                }
            }
            Edges(px, mask, p, p.deep);
            // exposed underside: a second, softer shadow row so overhangs read as thick
            if ((mask & 4) != 0)
                for (int x = 1; x < 15; x++) if (px.Get(x, 2).a > 0) px.Set(x, 2, Mul(px.Get(x, 2), 0.88f));
            if ((mask & 1) != 0)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (px.Get(x, 15).a == 0) continue;
                    int depth = 2 + (N(x, 3, s) > 0.55f ? 1 : 0) + (N(x, 4, s) > 0.86f ? 2 : 0);
                    for (int d = 0; d < depth; d++)
                    {
                        Color32 col = d == 0 ? p.topL : (d == depth - 1 ? p.topD : p.top);
                        if (p.style == 1 && d > 0 && N(x, d, s + 3) > 0.7f) col = p.topL;
                        px.Set(x, 15 - d, col);
                    }
                }
            }
            return px;
        }

        static Px Brick(int mask, int v, Pal p, int seed)
        {
            var px = new Px(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int row = y / 4;
                    int off = (row % 2) * 4 + v * 2;
                    bool mortar = y % 4 == 3 || (x + off) % 8 == 7;
                    Color32 col = mortar ? p.deep : (N(x / 2, y / 2, seed + row) < 0.2f ? p.d : p.b);
                    if (!mortar && (x + off) % 8 == 0) col = p.l;
                    if (!mortar && y % 4 == 0) col = Mul(col, 0.88f);
                    px.Set(x, y, col);
                }
            Edges(px, mask, p, p.deep);
            if ((mask & 1) != 0 && p.style == 2)
                for (int x = 0; x < 16; x++)
                {
                    if (px.Get(x, 15).a == 0) continue;
                    px.Set(x, 15, p.topL); px.Set(x, 14, p.top); px.Set(x, 13, p.topD);
                }
            return px;
        }

        /// <summary>
        /// A tomb wall: smooth sandstone laid in two long courses to a tile. The stone is almost plain - a little
        /// grain, nothing busy - and the joints are a shade darker, not black: the upright joints fall at different
        /// places in the two courses and in the two variants, so the wall reads as long stones in running bond,
        /// never as a grid. Loose sand lies deep on every top face.
        /// </summary>
        static Px Ashlar(int mask, int v, Pal p, int seed)
        {
            var px = new Px(16, 16);
            Color32 joint = Lerp(p.d, p.deep, 0.45f), worn = Lerp(p.b, p.l, 0.55f);
            int upA = v == 0 ? 5 : 11, upB = v == 0 ? 13 : 2;            // where the upright joint falls: upper course, lower course
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool upper = y >= 8;
                    Color32 col = p.b;
                    float n = N(x / 2, y / 2, seed + v * 31);
                    if (n < 0.07f) col = p.d; else if (n > 0.95f) col = worn;        // sparse grain
                    if (y == 0 || y == 8) col = joint;                                 // the bed under each course
                    else if (x == (upper ? upA : upB)) col = joint;                    // one upright joint in each
                    else if (y == 7 || y == 15) col = worn;                            // the worn upper edge of the stone
                    px.Set(x, y, col);
                }
            Edges(px, mask, p, p.deep);
            if ((mask & 1) != 0)
                for (int x = 0; x < 16; x++)
                {
                    if (px.Get(x, 15).a == 0) continue;
                    // the drift lies three to five rows deep, and a few grains trickle down the face
                    int depth = 3 + (N(x / 3, 0, seed + v * 5 + 11) > 0.5f ? 1 : 0) + (N(x / 4, 1, seed + 3) > 0.75f ? 1 : 0);
                    for (int d = 0; d < depth; d++) px.Set(x, 15 - d, d == 0 ? p.topL : d >= depth - 1 ? p.topD : p.top);
                    if (N(x, 2, seed + v + 23) > 0.9f) px.Set(x, 15 - depth - 1, p.topD);
                }
            return px;
        }

        static Px Bedrock(int mask, Pal p, int seed)
        {
            var px = new Px(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int bx = x / 8, by = y / 8, lx = x % 8, ly = y % 8;
                    float shade = 0.9f + N(bx, by, seed + 99) * 0.2f;
                    Color32 col = Mul(p.rock, shade);
                    if (N(x, y, seed + 5) < 0.12f) col = p.rockD;
                    if (lx == 7 || ly == 0) col = p.rockDeep;
                    else if (lx == 0 || ly == 7) col = p.rockL;
                    else if (lx == 6 || ly == 1) col = p.rockD;
                    px.Set(x, y, col);
                }
            // tiny rivet marks make bedrock instantly readable as "not breakable"
            px.Set(3, 11, p.rockL); px.Set(11, 3, p.rockL); px.Set(12, 12, p.rockL); px.Set(4, 4, p.rockL);
            Edges(px, mask, p, Mul(p.rockDeep, 0.7f));
            if ((mask & 1) != 0) for (int x = 0; x < 16; x++) if (px.Get(x, 15).a > 0) px.Set(x, 15, Mul(p.rockL, 1.15f));
            return px;
        }

        static Px Background(int v, Pal p, int seed)
        {
            var px = new Px(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float n = TileNoise(x / 4f, y / 4f, 4, seed + v * 17 + 300);
                    float n2 = N(x, y, seed + v);
                    Color32 col = n < 0.4f ? p.bgD : (n > 0.72f ? Lerp(p.bg, p.bgL, 0.5f) : p.bg);
                    if (n2 > 0.975f) col = p.bgL; else if (n2 < 0.02f) col = Mul(p.bgD, 0.85f);
                    px.Set(x, y, col);
                }
            if (p.ashlar)
            {
                // the back wall: the same long courses, in shadow and barely drawn; now and then a single carved sign
                Color32 j = Mul(p.bgD, 0.9f), g = Lerp(p.bg, p.bgL, 0.6f);
                for (int x = 0; x < 16; x++) { px.Set(x, 0, j); px.Set(x, 8, j); }
                for (int y = 1; y < 8; y++) px.Set(v % 2 == 0 ? 3 : 12, y, j);
                for (int y = 9; y < 16; y++) px.Set(v % 2 == 0 ? 10 : 6, y, j);
                if (v == 3) { px.Line(5, 12, 9, 12, g); px.Set(4, 11, g); px.Set(10, 11, g); px.Line(5, 10, 9, 10, g); px.Set(7, 11, g); }   // an eye
                return px;
            }
            if (p.style == 2)
            {
                for (int x = 0; x < 16; x++) { px.Set(x, 0, p.bgD); px.Set(x, 8, p.bgD); }
                for (int y = 0; y < 8; y++) { px.Set(v % 2 == 0 ? 0 : 8, y, p.bgD); px.Set(v % 2 == 0 ? 8 : 0, y + 8, p.bgD); }
            }
            if (v == 1) px.Line(2, 4, 13, 10, p.bgD);
            if (v == 2) { px.Ellipse(8, 6, 3.5f, 2.2f, p.bgL); px.Ellipse(8, 5.5f, 3f, 1.5f, p.bg); }
            if (v == 3)
            {
                if (p.style == 1) { px.Set(5, 9, C(120, 50, 30)); px.Set(6, 9, C(160, 70, 30)); px.Set(10, 4, C(120, 50, 30)); }
                else { px.Line(2, 12, 6, 9, p.bgL); px.Line(6, 9, 12, 10, p.bgL); }
            }
            return px;
        }

        static Px ShopBackground()
        {
            var px = new Px(16, 16);
            Color32 a = C(74, 48, 32), b = C(62, 40, 27), seam = C(40, 25, 17);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int plank = x / 4;
                    Color32 col = plank % 2 == 0 ? a : b;
                    if (x % 4 == 3) col = seam;
                    if (N(x, y, 77) > 0.93f) col = Mul(col, 1.15f);
                    px.Set(x, y, col);
                }
            px.Set(1, 13, C(150, 140, 120)); px.Set(9, 13, C(150, 140, 120)); px.Set(5, 2, C(150, 140, 120)); px.Set(13, 2, C(150, 140, 120));
            return px;
        }

        static Px Platform(Pal p)
        {
            var px = new Px(16, 16);
            px.Rect(0, 11, 16, 5, p.wood);
            for (int x = 0; x < 16; x++) { px.Set(x, 15, p.woodL); px.Set(x, 11, p.woodD); }
            px.Set(5, 13, p.woodD); px.Set(11, 13, p.woodD);
            px.Set(2, 14, p.woodD); px.Set(13, 14, p.woodD);
            px.Rect(2, 7, 2, 4, p.woodD); px.Rect(12, 7, 2, 4, p.woodD);
            px.Set(3, 10, p.wood); px.Set(12, 10, p.wood);
            return px;
        }

        static Px Ladder(Pal p)
        {
            var px = new Px(16, 16);
            if (p.vine)
            {
                Color32 stem = C(64, 128, 52), stemD = C(40, 90, 40), leaf = C(110, 182, 78);
                for (int y = 0; y < 16; y++)
                {
                    int x = 7 + (int)Mathf.Round(Mathf.Sin(y * 0.8f) * 1.2f);
                    px.Set(x, y, stem); px.Set(x + 1, y, stemD);
                }
                foreach (int y in new[] { 2, 7, 12 }) { px.Rect(3, y, 4, 2, leaf); px.Set(3, y + 1, stemD); }
                foreach (int y in new[] { 4, 10, 15 }) { px.Rect(10, y, 4, 2, leaf); px.Set(13, y + 1, stemD); }
                return px;
            }
            px.Rect(2, 0, 2, 16, p.wood); px.Rect(12, 0, 2, 16, p.wood);
            for (int y = 0; y < 16; y++) { px.Set(2, y, p.woodL); px.Set(13, y, p.woodD); }
            foreach (int y in new[] { 2, 6, 10, 14 })
            {
                px.Rect(4, y, 8, 1, p.woodL);
                px.Rect(4, y - 1, 8, 1, p.woodD);
            }
            return px;
        }

        static Px Spikes(Pal p)
        {
            var px = new Px(16, 16);
            px.Rect(0, 0, 16, 2, p.spikeD);
            for (int i = 0; i < 4; i++)
            {
                int cx = 2 + i * 4;
                for (int y = 2; y < 10; y++)
                {
                    int half = (10 - y) / 3;
                    for (int x = cx - half; x <= cx + half; x++) px.Set(x, y, x < cx ? p.spikeL : p.spike);
                    px.Set(cx + half, y, p.spikeD);
                }
                px.Set(cx, 10, p.spikeTip);
                px.Set(cx, 9, p.spikeTip);
            }
            return px;
        }

        static Px TrapBlock(Pal p)
        {
            var px = new Px(16, 16);
            Color32 s = p.style == 1 ? C(98, 84, 82) : C(120, 118, 112), sd = Mul(s, 0.7f), sl = Mul(s, 1.25f);
            px.Fill(s);
            for (int i = 0; i < 16; i++) { px.Set(i, 15, sl); px.Set(0, i, sl); px.Set(i, 0, sd); px.Set(15, i, sd); }
            px.Rect(3, 3, 10, 10, Mul(s, 0.9f));
            for (int i = 3; i < 13; i++) { px.Set(i, 12, sd); px.Set(3, i, sd); px.Set(i, 3, sl); px.Set(12, i, sl); }
            return px;
        }

        /// <summary>Cracked rubble: a pile of loose boulders with dark cracks - clearly different from dirt and bedrock.</summary>
        static Px Rubble(Pal p, int seed)
        {
            var px = new Px(16, 16);
            Color32 baseC = p.style == 1 ? C(122, 92, 84) : C(132, 122, 108);
            Color32 dark = Mul(baseC, 0.55f), light = Mul(baseC, 1.3f), crack = C(30, 22, 20);
            px.Fill(Mul(baseC, 0.7f));
            float[,] stones = { { 4, 4, 3.6f }, { 11, 3.5f, 3.4f }, { 7.5f, 9, 3.8f }, { 3, 12.5f, 2.8f }, { 12.5f, 11.5f, 3.2f } };
            for (int i = 0; i < stones.GetLength(0); i++)
            {
                float cx = stones[i, 0], cy = stones[i, 1], r = stones[i, 2];
                px.Disc(cx, cy, r, dark);
                px.Disc(cx - 0.4f, cy + 0.4f, r - 0.8f, baseC);
                px.Set((int)(cx - r * 0.4f), (int)(cy + r * 0.4f), light);
                px.Set((int)(cx - r * 0.4f) + 1, (int)(cy + r * 0.4f), light);
            }
            px.Line(2, 7, 6, 6, crack); px.Line(6, 6, 9, 7, crack); px.Line(10, 14, 12, 8, crack); px.Line(4, 1, 5, 3, crack);
            for (int i = 0; i < 16; i++) { px.Set(i, 0, Mul(dark, 0.8f)); px.Set(15, i, Mul(dark, 0.8f)); }
            return px;
        }

        static Px VentBlock()
        {
            var px = new Px(16, 16);
            Color32 m = C(64, 58, 66), md = C(38, 34, 40), ml = C(104, 98, 108), glow = C(240, 120, 40), glowL = C(255, 200, 90);
            px.Fill(m);
            for (int i = 0; i < 16; i++) { px.Set(i, 15, ml); px.Set(i, 0, md); px.Set(0, i, ml); px.Set(15, i, md); }
            foreach (int y in new[] { 4, 8, 12 })
            {
                px.Rect(3, y, 10, 1, glow);
                px.Rect(3, y - 1, 10, 1, md);
                px.Set(7, y, glowL); px.Set(8, y, glowL);
            }
            px.Set(1, 14, ml); px.Set(14, 14, ml); px.Set(1, 1, ml); px.Set(14, 1, ml);
            return px;
        }

        static Px GoldOverlay(int seed)
        {
            var px = new Px(16, 16);
            Color32 g = C(240, 196, 64), gl = C(255, 244, 160), gd = C(160, 112, 28);
            int[,] pts = { { 4, 10 }, { 10, 5 }, { 11, 11 }, { 5, 4 } };
            for (int i = 0; i < 4; i++)
            {
                int x = pts[i, 0], y = pts[i, 1];
                px.Rect(x, y, 2, 2, g);
                px.Set(x, y + 1, gl);
                px.Set(x + 1, y, gd);
                if (i % 2 == 0) { px.Set(x + 2, y, g); px.Set(x + 2, y + 1, gd); }
            }
            return px;
        }

        static Px GemOverlay()
        {
            var px = new Px(16, 16);
            Color32 g = C(60, 210, 130), gl = C(190, 255, 220), gd = C(20, 120, 70);
            px.Rect(7, 5, 3, 1, gd); px.Rect(6, 6, 5, 3, g); px.Rect(7, 9, 3, 1, g);
            px.Set(7, 8, gl); px.Set(8, 9, gl); px.Set(10, 6, gd); px.Set(9, 5, gd);
            return px;
        }

        static Px DecorFloor(Pal p, int i, int seed)
        {
            var px = new Px(16, 16);
            if (p.style == 0)
            {
                switch (i)
                {
                    case 0: // grass tuft
                        for (int x = 3; x < 13; x++)
                        {
                            int h = 2 + (int)(N(x, 1, seed) * 4);
                            for (int y = 0; y < h; y++) px.Set(x, y, y == h - 1 ? p.topL : p.top);
                        }
                        break;
                    case 1: // mushroom
                        px.Rect(7, 0, 2, 3, C(220, 210, 180));
                        px.Ellipse(8, 4, 3.5f, 1.8f, C(200, 60, 50));
                        px.Set(6, 4, C(255, 240, 230)); px.Set(9, 5, C(255, 240, 230));
                        break;
                    case 2: // skull
                        px.Ellipse(8, 2.5f, 2.5f, 2.2f, C(220, 214, 196));
                        px.Set(7, 2, C(40, 30, 30)); px.Set(9, 2, C(40, 30, 30)); px.Rect(7, 0, 3, 1, C(200, 194, 176));
                        break;
                    default: // pebbles
                        px.Rect(3, 0, 3, 2, p.l); px.Rect(10, 0, 2, 1, p.l); px.Set(4, 1, p.b); px.Rect(12, 0, 2, 2, p.d);
                        break;
                }
            }
            else if (p.style == 1)
            {
                switch (i)
                {
                    case 0: // ember crystal
                        px.Line(6, 0, 7, 6, C(255, 140, 50)); px.Line(8, 0, 10, 4, C(255, 190, 90)); px.Set(7, 6, C(255, 230, 160));
                        px.Line(5, 0, 5, 3, C(200, 80, 30));
                        break;
                    case 1: // ash pile
                        px.Ellipse(8, 0.5f, 5f, 2f, C(90, 80, 82)); px.Set(6, 1, C(255, 120, 40)); px.Set(10, 0, C(255, 120, 40));
                        break;
                    case 2: // fire flower
                        px.Rect(7, 0, 1, 4, C(80, 60, 40)); px.Disc(7.5f, 5f, 1.8f, C(255, 110, 40)); px.Set(7, 5, C(255, 220, 120));
                        break;
                    default:
                        px.Rect(4, 0, 2, 2, p.l); px.Rect(10, 0, 3, 1, p.d); px.Set(11, 1, C(220, 90, 40));
                        break;
                }
            }
            else
            {
                switch (i)
                {
                    case 0: // candle
                        px.Rect(7, 0, 2, 4, C(230, 220, 190)); px.Set(7, 4, C(60, 50, 40)); px.Set(7, 5, C(255, 200, 80)); px.Set(7, 6, C(255, 240, 160));
                        break;
                    case 1: // broken urn
                        px.Ellipse(8, 2, 3, 2, C(170, 110, 70)); px.Rect(6, 3, 2, 2, C(150, 96, 60));
                        break;
                    case 2: // coins
                        px.Rect(5, 0, 3, 1, C(240, 196, 64)); px.Rect(9, 0, 2, 1, C(240, 196, 64)); px.Set(6, 1, C(255, 244, 160));
                        break;
                    default:
                        px.Ellipse(8, 2.5f, 2.5f, 2.2f, C(220, 214, 196)); px.Set(7, 2, C(40, 30, 30)); px.Set(9, 2, C(40, 30, 30));
                        break;
                }
            }
            return px;
        }

        static Px DecorCeiling(Pal p, int i, int seed)
        {
            var px = new Px(16, 16);
            if (p.style == 0)
            {
                if (i == 0) { px.Line(5, 15, 6, 10, C(110, 76, 44)); px.Line(6, 10, 5, 6, C(110, 76, 44)); px.Line(10, 15, 11, 11, C(90, 62, 36)); }
                else if (i == 1) { for (int x = 4; x < 12; x++) { int h = 2 + (int)(N(x, 9, seed) * 5); for (int y = 0; y < h; y++) px.Set(x, 15 - y, y == h - 1 ? p.topD : p.top); } }
                else { px.Line(0, 15, 6, 9, C(200, 200, 210, 120)); px.Line(0, 11, 4, 15, C(200, 200, 210, 120)); px.Line(0, 13, 5, 13, C(200, 200, 210, 90)); }
            }
            else if (p.style == 1)
            {
                if (i == 0) { px.Rect(7, 11, 3, 5, C(120, 60, 40)); px.Rect(8, 8, 1, 3, C(255, 140, 50)); px.Set(8, 7, C(255, 220, 120)); }
                else if (i == 1) { for (int y = 6; y < 16; y++) px.Set(8, y, y % 2 == 0 ? C(110, 104, 112) : C(70, 66, 74)); }
                else { px.Line(4, 15, 5, 10, C(255, 120, 40)); px.Set(5, 9, C(255, 220, 140)); }
            }
            else
            {
                if (i == 0) { for (int y = 4; y < 16; y++) px.Set(8, y, y % 2 == 0 ? C(150, 146, 156) : C(96, 92, 104)); }
                else if (i == 1) { px.Rect(5, 6, 6, 10, C(120, 30, 40)); px.Rect(5, 6, 6, 1, C(200, 160, 60)); px.Set(7, 5, C(120, 30, 40)); px.Set(8, 4, C(120, 30, 40)); }
                else { px.Line(0, 15, 6, 9, C(200, 200, 210, 120)); px.Line(0, 11, 4, 15, C(200, 200, 210, 120)); }
            }
            return px;
        }

        static void SkyArt()
        {
            const string d = "Sky/";
            var g = new Px(4, 96);
            // a winter's day from the horizon up: pale and a little grey low down, a clear cold blue overhead;
            // quantised so the sky reads as pixel art, not a smooth ramp (dusk, dawn and night are tints laid over it)
            Color32[] stops = { C(222, 228, 232), C(202, 214, 226), C(176, 196, 216), C(150, 176, 206), C(124, 154, 192), C(102, 134, 176), C(86, 118, 162) };
            float[] at = { 0f, 0.14f, 0.3f, 0.48f, 0.66f, 0.84f, 1f };
            for (int y = 0; y < 96; y++)
            {
                float t = Mathf.Floor(y / 95f * 24f) / 24f;
                int k = 0;
                while (k < at.Length - 2 && t > at[k + 1]) k++;
                Color32 c = Lerp(stops[k], stops[k + 1], Mathf.InverseLerp(at[k], at[k + 1], t));
                for (int x = 0; x < 4; x++) g.Set(x, y, c);
            }
            Save(g, d + "gradient", PCenter);

            var sun = new Px(48, 48);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    float dx = x - 23.5f, dy = y - 23.5f, dd = Mathf.Sqrt(dx * dx + dy * dy);
                    // (a pale winter sun)
                    if (dd < 9f) sun.Set(x, y, C(255, 253, 240));
                    else if (dd < 11f) sun.Set(x, y, C(250, 246, 226));
                    else if (dd < 16f) sun.Set(x, y, C(246, 244, 232, 90));
                    else if (dd < 23f) sun.Set(x, y, C(240, 242, 240, 40));
                }
            Save(sun, d + "sun", PCenter);

            var range = new Px(128, 40);
            Color32 rg = C(158, 176, 198), rgL = C(192, 206, 222);
            for (int x = 0; x < 128; x++)
            {
                float hgt = 14f + 12f * Mathf.Abs(Mathf.Sin(x * 0.0736f + 0.6f)) + 6f * Mathf.Abs(Mathf.Sin(x * 0.196f + 2.1f));
                int h = Mathf.Min(39, (int)hgt);
                for (int y = 0; y < h; y++) range.Set(x, y, y > h - 4 ? rgL : rg);
            }
            Save(range, d + "range", PCenter);

            var far = new Px(128, 44);
            Color32 m = C(118, 138, 168), mL = C(158, 176, 200), snow = C(240, 244, 248);
            for (int x = 0; x < 128; x++)
            {
                float hgt = 16f + 10f * Mathf.Abs(Mathf.Sin(x * 0.049f * Mathf.PI)) + 7f * Mathf.Abs(Mathf.Sin(x * 0.11f + 1.3f)) + 2f * N(x, 1, 3);
                int h = Mathf.Min(43, (int)hgt);
                for (int y = 0; y < h; y++) far.Set(x, y, y > h - 3 && h > 30 ? snow : (y > h - 6 ? mL : m));
            }
            Save(far, d + "mountains", PCenter);

            var near = new Px(128, 32);
            Color32 hill = C(78, 96, 116), hillD = C(60, 76, 94);
            for (int x = 0; x < 128; x++)
            {
                float hgt = 10f + 6f * Mathf.Sin(x * 0.0982f) + 3f * Mathf.Sin(x * 0.31f + 2f);
                int h = (int)hgt;
                if (N(x / 3, 7, 11) > 0.7f) h += 3;
                for (int y = 0; y < Mathf.Min(31, h); y++) near.Set(x, y, y > h - 3 ? hill : hillD);
            }
            Save(near, d + "hills", PCenter);

            for (int k = 0; k < 2; k++)
            {
                var cl = new Px(k == 0 ? 64 : 40, k == 0 ? 22 : 14);
                Color32 c = C(232, 236, 242, 225), cs = C(188, 200, 216, 225), hl = C(248, 250, 254, 235);
                if (k == 0) { cl.Ellipse(16, 8, 12, 6, c); cl.Ellipse(34, 11, 15, 8, c); cl.Ellipse(50, 7, 10, 5, c); cl.Ellipse(30, 15, 9, 5, hl); }
                else { cl.Ellipse(12, 6, 9, 5, c); cl.Ellipse(26, 7, 11, 6, c); cl.Ellipse(22, 10, 6, 3, hl); }
                for (int x = 0; x < cl.w; x++) for (int y = 0; y < 4; y++) if (cl.Get(x, y).a > 0) cl.Set(x, y, cs);
                Save(cl, d + "cloud_" + k, PCenter);
            }
        }

        // ======================================================================================= CHARACTERS

        struct Pose
        {
            public int legA, legB, liftA, liftB, bob, arm;
            public bool crouch, stun, lookUp, dead;
            public int scarf;
        }

        class Look
        {
            public Color32 skin, skinD, hair, hat, hatD, hatL, jacket, jacketD, jacketL, scarf, scarfD, pants, pantsD, boot, bootD, belt, buckle, eye, outline;
            public bool bald, beard, apron;
            public Color32 beardC, apronC;
        }

        static readonly Look Hero = new Look
        {
            skin = C(240, 188, 140), skinD = C(196, 136, 98), hair = C(92, 54, 30),
            hat = C(150, 98, 50), hatD = C(84, 50, 26), hatL = C(196, 140, 80),
            jacket = C(58, 120, 136), jacketD = C(36, 78, 96), jacketL = C(96, 164, 172),
            scarf = C(214, 56, 56), scarfD = C(140, 30, 40),
            pants = C(84, 70, 96), pantsD = C(58, 48, 70), boot = C(96, 58, 32), bootD = C(62, 36, 20),
            belt = C(60, 36, 22), buckle = C(236, 200, 80), eye = C(28, 20, 24), outline = C(22, 14, 20)
        };

        static readonly Look Keeper = new Look
        {
            skin = C(228, 170, 126), skinD = C(186, 124, 86), hair = C(90, 60, 40),
            jacket = C(214, 210, 196), jacketD = C(160, 156, 144), jacketL = C(240, 238, 228),
            scarf = C(80, 60, 120), scarfD = C(50, 36, 80),
            pants = C(60, 60, 70), pantsD = C(40, 40, 50), boot = C(60, 40, 30), bootD = C(40, 26, 18),
            belt = C(40, 30, 20), buckle = C(200, 180, 120), eye = C(28, 20, 24), outline = C(22, 14, 20),
            bald = true, beard = true, beardC = C(110, 70, 44), apron = true, apronC = C(126, 84, 52)
        };

        static Px Humanoid(Pose s, Look k)
        {
            var p = new Px(20, 20);
            const int ox = 2, oy = 1;
            void R(int x, int y, int w, int h, Color32 c) => p.Rect(x + ox, y + oy, w, h, c);
            void P(int x, int y, Color32 c) => p.Set(x + ox, y + oy, c);

            if (s.dead)
            {
                R(1, 0, 3, 2, k.boot); R(4, 0, 3, 2, k.pants); R(7, 0, 1, 3, k.belt);
                R(8, 0, 3, 3, k.jacket); R(11, 0, 1, 3, k.scarf); R(12, 0, 3, 3, k.skin);
                P(13, 2, k.eye); P(14, 1, k.eye);
                if (!k.bald) { R(15, 0, 1, 4, k.hat); R(16, 1, 1, 2, k.hatD); }
                p.Outline(k.outline);
                return p;
            }

            int cr = s.crouch ? 3 : 0;
            int y0 = 5 - cr + s.bob;

            // back arm
            if (s.arm == 4) { R(4, y0 + 2, 1, 4, k.jacketD); P(4, y0 + 6, k.skin); }
            else if (s.arm == 6) { R(4, y0 + 3, 1, 2, k.jacketD); P(3, y0 + 5, k.skin); }
            else if (s.arm == 5) { }
            else { R(4, y0 + 1, 1, 3, k.jacketD); P(4, y0, k.skin); }

            // legs
            void Leg(int x, int lift, bool back)
            {
                var pc = back ? k.pantsD : k.pants;
                var bc = back ? k.bootD : k.boot;
                if (s.crouch) { R(x, 1 + lift, 3, 1, pc); R(x, lift, 3, 1, bc); }
                else { R(x, 2 + lift, 2, Mathf.Max(1, y0 - 2 - lift), pc); R(x, lift, 3, 2, bc); }
            }
            Leg(5 + s.legA, s.liftA, true);
            Leg(8 + s.legB, s.liftB, false);

            // torso
            R(5, y0, 6, 1, k.belt); P(9, y0, k.buckle);
            R(5, y0 + 1, 6, 3, k.jacket);
            R(5, y0 + 1, 1, 3, k.jacketD);
            P(9, y0 + 3, k.jacketL); P(10, y0 + 2, k.jacketL);
            if (k.apron) { R(7, y0 - 1, 4, 4, k.apronC); P(8, y0 + 2, Mul(k.apronC, 0.8f)); }
            R(5, y0 + 4, 6, 1, k.scarf);
            if (!k.beard)
            {
                if (s.scarf == 0) { R(3, y0 + 3, 2, 1, k.scarf); P(4, y0 + 4, k.scarfD); }
                else { R(3, y0 + 4, 2, 1, k.scarf); P(2, y0 + 5, k.scarfD); }
            }

            // head
            int hy = y0 + 5;
            R(6, hy, 5, 3, k.skin);
            if (!k.bald) R(5, hy, 2, 3, k.hair); else P(5, hy + 1, k.skinD);
            int ey = s.lookUp ? hy + 2 : hy + 1;
            if (s.stun) { P(8, ey, k.eye); P(10, ey, k.eye); }
            else P(9, ey, k.eye);
            P(11, hy + 1, k.skinD);
            P(7, hy + 1, k.skinD);
            if (k.beard) { R(7, hy - 1, 5, 2, k.beardC); P(11, hy + 1, k.beardC); P(8, hy - 2, k.beardC); }

            if (!k.bald)
            {
                int tilt = s.lookUp ? 1 : 0;
                R(4, hy + 3, 9, 1, k.hat);
                P(12, hy + 3 + tilt, k.hat);
                R(6, hy + 4, 5, 2, k.hat);
                R(6, hy + 4, 5, 1, k.hatD);
                P(7, hy + 5, k.hatL);
            }
            else { R(6, hy + 3, 5, 1, k.skin); P(7, hy + 3, Mul(k.skin, 1.1f)); }

            // front arm
            switch (s.arm)
            {
                case 0: R(11, y0 + 1, 1, 3, k.jacketD); P(11, y0, k.skin); break;
                case 1: R(11, y0 + 2, 3, 1, k.jacketD); P(14, y0 + 2, k.skin); break;
                case 2: P(11, y0 + 3, k.jacketD); P(11, y0 + 4, k.jacketD); P(12, y0 + 5, k.jacketD); P(12, y0 + 6, k.skin); break;
                case 3: R(11, y0 + 2, 1, 4, k.jacketD); P(11, y0 + 6, k.skin); break;
                case 4: R(11, y0 + 1, 1, 3, k.jacketD); P(11, y0, k.skin); break;
                case 5: R(10, y0 + 3, 1, 4, k.jacketD); R(11, y0 + 3, 1, 4, k.jacketD); P(10, y0 + 7, k.skin); P(11, y0 + 7, k.skin); break;
                case 6: R(11, y0 + 3, 1, 2, k.jacketD); P(12, y0 + 5, k.skin); break;
                case 7: R(11, y0, 2, 1, k.jacketD); P(13, y0, k.skin); break;
            }
            p.Outline(k.outline);
            if (s.stun)
            {
                Color32 st = C(255, 230, 90);
                P(4, 16, st); P(9, 17, st); P(13, 16, st); P(8, 16, C(255, 255, 255));
            }
            return p;
        }

        static void PlayerArt()
        {
            const string d = "Sprites/Player/";
            var k = Hero;
            Vector2 pv = new Vector2(0.5f, 1f / 20f);
            Save(Humanoid(new Pose(), k), d + "idle_0", pv);
            Save(Humanoid(new Pose { bob = 0, scarf = 1 }, k), d + "idle_1", pv);
            int[] la = { -2, 0, 2, 0 }, lb = { 2, 0, -2, 0 }, liftA = { 0, 1, 0, 0 }, liftB = { 0, 0, 0, 1 }, bob = { 0, 1, 0, 1 };
            for (int i = 0; i < 4; i++)
                Save(Humanoid(new Pose { legA = la[i], legB = lb[i], liftA = liftA[i], liftB = liftB[i], bob = bob[i], arm = i % 2 == 0 ? 0 : 7, scarf = i % 2 }, k), d + "run_" + i, pv);
            Save(Humanoid(new Pose { legA = -1, legB = 1, liftA = 1, liftB = 2, arm = 6, scarf = 1 }, k), d + "jump_0", pv);
            Save(Humanoid(new Pose { legA = 0, legB = 0, arm = 6, scarf = 0 }, k), d + "fall_0", pv);
            Save(Humanoid(new Pose { crouch = true }, k), d + "crouch_0", pv);
            Save(Humanoid(new Pose { crouch = true, legA = -1, legB = 1, arm = 7 }, k), d + "crawl_0", pv);
            Save(Humanoid(new Pose { crouch = true, legA = 1, legB = -1, arm = 7, scarf = 1 }, k), d + "crawl_1", pv);
            Save(Humanoid(new Pose { arm = 3, liftA = 1 }, k), d + "climb_0", pv);
            Save(Humanoid(new Pose { arm = 4, liftB = 1, scarf = 1 }, k), d + "climb_1", pv);
            Save(Humanoid(new Pose { arm = 5, legA = 0, legB = 0, scarf = 1 }, k), d + "hang_0", pv);
            Save(Humanoid(new Pose { arm = 2 }, k), d + "whip_0", pv);
            Save(Humanoid(new Pose { arm = 1, legB = 1 }, k), d + "whip_1", pv);
            Save(Humanoid(new Pose { arm = 1 }, k), d + "throw_0", pv);
            Save(Humanoid(new Pose { stun = true, arm = 0 }, k), d + "stunned_0", pv);
            Save(Humanoid(new Pose { stun = true, arm = 0, scarf = 1, bob = 1 }, k), d + "stunned_1", pv);
            Save(Humanoid(new Pose { dead = true }, k), d + "dead_0", pv);
            Save(Humanoid(new Pose { lookUp = true }, k), d + "lookup_0", pv);

            // blade strokes (centre pivot, positioned by code; the files keep their old "whip_" names)
            Color32 st = C(206, 214, 230), stl = C(244, 248, 255), std = C(120, 128, 152), grip = C(120, 74, 40), guard = C(228, 180, 70);
            // back swing: the blade raised up and behind the shoulder
            var wb = new Px(16, 16);
            wb.Line(11, 4, 4, 13, st); wb.Line(12, 5, 5, 14, std); wb.Line(10, 4, 3, 13, stl); wb.Set(3, 14, stl);
            wb.Set(12, 3, guard); wb.Set(13, 4, guard); wb.Set(11, 2, guard); wb.Set(13, 2, grip); wb.Set(14, 1, grip);
            Save(wb, d + "whip_back", PCenter);
            // forward cut: the blade level in front, with the trail of the swing above it
            var wf = new Px(24, 8);
            wf.Rect(0, 3, 2, 2, grip); wf.Rect(2, 1, 1, 5, guard);
            wf.Rect(3, 3, 18, 1, st); wf.Rect(3, 4, 17, 1, stl); wf.Rect(3, 2, 16, 1, std);
            wf.Set(21, 3, stl); wf.Set(20, 4, stl); wf.Set(22, 3, stl);
            wf.Line(7, 6, 19, 6, C(255, 255, 255, 110)); wf.Line(11, 7, 17, 7, C(255, 255, 255, 60)); wf.Set(20, 5, C(255, 255, 255, 110));
            Save(wf, d + "whip_front", PCenter);

            // glider canopy (shown above the player while gliding)
            {
                var gl = new Px(26, 14);
                Color32 red = C(222, 72, 60), yel = C(250, 204, 84), ol = C(60, 24, 20), str = C(220, 210, 190);
                for (int x = 1; x < 25; x++)
                {
                    float t = (x - 12.5f) / 12f;
                    int yTop = 12 - (int)(t * t * 5f);
                    for (int y = yTop - 3; y <= yTop; y++) gl.Set(x, y, ((x / 4) % 2 == 0) ? red : yel);
                }
                gl.Outline(ol);
                gl.Line(2, 6, 12, 0, str); gl.Line(23, 6, 13, 0, str); gl.Line(12, 9, 12, 0, str);
                Save(gl, d + "glider", PCenter);
            }

            // shopkeeper
            const string sk = "Sprites/Shopkeeper/";
            Save(Humanoid(new Pose(), Keeper), sk + "idle_0", pv);
            Save(Humanoid(new Pose { bob = 1 }, Keeper), sk + "idle_1", pv);
            var angry = new Look();
            angry = Keeper;
            var a0 = Humanoid(new Pose { arm = 2, legA = -1, legB = 1 }, Keeper);
            var a1 = Humanoid(new Pose { arm = 1, legA = 1, legB = -1, bob = 1 }, Keeper);
            foreach (var a in new[] { a0, a1 }) { a.Set(10, 14, C(255, 40, 40)); a.Set(11, 14, C(255, 40, 40)); }
            Save(a0, sk + "angry_0", pv);
            Save(a1, sk + "angry_1", pv);
        }

        // ======================================================================================= ENEMIES

        static void EnemyArt()
        {
            Vector2 pv18 = new Vector2(0.5f, 1f / 18f);
            const string d = "Sprites/Enemies/";
            // Snake
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(18, 18);
                Color32 g = C(76, 164, 64), gd = C(44, 104, 40), bel = C(206, 212, 126), red = C(220, 50, 50);
                p.Ellipse(8, 3f, 6f, 2.2f, g);
                p.Rect(3, 1, 10, 1, bel);
                p.Rect(11, 3, 3, 4, g);
                p.Ellipse(14f, 8f, 2.8f, 2f, g);
                p.Set(15, 8, C(20, 20, 20)); p.Set(15, 9, C(255, 255, 200));
                p.Set(5, 4, gd); p.Set(8, 4, gd); p.Set(11, 4, gd); p.Set(12, 6, gd);
                if (f == 0) { p.Set(17, 7, red); p.Set(17, 8, red); }
                else { p.Set(17, 7, red); }
                p.Set(1, 2 + f, g); p.Set(2, 2, g);
                p.Outline(C(18, 46, 20));
                Save(p, d + $"snake_{f}", pv18);
            }
            // Bats (normal + ember)
            BatArt(d + "bat", C(112, 72, 112), C(78, 48, 88), C(255, 60, 60), pv18);
            BatArt(d + "emberbat", C(170, 70, 40), C(110, 40, 30), C(255, 220, 80), pv18);
            HawkArt(pv18);
            BatArt(d + "frostbat", C(150, 202, 238), C(92, 148, 204), C(255, 255, 255), pv18);
            FrostEnemyArt(pv18);
            SporeArt(pv18);
            GearworksArt(pv18);
            SandsunkArt();
            GeodeArt();
            BroodArt(pv18);
            TreeArt();
            // Spider
            {
                Color32 b = C(52, 40, 64), bl = C(98, 78, 120), eye = C(255, 60, 60), leg = C(36, 28, 44);
                for (int f = 0; f < 3; f++)
                {
                    var p = new Px(18, 18);
                    float by = f == 2 ? 6f : 5f;
                    for (int i = 0; i < 4; i++)
                    {
                        int dx = 2 + i;
                        if (f == 0) { p.Line(8, (int)by, 8 - dx - 1, (int)by + 2, leg); p.Line(8 - dx - 1, (int)by + 2, 8 - dx - 2, 0, leg); p.Line(9, (int)by, 9 + dx + 1, (int)by + 2, leg); p.Line(9 + dx + 1, (int)by + 2, 9 + dx + 2, 0, leg); }
                        else if (f == 1) { p.Line(8, (int)by, 8 - dx - 2, (int)by + 2 - i, leg); p.Line(9, (int)by, 9 + dx + 2, (int)by + 2 - i, leg); }
                        else { p.Line(8, (int)by, 7 - i, (int)by + 3, leg); p.Line(9, (int)by, 10 + i, (int)by + 3, leg); }
                    }
                    p.Ellipse(8.5f, by + 1f, 3.6f, 3f, b);
                    p.Set(7, (int)by + 3, bl); p.Set(8, (int)by + 3, bl);
                    p.Set(7, (int)by, eye); p.Set(10, (int)by, eye); p.Set(8, (int)by - 1, eye);
                    if (f == 2) for (int y = (int)by + 4; y < 18; y++) p.Set(8, y, C(220, 220, 230, 200));
                    p.Outline(C(14, 10, 18));
                    Save(p, d + $"spider_{f}", pv18);
                }
            }
            // Toad
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(18, 18);
                Color32 o = C(226, 104, 50), od = C(150, 52, 30), bel = C(252, 196, 112), eye = C(20, 20, 20);
                if (f == 0)
                {
                    p.Rect(2, 0, 3, 2, od); p.Rect(13, 0, 3, 2, od);
                    p.Ellipse(9, 4f, 6.5f, 3.6f, o);
                    p.Ellipse(9, 3f, 4f, 2f, bel);
                    p.Disc(6, 8, 1.6f, o); p.Disc(12, 8, 1.6f, o);
                    p.Set(6, 8, eye); p.Set(12, 8, eye);
                    p.Line(7, 5, 11, 5, od);
                    p.Set(4, 6, od); p.Set(14, 5, od);
                }
                else
                {
                    p.Line(4, 3, 2, 0, od); p.Line(13, 3, 15, 0, od);
                    p.Ellipse(9, 7f, 5.5f, 4f, o);
                    p.Ellipse(9, 6f, 3.5f, 2.4f, bel);
                    p.Disc(6, 11, 1.6f, o); p.Disc(12, 11, 1.6f, o);
                    p.Set(6, 11, eye); p.Set(12, 11, eye);
                    p.Rect(3, 7, 2, 1, od); p.Rect(14, 7, 2, 1, od);
                }
                p.Outline(C(60, 20, 14));
                Save(p, d + $"toad_{f}", pv18);
            }
            // Brute (24x24)
            {
                Vector2 pv = new Vector2(0.5f, 1f / 24f);
                Color32 fur = C(104, 86, 116), furD = C(70, 56, 82), furL = C(140, 120, 150), bel = C(156, 136, 140), snout = C(232, 150, 160), claw = C(240, 236, 220), eye = C(255, 220, 60);
                for (int f = 0; f < 4; f++)
                {
                    var p = new Px(24, 24);
                    int ox = 2, oy = 1;
                    int lA = f == 1 ? 1 : 0, lB = f == 0 ? 1 : 0;
                    p.Rect(ox + 4, oy + lA, 4, 3, furD); p.Rect(ox + 11, oy + lB, 4, 3, furD);
                    float hx = f == 2 ? 17f : 15f, hy = f == 2 ? 9f : 11f;
                    p.Ellipse(ox + 9.5f, oy + 7f, 7.5f, 5.5f, fur);
                    p.Ellipse(ox + 10f, oy + 6f, 4.5f, 3.5f, bel);
                    p.Ellipse(ox + hx, oy + hy, 4f, 3.4f, fur);
                    p.Ellipse(ox + hx + 3.5f, oy + hy - 1f, 1.6f, 1.2f, snout);
                    p.Set(ox + (int)hx + 1, oy + (int)hy + 1, f == 3 ? C(30, 30, 30) : eye);
                    if (f == 2) p.Line(ox + (int)hx - 1, oy + (int)hy + 3, ox + (int)hx + 2, oy + (int)hy + 2, furD);
                    p.Set(ox + 4, oy + 11, furL); p.Set(ox + 6, oy + 12, furL); p.Set(ox + 9, oy + 12, furL);
                    p.Rect(ox + 15, oy + 3, 3, 2, furD);
                    p.Set(ox + 18, oy + 3, claw); p.Set(ox + 18, oy + 4, claw); p.Set(ox + 17, oy + 2, claw);
                    p.Outline(C(28, 20, 34));
                    if (f == 3) { p.Set(ox + 12, oy + 17, C(255, 230, 90)); p.Set(ox + 16, oy + 18, C(255, 230, 90)); p.Set(ox + 19, oy + 16, C(255, 255, 255)); }
                    Save(p, d + $"brute_{f}", pv);
                }
            }
            // The Reaper (24x32): what comes when the player stays too long. A black hooded robe, a skull with red
            // eyes, a scythe over the shoulder. The files keep the old "wraith_" names. Art faces right.
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(24, 32);
                Color32 robe = C(26, 24, 36), fold = C(54, 50, 72), dark = C(8, 8, 14);
                Color32 bone = C(232, 228, 208), boneD = C(168, 160, 140), eye = C(255, 60, 50);
                Color32 wood = C(112, 76, 46), woodD = C(72, 48, 28), steel = C(222, 230, 242), steelD = C(132, 142, 166);
                // scythe first, the robe is drawn over its shaft
                p.Line(20, 2, 20, 30, wood); p.Line(21, 2, 21, 29, woodD);
                p.Line(20, 30, 15, 31, steel); p.Line(15, 31, 9, 29, steel); p.Line(9, 29, 5, 25, steel); p.Set(4, 24, steel);
                p.Line(19, 29, 15, 30, steelD); p.Line(15, 30, 10, 28, steelD); p.Line(10, 28, 6, 25, steelD);
                p.Line(20, 29, 16, 30, steel);
                // ragged hem: the two frames make it flutter
                for (int x = 6; x < 18; x++)
                {
                    int t = ((x + f * 2) % 4 < 2) ? 0 : 2;
                    for (int y = t; y < 4; y++) p.Set(x, y, robe);
                }
                // robe: wide at the bottom, narrow at the shoulders
                for (int y = 4; y < 19; y++)
                {
                    int hw = 6 - (y - 4) * 2 / 14;
                    p.Rect(12 - hw, y, hw * 2, 1, robe);
                }
                p.Line(9, 5, 10, 16, fold); p.Line(14, 4, 13, 15, fold); p.Line(16, 5, 15, 12, fold);
                // hood, the dark inside it, the skull
                p.Disc(12f, 23f, 5.4f, robe);
                p.Line(8, 26, 12, 28, fold); p.Line(12, 28, 16, 26, fold);
                p.Ellipse(12.5f, 22.5f, 3.2f, 3.4f, dark);
                p.Ellipse(12.5f, 22.6f, 2.4f, 2.6f, bone);
                p.Rect(11, 19, 3, 2, boneD); p.Set(12, 19, dark); p.Set(11, 20, dark); p.Set(13, 20, dark);
                p.Rect(10, 22, 2, 2, dark); p.Rect(13, 22, 2, 2, dark);
                p.Set(f == 0 ? 11 : 10, 22, eye); p.Set(f == 0 ? 14 : 13, 22, eye);
                p.Set(12, 21, dark);
                // the sleeve and the bony hand on the shaft
                p.Rect(15, 13, 4, 3, robe); p.Line(15, 15, 18, 15, fold);
                p.Set(19, 14, bone); p.Set(20, 14, bone); p.Set(19, 13, boneD); p.Set(20, 15, bone);
                // a pale rim so the black shape reads against a dark cave
                p.Outline(C(150, 156, 196, 230));
                Save(p, d + $"wraith_{f}", new Vector2(0.5f, 0f));
            }
        }

        /// <summary>Enemies of the frozen caves: penguin (stand, hop, belly slide) and yeti (idle, arm raised to throw).</summary>
        static void FrostEnemyArt(Vector2 pv)
        {
            const string d = "Sprites/Enemies/";
            Color32 navy = C(44, 54, 92), navyL = C(78, 94, 140), white = C(240, 246, 252), shade = C(190, 208, 230), orange = C(246, 160, 50);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                if (f < 2)
                {
                    int lean = f;                        // frame 1 leans forward, flippers up
                    p.Ellipse(9 + lean, 6.5f, 4f, 6f, navy);
                    p.Ellipse(10 + lean, 5.5f, 2.6f, 4.4f, white);
                    p.Set(8 + lean, 11, navyL); p.Set(7 + lean, 10, navyL);
                    if (f == 0) { p.Rect(4, 4, 1, 4, navy); p.Rect(13, 4, 1, 4, navy); }
                    else { p.Rect(4, 8, 2, 1, navy); p.Rect(3, 9, 2, 1, navy); p.Rect(14, 8, 2, 1, navy); p.Rect(15, 9, 2, 1, navy); }
                    p.Set(11 + lean, 10, C(20, 20, 30)); p.Set(11 + lean, 11, white);
                    p.Rect(13 + lean, 9, 2, 1, orange);
                    p.Rect(6, 0, 3, 1, orange); p.Rect(10, 0, 3, 1, orange);
                }
                else
                {
                    // belly slide, head first
                    p.Ellipse(8, 3.2f, 7f, 3f, navy);
                    p.Ellipse(8, 2.2f, 5.4f, 1.6f, white);
                    p.Disc(14f, 4.6f, 2.6f, navy);
                    p.Set(15, 5, C(20, 20, 30)); p.Set(15, 6, white);
                    p.Rect(16, 4, 2, 1, orange);
                    p.Rect(0, 2, 2, 1, orange); p.Rect(0, 4, 2, 1, orange);
                    p.Rect(5, 6, 4, 1, navyL);
                }
                p.Outline(C(18, 22, 44));
                Save(p, d + $"penguin_{f}", pv);
            }
            Color32 fur = C(232, 240, 250), furD = C(178, 198, 224), face = C(104, 134, 178), dark = C(26, 32, 56);
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(20, 20);
                p.Rect(5, 0, 4, 2, furD); p.Rect(11, 0, 4, 2, furD);
                p.Ellipse(10, 8f, 6f, 7.5f, fur);
                p.Ellipse(10, 4.5f, 5f, 3.5f, furD);
                p.Ellipse(10, 6.5f, 4.6f, 3.6f, fur);
                p.Rect(3, 4, 2, 6, fur); p.Rect(3, 4, 2, 1, furD);
                if (f == 0) { p.Rect(15, 4, 2, 6, fur); p.Rect(15, 4, 2, 1, furD); }
                else { p.Rect(15, 10, 2, 6, fur); p.Disc(16f, 17f, 1.8f, C(250, 252, 255)); }
                p.Ellipse(11, 11f, 3f, 2.4f, face);
                p.Set(10, 12, dark); p.Set(12, 12, dark);
                p.Rect(10, 10, 3, 1, dark); p.Set(10, 9, C(250, 250, 250)); p.Set(12, 9, C(250, 250, 250));
                p.Set(7, 15, furD); p.Set(13, 15, furD);
                p.Outline(C(40, 56, 92));
                Save(p, d + $"yeti_{f}", new Vector2(0.5f, 1f / 20f));
            }
        }

        /// <summary>The Broodmother's leavings: a sheet of web on the floor, and an egg sac (two frames: it throbs).</summary>
        static void BroodArt(Vector2 pv)
        {
            {
                var p = new Px(40, 8);
                Color32 w = C(236, 240, 246, 230), wd = C(190, 198, 214, 170);
                p.Rect(1, 0, 38, 1, wd);
                for (int x = 1; x < 39; x += 4) { p.Line(x, 0, x + 2, 5 + (x / 4) % 2, w); p.Line(x + 2, 5 + (x / 4) % 2, x + 4, 0, w); }
                for (int x = 3; x < 37; x += 6) p.Rect(x, 2, 4, 1, wd);
                p.Line(0, 0, 4, 7, w); p.Line(39, 0, 35, 7, w);
                Save(p, "Sprites/Props/web", new Vector2(0.5f, 0f));
            }
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(18, 18);
                Color32 sac = C(214, 224, 190), sacD = C(150, 166, 130), sacL = C(250, 255, 236), vein = C(110, 150, 90);
                float rx = f == 0 ? 4.6f : 5.4f, ry = f == 0 ? 5.6f : 5f;
                p.Ellipse(9f, ry, rx, ry, sac); p.Ellipse(9f, 2.2f, rx - 1f, 1.8f, sacD);
                p.Line(7, 3, 6, 8, vein); p.Line(11, 3, 12, 7, vein); p.Set(9, 9, sacL); p.Set(8, 8, sacL);
                p.Set(9, 5, C(40, 30, 50)); p.Set(10, 6, C(40, 30, 50));          // something moving inside
                p.Outline(C(40, 50, 34));
                Save(p, $"Sprites/Enemies/eggsac_{f}", pv);
            }
            {
                // a cocoon on its thread: three tiles tall, the cocoon itself is the lower part
                var p = new Px(16, 48);
                Color32 silk = C(222, 226, 204), silkD = C(160, 170, 140), silkL = C(250, 252, 240), thread = C(200, 206, 186), ol = C(40, 50, 34);
                p.Ellipse(8f, 9.5f, 5.4f, 9f, silk);
                p.Ellipse(6.4f, 11f, 2f, 5f, silkL);
                for (int y = 3; y < 18; y += 3) p.Line(3, y, 12, y + 2, silkD);          // the winding of the silk
                p.Set(9, 8, C(40, 30, 50)); p.Set(10, 11, C(40, 30, 50));              // something moving inside
                p.Outline(ol);
                p.Rect(7, 19, 2, 2, silkD);
                p.Rect(8, 21, 1, 27, thread); p.Rect(7, 21, 1, 27, ol);
                Save(p, "Sprites/Props/cocoon", new Vector2(0.5f, 0f));
            }
        }

        /// <summary>
        /// Gearworks: the conveyor belt, its three machines (sentry, drone, scrap beetle), their bolt, and the
        /// Foreman's gear.
        /// </summary>
        static void GearworksArt(Vector2 pv)
        {
            Color32 ol = C(20, 18, 22);
            Color32 steel = C(150, 156, 170), steelD = C(92, 98, 114), steelL = C(214, 220, 232);
            Color32 brass = C(206, 160, 74), brassD = C(138, 100, 42), brassL = C(250, 220, 130);
            // conveyor belt: a low band on the floor; the ridges shift from frame to frame
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(16, 16);
                Color32 belt = C(54, 52, 60), beltL = C(92, 90, 104), ridge = C(140, 138, 152);
                p.Rect(0, 0, 16, 4, belt); p.Rect(0, 3, 16, 1, beltL); p.Rect(0, 0, 16, 1, C(30, 28, 34));
                for (int x = f * 2 - 6; x < 16; x += 6) { p.Rect(x, 1, 2, 2, ridge); p.Set(x + 2, 2, ridge); }
                p.Set(0, 4, brassD); p.Set(15, 4, brassD);
                Save(p, $"Sprites/Props/conveyor_{f}", new Vector2(0.5f, 0f));
            }
            // sentry: a wind-up guard, a tall shield in front. 0 / 1 walk, 2 jab (spear out)
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                int step = f == 1 ? 1 : 0;
                p.Rect(4, 0, 2, 2 - step, steelD); p.Rect(8, 0, 2, 1 + step, steelD);              // feet
                p.Rect(3, 2, 8, 7, steel); p.Rect(3, 2, 2, 7, steelD);                              // body
                p.Disc(7f, 11.5f, 3.2f, brass); p.Rect(5, 11, 5, 1, C(30, 26, 30)); p.Set(8, 11, C(255, 90, 60));   // head, visor slit, eye
                p.Set(6, 15, brassL); p.Set(7, 15, brassL);
                p.Rect(0, 5, 3, 1, brassD); p.Rect(0, 4 + (f == 1 ? 2 : 0), 1, 3, brassD);            // wind-up key on its back
                if (f == 2) { p.Line(10, 7, 17, 7, steelL); p.Set(17, 8, steelL); p.Set(17, 6, steelL); }   // spear thrust
                // the shield
                p.Rect(11, 1, 4, 12, brass); p.Rect(11, 1, 1, 12, brassD); p.Rect(14, 1, 1, 12, brassL);
                p.Rect(12, 6, 2, 2, steelL); p.Set(12, 11, brassD); p.Set(13, 3, brassL);
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/sentry_{f}", pv);
            }
            // drone: 0 / 1 hovering (rotor turns), 2 aiming (red eye)
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                Color32 eye = f == 2 ? C(255, 70, 50) : C(110, 230, 220), eyeL = f == 2 ? C(255, 200, 170) : C(220, 255, 250);
                p.Ellipse(9f, 7f, 5f, 4f, steel); p.Ellipse(9f, 5.5f, 4f, 2f, steelD);
                p.Disc(9f, 7.5f, 2.2f, eye); p.Set(9, 8, eyeL);
                p.Rect(8, 11, 2, 2, steelD);
                if (f == 1) p.Rect(5, 13, 8, 1, steelL); else p.Rect(2, 13, 14, 1, steelL);
                p.Set(5, 3, brass); p.Set(12, 3, brass); p.Rect(8, 2, 2, 1, brassD);
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/drone_{f}", pv);
            }
            // scrap beetle: 0 / 1 scuttling, 2 with a lit bomb inside (glowing)
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                Color32 shell = f == 2 ? C(236, 96, 60) : C(120, 112, 96), shellD = f == 2 ? C(160, 50, 36) : C(78, 72, 62), shellL = f == 2 ? C(255, 220, 150) : C(176, 168, 148);
                int leg = f == 1 ? 1 : 0;
                for (int i = 0; i < 3; i++) p.Set(4 + i * 4, (i + leg) % 2, shellD);
                p.Ellipse(8.5f, 3.5f, 6.5f, 3f, shell); p.Ellipse(8.5f, 2.5f, 5.5f, 1.6f, shellD);
                p.Line(8, 2, 8, 6, shellD);
                p.Set(5, 5, shellL); p.Set(11, 5, shellL);
                p.Rect(14, 3, 2, 2, shellD); p.Set(16, 4, C(250, 240, 200)); p.Set(16, 2, shellD); p.Set(17, 5, shellD);   // head and jaws
                p.Outline(ol);
                Save(p, $"Sprites/Enemies/beetle_{f}", pv);
            }
            {
                // saw blade: a toothed disc (it spins at runtime), and the rail it runs on
                var p = new Px(14, 14);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI / 5f;
                    p.Set(Mathf.RoundToInt(6.5f + Mathf.Cos(a) * 6f), Mathf.RoundToInt(6.5f + Mathf.Sin(a) * 6f), steelL);
                    p.Set(Mathf.RoundToInt(6.5f + Mathf.Cos(a + 0.2f) * 5.2f), Mathf.RoundToInt(6.5f + Mathf.Sin(a + 0.2f) * 5.2f), steel);
                }
                p.Disc(6.5f, 6.5f, 4.6f, steel); p.Disc(6.5f, 6.5f, 3f, steelD); p.Disc(6.5f, 6.5f, 1.2f, brass);
                p.Set(4, 9, steelL); p.Set(5, 10, steelL); p.Set(9, 4, C(70, 74, 86));
                p.Outline(ol);
                Save(p, "Sprites/Traps/saw", PCenter);
                var r = new Px(16, 3);
                r.Rect(0, 0, 16, 1, C(40, 38, 44)); r.Rect(0, 1, 16, 1, steelD); r.Rect(0, 2, 16, 1, C(60, 62, 72));
                r.Set(3, 1, brassD); r.Set(11, 1, brassD);
                Save(r, "Sprites/Traps/sawrail", new Vector2(0.5f, 0f));
            }
            {
                // the drone's bolt
                var p = new Px(8, 8);
                p.Disc(3.5f, 3.5f, 2.6f, C(255, 120, 60)); p.Disc(3.5f, 3.5f, 1.3f, C(255, 240, 190));
                Save(p, "Sprites/Bosses/bolt", PCenter);
            }
            {
                // the Foreman's thrown gear
                var p = new Px(12, 12);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    p.Rect(Mathf.RoundToInt(5.5f + Mathf.Cos(a) * 4.6f), Mathf.RoundToInt(5.5f + Mathf.Sin(a) * 4.6f), 1, 1, brassD);
                }
                p.Disc(5.5f, 5.5f, 4f, brass); p.Disc(5.5f, 5.5f, 1.6f, C(40, 34, 30)); p.Set(4, 8, brassL); p.Set(5, 8, brassL);
                p.Outline(ol);
                Save(p, "Sprites/Bosses/gear", PCenter);
            }
        }

        /// <summary>Upper half of an ellipse standing on row 'baseY': a mushroom cap.</summary>
        static void Cap(Px p, float cx, int baseY, float rx, float ry, Color32 col)
        {
            for (int y = baseY; y <= baseY + Mathf.CeilToInt(ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float u = (x + 0.5f - cx) / rx, v = (y + 0.5f - baseY) / ry;
                    if (u * u + v * v <= 1f) p.Set(x, y, col);
                }
        }

        /// <summary>One small glowing mushroom: pale stem, teal cap with a bright rim and two light spots.</summary>
        static void SmallShroom(Px p, int x, int y0, int stemH, float capR, Color32 stem, Color32 cap, Color32 capL, Color32 capD)
        {
            p.Rect(x, y0, 2, stemH, stem);
            Cap(p, x + 1f, y0 + stemH, capR, capR * 0.8f, cap);
            Cap(p, x + 1f, y0 + stemH, capR, 1f, capD);
            p.Set(x, y0 + stemH + Mathf.Max(1, (int)(capR * 0.5f)), capL);
            if (capR >= 3f) p.Set(x + 2, y0 + stemH + 1, capL);
        }

        /// <summary>
        /// Sporegloom: the glowing plants (decor mushrooms, the bounce cap), its three creatures (puffshroom, gloom
        /// moth, lurecap) and the spore cloud.
        /// </summary>
        static void SporeArt(Vector2 pv)
        {
            Color32 stem = C(206, 196, 214), stemD = C(150, 138, 168);
            Color32 teal = C(70, 214, 186), tealL = C(190, 255, 236), tealD = C(34, 132, 128);
            Color32 ol = C(20, 16, 34);
            // decor: three little clusters. Variant 0 is also what a sleeping lurecap looks like.
            for (int v = 0; v < 3; v++)
            {
                var p = new Px(16, 16);
                if (v == 0) { SmallShroom(p, 6, 0, 4, 4.2f, stem, teal, tealL, tealD); SmallShroom(p, 12, 0, 2, 2.2f, stem, teal, tealL, tealD); }
                else if (v == 1) { SmallShroom(p, 3, 0, 3, 2.6f, stem, teal, tealL, tealD); SmallShroom(p, 9, 0, 6, 3.4f, stem, teal, tealL, tealD); }
                else { SmallShroom(p, 2, 0, 2, 2.2f, stem, teal, tealL, tealD); SmallShroom(p, 7, 0, 3, 2.6f, stem, teal, tealL, tealD); SmallShroom(p, 12, 0, 5, 2.4f, stem, teal, tealL, tealD); }
                p.Outline(ol);
                Save(p, $"Sprites/Props/glowshroom_{v}", new Vector2(0.5f, 0f));
            }
            // bounce cap: a wide, low trampoline of a mushroom (rest, squashed)
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(26, 14);
                int stemH = f == 0 ? 4 : 2;
                float ry = f == 0 ? 6f : 4f, rx = f == 0 ? 11f : 12.4f;
                p.Rect(10, 0, 6, stemH, stem); p.Rect(14, 0, 2, stemH, stemD);
                Cap(p, 13f, stemH, rx, ry, teal);
                Cap(p, 13f, stemH, rx, 1.2f, tealD);
                for (int i = 0; i < 4; i++) p.Rect(5 + i * 5, stemH + 2 + (i % 2), 2, 1, tealL);
                p.Rect(9, stemH + (int)ry - 2, 8, 1, tealL);
                p.Outline(ol);
                Save(p, $"Sprites/Props/bouncecap_{f}", new Vector2(0.5f, 0f));
            }
            // puffshroom: 0/1 walk, 2 swollen, 3 deflated
            {
                Color32 cap = C(168, 92, 196), capL = C(226, 168, 240), capD = C(104, 52, 130), spot = C(214, 255, 170), eye = C(24, 20, 30);
                for (int f = 0; f < 4; f++)
                {
                    var p = new Px(18, 18);
                    int lift = f == 1 ? 1 : 0;
                    float rx = f == 2 ? 8f : f == 3 ? 5.5f : 6.5f, ry = f == 2 ? 9f : f == 3 ? 3.5f : 6.5f;
                    int bodyH = f == 3 ? 4 : 5;
                    p.Rect(5, 0, 2, 2 - lift, stemD); p.Rect(11, 0, 2, 1 + lift, stemD);          // feet
                    p.Rect(5, 1, 8, bodyH, stem); p.Rect(11, 1, 2, bodyH, stemD);
                    p.Set(7, 3, eye); p.Set(10, 3, eye);
                    if (f == 2) p.Rect(8, 2, 2, 1, eye);                                            // cheeks puffed, mouth tight
                    Cap(p, 9f, 1 + bodyH, rx, ry, cap);
                    Cap(p, 9f, 1 + bodyH, rx, 1.2f, capD);
                    p.Set(6, 2 + bodyH + (int)(ry * 0.4f), spot); p.Set(11, 2 + bodyH + (int)(ry * 0.3f), spot); p.Set(9, bodyH + (int)ry - 1, capL);
                    if (f == 2) { p.Set(4, 4 + bodyH, spot); p.Set(13, 5 + bodyH, spot); p.Set(8, 5 + bodyH, spot); }
                    p.Outline(ol);
                    if (f == 3) { p.Set(3, 12, spot); p.Set(14, 13, spot); p.Set(9, 15, spot); }   // the last spores hanging in the air
                    Save(p, $"Sprites/Enemies/puffshroom_{f}", pv);
                }
            }
            // gloom moth: 0/1 flying (wings up / down), 2 hanging with folded wings
            {
                Color32 wing = C(112, 96, 150), wingL = C(170, 156, 206), wingD = C(70, 58, 100), body = C(58, 46, 70), glowC = C(255, 236, 150);
                for (int f = 0; f < 3; f++)
                {
                    var p = new Px(18, 18);
                    if (f < 2)
                    {
                        int wy = f == 0 ? 9 : 6;
                        p.Ellipse(5f, wy + 1f, 4f, f == 0 ? 3.4f : 2.4f, wing); p.Ellipse(13f, wy + 1f, 4f, f == 0 ? 3.4f : 2.4f, wing);
                        p.Ellipse(5.5f, wy - 2f, 2.6f, 1.8f, wingD); p.Ellipse(12.5f, wy - 2f, 2.6f, 1.8f, wingD);
                        p.Set(4, wy + 1, wingL); p.Set(5, wy + 2, wingL); p.Set(13, wy + 1, wingL); p.Set(12, wy + 2, wingL);
                        p.Set(4, wy, glowC); p.Set(13, wy, glowC);                                 // eye spots on the wings
                        p.Rect(8, 4, 2, 7, body);
                        p.Set(8, 11, glowC); p.Set(9, 11, glowC);                                  // head lit by what it drank
                        p.Set(7, 12, body); p.Set(10, 12, body); p.Set(6, 13, body); p.Set(11, 13, body);   // feelers
                    }
                    else
                    {
                        p.Rect(8, 8, 2, 9, body);
                        p.Ellipse(9f, 11f, 3.2f, 5f, wing);
                        p.Line(9, 7, 9, 15, wingD);
                        p.Set(7, 11, wingL); p.Set(11, 11, wingL);
                        p.Set(8, 6, glowC); p.Set(9, 6, glowC);
                    }
                    p.Outline(ol);
                    Save(p, $"Sprites/Enemies/moth_{f}", pv);
                }
            }
            // lurecap: 0 asleep (the decor cluster), 1 awake on its feet, 2 leaping
            {
                Color32 red = C(255, 90, 80), tooth = C(250, 250, 240), eye = C(24, 20, 30);
                for (int f = 0; f < 3; f++)
                {
                    var p = new Px(18, 18);
                    if (f == 0)
                    {
                        SmallShroom(p, 7, 1, 4, 4.2f, stem, teal, tealL, tealD); SmallShroom(p, 13, 1, 2, 2.2f, stem, teal, tealL, tealD);
                    }
                    else
                    {
                        int up = f == 2 ? 3 : 0;
                        if (f == 1) { p.Rect(5, 0, 2, 2, stemD); p.Rect(11, 0, 2, 2, stemD); }
                        else { p.Line(5, 2, 3, 0, stemD); p.Line(12, 2, 14, 0, stemD); }
                        p.Rect(5, 2 + up, 8, 5, stem);
                        p.Rect(6, 3 + up, 6, 2, eye);                                               // a wide mouth
                        p.Set(6, 4 + up, tooth); p.Set(8, 4 + up, tooth); p.Set(10, 4 + up, tooth); p.Set(7, 3 + up, tooth); p.Set(9, 3 + up, tooth); p.Set(11, 3 + up, tooth);
                        Cap(p, 9f, 7 + up, 6.5f, 5.6f, teal);
                        Cap(p, 9f, 7 + up, 6.5f, 1.2f, tealD);
                        p.Set(6, 8 + up, red); p.Set(7, 8 + up, red); p.Set(10, 8 + up, red); p.Set(11, 8 + up, red);   // eyes under the brim
                        p.Set(8, 11 + up, tealL); p.Set(12, 9 + up, tealL);
                    }
                    p.Outline(ol);
                    Save(p, $"Sprites/Enemies/lurecap_{f}", pv);
                }
            }
            // spore cloud: a loose puff of dots, white - tinted and faded at runtime
            {
                var p = new Px(40, 36);
                Color32 w = C(255, 255, 255, 150), wl = C(255, 255, 255, 230), wd = C(255, 255, 255, 80);
                p.Disc(20, 18, 11f, wd); p.Disc(12, 15, 7.5f, wd); p.Disc(28, 16, 8f, wd); p.Disc(19, 25, 7.5f, wd); p.Disc(22, 10, 7f, wd);
                p.Disc(20, 18, 7f, w); p.Disc(13, 15, 4f, w); p.Disc(27, 17, 4.5f, w); p.Disc(19, 24, 4f, w);
                for (int i = 0; i < 46; i++)
                {
                    int x = 3 + (int)(N(i, 7, 91) * 34f), y = 3 + (int)(N(i, 13, 57) * 30f);
                    p.Set(x, y, wl);
                }
                Save(p, "Center/sporecloud", PCenter);
            }
        }

        static Px Scale(Px s, int n)
        {
            var p = new Px(s.w * n, s.h * n);
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++) p.c[y * p.w + x] = s.c[(y / n) * s.w + x / n];
            return p;
        }

        /// <summary>
        /// The Broodmother, drawn for herself (4 tiles by 2.9; she faces right): a swollen abdomen with a red
        /// hourglass, a plated chest, a head full of eyes, eight jointed legs arching over her back.
        /// 0 standing, 1 crouched to strike (jaws apart, eyes ablaze), 2 falling (legs trailing above her),
        /// 3-6 crawling: her legs step in two sets of four, each set forward while the other pushes back.
        /// </summary>
        static void BroodmotherArt()
        {
            Color32 d = C(46, 20, 68), m = C(88, 42, 124), l = C(134, 74, 172), h = C(186, 128, 218);
            Color32 legD = C(58, 26, 84), legM = C(104, 52, 140), legL = C(160, 100, 196);
            Color32 farD = C(36, 16, 54), farM = C(62, 30, 90);
            Color32 ol = C(22, 9, 34), red = C(232, 52, 84), redL = C(255, 140, 150), fang = C(238, 228, 200);

            // a round body lit from the upper right, in four flat tones
            void Blob(Px p, float cx, float cy, float rx, float ry)
            {
                for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                    for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                    {
                        float nx = (x - cx) / rx, ny = (y - cy) / ry;
                        if (nx * nx + ny * ny > 1f) continue;
                        float v = nx * 0.45f + ny * 0.75f;
                        p.Set(x, y, v > 0.55f ? h : v > 0.12f ? l : v > -0.38f ? m : d);
                    }
            }
            // one segment of a leg, two pixels thick, lit along its upper side
            void Seg(Px p, int x0, int y0, int x1, int y1, Color32 body, Color32 lit)
            {
                p.Line(x0, y0, x1, y1, body); p.Line(x0 + 1, y0, x1 + 1, y1, body); p.Line(x0, y0 - 1, x1, y1 - 1, body);
                p.Line(x0, y0 + 1, x1, y1 + 1, lit);
            }
            void Leg(Px p, int hx, int hy, int kx, int ky, int fx, int fy, bool near)
            {
                Seg(p, hx, hy, kx, ky, near ? legM : farM, near ? legL : farM);
                Seg(p, kx, ky, fx, fy, near ? legD : farD, near ? legM : farD);
                p.Disc(kx + 0.5f, ky, 1.6f, near ? legL : farM);      // the knee
            }

            for (int f = 0; f < 7; f++)
            {
                var p = new Px(64, 46);
                bool crawl = f >= 3;
                int by = f == 1 ? -4 : f == 2 ? -6 : crawl && f % 2 == 0 ? -1 : 0;     // how far the body hangs down between the legs
                // (hip x, knee x, knee y, foot x) of the four legs on one side, front to back
                int[,] stand = { { 46, 56, 32, 62 }, { 43, 50, 37, 54 }, { 39, 31, 39, 22 }, { 37, 18, 41, 5 } };
                int[,] fall = { { 46, 57, 27, 62 }, { 43, 51, 31, 54 }, { 39, 30, 31, 21 }, { 37, 14, 29, 6 } };
                for (int pass = 0; pass < 2; pass++)
                {
                    bool near = pass == 1;
                    if (near)
                    {
                        // the body goes between the far legs and the near ones
                        Blob(p, 21, 22 + by, 15, 12);
                        // plates across the abdomen, and the hourglass
                        for (int i = 0; i < 3; i++)
                            for (int y = 12 + by; y <= 33 + by; y++)
                            {
                                int x = 10 + i * 7 + Mathf.RoundToInt(Mathf.Abs(y - 22 - by) * 0.25f);
                                if (p.Opaque(x, y)) p.Set(x, y, d);
                            }
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            int half = Mathf.RoundToInt(Mathf.Abs(dy) * 0.7f);
                            for (int dx = -half; dx <= half; dx++) p.Set(22 + dx, 23 + by + dy, dy > 0 && dx >= 0 ? redL : red);
                        }
                        p.Rect(5, 20 + by, 2, 3, d);                                   // spinneret
                        Blob(p, 41, 17 + by, 9, 8);
                        Blob(p, 49, 16 + by, 6, 6);
                        p.Line(36, 22 + by, 44, 24 + by, d);                            // the seam of her chest plate
                        // jaws: shut, or apart with venom on them
                        if (f == 1) { p.Line(50, 10 + by, 48, 5 + by, fang); p.Line(54, 11 + by, 57, 6 + by, fang); p.Set(48, 4 + by, C(150, 230, 90)); p.Set(57, 5 + by, C(150, 230, 90)); }
                        else { p.Line(50, 10 + by, 51, 6 + by, fang); p.Line(53, 11 + by, 54, 7 + by, fang); }
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        int hx, kx, ky, fx, fy;
                        if (f == 2) { hx = fall[i, 0]; kx = fall[i, 1]; ky = fall[i, 2]; fx = fall[i, 3]; fy = 44; }
                        else { hx = stand[i, 0]; kx = stand[i, 1]; ky = stand[i, 2] + (f == 1 ? 2 : 0); fx = stand[i, 3]; fy = 1; }
                        if (!near) { int back = i < 2 ? -4 : 4; kx += back; ky -= 5; fx += back; if (f == 2) fy -= 3; }
                        if (crawl)
                        {
                            // a step: the foot swings forward off the ground, comes down, and is drawn back along it
                            float t = (f - 3) / 4f + ((i % 2 == 0) == near ? 0f : 0.5f);
                            float swing = Mathf.Cos(t * Mathf.PI * 2f), lift = Mathf.Sin(t * Mathf.PI * 2f);
                            fx += Mathf.RoundToInt(swing * 3f); kx += Mathf.RoundToInt(swing * 1.5f);
                            if (lift > 0.3f) { fy += 3; ky += 1; }
                        }
                        Leg(p, hx, 18 + by, kx, ky, Mathf.Clamp(fx, 1, 62), fy, near);
                    }
                }
                p.Outline(ol);
                // eyes: two big ones and a scatter of small ones (ablaze when she strikes), bristles on her back
                Color32 eye = f == 1 ? C(255, 210, 80) : red, glint = C(255, 255, 255);
                p.Rect(49, 17 + by, 3, 3, eye); p.Set(51, 19 + by, glint);
                p.Rect(53, 15 + by, 2, 2, eye); p.Set(54, 16 + by, glint);
                p.Set(47, 20 + by, eye); p.Set(51, 13 + by, eye); p.Set(46, 16 + by, eye);
                p.Line(12, 32 + by, 10, 35 + by, ol); p.Line(19, 34 + by, 18, 37 + by, ol); p.Line(27, 33 + by, 28, 36 + by, ol);
                Save(p, $"Sprites/Bosses/broodmother_{f}", new Vector2(0.5f, 1f / 46f));
            }
        }

        /// <summary>
        /// The bosses (each drawn for itself: see BroodmotherArt and ArtGenerator.Bosses.cs), what they throw, and
        /// the treasure chest.
        /// </summary>
        static void BossArt()
        {
            const string o = "Sprites/Bosses/";
            BroodmotherArt();
            OldBossesArt();      // the Forge Brute, the Yeti King, the Storm Hawk, the Rot Heart, the Hollow Warden, the Cogwyrm
            {
                // the Yeti King's snow boulder (it grows and turns at runtime)
                var p = new Px(20, 20);
                p.Disc(9.5f, 9.5f, 9f, C(236, 244, 252)); p.Disc(8f, 11f, 6f, C(255, 255, 255)); p.Disc(12f, 7f, 4f, C(196, 214, 236));
                p.Set(5, 6, C(176, 198, 226)); p.Set(13, 13, C(176, 198, 226)); p.Set(9, 15, C(210, 226, 244)); p.Set(15, 9, C(160, 184, 216));
                p.Outline(C(70, 96, 140));
                Save(p, o + "snowboulder", PCenter);
            }
            {
                // the Rot Heart's tendril: a thick thorny whip, tip to the right
                var p = new Px(28, 8);
                Color32 t = C(96, 60, 120), tl = C(160, 110, 190), tv = C(70, 214, 186);
                for (int x = 0; x < 28; x++) { int w = x < 20 ? 4 : 4 - (x - 20) / 2; int y = 2 + (int)Mathf.Round(Mathf.Sin(x * 0.55f)); if (w > 0) p.Rect(x, y, 1, w, t); }
                for (int x = 2; x < 24; x += 5) { p.Set(x, 7, tv); p.Set(x + 2, 0, tv); p.Set(x + 1, 4, tl); }
                p.Outline(C(22, 14, 34));
                Save(p, o + "tendril", PCenter);
            }
            {
                // "no damage" badge (the [flawless] token in UI texts): a whole golden heart behind a little shield rim
                var p = new Px(16, 16);
                Color32 gold = C(255, 210, 80), goldL = C(255, 244, 170), goldD = C(196, 140, 40), red = C(236, 60, 84), redL = C(255, 150, 170);
                p.Disc(5.5f, 10f, 3.6f, gold); p.Disc(10.5f, 10f, 3.6f, gold);
                for (int y = 2; y <= 9; y++) { int hw = (y - 2) + 1; if (hw > 7) hw = 7; p.Rect(8 - hw, y, hw * 2, 1, gold); }
                p.Disc(5.5f, 10f, 2.2f, red); p.Disc(10.5f, 10f, 2.2f, red);
                for (int y = 4; y <= 9; y++) { int hw = (y - 4) + 1; if (hw > 5) hw = 5; p.Rect(8 - hw, y, hw * 2, 1, red); }
                p.Set(5, 11, redL); p.Set(4, 10, redL); p.Set(3, 12, goldL); p.Set(12, 12, goldL); p.Set(8, 2, goldD); p.Set(7, 2, goldD);
                p.Outline(C(60, 34, 14));
                Save(p, "UI/Glyphs/flawless", PCenter);
            }
            {
                // the "up / down chooses" mark (the [updown] token): a little direction cross, its up and down arms lit
                var p = new Px(16, 16);
                Color32 body = C(64, 60, 84), bodyD = C(40, 38, 56), lit = C(236, 232, 244), dim = C(110, 106, 132);
                p.Rect(6, 1, 4, 14, body); p.Rect(1, 6, 14, 4, body);              // the cross
                p.Rect(6, 1, 4, 1, bodyD); p.Rect(1, 6, 1, 4, bodyD); p.Rect(14, 6, 1, 4, bodyD); p.Rect(6, 14, 4, 1, bodyD);
                p.Rect(7, 11, 2, 1, lit); p.Rect(6, 10, 4, 1, lit); p.Set(7, 12, lit); p.Set(8, 12, lit);                 // up arrow
                p.Rect(7, 4, 2, 1, lit); p.Rect(6, 5, 4, 1, lit); p.Set(7, 3, lit); p.Set(8, 3, lit);                    // down arrow
                p.Set(3, 7, dim); p.Set(3, 8, dim); p.Set(12, 7, dim); p.Set(12, 8, dim);                                // left / right: not used here
                p.Outline(C(18, 16, 28));
                Save(p, "UI/Glyphs/updown", PCenter);
            }
            {
                var p = new Px(9, 9);
                p.Disc(4f, 4f, 3.4f, C(150, 90, 230)); p.Disc(4f, 4f, 2f, C(214, 170, 255)); p.Set(4, 4, C(255, 255, 255));
                Save(p, o + "orb", PCenter);
            }
            // venom glob (Broodmother's spit): two frames, the drip trails behind (art points right)
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(12, 9);
                Color32 v = C(110, 200, 60), vl = C(190, 245, 120), vd = C(50, 120, 40);
                p.Disc(7.5f, 4f, 3.3f, v); p.Disc(8.3f, 4.6f, 1.5f, vl); p.Set(9, 5, C(240, 255, 210));
                p.Set(7, 1, vd); p.Set(8, 1, vd); p.Set(6, 1, vd);
                if (f == 0) { p.Rect(2, 4, 3, 1, v); p.Set(1, 4, vd); p.Set(3, 6, vd); }
                else { p.Rect(3, 3, 2, 1, v); p.Set(1, 3, vd); p.Set(2, 5, v); p.Set(0, 5, vd); }
                p.Outline(C(24, 50, 22));
                Save(p, o + "venom_" + f, PCenter);
            }
            // feather (Storm Hawk): a quill flying point first (art points right)
            {
                var p = new Px(14, 7);
                Color32 w = C(236, 244, 255), b = C(150, 186, 236), d = C(84, 112, 170);
                p.Line(1, 3, 12, 3, d);                                  // the shaft
                p.Line(3, 4, 10, 4, w); p.Line(4, 5, 8, 5, b); p.Line(3, 2, 10, 2, w); p.Line(4, 1, 8, 1, b);
                p.Set(11, 4, b); p.Set(11, 2, b); p.Set(13, 3, w); p.Set(0, 3, d);
                p.Outline(C(30, 40, 70));
                Save(p, o + "feather", PCenter);
            }
            // shockwave (slams): a crest of dust and force running along the floor (art runs right; drawn pale so the
            // boss can tint it - green for venom, orange for embers). Two frames; bottom pivot, it sits on the ground.
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(16, 12);
                Color32 w = C(255, 255, 255), g1 = C(226, 226, 232), g2 = C(170, 170, 184);
                // the leading crest: a tall curved wall on the right
                p.Line(12, 0, 13, 4, w); p.Line(13, 4, 12, 8, w); p.Line(12, 8, 10, 10 + f, w);
                p.Line(11, 0, 12, 4, g1); p.Line(12, 4, 11, 7, g1); p.Line(11, 7, 9, 9, g1);
                p.Line(14, 0, 14, 3, g1);
                // what it kicks up behind
                p.Rect(6, 0, 5, 2, g1); p.Rect(2, 0, 4, 1, g2); p.Rect(8, 2, 3, 2 + f, g2);
                p.Set(7, 4 + f, g1); p.Set(5, 3 - f, g2); p.Set(3, 2 + f, g2); p.Set(9, 6, g2); p.Set(1, 1, g2);
                p.Outline(C(60, 60, 70));
                Save(p, o + "shock_" + f, PItem);
            }
            {
                // rock dropper: a carved stone mouth in the ceiling
                var p = new Px(16, 16);
                Color32 st = C(126, 118, 112), stD = C(82, 76, 74), stL = C(170, 162, 152), hole = C(16, 12, 16);
                p.Rect(1, 4, 14, 12, st); p.Rect(1, 14, 14, 2, stL); p.Rect(1, 4, 1, 12, stL); p.Rect(14, 4, 1, 12, stD);
                p.Rect(4, 4, 8, 5, hole); p.Rect(5, 9, 6, 1, hole);
                p.Rect(4, 9, 1, 2, stD); p.Rect(11, 9, 1, 2, stD);                      // teeth
                p.Rect(3, 11, 3, 2, hole); p.Rect(10, 11, 3, 2, hole);                  // eyes
                p.Set(4, 12, C(255, 150, 60)); p.Set(11, 12, C(255, 150, 60));
                p.Outline(C(30, 26, 30));
                Save(p, "Sprites/Traps/dropper", new Vector2(0.5f, 0f));
            }
            {
                // crumbling platform: rotten, cracked planks drawn over the platform tile
                var p = new Px(16, 16);
                Color32 w = C(132, 92, 54), wD = C(84, 54, 30), wL = C(170, 128, 80), crack = C(40, 24, 14);
                p.Rect(0, 11, 16, 5, w); p.Rect(0, 15, 16, 1, wL); p.Rect(0, 11, 16, 1, wD);
                p.Line(3, 15, 5, 12, crack); p.Line(5, 12, 4, 11, crack); p.Line(10, 15, 9, 13, crack); p.Line(9, 13, 11, 11, crack);
                p.Set(7, 14, crack); p.Set(13, 13, crack); p.Set(1, 12, crack);
                p.Rect(6, 10, 2, 1, wD); p.Rect(12, 10, 2, 1, wD);                       // splinters hanging
                Save(p, "Sprites/Traps/crumble", new Vector2(0.5f, 0f));
            }
            for (int f = 0; f < 2; f++)
            {
                // treasure chest: closed / open
                var p = new Px(18, 16);
                Color32 wood = C(150, 96, 50), woodD = C(100, 62, 32), woodL = C(190, 132, 74), gold = C(246, 204, 80), goldD = C(176, 126, 36);
                p.Rect(2, 0, 14, 7, wood); p.Rect(2, 0, 14, 1, woodD); p.Rect(2, 0, 1, 7, woodD); p.Rect(15, 0, 1, 7, woodD);
                p.Rect(5, 0, 1, 7, goldD); p.Rect(12, 0, 1, 7, goldD);
                if (f == 0)
                {
                    p.Rect(2, 7, 14, 4, woodL); p.Rect(2, 10, 14, 1, wood); p.Rect(3, 11, 12, 1, wood);
                    p.Rect(5, 7, 1, 4, goldD); p.Rect(12, 7, 1, 4, goldD);
                    p.Rect(8, 5, 2, 3, gold); p.Set(8, 6, goldD);
                }
                else
                {
                    p.Rect(3, 7, 12, 2, C(40, 26, 16));                                        // dark inside
                    p.Rect(4, 7, 3, 1, gold); p.Rect(9, 7, 2, 2, gold); p.Set(12, 7, C(120, 230, 160)); // loot glinting
                    p.Rect(2, 9, 14, 2, woodD);                                                // lid thrown back
                    p.Rect(2, 11, 14, 3, woodL); p.Rect(5, 11, 1, 3, goldD); p.Rect(12, 11, 1, 3, goldD);
                }
                p.Outline(C(40, 24, 12));
                Save(p, $"Sprites/Props/chest_{f}", new Vector2(0.5f, 1f / 16f));
            }
        }

        /// <summary>
        /// One-shot effects of the bosses' counter-attacks (4 frames each, 32 x 32): fangs snapping upward, an impact
        /// burst, a ring of ice shards, rising gusts of wind.
        /// </summary>
        static void CounterFxArt()
        {
            for (int f = 0; f < 4; f++)
            {
                int a = f == 3 ? 130 : 255;
                {
                    // bite: two fangs thrusting up, venom at the tips
                    var p = new Px(32, 32);
                    int h = 9 + f * 6;
                    Color32 fang = C(246, 250, 236, a), edge = C(150, 236, 110, a), drip = C(120, 255, 90, a);
                    foreach (int cx in new[] { 10, 21 })
                        for (int y = 0; y < h; y++)
                        {
                            int w = Mathf.Max(1, Mathf.RoundToInt(4f * (1f - y / (float)h)));
                            int lean = (cx < 16 ? 1 : -1) * y / 6;
                            for (int x = -w; x <= w; x++) p.Set(cx + lean + x, 2 + y, Mathf.Abs(x) == w ? edge : fang);
                        }
                    if (f >= 1) { p.Set(10 + h / 6, 3 + h, drip); p.Set(21 - h / 6, 3 + h, drip); p.Set(16, 4 + f * 5, drip); p.Set(15, 2 + f * 5, drip); }
                    Save(p, $"Center/cfx_bite_{f}", PCenter);
                }
                {
                    // buck: an impact burst of rays
                    var p = new Px(32, 32);
                    Color32 hot = C(255, 232, 150, a), ray = C(255, 160, 60, a);
                    float r0 = 2f + f * 3f, r1 = 7f + f * 3.5f;
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f + 0.2f;
                        Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        p.Line(16 + (int)(d.x * r0), 16 + (int)(d.y * r0), 16 + (int)(d.x * r1), 16 + (int)(d.y * r1), i % 2 == 0 ? hot : ray);
                        if (i % 2 == 0) p.Line(17 + (int)(d.x * r0), 16 + (int)(d.y * r0), 17 + (int)(d.x * r1), 16 + (int)(d.y * r1), ray);
                    }
                    if (f < 2) p.Disc(16f, 16f, 3.5f - f, hot);
                    Save(p, $"Center/cfx_buck_{f}", PCenter);
                }
                {
                    // frost: a ring of ice shards flying outward
                    var p = new Px(32, 32);
                    Color32 ice = C(190, 234, 255, a), core = C(255, 255, 255, a), deep = C(120, 180, 236, a);
                    float r = 4f + f * 3.4f;
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f;
                        int x = 16 + Mathf.RoundToInt(Mathf.Cos(ang) * r), y = 16 + Mathf.RoundToInt(Mathf.Sin(ang) * r);
                        p.Set(x, y, core); p.Set(x - 1, y, ice); p.Set(x + 1, y, ice); p.Set(x, y - 1, ice); p.Set(x, y + 1, ice);
                        int ox = Mathf.RoundToInt(Mathf.Cos(ang) * 2f), oy = Mathf.RoundToInt(Mathf.Sin(ang) * 2f);
                        p.Set(x + ox, y + oy, deep);
                    }
                    if (f == 0) { p.Disc(16f, 16f, 3f, core); p.Disc(16f, 16f, 1.5f, ice); }
                    Save(p, $"Center/cfx_frost_{f}", PCenter);
                }
                {
                    // gust: three chevrons of wind rising
                    var p = new Px(32, 32);
                    Color32 wind = C(240, 250, 255, a), soft = C(190, 220, 240, a);
                    for (int i = 0; i < 3; i++)
                    {
                        int y = 3 + i * 7 + f * 3, hw = 10 - i * 2;
                        p.Line(16 - hw, y, 16, y + 5, wind); p.Line(16, y + 5, 16 + hw, y, wind);
                        p.Line(16 - hw, y - 1, 16, y + 4, soft); p.Line(16, y + 4, 16 + hw, y - 1, soft);
                    }
                    Save(p, $"Center/cfx_gust_{f}", PCenter);
                }
            }
        }

        static void HawkArt(Vector2 pv)
        {
            const string d = "Sprites/Enemies/";
            Color32 body = C(134, 92, 52), wing = C(100, 66, 36), wingL = C(160, 116, 70), head = C(242, 236, 220), beak = C(240, 190, 60), eye = C(20, 16, 16);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                if (f < 2)
                {
                    int wy = f == 0 ? 9 : 4;
                    p.Line(8, 7, 1, wy, wing); p.Line(8, 8, 2, wy + 1, wingL); p.Line(7, 6, 0, wy - 1, wing);
                    p.Ellipse(9, 7, 3.5f, 2.2f, body);
                    p.Rect(4, 6, 2, 2, wing);
                    p.Disc(13.5f, 8.5f, 2f, head);
                    p.Set(16, 8, beak); p.Set(16, 7, beak); p.Set(14, 9, eye);
                    p.Rect(2, 6, 2, 1, body);
                }
                else
                {
                    // stoop: beak first (pointing down), wings swept back in a V, tail feathers trailing.
                    // Hawk.LateUpdate rotates this sprite along the dive, so it uses a centre pivot.
                    p.Rect(8, 14, 3, 3, wing); p.Set(7, 16, wing); p.Set(11, 16, wing);
                    for (int i = 0; i < 6; i++)
                    {
                        p.Set(6 - i / 2, 8 + i, wing); p.Set(7 - i / 2, 8 + i, wing); p.Set(5 - i / 2, 9 + i, wingL);
                        p.Set(12 + i / 2, 8 + i, wing); p.Set(11 + i / 2, 8 + i, wing); p.Set(13 + i / 2, 9 + i, wingL);
                    }
                    p.Ellipse(9, 9, 2.2f, 5f, body);
                    p.Disc(9, 4, 2f, head);
                    p.Set(9, 1, beak); p.Set(9, 2, beak); p.Set(8, 4, eye); p.Set(10, 4, eye);
                }
                p.Outline(C(40, 26, 16));
                Save(p, d + $"hawk_{f}", f < 2 ? pv : new Vector2(0.5f, 0.5f));
            }
        }

        static void TreeArt()
        {
            const string d = "Sprites/Props/";
            Color32 trunk = C(112, 74, 42), trunkD = C(78, 50, 28), leaf = C(72, 142, 62), leafL = C(116, 184, 82), leafD = C(44, 98, 46);
            var t0 = new Px(32, 48);
            t0.Rect(14, 1, 4, 20, trunk); t0.Rect(16, 1, 2, 20, trunkD); t0.Line(17, 14, 22, 19, trunk); t0.Line(14, 12, 9, 17, trunk);
            t0.Disc(16, 30, 11.5f, leafD); t0.Disc(9, 25, 7.5f, leafD); t0.Disc(23, 25, 7.5f, leafD); t0.Disc(16, 38, 8f, leafD);
            t0.Disc(15, 31, 10f, leaf); t0.Disc(9, 26, 6f, leaf); t0.Disc(22, 26, 6f, leaf); t0.Disc(15, 38, 6.5f, leaf);
            t0.Disc(12, 35, 3.5f, leafL); t0.Disc(19, 30, 2.5f, leafL); t0.Disc(8, 28, 2f, leafL);
            t0.Outline(C(26, 40, 22));
            Save(t0, d + "tree_0", new Vector2(0.5f, 1f / 48f));
            var t1 = new Px(32, 48);
            t1.Rect(15, 1, 3, 9, trunk); t1.Rect(17, 1, 1, 9, trunkD);
            for (int layer = 0; layer < 4; layer++)
            {
                int baseY = 8 + layer * 9, half = 12 - layer * 2;
                for (int y = 0; y < 12; y++)
                {
                    int hw = (int)(half * (1f - y / 12f));
                    for (int x = 16 - hw; x <= 16 + hw; x++) t1.Set(x, baseY + y, x < 16 ? leaf : leafD);
                }
            }
            t1.Set(16, 46, leafL); t1.Set(13, 30, leafL); t1.Set(12, 21, leafL);
            t1.Outline(C(20, 44, 30));
            Save(t1, d + "tree_1", new Vector2(0.5f, 1f / 48f));
        }

        static void BatArt(string prefix, Color32 body, Color32 wing, Color32 eye, Vector2 pv)
        {
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(18, 18);
                if (f < 2)
                {
                    int wy = f == 0 ? 8 : 4;
                    p.Rect(1, wy, 6, 2, wing); p.Rect(0, wy + (f == 0 ? 2 : -1), 3, 2, wing);
                    p.Rect(11, wy, 6, 2, wing); p.Rect(15, wy + (f == 0 ? 2 : -1), 3, 2, wing);
                    p.Ellipse(9, 6.5f, 2.8f, 3.2f, body);
                    p.Set(7, 10, body); p.Set(11, 10, body);
                    p.Set(8, 7, eye); p.Set(10, 7, eye);
                    p.Set(9, 5, C(240, 240, 240));
                }
                else
                {
                    p.Ellipse(9, 9f, 2.6f, 4f, body);
                    p.Rect(6, 7, 1, 5, wing); p.Rect(12, 7, 1, 5, wing);
                    p.Set(8, 15, C(60, 50, 50)); p.Set(10, 15, C(60, 50, 50)); p.Set(8, 14, body); p.Set(10, 14, body);
                    p.Set(7, 5, body); p.Set(11, 5, body);
                    p.Set(8, 7, eye); p.Set(10, 7, eye);
                }
                p.Outline(C(24, 14, 26));
                Save(p, $"{prefix}_{f}", pv);
            }
        }

        // ======================================================================================= ITEMS

        static Px ItemCanvas() => new Px(16, 16);
        static readonly Vector2 PItem = new Vector2(0.5f, 1f / 16f);

        static void ItemArt()
        {
            const string d = "Sprites/Items/";
            Color32 ol = C(30, 22, 22);
            {
                // pebbles: three small stones lying together (shot for the slingshot)
                var p = ItemCanvas(); Color32 st = C(140, 134, 128), stL = C(190, 184, 176), stD = C(96, 92, 88);
                p.Ellipse(5.5f, 2f, 2.4f, 1.8f, st); p.Ellipse(10.5f, 2f, 2.6f, 2f, st); p.Ellipse(8f, 4.6f, 2.2f, 1.7f, st);
                p.Set(4, 3, stL); p.Set(5, 3, stL); p.Set(10, 3, stL); p.Set(7, 5, stL); p.Set(8, 5, stL); p.Set(12, 1, stD); p.Set(6, 1, stD);
                p.Outline(ol); Save(p, d + "pebbles", PItem);
            }
            {
                // one pebble in flight
                var p = new Px(6, 6); p.Disc(3f, 3f, 1.8f, C(150, 144, 138)); p.Set(3, 4, C(206, 200, 192)); p.Set(2, 1, C(100, 96, 92));
                p.Outline(ol); Save(p, d + "pebble", PCenter);
            }
            {
                // the Pebble Pouch: a small leather bag with stones showing at its mouth
                var p = ItemCanvas(); Color32 lt = C(150, 104, 62), ltD = C(104, 70, 40), ltL = C(190, 142, 90), st = C(150, 144, 138), tie = C(214, 190, 120);
                p.Ellipse(8f, 4.5f, 5f, 4.2f, lt); p.Rect(5, 8, 6, 2, lt); p.Rect(5, 8, 6, 1, tie);
                p.Rect(4, 2, 1, 4, ltD); p.Rect(5, 1, 6, 1, ltD); p.Set(10, 6, ltL); p.Set(11, 5, ltL); p.Set(10, 5, ltL);
                p.Set(6, 10, st); p.Set(8, 11, st); p.Set(9, 10, st); p.Set(7, 10, C(196, 190, 182));
                p.Outline(ol); Save(p, d + "pebblepouch", PItem);
            }
            { var p = ItemCanvas(); p.Ellipse(8, 3.5f, 3.6f, 2.6f, C(124, 118, 112)); p.Set(6, 5, C(176, 170, 162)); p.Set(7, 5, C(176, 170, 162)); p.Set(10, 2, C(86, 82, 78)); p.Set(9, 1, C(86, 82, 78)); p.Outline(ol); Save(p, d + "rock", PItem); }
            {
                // ice chunk left by a frost bat
                var p = ItemCanvas();
                p.Ellipse(8, 4f, 4.2f, 3.4f, C(150, 206, 240));
                p.Rect(5, 5, 3, 2, C(206, 236, 252)); p.Set(6, 6, C(255, 255, 255)); p.Set(5, 6, C(255, 255, 255));
                p.Rect(9, 1, 3, 2, C(98, 152, 204)); p.Set(11, 3, C(98, 152, 204));
                p.Line(7, 2, 9, 5, C(206, 236, 252));
                p.Outline(C(40, 72, 116));
                Save(p, d + "icechunk", PItem);
            }
            {
                var p = ItemCanvas();
                p.Disc(8f, 4f, 2.8f, C(244, 250, 255));
                p.Set(7, 2, C(196, 214, 238)); p.Set(8, 2, C(196, 214, 238)); p.Set(9, 2, C(196, 214, 238)); p.Set(10, 3, C(196, 214, 238));
                p.Outline(C(96, 126, 170));
                Save(p, d + "snowball", PItem);
            }
            {
                var p = ItemCanvas(); Color32 cl = C(182, 108, 66), cd = C(128, 70, 42), cll = C(220, 150, 100);
                p.Ellipse(8, 4.5f, 4.2f, 4f, cl); p.Rect(6, 8, 4, 2, cl); p.Rect(5, 10, 6, 1, cll);
                for (int x = 4; x < 13; x++) if (x % 2 == 0) p.Set(x, 4, cd);
                p.Set(6, 6, cll); p.Set(5, 5, cll); p.Rect(6, 1, 5, 1, cd);
                p.Outline(ol); Save(p, d + "pot", PItem);
            }
            {
                var p = ItemCanvas(); Color32 wd = C(156, 108, 62), wdd = C(100, 66, 36), m = C(120, 120, 132);
                p.Rect(3, 1, 10, 10, wd);
                for (int y = 1; y < 11; y += 3) p.Rect(3, y, 10, 1, wdd);
                p.Line(3, 1, 12, 10, wdd); p.Line(3, 10, 12, 1, wdd);
                p.Set(3, 1, m); p.Set(12, 1, m); p.Set(3, 10, m); p.Set(12, 10, m);
                p.Outline(ol); Save(p, d + "crate", PItem);
            }
            {
                var p = ItemCanvas(); Color32 sh = C(156, 116, 70), tip = C(190, 190, 200), fl = C(230, 230, 230), fr = C(200, 60, 60);
                p.Rect(3, 2, 9, 1, sh); p.Set(12, 1, tip); p.Set(12, 2, tip); p.Set(12, 3, tip); p.Set(13, 2, tip); p.Set(14, 2, C(240, 240, 250));
                p.Set(1, 1, fl); p.Set(2, 1, fr); p.Set(1, 3, fl); p.Set(2, 3, fr); p.Set(2, 2, sh);
                Save(p, d + "arrow", PItem);
            }
            for (int f = 0; f < 3; f++)
            {
                var p = ItemCanvas(); Color32 b = C(40, 40, 54), bl = C(110, 110, 132);
                p.Disc(8, 4.5f, 3.6f, b); p.Set(6, 6, bl); p.Set(7, 7, bl);
                p.Set(9, 8, C(120, 90, 50)); p.Set(10, 9, C(120, 90, 50));
                p.Outline(C(14, 14, 20));
                if (f == 1) { p.Set(11, 10, C(255, 230, 90)); p.Set(12, 11, C(255, 140, 40)); }
                if (f == 2) { p.Set(11, 10, C(255, 255, 255)); p.Set(12, 10, C(255, 90, 40)); p.Set(11, 11, C(255, 200, 60)); }
                Save(p, d + $"bomb_{f}", PItem);
            }
            { var p = ItemCanvas(); p.Ellipse(8, 2f, 2.4f, 1.6f, C(240, 196, 64)); p.Set(7, 3, C(255, 244, 160)); p.Set(9, 1, C(170, 120, 30)); p.Outline(C(80, 54, 10)); Save(p, d + "gold_small", PItem); }
            { var p = ItemCanvas(); p.Ellipse(8, 3f, 4f, 2.6f, C(240, 196, 64)); p.Rect(6, 4, 3, 1, C(255, 244, 160)); p.Set(10, 1, C(170, 120, 30)); p.Set(11, 2, C(170, 120, 30)); p.Outline(C(80, 54, 10)); Save(p, d + "gold_big", PItem); }
            {
                var p = ItemCanvas(); Color32 g = C(60, 210, 130), gl = C(190, 255, 220), gd = C(20, 120, 70);
                p.Set(8, 1, gd); p.Rect(7, 2, 3, 1, g); p.Rect(6, 3, 5, 2, g); p.Rect(7, 5, 3, 1, gl); p.Set(7, 4, gl); p.Set(10, 3, gd);
                p.Outline(C(10, 60, 30)); Save(p, d + "gem", PItem);
            }
            {
                var p = ItemCanvas(); Color32 s = C(146, 100, 58), sd = C(100, 66, 36);
                // (drawn a row clear of the sheet's lower edge: the outline has to close under it)
                p.Ellipse(8, 5.5f, 4.5f, 4f, s); p.Rect(7, 9, 2, 2, sd); p.Rect(6, 11, 4, 1, s);
                p.Disc(8, 5f, 1.8f, C(40, 40, 54)); p.Set(7, 6, C(110, 110, 130));
                p.Outline(ol); Save(p, d + "bombbag", PItem);
            }
            {
                var p = ItemCanvas(); Color32 r = C(196, 156, 92), rd = C(130, 96, 50);
                // (as the bomb bag: a row up, room under it for its outline)
                p.Ellipse(8, 4f, 5.5f, 3f, r); p.Ellipse(8, 4f, 3.5f, 1.6f, rd); p.Ellipse(8, 4f, 2f, 0.8f, r);
                p.Set(13, 6, r); p.Set(14, 7, r);
                p.Outline(ol); Save(p, d + "ropepile", PItem);
            }
            {
                var p = ItemCanvas(); Color32 g = C(236, 190, 60), gl = C(255, 240, 150), gd = C(160, 110, 30);
                p.Rect(5, 1, 6, 2, gd); p.Rect(6, 3, 4, 1, g); p.Ellipse(8, 7f, 3.2f, 3.6f, g);
                p.Set(7, 8, C(40, 20, 10)); p.Set(9, 8, C(40, 20, 10)); p.Rect(7, 5, 3, 1, gd); p.Set(6, 9, gl); p.Set(7, 10, gl);
                p.Outline(C(80, 50, 10)); Save(p, d + "idol", PItem);
            }
            {
                var p = ItemCanvas(); Color32 r = C(255, 186, 56), rl = C(255, 246, 190), rd = C(212, 112, 28);       // the Sunstone: warm gold
                p.Rect(7, 1, 2, 1, rd); p.Rect(6, 2, 4, 8, r); p.Rect(5, 4, 1, 4, rd); p.Rect(10, 4, 1, 4, rd); p.Rect(7, 10, 2, 1, r);
                p.Set(7, 7, rl); p.Set(7, 8, rl); p.Set(8, 9, C(255, 255, 255)); p.Rect(8, 3, 1, 3, rd);
                p.Outline(C(255, 226, 130, 170)); Save(p, d + "heartstone", PItem);
            }
            // upgrade icons
            {
                // ice cleats: a fur-lined boot with steel spikes under the sole
                var p = ItemCanvas(); Color32 b = C(84, 110, 156), bd = C(52, 70, 108), fur = C(236, 244, 252), sp = C(206, 214, 226);
                p.Rect(5, 5, 4, 7, b); p.Rect(5, 3, 8, 3, b); p.Rect(5, 3, 8, 1, bd); p.Rect(5, 11, 4, 2, fur);
                p.Set(9, 5, bd); p.Set(12, 5, bd);
                p.Set(6, 2, sp); p.Set(8, 2, sp); p.Set(10, 2, sp); p.Set(12, 2, sp); p.Set(6, 1, sp); p.Set(10, 1, sp);
                p.Outline(ol); Save(p, d + "iceboots", PItem);
            }
            {
                // heavy boots: thick iron-soled boots
                var p = ItemCanvas(); Color32 b = C(96, 84, 76), bd = C(60, 52, 48), iron = C(176, 182, 196), ironD = C(110, 116, 132);
                p.Rect(5, 5, 4, 7, b); p.Rect(5, 3, 8, 3, b); p.Rect(5, 11, 4, 1, bd);
                p.Rect(4, 1, 10, 2, iron); p.Rect(4, 1, 10, 1, ironD); p.Set(6, 4, iron); p.Set(9, 4, iron); p.Set(12, 4, iron);
                p.Outline(ol); Save(p, d + "heavyboots", PItem);
            }
            {
                // magnet charm: a horseshoe magnet on a cord
                var p = ItemCanvas(); Color32 red = C(220, 60, 60), redD = C(150, 30, 36), tip = C(220, 224, 232);
                p.Rect(4, 3, 3, 7, red); p.Rect(9, 3, 3, 7, red); p.Rect(4, 9, 8, 3, red); p.Rect(4, 9, 1, 3, redD); p.Rect(11, 3, 1, 9, redD);
                p.Rect(4, 1, 3, 2, tip); p.Rect(9, 1, 3, 2, tip);
                p.Set(7, 13, C(150, 110, 70)); p.Set(8, 13, C(150, 110, 70));
                p.Outline(ol); Save(p, d + "magnetcharm", PItem);
            }
            {
                // lantern: a brass frame, a glass body with a flame, a ring to carry it by
                var p = ItemCanvas(); Color32 br = C(196, 150, 60), brD = C(128, 92, 34), glass = C(255, 226, 130), fl = C(255, 250, 220), fo = C(255, 150, 50);
                p.Rect(5, 1, 6, 2, br); p.Rect(5, 1, 6, 1, brD);
                p.Rect(5, 3, 6, 6, glass); p.Rect(5, 3, 1, 6, br); p.Rect(10, 3, 1, 6, brD);
                p.Rect(7, 4, 2, 3, fo); p.Set(7, 6, fl); p.Set(8, 5, fl); p.Set(7, 7, fo);
                p.Rect(5, 9, 6, 1, br); p.Rect(6, 10, 4, 1, brD);
                p.Set(6, 11, br); p.Set(9, 11, br); p.Rect(6, 12, 4, 1, br);
                p.Outline(ol); Save(p, d + "lantern", PItem);
            }
            {
                // spore mask: a leather face mask with a round filter and goggles
                var p = ItemCanvas(); Color32 lea = C(120, 84, 60), leaD = C(78, 52, 38), glass = C(120, 230, 210), met = C(176, 180, 196), metD = C(110, 114, 132);
                p.Ellipse(8, 7.5f, 5f, 5f, lea); p.Rect(4, 9, 8, 1, leaD);
                p.Disc(6f, 9.5f, 1.6f, glass); p.Disc(10f, 9.5f, 1.6f, glass); p.Set(6, 10, C(230, 255, 250)); p.Set(10, 10, C(230, 255, 250));
                p.Disc(8f, 4.5f, 2.6f, met); p.Set(7, 4, metD); p.Set(9, 4, metD); p.Set(8, 5, metD); p.Set(8, 3, metD);
                p.Rect(2, 8, 2, 1, leaD); p.Rect(12, 8, 2, 1, leaD);
                p.Outline(ol); Save(p, d + "sporemask", PItem);
            }
            {
                // fur coat
                var p = ItemCanvas(); Color32 c = C(150, 96, 56), cd = C(104, 62, 36), fur = C(240, 236, 224), furD = C(200, 194, 178);
                p.Rect(5, 1, 6, 9, c); p.Rect(3, 4, 2, 6, c); p.Rect(11, 4, 2, 6, c);
                p.Rect(5, 10, 6, 2, fur); p.Rect(6, 12, 4, 1, furD);
                p.Rect(3, 3, 2, 1, fur); p.Rect(11, 3, 2, 1, fur); p.Rect(5, 1, 6, 1, furD);
                p.Rect(8, 2, 1, 8, cd); p.Set(7, 5, C(230, 190, 90)); p.Set(7, 8, C(230, 190, 90));
                p.Outline(ol); Save(p, d + "warmcoat", PItem);
            }
            {
                var p = ItemCanvas(); Color32 b = C(140, 86, 46), bd = C(90, 52, 26), sp = C(190, 190, 200);
                p.Rect(5, 6, 4, 6, b); p.Rect(5, 5, 7, 2, b); p.Rect(5, 11, 4, 1, bd);
                p.Line(6, 1, 10, 2, sp); p.Line(6, 3, 10, 4, sp); p.Line(10, 2, 6, 3, sp); p.Set(6, 4, sp);
                p.Outline(ol); Save(p, d + "springboots", PItem);
            }
            {
                var p = ItemCanvas(); Color32 m = C(170, 176, 196), md = C(100, 104, 124);
                // tempered blade (the hand upgrade; the id is still "ironwhip")
                p.Line(6, 5, 12, 13, m); p.Line(7, 5, 13, 13, C(236, 240, 252)); p.Line(5, 5, 11, 13, md); p.Set(13, 14, C(236, 240, 252));
                p.Line(3, 6, 8, 3, C(228, 180, 70)); p.Line(4, 2, 5, 3, C(110, 70, 40)); p.Set(3, 1, C(228, 180, 70));
                p.Outline(ol); Save(p, d + "ironwhip", PItem);
            }
            {
                var p = ItemCanvas(); Color32 g = C(220, 176, 60);
                p.Disc(8, 5f, 4.5f, g); p.Disc(8, 5f, 3.4f, C(236, 230, 210));
                p.Line(6, 3, 10, 7, C(220, 50, 50)); p.Set(8, 5, C(40, 40, 40));
                p.Outline(ol); Save(p, d + "compass", PItem);
            }
            {
                var p = ItemCanvas(); Color32 w = C(140, 90, 50), wd = C(90, 56, 30);
                p.Rect(4, 1, 8, 9, w); p.Rect(4, 3, 8, 1, wd); p.Rect(4, 7, 8, 1, wd);
                p.Set(7, 10, C(60, 60, 60)); p.Set(8, 11, C(255, 200, 60));
                p.Rect(7, 4, 2, 2, C(240, 240, 230)); p.Set(7, 4, C(30, 30, 30));
                p.Outline(ol); Save(p, d + "blastpowder", PItem);
            }
            {
                var p = ItemCanvas(); HeartShape(p, 2, 1, C(220, 40, 60), C(255, 150, 160), C(140, 20, 40)); p.Outline(ol); Save(p, d + "heart", PItem);
            }
            foreach (var big in new[] { false, true })
            {
                // the healing flask (+3) and the elixir (everything): a stoppered bottle, red or gold, with a heart in it
                var p = ItemCanvas();
                Color32 glass = C(200, 225, 235), glassD = C(120, 150, 170), cork = C(150, 104, 62);
                Color32 liq = big ? C(255, 200, 60) : C(220, 40, 60), liqL = big ? C(255, 240, 170) : C(255, 150, 160), liqD = big ? C(190, 130, 30) : C(140, 20, 40);
                p.Rect(6, 12, 4, 2, cork); p.Rect(6, 10, 4, 2, glass); p.Rect(5, 10, 1, 1, glassD); p.Rect(10, 10, 1, 1, glassD);
                p.Rect(4, 2, 8, 8, glass); p.Rect(3, 3, 10, 6, glass); p.Rect(3, 3, 1, 6, glassD); p.Rect(4, 2, 8, 1, glassD);
                p.Rect(4, 3, 8, big ? 6 : 4, liq); p.Rect(4, 3, 8, 1, liqD); p.Rect(4, big ? 8 : 6, 8, 1, liqL);
                p.Set(10, 8, C(255, 255, 255)); p.Set(10, 7, C(255, 255, 255));
                if (big) { p.Set(13, 13, C(255, 255, 230)); p.Set(13, 14, liqL); p.Set(13, 12, liqL); p.Set(12, 13, liqL); p.Set(14, 13, liqL); }
                p.Outline(ol);
                Save(p, d + (big ? "elixir" : "healflask"), PItem);
            }
            {
                // heart vessel (max-health item): a golden heart with a red core and a glint
                var p = ItemCanvas();
                HeartShape(p, 3, 3, C(255, 200, 60), C(255, 240, 170), C(190, 130, 30));
                Color32 red = C(220, 40, 60);
                p.Rect(5, 7, 5, 2, red); p.Rect(6, 6, 3, 1, red); p.Set(7, 5, red);
                p.Set(5, 8, C(255, 150, 160));
                p.Outline(ol);
                p.Set(13, 13, C(255, 255, 230)); p.Set(13, 14, C(255, 240, 170)); p.Set(13, 12, C(255, 240, 170)); p.Set(12, 13, C(255, 240, 170)); p.Set(14, 13, C(255, 240, 170));
                Save(p, d + "heartvessel", PItem);
            }
            // rope segments (centre pivot)
            Color32 rc = C(196, 156, 92), rcd = C(130, 96, 50);
            {
                var p = new Px(16, 16); for (int y = 0; y < 16; y++) { p.Set(7, y, y % 4 < 2 ? rc : rcd); p.Set(8, y, y % 4 < 2 ? rcd : rc); }
                Save(p, "Center/rope_seg", PCenter);
                var t = p.Copy(); t.Rect(5, 13, 6, 2, C(150, 150, 160)); t.Set(5, 12, C(150, 150, 160)); t.Set(10, 12, C(150, 150, 160)); t.Set(4, 11, C(200, 200, 210)); t.Set(11, 11, C(200, 200, 210));
                Save(t, "Center/rope_top", PCenter);
                var b = new Px(16, 16); for (int y = 6; y < 16; y++) { b.Set(7, y, y % 4 < 2 ? rc : rcd); b.Set(8, y, y % 4 < 2 ? rcd : rc); }
                b.Set(6, 5, rc); b.Set(9, 5, rcd); b.Set(7, 4, rc);
                Save(b, "Center/rope_end", PCenter);
                var hook = new Px(16, 16); hook.Rect(7, 4, 2, 8, rc); hook.Rect(5, 12, 6, 2, C(150, 150, 160)); hook.Set(4, 11, C(200, 200, 210)); hook.Set(11, 11, C(200, 200, 210));
                Save(hook, "Center/rope_hook", PCenter);

                Color32 m = C(150, 150, 160), ml = C(205, 205, 215), md = C(88, 88, 100);
                // Hook thrown over a ledge lip beside the rope (wall on the right; flipped for the left).
                // 32x24: rope column centre at x 7-8, wall starts at x 16, ledge top at y 16.
                var lip = new Px(32, 24);
                for (int y = 0; y < 15; y++) { lip.Set(7, y, y % 4 < 2 ? rc : rcd); lip.Set(8, y, y % 4 < 2 ? rcd : rc); }
                lip.Set(7, 15, rc); lip.Set(8, 15, rcd); lip.Set(9, 15, rc); lip.Set(8, 16, rc); lip.Set(9, 16, rcd);
                for (int x = 10; x < 20; x++) { lip.Set(x, 16, x % 4 < 2 ? rc : rcd); lip.Set(x, 17, x % 4 < 2 ? rcd : rc); }
                lip.Set(20, 16, m); lip.Set(20, 17, ml); lip.Set(20, 18, m);           // ring
                lip.Rect(21, 17, 4, 1, m); lip.Rect(21, 18, 3, 1, md);                  // shank
                lip.Set(25, 16, m); lip.Set(25, 17, ml); lip.Set(25, 18, ml); lip.Set(25, 19, m); lip.Set(24, 19, m);
                lip.Set(26, 15, m); lip.Set(26, 16, md); lip.Set(23, 15, m); lip.Set(23, 16, md); // prongs biting into the ground
                lip.Set(26, 19, ml); lip.Set(27, 20, m);
                Save(lip, "Center/rope_lip", new Vector2(8f / 32f, 8f / 24f));

                // Piton driven into a wall face beside the rope. 32x16: wall starts at x 16.
                var pit = new Px(32, 16);
                for (int y = 0; y < 11; y++) { pit.Set(7, y, y % 4 < 2 ? rc : rcd); pit.Set(8, y, y % 4 < 2 ? rcd : rc); }
                pit.Set(7, 11, rc); pit.Set(8, 11, rcd); pit.Set(9, 11, rc); pit.Set(9, 12, rcd); pit.Set(10, 12, rc);
                for (int x = 10; x < 13; x++) { pit.Set(x, 12, x % 4 < 2 ? rc : rcd); pit.Set(x, 13, x % 4 < 2 ? rcd : rc); }
                pit.Set(13, 11, m); pit.Set(13, 12, ml); pit.Set(13, 13, ml); pit.Set(13, 14, m); // eye
                pit.Rect(14, 12, 3, 2, m); pit.Rect(14, 12, 3, 1, ml);
                pit.Rect(17, 11, 2, 4, md); pit.Set(17, 12, m); pit.Set(17, 13, m);      // head in the wall
                pit.Set(19, 12, md); pit.Set(19, 13, md);
                Save(pit, "Center/rope_piton", new Vector2(8f / 32f, 0.5f));
            }
        }

        static void HeartShape(Px p, int ox, int oy, Color32 c, Color32 hl, Color32 sh)
        {
            string[] rows =
            {
                ".##...##..",
                "####.####.",
                "#########.",
                "#########.",
                ".#######..",
                "..#####...",
                "...###....",
                "....#.....",
            };
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] == '#') p.Set(ox + x, oy + rows.Length - 1 - r, c);
            p.Set(ox + 1, oy + 6, hl); p.Set(ox + 2, oy + 6, hl); p.Set(ox + 1, oy + 5, hl);
            p.Set(ox + 6, oy + 2, sh); p.Set(ox + 5, oy + 1, sh); p.Set(ox + 7, oy + 3, sh);
        }

        // ======================================================================================= TRAPS & PROPS

        /// <summary>The people of Brindlemoor (one picture each, in the order of Villagers.All), the cage they are
        /// found in, and a village house with dark and with lit windows.</summary>
        /// <summary>
        /// What the story puts on the mountain (Story): the pillar with the old signs, the bound spirit and its
        /// chain, the paper and the copy made on it, and the hammer - its three parts and the whole of it.
        /// </summary>
        static void StoryArt()
        {
            Color32 ol = C(26, 22, 30);
            Color32 sign = C(120, 226, 245), signD = C(60, 150, 190);
            {
                // the pillar: a worn shaft on a foot, a cap on top, signs cut into its face and still faintly lit
                var p = new Px(18, 32);
                Color32 st = C(126, 124, 138), stD = C(84, 82, 98), stL = C(170, 168, 180), moss = C(74, 120, 70);
                p.Rect(2, 0, 14, 3, stD); p.Rect(3, 1, 12, 2, st);
                p.Rect(5, 3, 8, 23, st); p.Rect(5, 3, 2, 23, stL); p.Rect(11, 3, 2, 23, stD);
                p.Rect(3, 26, 12, 3, st); p.Rect(3, 28, 12, 1, stL); p.Rect(4, 29, 10, 1, stD);
                // the signs: no letters anybody knows
                int[,] marks = { { 7, 22, 3, 1 }, { 8, 20, 1, 2 }, { 7, 17, 1, 2 }, { 9, 17, 2, 1 }, { 9, 16, 1, 1 }, { 7, 13, 3, 1 }, { 7, 12, 1, 1 }, { 9, 11, 1, 2 },
                                 { 8, 8, 2, 1 }, { 7, 7, 1, 2 }, { 10, 6, 1, 2 }, { 8, 5, 2, 1 } };
                for (int i = 0; i < marks.GetLength(0); i++) p.Rect(marks[i, 0], marks[i, 1], marks[i, 2], marks[i, 3], i % 3 == 0 ? signD : sign);
                p.Set(6, 24, stD); p.Set(12, 19, stL); p.Set(6, 9, stD); p.Line(11, 24, 12, 21, stD);       // cracks
                p.Rect(2, 2, 3, 1, moss); p.Set(5, 3, moss); p.Rect(12, 1, 3, 1, moss); p.Set(3, 26, moss);
                p.Outline(ol); Save(p, "Sprites/Props/runepillar", new Vector2(0.5f, 0f));
            }
            {
                // a spirit: pale, see-through at the hem, two dark eyes; nothing else to it
                var p = new Px(16, 18);
                Color32 b = C(214, 238, 250, 235), bL = C(244, 252, 255, 245), bD = C(150, 196, 226, 200), hem = C(150, 196, 226, 120);
                p.Ellipse(8f, 11f, 5f, 5.6f, b);
                p.Rect(3, 4, 10, 7, b);
                p.Rect(3, 2, 2, 2, hem); p.Rect(7, 2, 2, 2, hem); p.Rect(11, 2, 2, 2, hem); p.Rect(3, 4, 10, 1, bD);
                p.Rect(5, 13, 3, 2, bL); p.Set(4, 12, bL);
                p.Rect(5, 9, 2, 3, C(40, 52, 80)); p.Rect(9, 9, 2, 3, C(40, 52, 80));
                p.Rect(7, 6, 2, 1, C(70, 90, 120));
                Save(p, "Sprites/NPC/ghost", new Vector2(0.5f, 2f / 18f));
            }
            {
                // what holds it: a staple in the ground, three links and a band
                var p = new Px(8, 10);
                Color32 ir = C(92, 92, 108), irL = C(140, 142, 160);
                p.Rect(2, 0, 4, 1, ir); p.Rect(3, 1, 2, 2, irL); p.Rect(3, 3, 2, 2, ir); p.Rect(3, 5, 2, 2, irL); p.Rect(2, 7, 4, 2, ir); p.Set(3, 8, irL);
                p.Outline(ol); Save(p, "Sprites/Props/ghostchain", new Vector2(0.5f, 0f));
            }
            const string d = "Sprites/Items/";
            {
                // a sheet of paper and a stick of charcoal
                var p = ItemCanvas(); Color32 pa = C(236, 228, 206), paD = C(196, 186, 160);
                p.Rect(4, 2, 9, 12, pa); p.Rect(4, 2, 9, 1, paD); p.Set(12, 13, Px.Clear); p.Set(11, 13, paD); p.Set(12, 12, paD);
                p.Line(2, 4, 6, 1, C(44, 40, 46));
                p.Outline(ol); Save(p, d + "paper", PItem);
            }
            {
                // the same sheet with the signs rubbed onto it
                var p = ItemCanvas(); Color32 pa = C(236, 228, 206), paD = C(196, 186, 160), ink = C(40, 96, 128);
                p.Rect(4, 2, 9, 12, pa); p.Rect(4, 2, 9, 1, paD); p.Set(12, 13, Px.Clear); p.Set(11, 13, paD); p.Set(12, 12, paD);
                p.Rect(6, 11, 3, 1, ink); p.Set(7, 10, ink); p.Rect(9, 9, 2, 1, ink); p.Set(6, 8, ink); p.Set(6, 7, ink); p.Rect(8, 7, 3, 1, ink); p.Set(10, 6, ink);
                p.Rect(6, 4, 2, 1, ink); p.Set(9, 4, ink); p.Set(9, 5, ink);
                p.Outline(ol); Save(p, d + "rubbing", PItem);
            }
            Color32 wood = C(54, 44, 52), woodL = C(92, 76, 84), band = C(214, 176, 80), iron = C(128, 132, 148), ironL = C(190, 194, 208), ironD = C(80, 82, 98);
            {
                // the haft: black wood, bound with brass at both ends
                var p = ItemCanvas();
                p.Line(3, 2, 12, 12, wood); p.Line(4, 2, 13, 12, woodL); p.Line(3, 3, 12, 13, wood);
                p.Rect(3, 2, 2, 2, band); p.Rect(11, 11, 2, 2, band);
                p.Outline(ol); Save(p, d + "hammer_haft", PItem);
            }
            {
                // the head: a block of grey iron with the hole the haft goes through
                var p = ItemCanvas();
                p.Rect(3, 4, 10, 7, iron); p.Rect(3, 10, 10, 1, ironL); p.Rect(3, 4, 10, 1, ironD); p.Rect(3, 4, 1, 7, ironL); p.Rect(12, 4, 1, 7, ironD);
                p.Rect(7, 6, 2, 3, C(30, 28, 36)); p.Set(5, 8, ironL); p.Set(10, 6, ironD);
                p.Outline(ol); Save(p, d + "hammer_head", PItem);
            }
            {
                // the heart: a light with no flame
                var p = ItemCanvas();
                p.Disc(8f, 7.5f, 4.6f, C(120, 226, 245)); p.Disc(8f, 7.5f, 3f, C(200, 244, 252)); p.Disc(8f, 7.5f, 1.4f, C(255, 255, 255));
                p.Set(8, 13, sign); p.Set(8, 2, sign); p.Set(2, 7, sign); p.Set(14, 7, sign);
                p.Outline(C(40, 96, 128)); Save(p, d + "hammer_core", PItem);
            }
            {
                // the hammer, whole, standing on the foot of its haft (it is swung by that foot: HammerSwing): black
                // wood bound in brass, a broad head of grey iron with flared faces, and the heart set in the middle of it
                var p = ItemCanvas();
                p.Rect(7, 1, 2, 9, wood); p.Rect(7, 1, 1, 9, woodL);
                p.Rect(6, 0, 4, 2, band); p.Set(6, 0, Mul(band, 0.7f)); p.Set(9, 0, Mul(band, 0.7f));
                p.Set(7, 4, band); p.Set(8, 5, band); p.Set(7, 6, band);                                    // the grip's binding
                p.Rect(6, 9, 4, 1, band);                                                                   // the collar under the head
                p.Rect(3, 10, 10, 5, iron); p.Rect(3, 14, 10, 1, ironL); p.Rect(3, 10, 10, 1, ironD);
                p.Rect(2, 10, 2, 5, ironD); p.Rect(12, 10, 2, 5, ironD); p.Rect(2, 14, 2, 1, iron); p.Rect(12, 14, 2, 1, iron);   // the two faces, a little wider
                p.Set(4, 13, ironL); p.Set(5, 13, ironL); p.Set(11, 11, ironD);
                p.Rect(7, 11, 2, 3, C(120, 226, 245)); p.Set(7, 12, C(255, 255, 255)); p.Set(8, 12, C(200, 244, 252));
                p.Outline(ol); Save(p, d + "hammer", PItem);
            }
            {
                // the same hammer as it is in the hand (HammerSwing): pixel for pixel the size of the player, so
                // what makes it a two-handed thing is the length of its haft, not a bigger picture
                var p = new Px(16, 28);
                p.Rect(7, 1, 2, 21, wood); p.Rect(7, 1, 1, 21, woodL);
                p.Rect(6, 0, 4, 2, band); p.Set(6, 0, Mul(band, 0.7f)); p.Set(9, 0, Mul(band, 0.7f));
                p.Set(7, 4, band); p.Set(8, 5, band); p.Set(7, 6, band); p.Set(8, 7, band); p.Set(7, 8, band);    // the grip's binding
                p.Rect(7, 14, 2, 1, band);                                                                          // a band halfway up
                p.Rect(6, 21, 4, 1, band);                                                                          // the collar under the head
                p.Rect(3, 22, 10, 5, iron); p.Rect(3, 26, 10, 1, ironL); p.Rect(3, 22, 10, 1, ironD);
                p.Rect(2, 22, 2, 5, ironD); p.Rect(12, 22, 2, 5, ironD); p.Rect(2, 26, 2, 1, iron); p.Rect(12, 26, 2, 1, iron);
                p.Set(4, 25, ironL); p.Set(5, 25, ironL); p.Set(11, 23, ironD);
                p.Rect(7, 23, 2, 3, C(120, 226, 245)); p.Set(7, 24, C(255, 255, 255)); p.Set(8, 24, C(200, 244, 252));
                p.Outline(ol); Save(p, d + "hammer_long", new Vector2(0.5f, 1f / 28f));
            }
        }

        static void VillagerArt()
        {
            const string d = "Sprites/NPC/";
            Look Make(Color32 coat, Color32 hat, Color32 hair, Color32 scarf, bool bald, bool beard, bool apron) => new Look
            {
                skin = C(232, 176, 132), skinD = C(188, 128, 92), hair = hair,
                hat = hat, hatD = Mul(hat, 0.6f), hatL = Lerp(hat, C(255, 255, 255), 0.25f),
                jacket = coat, jacketD = Mul(coat, 0.65f), jacketL = Lerp(coat, C(255, 255, 255), 0.25f),
                scarf = scarf, scarfD = Mul(scarf, 0.65f),
                pants = C(70, 62, 78), pantsD = C(48, 42, 56), boot = C(80, 50, 30), bootD = C(52, 32, 20),
                belt = C(50, 32, 20), buckle = C(210, 180, 100), eye = C(28, 20, 24), outline = C(22, 14, 20),
                bald = bald, beard = beard, beardC = Mul(hair, 0.9f), apron = apron, apronC = C(126, 84, 52)
            };
            var looks = new[]
            {
                Make(C(120, 70, 60), C(90, 60, 40), C(200, 110, 50), C(60, 60, 70), true, true, true),          // Hilde the Smith
                Make(C(96, 130, 80), C(150, 120, 70), C(110, 80, 50), C(200, 170, 90), false, false, false),    // Tam the Ropemaker
                Make(C(220, 216, 204), C(180, 60, 70), C(70, 50, 44), C(180, 60, 70), false, false, true),      // Maren the Healer
                Make(C(70, 90, 150), C(60, 70, 110), C(160, 160, 170), C(220, 200, 150), false, true, false),   // Oswin the Mapmaker
                Make(C(170, 110, 160), C(210, 170, 60), C(60, 40, 30), C(90, 160, 150), false, false, false),   // Pip the Peddler
                Make(C(60, 60, 76), C(40, 40, 54), C(210, 210, 215), C(240, 190, 80), false, false, false),     // Bera the Lamplighter
                Make(C(140, 56, 60), C(90, 40, 44), C(235, 235, 240), C(230, 200, 110), true, true, false),      // Elder Aldous
            };
            for (int i = 0; i < looks.Length; i++)
                Save(Humanoid(new Pose(), looks[i]), d + "villager_" + i, new Vector2(0.5f, 1f / 20f));
            // the stranger on the mountain (Story): a trader's coat gone grey, the hat pulled low, a violet scarf
            Save(Humanoid(new Pose(), Make(C(74, 70, 90), C(42, 40, 56), C(156, 156, 168), C(120, 70, 150), false, true, false)), d + "stranger", new Vector2(0.5f, 1f / 20f));
            {
                // the people in the street: nobody in particular, in winter coats
                var folk = new[]
                {
                    Make(C(150, 96, 60), C(110, 70, 50), C(70, 46, 30), C(200, 180, 120), false, false, false),
                    Make(C(84, 104, 140), C(190, 190, 200), C(150, 100, 60), C(190, 70, 70), false, true, false),
                    Make(C(130, 70, 100), C(90, 50, 80), C(40, 30, 30), C(220, 210, 190), false, false, true),
                    Make(C(90, 120, 90), C(70, 90, 70), C(200, 196, 190), C(160, 110, 60), true, true, false),
                };
                int[] la = { -2, 0, 2, 0 }, lb = { 2, 0, -2, 0 }, liftA = { 0, 1, 0, 0 }, liftB = { 0, 0, 0, 1 }, bob = { 0, 1, 0, 1 };
                for (int i = 0; i < folk.Length; i++)
                {
                    Save(Humanoid(new Pose(), folk[i]), d + $"folk_{i}_0", new Vector2(0.5f, 1f / 20f));
                    for (int f = 0; f < 4; f++)
                        Save(Humanoid(new Pose { legA = la[f], legB = lb[f], liftA = liftA[f], liftB = liftB[f], bob = bob[f], arm = f % 2 == 0 ? 0 : 7, scarf = f % 2 }, folk[i]), d + $"folk_{i}_{f + 1}", new Vector2(0.5f, 1f / 20f));
                }
                // children: small, a bobble hat, a coat too big for them (0 standing, 1 / 2 running)
                Color32[] coats = { C(214, 70, 70), C(70, 130, 200), C(230, 180, 60) }, hats = { C(240, 236, 226), C(230, 120, 60), C(110, 180, 120) };
                Color32 skin = C(238, 186, 144), ol = C(22, 14, 20), boot = C(70, 46, 30), eye = C(28, 20, 24);
                for (int i = 0; i < coats.Length; i++)
                    for (int f = 0; f < 3; f++)
                    {
                        var p = new Px(14, 18);      // (room above the hat for its bobble and the outline)
                        int by = f == 2 ? 1 : 0;      // a bounce in the run
                        // legs and boots
                        if (f == 0) { p.Rect(4, 1, 2, 3, boot); p.Rect(8, 1, 2, 3, boot); }
                        else if (f == 1) { p.Rect(3, 1, 2, 3, boot); p.Rect(9, 2, 2, 2, boot); }
                        else { p.Rect(5, 2, 2, 2, boot); p.Rect(7, 1, 2, 3, boot); }
                        // coat, arms, scarf
                        p.Rect(3, 4 + by, 8, 5, coats[i]); p.Rect(3, 4 + by, 8, 1, Mul(coats[i], 0.65f)); p.Rect(3, 4 + by, 2, 5, Mul(coats[i], 0.8f));
                        if (f == 0) { p.Rect(2, 5, 1, 3, coats[i]); p.Rect(11, 5, 1, 3, coats[i]); }
                        else { p.Rect(1, 6 + by, 2, 2, coats[i]); p.Rect(11, 7 + by, 2, 2, coats[i]); }
                        p.Rect(3, 9 + by, 8, 1, hats[i]);
                        // head and hat
                        p.Rect(4, 10 + by, 6, 3, skin); p.Set(8, 11 + by, eye);
                        p.Rect(3, 13 + by, 8, 2, hats[i]); p.Rect(3, 13 + by, 8, 1, Mul(hats[i], 0.75f));
                        p.Rect(6, 15 + by, 2, 1, hats[i]);
                        p.Outline(ol);
                        Save(p, d + $"kid_{i}_{f}", new Vector2(0.5f, 1f / 18f));
                    }
            }

            {
                // the cage: iron bars with a rail top and bottom
                var p = new Px(20, 24);
                Color32 bar = C(150, 150, 165), barD = C(84, 84, 100), rail = C(110, 110, 126), railD = C(64, 64, 78);
                for (int x = 1; x < 20; x += 4) { p.Rect(x, 2, 1, 20, bar); p.Rect(x + 1, 2, 1, 20, barD); }
                p.Rect(0, 0, 20, 2, rail); p.Rect(0, 0, 20, 1, railD);
                p.Rect(0, 22, 20, 2, rail); p.Rect(0, 22, 20, 1, railD);
                Save(p, d + "cage", new Vector2(0.5f, 0f));
            }
            VillageArt();       // the street fronts and the rooms (ArtGenerator.Village.cs)
            {
                // village-only wares: the Healer's draught (a red flask) and the Lamplighter's oil (a brass can)
                var p = new Px(16, 16);
                Color32 glass = C(200, 220, 235), red = C(214, 60, 70), redL = C(250, 130, 130), cork = C(150, 110, 70), ol = C(22, 14, 20);
                p.Rect(5, 2, 6, 8, ol); p.Rect(6, 3, 4, 6, red); p.Set(6, 7, redL); p.Set(6, 6, redL);
                p.Rect(7, 10, 2, 3, ol); p.Rect(7, 10, 2, 2, glass); p.Rect(6, 13, 4, 2, cork);
                Save(p, d + "ware_vigor", PCenter);
                p = new Px(16, 16);
                Color32 brass = C(214, 170, 80), brassD = C(150, 108, 44), flame = C(255, 220, 120);
                p.Rect(4, 2, 8, 7, ol); p.Rect(5, 3, 6, 5, brass); p.Rect(5, 3, 6, 1, brassD); p.Rect(9, 4, 1, 3, brassD);
                p.Rect(11, 6, 3, 1, brassD); p.Rect(13, 7, 1, 2, brassD);                    // spout
                p.Rect(7, 9, 2, 2, brassD); p.Set(7, 12, flame); p.Set(8, 12, flame); p.Set(8, 13, C(255, 150, 60));
                Save(p, d + "ware_lampoil", PCenter);
            }
        }

        static void TrapAndPropArt()
        {
            const string d = "Sprites/Traps/";
            // arrow trap face (overlay on the trap block tile)
            foreach (var armed in new[] { true, false })
            {
                var p = new Px(16, 16);
                Color32 dk = C(24, 20, 22), eye = C(60, 54, 56);
                p.Rect(11, 6, 5, 4, dk);
                p.Rect(5, 10, 2, 2, eye); p.Rect(9, 10, 2, 2, eye);
                p.Rect(6, 4, 4, 1, eye);
                if (armed) { p.Set(14, 7, C(190, 190, 200)); p.Set(15, 7, C(230, 230, 240)); p.Set(14, 8, C(150, 150, 160)); }
                Save(p, d + (armed ? "arrowtrap_armed" : "arrowtrap_empty"), new Vector2(0.5f, 0f));
            }
            // stalactite
            {
                var p = new Px(16, 16);
                Color32 s = C(150, 122, 92), sd = C(104, 82, 60), sl = C(190, 162, 130);
                // (it comes down to a point - one pixel, a row clear of the sheet's edge so that the outline closes under it)
                for (int y = 2; y < 15; y++)
                {
                    int half = (int)(((y - 2) / 12f) * 4.2f);
                    for (int x = 8 - half - 1; x <= 7 + half + 1; x++) if (x >= 7 - half && x <= 8 + half) p.Set(x, y, x <= 7 ? sl : (x == 8 + half ? sd : s));
                }
                p.Set(7, 1, sl);
                p.Outline(C(50, 38, 28));
                Save(p, d + "stalactite", new Vector2(0.5f, 0f));
            }
            // crusher block + chain
            {
                var p = new Px(16, 16);
                Color32 m = C(92, 88, 100), md = C(56, 52, 62), ml = C(136, 132, 146), sp = C(200, 200, 212);
                p.Rect(0, 4, 16, 12, m);
                for (int i = 0; i < 16; i++) { p.Set(i, 15, ml); p.Set(0, i < 12 ? i + 4 : 15, ml); p.Set(15, i < 12 ? i + 4 : 15, md); p.Set(i, 4, md); }
                p.Set(3, 12, ml); p.Set(12, 12, ml); p.Set(3, 7, ml); p.Set(12, 7, ml);
                p.Rect(6, 8, 4, 4, md); p.Set(7, 10, C(255, 80, 60)); p.Set(8, 10, C(255, 80, 60));
                for (int i = 0; i < 4; i++)
                {
                    int cx = 2 + i * 4;
                    p.Set(cx, 0, sp); p.Rect(cx - 1, 1, 3, 1, sp); p.Rect(cx - 1, 2, 3, 2, sp);
                }
                Save(p, d + "crusher", new Vector2(0.5f, 0f));
                var ch = new Px(16, 8);
                for (int y = 0; y < 8; y++) { Color32 c = y % 4 < 2 ? C(140, 136, 150) : C(90, 86, 100); ch.Set(7, y, c); ch.Set(8, y, y % 4 == 0 ? C(60, 56, 66) : c); }
                Save(ch, d + "chain", PCenter);
            }
            // flame column frames
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(16, 32);
                for (int y = 0; y < 31; y++)
                {
                    float t = y / 31f;
                    float wob = Mathf.Sin((y + f * 5) * 0.6f) * 1.2f;
                    float halfW = Mathf.Lerp(5.5f, 1.2f, t) + (N(y, f, 5) - 0.5f) * 1.5f;
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = Mathf.Abs(x + 0.5f - 8f - wob);
                        if (dx > halfW) continue;
                        float core = dx / Mathf.Max(0.5f, halfW);
                        Color32 col = core < 0.35f ? C(255, 246, 180) : core < 0.7f ? C(255, 164, 48) : C(222, 66, 32);
                        if (t > 0.8f && N(x, y, f + 9) > 0.6f) continue;
                        p.Set(x, y, col);
                    }
                }
                Save(p, d + $"flame_{f}", new Vector2(0.5f, 0f));
            }

            const string pr = "Sprites/Props/";
            // doors (16x24, bottom pivot)
            for (int k = 0; k < 3; k++)
            {
                var p = new Px(16, 24);
                Color32 wood = C(124, 82, 46), woodD = C(80, 50, 26), woodL = C(168, 118, 70), inside = k == 0 ? C(14, 10, 16) : C(18, 26, 22);
                p.Rect(1, 0, 14, 20, wood);
                p.Disc(8, 18, 7f, wood);
                p.Rect(3, 0, 10, 18, inside);
                p.Disc(8, 17, 5f, inside);
                for (int y = 0; y < 20; y++) { p.Set(1, y, woodL); p.Set(14, y, woodD); }
                p.Rect(2, 0, 1, 18, woodD); p.Rect(13, 0, 1, 18, woodL);
                if (k == 0) { p.Set(12, 15, C(255, 200, 80)); p.Set(12, 14, C(255, 160, 50)); p.Set(12, 16, C(90, 70, 50)); }
                if (k >= 1)
                {
                    for (int y = 0; y < 16; y++) for (int x = 4; x < 12; x++) if (N(x, y, 3) > 0.92f) p.Set(x, y, C(80, 200, 120, 160));
                    Color32 ar = C(255, 220, 90);
                    p.Rect(6, 20, 4, 1, ar); p.Set(9, 21, ar); p.Set(9, 19, ar); p.Set(10, 20, ar);
                }
                if (k == 2)
                {
                    Color32 bar = C(150, 150, 166), barD = C(90, 90, 106);
                    for (int x = 4; x < 13; x += 2) p.Rect(x, 0, 1, 19, bar);
                    p.Rect(3, 9, 10, 1, barD);
                    p.Rect(7, 7, 2, 3, C(220, 180, 60));
                }
                p.Outline(C(30, 20, 14));
                Save(p, pr + (k == 0 ? "door_entrance" : k == 1 ? "door_exit" : "door_locked"), new Vector2(0.5f, 1f / 24f));
            }
            // stone gatehouse behind a door that stands in the open (outdoor surface): see Door.housingSprite
            {
                var p = new Px(34, 34);
                Color32 st = C(170, 152, 124), stD = C(126, 110, 88), stL = C(206, 190, 160), mortar = C(96, 82, 66);
                Color32 grass = C(92, 164, 68), grassL = C(142, 206, 94);
                for (int y = 0; y < 27; y++)
                    for (int x = 3; x < 31; x++)
                    {
                        int course = y / 5;
                        bool joint = y % 5 == 4 || (x + (course % 2) * 4) % 8 == 3;
                        p.Set(x, y, joint ? mortar : (y % 5 == 3 ? stL : (y % 5 == 0 ? stD : st)));
                    }
                for (int y = 0; y < 27; y++) { p.Set(3, y, stL); p.Set(30, y, stD); }
                p.Rect(1, 27, 32, 3, stL); p.Rect(1, 27, 32, 1, stD);          // lintel slab
                p.Rect(2, 30, 30, 1, grass);                                    // turf on the roof
                for (int x = 2; x < 32; x++) if (N(x, 1, 77) > 0.45f) p.Set(x, 31, grassL);
                for (int x = 4; x < 30; x++)                                    // moss hanging over the edge
                    if (N(x, 2, 91) > 0.7f) { p.Set(x, 29, grass); if (N(x, 3, 91) > 0.5f) p.Set(x, 28, grass); }
                p.Outline(C(30, 20, 14));
                Save(p, pr + "door_housing", new Vector2(0.5f, 1f / 34f));
            }
            {
                var p = new Px(16, 16);
                Color32 s = C(140, 136, 150), sd = C(90, 86, 100), sl = C(186, 182, 196);
                p.Rect(2, 0, 12, 2, sd); p.Rect(4, 2, 8, 3, s); p.Rect(3, 5, 10, 2, sl); p.Rect(4, 2, 1, 3, sl); p.Rect(11, 2, 1, 3, sd);
                p.Outline(C(30, 28, 36)); Save(p, pr + "altar", PItem);
            }
            {
                var p = new Px(16, 16);
                Color32 w = C(150, 104, 60), wd = C(100, 66, 36), tx = C(70, 44, 24);
                p.Rect(7, 0, 2, 8, wd); p.Rect(2, 7, 12, 7, w);
                for (int x = 2; x < 14; x++) { p.Set(x, 13, C(186, 136, 82)); p.Set(x, 7, wd); }
                p.Rect(4, 11, 8, 1, tx); p.Rect(4, 9, 6, 1, tx);
                p.Outline(C(30, 20, 14)); Save(p, pr + "sign", PItem);
            }
            {
                var p = new Px(16, 16);
                Color32 w = C(120, 40, 40), g = C(250, 210, 90);
                p.Rect(1, 4, 14, 10, w); p.Rect(1, 13, 14, 1, C(180, 70, 60)); p.Rect(7, 14, 2, 2, C(90, 60, 40));
                p.Rect(7, 6, 2, 7, g); p.Rect(5, 11, 5, 1, g); p.Rect(5, 9, 5, 1, g); p.Rect(6, 7, 5, 1, g); p.Set(5, 10, g); p.Set(10, 8, g);
                p.Outline(C(30, 14, 14)); Save(p, pr + "shopsign", PItem);
            }
        }

        // ======================================================================================= FX & UI

        static void FxArt()
        {
            var px = new Px(1, 1); px.Set(0, 0, C(255, 255, 255)); Save(px, "Center/pixel", PCenter);
            // soft round glow behind collectibles (tinted per type at runtime)
            {
                var gl = new Px(16, 16);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = (x - 7.5f) / 8f, dy = (y - 7.5f) / 8f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        if (a > 0f) gl.Set(x, y, C(255, 255, 255, (int)(Mathf.Pow(a, 1.1f) * 255)));
                    }
                Save(gl, "Center/glow", PCenter);
            }
            // bouncing "pick me up" arrow
            {
                var ar = new Px(9, 7);
                Color32 w = C(255, 255, 255), o = C(30, 22, 26);
                ar.Rect(1, 4, 7, 2, w); ar.Rect(2, 3, 5, 1, w); ar.Rect(3, 2, 3, 1, w); ar.Set(4, 1, w);
                ar.Outline(o);
                Save(ar, "Center/hint_arrow", PCenter);
            }
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(8, 8);
                float r = 1.5f + f * 0.8f;
                p.Disc(4, 4, r, C(220, 220, 220, 220 - f * 40));
                p.Disc(3.5f, 4.5f, r * 0.5f, C(255, 255, 255, 230 - f * 40));
                Save(p, $"Center/smoke_{f}", PCenter);
            }
            for (int f = 0; f < 6; f++)
            {
                var p = new Px(80, 80);
                switch (f)
                {
                    case 0: p.Disc(40, 40, 16, C(255, 200, 80)); p.Disc(40, 40, 11, C(255, 255, 220)); break;
                    case 1: p.Disc(40, 40, 28, C(240, 110, 40)); p.Disc(40, 40, 22, C(255, 190, 70)); p.Disc(40, 40, 13, C(255, 255, 230)); break;
                    case 2: p.Disc(40, 40, 36, C(200, 60, 30)); p.Disc(40, 40, 30, C(250, 140, 40)); p.Disc(40, 40, 20, C(255, 220, 110)); break;
                    case 3:
                        p.Disc(40, 40, 38, C(150, 50, 30)); p.Disc(40, 40, 32, C(230, 110, 40)); p.Disc(40, 40, 22, C(255, 170, 70));
                        for (int y = 0; y < 80; y++) for (int x = 0; x < 80; x++) if (N(x / 3, y / 3, 41) > 0.82f) p.Set(x, y, Clear);
                        break;
                    case 4:
                        p.Disc(40, 40, 39, C(90, 80, 80, 200)); p.Disc(40, 40, 30, Clear);
                        for (int y = 0; y < 80; y++) for (int x = 0; x < 80; x++) if (N(x / 4, y / 4, 43) > 0.6f) p.Set(x, y, Clear);
                        break;
                    case 5:
                        for (int y = 0; y < 80; y++) for (int x = 0; x < 80; x++)
                        {
                            float dx = x - 40, dy = y - 40, dd = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dd < 40 && dd > 26 && N(x / 4, y / 4, 47) > 0.75f) p.Set(x, y, C(110, 104, 104, 150));
                        }
                        break;
                }
                Save(p, $"Center/explosion_{f}", PCenter);
            }
        }

        static void UiArt()
        {
            WaypointArt();
            const string d = "UI/";
            {
                // the Field Guide: a bound book, a mountain stamped on its cover, a ribbon
                var p = new Px(16, 16);
                Color32 cover = C(126, 54, 50), coverD = C(84, 34, 36), coverL = C(170, 84, 70), page = C(238, 228, 200), pageD = C(196, 182, 150), gold = C(240, 200, 90), ol = C(30, 14, 16);
                p.Rect(3, 2, 10, 12, cover); p.Rect(3, 2, 2, 12, coverD); p.Rect(11, 3, 2, 10, coverL);
                p.Rect(5, 1, 8, 1, page); p.Rect(13, 3, 1, 10, page); p.Rect(5, 1, 8, 1, pageD);
                p.Set(8, 9, gold); p.Rect(7, 8, 3, 1, gold); p.Rect(6, 7, 5, 1, gold); p.Rect(6, 11, 5, 1, gold);
                p.Rect(9, 0, 1, 3, C(214, 60, 70));
                p.Outline(ol);
                Save(p, d + "icon_book", PCenter);
            }
            {
                // the strip behind a prompt tag: solid, then fading out to the right
                var p = new Px(64, 4);
                for (int x = 0; x < 64; x++)
                {
                    byte a = (byte)(x < 34 ? 255 : Mathf.RoundToInt(255f * Mathf.Pow(1f - (x - 34) / 30f, 1.5f)));
                    for (int y = 0; y < 4; y++) p.Set(x, y, new Color32(255, 255, 255, a));
                }
                Save(p, d + "tag_fade", PCenter);
            }
            { var p = new Px(12, 10); HeartShape(p, 1, 1, C(220, 40, 60), C(255, 150, 160), C(140, 20, 40)); p.Outline(C(30, 10, 14)); Save(p, d + "icon_heart", PCenter); }
            { var p = new Px(12, 12); p.Disc(5.5f, 5f, 4f, C(40, 40, 54)); p.Set(4, 7, C(130, 130, 150)); p.Set(3, 6, C(130, 130, 150)); p.Set(8, 9, C(140, 100, 60)); p.Set(9, 10, C(255, 200, 60)); p.Outline(C(10, 10, 16)); Save(p, d + "icon_bomb", PCenter); }
            {
                // the slingshot: a forked branch, a band between its tips, a pebble in the band
                var p = new Px(16, 16); Color32 wd = C(150, 104, 62), wdD = C(104, 70, 40), wdL = C(196, 150, 96), band = C(196, 64, 60), st = C(170, 164, 158);
                p.Rect(7, 0, 2, 7, wd); p.Rect(7, 0, 1, 7, wdD);                                   // the handle
                p.Line(7, 7, 4, 11, wd); p.Line(6, 7, 3, 11, wdD); p.Line(8, 7, 11, 11, wd); p.Line(9, 7, 12, 11, wdL);   // the fork
                p.Rect(3, 11, 2, 3, wd); p.Rect(11, 11, 2, 3, wd); p.Set(12, 13, wdL);
                p.Line(4, 13, 7, 10, band); p.Line(11, 13, 8, 10, band);                              // the band, drawn back
                p.Rect(7, 9, 2, 2, st); p.Set(8, 10, C(220, 214, 206));
                p.Outline(C(40, 26, 12)); Save(p, d + "icon_slingshot", PCenter);
            }
            { var p = new Px(12, 12); Color32 r = C(200, 160, 96), rd = C(130, 96, 50); p.Ellipse(6, 5, 4.5f, 3.5f, r); p.Ellipse(6, 5, 2.6f, 1.8f, rd); p.Ellipse(6, 5, 1.2f, 0.8f, r); p.Line(9, 8, 10, 10, r); p.Outline(C(40, 26, 12)); Save(p, d + "icon_rope", PCenter); }
            { var p = new Px(12, 12); p.Disc(6, 6, 4.5f, C(240, 196, 64)); p.Disc(6, 6, 3f, C(214, 166, 40)); p.Rect(5, 4, 2, 5, C(255, 236, 140)); p.Outline(C(80, 54, 10)); Save(p, d + "icon_gold", PCenter); }
            { var p = new Px(12, 12); p.Disc(6, 6, 4.5f, C(220, 220, 230)); p.Disc(6, 6, 3.4f, C(60, 64, 80)); p.Line(6, 6, 6, 9, C(255, 255, 255)); p.Line(6, 6, 8, 6, C(255, 255, 255)); p.Outline(C(20, 20, 30)); Save(p, d + "icon_clock", PCenter); }
            {
                var p = new Px(16, 16); Color32 g = C(255, 210, 80), gd = C(180, 130, 30);
                p.Rect(2, 7, 8, 2, g); for (int i = 0; i < 5; i++) { p.Rect(10 + i, 3 + i, 1, 10 - 2 * i, i == 0 ? gd : g); }
                p.Outline(C(40, 26, 6)); Save(p, d + "compass_arrow", PCenter);
            }
            {
                // Panel (9-slice, 8 px border): rounded bronze frame lit from the top-left, gold studs in the corners.
                const int n = 24;
                var p = new Px(n, n);
                Color32 fill = C(26, 20, 30, 240), fillTop = C(18, 14, 22, 240), outline = C(16, 11, 14);
                Color32 brL = C(204, 164, 102), br = C(156, 120, 76), brD = C(104, 78, 50), inner = C(12, 9, 13);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int ring = Ring(x, y, n, 3);
                        if (ring < 0) continue;
                        bool lit = Mathf.Min(x, n - 1 - y) <= Mathf.Min(n - 1 - x, y);   // nearest to the left / top edge
                        Color32 col = ring == 0 ? outline : ring == 1 ? (lit ? brL : br) : ring == 2 ? br : ring == 3 ? (lit ? brD : Mul(brD, 0.8f))
                            : ring == 4 ? inner : (ring == 5 && n - 1 - y == 5 ? fillTop : fill);
                        p.Set(x, y, col);
                    }
                foreach (int cx in new[] { 2, n - 5 })
                    foreach (int cy in new[] { 2, n - 5 })
                    {
                        p.Rect(cx, cy, 3, 3, C(236, 196, 104));
                        p.Set(cx, cy + 2, C(255, 240, 180)); p.Set(cx + 2, cy, C(160, 112, 46));
                    }
                Save(p, d + "panel", PCenter, new Vector4(8, 8, 8, 8));
            }
            {
                // Button (9-slice, tinted by the UI): rounded, light top edge, darker lower half and bottom edge.
                const int n = 16;
                var p = new Px(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int ring = Ring(x, y, n, 2);
                        if (ring < 0) continue;
                        int g = ring == 0 ? 44 : ring == 1 ? (n - 1 - y == 1 ? 255 : y == 1 ? 140 : 222) : (y == 2 ? 178 : y < n / 2 ? 208 : 240);
                        p.Set(x, y, C(g, g, g));
                    }
                Save(p, d + "button", PCenter, new Vector4(5, 5, 5, 5));
            }
            {
                // Slot (9-slice): only the rim takes the tint, the middle stays dark so an icon reads on top of it.
                const int n = 16;
                var p = new Px(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int ring = Ring(x, y, n, 3);
                        if (ring < 0) continue;
                        bool lit = Mathf.Min(x, n - 1 - y) <= Mathf.Min(n - 1 - x, y);
                        p.Set(x, y, ring == 0 ? C(24, 18, 24) : ring == 1 ? (lit ? C(255, 255, 255) : C(190, 190, 190)) : ring == 2 ? C(150, 150, 150)
                            : ring == 3 ? C(20, 16, 22, 245) : C(58, 50, 62, 235));
                    }
                Save(p, d + "slot", PCenter, new Vector4(5, 5, 5, 5));
            }
            LogoArt();
            MenuArt();
            MenuPointerArt();
            SoftPanelArt();
            GlyphArt();
            {
                var p = new Px(64, 64);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                    float dd = Mathf.Sqrt(dx * dx + dy * dy);
                    p.Set(x, y, C(255, 255, 255, (int)(Mathf.Clamp01((dd - 0.55f) / 0.45f) * 255)));
                }
                Save(p, d + "vignette", PCenter);
            }
        }

        /// <summary>Distance (in whole pixels) from the edge of an n x n rounded square; -1 outside the rounded corners.</summary>
        static int Ring(int x, int y, int n, int radius)
        {
            int dx = Mathf.Min(x, n - 1 - x), dy = Mathf.Min(y, n - 1 - y);
            if (dx < radius && dy < radius)
            {
                float ax = radius - dx - 0.5f, ay = radius - dy - 0.5f;
                float r = radius - Mathf.Sqrt(ax * ax + ay * ay);
                return r < 0f ? -1 : (int)r;
            }
            return Mathf.Min(dx, dy);
        }

        // ---- input glyphs: the pictures behind "[jump]", "[use]"... in UI texts (InputGlyphSet / GlyphText)

        static readonly Color32 KeyFace = C(236, 232, 224), KeyLip = C(150, 144, 150), KeyInk = C(44, 36, 48), KeyLine = C(30, 24, 32);
        static readonly Color32 PadBody = C(86, 82, 100), PadLight = C(132, 128, 150), PadInk = C(246, 242, 234);

        static int TextW(string s) => s.Length * 4 - 1;

        static void DrawText(Px p, string s, int x, int y, Color32 col)
        {
            foreach (char ch in s)
            {
                var g = Glyphs[ch];
                for (int r = 0; r < 5; r++)
                    for (int cx = 0; cx < 3; cx++)
                        if (g[r][cx] == '1') p.Set(x + cx, y + 4 - r, col);
                x += 4;
            }
        }

        /// <summary>5 x 5 arrow; dir 0 up, 1 right, 2 down, 3 left.</summary>
        static void DrawArrow(Px p, int ox, int oy, int dir, Color32 col)
        {
            int[,] up = { { 2, 4 }, { 1, 3 }, { 2, 3 }, { 3, 3 }, { 0, 2 }, { 1, 2 }, { 2, 2 }, { 3, 2 }, { 4, 2 }, { 2, 1 }, { 2, 0 } };
            for (int i = 0; i < up.GetLength(0); i++)
            {
                int x = up[i, 0], y = up[i, 1];
                int rx = dir == 0 ? x : dir == 1 ? y : dir == 2 ? x : 4 - y;
                int ry = dir == 0 ? y : dir == 1 ? x : dir == 2 ? 4 - y : x;
                p.Set(ox + rx, oy + ry, col);
            }
        }

        /// <summary>A keyboard key: light face, darker front lip, rounded corners. Returns the canvas (1 px margin).</summary>
        static Px KeyCap(int w, Color32 face, Color32 lip)
        {
            const int h = 10;
            var p = new Px(w + 2, h + 2);
            p.Rect(1, 1, w, h, lip);
            p.Rect(1, 2, w, h - 1, face);
            p.Rect(2, h, w - 2, 1, C(255, 255, 255));
            p.Set(1, 1, Clear); p.Set(w, 1, Clear); p.Set(1, h, Clear); p.Set(w, h, Clear);
            return p;
        }

        /// <summary>
        /// Blank button shapes for input prompts. The lettering is not drawn here: GlyphText writes it on top with
        /// the normal UI font so it stays sharp (labels live in Data/InputGlyphs.asset).
        /// </summary>
        static void GlyphArt()
        {
            {
                // keyboard key (9-slice: stretches for "Shift", "Enter"...)
                var p = KeyCap(10, KeyFace, KeyLip);
                p.Outline(KeyLine);
                Save(p, "UI/Glyphs/key", PCenter, new Vector4(4, 4, 4, 4));
            }
            {
                // shoulder / trigger / menu button (9-slice)
                var p = new Px(14, 12);
                p.Rect(1, 1, 12, 10, PadBody);
                p.Rect(2, 10, 10, 1, PadLight);
                p.Set(1, 1, Clear); p.Set(12, 1, Clear); p.Set(1, 10, Clear); p.Set(12, 10, Clear);
                p.Outline(KeyLine);
                Save(p, "UI/Glyphs/pill", PCenter, new Vector4(4, 4, 4, 4));
            }
            void Round(string name, Color32 col)
            {
                var p = new Px(13, 13);
                p.Disc(6f, 6f, 5f, Mul(col, 0.72f));
                p.Disc(6f, 6.5f, 4.3f, col);
                p.Set(3, 9, Lerp(col, C(255, 255, 255), 0.6f)); p.Set(4, 10, Lerp(col, C(255, 255, 255), 0.6f));
                p.Outline(KeyLine);
                Save(p, "UI/Glyphs/" + name, PCenter);
            }
            Round("pad_green", C(84, 176, 84)); Round("pad_blue", C(74, 124, 222)); Round("pad_red", C(214, 74, 74));
            Round("pad_yellow", C(236, 190, 56)); Round("pad_dark", PadBody);
        }

        /// <summary>Reads a sprite written earlier in this run back into a pixel buffer.</summary>
        /// <summary>
        /// The game's icon (window, taskbar, the .exe): the explorer on a mossy ledge in the mouth of a cave, lit
        /// from behind by a warm glow, in the same gold frame as the UI panels. Drawn at 64x64 and saved four times
        /// larger so it stays crisp. ProjectBuilder sets it as the player icon.
        /// </summary>
        static void AppIconArt()
        {
            const int S = 64;
            var p = new Px(S, S);
            // the dark of the cave
            p.Fill(C(18, 13, 26));
            // the glow behind the explorer: rings from deep red-brown to warm gold
            Color32[] glow = { C(34, 22, 36), C(52, 30, 40), C(78, 44, 44), C(112, 64, 46), C(150, 92, 52), C(196, 132, 64), C(236, 178, 92) };
            for (int i = 0; i < glow.Length; i++) p.Disc(32f, 31f, 27f - i * 3.4f, glow[i]);
            // the cave mouth: dark rock closing in from the corners and the top
            Color32 rock = C(40, 32, 52), rockD = C(26, 20, 36);
            for (int x = 0; x < S; x++)
            {
                int h = (int)(10 + 6 * Mathf.Sin(x * 0.33f) + 16f * Mathf.Abs(x - 31.5f) / 32f * Mathf.Abs(x - 31.5f) / 32f * 2.2f);
                p.Rect(x, S - h, 1, h, rock);
                if (h > 3) p.Rect(x, S - h, 1, 2, rockD);
            }
            for (int y = 0; y < S; y++)
            {
                int w = (int)(4 + 3 * Mathf.Sin(y * 0.4f) + (y > 40 ? (y - 40) * 0.5f : 0f));
                p.Rect(0, y, w, 1, rock); p.Rect(S - w, y, w, 1, rock);
            }
            // a stalactite or two
            p.Line(20, 54, 20, 47, rockD); p.Line(21, 55, 21, 49, rock); p.Line(44, 53, 44, 45, rockD); p.Line(45, 54, 45, 48, rock);
            // the ledge: earth with a mossy top
            Color32 dirt = C(110, 74, 48), dirtD = C(78, 50, 34), moss = C(84, 150, 62), mossL = C(146, 200, 90);
            p.Rect(0, 0, S, 13, dirt);
            for (int x = 0; x < S; x += 5) p.Set(x + (x / 5) % 3, 4 + (x / 5) % 5, dirtD);
            for (int x = 0; x < S; x += 7) p.Rect(x + 2, 8, 2, 1, dirtD);
            p.Rect(0, 12, S, 3, moss); p.Rect(0, 14, S, 1, mossL);
            for (int x = 1; x < S; x += 4) p.Set(x, 11 - (x / 4) % 2, moss);
            // the explorer, twice the size of the sprite, standing on the ledge
            var hero = Scale(LoadPx("Sprites/Player/idle_0"), 2);
            int ox = (S - hero.w) / 2, oy = 13;
            for (int y = 0; y < hero.h; y++)
                for (int x = 0; x < hero.w; x++)
                {
                    var c = hero.Get(x, y);
                    if (c.a > 0) p.Set(ox + x, oy + y, c);
                }
            // the gold frame of the UI
            Color32 gold = C(214, 170, 86), goldL = C(255, 226, 140), goldD = C(120, 84, 40), edge = C(20, 14, 20);
            p.Rect(0, 0, S, 1, edge); p.Rect(0, S - 1, S, 1, edge); p.Rect(0, 0, 1, S, edge); p.Rect(S - 1, 0, 1, S, edge);
            p.Rect(1, 1, S - 2, 2, gold); p.Rect(1, S - 3, S - 2, 2, gold); p.Rect(1, 1, 2, S - 2, gold); p.Rect(S - 3, 1, 2, S - 2, gold);
            p.Rect(1, S - 2, S - 2, 1, goldL); p.Rect(1, 1, 1, S - 2, goldL);
            p.Rect(1, 1, S - 2, 1, goldD); p.Rect(S - 2, 1, 1, S - 2, goldD);
            p.Rect(3, 3, S - 6, 1, edge); p.Rect(3, S - 4, S - 6, 1, edge); p.Rect(3, 3, 1, S - 6, edge); p.Rect(S - 4, 3, 1, S - 6, edge);
            foreach (var (cx, cy) in new[] { (1, 1), (S - 5, 1), (1, S - 5), (S - 5, S - 5) })
            {
                p.Rect(cx, cy, 4, 4, goldL); p.Rect(cx + 1, cy + 1, 2, 2, gold);
            }
            Save(Scale(p, 4), "UI/app_icon", PCenter);
        }

        static Px LoadPx(string rel)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Root + rel + ".png")));
            var p = new Px(tex.width, tex.height);
            var src = tex.GetPixels32();
            for (int i = 0; i < src.Length; i++) p.c[i] = src[i];
            Object.DestroyImmediate(tex);
            return p;
        }

        static readonly Dictionary<char, string[]> LogoGlyphs = new Dictionary<char, string[]>
        {
            ['G'] = new[] { ".#####.", "##...##", "##.....", "##.....", "##.####", "##...##", "##...##", "##...##", ".#####." },
            ['L'] = new[] { "##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "#######" },
            ['O'] = new[] { ".#####.", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####." },
            ['M'] = new[] { "##...##", "###.###", "#######", "##.#.##", "##...##", "##...##", "##...##", "##...##", "##...##" },
            ['D'] = new[] { "######.", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "######." },
            ['E'] = new[] { "#######", "##.....", "##.....", "##.....", "#####..", "##.....", "##.....", "##.....", "#######" },
            ['V'] = new[] { "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".##.##.", "..###..", "...#..." },
            ['B'] = new[] { "######.", "##...##", "##...##", "##...##", "######.", "##...##", "##...##", "##...##", "######." },
            ['R'] = new[] { "######.", "##...##", "##...##", "##...##", "######.", "##.##..", "##..##.", "##...##", "##...##" },
            ['I'] = new[] { "#######", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..", "#######" },
            ['N'] = new[] { "##...##", "###..##", "####.##", "##.####", "##..###", "##...##", "##...##", "##...##", "##...##" },
        };

        /// <summary>Title logo: chunky gold pixel letters with a dark outline and a drop shadow.</summary>
        static void LogoArt()
        {
            const string word = "BRINDLEMOOR";        // the series: the name of the hero's village
            const int S = 2, OX = 3, OY = 6, LH = 9 * S;
            var p = new Px(word.Length * 16 + 6, LH + 9);
            Color32 hi = C(255, 250, 204), light = C(255, 224, 120), mid = C(250, 184, 62), dark = C(206, 128, 40);
            for (int i = 0; i < word.Length; i++)
            {
                var g = LogoGlyphs[word[i]];
                for (int cy = 0; cy < 9; cy++)
                    for (int cx = 0; cx < 7; cx++)
                    {
                        if (g[cy][cx] != '#') continue;
                        for (int sy = 0; sy < S; sy++)
                            for (int sx = 0; sx < S; sx++)
                            {
                                int py = OY + (8 - cy) * S + sy;
                                float t = (py - OY) / (float)(LH - 1);
                                p.Set(OX + i * 16 + cx * S + sx, py, t > 0.66f ? light : t > 0.3f ? mid : dark);
                            }
                    }
            }
            var top = p.Copy();
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                    if (top.Opaque(x, y) && !top.Opaque(x, y + 1)) p.Set(x, y, hi);
            p.Outline(C(64, 32, 12));
            var q = new Px(p.w, p.h);
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                    if (p.Opaque(x, y)) { q.Set(x + 1, y - 2, C(10, 6, 8, 200)); q.Set(x, y - 2, C(10, 6, 8, 200)); }
            q.Blit(p, 0, 0);
            Save(q, "UI/logo", PCenter);
        }

        /// <summary>
        /// Title screen picture. It is its own illustration, finer than the in-game art (480 x 270 against the
        /// game's 16-pixel tiles): the menu does not have to look like a level.
        /// </summary>
        /// <summary>Where the animated scarf goes on the title picture (picture pixels, y up), and its frames.</summary>
        public static RectInt MenuScarfRect;
        public const int ScarfFrames = 6, ScarfW = 18, ScarfH = 12;
        public const int MenuArtW = 480, MenuArtH = 270;

        static void MenuArt()
        {
            // Title picture (480 x 270, not the in-game art): a winter dusk over the valley of Brindlemoor. Far off,
            // warm light spills out of a cave high on the great peak; the village lies dark and snowed in below it;
            // on a ledge in the foreground the traveller stands with a lantern, about to set out.
            const int W = 480, H = 270;
            var p = new Px(W, H);
            void Blend(int x, int y, Color32 col, float a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
                p.Set(x, y, Lerp(p.Get(x, y), col, Mathf.Clamp01(a)));
            }
            float Ridge(float x, float f1, float f2, int seed) =>
                Mathf.Sin(x * f1 + seed) * 0.5f + Mathf.Sin(x * f2 + seed * 1.7f) * 0.3f + (N((int)(x / 3f), seed, 17) - 0.5f) * 0.25f;

            // ---- sky: cold gradient with soft dither, stars
            float[] stops = { 70f, 120f, 170f, 220f, 270f };
            Color32[] cols = { C(96, 112, 150), C(62, 74, 124), C(36, 44, 96), C(20, 24, 66), C(10, 12, 40) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float yy = Mathf.Clamp(y + (N(x, y, 5) - 0.5f) * 5f, stops[0], stops[4] - 0.01f);
                    int k = 0;
                    while (yy >= stops[k + 1]) k++;
                    Color32 col = Lerp(cols[k], cols[k + 1], (yy - stops[k]) / (stops[k + 1] - stops[k]));
                    if (y > 150 && N(x, y, 11) > 0.9955f) col = N(x, y, 3) > 0.5f ? C(255, 250, 236) : C(190, 204, 240);
                    p.Set(x, y, col);
                }
            // a pale, heatless sun behind thin cloud
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x - 58f) * (x - 58f) + (y - 178f) * (y - 178f));
                    if (d < 13f) p.Set(x, y, C(226, 232, 246));
                    else if (d < 46f) Blend(x, y, C(170, 186, 226), (1f - (d - 13f) / 33f) * 0.42f);
                }
            // cloud banks: long flat blobs, lit along the top
            for (int i = 0; i < 26; i++)
            {
                float cx = N(i, 1, 71) * W, cy = 150f + N(i, 2, 71) * 96f, rx = 30f + N(i, 3, 71) * 60f, ry = 4f + N(i, 4, 71) * 7f;
                for (int y = (int)(cy - ry); y <= cy + ry; y++)
                    for (int x = (int)(cx - rx); x <= cx + rx; x++)
                    {
                        float u = (x - cx) / rx, v = (y - cy) / ry;
                        float e = u * u + v * v + (N(x / 3, y / 2, 23) - 0.5f) * 0.5f;
                        if (e < 1f) Blend(x, y, v > 0.2f ? C(120, 134, 180) : C(52, 60, 108), 0.34f * (1f - e));
                    }
            }

            // ---- the far range, and the great peak with the lit cave
            const float PX = 352f;                      // the peak's x
            int[] far = new int[W];
            for (int x = 0; x < W; x++)
            {
                float peak = Mathf.Max(0f, 1f - Mathf.Abs(x - PX) / 118f);
                far[x] = (int)(118f + (Mathf.Sin(x * 0.021f + 3f) * 0.5f + Mathf.Sin(x * 0.057f + 5.1f) * 0.3f + Mathf.Sin(x * 0.13f + 1.3f) * 0.12f) * 16f
                               + peak * peak * 104f + Mathf.Max(0f, 1f - Mathf.Abs(x - 120f) / 90f) * 26f);
            }
            for (int x = 0; x < W; x++)
                for (int y = 60; y <= far[x]; y++)
                {
                    int depth = far[x] - y;
                    // slopes that rise toward the right face the pale sun on the left: lit; the others lie in shade
                    float slope = far[Mathf.Clamp(x + 7, 0, W - 1)] - far[Mathf.Clamp(x - 7, 0, W - 1)];
                    float light = Mathf.Clamp01(0.5f + slope / 18f);
                    Color32 rock = Lerp(C(34, 40, 80), C(60, 70, 116), light);
                    // snow: caps the tops, deeper on the lit side; its lower edge is ragged, not striped
                    float snowLine = Mathf.Lerp(10f, 30f, light) + Mathf.Sin(x * 0.31f + depth * 0.2f) * 3f + (N(x / 2, y / 2, 31) - 0.5f) * 5f;
                    Color32 col = depth < snowLine ? Lerp(C(112, 128, 178), C(204, 216, 240), light) : rock;
                    // haze: the range fades into the valley air toward its foot
                    col = Lerp(col, C(46, 56, 104), Mathf.Clamp01(depth / 150f) * 0.75f);
                    p.Set(x, y, col);
                }
            // the cave: a mouth of warm light high on the peak, its glow on the snow around it and in the air above
            const int CX = 350, CY = 176;
            for (int y = CY - 76; y <= CY + 76; y++)       // the whole circle of the glow, so it has no cut edge
                for (int x = CX - 70; x <= CX + 70; x++)
                {
                    float d = Mathf.Sqrt((x - CX) * (x - CX) + (y - CY) * (y - CY) * 0.8f);
                    if (d < 62f) Blend(x, y, C(255, 196, 96), (1f - d / 62f) * (1f - d / 62f) * 0.62f);
                }
            for (int i = 0; i < 9; i++)             // rays fanning up out of the mouth
            {
                float a = Mathf.PI * (0.18f + i * 0.08f);
                for (float r = 10f; r < 78f; r += 0.5f)
                    Blend(CX + (int)(Mathf.Cos(a) * r), CY + 4 + (int)(Mathf.Sin(a) * r), C(255, 224, 150), 0.16f * (1f - r / 78f));
            }
            p.Ellipse(CX, CY, 9f, 6.5f, C(255, 214, 120)); p.Ellipse(CX, CY - 1f, 6.5f, 4.2f, C(255, 244, 196));
            p.Rect(CX - 9, CY - 8, 19, 3, C(44, 36, 60)); p.Line(CX - 10, CY - 6, CX - 7, CY + 2, C(44, 36, 60)); p.Line(CX + 10, CY - 6, CX + 7, CY + 2, C(44, 36, 60));

            // ---- nearer hills with pines, falling to the valley
            int[] mid = new int[W];
            for (int x = 0; x < W; x++) mid[x] = (int)(92f + Ridge(x, 0.03f, 0.09f, 9) * 14f + Mathf.Max(0f, 1f - Mathf.Abs(x - 450f) / 80f) * 30f);
            for (int x = 0; x < W; x++)
                for (int y = 40; y <= mid[x]; y++)
                {
                    int depth = mid[x] - y;
                    Color32 col = depth < 4 + (int)(N(x, 5, 41) * 4) ? C(150, 168, 214) : Lerp(C(30, 38, 78), C(22, 28, 60), Mathf.Clamp01(depth / 50f));
                    p.Set(x, y, col);
                }
            for (int i = 0; i < 46; i++)
            {
                int x = (int)(N(i, 7, 53) * W);
                if (x > 290 && x < 410) continue;       // the village stands in the gap
                int by = mid[Mathf.Clamp(x, 0, W - 1)] - 2, h = 9 + (int)(N(i, 8, 53) * 9);
                for (int y = 0; y < h; y++)
                {
                    int hw = Mathf.Max(0, (h - y) * 4 / h - (y % 3 == 0 ? 0 : 1));
                    p.Rect(x - hw, by + y, hw * 2 + 1, 1, y % 3 == 2 ? C(160, 176, 216) : C(16, 24, 46));
                }
            }

            // ---- the valley floor: snowfields in bands, a frozen stream
            for (int y = 0; y < 78; y++)
                for (int x = 0; x < W; x++)
                {
                    if (y > mid[x] - 12 + (int)(Mathf.Sin(x * 0.05f) * 4f) - 20) continue;
                    float t = y / 78f;
                    Color32 col = Lerp(C(64, 78, 132), C(132, 150, 204), t);
                    float band = Mathf.Sin(x * 0.035f + y * 0.21f + Mathf.Sin(y * 0.3f) * 1.2f);
                    if (band > 0.72f) col = Lerp(col, C(190, 204, 238), 0.5f);
                    else if (band < -0.8f) col = Lerp(col, C(40, 52, 100), 0.4f);
                    if (N(x, y, 61) > 0.997f) col = C(226, 234, 250);        // a rare glint, not a field of dots
                    p.Set(x, y, col);
                }
            for (int x = 150; x < W; x++)           // the stream, catching the cave light
            {
                int sy = 30 + (int)(Mathf.Sin(x * 0.03f) * 9f + Mathf.Sin(x * 0.11f) * 2f);
                for (int dy = 0; dy < 3; dy++) Blend(x, sy + dy, x > 300 ? C(255, 206, 130) : C(170, 196, 240), 0.5f);
            }

            // ---- Brindlemoor: a handful of snowed-in houses, a few windows still lit.
            // They stand on a broad rise of snow at the foot of the peak, so every house has ground under it.
            float Rise(int x) => 52f + 22f * Mathf.Max(0f, Mathf.Cos((x - 350f) / 78f * Mathf.PI * 0.5f)) + Mathf.Sin(x * 0.19f) * 1.2f;
            for (int x = 268; x < 432; x++)
            {
                int top = (int)Rise(x);
                for (int y = 24; y <= top; y++)
                {
                    int depth = top - y;
                    // lit from the cave above on the right, in shade toward the left foot
                    float side = Mathf.Clamp01((x - 268f) / 164f);
                    Color32 col = Lerp(Lerp(C(84, 100, 158), C(126, 142, 196), side), C(150, 166, 214), Mathf.Clamp01(1f - depth / 26f) * 0.6f);
                    if (depth < 2) col = C(214, 224, 244);                                     // the crest
                    else if (Mathf.Sin(x * 0.11f + y * 0.37f) > 0.86f) col = Lerp(col, C(196, 208, 238), 0.45f);   // drifts
                    // fade the foot of the rise into the snowfield so it has no hard lower edge
                    // ...and its two ends melt into the valley the same way (no seam where the rise begins)
                    float melt = Mathf.Clamp01((y - 24) / 10f) * Mathf.Clamp01((x - 268) / 22f) * Mathf.Clamp01((432 - x) / 22f);
                    if (depth < 2 && melt > 0.5f) melt = 1f;
                    p.Set(x, y, Lerp(p.Get(x, y), col, melt));
                }
            }
            void House(int x, int y, int w, int h, bool lit)
            {
                // the ground it stands on: a shadow thrown to the left, snow banked against the wall
                for (int i = 0; i < w + 5; i++) Blend(x - 5 + i, y - 1, C(40, 48, 96), 0.55f);
                for (int i = 0; i < 4; i++) Blend(x - 4 + i, y, C(40, 48, 96), 0.4f);
                p.Rect(x, y, w, h, C(40, 36, 58)); p.Rect(x, y, 1, h, C(28, 24, 44));
                p.Rect(x - 1, y - 1, w + 3, 1, C(206, 218, 242)); p.Set(x + w, y, C(206, 218, 242)); p.Set(x + w + 1, y - 1, C(206, 218, 242));
                for (int i = 0; i <= w / 2 + 1; i++) p.Rect(x - 1 + i, y + h + i, w + 2 - i * 2, 1, i == 0 ? C(32, 28, 50) : C(214, 224, 244));    // roof under snow
                p.Rect(x + w - 3, y + h + 1, 2, 4, C(40, 36, 58));                                                                               // chimney
                if (lit)
                {
                    p.Rect(x + 2, y + 2, 2, 2, C(255, 206, 116));
                    if (w > 8) p.Rect(x + w - 5, y + 2, 2, 2, C(255, 190, 96));
                    for (int gy = -3; gy <= 5; gy++) for (int gx = -3; gx <= w + 2; gx++) Blend(x + gx, y + gy, C(255, 196, 110), 0.1f);
                    for (int s = 0; s < 14; s++) Blend(x + w - 2 + (int)(Mathf.Sin(s * 0.5f) * 2f) + s / 3, y + h + 5 + s, C(170, 180, 214), 0.35f * (1f - s / 14f));
                }
            }
            // back row first (higher on the rise), then the bell tower, then the front row over them
            House(318, 60, 9, 6, false); House(336, 63, 11, 6, true); House(362, 62, 9, 5, false); House(380, 58, 10, 6, true);
            p.Rect(351, 64, 3, 13, C(34, 30, 52)); p.Rect(350, 77, 5, 2, C(214, 224, 244)); p.Set(352, 73, C(255, 206, 116));     // the little bell tower
            for (int i = 0; i < 7; i++) Blend(346 + i, 63, C(40, 48, 96), 0.5f);
            House(300, 46, 11, 6, true); House(326, 44, 9, 6, false); House(344, 48, 12, 7, true); House(370, 45, 10, 6, false);
            House(392, 47, 9, 5, true);

            // ---- foreground: the ledge on the left, dark rock under snow
            int[] ledge = new int[W];
            for (int x = 0; x < W; x++)
                ledge[x] = x < 176 ? (int)(100f - x * 0.04f + Ridge(x, 0.05f, 0.17f, 21) * 5f - Mathf.Max(0f, x - 140f) * 1.9f) : -1;
            for (int x = 0; x < 176; x++)
                for (int y = 0; y <= ledge[x]; y++)
                {
                    int depth = ledge[x] - y;
                    Color32 col = depth < 4 ? C(208, 220, 244) : depth < 6 ? C(126, 144, 196)
                        : Lerp(C(34, 30, 54), C(16, 14, 30), Mathf.Clamp01(depth / 70f));
                    if (depth >= 6 && (N(x / 4, y / 3, 83) > 0.78f)) col = Lerp(col, C(54, 50, 84), 0.6f);
                    p.Set(x, y, col);
                }
            // the right-hand corner: a dark spur and a leaning pine, framing the picture
            for (int x = 400; x < W; x++)
            {
                int top = (int)(54f + (x - 400) * 0.75f + Ridge(x, 0.07f, 0.2f, 33) * 5f);
                for (int y = 0; y <= top; y++)
                {
                    int depth = top - y;
                    p.Set(x, y, depth < 3 ? C(196, 208, 238) : Lerp(C(30, 26, 50), C(14, 12, 28), Mathf.Clamp01(depth / 60f)));
                }
            }
            for (int y = 0; y < 86; y++)
            {
                int tx = 452 + y / 9, hw = Mathf.Max(0, (86 - y) / 6 - (y % 7 < 3 ? 2 : 0));
                p.Rect(tx - hw, 96 + y, hw * 2 + 1, 1, y % 7 == 3 ? C(170, 186, 226) : C(12, 18, 34));
            }
            p.Rect(455, 60, 4, 40, C(20, 16, 28));

            // ---- the traveller on the ledge, seen from behind, lantern in hand (drawn for this picture, 2x the sprite's scale)
            {
                int fx = 96, fy = ledge[96] + 1;
                Color32 cloak = C(40, 96, 110), cloakD = C(24, 62, 78), cloakL = C(70, 140, 150), hat = C(126, 84, 46), hatD = C(84, 54, 30), scarf = C(214, 62, 56), scarfD = C(150, 36, 40), boot = C(52, 36, 30), skin = C(232, 190, 150);
                // lantern glow first, on the snow and the air around
                for (int y = -18; y <= 22; y++)
                    for (int x = -26; x <= 26; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d < 26f) Blend(fx + 12 + x, fy + 8 + y, C(255, 200, 110), (1f - d / 26f) * 0.5f);
                    }
                p.Rect(fx - 4, fy, 4, 5, boot); p.Rect(fx + 2, fy, 4, 5, boot);
                for (int y = 5; y < 26; y++)             // the cloak: wide at the hem, narrow at the shoulders
                {
                    int hw = 8 - (y - 5) * 4 / 21;
                    p.Rect(fx - hw, fy + y, hw * 2 + 1, 1, cloak);
                    p.Set(fx - hw, fy + y, cloakD); p.Set(fx + hw, fy + y, cloakL);
                }
                p.Line(fx - 2, fy + 6, fx - 1, fy + 22, cloakD); p.Line(fx + 3, fy + 6, fx + 2, fy + 20, cloakD);
                p.Rect(fx - 3, fy + 26, 7, 5, skin); p.Rect(fx - 3, fy + 26, 7, 2, hatD);                         // neck and the back of the head
                p.Rect(fx - 8, fy + 30, 17, 2, hat); p.Rect(fx - 4, fy + 32, 9, 4, hat); p.Rect(fx - 4, fy + 32, 9, 1, hatD); p.Rect(fx - 8, fy + 30, 17, 1, hatD);
                p.Rect(fx - 4, fy + 24, 9, 3, scarf);                                                              // scarf round the neck
                // Its loose ends are not painted here: they blow in the wind as an animation laid over the picture
                // (the frames below, shown by MenuSpriteAnim). The region they cover, in picture pixels:
                MenuScarfRect = new RectInt(fx - 5 - ScarfW, fy + 24 - ScarfH / 2 + 1, ScarfW, ScarfH);
                for (int f = 0; f < ScarfFrames; f++)
                {
                    var sp = new Px(ScarfW, ScarfH);
                    float phase = f * Mathf.PI * 2f / ScarfFrames;
                    for (int i = 0; i < 15; i++)
                    {
                        // a wave that runs out along the scarf and grows toward its free end; the end also lifts and dips
                        float k = i / 14f;
                        // (a long, soft wave: cloth, not a whip; three pixels thick at the neck, thinning to one at the tip)
                        float wave = Mathf.Sin(i * 0.4f - phase) * (0.3f + 2.2f * k) + Mathf.Sin(phase) * 1.0f * k;
                        int thick = i < 5 ? 3 : i < 11 ? 2 : 1;
                        int y = ScarfH / 2 - 1 + Mathf.RoundToInt(wave);
                        int x = ScarfW - 1 - i;
                        sp.Rect(x, y, 1, thick, scarf);
                        sp.Set(x, y, scarfD);                                                // shaded underside: it reads as a band
                        if (i == 14) sp.Set(x - 0, y + (f % 2 == 0 ? 1 : -1), scarfD);      // the frayed tip flicks
                    }
                    Save(sp, $"UI/menu_scarf_{f}", PCenter);
                }
                p.Rect(fx + 8, fy + 14, 5, 2, cloakL);                                                             // the arm holding the lantern out
                p.Rect(fx + 12, fy + 6, 1, 8, C(60, 50, 44));
                p.Rect(fx + 10, fy + 2, 5, 5, C(255, 226, 140)); p.Rect(fx + 11, fy + 3, 3, 3, C(255, 250, 220)); p.Rect(fx + 10, fy + 7, 5, 1, C(90, 70, 40)); p.Rect(fx + 10, fy + 1, 5, 1, C(90, 70, 40));
            }

            // (no snow is painted in: it falls for real over the picture - MenuSnow)
            Save(p, "UI/menu_bg", PCenter);
        }

        /// <summary>
        /// Title-screen panels: a dark, slightly see-through sheet with a thin gold line (no heavy frame), and the
        /// strip used for its buttons and rows (white, tinted by the button's colours). Both are 9-sliced.
        /// </summary>
        static void SoftPanelArt()
        {
            {
                const int S = 24;
                var p = new Px(S, S);
                Color32 fill = C(9, 11, 28, 240), line = C(214, 176, 96, 215), lineD = C(120, 92, 50, 190);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        bool corner = (x < 2 || x >= S - 2) && (y < 2 || y >= S - 2) && !((x == 1 || x == S - 2) && (y == 1 || y == S - 2));
                        if (corner) continue;                                   // clipped corners
                        bool edge = x == 1 || y == 1 || x == S - 2 || y == S - 2 || ((x == 2 || x == S - 3) && (y == 2 || y == S - 3));
                        bool outer = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                        p.Set(x, y, outer ? C(4, 5, 14, 150) : edge ? (y <= 2 || x <= 2 ? line : lineD) : fill);
                    }
                Save(p, "UI/panel_soft", PCenter, new Vector4(8, 8, 8, 8));
            }
            {
                const int S = 16;
                var p = new Px(S, S);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        bool corner = (x == 0 || x == S - 1) && (y == 0 || y == S - 1);
                        if (corner) continue;
                        bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                        p.Set(x, y, edge ? C(255, 255, 255, 255) : C(255, 255, 255, 150));
                    }
                Save(p, "UI/row_soft", PCenter, new Vector4(5, 5, 5, 5));
            }
            {
                // an item slot in the same look: dark sheet, a thin line that takes the Image's colour
                // (white = chosen, gold = worn / in use, grey = the rest)
                const int S = 16;
                var p = new Px(S, S);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        bool corner = (x == 0 || x == S - 1) && (y == 0 || y == S - 1);
                        if (corner) continue;
                        bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                        p.Set(x, y, edge ? C(255, 255, 255, 255) : C(26, 30, 62, 215));
                    }
                Save(p, "UI/slot_soft", PCenter, new Vector4(5, 5, 5, 5));
            }
            {
                // a soft round shade (white, tinted by the Image): laid behind the title-screen list so the
                // words do not sink into the bright snow of the picture
                const int S = 64;
                var p = new Px(S, S);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x + 0.5f - S * 0.5f) / (S * 0.5f), dy = (y + 0.5f - S * 0.5f) / (S * 0.5f);
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        p.Set(x, y, C(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f)));
                    }
                Save(p, "UI/menu_shade", PCenter, Vector4.zero);
            }
        }

        /// <summary>The marker beside the menu entry in focus: a small gold arrowhead.</summary>
        static void MenuPointerArt()
        {
            var p = new Px(14, 11);
            Color32 g = C(255, 214, 110), gl = C(255, 246, 196), gd = C(196, 128, 40);
            for (int y = 0; y < 11; y++)
            {
                int w = 12 - Mathf.Abs(y - 5) * 2;
                if (w > 0) p.Rect(1, y, w, 1, y > 5 ? gl : y == 5 ? g : gd);
            }
            p.Rect(1, 4, 4, 3, g);
            p.Outline(C(40, 24, 10));
            Save(p, "UI/menu_pointer", PCenter);
        }

        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['0'] = new[] { "111", "101", "101", "101", "111" }, ['1'] = new[] { "010", "110", "010", "010", "111" },
            ['2'] = new[] { "111", "001", "111", "100", "111" }, ['3'] = new[] { "111", "001", "111", "001", "111" },
            ['4'] = new[] { "101", "101", "111", "001", "001" }, ['5'] = new[] { "111", "100", "111", "001", "111" },
            ['6'] = new[] { "111", "100", "111", "101", "111" }, ['7'] = new[] { "111", "001", "010", "010", "010" },
            ['8'] = new[] { "111", "101", "111", "101", "111" }, ['9'] = new[] { "111", "101", "111", "001", "111" },
            ['A'] = new[] { "010", "101", "111", "101", "101" }, ['B'] = new[] { "110", "101", "110", "101", "110" },
            ['C'] = new[] { "011", "100", "100", "100", "011" }, ['D'] = new[] { "110", "101", "101", "101", "110" },
            ['E'] = new[] { "111", "100", "110", "100", "111" }, ['F'] = new[] { "111", "100", "110", "100", "100" },
            ['G'] = new[] { "011", "100", "101", "101", "011" }, ['H'] = new[] { "101", "101", "111", "101", "101" },
            ['I'] = new[] { "111", "010", "010", "010", "111" }, ['J'] = new[] { "001", "001", "001", "101", "010" },
            ['K'] = new[] { "101", "101", "110", "101", "101" }, ['L'] = new[] { "100", "100", "100", "100", "111" },
            ['M'] = new[] { "101", "111", "111", "101", "101" }, ['N'] = new[] { "110", "101", "101", "101", "101" },
            ['O'] = new[] { "010", "101", "101", "101", "010" }, ['P'] = new[] { "110", "101", "110", "100", "100" },
            ['Q'] = new[] { "010", "101", "101", "110", "011" }, ['R'] = new[] { "110", "101", "110", "101", "101" },
            ['S'] = new[] { "011", "100", "010", "001", "110" }, ['T'] = new[] { "111", "010", "010", "010", "010" },
            ['U'] = new[] { "101", "101", "101", "101", "111" }, ['V'] = new[] { "101", "101", "101", "101", "010" },
            ['W'] = new[] { "101", "101", "111", "111", "101" }, ['X'] = new[] { "101", "101", "010", "101", "101" },
            ['Y'] = new[] { "101", "101", "010", "010", "010" }, ['Z'] = new[] { "111", "001", "010", "100", "111" },
            ['$'] = new[] { "011", "110", "010", "011", "110" }, ['+'] = new[] { "000", "010", "111", "010", "000" },
            ['-'] = new[] { "000", "000", "111", "000", "000" }, ['!'] = new[] { "010", "010", "010", "000", "010" },
            ['?'] = new[] { "110", "001", "010", "000", "010" }, ['.'] = new[] { "000", "000", "000", "000", "010" },
            [':'] = new[] { "000", "010", "000", "010", "000" }, ['/'] = new[] { "001", "001", "010", "100", "100" },
            [' '] = new[] { "000", "000", "000", "000", "000" },
        };

        public const string FontChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ$+-!?.:/ ";

        static void FontArt()
        {
            for (int i = 0; i < FontChars.Length; i++)
            {
                var g = Glyphs[FontChars[i]];
                var p = new Px(5, 7);
                for (int r = 0; r < 5; r++)
                    for (int x = 0; x < 3; x++)
                        if (g[r][x] == '1') p.Set(1 + x, 1 + (4 - r), C(255, 255, 255));
                p.Outline(C(20, 16, 20));
                Save(p, $"Font/glyph_{i:00}", PCenter);
            }
        }

        // ======================================================================================= preview

        /// <summary>Writes a contact sheet of all generated art (4x scale) for quick visual review.</summary>
        public static void WritePreview(string outPath)
        {
            var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), Root), "*.png", SearchOption.AllDirectories);
            System.Array.Sort(files);
            const int cell = 84, cols = 16, scale = 4;
            int rows = (files.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(70, 60, 80, 255);
            sheet.SetPixels32(bg);
            for (int i = 0; i < files.Length; i++)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(files[i]));
                int s = Mathf.Max(1, Mathf.Min(scale, (cell - 4) / Mathf.Max(t.width, t.height)));
                int cx = (i % cols) * cell + 2, cy = (rows - 1 - i / cols) * cell + 2;
                for (int y = 0; y < t.height; y++)
                    for (int x = 0; x < t.width; x++)
                    {
                        var c = t.GetPixel(x, y);
                        if (c.a <= 0.01f) continue;
                        for (int yy = 0; yy < s; yy++)
                            for (int xx = 0; xx < s; xx++)
                            {
                                int px = cx + x * s + xx, py = cy + y * s + yy;
                                if (px < sheet.width && py < sheet.height)
                                {
                                    var under = sheet.GetPixel(px, py);
                                    sheet.SetPixel(px, py, Color.Lerp(under, c, c.a));
                                }
                            }
                    }
                Object.DestroyImmediate(t);
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
