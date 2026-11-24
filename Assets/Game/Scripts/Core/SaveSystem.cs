using System;
using System.IO;
using UnityEngine;

namespace Gloomdelve
{
    [Serializable]
    public class GameSettings
    {
        public float masterVolume = 0.8f;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public bool fullscreen = false;
        [Tooltip("Window size: an index into GameServices.WindowSizes (1280x720 .. 3840x2160). -1 = the largest that fits the screen.")] public int windowSize = -1;
        public bool screenShake = true;
        [Tooltip("The save slot played last (0-2): the one the menu shows records for.")] public int lastSlot = 0;
        [Tooltip("Keyboard keys the player changed (Settings > Keys). Empty = all default. See KeyBindings.")]
        public System.Collections.Generic.List<KeyBind> keys = new System.Collections.Generic.List<KeyBind>();
    }

    /// <summary>One changed keyboard key: action id (KeyBindings.Slots) and the Input System key name ("z", "leftShift").</summary>
    [Serializable]
    public class KeyBind
    {
        public string id;
        public string key;
    }

    /// <summary>
    /// The Records screen: everything since the game was first played, whichever save it was played in
    /// (totals.json) - the only count there is: no save keeps one of its own. Nights on the mountain only:
    /// nothing that happens in a Challenge fight is counted here.
    /// </summary>
    [Serializable]
    public class TotalRecords
    {
        public int runs;
        public int wins;
        public int deaths;
        public float fastestWin = -1f;
        [Tooltip("Creatures killed that were not bosses, and bosses brought down.")] public int kills;
        public int bossKills;
        [Tooltip("Not written any more: kills from before bosses were counted apart. Read once and put with the creatures (which of them were bosses was never written down).")]
        public int totalKills;
    }

    [Serializable]
    public class GameRecords
    {
        [Tooltip("Not written any more: runs, wins, deaths, kills and bests were kept with each save before the Records screen got a file of its own (TotalRecords). Read once, to carry them over.")]
        public int runs;
        public int wins;
        public int deaths;
        public int bestStage = -1;
        public string bestStageName = "-";
        public int bestGold;
        public float fastestWin = -1f;
        public int totalKills;
        [Tooltip("When this save was last played (shown on the save slot).")] public string lastPlayed = "";
        [Tooltip("The run in progress, if there is one.")] public RunSave run = new RunSave();
        [Tooltip("Not written any more: Challenge times were kept with each save before Challenge got a file of its own (ChallengeRecords). Read once, to carry them over - and an old save that has some is past the tutorial.")]
        public System.Collections.Generic.List<BossRecord> bosses = new System.Collections.Generic.List<BossRecord>();
        [Tooltip("Villagers of Brindlemoor freed on the mountain (ids from Villagers.All). They stay freed.")]
        public System.Collections.Generic.List<string> rescued = new System.Collections.Generic.List<string>();
        [Tooltip("Key items: the few things that are not lost when a run ends (\"pouch\" = the elder's Ember Pouch).")]
        public System.Collections.Generic.List<string> keyItems = new System.Collections.Generic.List<string>();
        [Tooltip("The tutorial has been finished or skipped in this save: a new game starts with it, later ones go straight to the village.")]
        public bool tutorialDone;
        [Tooltip("The scene of the first death has been seen in this save (EndingScene): Brann wakes in bed, on the same day, and knows it.")]
        public bool loopSeen;
        [Tooltip("Quests begun and done (see QuestLog).")]
        public System.Collections.Generic.List<QuestState> quests = new System.Collections.Generic.List<QuestState>();
        [Tooltip("Creatures killed, by kind (the Field Guide opens a creature's page with the first, and counts them).")]
        public System.Collections.Generic.List<KillCount> killCounts = new System.Collections.Generic.List<KillCount>();
        [Tooltip("Things picked up at least once in this save (see ItemLog): the first time gets a card, later times a line.")]
        public System.Collections.Generic.List<string> seenItems = new System.Collections.Generic.List<string>();
        [Tooltip("Embers in the Ember Pouch (the money of the village: only bosses drop it).")] public int embers;
        [Tooltip("Sparks in the pouch (the village's other money: every boss drops a handful; the villagers take them for what they sell for one climb).")] public int sparks;
        [Tooltip("How many embers the pouch holds (0 = what it held when the elder gave it).")] public int pouchCapacity;
        [Tooltip("How many times the elder has enlarged the bomb bag, the rope bag and the pebble bag (Bags: +5 each time, for good).")]
        public int bagBombs, bagRopes, bagPebbles;
    }

