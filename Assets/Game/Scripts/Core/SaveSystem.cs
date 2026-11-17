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
    }

    /// <summary>Settings and best records are saved as JSON in Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        public static GameSettings Settings { get; private set; } = new GameSettings();
        public static GameRecords Records { get; private set; } = new GameRecords();
        static bool loaded;

        static string Dir => Application.persistentDataPath;
        static string SettingsPath => Path.Combine(Dir, "settings.json");
        static string RecordsPath => Path.Combine(Dir, "records.json");
        public static string RunLogPath => Path.Combine(Dir, "run_log.txt");

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Settings = Load<GameSettings>(SettingsPath) ?? new GameSettings();
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
            SaveRecords();
            return news.Trim();
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
