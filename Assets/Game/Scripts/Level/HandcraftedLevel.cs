using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Parses a hand-made level (tutorial, final vault).
    /// Header lines "key: value" (sign1..sign9 = sign texts), then a line "---", then rows top to bottom.
    /// Legend: . empty  # dirt  X bedrock  B brick  = platform  H ladder  ^ spikes  A arrow trap  f flame vent
    ///         v stalactite  w crusher  $ gold vein  T gold  * gem  S entrance  G exit  Y Heartstone on altar
    ///         s snake  b bat  x spider  t toad  r brute  e ember bat  o rock  u pot  c crate  k bomb bag  q rope pile
    ///         Z boss (id from the header line "boss: id")  h treasure chest  K shopkeeper  M shop item
    ///         1-9 signs   + any symbol from GameConfig.customMarkers
    /// </summary>
    public static class HandcraftedLevel
    {
        public static LevelData Parse(string text, string name, LevelKind kind, int seed, IList<MarkerDef> customMarkers = null)
        {
            var custom = RoomLegend.CustomMap(customMarkers);
            var lines = text.Replace("\r", "").Split('\n');
            var header = new Dictionary<string, string>();
            int i = 0;
            for (; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.Trim() == "---") { i++; break; }
                int c = l.IndexOf(':');
                if (c > 0) header[l.Substring(0, c).Trim()] = l.Substring(c + 1).Trim();
            }
            var rows = new List<string>();
            for (; i < lines.Length; i++)
            {
                var l = lines[i].TrimEnd();
                if (l.Length == 0) continue;
                rows.Add(l);
            }
            int H = rows.Count, W = 0;
            foreach (var r in rows) W = Math.Max(W, r.Length);
            var g = new TileGrid(W, H);
            var d = new LevelData { name = name, kind = kind, seed = seed, grid = g };
            var rng = new Rng(seed);
            bool hasEntrance = false, hasExit = false;
            int houses = 0, rooms = 0;

            for (int ry = 0; ry < H; ry++)
            {
                string row = rows[ry];
                int y = H - 1 - ry;
                for (int x = 0; x < W; x++)
                {
                    char ch = x < row.Length ? row[x] : 'X';
                    var cell = new Vector2Int(x, y);
                    TileType t = TileType.Empty;
                    switch (ch)
                    {
                        case '#': t = TileType.Dirt; break;
                        case 'X': t = TileType.Bedrock; break;
                        case 'B': t = TileType.Brick; break;
                        case '=': t = TileType.Platform; break;
                        case 'H': t = TileType.Ladder; break;
                        case '^': t = TileType.Spikes; break;
                        case 'A': t = TileType.ArrowTrap; break;
                        case 'f': t = TileType.VentBlock; break;
                        case '$': t = TileType.GoldVein; break;
                        case '%': t = TileType.Rubble; break;
                        case 'S': d.entrance = cell; hasEntrance = true; break;
                        case 'G': d.exit = cell; hasExit = true; break;
                        case 'Y': d.goal = cell; d.spawns.Add(new SpawnPoint("altar", cell)); d.spawns.Add(new SpawnPoint("heartstone", cell)); break;
                        case 'v': d.spawns.Add(new SpawnPoint("stalactite", cell)); break;
                        case 'w': d.spawns.Add(new SpawnPoint("crusher", cell)); break;
                        case 'T': d.spawns.Add(new SpawnPoint(rng.Chance(0.6f) ? "gold_small" : "gold_big", cell)); break;
                        case '*': d.spawns.Add(new SpawnPoint("gem", cell)); break;
                        case 's': d.spawns.Add(new SpawnPoint("snake", cell, -1)); break;
                        case 'b': d.spawns.Add(new SpawnPoint("bat", cell)); break;
                        case 'x': d.spawns.Add(new SpawnPoint("spider", cell)); break;
                        case 't': d.spawns.Add(new SpawnPoint("toad", cell, -1)); break;
                        case 'r': d.spawns.Add(new SpawnPoint("brute", cell, -1)); break;
                        case 'e': d.spawns.Add(new SpawnPoint("emberbat", cell)); break;
                        case 'o': d.spawns.Add(new SpawnPoint("rock", cell)); break;
                        case 'u': d.spawns.Add(new SpawnPoint("pot", cell)); break;
                        case 'c': d.spawns.Add(new SpawnPoint("crate", cell)); break;
                        case 'k': d.spawns.Add(new SpawnPoint("bombbag", cell)); break;
                        case 'q': d.spawns.Add(new SpawnPoint("ropepile", cell)); break;
                        case 'Z': if (header.TryGetValue("boss", out var bossId) && bossId.Length > 0) d.spawns.Add(new SpawnPoint(bossId, cell, -1)); break;
                        case 'h': d.spawns.Add(new SpawnPoint("chest", cell)); break;
                        case 'K': d.spawns.Add(new SpawnPoint("shopkeeper", cell, -1)); break;
                        case 'M': d.spawns.Add(new SpawnPoint("shopitem", cell)); break;
                        // Brindlemoor: the doors of the street (V = the villagers' houses in the order of Villagers.All,
                        // W = the village hall, Q = the player's house) and the rooms behind them (I, in the order
                        // forge, ropewalk, apothecary, map room, oddments, lamp house, hall, home: the door of each room)
                        case 'V': d.spawns.Add(new SpawnPoint("building", cell, 0, houses++)); break;
                        case 'W': d.spawns.Add(new SpawnPoint("building", cell, 0, 6)); break;
                        case 'Q': d.spawns.Add(new SpawnPoint("building", cell, 0, 7)); break;
                        case 'I': d.spawns.Add(new SpawnPoint("interior", cell, 0, rooms++)); break;
                        // things that stand in the street
                        case 'i': d.spawns.Add(new SpawnPoint("sandpillar", cell)); break;          // a stone slab that rises and sinks, in this cell and the next (the Dune Queen's chamber)
                        case '~': d.spawns.Add(new SpawnPoint("quicksand", cell)); break;          // a cell of quicksand in the floor (the Dune Queen's chamber)
                        case 'C': d.spawns.Add(new SpawnPoint("cocoon", cell)); break;             // a cocoon on its thread (the Broodmother's den)
                        case 'R': d.spawns.Add(new SpawnPoint("pavement", cell)); break;           // the paved street (put it in the second cell of the row)
                        case 'L': d.spawns.Add(new SpawnPoint("streetlamp", cell)); break;         // lamp post, a string of lights running on to the next
                        case 'J': d.spawns.Add(new SpawnPoint("streetlamp_end", cell)); break;     // the last post of the row
                        case 'U': d.spawns.Add(new SpawnPoint("bench", cell)); break;
                        case 'F': d.spawns.Add(new SpawnPoint("firtree", cell)); break;
                        case 'N': d.spawns.Add(new SpawnPoint("snowman", cell)); break;
                        case 'D': d.spawns.Add(new SpawnPoint("woodpile", cell)); break;
                        case 'E': d.spawns.Add(new SpawnPoint("cratestack", cell)); break;
                        default:
                            if (ch >= '1' && ch <= '9')
                            {
                                header.TryGetValue("sign" + ch, out var msg);
                                d.spawns.Add(new SpawnPoint("sign", cell, 0, ch - '0', msg ?? ""));
                                d.signs.Add(msg ?? "");
                            }
                            else if (custom.TryGetValue(ch, out var cm))
                            {
                                t = cm.tile;
                                if (!string.IsNullOrEmpty(cm.spawnId)) d.spawns.Add(new SpawnPoint(cm.spawnId, cell, -1));
                            }
                            break;
                    }
                    g.SetRaw(x, y, t);
                }
            }
            if (!hasEntrance) throw new FormatException($"Level '{name}' has no entrance (S)");
            if (!hasExit) throw new FormatException($"Level '{name}' has no exit (G)");

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = g.Get(x, y);
                    if (t == TileType.ArrowTrap)
                    {
                        int dir = !g.IsSolid(x - 1, y) && g.IsSolid(x + 1, y) ? -1 : (!g.IsSolid(x + 1, y) ? 1 : -1);
                        d.spawns.Add(new SpawnPoint("arrowtrap", new Vector2Int(x, y), dir));
                    }
                    else if (t == TileType.VentBlock)
                        d.spawns.Add(new SpawnPoint("flamevent", new Vector2Int(x, y), 0, (x * 3 + y) % 7));
                }
            // "entrance: hidden" in the header: the player simply is there (waking up at home), no doorway is drawn
            bool hiddenEntrance = header.TryGetValue("entrance", out var entranceMode) && entranceMode.Trim() == "hidden";
            if (!hiddenEntrance) d.spawns.Insert(0, new SpawnPoint("door_entrance", d.entrance));
            d.spawns.Insert(hiddenEntrance ? 0 : 1, new SpawnPoint("door_exit", d.exit));
            return d;
        }
    }
}
