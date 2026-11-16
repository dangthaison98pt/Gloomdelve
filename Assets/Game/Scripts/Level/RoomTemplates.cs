using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// One 10x8 room layout parsed from Data/RoomTemplates.txt.
    /// Header:  [name] types=1,2 tags=start areas=1,2 weight=2
    /// Room types: 0 side, 1 left-right, 2 left-right-bottom, 3 left-right-top, 4 all four.
    /// Edit rooms with the Room Template Editor (menu Gloomdelve > Room Template Editor) or by hand in the text file.
    /// </summary>
    public class RoomTemplate
    {
        public string name;
        public List<int> types = new List<int>();
        public List<string> tags = new List<string>();
        public List<int> areas = new List<int>();
        public float weight = 1f;
        public char[,] cells = new char[RoomTemplates.W, RoomTemplates.H];   // [x, row]  row 0 = top
        /// <summary>Comment / blank lines that sit above this room in the file (kept when the file is saved again).</summary>
        public List<string> comments = new List<string>();

        public bool ForArea(int a) => areas.Count == 0 || areas.Contains(a);
        public bool HasTag(string t) => tags.Contains(t);

        /// <summary>start / exit / shop / idol / vault / gate_* / up_* rooms are only used for that purpose.</summary>
        public bool IsSpecial
        {
            get
            {
                foreach (var t in tags)
                    if (t == "start" || t == "exit" || t == "shop" || t == "idol" || t == "vault" || t.StartsWith("gate_") || t.StartsWith("up_")) return true;
                return false;
            }
        }

        public RoomTemplate Clone()
        {
            var c = new RoomTemplate { name = name, weight = weight };
            c.types.AddRange(types); c.tags.AddRange(tags); c.areas.AddRange(areas); c.comments.AddRange(comments);
            Array.Copy(cells, c.cells, cells.Length);
            return c;
        }

        public static RoomTemplate Blank(string name)
        {
            var t = new RoomTemplate { name = name };
            t.types.Add(1);
            for (int r = 0; r < RoomTemplates.H; r++)
                for (int x = 0; x < RoomTemplates.W; x++)
                    t.cells[x, r] = r == 0 || r == RoomTemplates.H - 1 ? '#' : '.';
            return t;
        }
    }

    public static class RoomTemplates
    {
        public const int W = 10, H = 8;

        static readonly Dictionary<string, List<RoomTemplate>> cache = new Dictionary<string, List<RoomTemplate>>();

        /// <summary>Parsed rooms of a template file (cached per text; treat the result as read-only).</summary>
        public static List<RoomTemplate> Parse(string text)
        {
            if (cache.TryGetValue(text, out var cached)) return cached;
            var list = ParseFresh(text);
            cache[text] = list;
            return list;
        }

        /// <summary>Parses into a new list that the caller may modify (used by the editor tool).</summary>
        public static List<RoomTemplate> ParseFresh(string text)
        {
            var list = new List<RoomTemplate>();
            var lines = text.Replace("\r", "").Split('\n');
            RoomTemplate cur = null;
            var pending = new List<string>();
            int row = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                if (cur == null)
                {
                    string t = line.Trim();
                    if (!t.StartsWith("["))
                    {
                        // comment or blank line between rooms: remembered so saving keeps it (blank lines only inside a block)
                        if (t.Length > 0 || pending.Count > 0) pending.Add(line.TrimEnd());
                        continue;
                    }
                    cur = ParseHeader(t);
                    while (pending.Count > 0 && pending[pending.Count - 1].Length == 0) pending.RemoveAt(pending.Count - 1);
                    cur.comments.AddRange(pending);
                    pending.Clear();
                    row = 0;
                    continue;
                }
                string r = line.TrimEnd();
                if (r.Length != W)
                    throw new FormatException($"Room template '{cur.name}' row {row} has {r.Length} chars (needs {W}) at line {li + 1}: '{r}'");
                for (int x = 0; x < W; x++) cur.cells[x, row] = r[x];
                row++;
                if (row == H) { list.Add(cur); cur = null; }
            }
            if (cur != null) throw new FormatException($"Room template '{cur.name}' is incomplete");
            return list;
        }

        static RoomTemplate ParseHeader(string t)
        {
            var rt = new RoomTemplate();
            int close = t.IndexOf(']');
            rt.name = t.Substring(1, close - 1);
            foreach (var part in t.Substring(close + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                switch (kv[0])
                {
                    case "types": foreach (var v in kv[1].Split(',')) { int n = int.Parse(v); if (!rt.types.Contains(n)) rt.types.Add(n); } break;
                    case "tags": foreach (var v in kv[1].Split(',')) if (v.Length > 0 && !rt.tags.Contains(v)) rt.tags.Add(v); break;
                    case "areas": foreach (var v in kv[1].Split(',')) { int n = int.Parse(v); if (!rt.areas.Contains(n)) rt.areas.Add(n); } break;
                    case "weight": rt.weight = float.Parse(kv[1], CultureInfo.InvariantCulture); break;
                }
            }
            return rt;
        }

        public static string HeaderLine(RoomTemplate t)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(t.name).Append("] types=").Append(string.Join(",", t.types));
            if (t.tags.Count > 0) sb.Append(" tags=").Append(string.Join(",", t.tags));
            if (t.areas.Count > 0) sb.Append(" areas=").Append(string.Join(",", t.areas));
            sb.Append(" weight=").Append(t.weight.ToString("0.###", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Writes rooms back in the RoomTemplates.txt format (comments above each room are kept).</summary>
        public static string Serialize(IList<RoomTemplate> rooms)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rooms.Count; i++)
            {
                var t = rooms[i];
                if (i > 0) sb.Append('\n');
                foreach (var c in t.comments) sb.Append(c).Append('\n');
                sb.Append(HeaderLine(t)).Append('\n');
                for (int r = 0; r < H; r++)
                {
                    for (int x = 0; x < W; x++) sb.Append(t.cells[x, r] == '\0' ? '.' : t.cells[x, r]);
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        public static RoomTemplate Pick(List<RoomTemplate> all, Rng rng, Func<RoomTemplate, bool> filter)
        {
            float total = 0f;
            foreach (var t in all) if (filter(t)) total += t.weight;
            if (total <= 0f) return null;
            float r = rng.Value() * total;
            RoomTemplate last = null;
            foreach (var t in all)
            {
                if (!filter(t)) continue;
                last = t;
                r -= t.weight;
                if (r <= 0f) return t;
            }
            return last;
        }
    }
}
