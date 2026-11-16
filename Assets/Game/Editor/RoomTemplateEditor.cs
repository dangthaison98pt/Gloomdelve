using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Designer tool for Data/RoomTemplates.txt (menu Gloomdelve > Room Template Editor):
    /// browse / filter rooms, paint the 10x8 grid from a palette, edit the header (types, tags, areas, weight),
    /// see rule-check results live, run the room through the real generator + validator over many seeds,
    /// preview a generated level, and jump into play mode in a level that contains the room.
    /// The text file stays the single source of truth; this window only reads and writes it.
    /// </summary>
    public class RoomTemplateEditor : EditorWindow
    {
        const string FilePath = "Assets/Game/Data/RoomTemplates.txt";
        const string ConfigPath = "Assets/Game/Data/GameConfig.asset";
        const string GameScene = "Assets/Game/Scenes/Game.unity";
        const int Cell = 40;
        const int RW = RoomTemplates.W, RH = RoomTemplates.H;

        static readonly string[] TypeNames = { "0 Phòng phụ", "1 Trái-Phải", "2 Trái-Phải-Xuống", "3 Trái-Phải-Lên", "4 Bốn phía" };
        static readonly string[] BaseTags = { "start", "exit", "shop", "idol", "vault", "surface", "under" };

        GameConfig cfg;
        List<RoomTemplate> rooms = new List<RoomTemplate>();
        List<LintIssue>[] lint = new List<LintIssue>[0];
        List<LegendEntry> legend = new List<LegendEntry>();
        readonly Dictionary<char, LegendEntry> legendMap = new Dictionary<char, LegendEntry>();
        string loadError;
        bool crlf;
        int sel;
        char brush = '#';
        string search = "";
        int typeFilter;            // 0 = all, 1.. = type (n-1)
        int tagFilter;             // index into tagFilterNames
        string[] tagFilterNames = { "Mọi tag" };
        Vector2 listScroll, sideScroll, paletteScroll;
        readonly List<char[,]> undo = new List<char[,]>();

        // test run
        int testSeeds = 30;
        int testStage;
        int previewSeed = 1;
        bool playGod = true;
        string testReport = "";
        Texture2D preview;
        LevelData previewLevel;

        GUIStyle cellStyle, listStyle, listSelStyle, smallStyle;

        [MenuItem("Gloomdelve/Room Template Editor", priority = 10)]
        public static void Open()
        {
            var w = GetWindow<RoomTemplateEditor>();
            w.titleContent = new GUIContent("Room Templates");
            w.minSize = new Vector2(1120, 660);
            w.Show();
        }

        void OnEnable()
        {
            saveChangesMessage = "Mẫu phòng có thay đổi chưa lưu. Lưu vào RoomTemplates.txt?";
            Load();
        }

        void OnDisable()
        {
            if (preview != null) DestroyImmediate(preview);
        }

        // ================================================================== data

        void Load()
        {
            cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            loadError = null;
            rooms = new List<RoomTemplate>();
            try
            {
                string text = File.ReadAllText(FilePath);
                crlf = text.Contains("\r\n");
                rooms = RoomTemplates.ParseFresh(text);
            }
            catch (Exception e) { loadError = e.Message; }
            legend = RoomLegend.All(cfg != null ? cfg.customMarkers : null);
            legendMap.Clear();
            foreach (var e in legend) legendMap[e.ch] = e;
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, rooms.Count - 1));
            undo.Clear();
            hasUnsavedChanges = false;
            Refresh();
        }

        RoomTemplate Current => sel >= 0 && sel < rooms.Count ? rooms[sel] : null;

        IEnumerable<string> KnownTags()
        {
            foreach (var t in BaseTags) yield return t;
            if (cfg != null && cfg.equipment != null)
                foreach (var e in cfg.equipment) if (e != null) yield return "gate_" + e.id;
        }

        /// <summary>Re-runs the rule checks and rebuilds the filter lists after any change.</summary>
        void Refresh()
        {
            var custom = cfg != null ? cfg.customMarkers : null;
            lint = new List<LintIssue>[rooms.Count];
            var seen = new Dictionary<string, int>();
            foreach (var r in rooms) { seen.TryGetValue(r.name ?? "", out int n); seen[r.name ?? ""] = n + 1; }
            for (int i = 0; i < rooms.Count; i++)
            {
                lint[i] = RoomLint.Check(rooms[i], custom);
                if (seen[rooms[i].name ?? ""] > 1) lint[i].Insert(0, new LintIssue(true, $"Tên '{rooms[i].name}' bị trùng với phòng khác."));
            }
            var tags = new List<string> { "Mọi tag", "(không có tag)" };
            foreach (var t in KnownTags()) if (!tags.Contains(t)) tags.Add(t);
            foreach (var r in rooms) foreach (var t in r.tags) if (!tags.Contains(t)) tags.Add(t);
            tagFilterNames = tags.ToArray();
            tagFilter = Mathf.Clamp(tagFilter, 0, tagFilterNames.Length - 1);
        }

        void Touch()
        {
            hasUnsavedChanges = true;
            Refresh();
            Repaint();
        }

        void PushUndo()
        {
            var t = Current;
            if (t == null) return;
            undo.Add((char[,])t.cells.Clone());
            if (undo.Count > 100) undo.RemoveAt(0);
        }

        public override void SaveChanges()
        {
            string text = RoomTemplates.Serialize(rooms);
            if (crlf) text = text.Replace("\n", "\r\n");
            File.WriteAllText(FilePath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(FilePath);
            base.SaveChanges();
            hasUnsavedChanges = false;
            Debug.Log($"[RoomTemplateEditor] Saved {rooms.Count} room templates to {FilePath}");
        }

        bool TrySave()
        {
            int errors = 0;
            for (int i = 0; i < rooms.Count; i++) errors += lint[i].Count(x => x.error);
            if (errors > 0 && !EditorUtility.DisplayDialog("Mẫu phòng còn lỗi",
                    $"Còn {errors} lỗi quy tắc (chấm đỏ trong danh sách). Phòng lỗi có thể làm màn không sinh được hoặc bị sinh lại nhiều lần.\n\nVẫn lưu?", "Vẫn lưu", "Huỷ"))
                return false;
            SaveChanges();
            return true;
        }

        string UniqueName(string baseName)
        {
            string n = baseName;
            for (int i = 2; rooms.Any(r => r.name == n); i++) n = baseName + "_" + i;
            return n;
        }

        // ================================================================== GUI

        void EnsureStyles()
        {
            if (cellStyle != null) return;
            cellStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            cellStyle.normal.textColor = Color.white;
            listStyle = new GUIStyle(EditorStyles.label) { richText = true, padding = new RectOffset(18, 4, 2, 2) };
            listSelStyle = new GUIStyle(listStyle);
            listSelStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            if (loadError != null)
            {
                EditorGUILayout.HelpBox("Không đọc được RoomTemplates.txt:\n" + loadError + "\n\nSửa file bằng trình soạn văn bản rồi bấm Tải lại.", MessageType.Error);
                return;
            }
            if (cfg == null) EditorGUILayout.HelpBox("Không tìm thấy GameConfig.asset - chạy Gloomdelve > Build Everything trước.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawCanvasColumn();
                DrawSideColumn();
            }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                    if (GUILayout.Button("Lưu", EditorStyles.toolbarButton, GUILayout.Width(60))) TrySave();
                if (GUILayout.Button("Tải lại", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Tải lại", "Bỏ mọi thay đổi chưa lưu?", "Bỏ thay đổi", "Huỷ")) Load();
                GUILayout.Space(12);
                if (GUILayout.Button("+ Phòng mới", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    rooms.Add(RoomTemplate.Blank(UniqueName("new_room")));
                    sel = rooms.Count - 1; undo.Clear(); ClearFilters(); Touch();
                }
                using (new EditorGUI.DisabledScope(Current == null))
                {
                    if (GUILayout.Button("Nhân bản", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    {
                        var c = Current.Clone();
                        c.name = UniqueName(Current.name + "_copy");
                        c.comments.Clear();
                        rooms.Insert(sel + 1, c);
                        sel++; undo.Clear(); Touch();
                    }
                    if (GUILayout.Button("Xoá", EditorStyles.toolbarButton, GUILayout.Width(50)) &&
                        EditorUtility.DisplayDialog("Xoá phòng", $"Xoá mẫu phòng '{Current.name}'?", "Xoá", "Huỷ"))
                    {
                        // comments above a deleted room (section headers) move down to the next room
                        if (sel + 1 < rooms.Count && Current.comments.Count > 0) rooms[sel + 1].comments.InsertRange(0, Current.comments);
                        rooms.RemoveAt(sel);
                        sel = Mathf.Clamp(sel, 0, Mathf.Max(0, rooms.Count - 1)); undo.Clear(); Touch();
                    }
                }
                GUILayout.FlexibleSpace();
                int err = 0, warn = 0;
                foreach (var l in lint) { if (l.Any(x => x.error)) err++; else if (l.Count > 0) warn++; }
                GUILayout.Label($"{rooms.Count} phòng   {err} lỗi   {warn} cảnh báo   {(hasUnsavedChanges ? "● CHƯA LƯU" : "đã lưu")}", EditorStyles.miniLabel);
            }
        }

        void ClearFilters() { search = ""; typeFilter = 0; tagFilter = 0; }

        bool Passes(RoomTemplate r)
        {
            if (search.Length > 0 && (r.name == null || r.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)) return false;
            if (typeFilter > 0 && !r.types.Contains(typeFilter - 1)) return false;
            if (tagFilter == 1 && r.tags.Count > 0) return false;
            if (tagFilter > 1 && !r.HasTag(tagFilterNames[tagFilter])) return false;
            return true;
        }

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250)))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                using (new EditorGUILayout.HorizontalScope())
                {
                    typeFilter = EditorGUILayout.Popup(typeFilter, new[] { "Mọi loại" }.Concat(TypeNames).ToArray());
                    tagFilter = EditorGUILayout.Popup(tagFilter, tagFilterNames);
                }
                listScroll = EditorGUILayout.BeginScrollView(listScroll, EditorStyles.helpBox);
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (!Passes(r)) continue;
                    foreach (var c in r.comments)
                        if (c.StartsWith("# ---")) GUILayout.Label(c.Trim('#', '-', ' '), EditorStyles.miniBoldLabel);
                    var rect = GUILayoutUtility.GetRect(new GUIContent(r.name), listStyle, GUILayout.ExpandWidth(true));
                    if (i == sel) EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.59f));
                    bool hasErr = lint[i].Any(x => x.error);
                    var dot = new Rect(rect.x + 5, rect.y + rect.height * 0.5f - 4, 8, 8);
                    EditorGUI.DrawRect(dot, hasErr ? new Color(0.9f, 0.25f, 0.25f) : lint[i].Count > 0 ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.3f, 0.75f, 0.35f));
                    string extra = string.Join(",", r.types) + (r.tags.Count > 0 ? "  " + string.Join(",", r.tags) : "");
                    GUI.Label(rect, $"{r.name}  <size=9><color=#9a9a9a>{extra}</color></size>", i == sel ? listSelStyle : listStyle);
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        sel = i; undo.Clear(); GUI.FocusControl(null); Event.current.Use(); Repaint();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ------------------------------------------------------------------ canvas + palette

        Color ColorOf(char ch) => legendMap.TryGetValue(ch, out var e) ? e.color : new Color(1f, 0f, 1f);

        void DrawCanvasColumn()
        {
            var t = Current;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(RW * Cell + 40)))
            {
                if (t == null) { GUILayout.Label("Chưa có phòng nào. Bấm '+ Phòng mới'."); return; }
                GUILayout.Space(4);
                var area = GUILayoutUtility.GetRect(RW * Cell + 30, RH * Cell + 30, GUILayout.ExpandWidth(false));
                var grid = new Rect(area.x + 22, area.y + 18, RW * Cell, RH * Cell);
                for (int x = 0; x < RW; x++) GUI.Label(new Rect(grid.x + x * Cell, area.y, Cell, 16), x.ToString(), smallStyle);
                for (int r = 0; r < RH; r++) GUI.Label(new Rect(area.x, grid.y + r * Cell, 20, Cell), r.ToString(), smallStyle);

                EditorGUI.DrawRect(new Rect(grid.x - 1, grid.y - 1, grid.width + 2, grid.height + 2), new Color(0.05f, 0.05f, 0.06f));
                for (int r = 0; r < RH; r++)
                    for (int x = 0; x < RW; x++)
                    {
                        char ch = t.cells[x, r];
                        var cr = new Rect(grid.x + x * Cell + 1, grid.y + r * Cell + 1, Cell - 2, Cell - 2);
                        var col = ColorOf(ch);
                        EditorGUI.DrawRect(cr, col);
                        if (ch != '.' && ch != '#')
                        {
                            cellStyle.normal.textColor = col.grayscale > 0.6f ? Color.black : Color.white;
                            GUI.Label(cr, ch.ToString(), cellStyle);
                        }
                    }
                DrawOpeningGuides(t, grid);
                HandlePaint(t, grid);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(22);
                    if (GUILayout.Button("Lật ngang", GUILayout.Width(80)))
                    {
                        PushUndo();
                        var c = (char[,])t.cells.Clone();
                        for (int r = 0; r < RH; r++) for (int x = 0; x < RW; x++) t.cells[x, r] = c[RW - 1 - x, r];
                        Touch();
                    }
                    if (GUILayout.Button("Xoá trắng", GUILayout.Width(80)))
                    {
                        PushUndo();
                        for (int r = 0; r < RH; r++) for (int x = 0; x < RW; x++) t.cells[x, r] = r == 0 || r == RH - 1 ? '#' : '.';
                        Touch();
                    }
                    using (new EditorGUI.DisabledScope(undo.Count == 0))
                        if (GUILayout.Button($"Hoàn tác ({undo.Count})", GUILayout.Width(100)))
                        {
                            t.cells = undo[undo.Count - 1];
                            undo.RemoveAt(undo.Count - 1);
                            Touch();
                        }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Label("Chuột trái: vẽ    Chuột phải: lấy ký hiệu ở ô đó    Vạch xanh ở mép: lối nối bắt buộc theo loại phòng", EditorStyles.miniLabel);
                GUILayout.Space(4);
                DrawPalette();
            }
        }

        /// <summary>Cyan bars on the room edges where the selected room types need an opening.</summary>
        void DrawOpeningGuides(RoomTemplate t, Rect grid)
        {
            var c = new Color(0.2f, 0.9f, 1f, 0.9f);
            bool sides = t.types.Any(x => x >= 1) || t.HasTag("shop");
            bool bottom = t.types.Contains(2) || t.types.Contains(4);
            bool top = t.types.Contains(3) || t.types.Contains(4);
            if (sides)
            {
                if (!t.HasTag("shop")) EditorGUI.DrawRect(new Rect(grid.x - 5, grid.y + 4 * Cell, 3, 3 * Cell), c);
                EditorGUI.DrawRect(new Rect(grid.xMax + 2, grid.y + 4 * Cell, 3, 3 * Cell), c);
            }
            if (bottom) EditorGUI.DrawRect(new Rect(grid.x + 4 * Cell, grid.yMax + 2, 2 * Cell, 3), c);
            if (top) EditorGUI.DrawRect(new Rect(grid.x + 3 * Cell, grid.y - 5, 4 * Cell, 3), c);
        }

        void HandlePaint(RoomTemplate t, Rect grid)
        {
            var e = Event.current;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || !grid.Contains(e.mousePosition)) return;
            int x = Mathf.Clamp((int)((e.mousePosition.x - grid.x) / Cell), 0, RW - 1);
            int r = Mathf.Clamp((int)((e.mousePosition.y - grid.y) / Cell), 0, RH - 1);
            if (e.button == 1)
            {
                brush = t.cells[x, r];
                Repaint();
            }
            else if (e.button == 0)
            {
                if (e.type == EventType.MouseDown) PushUndo();
                if (t.cells[x, r] != brush) { t.cells[x, r] = brush; Touch(); }
            }
            e.Use();
        }

        void DrawPalette()
        {
            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
            foreach (var group in legend.Select(l => l.group).Distinct())
            {
                GUILayout.Label(GroupName(group), EditorStyles.miniBoldLabel);
                int col = 0;
                EditorGUILayout.BeginHorizontal();
                foreach (var l in legend.Where(x => x.group == group))
                {
                    if (col == 4) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); col = 0; }
                    var rect = GUILayoutUtility.GetRect(104, 24, GUILayout.Width(104));
                    bool on = brush == l.ch;
                    EditorGUI.DrawRect(rect, on ? new Color(1f, 0.8f, 0.3f) : new Color(0.22f, 0.22f, 0.24f));
                    EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, 20, 20), l.color);
                    cellStyle.normal.textColor = l.color.grayscale > 0.6f ? Color.black : Color.white;
                    int fs = cellStyle.fontSize; cellStyle.fontSize = 13;
                    GUI.Label(new Rect(rect.x + 2, rect.y + 2, 20, 20), l.ch.ToString(), cellStyle);
                    cellStyle.fontSize = fs;
                    var ls = new GUIStyle(EditorStyles.miniLabel);
                    ls.normal.textColor = on ? Color.black : new Color(0.85f, 0.85f, 0.85f);
                    GUI.Label(new Rect(rect.x + 26, rect.y + 4, rect.width - 28, 16), new GUIContent(l.name, l.help), ls);
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        brush = l.ch; Event.current.Use(); Repaint();
                    }
                    GUILayout.Space(3);
                    col++;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            if (legendMap.TryGetValue(brush, out var b))
                EditorGUILayout.HelpBox($"'{b.ch}'  {b.name}: {b.help}", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static string GroupName(string g)
        {
            switch (g)
            {
                case "Terrain": return "ĐỊA HÌNH";
                case "Hazards": return "BẪY";
                case "Enemies": return "QUÁI";
                case "Loot": return "KHO BÁU / ĐỒ VẬT";
                case "Special": return "ĐẶC BIỆT";
                default: return "TÙY BIẾN (GameConfig > Custom Markers)";
            }
        }

        // ------------------------------------------------------------------ properties, checks, tests

        void DrawSideColumn()
        {
            var t = Current;
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
            if (t == null) { EditorGUILayout.EndScrollView(); return; }

            GUILayout.Label("THÔNG TIN PHÒNG", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.DelayedTextField("Tên", t.name);
            float weight = EditorGUILayout.FloatField(new GUIContent("Weight", "Tần suất được chọn so với các phòng cùng loại."), t.weight);
            if (EditorGUI.EndChangeCheck()) { t.name = name.Trim(); t.weight = weight; Touch(); }

            GUILayout.Label(new GUIContent("Loại phòng (types)", "Phòng được dùng ở vị trí nào trên đường đi. Chọn được nhiều loại."), EditorStyles.miniBoldLabel);
            for (int i = 0; i < TypeNames.Length; i++)
            {
                bool on = t.types.Contains(i);
                bool now = EditorGUILayout.ToggleLeft(TypeNames[i], on);
                if (now != on) { if (now) t.types.Add(i); else t.types.Remove(i); Touch(); }
            }

            GUILayout.Label(new GUIContent("Tag", "Phòng đặc biệt chỉ dùng cho đúng mục đích của tag."), EditorStyles.miniBoldLabel);
            var known = KnownTags().Distinct().ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                int n = 0;
                foreach (var tag in known)
                {
                    if (n > 0 && n % 4 == 0) { GUILayout.FlexibleSpace(); EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
                    bool on = t.HasTag(tag);
                    bool now = GUILayout.Toggle(on, tag, EditorStyles.miniButton, GUILayout.Width(88));
                    if (now != on) { if (now) t.tags.Add(tag); else t.tags.Remove(tag); Touch(); }
                    n++;
                }
                GUILayout.FlexibleSpace();
            }
            string others = string.Join(",", t.tags.Where(x => !known.Contains(x)));
            string newOthers = EditorGUILayout.DelayedTextField(new GUIContent("Tag khác", "Các tag ngoài danh sách, cách nhau bởi dấu phẩy."), others);
            if (newOthers != others)
            {
                t.tags.RemoveAll(x => !known.Contains(x));
                foreach (var s in newOthers.Split(',')) { string v = s.Trim(); if (v.Length > 0 && !t.tags.Contains(v)) t.tags.Add(v); }
                Touch();
            }

            GUILayout.Label(new GUIContent("Khu (areas)", "Không tick khu nào = dùng cho mọi khu."), EditorStyles.miniBoldLabel);
            foreach (var a in Areas())
            {
                bool on = t.areas.Contains(a.areaNumber);
                bool now = EditorGUILayout.ToggleLeft($"{a.areaNumber}  {a.displayName}", on);
                if (now != on) { if (now) t.areas.Add(a.areaNumber); else t.areas.Remove(a.areaNumber); Touch(); }
            }

            GUILayout.Label(new GUIContent("Ghi chú phía trên phòng", "Dòng chú thích trong file (bắt đầu bằng #)."), EditorStyles.miniBoldLabel);
            string com = string.Join("\n", t.comments);
            string newCom = EditorGUILayout.TextArea(com, GUILayout.MinHeight(34));
            if (newCom != com)
            {
                t.comments = newCom.Split('\n').Select(l => l.TrimEnd()).Select(l => l.Length == 0 || l.StartsWith("#") ? l : "# " + l).ToList();
                while (t.comments.Count > 0 && t.comments[t.comments.Count - 1].Length == 0) t.comments.RemoveAt(t.comments.Count - 1);
                while (t.comments.Count > 0 && t.comments[0].Length == 0) t.comments.RemoveAt(0);
                Touch();
            }

            GUILayout.Space(8);
            GUILayout.Label("KIỂM TRA QUY TẮC", EditorStyles.boldLabel);
            var issues = sel < lint.Length ? lint[sel] : null;
            if (issues == null || issues.Count == 0) EditorGUILayout.HelpBox("Không có lỗi quy tắc.", MessageType.Info);
            else foreach (var i in issues) EditorGUILayout.HelpBox(i.message, i.error ? MessageType.Error : MessageType.Warning);

            GUILayout.Space(8);
            DrawTestSection(t);
            EditorGUILayout.EndScrollView();
        }

        List<AreaConfig> Areas()
        {
            var list = new List<AreaConfig>();
            if (cfg != null && cfg.stages != null)
                foreach (var s in cfg.stages)
                    if (s != null && s.kind == LevelKind.Area && s.area != null && !list.Contains(s.area)) list.Add(s.area);
            return list;
        }

        void DrawTestSection(RoomTemplate t)
        {
            GUILayout.Label("THỬ TRONG BỘ SINH MÀN", EditorStyles.boldLabel);
            if (cfg == null || cfg.stages == null) return;
            var stageIdx = new List<int>();
            for (int i = 0; i < cfg.stages.Length; i++)
                if (cfg.stages[i].kind == LevelKind.Area && cfg.stages[i].area != null && t.ForArea(cfg.stages[i].area.areaNumber)) stageIdx.Add(i);
            if (stageIdx.Count == 0) { EditorGUILayout.HelpBox("Không có màn nào thuộc các khu đã tick.", MessageType.Warning); return; }
            int pick = Mathf.Max(0, stageIdx.IndexOf(testStage));
            pick = EditorGUILayout.Popup("Màn", pick, stageIdx.Select(i => $"{cfg.stages[i].displayName}  {cfg.stages[i].area.displayName}").ToArray());
            testStage = stageIdx[pick];
            var stage = cfg.stages[testStage];

            using (new EditorGUILayout.HorizontalScope())
            {
                testSeeds = Mathf.Clamp(EditorGUILayout.IntField("Số seed", testSeeds), 1, 500);
                if (GUILayout.Button("Chạy thử", GUILayout.Width(90))) RunTest(t, stage);
            }
            if (!string.IsNullOrEmpty(testReport)) EditorGUILayout.HelpBox(testReport, MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                previewSeed = Mathf.Max(1, EditorGUILayout.IntField("Seed xem trước", previewSeed));
                if (GUILayout.Button("Xem màn", GUILayout.Width(70))) FindPreview(t, stage, previewSeed, 1);
                if (GUILayout.Button("Kế tiếp", GUILayout.Width(60))) FindPreview(t, stage, previewSeed + 1, 40);
            }
            if (preview != null && previewLevel != null)
            {
                float scale = Mathf.Max(4f, Mathf.Floor(Mathf.Min(8f, (position.width - 760f) / preview.width)));
                var pr = GUILayoutUtility.GetRect(preview.width * scale, preview.height * scale, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(pr, preview, ScaleMode.StretchToFill);
                foreach (var rr in previewLevel.forcedRooms)
                {
                    var o = new Rect(pr.x + rr.xMin * scale, pr.y + (previewLevel.Height - rr.yMax) * scale, rr.width * scale, rr.height * scale);
                    var yl = new Color(1f, 0.9f, 0.2f);
                    EditorGUI.DrawRect(new Rect(o.x, o.y, o.width, 2), yl); EditorGUI.DrawRect(new Rect(o.x, o.yMax - 2, o.width, 2), yl);
                    EditorGUI.DrawRect(new Rect(o.x, o.y, 2, o.height), yl); EditorGUI.DrawRect(new Rect(o.xMax - 2, o.y, 2, o.height), yl);
                }
                var v = previewLevel.validation;
                GUILayout.Label($"Seed {previewSeed}: {(v != null && v.ok ? "hợp lệ" : "KHÔNG hợp lệ")}, sinh {previewLevel.attempts} lần, phòng này xuất hiện {previewLevel.forcedRooms.Count} chỗ (khung vàng).  " +
                                "Xanh dương: cửa vào, xanh lá: cửa ra, đỏ: quái/bẫy, vàng: đồ.", EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                playGod = GUILayout.Toggle(playGod, new GUIContent("Bất tử", "Không mất máu khi chơi thử."), GUILayout.Width(70));
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(new GUIContent("▶ Chơi thử màn này", "Lưu file, mở scene Game và vào Play với seed xem trước; phòng đang chọn được ép vào mọi chỗ phù hợp.")))
                        PlayTest(t);
            }
        }

        GenRequest Request(RoomTemplate force, StageDef stage, int seed, List<RoomTemplate> templates) => new GenRequest
        {
            seed = LevelFactory.LevelSeed(seed, testStage), area = stage.area, levelNumber = stage.levelNumber, name = stage.displayName,
            config = cfg, templates = templates, forceTemplate = force
        };

        /// <summary>Templates as currently edited (unsaved changes included), without the room under test.</summary>
        List<RoomTemplate> WorkingSet() => rooms;

        void RunTest(RoomTemplate t, StageDef stage)
        {
            var set = WorkingSet();
            int usedLevels = 0, usedRooms = 0, invalid = 0, attempts = 0, baseAttempts = 0, baseInvalid = 0;
            var reasons = new Dictionary<string, int>();
            try
            {
                for (int i = 0; i < testSeeds; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Thử mẫu phòng", $"seed {i + 1}/{testSeeds}", (float)i / testSeeds)) { testSeeds = i; break; }
                    var d = LevelGenerator.Generate(Request(t, stage, i + 1, set));
                    attempts += d.attempts;
                    if (d.forcedRooms.Count > 0) { usedLevels++; usedRooms += d.forcedRooms.Count; }
                    if (d.validation == null || !d.validation.ok) invalid++;
                    foreach (var n in d.notes)
                    {
                        int k = n.IndexOf(": ", StringComparison.Ordinal);
                        string why = k >= 0 ? n.Substring(k + 2).Trim() : n;
                        reasons.TryGetValue(why, out int c); reasons[why] = c + 1;
                    }
                    var b = LevelGenerator.Generate(Request(null, stage, i + 1, set));
                    baseAttempts += b.attempts;
                    if (b.validation == null || !b.validation.ok) baseInvalid++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            int nSeeds = Mathf.Max(1, testSeeds);
            var sb = new StringBuilder();
            sb.AppendLine($"{stage.displayName} {stage.area.displayName}, {nSeeds} seed:");
            sb.AppendLine($"• Phòng được dùng trong {usedLevels}/{nSeeds} màn ({usedRooms} chỗ).");
            sb.AppendLine($"• Số lần sinh trung bình: {(float)attempts / nSeeds:0.00} lần/màn (không ép phòng: {(float)baseAttempts / nSeeds:0.00}).");
            sb.AppendLine($"• Màn không hợp lệ sau mọi lần thử: {invalid} (không ép phòng: {baseInvalid}).");
            if (reasons.Count > 0) sb.AppendLine("• Lý do bị sinh lại: " + string.Join(", ", reasons.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} ×{x.Value}")));
            if (usedLevels == 0) sb.Append("⚠ Phòng chưa được dùng lần nào: kiểm tra types / tag / khu, hoặc tăng số seed (phòng idol, vault, shop không xuất hiện ở mọi màn).");
            else if (invalid > baseInvalid) sb.Append("⚠ Phòng này làm xuất hiện màn không hợp lệ - cần sửa.");
            else if (attempts > baseAttempts * 1.5f + nSeeds) sb.Append("⚠ Phòng này làm bộ sinh màn phải sinh lại nhiều hơn hẳn bình thường - nên xem lại lối nối.");
            else sb.Append("✔ Ổn: mọi màn có phòng này đều được validator chứng minh là qua được.");
            testReport = sb.ToString();
        }

        void FindPreview(RoomTemplate t, StageDef stage, int fromSeed, int tries)
        {
            LevelData d = null;
            for (int i = 0; i < tries; i++)
            {
                d = LevelGenerator.Generate(Request(t, stage, fromSeed + i, WorkingSet()));
                previewSeed = fromSeed + i;
                if (d.forcedRooms.Count > 0) break;
            }
            previewLevel = d;
            BuildPreview(d);
        }

        void BuildPreview(LevelData d)
        {
            if (preview != null) DestroyImmediate(preview);
            var g = d.grid;
            preview = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            bool outdoor = cfg.stages[testStage].area.outdoor;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    preview.SetPixel(x, y, TileColor(g.Get(x, y), outdoor));
            var reg = cfg.prefabs;
            foreach (var sp in d.spawns)
            {
                if (!g.InBounds(sp.cell.x, sp.cell.y)) continue;
                var prefab = reg != null ? reg.Get(sp.id) : null;
                Color c;
                if (sp.id == "door_entrance") c = new Color(0.2f, 0.6f, 1f);
                else if (sp.id == "door_exit") c = new Color(0.2f, 1f, 0.4f);
                else if (prefab != null && (prefab.IsHazard || prefab is ArrowTrap || prefab is FlameVent)) c = new Color(1f, 0.25f, 0.25f);
                else if (prefab is Pickup || prefab is Item) c = new Color(1f, 0.9f, 0.3f);
                else continue;
                preview.SetPixel(sp.cell.x, sp.cell.y, c);
            }
            preview.Apply();
        }

        static Color TileColor(TileType t, bool outdoor)
        {
            switch (t)
            {
                case TileType.Empty: return outdoor ? new Color(0.2f, 0.28f, 0.45f) : new Color(0.1f, 0.09f, 0.11f);
                case TileType.Bedrock: return new Color(0.3f, 0.3f, 0.35f);
                case TileType.Platform: return new Color(0.72f, 0.52f, 0.28f);
                case TileType.Ladder: return new Color(0.85f, 0.7f, 0.4f);
                case TileType.Spikes: return new Color(0.8f, 0.3f, 0.45f);
                case TileType.GoldVein: return new Color(0.8f, 0.65f, 0.15f);
                case TileType.GemVein: return new Color(0.3f, 0.8f, 0.6f);
                case TileType.Rubble: return new Color(0.6f, 0.5f, 0.47f);
                case TileType.ArrowTrap: case TileType.VentBlock: return new Color(0.6f, 0.3f, 0.15f);
                default: return new Color(0.45f, 0.32f, 0.22f);
            }
        }

        void PlayTest(RoomTemplate t)
        {
            if (hasUnsavedChanges && !TrySave()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(GameScene);
            EditorPlaytest.Begin(previewSeed, testStage, t.name, playGod);
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Documentation / smoke test (run the editor WITHOUT -batchmode, with -executeMethod): opens the window,
        /// runs a preview, saves a screenshot to Docs/Screenshots/room_editor.png and quits.
        /// </summary>
        public static void CaptureForDocs()
        {
            var w = GetWindow<RoomTemplateEditor>();
            w.titleContent = new GUIContent("Room Templates");
            w.position = new Rect(80, 80, 1280, 780);
            w.Show();
            w.Focus();
            int frames = 0;
            void Tick()
            {
                frames++;
                w.Repaint();
                if (frames == 40)
                {
                    w.sel = Mathf.Max(0, w.rooms.FindIndex(r => r.name == "p1_platforms"));
                    w.testStage = 0;
                    w.testSeeds = 10;
                    w.RunTest(w.Current, w.cfg.stages[0]);
                    w.FindPreview(w.Current, w.cfg.stages[0], 1, 40);
                }
                if (frames < 160) return;
                EditorApplication.update -= Tick;
                var p = w.position;
                float ppp = EditorGUIUtility.pixelsPerPoint;
                int pw = Mathf.RoundToInt(p.width * ppp), ph = Mathf.RoundToInt(p.height * ppp);
                var px = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(p.position * ppp, pw, ph);
                var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs/Screenshots/room_editor.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("[RoomTemplateEditor] screenshot " + path);
                EditorApplication.Exit(0);
            }
            EditorApplication.update += Tick;
        }

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode) EditorPlaytest.End();
            };
        }
    }
}