    /// <summary>
    /// A run in progress, as it was when the player entered the level it is in: seed, where it has got to, and
    /// everything carried (see RunSession.Capture). Written at the start of every level, so going on with a saved
    /// game starts that level again; cleared when the run ends.
    /// </summary>
    [Serializable]
    public class RunSave
    {
        public bool active;
        public int seed, stageIndex;
        public int[] stageOrder;
        public string[] stageNames;
        public bool inVillage;
        public float extraTime;
        public int damageTaken;
        public int health, maxHealth, bombs, ropes, gold;
        public float totalTime;
        public System.Collections.Generic.List<string> upgrades = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> worn = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> purchases = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> gear = new System.Collections.Generic.List<string>();
        public int equipped, previousEquipped = -1;
        public System.Collections.Generic.List<string> counterIds = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<int> counterValues = new System.Collections.Generic.List<int>();
        public bool shopkeeperWanted, shopkeeperDead;
        public int kills, bossKills, levelsCompleted;
        public bool villagerFreed;
        public int bossDownStage = -1;
        public bool hammerHandle, hammerHead, hammerCore;
        public int ghostMask;
        [Tooltip("Name of the level it is in (shown on the save slot).")] public string stageName = "";
    }

    /// <summary>Challenge record of one boss (by its display name).</summary>
    [Serializable]
    public class BossRecord
    {
        public string boss;
        [Tooltip("Fastest win in seconds; below 0 = never beaten.")] public float bestTime = -1f;
        [Tooltip("Beaten at least once without losing any health.")] public bool noDamage;
    }

    /// <summary>Challenge: best time against each boss, and whether it was ever beaten without losing health.
    /// One list for the whole game (challenge.json), whichever save is being played.</summary>
    [Serializable]
    public class ChallengeRecords
    {
        public System.Collections.Generic.List<BossRecord> bosses = new System.Collections.Generic.List<BossRecord>();
    }

    /// <summary>
    /// Saved as JSON in Application.persistentDataPath. Settings (volume, keys, ...) are shared. Everything the
    /// player has achieved on the mountain - villagers freed, embers, key items, records - lives in one of three
    /// save slots (slot1.json .. slot3.json); 'Records' is the slot being played.
    /// The Records screen is not a save's: it counts everything since the game was first played (totals.json).
    /// Challenge stands apart from all three: its times are in a file of their own (challenge.json), and while a
    /// Challenge fight is on (Apart) 'Records' is a blank save that is never written - so nothing a save has
    /// earned goes into the fight, and nothing that happens in the fight is put down in a save.
    /// </summary>
    public static class SaveSystem
    {
        public static GameSettings Settings { get; private set; } = new GameSettings();
        /// <summary>The save being played - or, in a Challenge fight, a blank one that belongs to that fight alone.</summary>
        public static GameRecords Records => Apart ? apartRecords : slotRecords;
        static GameRecords slotRecords = new GameRecords();
        static GameRecords apartRecords = new GameRecords();
        /// <summary>A Challenge fight is on: no save is read or written.</summary>
        public static bool Apart => RunSession.Current != null && RunSession.Current.bossPractice;
        /// <summary>A Challenge fight begins: it starts from nothing, whatever the one before it left.</summary>
        public static void BeginApart() { apartRecords = new GameRecords(); }
        /// <summary>The Challenge times (the same for every save).</summary>
        public static ChallengeRecords Challenge { get; private set; } = new ChallengeRecords();
        /// <summary>The Records screen: all saves together, since the game was first played.</summary>
        public static TotalRecords Totals { get; private set; } = new TotalRecords();
        static bool loaded;

        static string Dir => !string.IsNullOrEmpty(DirOverride) ? DirOverride : !string.IsNullOrEmpty(ArgDir) ? ArgDir : Application.persistentDataPath;

