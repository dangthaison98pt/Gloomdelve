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
    public static class ArtGenerator
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

        static void Save(Px p, string rel, Vector2 pivot, Vector4 border = default)
        {
            if (rel.StartsWith("Sprites/Player/") || rel.StartsWith("Sprites/Enemies/") || rel.StartsWith("Sprites/Items/")) p = AutoShade(p);
            string path = Root + rel + ".png";
            p.Save(Path.Combine(Directory.GetCurrentDirectory(), path));
            ArtImport.Pending[path] = new ArtImport.Settings { pivot = pivot, border = border };
            written.Add(path);
        }

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
            BossArt();
            FxArt();
            UiArt();
            FontArt();
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

        static void AllTiles()
        {
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
                    Save(p.style == 2 ? Brick(m, v, p, seed) : Earth(m, v, p, seed), $"{dir}dirt_{m:00}_{v}", PCenter);
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
            // sunset bands from the horizon up; quantised so the sky reads as pixel art, not a smooth ramp
            Color32[] stops = { C(255, 206, 132), C(250, 160, 120), C(214, 124, 148), C(150, 110, 176), C(92, 92, 168), C(52, 68, 140), C(34, 46, 104) };
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
                    if (dd < 9f) sun.Set(x, y, C(255, 250, 214));
                    else if (dd < 11f) sun.Set(x, y, C(255, 232, 160));
                    else if (dd < 16f) sun.Set(x, y, C(255, 220, 150, 110));
                    else if (dd < 23f) sun.Set(x, y, C(255, 210, 150, 46));
                }
            Save(sun, d + "sun", PCenter);

            var range = new Px(128, 40);
            Color32 rg = C(168, 128, 172), rgL = C(206, 160, 184);
            for (int x = 0; x < 128; x++)
            {
                float hgt = 14f + 12f * Mathf.Abs(Mathf.Sin(x * 0.0736f + 0.6f)) + 6f * Mathf.Abs(Mathf.Sin(x * 0.196f + 2.1f));
                int h = Mathf.Min(39, (int)hgt);
                for (int y = 0; y < h; y++) range.Set(x, y, y > h - 4 ? rgL : rg);
            }
            Save(range, d + "range", PCenter);

            var far = new Px(128, 44);
            Color32 m = C(124, 110, 164), mL = C(170, 156, 200), snow = C(236, 226, 240);
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
                Color32 c = C(255, 240, 232, 225), cs = C(240, 190, 190, 225), hl = C(255, 252, 244, 235);
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
            // Wraith (24x24)
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(24, 24);
                Color32 body = C(186, 228, 240, 210), bodyD = C(120, 170, 200, 200), hole = C(20, 30, 50, 230);
                p.Disc(12, 15, 6.5f, body);
                p.Rect(6, 4, 13, 11, body);
                for (int x = 6; x < 19; x++)
                {
                    int t = ((x + f * 2) % 4 < 2) ? 3 : 1;
                    for (int y = 1; y < 4 + (t == 1 ? 0 : 0); y++) if (y >= t) p.Set(x, y, body);
                }
                for (int y = 4; y < 14; y++) { p.Set(6, y, bodyD); p.Set(18, y, bodyD); }
                p.Rect(8, 14, 3, 3, hole); p.Rect(14, 14, 3, 3, hole);
                p.Ellipse(12.5f, 9.5f, 2f, 1.6f + f * 0.4f, hole);
                p.Outline(C(80, 120, 160, 160));
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

        static Px Scale(Px s, int n)
        {
            var p = new Px(s.w * n, s.h * n);
            for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++) p.c[y * p.w + x] = s.c[(y / n) * s.w + x / n];
            return p;
        }

        static void Tint(Px p, float r, float g, float b)
        {
            for (int i = 0; i < p.c.Length; i++)
            {
                var c = p.c[i];
                if (c.a == 0) continue;
                p.c[i] = new Color32((byte)Mathf.Min(255, c.r * r), (byte)Mathf.Min(255, c.g * g), (byte)Mathf.Min(255, c.b * b), c.a);
            }
        }

        /// <summary>
        /// Bosses are the area's signature creature at double size with their own colours (and a crown for the
        /// yeti king). Also the boss projectile and the treasure chest.
        /// </summary>
        static void BossArt()
        {
            const string e = "Sprites/Enemies/", o = "Sprites/Bosses/";
            void Big(string src, string dst, float r, float g, float b, System.Action<Px> extra = null, bool centre = false)
            {
                var p = Scale(LoadPx(e + src), 2);
                Tint(p, r, g, b);
                extra?.Invoke(p);
                Save(p, o + dst, centre ? PCenter : new Vector2(0.5f, 2f / p.h));
            }
            Big("spider_0", "broodmother_0", 1.9f, 1.35f, 2.1f); Big("spider_1", "broodmother_1", 1.9f, 1.35f, 2.1f);
            for (int f = 0; f < 4; f++) Big($"brute_{f}", $"forgebrute_{f}", 1.25f, 0.72f, 0.6f);
            void Crown(Px p)
            {
                int top = 0;
                for (int y = p.h - 1; y >= 0 && top == 0; y--)
                    for (int x = 14; x < 26; x++) if (p.Opaque(x, y)) { top = y; break; }
                Color32 gold = C(250, 204, 70), goldD = C(190, 136, 36);
                p.Rect(13, top - 1, 14, 3, gold); p.Rect(13, top - 1, 14, 1, goldD);
                p.Rect(13, top + 2, 2, 3, gold); p.Rect(19, top + 2, 2, 4, gold); p.Rect(25, top + 2, 2, 3, gold);
                p.Set(20, top, C(220, 60, 80)); p.Set(19, top, C(220, 60, 80));
            }
            Big("yeti_0", "yetiking_0", 0.92f, 0.96f, 1.08f, Crown); Big("yeti_1", "yetiking_1", 0.92f, 0.96f, 1.08f, Crown);
            Big("hawk_0", "stormhawk_0", 0.8f, 0.85f, 1.2f); Big("hawk_1", "stormhawk_1", 0.8f, 0.85f, 1.2f);
            Big("hawk_2", "stormhawk_2", 0.8f, 0.85f, 1.2f, null, true);
            Big("wraith_0", "warden_0", 1.25f, 0.75f, 1.3f); Big("wraith_1", "warden_1", 1.25f, 0.75f, 1.3f);
            {
                var p = new Px(9, 9);
                p.Disc(4f, 4f, 3.4f, C(150, 90, 230)); p.Disc(4f, 4f, 2f, C(214, 170, 255)); p.Set(4, 4, C(255, 255, 255));
                Save(p, o + "orb", PCenter);
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
                p.Ellipse(8, 4.5f, 4.5f, 4f, s); p.Rect(7, 8, 2, 2, sd); p.Rect(6, 10, 4, 1, s);
                p.Disc(8, 4f, 1.8f, C(40, 40, 54)); p.Set(7, 5, C(110, 110, 130));
                p.Outline(ol); Save(p, d + "bombbag", PItem);
            }
            {
                var p = ItemCanvas(); Color32 r = C(196, 156, 92), rd = C(130, 96, 50);
                p.Ellipse(8, 3f, 5.5f, 3f, r); p.Ellipse(8, 3f, 3.5f, 1.6f, rd); p.Ellipse(8, 3f, 2f, 0.8f, r);
                p.Set(13, 5, r); p.Set(14, 6, r);
                p.Outline(ol); Save(p, d + "ropepile", PItem);
            }
            {
                var p = ItemCanvas(); Color32 g = C(236, 190, 60), gl = C(255, 240, 150), gd = C(160, 110, 30);
                p.Rect(5, 1, 6, 2, gd); p.Rect(6, 3, 4, 1, g); p.Ellipse(8, 7f, 3.2f, 3.6f, g);
                p.Set(7, 8, C(40, 20, 10)); p.Set(9, 8, C(40, 20, 10)); p.Rect(7, 5, 3, 1, gd); p.Set(6, 9, gl); p.Set(7, 10, gl);
                p.Outline(C(80, 50, 10)); Save(p, d + "idol", PItem);
            }
            {
                var p = ItemCanvas(); Color32 r = C(232, 50, 74), rl = C(255, 160, 180), rd = C(140, 20, 44);
                p.Rect(7, 1, 2, 1, rd); p.Rect(6, 2, 4, 8, r); p.Rect(5, 4, 1, 4, rd); p.Rect(10, 4, 1, 4, rd); p.Rect(7, 10, 2, 1, r);
                p.Set(7, 7, rl); p.Set(7, 8, rl); p.Set(8, 9, C(255, 255, 255)); p.Rect(8, 3, 1, 3, rd);
                p.Outline(C(255, 120, 140, 160)); Save(p, d + "heartstone", PItem);
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
                for (int y = 0; y < 15; y++)
                {
                    int half = (int)((y / 14f) * 4.2f);
                    for (int x = 8 - half - 1; x <= 7 + half + 1; x++) if (x >= 7 - half && x <= 8 + half) p.Set(x, y, x <= 7 ? sl : (x == 8 + half ? sd : s));
                }
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
            { var p = new Px(12, 10); HeartShape(p, 1, 1, C(220, 40, 60), C(255, 150, 160), C(140, 20, 40)); p.Outline(C(30, 10, 14)); Save(p, d + "icon_heart", PCenter); }
            { var p = new Px(12, 12); p.Disc(5.5f, 5f, 4f, C(40, 40, 54)); p.Set(4, 7, C(130, 130, 150)); p.Set(3, 6, C(130, 130, 150)); p.Set(8, 9, C(140, 100, 60)); p.Set(9, 10, C(255, 200, 60)); p.Outline(C(10, 10, 16)); Save(p, d + "icon_bomb", PCenter); }
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
        };

        /// <summary>Title logo: chunky gold pixel letters with a dark outline and a drop shadow.</summary>
        static void LogoArt()
        {
            const string word = "GLOOMDELVE";
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
        /// Main menu picture (320 x 180): the explorer at a camp torch inside a cave mouth, looking out at the sunset
        /// over the mountains. Characters are the game's own sprites, drawn at the same pixel size as the scenery.
        /// </summary>
        static void MenuArt()
        {
            const int W = 320, H = 180, FLOOR = 38;
            var p = new Px(W, H);

            // sky: banded sunset gradient, stars high up
            float[] stops = { 30f, 66f, 100f, 138f, 180f };
            Color32[] cols = { C(252, 176, 100), C(228, 114, 120), C(122, 72, 142), C(46, 40, 98), C(20, 18, 54) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float yy = Mathf.Clamp(y + (N(x, y, 5) - 0.5f) * 4f, stops[0], stops[4] - 0.01f);
                    int k = 0;
                    while (yy >= stops[k + 1]) k++;
                    Color32 col = Lerp(cols[k], cols[k + 1], (yy - stops[k]) / (stops[k + 1] - stops[k]));
                    if (y > 112 && N(x, y, 11) > 0.9935f) col = C(255, 244, 222);
                    p.Set(x, y, col);
                }
            // sun with a soft halo
            const float SX = 212f, SY = 76f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dd = Mathf.Sqrt((x - SX) * (x - SX) + (y - SY) * (y - SY));
                    if (dd < 13f) p.Set(x, y, C(255, 238, 180));
                    else if (dd < 15f) p.Set(x, y, C(255, 210, 144));
                    else if (dd < 36f) p.Set(x, y, Lerp(p.Get(x, y), C(255, 196, 128), (1f - dd / 36f) * 0.5f));
                }
            // clouds
            p.Ellipse(112, 120, 26, 3.5f, C(232, 140, 150)); p.Ellipse(124, 123, 15, 3f, C(250, 178, 164));
            p.Ellipse(262, 134, 20, 3f, C(170, 100, 150)); p.Ellipse(252, 136, 11, 2.5f, C(206, 130, 160));
            p.Ellipse(176, 150, 16, 2.5f, C(96, 64, 128));
            // mountain ranges and a pine forest silhouette
            for (int x = 0; x < W; x++)
            {
                int h1 = (int)(68 + 11 * Mathf.Sin(x * 0.045f) + 6 * Mathf.Sin(x * 0.11f + 1f));
                int h2 = (int)(54 + 8 * Mathf.Sin(x * 0.07f + 2f) + 4 * Mathf.Sin(x * 0.19f));
                int h3 = 43 + 4 - Mathf.Abs(x % 9 - 4) + (int)(2 * Mathf.Sin(x * 0.3f));
                for (int y = FLOOR - 2; y <= h1; y++) p.Set(x, y, y >= h1 - 1 ? C(176, 120, 150) : C(112, 74, 128));
                for (int y = FLOOR - 2; y <= h2; y++) p.Set(x, y, y == h2 ? C(96, 64, 112) : C(66, 46, 90));
                for (int y = FLOOR - 2; y <= h3; y++) p.Set(x, y, C(30, 24, 46));
            }

            // the cave around the opening: how deep a pixel sits inside the rock (<= 0: open air)
            (int cx, int len, int hw)[] stal = { (96, 44, 7), (128, 40, 6), (158, 52, 9), (204, 36, 6), (238, 58, 10), (270, 42, 7) };
            float Rock(int x, int y)
            {
                float n = TileNoise(x / 14f, y / 14f, 64, 5) * 0.6f + TileNoise(x / 5f, y / 5f, 64, 9) * 0.4f;
                float dx = (x - 166) / 128f, dy = (y - 100) / 68f;
                float dpt = Mathf.Sqrt(dx * dx + dy * dy) + (n - 0.5f) * 0.28f - 1f;
                foreach (var s in stal)
                {
                    float k = 1f - Mathf.Abs(x - s.cx) / (float)s.hw;
                    if (k > 0f && y > H - s.len * k) dpt = Mathf.Max(dpt, 0.04f + (y - (H - s.len * k)) / 90f);
                }
                if (y < FLOOR) dpt = Mathf.Max(dpt, 0.04f + (FLOOR - 1 - y) / 40f);
                return dpt;
            }
            const float TX = 84f, TY = FLOOR + 7f;   // camp torch
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dpt = Rock(x, y);
                    if (dpt <= 0f) continue;
                    Color32 col = Lerp(C(78, 56, 68), C(16, 12, 20), Mathf.Clamp01(dpt * 2.6f));
                    bool rim = Rock(x, y + 1) <= 0f || Rock(x + 1, y) <= 0f || Rock(x - 1, y) <= 0f || Rock(x, y - 1) <= 0f;
                    if (rim) col = y < FLOOR ? C(156, 116, 94) : C(138, 94, 88);          // edge lit by the sunset
                    else if (N(x / 2, y / 2, 21) > 0.84f) col = Mul(col, 1.2f);
                    else if (N(x / 3, y / 2, 22) < 0.12f) col = Mul(col, 0.78f);
                    float td = Mathf.Sqrt((x - TX) * (x - TX) + (y - TY) * (y - TY));
                    if (td < 46f) { float f = 1f - td / 46f; col = Lerp(col, C(255, 170, 90), f * f * 0.6f); }
                    p.Set(x, y, col);
                }
            // pebbles on the ledge
            for (int x = 8; x < W - 8; x += 7)
                if (N(x, 3, 31) > 0.55f && Rock(x, FLOOR) <= 0f) { p.Set(x, FLOOR, C(120, 90, 80)); p.Set(x + 1, FLOOR, C(84, 62, 62)); }
            // camp torch stuck in the ground
            p.Rect(84, FLOOR, 1, 6, C(116, 78, 42)); p.Set(84, FLOOR + 5, C(70, 44, 24));
            p.Set(83, FLOOR + 6, C(255, 140, 40)); p.Set(84, FLOOR + 6, C(255, 200, 80)); p.Set(85, FLOOR + 6, C(255, 140, 40));
            p.Set(84, FLOOR + 7, C(255, 236, 160)); p.Set(83, FLOOR + 7, C(255, 170, 60)); p.Set(84, FLOOR + 8, C(255, 190, 80));

            // the cast, at scenery pixel size
            p.Blit(LoadPx("Sprites/Player/idle_0"), 62, FLOOR - 1);
            var snake = LoadPx("Sprites/Enemies/snake_0"); snake.FlipX(); p.Blit(snake, 246, FLOOR - 1);
            p.Blit(LoadPx("Sprites/Enemies/bat_0"), 236, 104);
            var bat2 = LoadPx("Sprites/Enemies/bat_1"); p.Blit(bat2, 268, 118);
            p.Blit(LoadPx("Sprites/Items/idol"), 284, FLOOR - 1);
            Save(p, "UI/menu_bg", PCenter);
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
