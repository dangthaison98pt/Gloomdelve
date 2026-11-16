using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// One 10x8 room layout parsed from Data/RoomTemplates.txt.
    /// Header:  [name] types=1,2 tags=start areas=1,2 weight=2
    /// Room types: 0 side, 1 left-right, 2 left-right-bottom, 3 left-right-top, 4 all four.
    /// </summary>
    public class RoomTemplate
    {
        public string name;
        public HashSet<int> types = new HashSet<int>();
        public HashSet<string> tags = new HashSet<string>();
        public HashSet<int> areas = new HashSet<int>();
        public float weight = 1f;
        public char[,] cells = new char[RoomTemplates.W, RoomTemplates.H];   // [x, row]  row 0 = top

        public bool ForArea(int a) => areas.Count == 0 || areas.Contains(a);
        public bool HasTag(string t) => tags.Contains(t);
    }

    public static class RoomTemplates
    {
        public const int W = 10, H = 8;

        static readonly Dictionary<string, List<RoomTemplate>> cache = new Dictionary<string, List<RoomTemplate>>();

        public static List<RoomTemplate> Parse(string text)
        {
            if (cache.TryGetValue(text, out var cached)) return cached;
            var list = new List<RoomTemplate>();
            var lines = text.Replace("\r", "").Split('\n');
            RoomTemplate cur = null;
            int row = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                if (cur == null)
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    if (!t.StartsWith("[")) continue;
                    cur = ParseHeader(t);
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
            cache[text] = list;
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
                    case "types": foreach (var v in kv[1].Split(',')) rt.types.Add(int.Parse(v)); break;
                    case "tags": foreach (var v in kv[1].Split(',')) if (v.Length > 0) rt.tags.Add(v); break;
                    case "areas": foreach (var v in kv[1].Split(',')) rt.areas.Add(int.Parse(v)); break;
                    case "weight": rt.weight = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture); break;
                }
            }
            return rt;
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