        /// <summary>"-saveDir path" on the command line: save there instead (picture runs and tests keep away from the player's files).</summary>
        static string ArgDir
        {
            get
            {
                if (argDir != null) return argDir;
                argDir = "";
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-saveDir") argDir = args[i + 1];
                return argDir;
            }
        }
        static string argDir;
        static string SettingsPath => Path.Combine(Dir, "settings.json");
        public const int SlotCount = 3;
        /// <summary>The slot being played (0-2).</summary>
        public static int Slot { get; private set; }
        /// <summary>Tests: another folder to save in, so the player's own files are never touched.</summary>
        public static string DirOverride;

        static string LegacyRecordsPath => Path.Combine(Dir, "records.json");
        static string SlotPath(int i) => Path.Combine(Dir, $"slot{i + 1}.json");
        static string RecordsPath => SlotPath(Slot);
        static string ChallengePath => Path.Combine(Dir, "challenge.json");
        static string TotalsPath => Path.Combine(Dir, "totals.json");

        public static bool SlotUsed(int i) => i >= 0 && i < SlotCount && File.Exists(SlotPath(i));

        /// <summary>What a slot holds, without switching to it (null = empty).</summary>
        public static GameRecords PeekSlot(int i)
        {
            EnsureLoaded();
            if (i < 0 || i >= SlotCount) return null;
            if (i == Slot && SlotUsed(i)) return slotRecords;
            return Load<GameRecords>(SlotPath(i));
        }

        /// <summary>Plays this slot from now on (an empty one becomes a new game and is written at once).</summary>
        public static void SelectSlot(int i)
        {
            EnsureLoaded();
            i = Mathf.Clamp(i, 0, SlotCount - 1);
            if (i != Slot || !SlotUsed(i))
            {
                Slot = i;
                slotRecords = Load<GameRecords>(SlotPath(i)) ?? new GameRecords();
            }
            slotRecords.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            SaveRecords();
            if (Settings.lastSlot != i) { Settings.lastSlot = i; SaveSettings(); }
        }

