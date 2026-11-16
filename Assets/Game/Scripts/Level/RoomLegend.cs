using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>One symbol that may appear in a room template.</summary>
    public class LegendEntry
    {
        public char ch;
        public string name;
        public string help;
        public string group;      // Terrain / Hazards / Enemies / Loot / Special / Custom
        public Color color;
        public bool solid;        // blocks movement (for the template rule checks)
        public bool custom;
    }

    /// <summary>
    /// Every symbol of the room-template language in one place: the built-in ones (handled by LevelGenerator) and
    /// the designer-defined ones from GameConfig.customMarkers. Used by the generator, the rule checks and the
    /// Room Template Editor palette.
    /// </summary>
    public static class RoomLegend
    {
        /// <summary>Built-in marker symbols that the generator turns into traps / spawns.</summary>
        public const string BuiltinMarkers = "^AvfTEBIDKSOR!";

        static LegendEntry E(char ch, string name, string help, string group, Color color, bool solid = false) =>
            new LegendEntry { ch = ch, name = name, help = help, group = group, color = color, solid = solid };

        public static readonly LegendEntry[] Builtin =
        {
            E('.', "Trống", "Ô trống.", "Terrain", new Color(0.13f, 0.12f, 0.15f)),
            E('#', "Đất", "Đất - phá được bằng bom.", "Terrain", new Color(0.45f, 0.32f, 0.22f), true),
            E('X', "Bedrock", "Đá cứng - không phá được. Dùng cho vách/khung của chướng ngại.", "Terrain", new Color(0.33f, 0.33f, 0.38f), true),
            E('?', "Đất 55%", "55% là đất, 45% là trống (mỗi màn một khác).", "Terrain", new Color(0.33f, 0.25f, 0.19f)),
            E('=', "Sàn gỗ", "Sàn một chiều: nhảy xuyên lên, xuống + nhảy để rơi qua.", "Terrain", new Color(0.72f, 0.52f, 0.28f)),
            E('H', "Thang", "Thang leo.", "Terrain", new Color(0.85f, 0.7f, 0.4f)),
            E('$', "Mạch vàng", "Đất có vàng bên trong.", "Terrain", new Color(0.85f, 0.7f, 0.15f), true),
            E('%', "Đá vụn", "Đá vụn chắn đường - chỉ bom phá được (chướng ngại bom).", "Terrain", new Color(0.55f, 0.45f, 0.42f), true),
            E('~', "Vực gai", "Ô gai của vực (chướng ngại tàu lượn). Bên dưới tự thành bedrock.", "Hazards", new Color(0.75f, 0.2f, 0.35f)),
            E('^', "Chông", "Vị trí CÓ THỂ có chông (theo cấu hình khu). Cần sàn bên dưới.", "Hazards", new Color(0.85f, 0.3f, 0.3f)),
            E('A', "Bẫy tên", "Khối đất CÓ THỂ thành bẫy tên.", "Hazards", new Color(0.7f, 0.35f, 0.15f), true),
            E('v', "Vật rơi", "Vị trí CÓ THỂ có thạch nhũ / khối nghiền. Cần trần ngay phía trên.", "Hazards", new Color(0.6f, 0.6f, 0.75f)),
            E('f', "Lỗ lửa", "Khối đất CÓ THỂ thành lỗ phun lửa (ô phía trên phải trống).", "Hazards", new Color(0.95f, 0.5f, 0.1f), true),
            E('E', "Quái mặt đất", "Vị trí CÓ THỂ có quái mặt đất của khu. Cần sàn bên dưới.", "Enemies", new Color(0.4f, 0.8f, 0.35f)),
            E('B', "Quái trần", "Vị trí CÓ THỂ có quái bám trần của khu. Cần trần phía trên.", "Enemies", new Color(0.55f, 0.4f, 0.8f)),
            E('T', "Kho báu", "Vị trí CÓ THỂ có vàng / ngọc.", "Loot", new Color(1f, 0.85f, 0.3f)),
            E('I', "Đồ vật", "Vị trí CÓ THỂ có đá / bình / thùng.", "Loot", new Color(0.6f, 0.75f, 0.85f)),
            E('O', "Bệ tượng vàng", "Bệ + tượng vàng (phòng tag idol).", "Loot", new Color(1f, 0.7f, 0.1f)),
            E('D', "Cửa", "Cửa vào (phòng start) hoặc cửa ra (phòng exit). Đúng 1 cửa mỗi phòng.", "Special", new Color(0.3f, 0.75f, 0.95f)),
            E('K', "Chủ tiệm", "Chủ tiệm (phòng tag shop).", "Special", new Color(0.95f, 0.55f, 0.75f)),
            E('S', "Món hàng", "Chỗ bày hàng (phòng tag shop).", "Special", new Color(0.95f, 0.75f, 0.85f)),
            E('R', "Chỗ ném dây", "Chỗ đứng ném dây lên vách (chướng ngại dây).", "Special", new Color(0.8f, 0.65f, 0.45f)),
            E('!', "Biển gợi ý", "Biển gợi ý trang bị của chướng ngại. Cần sàn bên dưới.", "Special", new Color(0.9f, 0.9f, 0.6f)),
        };

        static readonly Dictionary<char, LegendEntry> builtinMap = BuildMap();

        static Dictionary<char, LegendEntry> BuildMap()
        {
            var m = new Dictionary<char, LegendEntry>();
            foreach (var e in Builtin) m[e.ch] = e;
            return m;
        }

        public static bool IsBuiltin(char ch) => builtinMap.ContainsKey(ch);

        /// <summary>Valid custom markers by symbol (a custom marker can never replace a built-in symbol).</summary>
        public static Dictionary<char, MarkerDef> CustomMap(IList<MarkerDef> custom)
        {
            var m = new Dictionary<char, MarkerDef>();
            if (custom == null) return m;
            foreach (var c in custom)
            {
                if (c == null || string.IsNullOrEmpty(c.symbol)) continue;
                char ch = c.symbol[0];
                if (char.IsWhiteSpace(ch) || ch == '[' || IsBuiltin(ch) || m.ContainsKey(ch)) continue;
                m[ch] = c;
            }
            return m;
        }

        /// <summary>Built-in symbols followed by the project's custom markers.</summary>
        public static List<LegendEntry> All(IList<MarkerDef> custom)
        {
            var list = new List<LegendEntry>(Builtin);
            foreach (var kv in CustomMap(custom))
                list.Add(new LegendEntry
                {
                    ch = kv.Key, name = string.IsNullOrEmpty(kv.Value.label) ? kv.Value.spawnId : kv.Value.label,
                    help = $"Marker tùy biến: sinh '{kv.Value.spawnId}' ({Mathf.RoundToInt(kv.Value.chance * 100f)}%).",
                    group = "Custom", color = kv.Value.color, solid = TileGrid.IsSolidType(kv.Value.tile), custom = true
                });
            return list;
        }
    }

    public struct LintIssue
    {
        public bool error;
        public string message;
        public LintIssue(bool error, string message) { this.error = error; this.message = message; }
    }

    /// <summary>
    /// Rule checks for one room template: the conventions that keep rooms connectable (see the header of
    /// RoomTemplates.txt). Errors mean the room will misbehave; warnings are things worth a second look.
    /// The real proof is still LevelValidator, which the generator runs on every level.
    /// </summary>
    public static class RoomLint
    {
        public static List<LintIssue> Check(RoomTemplate t, IList<MarkerDef> custom = null)
        {
            var issues = new List<LintIssue>();
            var legend = new Dictionary<char, LegendEntry>();
            foreach (var e in RoomLegend.All(custom)) legend[e.ch] = e;
            const int W = RoomTemplates.W, H = RoomTemplates.H;

            void Err(string m) => issues.Add(new LintIssue(true, m));
            void Warn(string m) => issues.Add(new LintIssue(false, m));
            bool Solid(int x, int r) => x < 0 || x >= W || r < 0 || r >= H || (legend.TryGetValue(t.cells[x, r], out var e) && e.solid);
            bool MaybeSolid(int x, int r) => Solid(x, r) || t.cells[x, r] == '?' || t.cells[x, r] == '=' || t.cells[x, r] == 'H';
            int Count(char ch) { int n = 0; foreach (char c in t.cells) if (c == ch) n++; return n; }

            if (string.IsNullOrWhiteSpace(t.name)) Err("Phòng chưa có tên.");
            else if (t.name.IndexOfAny(new[] { ' ', '[', ']', '=' }) >= 0) Err("Tên phòng không được chứa dấu cách, [ ] hoặc =.");
            if (t.types.Count == 0) Err("Chưa chọn loại phòng (types).");
            if (t.weight <= 0f) Warn("Weight <= 0: phòng này sẽ không bao giờ được chọn.");

            var unknown = new HashSet<char>();
            foreach (char c in t.cells) if (!legend.ContainsKey(c)) unknown.Add(c);
            foreach (char c in unknown) Err($"Ký hiệu lạ '{c}' (không có trong bảng ký hiệu).");

            bool shop = t.HasTag("shop");
            foreach (int type in t.types)
            {
                if (type < 0 || type > 4) { Err($"Loại phòng {type} không hợp lệ (0-4)."); continue; }
                if (type >= 1)
                {
                    bool left = !Solid(0, 4) || !Solid(0, 5) || !Solid(0, 6);
                    bool right = !Solid(W - 1, 4) || !Solid(W - 1, 5) || !Solid(W - 1, 6);
                    if (!left) Err($"Loại {type}: cạnh TRÁI phải hở ít nhất 1 ô ở hàng 4-6 để nối phòng bên cạnh.");
                    if (!right) Err($"Loại {type}: cạnh PHẢI phải hở ít nhất 1 ô ở hàng 4-6 để nối phòng bên cạnh.");
                }
                if (type == 2 || type == 4)
                    if (Solid(4, H - 1) || Solid(5, H - 1)) Err($"Loại {type}: lối xuống ở hàng 7 phải hở cả cột 4 và 5.");
                if (type == 3 || type == 4)
                {
                    bool top = false;
                    for (int x = 3; x <= 6; x++) if (!Solid(x, 0)) top = true;
                    if (!top) Err($"Loại {type}: lối lên ở hàng 0 phải hở trong các cột 3-6.");
                }
                if (type == 4)
                {
                    bool broken = false;
                    for (int x = 4; x <= 5; x++)
                        for (int r = 1; r < H - 1; r++) if (MaybeSolid(x, r)) broken = true;
                    if (!broken) Warn("Loại 4: nên có gờ / sàn gỗ ở cột 4-5 để chặn cú rơi xuyên phòng.");
                }
            }
            if (shop && t.types.Contains(0))
            {
                // shop rooms open to the right only (the generator mirrors them)
                if (Solid(W - 1, 4) && Solid(W - 1, 5) && Solid(W - 1, 6)) Err("Phòng shop phải hở cạnh PHẢI ở hàng 4-6 (bộ sinh màn tự lật khi cần).");
            }

            int doors = Count('D');
            if (t.HasTag("start") || t.HasTag("exit"))
            {
                if (doors != 1) Err($"Phòng start/exit cần đúng 1 cửa 'D' (đang có {doors}).");
            }
            else if (doors > 0) Warn("'D' chỉ có tác dụng trong phòng tag start / exit.");
            if (shop && (Count('K') != 1 || Count('S') == 0)) Err("Phòng shop cần đúng 1 chủ tiệm 'K' và ít nhất 1 chỗ bày hàng 'S'.");
            if (!shop && (Count('K') > 0 || Count('S') > 0)) Warn("'K' / 'S' chỉ có tác dụng trong phòng tag shop.");
            if (t.HasTag("idol") && Count('O') != 1) Err("Phòng idol cần đúng 1 bệ tượng 'O'.");
            if (t.HasTag("gate_rope") && Count('R') == 0) Warn("Phòng gate_rope nên có 'R' (chỗ đứng ném dây) để validator biết móc dây ở đâu.");
            if (t.HasTag("gate_bomb") && Count('%') == 0) Warn("Phòng gate_bomb nên có đá vụn '%'.");
            if (t.HasTag("gate_glider") && Count('~') == 0) Warn("Phòng gate_glider nên có vực gai '~'.");
            foreach (var tag in t.tags)
                if (tag.StartsWith("gate_") && !t.types.Contains(1)) Warn($"Phòng {tag} chỉ được đặt vào phòng loại 1 (trái-phải).");

            for (int r = 0; r < H; r++)
                for (int x = 0; x < W; x++)
                {
                    char c = t.cells[x, r];
                    bool floorBelow = r == H - 1 || MaybeSolid(x, r + 1);
                    bool roofAbove = r == 0 || MaybeSolid(x, r - 1);
                    if ((c == 'E' || c == '^' || c == '!' || c == 'D' || c == 'K' || c == 'S' || c == 'O' || c == 'R') && !floorBelow)
                        Warn($"'{c}' tại cột {x}, hàng {r} không có sàn bên dưới.");
                    if ((c == 'B' || c == 'v') && !roofAbove)
                        Warn($"'{c}' tại cột {x}, hàng {r} không có trần phía trên.");
                    if (c == 'f' && r > 0 && Solid(x, r - 1))
                        Warn($"Lỗ lửa 'f' tại cột {x}, hàng {r} bị bịt phía trên.");
                }
            return issues;
        }
    }
}
