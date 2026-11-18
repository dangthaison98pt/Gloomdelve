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
        public bool screenShake = true;
        [Tooltip("The save slot played last (0-2): the one the menu shows records and Challenge times for.")] public int lastSlot = 0;
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

    [Serializable]
    public class GameRecords
    {
        public int runs;
        public int wins;
        public int deaths;
        public int bestStage = -1;
        public string bestStageName = "-";
        public int bestGold;
        public float fastestWin = -1f;
        public int lastSeed;
        public int totalKills;
        [Tooltip("When this save was last played (shown on the save slot).")] public string lastPlayed = "";
        [Tooltip("The run in progress, if there is one.")] public RunSave run = new RunSave();
        [Tooltip("Challenge: best time against each boss, and whether it was ever beaten without losing health.")]
        public System.Collections.Generic.List<BossRecord> bosses = new System.Collections.Generic.List<BossRecord>();
        [Tooltip("Villagers of Brindlemoor freed on the mountain (ids from Villagers.All). They stay freed.")]
        public System.Collections.Generic.List<string> rescued = new System.Collections.Generic.List<string>();
        [Tooltip("Key items: the few things that are not lost when a run ends (\"pouch\" = the elder's Ember Pouch).")]
        public System.Collections.Generic.List<string> keyItems = new System.Collections.Generic.List<string>();
        [Tooltip("Embers in the Ember Pouch (the money of the village: only bosses drop it).")] public int embers;
        [Tooltip("How many embers the pouch holds (0 = what it held when the elder gave it).")] public int pouchCapacity;
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
        public int kills, levelsCompleted;
        public bool villagerFreed;
        public int bossDownStage = -1;
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

    /// <summary>
    /// Saved as JSON in Application.persistentDataPath. Settings (volume, keys, ...) are shared. Everything the
    /// player has achieved - villagers freed, embers, key items, records, Challenge times - lives in one of three
    /// save slots (slot1.json .. slot3.json); 'Records' is always the slot being played. A run in progress is not
    /// saved: a run is one night, and ends when the game is closed.
    /// </summary>
    public static class SaveSystem
    {
        public static GameSettings Settings { get; private set; } = new GameSettings();
        public static GameRecords Records { get; private set; } = new GameRecords();
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

        public static bool SlotUsed(int i) => i >= 0 && i < SlotCount && File.Exists(SlotPath(i));

        /// <summary>What a slot holds, without switching to it (null = empty).</summary>
        public static GameRecords PeekSlot(int i)
        {
            EnsureLoaded();
            if (i < 0 || i >= SlotCount) return null;
            if (i == Slot && SlotUsed(i)) return Records;
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
                Records = Load<GameRecords>(SlotPath(i)) ?? new GameRecords();
            }
            Records.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
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
            if (i == Slot) Records = new GameRecords();
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
            Settings = Load<GameSettings>(SettingsPath) ?? new GameSettings();
            Slot = Mathf.Clamp(Settings.lastSlot, 0, SlotCount - 1);
            // a save from before there were slots becomes slot 1 (the old file is left where it is)
            try { if (File.Exists(LegacyRecordsPath) && !File.Exists(SlotPath(0))) File.Copy(LegacyRecordsPath, SlotPath(0)); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not move the old save into slot 1: {e.Message}"); }
            Records = Load<GameRecords>(RecordsPath) ?? new GameRecords();
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
        public static void SaveRecords() => Write(RecordsPath, Records);

        /// <summary>Returns a short "new record" summary.</summary>
        public static string RecordRunEnd(RunState run, int stageReached)
        {
            EnsureLoaded();
            var r = Records;
            string news = "";
            r.runs++;
            r.lastSeed = run.seed;
            r.totalKills += run.kills;
            if (run.victory)
            {
                r.wins++;
                if (r.fastestWin < 0f || run.totalTime < r.fastestWin) { r.fastestWin = run.totalTime; news += "NEW FASTEST WIN!  "; }
            }
            else r.deaths++;
            if (stageReached > r.bestStage) { r.bestStage = stageReached; r.bestStageName = run.reachedStageName; news += "NEW DEEPEST!  "; }
            if (run.gold > r.bestGold) { r.bestGold = run.gold; news += "NEW BEST GOLD!"; }
            r.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            r.run = new RunSave();          // the run is over: nothing to go on with
            SaveRecords();
            return news.Trim();
        }

        /// <summary>The Challenge record of a boss, or null when it was never beaten there.</summary>
        public static BossRecord BossRecordOf(string boss)
        {
            EnsureLoaded();
            if (Records.bosses == null || string.IsNullOrEmpty(boss)) return null;
            foreach (var b in Records.bosses) if (b != null && b.boss == boss && b.bestTime >= 0f) return b;
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
            if (Records.bosses == null) Records.bosses = new System.Collections.Generic.List<BossRecord>();
            var rec = Records.bosses.Find(b => b != null && b.boss == boss);
            newBest = rec == null || rec.bestTime < 0f || time < rec.bestTime;
            firstFlawless = noDamage && (rec == null || !rec.noDamage);
            if (!save || string.IsNullOrEmpty(boss)) return;
            if (rec == null) { rec = new BossRecord { boss = boss }; Records.bosses.Add(rec); }
            if (newBest) rec.bestTime = time;
            if (noDamage) rec.noDamage = true;
            SaveRecords();
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