        /// <summary>Wipes a slot. If it is the one being played, play goes on with a blank one (not written until used).</summary>
        public static void EraseSlot(int i)
        {
            EnsureLoaded();
            if (i < 0 || i >= SlotCount) return;
            try { if (File.Exists(SlotPath(i))) File.Delete(SlotPath(i)); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not erase slot {i + 1}: {e.Message}"); }
            if (i == Slot) slotRecords = new GameRecords();
        }

        /// <summary>Automated runs and one-off modes never write a run in progress.</summary>
        static bool RunSaving(RunState run) => run != null && !run.isTutorial && !run.bossPractice && !ScenarioRunner.Active && !AutoTest.Active && !EditorPlaytest.Active;

        /// <summary>The run in progress of the slot being played (null = none).</summary>
        public static RunSave SavedRun => Records.run != null && Records.run.active ? Records.run : null;

        /// <summary>The start of a level: the run is saved as it is now. Going on with the save starts this level again.</summary>
        public static void Checkpoint(RunState run, string stageName)
        {
            if (!RunSaving(run)) return;
            EnsureLoaded();
            Records.run = RunSession.Capture(run, stageName);
            SaveRecords();
        }

        /// <summary>
        /// In the village the run is written again the moment it changes - a ware bought, a reward taken. What is
        /// paid there (embers) is saved at once, so what was paid for has to be too: a game left and taken up
        /// again would otherwise go back to the run as it was on arrival, the embers gone and the ware with them.
        /// (Not on the mountain: there the save is the start of the level, and stays so.)
        /// </summary>
        public static void CheckpointInVillage(RunState run)
        {
            if (run == null || !run.inVillage) return;
            var dir = GameDirector.Instance;
            Checkpoint(run, dir != null && dir.Stage != null ? dir.Stage.displayName : run.reachedStageName);
        }

        /// <summary>Things that must not happen twice if the level is entered again - the villager freed, the boss
        /// beaten - are written into the saved run the moment they happen.</summary>
        public static void CheckpointFlags(RunState run)
        {
            if (!RunSaving(run) || SavedRun == null) return;
            Records.run.villagerFreed = run.villagerFreed;
            Records.run.bossDownStage = run.bossDownStage;
            SaveRecords();
        }

        /// <summary>Forgets what is loaded (tests switch DirOverride and start over).</summary>
        public static void Reload() { loaded = false; EnsureLoaded(); }
        public static string RunLogPath => Path.Combine(Dir, "run_log.txt");

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            CarryOverOldSaves();
            Settings = Load<GameSettings>(SettingsPath) ?? new GameSettings();
            Slot = Mathf.Clamp(Settings.lastSlot, 0, SlotCount - 1);
            // a save from before there were slots becomes slot 1 (the old file is left where it is)
            try { if (File.Exists(LegacyRecordsPath) && !File.Exists(SlotPath(0))) File.Copy(LegacyRecordsPath, SlotPath(0)); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not move the old save into slot 1: {e.Message}"); }
            slotRecords = Load<GameRecords>(RecordsPath) ?? new GameRecords();
            Challenge = Load<ChallengeRecords>(ChallengePath) ?? ChallengeFromSlots();
            if (Challenge.bosses == null) Challenge.bosses = new System.Collections.Generic.List<BossRecord>();
            Totals = Load<TotalRecords>(TotalsPath) ?? TotalsFromSlots();
            if (Totals.totalKills > 0) { Totals.kills += Totals.totalKills; Totals.totalKills = 0; Write(TotalsPath, Totals); }      // (a file from before bosses were counted apart)
        }

        /// <summary>
        /// The records used to be kept with each save. The first time the game starts without a totals file, what
        /// the three saves hold is added up into it: counts summed, the fastest of the wins.
        /// </summary>
        static TotalRecords TotalsFromSlots()
        {
            var t = new TotalRecords();
            bool any = false;
            for (int i = 0; i < SlotCount; i++)
            {
                var r = i == Slot ? slotRecords : Load<GameRecords>(SlotPath(i));
                if (r == null) continue;
                any |= r.runs > 0 || r.totalKills > 0;
                t.runs += r.runs; t.wins += r.wins; t.deaths += r.deaths; t.kills += r.totalKills;
                if (r.fastestWin >= 0f && (t.fastestWin < 0f || r.fastestWin < t.fastestWin)) t.fastestWin = r.fastestWin;
            }
            if (any) Write(TotalsPath, t);
            return t;
        }

        /// <summary>
        /// Challenge times used to be kept with each save. The first time the game starts without a Challenge file,
        /// the best of what the three saves hold becomes that file (the saves are left as they are).
        /// </summary>
        static ChallengeRecords ChallengeFromSlots()
        {
            var all = new ChallengeRecords();
            for (int i = 0; i < SlotCount; i++)
            {
                var r = i == Slot ? slotRecords : Load<GameRecords>(SlotPath(i));
                if (r == null || r.bosses == null) continue;
                foreach (var b in r.bosses)
                {
                    if (b == null || string.IsNullOrEmpty(b.boss)) continue;
                    var rec = all.bosses.Find(x => x.boss == b.boss);
                    if (rec == null) { all.bosses.Add(new BossRecord { boss = b.boss, bestTime = b.bestTime, noDamage = b.noDamage }); continue; }
                    if (b.bestTime >= 0f && (rec.bestTime < 0f || b.bestTime < rec.bestTime)) rec.bestTime = b.bestTime;
                    rec.noDamage |= b.noDamage;
                }
            }
            if (all.bosses.Count > 0) Write(ChallengePath, all);
            return all;
        }

        /// <summary>
        /// The game used to be named "Gloomdelve" (company "Gloomdelve Team"), and Unity names the save folder after
        /// those: ...\LocalLow\Gloomdelve Team\Gloomdelve. The first time the renamed game starts with nothing saved
        /// yet, everything there is copied across (the old folder is left as it was, as a backup).
        /// </summary>
        static void CarryOverOldSaves()
        {
            if (!string.IsNullOrEmpty(DirOverride) || !string.IsNullOrEmpty(ArgDir)) return;
            try
            {
                string dir = Application.persistentDataPath;
                var company = Directory.GetParent(dir);
                if (company == null || company.Parent == null) return;
                string old = Path.Combine(company.Parent.FullName, "Gloomdelve Team", "Gloomdelve");
                if (!Directory.Exists(old) || string.Equals(Path.GetFullPath(old).TrimEnd('\\', '/'), Path.GetFullPath(dir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(Path.Combine(dir, "settings.json"))) return;
                for (int i = 0; i < SlotCount; i++) if (File.Exists(Path.Combine(dir, $"slot{i + 1}.json"))) return;
                Directory.CreateDirectory(dir);
                foreach (var f in Directory.GetFiles(old, "*.json"))
                    File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), false);
                foreach (var f in Directory.GetFiles(old, "*.txt"))
                {
                    string to = Path.Combine(dir, Path.GetFileName(f));
                    if (!File.Exists(to)) File.Copy(f, to, false);
                }
                Debug.Log($"[Save] Carried the saves over from {old}");
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not carry over the old saves: {e.Message}"); }
        }

        static T Load<T>(string path) where T : class
        {
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not read {path}: {e.Message}"); }
            return null;
        }

        static void Write(string path, object o)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(path, JsonUtility.ToJson(o, true));
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not write {path}: {e.Message}"); }
        }

        public static void SaveSettings() => Write(SettingsPath, Settings);
        /// <summary>Writes the save being played (nothing in a Challenge fight: it has no save).</summary>
        public static void SaveRecords() { if (!Apart) Write(RecordsPath, slotRecords); }

        /// <summary>
        /// This save is past the tutorial: it was finished or skipped - or the save is older than the tutorial's
        /// place at the start of a new game and already has something to show (a key item, a freed villager, a boss beaten).
        /// </summary>
        public static bool TutorialDone
        {
            get
            {
                var r = Records;
                return r.tutorialDone || (r.keyItems != null && r.keyItems.Count > 0) || (r.rescued != null && r.rescued.Count > 0) || (r.bosses != null && r.bosses.Count > 0);
            }
        }

        public static void MarkTutorialDone()
        {
            if (ScenarioRunner.Active || AutoTest.Active || Records.tutorialDone) return;
            Records.tutorialDone = true;
            SaveRecords();
        }

        /// <summary>Returns a short "new record" summary. givenUp: the player ended the run from the pause screen - a run, but not a death.</summary>
        public static string RecordRunEnd(RunState run, bool givenUp = false)
        {
            if (run == null || run.bossPractice) return "";      // a Challenge fight is not a night on the mountain: never counted
            EnsureLoaded();
            var r = Records;
            var t = Totals;
            string news = "";
            t.runs++;
            t.bossKills += run.bossKills;
            t.kills += Mathf.Max(0, run.kills - run.bossKills);
            if (run.victory)
            {
                t.wins++;
                if (t.fastestWin < 0f || run.totalTime < t.fastestWin) { t.fastestWin = run.totalTime; news += "NEW FASTEST WIN!"; }
            }
            else if (!givenUp) t.deaths++;
            Write(TotalsPath, t);
            r.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            r.run = new RunSave();          // the run is over: nothing to go on with
            SaveRecords();
            return news.Trim();
        }

        /// <summary>The Challenge record of a boss, or null when it was never beaten there.</summary>
        public static BossRecord BossRecordOf(string boss)
        {
            EnsureLoaded();
            if (Challenge.bosses == null || string.IsNullOrEmpty(boss)) return null;
            foreach (var b in Challenge.bosses) if (b != null && b.boss == boss && b.bestTime >= 0f) return b;
            return null;
        }

        /// <summary>
        /// A Challenge win: keeps the fastest time, and remembers a win without any health lost for good.
        /// newBest: this time beat the old one (or is the first). firstFlawless: the badge was earned just now.
        /// save = false only compares (tests must not write the player's records).
        /// </summary>
        public static void RecordBossWin(string boss, float time, bool noDamage, bool save, out bool newBest, out bool firstFlawless)
        {
            EnsureLoaded();
            if (Challenge.bosses == null) Challenge.bosses = new System.Collections.Generic.List<BossRecord>();
            var rec = Challenge.bosses.Find(b => b != null && b.boss == boss);
            newBest = rec == null || rec.bestTime < 0f || time < rec.bestTime;
            firstFlawless = noDamage && (rec == null || !rec.noDamage);
            if (!save || string.IsNullOrEmpty(boss)) return;
            if (rec == null) { rec = new BossRecord { boss = boss }; Challenge.bosses.Add(rec); }
            if (newBest) rec.bestTime = time;
            if (noDamage) rec.noDamage = true;
            Write(ChallengePath, Challenge);
        }

        public static void AppendRunLog(string line)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(RunLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}\n");
            }
            catch { /* ignore */ }
        }
    }
}
