using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The frames of an animation live together in one picture: Art/.../name_sheet.png, the frames side by side
    /// (2 px apart, standing on its bottom edge), each cut out as a sprite of its own with its old name (bat_0,
    /// bat_1 ...) and its old pivot - so one file is opened to change a whole animation. Art/sheets.json lists where
    /// each frame lies in its sheet.
    ///
    /// Loose frame files (name_0.png, name_1.png ... as the generator makes them the first time) are gathered into
    /// their sheet at the end of every art run (Pack): a new animation gets a new sheet, a new frame is added to the
    /// right of an existing one. What was there already in a sheet is never redrawn. References to a gathered
    /// frame's old file (prefabs, scenes, data) are pointed at its frame in the sheet.
    /// Not gathered: tiles, the font, the button glyphs, the sky, the rooms and house fronts, the villagers' portraits
    /// (sets of pictures, not animations).
    /// </summary>
    public static class ArtSheets
    {
        const string Root = ArtGenerator.Root;
        static string IndexPath => Path.Combine(Directory.GetCurrentDirectory(), Root + "sheets.json");
        const int Gap = 2;

        [Serializable] public class Frame { public string name; public int x, y, w, h; public float px, py; }
        [Serializable] public class Sheet { public string sheet; public List<Frame> frames = new List<Frame>(); }
        [Serializable] class Index { public List<Sheet> sheets = new List<Sheet>(); }

        static Index index;
        static Dictionary<string, (Sheet sheet, Frame frame)> byFrame;      // "Sprites/Enemies/bat_0" -> its sheet and place
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        /// <summary>Groups that are not named name_N but are one animation all the same (folder-relative names).</summary>
        static readonly Dictionary<string, string[]> Named = new Dictionary<string, string[]>
        {
            { "Sprites/Village/toy_cat", new[] { "toy_cat_sleep", "toy_cat_stretch", "toy_cat_sit", "toy_cat_walk_0", "toy_cat_walk_1" } },
        };

        static bool NotAnAnimation(string prefix) =>
            prefix.StartsWith("Tiles/") || prefix.StartsWith("Font/") || prefix.StartsWith("UI/Glyphs/") || prefix.StartsWith("Sky/") ||
            prefix == "Sprites/NPC/villager" || prefix.StartsWith("Sprites/Village/front_") ||
            Regex.IsMatch(prefix, @"^Sprites/Village/(room|room_fg|room_\d+_out)$");

        public static void Load()
        {
            index = null;
            try { if (File.Exists(IndexPath)) index = JsonUtility.FromJson<Index>(File.ReadAllText(IndexPath)); } catch (Exception e) { Debug.LogWarning("[Art] sheets.json: " + e.Message); }
            if (index == null) index = new Index();
            Reindex();
            spriteCache.Clear();
        }

        static void Reindex()
        {
            byFrame = new Dictionary<string, (Sheet, Frame)>();
            foreach (var s in index.sheets)
            {
                string folder = s.sheet.Contains("/") ? s.sheet.Substring(0, s.sheet.LastIndexOf('/') + 1) : "";
                foreach (var f in s.frames) byFrame[folder + f.name] = (s, f);
            }
        }

        static void SaveIndex() => File.WriteAllText(IndexPath, JsonUtility.ToJson(index, true));

        static void Ensure() { if (index == null) Load(); }

        /// <summary>This picture is a frame inside a sheet.</summary>
        public static bool Has(string rel) { Ensure(); return byFrame.ContainsKey(rel); }

        /// <summary>The frame's pixels, cut out of its sheet.</summary>
        public static Px Crop(string rel)
        {
            Ensure();
            var (s, f) = byFrame[rel];
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Root + s.sheet + ".png")));
            var p = new Px(f.w, f.h);
            var src = tex.GetPixels32();
            for (int y = 0; y < f.h; y++)
                for (int x = 0; x < f.w; x++) p.c[y * f.w + x] = src[(f.y + y) * tex.width + f.x + x];
            UnityEngine.Object.DestroyImmediate(tex);
            return p;
        }

        /// <summary>The frame's sprite (cut out of its sheet), or null.</summary>
        public static Sprite FrameSprite(string rel)
        {
            Ensure();
            if (!byFrame.TryGetValue(rel, out var hit)) return null;
            if (spriteCache.TryGetValue(rel, out var c) && c != null) return c;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Root + hit.sheet.sheet + ".png"))
                if (o is Sprite sp && sp.name == hit.frame.name) { spriteCache[rel] = sp; return sp; }
            return null;
        }

        /// <summary>This file is a sheet (its frames are cut out of it).</summary>
        public static bool IsSheet(string assetPath)
        {
            Ensure();
            if (!assetPath.StartsWith(Root) || !assetPath.EndsWith("_sheet.png")) return false;
            string rel = assetPath.Substring(Root.Length, assetPath.Length - Root.Length - 4);
            return index.sheets.Any(s => s.sheet == rel);
        }

        // ------------------------------------------------------------------ gathering loose frames

        /// <summary>
        /// Gather the loose frame files of every animation into its sheet, cut the sheets, and point every reference
        /// to a gathered file at its frame. Returns how many frames were gathered.
        /// </summary>
        public static int Pack()
        {
            Ensure();
            string full = Path.Combine(Directory.GetCurrentDirectory(), Root);
            // the loose pictures, by group
            var groups = new Dictionary<string, List<string>>();          // prefix -> loose frame rels
            foreach (var file in Directory.GetFiles(full, "*.png", SearchOption.AllDirectories))
            {
                string rel = file.Substring(full.Length).Replace('\\', '/');
                rel = rel.Substring(0, rel.Length - 4);
                if (rel.EndsWith("_hl") || rel.EndsWith("_sheet")) continue;
                string prefix = null;
                foreach (var kv in Named)
                {
                    string folder = kv.Key.Substring(0, kv.Key.LastIndexOf('/') + 1);
                    if (rel.StartsWith(folder) && kv.Value.Contains(rel.Substring(folder.Length))) { prefix = kv.Key; break; }
                }
                if (prefix == null)
                {
                    var m = Regex.Match(rel, @"^(.*)_(\d+)$");
                    if (!m.Success || NotAnAnimation(m.Groups[1].Value)) continue;
                    prefix = m.Groups[1].Value;
                }
                if (!groups.TryGetValue(prefix, out var l)) groups[prefix] = l = new List<string>();
                l.Add(rel);
            }
            int gathered = 0;
            var moved = new List<(string oldGuid, string rel)>();
            foreach (var kv in groups)
            {
                string sheetRel = kv.Key + "_sheet";
                var existing = index.sheets.FirstOrDefault(s => s.sheet == sheetRel);
                if (existing == null && kv.Value.Count < 2) continue;          // one picture alone is not an animation
                // the frames that are loose must be pixel art like the rest (16 per unit, no smoothing)
                bool plain = kv.Value.All(r => { var ti = AssetImporter.GetAtPath(Root + r + ".png") as TextureImporter; return ti == null || (Mathf.Approximately(ti.spritePixelsPerUnit, 16f) && ti.filterMode == FilterMode.Point); });
                if (!plain) continue;
                gathered += Gather(kv.Key, sheetRel, existing, kv.Value, moved);
            }
            if (gathered > 0)
            {
                SaveIndex();
                Reindex();
                AssetDatabase.Refresh();
            }
            Slice();
            if (moved.Count > 0) Repoint(moved);
            spriteCache.Clear();
            return gathered;
        }

        static int Order(string name, string[] named)
        {
            if (named != null) { int i = Array.IndexOf(named, name); return i < 0 ? 999 : i; }
            var m = Regex.Match(name, @"_(\d+)$");
            return m.Success ? int.Parse(m.Groups[1].Value) : 999;
        }

        static int Gather(string prefix, string sheetRel, Sheet sheet, List<string> loose, List<(string, string)> moved)
        {
            string folder = prefix.Contains("/") ? prefix.Substring(0, prefix.LastIndexOf('/') + 1) : "";
            Named.TryGetValue(prefix, out var named);
            // every frame: those already in the sheet (cut from it, as they are now) and the loose ones
            var frames = new List<(string name, Px px, Vector2 pivot)>();
            if (sheet != null)
                foreach (var f in sheet.frames) frames.Add((f.name, Crop(folder + f.name), new Vector2(f.px, f.py)));
            foreach (var rel in loose)
            {
                string name = rel.Substring(folder.Length);
                frames.RemoveAll(f => f.name == name);              // (a loose file wins over the frame of the same name)
                var ti = AssetImporter.GetAtPath(Root + rel + ".png") as TextureImporter;
                var pivot = new Vector2(0.5f, 0f);
                if (ti != null) { var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts); pivot = ts.spritePivot; }
                frames.Add((name, LoadLoose(rel), pivot));
            }
            frames.Sort((a, b) => Order(a.name, named).CompareTo(Order(b.name, named)));
            // laid side by side, standing on the bottom edge
            int w = frames.Sum(f => f.px.w) + Gap * (frames.Count - 1), h = frames.Max(f => f.px.h);
            var outPx = new Px(w, h);
            var s = sheet ?? new Sheet { sheet = sheetRel };
            s.frames.Clear();
            int x = 0;
            foreach (var f in frames)
            {
                for (int yy = 0; yy < f.px.h; yy++)
                    for (int xx = 0; xx < f.px.w; xx++) outPx.c[yy * w + x + xx] = f.px.c[yy * f.px.w + xx];
                s.frames.Add(new Frame { name = f.name, x = x, y = 0, w = f.px.w, h = f.px.h, px = f.pivot.x, py = f.pivot.y });
                x += f.px.w + Gap;
            }
            outPx.Save(Path.Combine(Directory.GetCurrentDirectory(), Root + sheetRel + ".png"));
            if (sheet == null) index.sheets.Add(s);
            foreach (var rel in loose)
            {
                string path = Root + rel + ".png";
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) moved.Add((guid, rel));
                AssetDatabase.DeleteAsset(path);
            }
            return loose.Count;
        }

        static Px LoadLoose(string rel)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Root + rel + ".png")));
            var p = new Px(tex.width, tex.height);
            var src = tex.GetPixels32();
            for (int i = 0; i < src.Length; i++) p.c[i] = src[i];
            UnityEngine.Object.DestroyImmediate(tex);
            return p;
        }

        // ------------------------------------------------------------------ cutting the sheets

        /// <summary>Each sheet imported as pixel art with its frames cut out (only where that is not so already).</summary>
        static void Slice()
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            foreach (var s in index.sheets)
            {
                string path = Root + s.sheet + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) continue; }
                bool settingsOk = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Multiple && ti.filterMode == FilterMode.Point
                                  && Mathf.Approximately(ti.spritePixelsPerUnit, 16f) && ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled;
                if (!settingsOk)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Multiple;
                    ti.spritePixelsPerUnit = 16; ti.filterMode = FilterMode.Point;
                    ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.isReadable = false;
                    var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts);
                    ts.spriteMeshType = SpriteMeshType.FullRect; ts.spriteGenerateFallbackPhysicsShape = false;
                    ti.SetTextureSettings(ts);
                }
                var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
                dp.InitSpriteEditorDataProvider();
                var old = dp.GetSpriteRects();
                bool rectsOk = old.Length == s.frames.Count && s.frames.All(f => old.Any(r => r.name == f.name && r.rect == new Rect(f.x, f.y, f.w, f.h)
                                                                                              && r.alignment == SpriteAlignment.Custom && Vector2.Distance(r.pivot, new Vector2(f.px, f.py)) < 0.0001f));
                if (settingsOk && rectsOk) continue;
                var rects = s.frames.Select(f =>
                {
                    var was = old.FirstOrDefault(r => r.name == f.name);          // keep each frame's id: references stay good
                    return new SpriteRect
                    {
                        name = f.name, rect = new Rect(f.x, f.y, f.w, f.h), alignment = SpriteAlignment.Custom, pivot = new Vector2(f.px, f.py),
                        spriteID = was != null ? was.spriteID : GUID.Generate(),
                    };
                }).ToArray();
                dp.SetSpriteRects(rects);
                var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
                ids?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                dp.Apply();
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ references

        /// <summary>Everything that pointed at a gathered frame's old file now points at its frame in the sheet.</summary>
        static void Repoint(List<(string oldGuid, string rel)> moved)
        {
            var map = new Dictionary<string, string>();       // "fileID: 21300000, guid: OLD" -> "fileID: N, guid: SHEET"
            foreach (var (oldGuid, rel) in moved)
            {
                var sp = FrameSprite(rel);
                if (sp == null) { Debug.LogWarning("[Art] no frame for " + rel); continue; }
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sp, out string guid, out long id)) continue;
                map["fileID: 21300000, guid: " + oldGuid] = $"fileID: {id}, guid: {guid}";
            }
            if (map.Count == 0) return;
            int files = 0;
            foreach (var file in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Assets"), "*.*", SearchOption.AllDirectories))
            {
                if (!(file.EndsWith(".prefab") || file.EndsWith(".unity") || file.EndsWith(".asset") || file.EndsWith(".anim") || file.EndsWith(".mat"))) continue;
                string text = File.ReadAllText(file);
                if (!text.Contains("21300000")) continue;
                string was = text;
                foreach (var kv in map) if (text.Contains(kv.Key)) text = text.Replace(kv.Key, kv.Value);
                if (text != was) { File.WriteAllText(file, text); files++; }
            }
            if (files > 0) AssetDatabase.Refresh();
            Debug.Log($"[Art] {moved.Count} frames gathered into sheets; {files} files pointed at them");
        }
    }
}
