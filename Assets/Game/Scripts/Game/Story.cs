using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// How the player finds out what the Sunstone is, and what breaks it. In order:
    ///  1. Stone pillars stand on the mountain (one in the first level of every area), carved with signs nobody can
    ///     read. Looking at one starts the task "The Old Signs".
    ///  2. The Peddler, once he is home, sells paper (embers). With a sheet in the bag a pillar can be copied: the
    ///     copy is a key item, kept for good.
    ///  3. Elder Aldous reads the copy: the stone holds the village in one day, no hand can break it, and the
    ///     three parts of a hammer lie on the mountain. From then on (kept with the save: KeyDecoded) every run
    ///     has the three parts in it:
    ///       - the haft, from a stranger who trades in nothing, in the second level of the run's SECOND area;
    ///       - the head, in a chest, in the second level of the THIRD area;
    ///       - the heart, from three spirits bound in the three levels of the FOURTH area, one in each: freed, all
    ///         three, they come together and hand it over.
    ///     The parts are the run's (lost with it, like everything carried). With all three the hammer is in the
    ///     bag: a piece of gear, used like the bombs - a blow that does three damage.
    ///  4. With the hammer in hand and the keeper of the Sunken Vault fallen, the stone can be broken instead of
    ///     carried home: that is the real end (EndingScene.PlayTrue), and the credits.
    /// Tests see none of it unless they ask (Story.forTests).
    /// </summary>
    public static class Story
    {
        public const string KeyRubbing = "rubbing", KeyDecoded = "decoded", KeyTrueEnd = "true_end";
        public const string Paper = "paper";
        public const string QuestSigns = "old_signs";
        public const string HammerGear = "hammer";
        public const string PillarId = "runepillar", StrangerId = "stranger", GhostId = "ghost", HeadChest = "hammerhead";
        public const string HammerName = "Stonebreaker";

        /// <summary>Tests: place the story's things although a test is running.</summary>
        public static bool forTests;

        public static bool HasRubbing => Villagers.HasKey(KeyRubbing);
        public static bool Decoded => Villagers.HasKey(KeyDecoded);

        /// <summary>What the signs say, once somebody has read them (the elder's words; a pillar looked at afterwards says the same).</summary>
        public const string Reading =
            "WHAT YOU TOOK FOR A SUN IS A STOPPED DAY. WHILE THE STONE IS WHOLE, NO MORNING COMES. NO HAND BREAKS IT. " +
            "THE HAMMER LIES IN THREE: THE HAFT WITH ONE WHO SELLS NOTHING, THE HEAD UNDER A LID, THE HEART WITH THE DEAD WHO CANNOT LEAVE.";

        // ------------------------------------------------------------------ where in a run

        /// <summary>Which area of the run a stage belongs to (0 = the first) and which of its levels it is (0-2; 3 = its boss).</summary>
        public static int AreaOf(int stageIndex) => stageIndex / 4;
        public static int LevelOf(int stageIndex) => stageIndex % 4;

        public static bool PillarIn(int stageIndex) => LevelOf(stageIndex) == 0;
        public static bool StrangerIn(RunState run) => Decoded && run != null && !run.hammerHandle && AreaOf(run.stageIndex) == 1 && LevelOf(run.stageIndex) == 1;
        public static bool HeadIn(RunState run) => Decoded && run != null && !run.hammerHead && AreaOf(run.stageIndex) == 2 && LevelOf(run.stageIndex) == 1;
        public static bool GhostIn(RunState run) => Decoded && run != null && !run.hammerCore && AreaOf(run.stageIndex) == 3 && LevelOf(run.stageIndex) < 3
                                                    && (run.ghostMask & (1 << LevelOf(run.stageIndex))) == 0;

        /// <summary>
        /// Puts what the story has in this level into it (GameDirector, once the level is built): each thing at a
        /// place on the floor that can be walked to from the entrance - proved, like the way to the exit.
        /// </summary>
        public static void Populate(World w, LevelData level, StageDef stage, RunState run, GameConfig cfg)
        {
            if (w == null || level == null || stage == null || run == null || cfg == null) return;
            if (stage.kind != LevelKind.Area || run.isTutorial || run.bossPractice || run.inVillage) return;
            if ((ScenarioRunner.Active || AutoTest.Active) && !forTests) return;
            if (!Villagers.Enabled) return;
            var want = new List<string>();
            if (PillarIn(run.stageIndex)) want.Add(PillarId);
            if (StrangerIn(run)) want.Add(StrangerId);
            if (HeadIn(run)) want.Add(HeadChest);
            if (GhostIn(run)) want.Add(GhostId);
            if (want.Count == 0) return;
            // what leads to the hammer is off the beaten path, in the side rooms of the level: it has to be looked
            // for. (A pillar may stand anywhere: it is there to be come across.)
            var parts = want.FindAll(id => id != PillarId);
            var spots = Spots(level, cfg.player, parts.Count, true, null);
            if (parts.Count < want.Count) spots.AddRange(Spots(level, cfg.player, 1, false, spots));
            want = parts;
            if (PillarIn(run.stageIndex)) want.Add(PillarId);
            for (int i = 0; i < want.Count && i < spots.Count; i++)
            {
                var cell = spots[i];
                var at = new Vector2(cell.x + 0.5f, cell.y);
                if (want[i] == HeadChest)
                {
                    var chest = cfg.prefabs.Get("gearchest");
                    if (chest != null) w.Spawn(chest, at, new SpawnPoint("gearchest", cell, 0, 0, HeadChest));
                }
                else w.SpawnById(want[i], at);
                Debug.Log($"[Story] {want[i]} at {cell.x},{cell.y} (entrance {level.entrance.x},{level.entrance.y}, exit {level.exit.x},{level.exit.y})");
            }
            if (spots.Count < want.Count) Debug.LogWarning($"[Story] no place found for {want.Count - spots.Count} of {want.Count} things in {stage.displayName}");
        }

        /// <summary>The cell lies in a room off the level's main path (a side room: not rock, not on the way from entrance to exit).</summary>
        public static bool OffPath(LevelData d, int x, int y)
        {
            if (d == null || d.roomTypes == null || d.roomRects == null) return false;
            for (int i = 0; i < d.roomRects.GetLength(0); i++)
                for (int j = 0; j < d.roomRects.GetLength(1); j++)
                    if (d.roomRects[i, j].Contains(new Vector2Int(x, y)))
                        return d.roomTypes[i, j] == 0 && (d.roomSolid == null || !d.roomSolid[i, j]);
            return false;
        }

        /// <summary>How the last call of Spots found its places (tests): proved walkable, taken on trust in a side room, or - there being no side room - on the main path after all.</summary>
        public static int lastProved, lastTrusted, lastOnPath;

        /// <summary>
        /// Up to 'count' cells of floor, apart from each other (and from 'avoid'), that the player can stand on.
        /// Never in a shop, never on top of something else, well away from the entrance.
        /// side = false: anywhere, and proved - walked to from the entrance with nothing but legs (and what the
        /// level's own gates ask for).
        /// side = true: only in the side rooms, off the main path. Proved where a side room can be walked into; a
        /// side room that is walled off (it wants a bomb or a rope, like the cage of a villager) is taken on
        /// trust; and a level with no side room at all gets a proved place on the path instead.
        /// </summary>
        public static List<Vector2Int> Spots(LevelData d, PlayerConfig player, int count, bool side = false, List<Vector2Int> avoid = null)
        {
            var found = new List<Vector2Int>();
            lastProved = lastTrusted = lastOnPath = 0;
            if (d == null || d.grid == null || count <= 0) return found;
            var g = d.grid;
            bool Apart(Vector2Int c, int dx, int dy)
            {
                foreach (var f in found) if (Mathf.Abs(f.x - c.x) < dx && Mathf.Abs(f.y - c.y) < dy) return false;
                if (avoid != null) foreach (var f in avoid) if (Mathf.Abs(f.x - c.x) < dx && Mathf.Abs(f.y - c.y) < dy) return false;
                return true;
            }
            bool Floor(int x, int y) => g.IsEmpty(x, y) && g.IsEmpty(x, y + 1) && g.IsSolid(x, y - 1);
            bool Taken(int x, int y)
            {
                foreach (var sp in d.spawns) if (Mathf.Abs(sp.cell.x - x) <= 1 && sp.cell.y >= y - 1 && sp.cell.y <= y + 2) return true;
                foreach (var r in d.shopRects) if (x >= r.xMin - 1 && x <= r.xMax && y >= r.yMin - 1 && y <= r.yMax) return true;
                return false;
            }
            var cand = new List<Vector2Int>();
            for (int y = 1; y < g.Height - 2; y++)
                for (int x = 2; x < g.Width - 2; x++)
                {
                    if (!Floor(x, y) || !Floor(x - 1, y) || !Floor(x + 1, y) || !g.IsEmpty(x, y + 2)) continue;
                    if (Mathf.Abs(x - d.entrance.x) + Mathf.Abs(y - d.entrance.y) < 9) continue;
                    if (Mathf.Abs(x - d.exit.x) < 3 && Mathf.Abs(y - d.exit.y) < 2) continue;
                    if (Taken(x, y)) continue;
                    if (side && !OffPath(d, x, y)) continue;
                    cand.Add(new Vector2Int(x, y));
                }
            // a seeded shuffle: the same level puts its things in the same places every time it is entered
            var rng = new Rng(Rng.Hash(d.seed, 5501));
            for (int i = cand.Count - 1; i > 0; i--) { int k = rng.Range(0, i + 1); (cand[i], cand[k]) = (cand[k], cand[i]); }

            var o = LevelValidator.DefaultOptions(player);
            o.grip = d.grip;
            o = LevelValidator.WithGear(d, o);
            var grid = LevelValidator.GearGrid(d, o);
            var start = MotorState.At(new Vector2(d.entrance.x + 0.5f, d.entrance.y));
            // a handful at a time: the first of them the search walks onto is a place that can be reached
            int from = 0;
            while (found.Count < count && from < cand.Count && from < 60)
            {
                var batch = new List<Vector2Int>();
                for (; from < cand.Count && batch.Count < 8; from++)
                    if (Apart(cand[from], 4, 3)) batch.Add(cand[from]);
                if (batch.Count == 0) continue;
                Vector2Int hit = default; bool any = false;
                LevelValidator.FindPath(grid, start, o, s =>
                {
                    foreach (var c in batch) if (LevelValidator.IsAtCell(s, c)) { hit = c; any = true; return true; }
                    return false;
                }, out _);
                if (any) { found.Add(hit); lastProved++; }
            }
            if (side)
            {
                // a side room nobody can simply walk into: still a side room (bombs and ropes open it)
                foreach (var c in cand)
                {
                    if (found.Count >= count) break;
                    if (Apart(c, 4, 3)) { found.Add(c); lastTrusted++; }
                }
                // no side room in this level at all: on the path then, but proved
                if (found.Count < count)
                {
                    var more = Spots(d, player, count - found.Count, false, avoid != null ? new List<Vector2Int>(avoid) { } : null);
                    int proved = lastProved, onPath = 0;
                    foreach (var c in more) if (Apart(c, 3, 2)) { found.Add(c); onPath++; }
                    lastProved = found.Count - onPath - lastTrusted; lastOnPath = onPath;
                }
                return found;
            }
            // nowhere proved (a level of narrow ledges): beside the way out, which is always reached
            for (int dx = 2; found.Count < count && dx <= 12; dx++)
                foreach (int sgn in new[] { -1, 1 })
                {
                    int x = d.exit.x + sgn * dx, y = d.exit.y;
                    if (found.Count >= count || !Floor(x, y) || Taken(x, y)) continue;
                    if (Apart(new Vector2Int(x, y), 3, 1)) found.Add(new Vector2Int(x, y));
                }
            return found;
        }

        // ------------------------------------------------------------------ the hammer's three parts

        public static Sprite Icon(GameConfig cfg, string what)
        {
            if (cfg == null || cfg.prefabs == null) return null;
            switch (what)
            {
                case KeyRubbing: return cfg.prefabs.Get(PillarId) is RunePillar rp ? rp.rubbingIcon : null;
                case "hammerhandle": return cfg.prefabs.Get(StrangerId) is Stranger st ? st.handleIcon : null;
                case "hammerhead": return cfg.prefabs.Get("gearchest") is Chest ch ? ch.hammerHeadIcon : null;
                case "hammercore": return cfg.prefabs.Get(GhostId) is Ghost gh ? gh.coreIcon : null;
                default: return null;
            }
        }

        /// <summary>One of the three parts has come into the bag: a card for it - and, with the third, the hammer itself.</summary>
        public static void GotPart(RunState run, GameConfig cfg, string part)
        {
            if (run == null) return;
            string name = part == "hammerhandle" ? "Hammer Haft" : part == "hammerhead" ? "Hammer Head" : "Heart of the Hammer";
            string about = part == "hammerhandle" ? "A haft of black wood, warm to the hand. One of three parts of the hammer the old signs speak of."
                         : part == "hammerhead" ? "A head of grey iron, heavier than it looks. One of three parts of the hammer the old signs speak of."
                         : "A light with no flame, given by the dead. One of three parts of the hammer the old signs speak of.";
            if (part == "hammerhandle") run.hammerHandle = true; else if (part == "hammerhead") run.hammerHead = true; else run.hammerCore = true;
            int have = (run.hammerHandle ? 1 : 0) + (run.hammerHead ? 1 : 0) + (run.hammerCore ? 1 : 0);
            var dir = GameDirector.Instance;
            var hud = dir != null ? dir.hud : null;
            if (hud != null) hud.Gain(part, name, Icon(cfg, part), 1, $"{about}\n\n{have} of 3.", true);
            Sfx.PlayUI("idol");
            QuestLog.Notify(QuestTrigger.Obtain, part);
            if (have < 3 || run.gear.Contains(HammerGear)) return;
            run.gear.Add(HammerGear);
            var gear = cfg != null ? cfg.FindGear(HammerGear) : null;
            if (hud != null && gear != null)
                hud.Gain(HammerGear, gear.displayName, gear.icon, 1,
                    $"Haft, head and heart: the hammer is whole. Hold [gear] to take it in hand, [use] to strike.  {gear.description}", true);
        }
    }
}
