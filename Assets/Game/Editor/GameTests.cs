using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Automated checks (menu Brindlemoor/Tests or batch -executeMethod):
    ///  * SeedSweep: generates every procedural level for N seeds, proves each one with LevelValidator,
    ///    checks entrance safety and spawn sanity, records retries and failure causes.
    ///  * LogicTests: movement rules (jump height, coyote time, jump buffer, ledge grab, ladders, crawl-hang,
    ///    spikes, fall height), validator sanity, run-state reset, shop effects, template coverage.
    /// Reports go to Docs/TestResults.
    /// </summary>
    public static class GameTests
    {
        static string ResultsDir => Path.Combine(Directory.GetCurrentDirectory(), "Docs/TestResults");

        static GameConfig Cfg => AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Game/Data/GameConfig.asset");

        static int ArgInt(string name, int def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name && int.TryParse(a[i + 1], out int v)) return v;
            return def;
        }

        [MenuItem("Brindlemoor/Tests/Seed Sweep (120 seeds)")]
        public static void SeedSweepMenu() => SeedSweep(120, 1);

        [MenuItem("Brindlemoor/Tests/Logic Tests")]
        public static void LogicTestsMenu() => LogicTests();

        public static void BatchLogic()
        {
            EditorApplication.Exit(LogicTests() ? 0 : 1);
        }

        public static void BatchAll()
        {
            bool a = LogicTests();
            bool b = SeedSweep(ArgInt("-seeds", 120), ArgInt("-firstSeed", 1));
            EditorApplication.Exit(a && b ? 0 : 1);
        }

        // ================================================================== seed sweep

        public static bool SeedSweep(int seeds, int firstSeed)
        {
            var cfg = Cfg;
            Directory.CreateDirectory(ResultsDir);
            var sb = new StringBuilder();
            var maps = new StringBuilder();
            sb.AppendLine($"Brindlemoor seed sweep  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"seeds {firstSeed}..{firstSeed + seeds - 1}, stages per seed: {cfg.stages.Count(s => s.kind == LevelKind.Area)} procedural + final");
            sb.AppendLine();
            int levels = 0, fails = 0, retries = 0, maxAttempts = 0, safetyIssues = 0, shops = 0, shopsExpected = 0, idols = 0, gated = 0, gates = 0, captives = 0, rooms = 0, cells = 0, dropsTrue = 0, dropsFalse = 0, runsWithoutCage = 0;
            // gold a level holds (an estimate): lying about, carried by its enemies, in veins that touch open air, in veins buried in rock
            double goldFloor = 0, goldEnemies = 0, goldVeinsOpen = 0, goldVeinsBuried = 0, goldIdols = 0;
            double nugget = cfg.prefabs.Get("gold_small") is Pickup pn ? pn.value : 25, chunk = cfg.prefabs.Get("gold_big") is Pickup pc ? pc.value : 75,
                   gemWorth = cfg.prefabs.Get("gem") is Pickup pg ? pg.value : 150;
            var gatesByGear = new Dictionary<string, int>();
            double ms = 0, maxMs = 0;
            var reasonCount = new Dictionary<string, int>();
            var retrySeeds = new List<string>();
            var templateUse = new Dictionary<string, int>();

            for (int seed = firstSeed; seed < firstSeed + seeds; seed++)
            {
                for (int si = 0; si < cfg.stages.Length; si++)
                {
                    var st = cfg.stages[si];
                    if (st.kind != LevelKind.Area) continue;
                    var t0 = DateTime.Now;
                    var d = LevelFactory.Create(cfg, st, seed, si);
                    double el = (DateTime.Now - t0).TotalMilliseconds;
                    ms += el; maxMs = Math.Max(maxMs, el);
                    levels++;
                    retries += d.attempts - 1;
                    maxAttempts = Math.Max(maxAttempts, d.attempts);
                    if (d.attempts > 1) retrySeeds.Add($"{seed}/{st.displayName}:{d.attempts}");
                    foreach (var n in d.notes)
                    {
                        string key = n.Substring(n.IndexOf(':') + 1).Trim();
                        reasonCount[key] = reasonCount.TryGetValue(key, out int c) ? c + 1 : 1;
                    }
                    if (!d.validation.ok) { fails++; sb.AppendLine($"FAIL seed {seed} {st.displayName}: {d.validation.reason}"); }
                    var issues = SafetyIssues(d, cfg);
                    if (issues.Count > 0) { safetyIssues += issues.Count; foreach (var i in issues) sb.AppendLine($"SAFETY seed {seed} {st.displayName}: {i}"); }
                    if (d.hasShop) shops++;
                    float sc = st.area.shopChanceByLevel != null && st.levelNumber - 1 < st.area.shopChanceByLevel.Length ? st.area.shopChanceByLevel[st.levelNumber - 1] : 0f;
                    if (sc >= 1f) { shopsExpected++; if (!d.hasShop) sb.AppendLine($"NOTE seed {seed} {st.displayName}: guaranteed shop could not be placed"); }
                    if (d.hasIdol) idols++;
                    foreach (var sp in d.spawns) if (sp.id == "captive") { captives++; break; }
                    if (d.roomSolid != null) foreach (bool solid in d.roomSolid) { cells++; if (!solid) rooms++; }
                    dropsTrue += d.trueDrops; dropsFalse += d.falseDrops;
                    foreach (var sp in d.spawns)
                    {
                        if (sp.id == "gold_small") goldFloor += nugget; else if (sp.id == "gold_big") goldFloor += chunk; else if (sp.id == "gem") goldFloor += gemWorth;
                        else if (sp.id == "idol") goldIdols += cfg.prefabs.Get("idol") is Treasure idolT ? idolT.value : 0;
                        else if (cfg.prefabs.Get(sp.id) is Enemy en && en.goldDrop > 0)
                            goldEnemies += (en.goldDrop / 150 * chunk + Math.Ceiling((en.goldDrop % 150) / 50.0) * nugget) * en.goldChance * (1f - en.heartDropChance);
                    }
                    for (int ty = 1; ty < d.Height - 1; ty++)
                        for (int tx = 1; tx < d.Width - 1; tx++)
                        {
                            var tt = d.grid.Get(tx, ty);
                            if (tt != TileType.GoldVein && tt != TileType.GemVein) continue;
                            double worth = tt == TileType.GemVein ? gemWorth : nugget * 1.5 + chunk / 3.0;        // a gold vein: 1-2 nuggets, a chunk one time in three
                            bool open = d.grid.IsEmpty(tx + 1, ty) || d.grid.IsEmpty(tx - 1, ty) || d.grid.IsEmpty(tx, ty + 1) || d.grid.IsEmpty(tx, ty - 1);
                            if (open) goldVeinsOpen += worth; else goldVeinsBuried += worth;
                        }
                    if (!string.IsNullOrEmpty(st.area.requiredGear))
                    {
                        if (d.gateCount > 0) { gated++; gates += d.gateCount; gatesByGear[d.gateGear] = (gatesByGear.TryGetValue(d.gateGear, out int gg) ? gg : 0) + d.gateCount; }
                        else { safetyIssues++; sb.AppendLine($"GATE seed {seed} {st.displayName}: no mandatory {st.area.requiredGear} gate"); }
                        if (d.validation.bypassed) { safetyIssues++; sb.AppendLine($"GATE seed {seed} {st.displayName}: gate bypassable"); }
                    }
                    if (d.roomTemplates != null) foreach (var tn in d.roomTemplates) { var k = tn.Replace("(m)", ""); templateUse[k] = templateUse.TryGetValue(k, out int u) ? u + 1 : 1; }
                    if (seed < firstSeed + 2) maps.AppendLine(Ascii(d, $"seed {seed} stage {st.displayName} levelSeed {d.seed} attempts {d.attempts}"));
                }
                // every run hides one caged villager: the level of this run that is meant to have the cage has to have it
                {
                    var plan = new RunState { seed = seed };
                    RunPlan.Ensure(plan, cfg);
                    int at = Villagers.CaptiveStage(seed);
                    bool caged = false;
                    if (at >= 0 && plan.stageOrder != null && at < plan.stageOrder.Length)
                    {
                        var cs = cfg.stages[plan.stageOrder[at]].Named(plan.stageNames[at]);
                        if (cs.kind == LevelKind.Area)
                            foreach (var sp in LevelFactory.Create(cfg, cs, seed, at).spawns) if (sp.id == "captive") { caged = true; break; }
                    }
                    if (!caged) { runsWithoutCage++; sb.AppendLine($"CAGE seed {seed}: the run has no caged villager (level {at} of the run)"); }
                }
            }

            // final + tutorial
            var finalStage = cfg.stages.First(s => s.kind == LevelKind.Final);
            var fd = LevelFactory.Create(cfg, finalStage, 1, cfg.stages.Length - 1);
            bool finalOk = fd.validation != null && fd.validation.ok;
            var fplan = LevelValidator.PlanFinal(fd, LevelValidator.DefaultOptions(cfg.player), out string fwhy);
            maps.AppendLine(Ascii(fd, "FINAL"));
            bool tutOk;
            try { var td = HandcraftedLevel.Parse(cfg.tutorial.handcrafted.text, "Tutorial", LevelKind.Tutorial, 1); tutOk = td.signs.Count == 9; maps.AppendLine(Ascii(td, "TUTORIAL")); }
            catch (Exception e) { tutOk = false; sb.AppendLine("Tutorial parse error: " + e.Message); }

            sb.AppendLine();
            sb.AppendLine($"Procedural levels generated : {levels}");
            sb.AppendLine($"Levels without a valid route: {fails}");
            sb.AppendLine($"Entrance/spawn safety issues: {safetyIssues}");
            sb.AppendLine($"Total re-rolls (attempts-1) : {retries}   max attempts for one level: {maxAttempts}");
            sb.AppendLine($"Generation time             : avg {ms / Math.Max(1, levels):0.0} ms, max {maxMs:0} ms (incl. validation)");
            sb.AppendLine($"Shops placed                : {shops} (levels with guaranteed shop: {shopsExpected})");
            sb.AppendLine($"Idol rooms                  : {idols}");
            sb.AppendLine($"Levels with a caged villager: {captives}");
            sb.AppendLine($"Runs without a caged villager: {runsWithoutCage} of {seeds}");
            sb.AppendLine($"Holes in the path's floors  : {dropsTrue / (float)Math.Max(1, levels):0.0} lead on per level, {dropsFalse / (float)Math.Max(1, levels):0.0} are dead ends");
            double lv = Math.Max(1, levels);
            sb.AppendLine($"Gold per level (estimate)   : {goldFloor / lv:0} lying about + {goldEnemies / lv:0} from enemies + {goldVeinsOpen / lv:0} in veins at the surface + {goldIdols / lv:0} idols = {(goldFloor + goldEnemies + goldVeinsOpen + goldIdols) / lv:0}   (and {goldVeinsBuried / lv:0} buried in rock, bombs only)");
            sb.AppendLine($"Rooms in the grid           : {rooms} of {cells} cells ({100f * rooms / Math.Max(1, cells):0}% - the rest is solid rock)");
            sb.AppendLine($"Levels with mandatory gear  : {gated} / {levels}   gates total {gates}  ({string.Join(", ", gatesByGear.Select(k => k.Key + " " + k.Value))})");
            sb.AppendLine("  (each gated level: exit reachable WITH the gear, NOT reachable without it - checked by the validator)");
            sb.AppendLine($"Final level validator       : {(finalOk ? "OK" : "FAIL " + fd.validation?.reason)}; bot plan {(fplan != null ? fplan.Count + " frames" : "NONE " + fwhy)}");
            sb.AppendLine($"Tutorial parse              : {(tutOk ? "OK (9 signs)" : "FAIL")}");
            sb.AppendLine();
            sb.AppendLine("Re-roll causes (rejected attempts):");
            foreach (var kv in reasonCount.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
            sb.AppendLine();
            sb.AppendLine("Levels that needed re-rolls (seed/stage:attempts):");
            sb.AppendLine("  " + string.Join("  ", retrySeeds.Take(200)));
            sb.AppendLine();
            sb.AppendLine("Template usage in accepted levels:");
            foreach (var kv in templateUse.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Key,-18} {kv.Value}");
            File.WriteAllText(Path.Combine(ResultsDir, "seed_sweep.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(ResultsDir, "sample_maps.txt"), maps.ToString());
            bool ok = fails == 0 && safetyIssues == 0 && runsWithoutCage == 0 && finalOk && fplan != null && tutOk;
            Debug.Log($"[Tests] Seed sweep {(ok ? "PASSED" : "FAILED")}: {levels} levels, {fails} fails, {safetyIssues} safety issues, {retries} re-rolls, final {finalOk}");
            return ok;
        }

        static List<string> SafetyIssues(LevelData d, GameConfig cfg)
        {
            var list = new List<string>();
            var g = d.grid;
            var e = d.entrance;
            if (!g.IsSolid(e.x, e.y - 1)) list.Add("entrance has no floor");
            if (!g.IsSolid(d.exit.x, d.exit.y - 1)) list.Add("exit has no floor");
            if (!g.IsEmpty(e.x, e.y)) list.Add("entrance cell blocked");
            int R = cfg.entranceSafeRadius;
            for (int y = e.y - R; y <= e.y + R; y++)
                for (int x = e.x - R; x <= e.x + R; x++)
                {
                    var t = g.Get(x, y);
                    if (t == TileType.Spikes || t == TileType.ArrowTrap || t == TileType.VentBlock) list.Add($"{t} near entrance at {x},{y}");
                }
            for (int x = 0; x < g.Width; x++)
            {
                if (g.Get(x, e.y) != TileType.ArrowTrap) continue;
                var sp = d.spawns.FirstOrDefault(s => s.id == "arrowtrap" && s.cell == new Vector2Int(x, e.y));
                int dir = sp.dir;
                if ((dir > 0 && x < e.x) || (dir < 0 && x > e.x))
                {
                    bool clear = true;
                    for (int cx = x + dir; cx != e.x; cx += dir) if (g.IsSolid(cx, e.y)) { clear = false; break; }
                    if (clear && Math.Abs(x - e.x) <= 8) list.Add($"arrow trap at {x},{e.y} aims at the entrance");
                }
            }
            foreach (var sp in d.spawns)
            {
                bool hostile = sp.id == "snake" || sp.id == "bat" || sp.id == "spider" || sp.id == "toad" || sp.id == "brute" || sp.id == "emberbat" || sp.id == "hawk"
                    || sp.id == "penguin" || sp.id == "yeti" || sp.id == "frostbat" || sp.id == "puffshroom" || sp.id == "lurecap" || sp.id == "moth" || sp.id == "sentry" || sp.id == "drone" || sp.id == "beetle";
                if (hostile && Math.Abs(sp.cell.x - e.x) <= cfg.enemySafeRadius && Math.Abs(sp.cell.y - e.y) <= cfg.enemySafeRadius)
                    list.Add($"{sp.id} spawns {sp.cell} near entrance");
                if ((sp.id == "stalactite" || sp.id == "crusher") && Math.Abs(sp.cell.x - e.x) <= 1 && sp.cell.y >= e.y)
                    list.Add($"{sp.id} above entrance");
                if (sp.id != "arrowtrap" && sp.id != "flamevent" && sp.id != "crumble" && !g.IsEmpty(sp.cell.x, sp.cell.y)) list.Add($"{sp.id} spawned inside {g.Get(sp.cell.x, sp.cell.y)} at {sp.cell}");
                if (cfg.prefabs.Get(sp.id) == null) list.Add($"unknown spawn id {sp.id}");
            }
            return list;
        }

        public static string Ascii(LevelData d, string title)
        {
            var g = d.grid;
            var chars = new char[g.Width, g.Height];
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    char c = '.';
                    switch (g.Get(x, y))
                    {
                        case TileType.Dirt: c = '#'; break;
                        case TileType.Bedrock: c = 'X'; break;
                        case TileType.Brick: c = 'B'; break;
                        case TileType.Platform: c = '='; break;
                        case TileType.Ladder: c = 'H'; break;
                        case TileType.Spikes: c = '^'; break;
                        case TileType.ArrowTrap: c = 'A'; break;
                        case TileType.GoldVein: c = '$'; break;
                        case TileType.GemVein: c = '*'; break;
                        case TileType.VentBlock: c = 'f'; break;
                        case TileType.Rubble: c = '%'; break;
                    }
                    chars[x, y] = c;
                }
            var map = new Dictionary<string, char>
            {
                ["snake"] = 's', ["bat"] = 'b', ["spider"] = 'x', ["toad"] = 't', ["brute"] = 'r', ["emberbat"] = 'e', ["shopkeeper"] = 'K',
                ["shopitem"] = 'S', ["rock"] = 'o', ["pot"] = 'u', ["crate"] = 'c', ["gold_small"] = 'g', ["gold_big"] = 'G', ["gem"] = '*',
                ["idol"] = 'I', ["stalactite"] = 'v', ["crusher"] = 'w', ["heartstone"] = 'Y',
            };
            foreach (var sp in d.spawns) if (map.TryGetValue(sp.id, out char c) && g.InBounds(sp.cell.x, sp.cell.y)) chars[sp.cell.x, sp.cell.y] = c;
            chars[d.entrance.x, d.entrance.y] = '@';
            chars[d.exit.x, d.exit.y] = '%';
            var sb = new StringBuilder();
            sb.AppendLine("== " + title + "  (@ entrance, % exit)");
            for (int y = g.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < g.Width; x++) sb.Append(chars[x, y]);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ================================================================== logic tests

        static TileGrid Flat(int w, int h)
        {
            var g = new TileGrid(w, h);
            for (int x = 0; x < w; x++) { g.SetRaw(x, 0, TileType.Bedrock); g.SetRaw(x, 1, TileType.Dirt); }
            for (int y = 0; y < h; y++) { g.SetRaw(0, y, TileType.Bedrock); g.SetRaw(w - 1, y, TileType.Bedrock); }
            return g;
        }

        static MotorInput In(int x = 0, bool up = false, bool down = false, bool jp = false, bool jh = false) =>
            new MotorInput { x = x, up = up, down = down, jumpPressed = jp, jumpHeld = jh, speedMult = 1, jumpMult = 1 };

        public static bool LogicTests()
        {
            var cfg = Cfg;
            var st = cfg.player.motor;
            var log = new StringBuilder();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
                log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
            }
            float dt = GameConst.Dt;

            // full jump height
            {
                var g = Flat(20, 12);
                var s = MotorState.At(new Vector2(5.5f, 2f));
                float apex = 2f;
                for (int f = 0; f < 90; f++) { PlayerMotor.Step(ref s, In(jp: f == 0, jh: true), g, st, dt); apex = Mathf.Max(apex, s.pos.y); }
                Check("full jump height ~2.4 tiles", apex - 2f > 2.25f && apex - 2f < 2.6f, $"{apex - 2f:0.00}");
                var s2 = MotorState.At(new Vector2(5.5f, 2f));
                float apex2 = 2f;
                for (int f = 0; f < 90; f++) { PlayerMotor.Step(ref s2, In(jp: f == 0, jh: f < 3), g, st, dt); apex2 = Mathf.Max(apex2, s2.pos.y); }
                Check("tap jump is lower (variable height)", apex2 - 2f < 1.2f, $"{apex2 - 2f:0.00}");
            }
            // coyote time
            {
                var g = Flat(20, 12);
                for (int x = 8; x < 19; x++) g.SetRaw(x, 1, TileType.Empty);
                var s = MotorState.At(new Vector2(6.5f, 2f));
                bool jumped = false; int airFrames = 0;
                for (int f = 0; f < 60 && !jumped; f++)
                {
                    bool press = airFrames == 3;
                    PlayerMotor.Step(ref s, In(x: 1, jp: press, jh: press), g, st, dt);
                    if (!s.grounded) airFrames++;
                    if (s.jumpedThisStep) jumped = true;
                }
                Check("coyote time: jump 3 frames after leaving the ledge", jumped);
            }
            // jump buffer
            {
                var g = Flat(20, 12);
                var s = MotorState.At(new Vector2(5.5f, 4f));
                s.grounded = false;
                bool jumpedOnLanding = false; int f0 = -1;
                for (int f = 0; f < 120; f++)
                {
                    float distToFloor = s.pos.y - 2f;
                    bool press = f0 < 0 && distToFloor < 0.5f && s.vel.y < 0f;
                    if (press) f0 = f;
                    PlayerMotor.Step(ref s, In(jp: press, jh: true), g, st, dt);
                    if (s.jumpedThisStep && f0 >= 0) { jumpedOnLanding = true; break; }
                }
                Check("jump buffer: early press jumps on landing", jumpedOnLanding);
            }
            // two-tile step reachable, three-tile needs ledge grab
            {
                var g = Flat(20, 12);
                for (int x = 8; x < 18; x++) { g.SetRaw(x, 2, TileType.Dirt); g.SetRaw(x, 3, TileType.Dirt); }
                var s = MotorState.At(new Vector2(6.5f, 2f));
                for (int f = 0; f < 120; f++) PlayerMotor.Step(ref s, In(x: f < 30 ? 1 : 0, jp: f == 0, jh: true), g, st, dt);
                Check("jump onto a 2-high ledge", s.pos.y >= 3.99f && s.pos.x > 8f, $"pos {s.pos}");
                var g3 = Flat(20, 12);
                for (int y = 2; y <= 4; y++) g3.SetRaw(8, y, TileType.Dirt);
                var s3 = MotorState.At(new Vector2(7.6f, 2f));
                bool hung = false;
                for (int f = 0; f < 120; f++) { PlayerMotor.Step(ref s3, In(x: 1, jp: f == 0, jh: true), g3, st, dt); if (s3.mode == MotorMode.Hang) { hung = true; break; } }
                Check("ledge grab on a 3-high wall", hung, $"pos {s3.pos}");
                for (int f = 0; f < 40; f++) PlayerMotor.Step(ref s3, In(up: true), g3, st, dt);
                Check("climb up from ledge hang", s3.mode == MotorMode.Normal && s3.grounded && s3.pos.y >= 4.99f, $"pos {s3.pos} mode {s3.mode}");
            }
            // ladder
            {
                var g = Flat(20, 14);
                for (int y = 2; y <= 7; y++) { g.SetRaw(9, y, TileType.Dirt); g.SetRaw(8, y, TileType.Ladder); }
                var s = MotorState.At(new Vector2(8.5f, 2f));
                for (int f = 0; f < 200; f++) PlayerMotor.Step(ref s, In(up: true), g, st, dt);
                Check("ladder climb to the top", s.mode == MotorMode.Normal && s.grounded && Mathf.Abs(s.pos.y - 8f) < 0.01f, $"pos {s.pos} mode {s.mode}");
                for (int f = 0; f < 200; f++) PlayerMotor.Step(ref s, In(down: true), g, st, dt);
                Check("ladder climb down to the floor", s.grounded && Mathf.Abs(s.pos.y - 2f) < 0.01f, $"pos {s.pos} mode {s.mode}");
            }
            // regression: jumping off a ladder that touches the ceiling must not push the player into the ceiling
            {
                var g = Flat(20, 14);
                for (int y = 2; y <= 6; y++) g.SetRaw(8, y, TileType.Ladder);
                for (int x = 5; x <= 11; x++) { g.SetRaw(x, 7, TileType.Dirt); g.SetRaw(x, 8, TileType.Dirt); }
                var s = MotorState.At(new Vector2(8.5f, 2f));
                bool inside = false; float maxHead = 0f;
                for (int f = 0; f < 120; f++) { PlayerMotor.Step(ref s, In(up: true), g, st, dt); maxHead = Mathf.Max(maxHead, s.pos.y + st.height); }
                for (int jump = 0; jump < 5; jump++)
                {
                    for (int f = 0; f < 70; f++)
                    {
                        PlayerMotor.Step(ref s, In(up: f > 40, jp: f == 0, jh: f < 20), g, st, dt);
                        if (BodyPhysics.OverlapsSolid(g, s.pos, st.width * 0.5f, st.height)) inside = true;
                        maxHead = Mathf.Max(maxHead, s.pos.y + st.height);
                    }
                }
                Check("jumping off a ladder under a ceiling never enters the ceiling", !inside && maxHead <= 7.001f && s.pos.y < 7f, $"maxHead {maxHead:0.00}, pos {s.pos}");
            }
            // regression: jumping up into a one-way platform from below must not pop the player on top of it
            {
                var g = Flat(20, 14);
                for (int x = 1; x < 19; x++) g.SetRaw(x, 2, TileType.Dirt);
                for (int x = 6; x <= 11; x++) g.SetRaw(x, 5, TileType.Platform);
                var s = MotorState.At(new Vector2(8.5f, 3f));
                for (int f = 0; f < 90; f++) PlayerMotor.Step(ref s, In(jp: f == 0, jh: true), g, st, dt);
                Check("a jump that does not clear a platform falls back down", s.grounded && Mathf.Abs(s.pos.y - 3f) < 0.01f, $"pos {s.pos}");
            }
            // crawl off edge -> hang
            {
                var g = Flat(20, 12);
                for (int x = 9; x < 19; x++) g.SetRaw(x, 1, TileType.Empty);
                var s = MotorState.At(new Vector2(7.5f, 2f));
                bool hung = false;
                for (int f = 0; f < 120; f++) { PlayerMotor.Step(ref s, In(x: 1, down: true), g, st, dt); if (s.mode == MotorMode.Hang) { hung = true; break; } }
                Check("crawling off an edge hangs from it", hung, $"pos {s.pos}");
            }
            // spikes
            {
                var g = Flat(20, 12);
                g.SetRaw(10, 2, TileType.Spikes);
                var s = MotorState.At(new Vector2(10.5f, 5f)); s.grounded = false;
                bool dead = false;
                for (int f = 0; f < 60; f++) { PlayerMotor.Step(ref s, In(), g, st, dt); if (s.hitSpikes) { dead = true; break; } }
                Check("falling onto spikes is lethal", dead);
                var s2 = MotorState.At(new Vector2(8.5f, 2f));
                bool dead2 = false;
                for (int f = 0; f < 40; f++) { PlayerMotor.Step(ref s2, In(x: 1), g, st, dt); if (s2.hitSpikes) dead2 = true; }
                Check("walking into spikes is safe", !dead2);
            }
            // fall height
            {
                var g = Flat(20, 20);
                var s = MotorState.At(new Vector2(10.5f, 13f)); s.grounded = false; s.apexY = 13f;
                for (int f = 0; f < 200 && !s.landed; f++) PlayerMotor.Step(ref s, In(), g, st, dt);
                Check("long fall reports fall distance >= damage height", s.fallDistance >= cfg.player.fallDamageHeight, $"{s.fallDistance:0.0}");
            }
            // one-way platform drop
            {
                var g = Flat(20, 12);
                g.SetRaw(8, 5, TileType.Platform);
                var s = MotorState.At(new Vector2(8.5f, 6f));
                PlayerMotor.Step(ref s, In(down: true, jp: true, jh: true), g, st, dt);
                for (int f = 0; f < 60; f++) PlayerMotor.Step(ref s, In(), g, st, dt);
                Check("down+jump drops through a platform", s.pos.y < 3f, $"pos {s.pos}");
            }
            // validator sanity
            {
                var g = Flat(30, 14);
                var d = new LevelData { grid = g, entrance = new Vector2Int(3, 2), exit = new Vector2Int(25, 2), name = "flat" };
                var r = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
                Check("validator: flat corridor reachable", r.ok);
                for (int y = 2; y < 13; y++) g.SetRaw(15, y, TileType.Bedrock);
                var r2 = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
                Check("validator: wall blocks the exit", !r2.ok);
                for (int y = 2; y < 13; y++) g.SetRaw(15, y, TileType.Empty);
                for (int x = 10; x < 15; x++) g.SetRaw(x, 1, TileType.Empty);
                for (int x = 10; x < 15; x++) g.SetRaw(x, 0, TileType.Empty);
                var r3 = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
                Check("validator: 5-wide pit is not jumpable", !r3.ok);
                for (int x = 10; x < 15; x++) { g.SetRaw(x, 0, TileType.Bedrock); g.SetRaw(x, 1, TileType.Dirt); }
                for (int y = 2; y <= 5; y++) g.SetRaw(15, y, TileType.Dirt);
                var r4 = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
                Check("validator: 4-high wall is not climbable", !r4.ok);
                g.SetRaw(15, 5, TileType.Empty);
                var r5 = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
                Check("validator: 3-high wall is climbable with a ledge grab", r5.ok);
            }
            // run state reset
            {
                var r1 = RunSession.NewRun(123, false, cfg.player);
                int c1 = RunSession.RunCounter;
                r1.gold = 999; r1.bombs = 0; r1.AddUpgrade(cfg.shop.Find("springboots")); r1.AddResource("test_counter", 3); r1.stageIndex = 4; r1.totalTime = 100; r1.kills = 7;
                var r2 = RunSession.NewRun(456, false, cfg.player);
                bool fresh = r2 != r1 && r2.gold == cfg.player.startGold && r2.bombs == r2.BombCap && r2.upgrades.Count == 0 && r2.stats.IsZero && !r2.counters.ContainsKey("test_counter") && r2.Pebbles == r2.PebbleCap && r2.stageIndex == 0 &&
                             r2.totalTime == 0 && r2.kills == 0 && r2.health == cfg.player.startHealth && RunSession.RunCounter == c1 + 1;
                Check("new run does not keep the previous run's state", fresh);
                RunSession.Clear();
            }
            // save slots (in a scratch folder: the player's own files are not touched)
            {
                string scratch = Path.Combine(Path.GetTempPath(), "gloomdelve_slot_test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratch);
                try
                {
                    File.WriteAllText(Path.Combine(scratch, "records.json"), "{\"runs\":7,\"embers\":2}");
                    SaveSystem.DirOverride = scratch;
                    SaveSystem.Reload();
                    Check("save slots: a save from before slots existed becomes slot 1", SaveSystem.Slot == 0 && SaveSystem.SlotUsed(0) && SaveSystem.Records.runs == 7 && SaveSystem.Records.embers == 2);
                    Check("save slots: the other two start empty", !SaveSystem.SlotUsed(1) && !SaveSystem.SlotUsed(2) && SaveSystem.PeekSlot(1) == null);
                    SaveSystem.SelectSlot(1);
                    SaveSystem.Records.embers = 4; SaveSystem.Records.rescued.Add("smith"); SaveSystem.SaveRecords();
                    Check("save slots: choosing an empty slot starts a new game in it", SaveSystem.Slot == 1 && SaveSystem.SlotUsed(1) && SaveSystem.Records.runs == 0);
                    SaveSystem.SelectSlot(0);
                    Check("save slots: each slot keeps its own progress", SaveSystem.Records.embers == 2 && SaveSystem.Records.rescued.Count == 0 && SaveSystem.PeekSlot(1).embers == 4 && SaveSystem.PeekSlot(1).rescued.Count == 1);
                    SaveSystem.Reload();
                    Check("save slots: the game comes back to the slot played last", SaveSystem.Slot == 0);
                    SaveSystem.SelectSlot(1); SaveSystem.Reload();
                    Check("save slots: ... whichever it was", SaveSystem.Slot == 1 && SaveSystem.Records.embers == 4);
                    SaveSystem.EraseSlot(1);
                    Check("save slots: erasing empties a slot and leaves the others alone", !SaveSystem.SlotUsed(1) && SaveSystem.Records.embers == 0 && SaveSystem.PeekSlot(0).runs == 7);

                    // Challenge stands apart from every save: its times are one list for the whole game, and a
                    // fight neither takes anything from the save being played nor leaves anything in it
                    File.WriteAllText(Path.Combine(scratch, "slot1.json"), "{\"runs\":7,\"embers\":4,\"pouchCapacity\":10,\"bagBombs\":2,\"bagRopes\":1,\"bagPebbles\":1,\"keyItems\":[\"pouch\"],\"seenItems\":[\"bombs\"],\"bosses\":[{\"boss\":\"Broodmother\",\"bestTime\":50,\"noDamage\":false},{\"boss\":\"Yeti King\",\"bestTime\":90,\"noDamage\":true}]}");
                    File.WriteAllText(Path.Combine(scratch, "slot3.json"), "{\"bosses\":[{\"boss\":\"Broodmother\",\"bestTime\":40,\"noDamage\":true}]}");
                    SaveSystem.Reload();
                    SaveSystem.SelectSlot(0);
                    var bm = SaveSystem.BossRecordOf("Broodmother"); var yk = SaveSystem.BossRecordOf("Yeti King");
                    Check("challenge: the times the saves used to keep become one list, the best of each", File.Exists(Path.Combine(scratch, "challenge.json")) && bm != null && bm.bestTime == 40f && bm.noDamage && yk != null && yk.bestTime == 90f && yk.noDamage);
                    var home = RunSession.NewRun(5, false, cfg.player);
                    Check("challenge: (a run of the save has what the elder made of its bags)", home.BombCap == cfg.player.bombCapacity + 10 && Villagers.Embers == 4);
                    string slotBefore = File.ReadAllText(Path.Combine(scratch, "slot1.json"));
                    var fight = RunSession.NewBossPractice(0, cfg.player);
                    Check("challenge: a fight takes nothing from the save - bags of the first size, no pouch, no embers",
                        SaveSystem.Apart && fight.BombCap == cfg.player.bombCapacity && fight.RopeCap == cfg.player.ropeCapacity && fight.PebbleCap == cfg.player.pebbleCapacity
                        && fight.bombs == cfg.player.bombCapacity && !Villagers.HasKey(Villagers.KeyPouch) && Villagers.Embers == 0 && Villagers.PouchCapacity == Villagers.PouchStartCapacity);
                    Check("challenge: it is fought with the slingshot and the bombs alone - no rope, no Recall Charm", fight.gear.Count == 2 && fight.gear.Contains("slingshot") && fight.gear.Contains("bomb") && fight.ropes == 0 && home.gear.Contains("rope") && home.gear.Contains("recall"));
                    Codex.CountKill("Snake"); bool card = ItemLog.FirstTime("compass");
                    Check("challenge: the Field Guide is in the bag with every page open, and nothing picked up gets a card", Codex.Owned && Codex.AllKnown && !card);
                    SaveSystem.RecordBossWin("Broodmother", 30f, false, true, out bool best, out bool flawlessNow);
                    SaveSystem.SaveRecords();
                    Check("challenge: a win is put down in the Challenge list and nowhere else", best && !flawlessNow && SaveSystem.BossRecordOf("Broodmother").bestTime == 30f && File.ReadAllText(Path.Combine(scratch, "slot1.json")) == slotBefore);
                    RunSession.Clear();
                    Check("challenge: out of the fight the save is as it was", !SaveSystem.Apart && SaveSystem.Records.embers == 4 && SaveSystem.Records.killCounts.Count == 0 && SaveSystem.Records.seenItems.Count == 1 && Codex.Kills("Snake") == 0);
                    SaveSystem.SelectSlot(2); SaveSystem.Reload();
                    Check("challenge: the list is the same whichever save is played", SaveSystem.Slot == 2 && SaveSystem.BossRecordOf("Broodmother").bestTime == 30f && SaveSystem.BossRecordOf("Yeti King") != null);

                    // the story's things are put where they can be walked to (Story.Spots): over a spread of levels
                    {
                        int asked = 0, got = 0, bad = 0; double worst = 0;
                        int sideAsked = 0, sideGot = 0, sideProved = 0, sideTrusted = 0, sideOnPath = 0;
                        var areaStages = new List<int>();
                        for (int si = 0; si < cfg.stages.Length; si++) if (cfg.stages[si].kind == LevelKind.Area) areaStages.Add(si);
                        for (int seed = 1; seed <= 12; seed++)
                        {
                            int si = areaStages[(seed * 5) % areaStages.Count];
                            var lvl = LevelFactory.Create(cfg, cfg.stages[si], seed, si);
                            var t0 = DateTime.Now;
                            var spots = Story.Spots(lvl, cfg.player, 2);
                            worst = Math.Max(worst, (DateTime.Now - t0).TotalMilliseconds);
                            asked += 2; got += spots.Count;
                            // ... and what leads to the hammer stands off the main path, wherever the level has a side room
                            var aside = Story.Spots(lvl, cfg.player, 1, true, spots);
                            sideAsked++; sideGot += aside.Count; sideProved += Story.lastProved; sideTrusted += Story.lastTrusted; sideOnPath += Story.lastOnPath;
                            foreach (var c in aside) if (Story.lastOnPath == 0 && !Story.OffPath(lvl, c.x, c.y)) bad++;
                            foreach (var c in spots)
                                if (!lvl.grid.IsEmpty(c.x, c.y) || !lvl.grid.IsEmpty(c.x, c.y + 1) || !lvl.grid.IsSolid(c.x, c.y - 1) || (c.x == lvl.entrance.x && c.y == lvl.entrance.y)) bad++;
                            if (spots.Count == 2 && spots[0] == spots[1]) bad++;
                        }
                        Check("story: a pillar and a part of the hammer find a place to stand in every level", got == asked && bad == 0, $"{got}/{asked} places, {bad} bad, slowest search {worst:0} ms");
                        Check("story: what leads to the hammer stands in a side room, off the main path", sideGot == sideAsked && bad == 0,
                            $"{sideGot}/{sideAsked} levels: {sideProved} walked into, {sideTrusted} behind a wall (bomb or rope), {sideOnPath} on the path for want of a side room");
                    }

                    {
                        var oswin = Villagers.All[Villagers.IndexOf("mapper")];
                        var maps = Villagers.WaresOf(oswin, cfg.shop, 777);
                        var sheetOfMap = maps.Find(o => o.id == MapScreen.ItemId);
                        var letter = maps.Find(o => o.id == Villagers.KeyLetter);
                        Check("map: the Mapmaker sells a spyglass, the map and his lost letter (the compass is open from the start); a description is shown with a capital letter",
                            maps.Count == 3 && !maps.Exists(o => o.id == "compass") && maps.Exists(o => o.id == "spyglass" && o.isUpgrade && o.icon != null)
                            && letter != null && letter.icon != null && !letter.isUpgrade && Villagers.PaidInEmbers(letter) && Villagers.EmberPrice(letter, oswin) == 3
                            && sheetOfMap != null && sheetOfMap.icon != null && !sheetOfMap.isUpgrade && Villagers.EmberPrice(sheetOfMap, oswin) == 2
                            && UIUtil.Cap("points to the exit") == "Points to the exit" && UIUtil.Cap("+3 bombs") == "+3 bombs" && UIUtil.Cap("Bombs.") == "Bombs." && UIUtil.Cap("[jump] up") == "[jump] up" && UIUtil.Cap("") == "");
                        Check("money: a boss leaves one ember and five sparks; the map and the blueprints are paid in embers, paper in sparks",
                            Villagers.EmbersPerBoss == 1 && Villagers.SparksPerBoss == 5 && Villagers.PaidInEmbers(sheetOfMap) && Villagers.PaidInEmbers(cfg.shop.Find("spyglass")) && !Villagers.PaidInEmbers(cfg.shop.Find("compass"))
                            && !Villagers.PaidInEmbers(cfg.shop.Find(Story.Paper)) && cfg.prefabs.Get("spark") is Pickup sparkPrefab && sparkPrefab.resource == "spark");
                        var pip = Villagers.All[Villagers.IndexOf("peddler")];
                        var stall = Villagers.WaresOf(pip, cfg.shop, 777);
                        var sheet = stall.Find(o => o.id == Story.Paper);
                        Check("story: the Peddler sells paper for an ember, and the mountain's own shelves and chests never carry it",
                            sheet != null && sheet.icon != null && !sheet.isUpgrade && Villagers.EmberPrice(sheet, pip) == 1 && stall.Count == 7 && !pip.blueprints && stall.Exists(o => o.id == "bombs") && stall.Exists(o => o.id == "pebbles") && stall.Exists(o => o.id == "lampoil") && stall.Exists(o => o.id == FlareGear.OfferId) && stall.Exists(o => o.id == "vigor") && cfg.FindGear(Story.HammerGear) != null
                            && cfg.prefabs.Get(Story.PillarId) is RunePillar && cfg.prefabs.Get(Story.StrangerId) is Stranger && cfg.prefabs.Get(Story.GhostId) is Ghost
                            && cfg.quests != null && cfg.quests.Find(Story.QuestSigns) != null && System.Array.IndexOf(cfg.player.startingGear, Story.HammerGear) < 0);
                    }

                    {
                        // chests: a plain wooden loot chest for the mountain; the special ones (camp, Field Guide, gear by a gate, the hammer's head) are fine
                        var lootPf = cfg.prefabs.Get("lootchest") as Chest;
                        var fine = new[] { "chest", "codexchest", "gearchest" };
                        bool allFine = true;
                        foreach (var id in fine) { var ch = cfg.prefabs.Get(id) as Chest; var sr = ch != null ? ch.GetComponentInChildren<SpriteRenderer>() : null; if (ch == null || ch.loot || sr == null || sr.sprite == null || !sr.sprite.name.StartsWith("chestfine")) allFine = false; }
                        var lsr = lootPf != null ? lootPf.GetComponentInChildren<SpriteRenderer>() : null;
                        Check("chests: the mountain's loot chest is plain wood; the camp's, the Field Guide's and the special gear's are fine ones",
                            lootPf != null && lootPf.loot && lootPf.lootRolls > 0 && lsr != null && lsr.sprite != null && lsr.sprite.name == "chest_0" && allFine
                            && cfg.lootChestsPerLevel >= 1 && cfg.lootChestChance > 0f);
                    }

                    {
                        // what is open from the start and what wants a blueprint: of each area's pair the lesser piece is open
                        string[] open = { "compass", "ironcap", "softsoles", "pebblepouch", "warmcoat", "sporemask", "heavyboots", "wardscarab", "truthlens" };
                        string[] plans = { "ironwhip", "blastpowder", "springboots", "hobnailboots", "spyglass", "iceboots", "lantern", "magnetcharm", "sandstriders", "dampener", "lampoil", FlareGear.OfferId, "vigor" };
                        bool ok = true; string why = "";
                        foreach (var id in open) if (Blueprints.VillageWares.Contains(id)) { ok = false; why += id + " has a stall; "; }
                        foreach (var id in plans)
                        {
                            var o = cfg.shop.Find(id);
                            if (!Blueprints.VillageWares.Contains(id)) { ok = false; why += id + " has no stall; "; }
                            if (o == null || o.blueprintPrice <= 0) { ok = false; why += id + " has no price; "; }
                        }
                        Check("blueprints: the lesser piece of each area is open, the stronger has a stall; every blueprint has its price", ok, why);
                    }
                    Check("village: Pip's Oddments (every consumable for a climb) is the door next to the player's own house",
                        HandcraftedLevel.StreetOrder.Length == 6 && HandcraftedLevel.StreetOrder[5] == Villagers.IndexOf("peddler"));
                    {
                        // blueprints: the mountain's shelves carry a villager's ware only once its blueprint is bought;
                        // their two consumables are drawn from bombs, ropes and the known consumable blueprints
                        Villagers.testRescued = new HashSet<string>();
                        Villagers.ResetTestState();
                        Blueprints.Refresh();
                        bool OnlyAllowed(out string bad)
                        {
                            bad = null;
                            for (int sd = 0; sd < 300; sd++)
                            {
                                var shelf = LevelGenerator.BuildShopOffers(cfg, new Rng(sd), null);
                                foreach (var id in shelf) if (Blueprints.Locked(id)) { bad = id; return false; }
                            }
                            return true;
                        }
                        var seen = new HashSet<string>();
                        for (int sd = 0; sd < 300; sd++) { var shelf = LevelGenerator.BuildShopOffers(cfg, new Rng(sd), null); seen.Add(shelf[0]); seen.Add(shelf[1]); }
                        bool plainOk = OnlyAllowed(out string bad0);
                        bool plainTwo = seen.SetEquals(new[] { "bombs", "ropes", "pebbles" }) && cfg.shop.Find("pebbles") != null;
                        Blueprints.Learn("lampoil"); Blueprints.Learn("ironwhip");
                        var seen2 = new HashSet<string>(); bool blade = false;
                        for (int sd = 0; sd < 300; sd++)
                        {
                            var shelf = LevelGenerator.BuildShopOffers(cfg, new Rng(sd), null);
                            seen2.Add(shelf[0]); seen2.Add(shelf[1]);
                            if (shelf.Contains("ironwhip")) blade = true;
                        }
                        bool learnedOk = OnlyAllowed(out string bad1);
                        Check("blueprints: with none known a shelf draws its two consumables from bombs, ropes and pebbles, and has no villager's equipment; known ones join the draw",
                            plainOk && plainTwo && learnedOk && seen2.Contains("lampoil") && !seen2.Contains(FlareGear.OfferId) && blade && Blueprints.VillageWares.Contains("ironwhip")
                            && !Blueprints.VillageWares.Contains(MapScreen.ItemId) && !Blueprints.VillageWares.Contains(Story.Paper),
                            $"consumables seen {string.Join(",", seen)} then {string.Join(",", seen2)}; locked on a shelf: {bad0 ?? "-"} / {bad1 ?? "-"}; blade {blade}");
                        Villagers.ResetTestState();
                        Villagers.testRescued = null;
                        Blueprints.Refresh();
                    }

                    // waking in bed after a night that ended: a line for it, and a scene for a save's first death
                    Check("the loop: a waking after a win has its line, after a death one of several in turn, and any other waking none",
                        EndingScene.WakingWords(EndingScene.Night.Won, 3) == EndingScene.WonWakingLine && EndingScene.WakingWords(EndingScene.Night.None, 3) == ""
                        && EndingScene.WakingWords(EndingScene.Night.Died, 1) != EndingScene.WakingWords(EndingScene.Night.Died, 2)
                        && EndingScene.WakingWords(EndingScene.Night.Died, 1) == EndingScene.WakingWords(EndingScene.Night.Died, 1 + EndingScene.DeathWakingLines.Length));
                    bool dueNew = EndingScene.FirstDeathDue(EndingScene.Night.Died), notAfterWin = !EndingScene.FirstDeathDue(EndingScene.Night.Won);
                    SaveSystem.Records.loopSeen = true;
                    Check("the loop: the first death of a save gets its scene - once, and never a win", dueNew && notAfterWin && !EndingScene.FirstDeathDue(EndingScene.Night.Died));
                    SaveSystem.Records.loopSeen = false;

                    // the Records screen: everything since the game was first played, all saves together
                    File.Delete(Path.Combine(scratch, "totals.json"));
                    File.WriteAllText(Path.Combine(scratch, "slot1.json"), "{\"runs\":7,\"wins\":0,\"deaths\":6,\"bestStage\":3,\"bestStageName\":\"Mossroot 4\",\"bestGold\":200,\"fastestWin\":-1,\"totalKills\":40}");
                    File.WriteAllText(Path.Combine(scratch, "slot3.json"), "{\"runs\":3,\"wins\":1,\"deaths\":2,\"bestStage\":9,\"bestStageName\":\"The Sunken Vault\",\"bestGold\":500,\"fastestWin\":100,\"totalKills\":15}");
                    SaveSystem.Reload();
                    var tot = SaveSystem.Totals;
                    Check("records: what the saves used to keep is added up - counts summed, the fastest of the wins",
                        tot.runs == 10 && tot.wins == 1 && tot.deaths == 8 && tot.kills == 55 && tot.bossKills == 0 && tot.fastestWin == 100f);
                    SaveSystem.SelectSlot(0);
                    var night = RunSession.NewRun(5, false, cfg.player);
                    night.kills = 5; night.bossKills = 2; night.victory = true; night.totalTime = 80f;
                    string said = SaveSystem.RecordRunEnd(night);
                    tot = SaveSystem.Totals;
                    Check("records: a night that ends is counted once, for the game - creatures and bosses apart, and no save keeps a count of its own",
                        tot.runs == 11 && tot.wins == 2 && tot.deaths == 8 && tot.kills == 58 && tot.bossKills == 2 && tot.fastestWin == 80f && said == "NEW FASTEST WIN!" && SaveSystem.Records.runs == 7);
                    var bout = RunSession.NewBossPractice(0, cfg.player);
                    bout.kills = 1; bout.bossKills = 1; bout.victory = true; bout.totalTime = 5f;
                    SaveSystem.RecordRunEnd(bout);
                    tot = SaveSystem.Totals;
                    Check("records: nothing of a Challenge fight is counted - not the fight, not the boss, not its time", tot.runs == 11 && tot.wins == 2 && tot.bossKills == 2 && tot.kills == 58 && tot.fastestWin == 80f);
                    RunSession.Clear();
                    SaveSystem.SelectSlot(2); SaveSystem.Reload();
                    Check("records: they are the same whichever save is played, and after a restart", SaveSystem.Slot == 2 && SaveSystem.Totals.runs == 11 && SaveSystem.Totals.bossKills == 2 && SaveSystem.Totals.kills == 58);
                    File.WriteAllText(Path.Combine(scratch, "totals.json"), "{\"runs\":30,\"wins\":1,\"deaths\":20,\"fastestWin\":1158.7,\"totalKills\":752}");
                    SaveSystem.Reload();
                    Check("records: kills from before bosses were counted apart go with the creatures", SaveSystem.Totals.kills == 752 && SaveSystem.Totals.bossKills == 0 && SaveSystem.Totals.totalKills == 0 && SaveSystem.Totals.runs == 30);
                }
                finally
                {
                    RunSession.Clear();
                    SaveSystem.DirOverride = null;
                    SaveSystem.Reload();
                    try { Directory.Delete(scratch, true); } catch { }
                }
            }
            // a run in progress: saved, written out, read back, the same
            {
                var a = RunSession.NewRun(4242, false, cfg.player);
                RunPlan.Ensure(a, cfg);
                a.stageIndex = 5; a.inVillage = false; a.gold = 137; a.health = 3; a.maxHealth = 11; a.bombs = 6; a.ropes = 1; a.totalTime = 321.5f; a.kills = 9;
                a.extraTime = 120f; a.villagerFreed = true; a.bossDownStage = 3; a.shopkeeperWanted = true;
                a.AddUpgrade(cfg.shop.Find("springboots")); a.AddUpgrade(cfg.shop.Find("compass")); a.AddResource("test_counter", 4);
                a.Equip("rope");
                var json = JsonUtility.ToJson(RunSession.Capture(a, "2-2"));
                var b = RunSession.Restore(JsonUtility.FromJson<RunSave>(json), cfg);
                bool same = b != a && b.seed == a.seed && b.stageIndex == 5 && b.gold == 137 && b.health == 3 && b.maxHealth == 11 && b.bombs == 6 && b.ropes == 1
                    && Mathf.Approximately(b.totalTime, 321.5f) && b.kills == 9 && b.extraTime == 120f && b.villagerFreed && b.bossDownStage == 3 && b.shopkeeperWanted
                    && b.HasUpgrade("springboots") && b.HasUpgrade("compass") && b.IsWorn("springboots") == a.IsWorn("springboots")
                    && Mathf.Approximately(b.stats.jumpBonus, a.stats.jumpBonus) && b.GetResource("test_counter") == 4 && b.EquippedId == "rope"
                    && b.gear.SequenceEqual(a.gear) && b.stageOrder.SequenceEqual(a.stageOrder) && b.stageNames.SequenceEqual(a.stageNames);
                Check("saved run: everything carried comes back (seed, level, health, gold, gear, upgrades and what is worn)", same);
                var levelA = LevelFactory.Create(cfg, cfg.stages[a.stageOrder[5]].Named(a.stageNames[5]), a.seed, 5);
                var levelB = LevelFactory.Create(cfg, cfg.stages[b.stageOrder[5]].Named(b.stageNames[5]), b.seed, 5);
                bool sameLevel = levelA.Width == levelB.Width && levelA.Height == levelB.Height && levelA.entrance == levelB.entrance && levelA.exit == levelB.exit && levelA.spawns.Count == levelB.spawns.Count;
                for (int y = 0; sameLevel && y < levelA.Height; y++)
                    for (int x = 0; x < levelA.Width; x++)
                        if (levelA.grid.Get(x, y) != levelB.grid.Get(x, y)) { sameLevel = false; break; }
                Check("saved run: the level it was in is built again exactly as it was", sameLevel);
                RunSession.Clear();
            }
            // shop effects
            {
                var run = new RunState { bombs = 1, ropes = 1, health = 1, maxHealth = 10 };
                // (the two village-only wares are checked in the villagers scenario: they change health and the clock)
                foreach (var o in cfg.shop.offers) if (o.id != "vigor" && o.id != "lampoil") ShopItem.Apply(run, o);
                bool allOwned = cfg.shop.offers.Where(o => o.isUpgrade).All(o => run.HasUpgrade(o.id));
                Check("shop effects apply", run.bombs == 4 && run.ropes == 4 && run.health == 10 && allOwned &&
                      run.stats.jumpBonus > 0f && run.stats.whipDamageBonus > 0 && run.stats.whipRangeBonus > 0f && run.stats.blastRadiusBonus > 0f,
                      $"jump +{run.stats.jumpBonus} whip +{run.stats.whipDamageBonus}/+{run.stats.whipRangeBonus} blast +{run.stats.blastRadiusBonus}");
                {
                    // healing comes in three sizes: a sip, a flask, and the elixir that restores everything (never past the limit)
                    var hurt = new RunState { health = 1, maxHealth = 10 };
                    ShopItem.Apply(hurt, cfg.shop.Find("heart")); int a1 = hurt.health;
                    ShopItem.Apply(hurt, cfg.shop.Find("heart3")); int a2 = hurt.health;
                    ShopItem.Apply(hurt, cfg.shop.Find("heartfull")); int a3 = hurt.health;
                    int pt = cfg.shop.Find("heart").price, pf = cfg.shop.Find("heart3").price, pe = cfg.shop.Find("heartfull").price;
                    Check("healing: tonic +3, flask +5, elixir to full - 50 gold a heart (the elixir as ten)", a1 == 4 && a2 == 9 && a3 == 10 && pt == 3 * 50 && pf == 5 * 50 && pe == 10 * 50,
                          $"health 1 -> {a1} -> {a2} -> {a3}; prices {pt} / {pf} / {pe}");
                    var seenHeal = new HashSet<string>();
                    for (int r = 0; r < 100; r++) seenHeal.Add(cfg.shop.HealOffer(r));
                    Check("healing: shelves carry all three sizes over time", seenHeal.Count == 3, string.Join(",", seenHeal));
                }
                var twice = new RunState();
                var boots = cfg.shop.Find("springboots");
                twice.AddUpgrade(boots); twice.AddUpgrade(boots);
                Check("an upgrade applies its modifiers only once", twice.upgrades.Count == 1 && Mathf.Approximately(twice.stats.jumpBonus, boots.modifiers.jumpBonus));
                var custom = new RunState();
                custom.AddResource("arrows", 5); custom.AddResource("arrows", -2);
                Check("custom resources are counted", custom.GetResource("arrows") == 3 && custom.GetResource("nothing") == 0);
                Check("every equipment asset has a behaviour", cfg.equipment.All(e => GearRegistry.Get(e.id) != null),
                      string.Join(",", cfg.equipment.Select(e => e.id)));
            }
            // templates
            {
                var tl = RoomTemplates.Parse(cfg.roomTemplates.text);
                bool cover = new[] { 0, 1, 2, 3, 4 }.All(t => tl.Any(x => x.types.Contains(t) && !x.HasTag("start") && !x.HasTag("exit") && !x.HasTag("shop")));
                bool starts = new[] { 1, 2 }.All(t => tl.Any(x => x.HasTag("start") && x.types.Contains(t)));
                bool exits = new[] { 1, 3 }.All(t => tl.Any(x => x.HasTag("exit") && x.types.Contains(t)));
                bool special = tl.Any(x => x.HasTag("shop")) && tl.Any(x => x.HasTag("idol")) && tl.Any(x => x.HasTag("vault"));
                Check($"room templates cover every room type ({tl.Count} templates)", cover && starts && exits && special);
            }
            // room template file: save format + rule checks (Room Template Editor)
            {
                string src = cfg.roomTemplates.text.Replace("\r", "");
                var rooms = RoomTemplates.ParseFresh(src);
                string saved = RoomTemplates.Serialize(rooms);
                string diff = "";
                if (saved.TrimEnd() != src.TrimEnd())
                {
                    var a = saved.Split('\n'); var b = src.Split('\n');
                    for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
                        if (a[i] != b[i]) { diff = $"line {i + 1}: saved '{a[i]}' vs file '{b[i]}'"; break; }
                    if (diff == "") diff = $"line count {a.Length} vs {b.Length}";
                }
                Check("room template file survives parse -> save unchanged", diff == "", diff);
                int errs = 0, warns = 0; string firstErr = "";
                var lintReport = new StringBuilder("Room template rule checks (same checks as the Room Template Editor)");
                lintReport.AppendLine();
                foreach (var r in rooms)
                    foreach (var issue in RoomLint.Check(r, cfg.customMarkers))
                    {
                        lintReport.AppendLine($"{(issue.error ? "ERROR" : "warn ")}  {r.name}: {issue.message}");
                        if (!issue.error) { warns++; continue; }
                        errs++;
                        if (firstErr == "") firstErr = r.name + ": " + issue.message;
                    }
                Directory.CreateDirectory(ResultsDir);
                File.WriteAllText(Path.Combine(ResultsDir, "room_lint.txt"), lintReport.ToString());
                Check("room rule checks: shipped templates have no errors", errs == 0, $"{errs} errors, {warns} warnings  {firstErr}");
                var bad = RoomTemplate.Blank("bad"); bad.types.Clear(); bad.types.Add(2);
                Check("room rule checks catch a drop room without a hole", RoomLint.Check(bad).Any(x => x.error));
                var bad2 = RoomTemplate.Blank("bad2"); bad2.cells[3, 3] = '@';
                Check("room rule checks catch an unknown symbol", RoomLint.Check(bad2).Any(x => x.error));
            }
            // forced test template + designer-defined marker
            {
                var marker = new MarkerDef { symbol = "Z", label = "test rock", spawnId = "rock", chance = 1f, placement = MarkerPlacement.OnFloor };
                cfg.customMarkers.Add(marker);
                try
                {
                    var room = RoomTemplate.Blank("forced_test");
                    room.cells[4, 6] = 'Z';
                    int used = 0, spawned = 0, valid = 0;
                    for (int seed = 1; seed <= 6; seed++)
                    {
                        var req = new GenRequest
                        {
                            seed = seed, area = cfg.stages[0].area, levelNumber = 1, name = "forced", config = cfg,
                            templates = RoomTemplates.Parse(cfg.roomTemplates.text), forceTemplate = room
                        };
                        var d = LevelGenerator.Generate(req);
                        if (d.validation != null && d.validation.ok) valid++;
                        used += d.forcedRooms.Count;
                        foreach (var sp in d.spawns)
                            if (sp.id == "rock")
                                foreach (var rr in d.forcedRooms)
                                    if (rr.Contains(sp.cell) && sp.cell.y == rr.yMin + 1) spawned++;
                    }
                    Check("forced template is placed in generated levels", used > 0 && valid == 6, $"{used} rooms in 6 levels, {valid} valid");
                    Check("custom marker spawns its prefab", spawned > 0 && spawned <= used, $"{spawned} rocks in {used} forced rooms");
                }
                finally { cfg.customMarkers.Remove(marker); }
            }
            // determinism
            {
                var st0 = cfg.stages[0];
                var a = LevelFactory.Create(cfg, st0, 777, 0);
                var b = LevelFactory.Create(cfg, st0, 777, 0);
                bool same = a.entrance == b.entrance && a.exit == b.exit && a.spawns.Count == b.spawns.Count && Ascii(a, "") == Ascii(b, "");
                Check("same seed -> identical level", same);
            }

            log.AppendLine($"\n{pass} passed, {fail} failed");
            Directory.CreateDirectory(ResultsDir);
            File.WriteAllText(Path.Combine(ResultsDir, "logic_tests.txt"), log.ToString());
            Debug.Log("[Tests] Logic tests:\n" + log);
            return fail == 0;
        }
    }
}
