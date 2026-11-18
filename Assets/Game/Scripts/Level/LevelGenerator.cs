using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    public class GenRequest
    {
        public int seed;
        public AreaConfig area;
        public int levelNumber = 1;
        public string name;
        public GameConfig config;
        public List<RoomTemplate> templates;
        /// <summary>Testing: use this template in every room it fits (Room Template Editor). Null in the real game.</summary>
        public RoomTemplate forceTemplate;
        /// <summary>This level hides the run's caged villager in one of its side rooms (see Villagers.CaptiveStage).</summary>
        public bool captive;
    }

    /// <summary>
    /// Room-based generator: a solution path is walked through a grid of rooms (left/right/down, never backtracking;
    /// areas with AreaConfig.pathUp > 0 may also climb, and can run bottom to top; areas with AreaConfig.pathAcross
    /// run from one side of the map to the other),
    /// each room gets a template matching its required openings, templates contain probabilistic tiles and marker
    /// slots (enemies, traps, treasure), then the result is proven traversable by LevelValidator. Seeded and deterministic.
    /// </summary>
    public static class LevelGenerator
    {
        const int RW = RoomTemplates.W, RH = RoomTemplates.H;

        enum RoomKind { Path, Side, Start, Exit, Shop, Idol, Vault, Gate, Captive }

        struct Marker
        {
            public Vector2Int cell; public char ch; public RoomKind kind;
        }

        public static LevelData Generate(GenRequest req)
        {
            var opts = LevelValidator.DefaultOptions(req.config.player);
            LevelData last = null;
            var reasons = new List<string>();
            int max = Mathf.Max(1, req.config.maxGenerationAttempts);
            LevelData Attempt(int a)
            {
                var rng = new Rng(Rng.Hash(req.seed, a * 7919 + 1));
                var d = Build(req, rng);
                d.attempts = a + 1;
                d.validation = LevelValidator.Validate(d, opts);
                return d;
            }
            // The first attempt usually passes and runs alone. When it does not, the following attempts are built
            // and checked side by side on the other processor cores, and the earliest one that passes wins - the
            // same level as trying them one after another, without waiting for each failure in turn.
            int batch = Mathf.Clamp(System.Environment.ProcessorCount - 1, 1, 8);
            for (int a0 = 0; a0 < max;)
            {
                int n = a0 == 0 ? 1 : Mathf.Min(batch, max - a0);
                var res = new LevelData[n];
                if (n == 1) res[0] = Attempt(a0);
                else
                {
                    int first = a0;
                    try { System.Threading.Tasks.Parallel.For(0, n, i => res[i] = Attempt(first + i)); }
                    catch (System.AggregateException e) { throw e.InnerException ?? e; }
                }
                for (int i = 0; i < n; i++)
                {
                    var d = res[i];
                    if (d.validation.ok)
                    {
                        d.notes.AddRange(reasons);
                        return d;
                    }
                    reasons.Add($"attempt {a0 + i + 1}: {d.validation.reason}");
                    last = d;
                }
                a0 += n;
            }
            Debug.LogError($"[Gen] Seed {req.seed} '{req.name}': no valid layout after {max} attempts. Using last attempt.");
            last.notes.AddRange(reasons);
            return last;
        }

        static bool IsSpecialTag(RoomTemplate t) => t.IsSpecial;

        /// <summary>Whether a forced test template may fill a room of this kind / type.</summary>
        static bool ForceFits(RoomTemplate f, RoomKind kind, int type, string gear)
        {
            switch (kind)
            {
                case RoomKind.Start: return f.HasTag("start") && f.types.Contains(type);
                case RoomKind.Exit: return f.HasTag("exit") && f.types.Contains(type);
                case RoomKind.Shop: return f.HasTag("shop");
                case RoomKind.Idol: return f.HasTag("idol");
                case RoomKind.Vault: return f.HasTag("vault");
                case RoomKind.Captive: return f.HasTag("captive");
                case RoomKind.Gate: return f.HasTag("gate_" + gear) && f.types.Contains(type);
                default: return !f.IsSpecial && f.types.Contains(type);
            }
        }

        public static LevelData Build(GenRequest req, Rng rng)
        {
            var area = req.area;
            var cfg = req.config;
            var custom = RoomLegend.CustomMap(cfg.customMarkers);
            int rx = Mathf.Max(2, area.roomsX), ry = Mathf.Max(2, area.roomsY);
            float scale = 1f + Mathf.Max(0, req.levelNumber - 1) * area.perLevelDensityBonus;

            // ---------------------------------------------------------------- 1. solution path
            bool needsGateRoom = !string.IsNullOrEmpty(area.requiredGear);
            int[,] types = null;
            bool[,] onPath = null;
            // Climbing levels (AreaConfig.pathUp): rooms the path leaves through the top / enters from below.
            bool[,] climbOut = new bool[rx, ry], climbIn = new bool[rx, ry];
            bool across = area.pathAcross;
            bool freeWalk = area.pathUp > 0f && !across;
            int startRow = freeWalk && area.pathStartBottom ? ry - 1 : 0;
            int sx = 0, cx = 0, cy = 0;
            // Sideways levels (AreaConfig.pathAcross): in at one side of the map, out at the other. Every step either
            // heads for the far side or changes floor (a drop, or a ladder up); the path never turns back, so it
            // cannot cross itself. The first and the last room are entered / left sideways.
            for (int walk = 0; across && walk < 80; walk++)
            {
                types = new int[rx, ry];
                onPath = new bool[rx, ry];
                climbOut = new bool[rx, ry]; climbIn = new bool[rx, ry];
                var openTop = new bool[rx, ry]; var openBottom = new bool[rx, ry];
                int dir = rng.Chance(0.5f) ? 1 : -1;
                int goalCol = dir > 0 ? rx - 1 : 0;
                sx = dir > 0 ? 0 : rx - 1;
                startRow = rng.Range(0, ry);
                cx = sx; cy = startRow;
                onPath[cx, cy] = true;
                int len = 1, floors = 0;
                bool lastSide = false, done = false, stuck = false;
                for (int guard = 0; guard < 200 && !done && !stuck; guard++)
                {
                    if (cx == goalCol && lastSide) { done = true; break; }
                    // two floor changes in a row are allowed, three are not: the level is about going sideways
                    float wF = area.pathSide;
                    float wD = len > 1 && floors < 2 && cx != goalCol && cy + 1 < ry && !onPath[cx, cy + 1] ? area.pathDown : 0f;
                    float wU = len > 1 && floors < 2 && cx != goalCol && cy - 1 >= 0 && !onPath[cx, cy - 1] ? area.pathUp : 0f;
                    float total = wF + wD + wU;
                    if (total <= 0f) { stuck = true; break; }
                    float roll = rng.Range(0f, total);
                    if (roll < wF) { cx += dir; lastSide = true; floors = 0; }
                    else if (roll < wF + wD) { openBottom[cx, cy] = true; cy++; openTop[cx, cy] = true; lastSide = false; floors++; }
                    else { openTop[cx, cy] = true; climbOut[cx, cy] = true; cy--; openBottom[cx, cy] = true; climbIn[cx, cy] = true; lastSide = false; floors++; }
                    onPath[cx, cy] = true;
                    len++;
                }
                for (int y = 0; y < ry; y++)
                    for (int x = 0; x < rx; x++)
                        types[x, y] = !onPath[x, y] ? 0 : openTop[x, y] && openBottom[x, y] ? 4 : openBottom[x, y] ? 2 : openTop[x, y] ? 3 : 1;
                if (!done) continue;
                if (!needsGateRoom) break;
                bool cand = false;
                for (int y = 0; y < ry && !cand; y++)
                    for (int x = 0; x < rx; x++)
                        if (onPath[x, y] && types[x, y] == 1 && !(x == sx && y == startRow) && !(x == cx && y == cy)) { cand = true; break; }
                if (cand) break;
            }
            // Walk the solution path; areas with gear gates re-walk (same rng stream) until the path has at least
            // one plain left-right room (not start/exit) to turn into a gate.
            for (int walk = 0; freeWalk && walk < 80; walk++)
            {
                // Free walk: sideways, down or up by the area's weights, never crossing itself. The start and exit
                // rooms are always entered / left sideways, so they stay plain left-right rooms.
                types = new int[rx, ry];
                onPath = new bool[rx, ry];
                climbOut = new bool[rx, ry]; climbIn = new bool[rx, ry];
                var openTop = new bool[rx, ry]; var openBottom = new bool[rx, ry];
                int goalRow = area.pathStartBottom ? 0 : ry - 1;
                int minLen = ry + 2;
                sx = rng.Range(0, rx);
                cx = sx; cy = startRow;
                onPath[cx, cy] = true;
                int len = 1;
                bool lastSide = false, done = false;
                for (int guard = 0; guard < 200 && !done; guard++)
                {
                    if (cy == goalRow && lastSide && len >= minLen && rng.Chance(0.6f)) { done = true; break; }
                    float wL = cx - 1 >= 0 && !onPath[cx - 1, cy] ? area.pathSide : 0f;
                    float wR = cx + 1 < rx && !onPath[cx + 1, cy] ? area.pathSide : 0f;
                    float wD = len > 1 && cy + 1 < ry && !onPath[cx, cy + 1] ? area.pathDown : 0f;
                    float wU = len > 1 && cy - 1 >= 0 && !onPath[cx, cy - 1] ? area.pathUp : 0f;
                    float total = wL + wR + wD + wU;
                    if (total <= 0f) { done = cy == goalRow && lastSide && len >= 3; break; }
                    float roll = rng.Range(0f, total);
                    if (roll < wL) { cx--; lastSide = true; }
                    else if (roll < wL + wR) { cx++; lastSide = true; }
                    else if (roll < wL + wR + wD) { openBottom[cx, cy] = true; cy++; openTop[cx, cy] = true; lastSide = false; }
                    else { openTop[cx, cy] = true; climbOut[cx, cy] = true; cy--; openBottom[cx, cy] = true; climbIn[cx, cy] = true; lastSide = false; }
                    onPath[cx, cy] = true;
                    len++;
                }
                for (int y = 0; y < ry; y++)
                    for (int x = 0; x < rx; x++)
                        types[x, y] = !onPath[x, y] ? 0 : openTop[x, y] && openBottom[x, y] ? 4 : openBottom[x, y] ? 2 : openTop[x, y] ? 3 : 1;
                if (!done) continue;
                if (!needsGateRoom) break;
                bool cand = false;
                for (int y = 0; y < ry && !cand; y++)
                    for (int x = 0; x < rx; x++)
                        if (onPath[x, y] && types[x, y] == 1 && !(x == sx && y == startRow) && !(x == cx && y == cy)) { cand = true; break; }
                if (cand) break;
            }
            for (int walk = 0; !freeWalk && !across && walk < 30; walk++)
            {
                types = new int[rx, ry];
                onPath = new bool[rx, ry];
                sx = rng.Range(0, rx);
                cx = sx; cy = 0;
                types[cx, cy] = 1; onPath[cx, cy] = true;
                for (int guard = 0; guard < 500; guard++)
                {
                    int roll = rng.Range(1, 6);
                    int move = roll <= 2 ? -1 : (roll <= 4 ? 1 : 0);
                    if (move != 0)
                    {
                        int nx = cx + move;
                        if (nx >= 0 && nx < rx && !onPath[nx, cy])
                        {
                            cx = nx; onPath[cx, cy] = true;
                            if (types[cx, cy] == 0) types[cx, cy] = 1;
                            continue;
                        }
                    }
                    if (cy == ry - 1) break;
                    types[cx, cy] = types[cx, cy] == 3 ? 4 : 2;
                    cy++;
                    onPath[cx, cy] = true;
                    types[cx, cy] = 3;
                }
                if (!needsGateRoom) break;
                bool hasCandidate = false;
                for (int y = 0; y < ry && !hasCandidate; y++)
                    for (int x = 0; x < rx; x++)
                        if (onPath[x, y] && types[x, y] == 1 && !(x == sx && y == 0) && !(x == cx && y == cy)) { hasCandidate = true; break; }
                if (hasCandidate) break;
            }
            var startRoom = new Vector2Int(sx, startRow);
            var exitRoom = new Vector2Int(cx, cy);

            // ---------------------------------------------------------------- 2. special rooms
            var kinds = new RoomKind[rx, ry];
            for (int y = 0; y < ry; y++)
                for (int x = 0; x < rx; x++)
                    kinds[x, y] = onPath[x, y] ? RoomKind.Path : RoomKind.Side;
            kinds[startRoom.x, startRoom.y] = RoomKind.Start;
            kinds[exitRoom.x, exitRoom.y] = RoomKind.Exit;

            // Terrain puzzle gates: path rooms that can only be crossed with the area's required gear.
            string gear = string.IsNullOrEmpty(area.requiredGear) ? null : area.requiredGear;
            int wantGates = 0;
            if (gear != null && area.gatesByLevel != null && area.gatesByLevel.Length > 0)
                wantGates = area.gatesByLevel[Mathf.Clamp(req.levelNumber - 1, 0, area.gatesByLevel.Length - 1)];
            int gatesPlaced = 0;
            if (wantGates > 0)
            {
                var gateCand = new List<Vector2Int>();
                for (int y = 0; y < ry; y++)
                    for (int x = 0; x < rx; x++)
                        if (kinds[x, y] == RoomKind.Path && types[x, y] == 1) gateCand.Add(new Vector2Int(x, y));
                while (gatesPlaced < wantGates && gateCand.Count > 0)
                {
                    int k = rng.Range(0, gateCand.Count);
                    var r = gateCand[k];
                    gateCand.RemoveAt(k);
                    kinds[r.x, r.y] = RoomKind.Gate;
                    gatesPlaced++;
                }
            }

            var mirror = new bool[rx, ry];
            float shopChance = area.shopChanceByLevel != null && req.levelNumber - 1 < area.shopChanceByLevel.Length
                ? area.shopChanceByLevel[req.levelNumber - 1] : 0f;
            if (rng.Chance(shopChance))
            {
                var cand = new List<(Vector2Int room, int dir)>();
                for (int y = 0; y < ry; y++)
                    for (int x = 0; x < rx; x++)
                    {
                        if (kinds[x, y] != RoomKind.Side) continue;
                        if (x + 1 < rx && onPath[x + 1, y]) cand.Add((new Vector2Int(x, y), 1));
                        if (x - 1 >= 0 && onPath[x - 1, y]) cand.Add((new Vector2Int(x, y), -1));
                    }
                if (cand.Count > 0)
                {
                    var c = rng.Pick(cand);
                    kinds[c.room.x, c.room.y] = RoomKind.Shop;
                    mirror[c.room.x, c.room.y] = c.dir < 0;   // shop templates open to the right
                }
            }
            var sides = new List<Vector2Int>();
            for (int y = 0; y < ry; y++)
                for (int x = 0; x < rx; x++)
                    if (kinds[x, y] == RoomKind.Side) sides.Add(new Vector2Int(x, y));
            // the run's caged villager: in a side room, found only by those who leave the path (it gets first pick
            // of the side rooms so the one level that should have it always does)
            if (req.captive && sides.Count > 0)
            {
                var r = sides[rng.Range(0, sides.Count)]; sides.Remove(r); kinds[r.x, r.y] = RoomKind.Captive;
            }
            if (sides.Count > 0 && rng.Chance(area.idolRoomChance))
            {
                var r = sides[rng.Range(0, sides.Count)]; sides.Remove(r); kinds[r.x, r.y] = RoomKind.Idol;
            }
            if (sides.Count > 0 && rng.Chance(area.vaultRoomChance))
            {
                var r = sides[rng.Range(0, sides.Count)]; sides.Remove(r); kinds[r.x, r.y] = RoomKind.Vault;
            }


            // ---------------------------------------------------------------- 3. templates
            var chosen = new RoomTemplate[rx, ry];
            int areaNo = area.areaNumber;
            var all = req.templates;
            for (int y = 0; y < ry; y++)
                for (int x = 0; x < rx; x++)
                {
                    int type = types[x, y];
                    var kind = kinds[x, y];
                    int rowY = y;
                    bool RowOk(RoomTemplate rt) => area.outdoor && rowY == 0 ? rt.HasTag("surface") : !rt.HasTag("surface") && !(rt.HasTag("under") && rowY == 0);
                    RoomTemplate t = null;
                    var force = req.forceTemplate;
                    // a room the path climbs out of / into needs a template with the ladder for it
                    bool needOut = climbOut[x, y], needIn = climbIn[x, y];
                    bool UpOk(RoomTemplate rt) => rt.types.Contains(type) && rt.ForArea(areaNo) &&
                        (!needOut || rt.HasTag("up_out")) && (!needIn || rt.HasTag("up_in")) && (needOut || !rt.HasTag("up_out")) && (needIn || !rt.HasTag("up_in"));
                    if (force != null && force.ForArea(areaNo) && RowOk(force) && ForceFits(force, kind, type, gear)) t = force;
                    else if (kind == RoomKind.Path && (needOut || needIn))
                        t = RoomTemplates.Pick(all, rng, tt => UpOk(tt) && RowOk(tt)) ?? RoomTemplates.Pick(all, rng, tt => UpOk(tt));
                    else switch (kind)
                    {
                        case RoomKind.Start: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("start") && tt.types.Contains(type) && tt.ForArea(areaNo) && RowOk(tt)) ?? RoomTemplates.Pick(all, rng, tt => tt.HasTag("start") && tt.types.Contains(type) && tt.ForArea(areaNo)); break;
                        case RoomKind.Exit: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("exit") && tt.types.Contains(type) && tt.ForArea(areaNo) && RowOk(tt)) ?? RoomTemplates.Pick(all, rng, tt => tt.HasTag("exit") && tt.types.Contains(type) && tt.ForArea(areaNo)); break;
                        case RoomKind.Shop: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("shop") && tt.ForArea(areaNo)); break;
                        case RoomKind.Idol: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("idol") && tt.ForArea(areaNo)); break;
                        case RoomKind.Vault: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("vault") && tt.ForArea(areaNo)); break;
                        case RoomKind.Captive: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("captive") && tt.ForArea(areaNo) && RowOk(tt)) ?? RoomTemplates.Pick(all, rng, tt => tt.HasTag("captive") && tt.ForArea(areaNo)); break;
                        case RoomKind.Gate: t = RoomTemplates.Pick(all, rng, tt => tt.HasTag("gate_" + gear) && tt.types.Contains(type) && RowOk(tt)); break;
                    }
                    if (t == null)
                        t = RoomTemplates.Pick(all, rng, tt => !IsSpecialTag(tt) && tt.types.Contains(type) && tt.ForArea(areaNo) && RowOk(tt))
                            ?? RoomTemplates.Pick(all, rng, tt => !IsSpecialTag(tt) && tt.types.Contains(type) && tt.ForArea(areaNo))
                            ?? RoomTemplates.Pick(all, rng, tt => !IsSpecialTag(tt) && tt.types.Contains(type));
                    if (t == null) throw new System.Exception($"No room template for type {type}");
                    chosen[x, y] = t;
                    if (kind != RoomKind.Shop) mirror[x, y] = rng.Chance(0.5f);
                }

            // ---------------------------------------------------------------- 4. tiles + markers
            int sky = area.outdoor ? Mathf.Max(0, area.skyRows) : 0;
            int W = rx * RW + 2, H = ry * RH + 2 + sky;
            int roomTop = H - 2 - sky;   // top row of the room area
            var g = new TileGrid(W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (y == 0 || ((x == 0 || x == W - 1) && y <= roomTop) || (y == H - 1 && sky == 0)) g.SetRaw(x, y, TileType.Bedrock);

            var markers = new List<Marker>();
            var thornPits = new List<Vector2Int>();
            var data = new LevelData
            {
                name = req.name, kind = LevelKind.Area, seed = req.seed, grid = g, grip = area.groundGrip,
                roomTypes = types, roomTemplates = new string[rx, ry]
            };
            var roomRects = new Dictionary<Vector2Int, RectInt>();
            for (int ryi = 0; ryi < ry; ryi++)
                for (int rxi = 0; rxi < rx; rxi++)
                {
                    var t = chosen[rxi, ryi];
                    data.roomTemplates[rxi, ryi] = t.name + (mirror[rxi, ryi] ? "(m)" : "");
                    int ox = 1 + rxi * RW;
                    int oyTop = roomTop - ryi * RH;
                    roomRects[new Vector2Int(rxi, ryi)] = new RectInt(ox, oyTop - RH + 1, RW, RH);
                    if (t == req.forceTemplate) data.forcedRooms.Add(roomRects[new Vector2Int(rxi, ryi)]);
                    for (int r = 0; r < RH; r++)
                        for (int c = 0; c < RW; c++)
                        {
                            char ch = t.cells[mirror[rxi, ryi] ? RW - 1 - c : c, r];
                            int gx = ox + c, gy = oyTop - r;
                            TileType tt = TileType.Empty;
                            switch (ch)
                            {
                                case '#': tt = TileType.Dirt; break;
                                case 'X': tt = TileType.Bedrock; break;
                                case '?': tt = rng.Chance(0.55f) ? TileType.Dirt : TileType.Empty; break;
                                case '=': tt = TileType.Platform; break;
                                case 'H': tt = TileType.Ladder; break;
                                case '$': tt = TileType.GoldVein; break;
                                case 'A': case 'f': tt = TileType.Dirt; break;
                                case '%': tt = TileType.Rubble; break;
                                case '~': tt = TileType.Spikes; thornPits.Add(new Vector2Int(gx, gy)); break;
                                default: if (custom.TryGetValue(ch, out var cmTile)) tt = cmTile.tile; break;
                            }
                            g.SetRaw(gx, gy, tt);
                            if (RoomLegend.BuiltinMarkers.IndexOf(ch) >= 0 || custom.ContainsKey(ch))
                                markers.Add(new Marker { cell = new Vector2Int(gx, gy), ch = ch, kind = kinds[rxi, ryi] });
                        }
                    if (kinds[rxi, ryi] == RoomKind.Shop) { data.shopRects.Add(roomRects[new Vector2Int(rxi, ryi)]); data.hasShop = true; }
                }

            // Outdoor: the side border above the terrain is removed (out of bounds still blocks), so no stone
            // pillars stick up into the sky at the map edges.
            if (sky > 0)
                foreach (int bx in new[] { 0, W - 1 })
                {
                    int nx = bx == 0 ? 1 : W - 2;
                    for (int y = roomTop; y > 0 && !g.IsSolid(nx, y); y--) g.SetRaw(bx, y, TileType.Empty);
                }

            // Glider gates: the thorn pit sits on bedrock so it can never be dug out or fallen through.
            foreach (var c in thornPits)
            {
                if (c.y - 1 > 0) g.SetRaw(c.x, c.y - 1, TileType.Bedrock);
                data.lethalSpikes.Add(c);   // the chasm kills: a gate cannot be paid for with two hearts
            }

            // ---------------------------------------------------------------- 5. veins
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (g.Get(x, y) != TileType.Dirt) continue;
                    if (rng.Chance(area.gemVeinChance)) g.SetRaw(x, y, TileType.GemVein);
                    else if (rng.Chance(area.goldVeinChance)) g.SetRaw(x, y, TileType.GoldVein);
                }

            // ---------------------------------------------------------------- 6. doors
            data.entrance = FindDoor(markers, RoomKind.Start, g, roomRects[startRoom], rng);
            data.exit = FindDoor(markers, RoomKind.Exit, g, roomRects[exitRoom], rng);
            PrepareDoor(g, data.entrance);
            PrepareDoor(g, data.exit);

            // ---------------------------------------------------------------- 7. traps from markers
            var trapDir = new Dictionary<int, int>();
            var spawns = data.spawns;
            float risk(RoomKind k) => k == RoomKind.Side || k == RoomKind.Idol || k == RoomKind.Vault ? area.sideRoomRiskMultiplier : 1f;
            foreach (var m in markers)
            {
                var c = m.cell;
                if (m.kind == RoomKind.Shop && m.ch != 'K' && m.ch != 'S') { continue; }
                float p = Mathf.Clamp01(area.markedTrapChance * scale * risk(m.kind));
                switch (m.ch)
                {
                    case '^':
                        if (area.spikes && rng.Chance(p)) g.SetRaw(c.x, c.y, TileType.Spikes);
                        break;
                    case 'A':
                        if (area.arrowTraps && rng.Chance(p)) g.SetRaw(c.x, c.y, TileType.ArrowTrap);
                        break;
                    case 'f':
                        if (area.flameVents && rng.Chance(p)) g.SetRaw(c.x, c.y, TileType.VentBlock);
                        else if (area.spikes && rng.Chance(0.3f) && g.IsEmpty(c.x, c.y + 1)) g.SetRaw(c.x, c.y + 1, TileType.Spikes);
                        break;
                    case 'v':
                        if (rng.Chance(Mathf.Clamp01(area.ceilingHazardChance * scale * risk(m.kind))) && g.IsSolid(c.x, c.y + 1))
                        {
                            if (area.stalactites && (!area.crushers || rng.Chance(0.5f))) spawns.Add(new SpawnPoint("stalactite", c));
                            else if (area.crushers) spawns.Add(new SpawnPoint("crusher", c));
                        }
                        break;
                }
            }

            // ---------------------------------------------------------------- 8. random extra traps
            bool InShop(int x, int y) { foreach (var r in data.shopRects) if (r.Contains(new Vector2Int(x, y))) return true; return false; }
            RoomKind KindAt(int x, int y)
            {
                int rxi = Mathf.Clamp((x - 1) / RW, 0, rx - 1), ryi = Mathf.Clamp((roomTop - y) / RH, 0, ry - 1);
                return kinds[rxi, ryi];
            }
            for (int y = 2; y < H - 2; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (InShop(x, y)) continue;
                    var k = KindAt(x, y);
                    float rk = risk(k);
                    if (area.spikes && g.IsEmpty(x, y) && g.Get(x, y - 1) == TileType.Dirt && g.IsEmpty(x, y + 1) &&
                        rng.Chance(area.spikeDensity * scale * rk))
                        g.SetRaw(x, y, TileType.Spikes);
                    else if (area.arrowTraps && g.Get(x, y) == TileType.Dirt && rng.Chance(area.arrowTrapDensity * scale * rk))
                    {
                        int d = 0;
                        if (g.IsEmpty(x + 1, y) && g.IsEmpty(x + 2, y) && g.IsEmpty(x + 3, y)) d = 1;
                        else if (g.IsEmpty(x - 1, y) && g.IsEmpty(x - 2, y) && g.IsEmpty(x - 3, y)) d = -1;
                        if (d != 0) { g.SetRaw(x, y, TileType.ArrowTrap); trapDir[y * W + x] = d; }
                    }
                }

            // ---------------------------------------------------------------- 9. enemies, items, treasure, shop, idol
            var shopOffers = BuildShopOffers(cfg, rng, area);
            int shopSlot = 0;
            var shopSpots = new List<Marker>();
            foreach (var m in markers)
            {
                var c = m.cell;
                float rk = risk(m.kind);
                switch (m.ch)
                {
                    case 'E':
                        if (m.kind == RoomKind.Shop || !g.IsEmpty(c.x, c.y) || !g.IsSolid(c.x, c.y - 1)) break;
                        if (rng.Chance(Mathf.Clamp01(area.groundEnemyChance * scale * rk)))
                            spawns.Add(new SpawnPoint(WeightedId.Pick(area.groundEnemies, rng), c, rng.Chance(0.5f) ? -1 : 1));
                        break;
                    case 'B':
                        if (m.kind == RoomKind.Shop || !g.IsEmpty(c.x, c.y) || !g.IsSolid(c.x, c.y + 1)) break;
                        if (rng.Chance(Mathf.Clamp01(area.ceilingEnemyChance * scale * rk)))
                            spawns.Add(new SpawnPoint(WeightedId.Pick(area.ceilingEnemies, rng), c));
                        break;
                    case 'I':
                        if (!g.IsEmpty(c.x, c.y)) break;
                        if (rng.Chance(area.itemChance)) spawns.Add(new SpawnPoint(WeightedId.Pick(area.items, rng), c));
                        break;
                    case 'T':
                    {
                        if (!g.IsEmpty(c.x, c.y)) break;
                        float tc = m.kind == RoomKind.Vault ? 1f : (m.kind == RoomKind.Path || m.kind == RoomKind.Start || m.kind == RoomKind.Exit ? area.treasureChancePath : area.treasureChanceSide);
                        if (rng.Chance(tc))
                        {
                            float r = rng.Value();
                            string id = r < 0.55f ? "gold_small" : (r < 0.85f ? "gold_big" : "gem");
                            spawns.Add(new SpawnPoint(id, c));
                        }
                        break;
                    }
                    case 'K':
                        if (m.kind == RoomKind.Shop) spawns.Add(new SpawnPoint("shopkeeper", c, -1));
                        break;
                    case 'S':
                        if (m.kind == RoomKind.Shop) shopSpots.Add(m);
                        break;
                    case 'R':
                        data.ropeSpots.Add(c);
                        break;
                    case '!':
                        if (gear != null && g.IsEmpty(c.x, c.y) && g.IsSolid(c.x, c.y - 1))
                        {
                            var gearDef = cfg.FindGear(gear);
                            string hint = gearDef != null && !string.IsNullOrEmpty(gearDef.gateSign) ? gearDef.gateSign
                                : $"You need your {gear.ToUpperInvariant()} to get past this. Hold [gear] to change gear.";
                            spawns.Add(new SpawnPoint("sign", c, 0, 0, hint));
                        }
                        break;
                    case 'O':
                        spawns.Add(new SpawnPoint("altar", c));
                        spawns.Add(new SpawnPoint("idol", c));
                        data.hasIdol = true;
                        break;
                    default:
                        // designer-defined marker (GameConfig.customMarkers): spawn its prefab here
                        if (!custom.TryGetValue(m.ch, out var cm) || string.IsNullOrEmpty(cm.spawnId)) break;
                        if (!g.IsEmpty(c.x, c.y) && g.Get(c.x, c.y) != cm.tile) break;   // a marker may own the tile it wrote
                        if (cm.placement == MarkerPlacement.OnFloor && !g.IsSolid(c.x, c.y - 1) && !g.IsPlatformTop(c.x, c.y - 1)) break;
                        if (cm.placement == MarkerPlacement.UnderCeiling && !g.IsSolid(c.x, c.y + 1)) break;
                        if (rng.Chance(cm.chance)) spawns.Add(new SpawnPoint(cm.spawnId, c, rng.Chance(0.5f) ? -1 : 1));
                        break;
                }
            }
            shopSpots.Sort((a, b) => a.cell.x.CompareTo(b.cell.x));
            foreach (var m in shopSpots)
            {
                if (shopSlot >= shopOffers.Count) break;
                spawns.Add(new SpawnPoint("shopitem", m.cell, 0, shopSlot, shopOffers[shopSlot]));
                shopSlot++;
            }
            if (data.hasShop)
            {
                var sr = data.shopRects[0];
                spawns.Add(new SpawnPoint("shopsign", new Vector2Int(sr.x + (mirrorShop(kinds, mirror, rx, ry) ? 1 : RW - 2), sr.y + RH - 3)));
            }

            // extra wandering enemies in path rooms
            for (int ryi = 0; ryi < ry; ryi++)
                for (int rxi = 0; rxi < rx; rxi++)
                {
                    var k = kinds[rxi, ryi];
                    if (k == RoomKind.Start || k == RoomKind.Shop) continue;
                    if (!rng.Chance(Mathf.Clamp01(area.extraEnemiesPerRoom * scale * risk(k)))) continue;
                    var rr = roomRects[new Vector2Int(rxi, ryi)];
                    for (int tries = 0; tries < 12; tries++)
                    {
                        int x = rng.Range(rr.xMin, rr.xMax), y = rng.Range(rr.yMin, rr.yMax);
                        if (g.IsEmpty(x, y) && g.IsSolid(x, y - 1) && g.IsEmpty(x, y + 1))
                        {
                            spawns.Add(new SpawnPoint(WeightedId.Pick(area.groundEnemies, rng), new Vector2Int(x, y), rng.Chance(0.5f) ? -1 : 1));
                            break;
                        }
                    }
                }

            // ---------------------------------------------------------------- 9b. outdoor: flying enemies, trees
            if (area.outdoor)
            {
                int air = Mathf.RoundToInt(area.airEnemiesPerLevel * scale);
                for (int k = 0, tries = 0; k < air && tries < 300; tries++)
                {
                    int x = rng.Range(2, W - 2), y = rng.Range(roomTop - RH + 2, H - 2);
                    bool open = true;
                    for (int dy = -4; dy <= 1 && open; dy++) if (!g.IsEmpty(x, y + dy)) open = false;
                    if (!open || (Mathf.Abs(x - data.entrance.x) <= cfg.enemySafeRadius + 2 && Mathf.Abs(y - data.entrance.y) <= cfg.enemySafeRadius + 2)) continue;
                    spawns.Add(new SpawnPoint(WeightedId.Pick(area.airEnemies, rng), new Vector2Int(x, y)));
                    k++;
                }
                for (int x = 2; x < W - 2; x++)
                {
                    if (!rng.Chance(area.treeChance)) continue;
                    int top = -1;
                    for (int y = H - 2; y > 1; y--) if (g.IsSolid(x, y)) { top = y; break; }
                    if (top < 0 || g.Get(x, top) == TileType.Bedrock && rng.Chance(0.5f)) continue;
                    bool room = true;
                    for (int dy = 1; dy <= 3 && room; dy++) if (!g.IsEmpty(x, top + dy) || !g.IsEmpty(x - 1, top + dy) || !g.IsEmpty(x + 1, top + dy)) room = false;
                    if (!room || Mathf.Abs(x - data.entrance.x) <= 1 || Mathf.Abs(x - data.exit.x) <= 1) continue;
                    spawns.Add(new SpawnPoint("tree", new Vector2Int(x, top + 1), 0, rng.Range(0, 2)));
                    x += 3;
                }
            }

            // ---------------------------------------------------------------- 9c. Sporegloom: bounce mushrooms and glowing plants
            // Both stand on plain floor. They are extras: the route check ignores them, so a level never depends on a bounce.
            if (area.bounceCapChance > 0f || area.glowPlantChance > 0f)
            {
                var taken = new HashSet<Vector2Int>();
                foreach (var sp in spawns) taken.Add(sp.cell);
                bool NearADoor(int x, int y) => (Mathf.Abs(x - data.entrance.x) <= 2 && Mathf.Abs(y - data.entrance.y) <= 2) || (Mathf.Abs(x - data.exit.x) <= 2 && Mathf.Abs(y - data.exit.y) <= 2);
                var caps = new List<Vector2Int>();
                var plants = new List<Vector2Int>();
                for (int y = 2; y < H - 2; y++)
                    for (int x = 2; x < W - 2; x++)
                    {
                        if (!g.IsEmpty(x, y) || !g.IsSolid(x, y - 1) || taken.Contains(new Vector2Int(x, y)) || NearADoor(x, y) || InShop(x, y)) continue;
                        if (g.IsSpike(x - 1, y) || g.IsSpike(x + 1, y)) continue;
                        // a bounce mushroom wants at least five cells of air above it, or the bounce just hits the ceiling
                        bool air = true;
                        for (int dy = 1; dy <= 5 && air; dy++) if (!g.IsEmpty(x, y + dy)) air = false;
                        if (air && rng.Chance(area.bounceCapChance))
                        {
                            bool near = false;
                            foreach (var c in caps) if (Mathf.Abs(c.x - x) + Mathf.Abs(c.y - y) < 6) { near = true; break; }
                            if (!near) { caps.Add(new Vector2Int(x, y)); taken.Add(new Vector2Int(x, y)); continue; }
                        }
                        if (rng.Chance(area.glowPlantChance))
                        {
                            bool near = false;
                            foreach (var c in plants) if (Mathf.Abs(c.x - x) + Mathf.Abs(c.y - y) < 5) { near = true; break; }
                            if (!near) { plants.Add(new Vector2Int(x, y)); taken.Add(new Vector2Int(x, y)); }
                        }
                    }
                foreach (var c in caps) spawns.Add(new SpawnPoint("bouncecap", c));
                foreach (var c in plants) spawns.Add(new SpawnPoint("glowshroom", c, 0, rng.Range(0, 3)));
            }

            // ---------------------------------------------------------------- 9d. Gearworks: conveyor belts
            // Only in the middle of long plain floors: three plain floor cells stay free at both ends, so a belt never
            // touches a pit, a ledge, a wall or spikes - a jump planned by the route check always starts off the belt.
            if (area.conveyorChance > 0f)
            {
                var used = new HashSet<Vector2Int>();
                foreach (var sp in spawns) used.Add(sp.cell);
                bool Plain(int x, int y)
                {
                    if (!g.IsEmpty(x, y) || !g.IsEmpty(x, y + 1) || !g.IsSolid(x, y - 1) || g.IsSpike(x - 1, y) || g.IsSpike(x + 1, y)) return false;
                    if (Mathf.Abs(x - data.entrance.x) <= 2 && Mathf.Abs(y - data.entrance.y) <= 2) return false;
                    if (Mathf.Abs(x - data.exit.x) <= 2 && Mathf.Abs(y - data.exit.y) <= 2) return false;
                    return !InShop(x, y);
                }
                for (int y = 2; y < H - 2; y++)
                    for (int x = 2; x < W - 2; x++)
                    {
                        if (!Plain(x, y)) continue;
                        int end = x;
                        while (end + 1 < W - 2 && Plain(end + 1, y)) end++;
                        int len = end - x + 1;
                        if (len >= 9 && rng.Chance(area.conveyorChance))
                        {
                            int beltLen = Mathf.Min(len - 6, rng.Range(3, 7));
                            int start = x + 3 + rng.Range(0, len - 6 - beltLen + 1);
                            int dir = rng.Chance(0.5f) ? -1 : 1;
                            for (int bx = start; bx < start + beltLen; bx++)
                                if (!used.Contains(new Vector2Int(bx, y))) spawns.Add(new SpawnPoint("conveyor", new Vector2Int(bx, y), dir));
                        }
                        x = end;
                    }
            }

            // ---------------------------------------------------------------- 9e. Gearworks: saw blades on rails
            // A rail of 3-4 cells on plain floor with four cells of air above it (room for a full jump) and two plain
            // floor cells free at each end (somewhere to take off and to land). Never on a belt.
            if (area.sawChance > 0f)
            {
                var busy = new HashSet<Vector2Int>();
                foreach (var sp in spawns) busy.Add(sp.cell);
                bool Clear(int x, int y)
                {
                    if (busy.Contains(new Vector2Int(x, y)) || !g.IsSolid(x, y - 1) || g.IsSpike(x - 1, y) || g.IsSpike(x + 1, y)) return false;
                    for (int dy = 0; dy <= 3; dy++) if (!g.IsEmpty(x, y + dy)) return false;
                    if (Mathf.Abs(x - data.entrance.x) <= 3 && Mathf.Abs(y - data.entrance.y) <= 2) return false;
                    if (Mathf.Abs(x - data.exit.x) <= 3 && Mathf.Abs(y - data.exit.y) <= 2) return false;
                    return !InShop(x, y);
                }
                for (int y = 2; y < H - 4; y++)
                    for (int x = 2; x < W - 2; x++)
                    {
                        if (!Clear(x, y)) continue;
                        int end = x;
                        while (end + 1 < W - 2 && Clear(end + 1, y)) end++;
                        int len = end - x + 1;
                        if (len >= 7 && rng.Chance(area.sawChance))
                        {
                            int rail = Mathf.Min(len - 4, rng.Range(3, 5));
                            int start = x + 2 + rng.Range(0, len - 4 - rail + 1);
                            spawns.Add(new SpawnPoint("sawblade", new Vector2Int(start, y), 0, rail));
                            for (int bx = start; bx < start + rail; bx++) busy.Add(new Vector2Int(bx, y));
                        }
                        x = end;
                    }
            }

            // ---------------------------------------------------------------- 10. safety + cleanup
            SafetyPass(data, cfg, area);

            // tile-bound trap entities from the final grid
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = g.Get(x, y);
                    if (t == TileType.ArrowTrap)
                    {
                        trapDir.TryGetValue(y * W + x, out int pre);
                        int d = ArrowDir(g, x, y, rng, pre);
                        if (d == 0) { g.SetRaw(x, y, TileType.Dirt); continue; }
                        spawns.Add(new SpawnPoint("arrowtrap", new Vector2Int(x, y), d));
                    }
                    else if (t == TileType.VentBlock)
                    {
                        if (!g.IsEmpty(x, y + 1)) { g.SetRaw(x, y, TileType.Dirt); continue; }
                        spawns.Add(new SpawnPoint("flamevent", new Vector2Int(x, y), 0, rng.Range(0, 7)));
                    }
                }
            spawns.Insert(0, new SpawnPoint("door_entrance", data.entrance));
            spawns.Insert(1, new SpawnPoint("door_exit", data.exit));
            data.gateGear = gatesPlaced > 0 ? gear : null;
            data.gateCount = gatesPlaced;
            data.gatesWanted = wantGates;
            if (wantGates > 0 && gatesPlaced == 0) data.notes.Add("no room available for a gear gate");
            return data;
        }

        static bool mirrorShop(RoomKind[,] kinds, bool[,] mirror, int rx, int ry)
        {
            for (int y = 0; y < ry; y++) for (int x = 0; x < rx; x++) if (kinds[x, y] == RoomKind.Shop) return mirror[x, y];
            return false;
        }

        static List<string> BuildShopOffers(GameConfig cfg, Rng rng, AreaConfig area)
        {
            var list = new List<string> { "bombs", "ropes", "heart" };
            var ups = new List<string>();
            if (cfg.shop != null) foreach (var o in cfg.shop.offers) if (o.isUpgrade) ups.Add(o.id);
            int want = 2;
            if (area != null && area.shopFeatured != null)
                foreach (var f in area.shopFeatured)
                    if (want > 0 && ups.Remove(f)) { list.Add(f); want--; }
            for (int i = 0; i < want && ups.Count > 0; i++)
            {
                int k = rng.Range(0, ups.Count);
                list.Add(ups[k]);
                ups.RemoveAt(k);
            }
            // shuffle the display order a little: consumables stay first
            return list;
        }

        static int ArrowDir(TileGrid g, int x, int y, Rng rng, int preset)
        {
            bool l = !g.IsSolid(x - 1, y), r = !g.IsSolid(x + 1, y);
            if (preset < 0 && l) return -1;
            if (preset > 0 && r) return 1;
            if (l && r) return rng.Chance(0.5f) ? -1 : 1;
            if (l) return -1;
            if (r) return 1;
            return 0;
        }

        static Vector2Int FindDoor(List<Marker> markers, RoomKind kind, TileGrid g, RectInt room, Rng rng)
        {
            foreach (var m in markers) if (m.kind == kind && m.ch == 'D') return m.cell;
            // Fallback: any floor cell in the room.
            for (int tries = 0; tries < 200; tries++)
            {
                int x = rng.Range(room.xMin + 1, room.xMax - 1), y = rng.Range(room.yMin + 1, room.yMax - 1);
                if (g.IsEmpty(x, y) && g.IsSolid(x, y - 1)) return new Vector2Int(x, y);
            }
            return new Vector2Int(room.xMin + RW / 2, room.yMin + 1);
        }

        static void PrepareDoor(TileGrid g, Vector2Int d)
        {
            g.SetRaw(d.x, d.y, TileType.Empty);
            if (g.Get(d.x, d.y + 1) != TileType.Bedrock) g.SetRaw(d.x, d.y + 1, TileType.Empty);
            if (!g.IsSolid(d.x, d.y - 1)) g.SetRaw(d.x, d.y - 1, TileType.Dirt);
        }

        /// <summary>Removes hazards near the entrance, inside shops, and invalid trap placements.</summary>
        public static void SafetyPass(LevelData d, GameConfig cfg, AreaConfig area)
        {
            var g = d.grid;
            var e = d.entrance;
            int R = cfg.entranceSafeRadius;
            for (int y = e.y - R; y <= e.y + R; y++)
                for (int x = e.x - R; x <= e.x + R; x++)
                {
                    var t = g.Get(x, y);
                    if (t == TileType.Spikes) g.SetRaw(x, y, TileType.Empty);
                    else if (t == TileType.ArrowTrap || t == TileType.VentBlock) g.SetRaw(x, y, TileType.Dirt);
                }
            // Arrow traps that can see the entrance spot.
            for (int x = 0; x < g.Width; x++)
            {
                if (g.Get(x, e.y) != TileType.ArrowTrap) continue;
                int dir = x < e.x ? 1 : -1;
                bool clear = true;
                for (int cx = x + dir; cx != e.x; cx += dir) if (g.IsSolid(cx, e.y)) { clear = false; break; }
                if (clear && Mathf.Abs(x - e.x) <= 9) g.SetRaw(x, e.y, TileType.Dirt);
            }
            // Doors and shops are hazard-free.
            foreach (var door in new[] { d.entrance, d.exit })
                for (int dx = -1; dx <= 1; dx++)
                    if (g.Get(door.x + dx, door.y) == TileType.Spikes) g.SetRaw(door.x + dx, door.y, TileType.Empty);
            foreach (var r in d.shopRects)
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        var t = g.Get(x, y);
                        if (t == TileType.Spikes) g.SetRaw(x, y, TileType.Empty);
                        else if (t == TileType.ArrowTrap || t == TileType.VentBlock) g.SetRaw(x, y, TileType.Dirt);
                    }
            // Spikes need a floor.
            for (int y = 1; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.Get(x, y) == TileType.Spikes && !g.IsSolid(x, y - 1)) g.SetRaw(x, y, TileType.Empty);

            // Spawns: enemies away from the entrance; no ceiling hazards above it; nothing on spikes / in walls.
            int ER = cfg.enemySafeRadius;
            var reg = cfg.prefabs;
            d.spawns.RemoveAll(sp =>
            {
                var c = sp.cell;
                // What counts as dangerous comes from the prefab itself (Entity.IsHazard / IsCeilingHazard),
                // so new enemies and traps are covered without touching this list.
                var prefab = reg != null ? reg.Get(sp.id) : null;
                bool hostile = prefab != null && prefab.IsHazard;
                bool fromCeiling = prefab != null && prefab.IsCeilingHazard;
                if (hostile && Mathf.Abs(c.x - e.x) <= ER && Mathf.Abs(c.y - e.y) <= ER) return true;
                if (fromCeiling && Mathf.Abs(c.x - e.x) <= 1 && c.y >= e.y) return true;
                if (fromCeiling && Mathf.Abs(c.x - d.exit.x) <= 0 && c.y >= d.exit.y && c.y - d.exit.y < 3) return true;
                if (hostile)
                    foreach (var r in d.shopRects) if (r.Contains(c)) return true;
                if (!g.IsEmpty(c.x, c.y) && sp.id != "arrowtrap" && sp.id != "flamevent" && sp.id != "crumble") return true;
                return false;
            });
        }
    }
}
