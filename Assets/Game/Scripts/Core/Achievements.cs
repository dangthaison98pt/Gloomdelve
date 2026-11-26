using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>One achievement: what it is called in the game, and on Steam.</summary>
    public class AchievementDef
    {
        /// <summary>The game's own id (kept in achievements.json).</summary>
        public string id;
        /// <summary>The API name on Steam (Steamworks > Stats &amp; Achievements): the same list, entered there by hand.</summary>
        public string steamId;
        public string title;
        public string description;
        /// <summary>A secret: until it is earned the list shows only "???" and a hint.</summary>
        public bool hidden;
        /// <summary>What the list shows of a secret one not yet earned.</summary>
        public string hint = "A secret.";
        /// <summary>Its picture, Art/Sprites/UI/ach_&lt;id&gt; (the list and the toast).</summary>
        public string Icon => "ach_" + id;
    }

    /// <summary>
    /// Where an achievement also goes when it is earned (Steam, later). The game keeps its own list whatever the
    /// backend: it is the list that the menu shows, and on start everything already earned is sent along again, so
    /// a backend added later (or a Steam account that was offline) catches up by itself.
    /// </summary>
    public interface IAchievementBackend
    {
        void Unlock(AchievementDef def);
        /// <summary>Clear one (Steam: for testing only).</summary>
        void Clear(AchievementDef def);
    }

    /// <summary>
    /// Achievements: earned once, for good, for the player on this machine (not for one save - like Steam's, they
    /// belong to the player, not to a game in progress). Kept in achievements.json beside the saves; tests keep
    /// theirs in memory. Earning one shows a toast that slides down from the top of the screen (AchievementToast).
    ///
    /// Steam: the list below is the one to enter on Steamworks (same API names, steamId). With Steamworks.NET in the
    /// project and STEAMWORKS_NET defined, SteamAchievementBackend sends every one along (SteamUserStats); nothing
    /// else in the game has to change.
    /// </summary>
    public static class Achievements
    {
        public const string OcarinaOfGoat = "ocarina_of_goat";
        public const string FirstVictory = "first_victory";

        /// <summary>
        /// The bosses that have an achievement of their own (the first time each falls on a run - a Challenge fight
        /// does not count): the name it goes by in the game (Boss.displayName), its id here, its prefab (its picture
        /// is drawn from it), the achievement's name.
        /// </summary>
        public static readonly (string boss, string id, string prefab, string title)[] BossList =
        {
            ("Broodmother", "boss_broodmother", "broodmother", "Pest Control"),
            ("Rot Heart", "boss_rot_heart", "sporeheart", "Heartbreaker"),
            ("Forge Brute", "boss_forge_brute", "forgebrute", "Quenched"),
            ("Yeti King", "boss_yeti_king", "yetiking", "Cold Feet"),
            ("Storm Hawk", "boss_storm_hawk", "stormhawk", "Clipped Wings"),
            ("Cogwyrm", "boss_cogwyrm", "gearwyrm", "Stripped Gears"),
            ("Dune Queen", "boss_dune_queen", "dunequeen", "Dethroned"),
            ("The Facet", "boss_the_facet", "facet", "Shattered"),
            ("Eclipse King", "boss_eclipse_king", "eclipseking", "Totality"),
        };

        /// <summary>The id of a boss's achievement (by its displayName), or null when it has none.</summary>
        public static string BossId(string boss)
        {
            foreach (var b in BossList) if (b.boss == boss) return b.id;
            return null;
        }

        static AchievementDef[] BuildList()
        {
            var list = new List<AchievementDef>
            {
                new AchievementDef
                {
                    id = FirstVictory, steamId = "ACH_FIRST_VICTORY", title = "First Light",
                    description = "Win a run for the first time.",
                },
            };
            foreach (var b in BossList)
                list.Add(new AchievementDef
                {
                    id = b.id, steamId = "ACH_" + b.id.ToUpperInvariant(), title = b.title,
                    description = "Defeat " + b.boss + " on a run (Challenge fights do not count).",
                });
            list.AddRange(Extras);
            return list.ToArray();
        }

        /// <summary>The secrets and the rest, after the victory and the bosses.</summary>
        static readonly AchievementDef[] Extras =
        {
            new AchievementDef
            {
                id = OcarinaOfGoat, steamId = "ACH_OCARINA_OF_GOAT", title = "Ocarina of GOAT", hidden = true,
                description = "Found a little blue ocarina. Whoever carved it was the greatest of all time. Probably.",
                hint = "Somewhere in Brindlemoor, something wants a good shake.",
            },
        };

        /// <summary>Every achievement, in the order the menu lists them (after Extras: static fields are set in order).</summary>
        public static readonly AchievementDef[] All = BuildList();

        /// <summary>A run has ended: one won (not a Challenge fight, not the tutorial) earns First Light.</summary>
        public static void RunEnded(RunState run)
        {
            if (run != null && run.victory && !run.bossPractice && !run.isTutorial) Unlock(FirstVictory);
        }

        /// <summary>A boss has fallen: the first time on a run (never in a Challenge fight) earns its achievement.</summary>
        public static void BossDefeated(RunState run, string boss)
        {
            if (run == null || run.bossPractice || run.isTutorial) return;
            var id = BossId(boss);
            if (id != null) Unlock(id);
        }

        /// <summary>Earned now (the toast listens).</summary>
        public static event Action<AchievementDef> Earned;

        /// <summary>Where else earned achievements go (Steam); null = only the game's own list.</summary>
        public static IAchievementBackend Backend;

        [Serializable]
        class Saved { public List<string> earned = new List<string>(); public List<string> earnedAt = new List<string>(); }

        static Saved saved;
        static readonly HashSet<string> testEarned = new HashSet<string>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        /// <summary>Tests: nothing earned.</summary>
        public static void ResetTestState() { testEarned.Clear(); }

        public static AchievementDef Find(string id) { foreach (var a in All) if (a.id == id) return a; return null; }

        public static bool IsEarned(string id) => Testing ? testEarned.Contains(id) : Load().earned.Contains(id);

        public static int EarnedCount { get { int n = 0; foreach (var a in All) if (IsEarned(a.id)) n++; return n; } }

        /// <summary>When it was earned (local time, "yyyy-MM-dd"), or null.</summary>
        public static string EarnedOn(string id)
        {
            if (Testing) return null;
            var s = Load();
            int i = s.earned.IndexOf(id);
            return i >= 0 && i < s.earnedAt.Count ? s.earnedAt[i] : null;
        }

        /// <summary>Earn it (once: nothing happens the second time). True when it was earned just now.</summary>
        public static bool Unlock(string id)
        {
            var def = Find(id);
            if (def == null || IsEarned(id)) return false;
            if (Testing) testEarned.Add(id);
            else
            {
                var s = Load();
                s.earned.Add(id);
                s.earnedAt.Add(DateTime.Now.ToString("yyyy-MM-dd"));
                Write(s);
            }
            try { Backend?.Unlock(def); } catch (Exception e) { Debug.LogWarning("[Achievements] backend: " + e.Message); }
            Earned?.Invoke(def);
            return true;
        }

        /// <summary>Send everything already earned to the backend again (on start: Steam catches up).</summary>
        public static void SyncBackend()
        {
            if (Backend == null) return;
            foreach (var a in All)
                if (IsEarned(a.id)) { try { Backend.Unlock(a); } catch (Exception e) { Debug.LogWarning("[Achievements] backend: " + e.Message); } }
        }

        static string FilePath => SaveSystem.FileInSaveDir("achievements.json");

        static Saved Load()
        {
            if (saved != null) return saved;
            saved = new Saved();
            try { if (File.Exists(FilePath)) saved = JsonUtility.FromJson<Saved>(File.ReadAllText(FilePath)) ?? new Saved(); }
            catch (Exception e) { Debug.LogWarning("[Achievements] could not read: " + e.Message); saved = new Saved(); }
            return saved;
        }

        static void Write(Saved s)
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(s, true)); }
            catch (Exception e) { Debug.LogWarning("[Achievements] could not write: " + e.Message); }
        }

        /// <summary>The game's start: the Steam backend, when the project has Steamworks.NET.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if STEAMWORKS_NET
            if (Backend == null && SteamAchievementBackend.Ready) Backend = new SteamAchievementBackend();
#endif
            SyncBackend();
        }
    }

#if STEAMWORKS_NET
    /// <summary>
    /// Steam (Steamworks.NET): an achievement earned in the game is set on Steam and stored at once. Needs SteamAPI
    /// initialised (SteamManager) and the achievements entered on Steamworks with the API names of Achievements.All.
    /// </summary>
    public class SteamAchievementBackend : IAchievementBackend
    {
        public static bool Ready => SteamManager.Initialized;
        public void Unlock(AchievementDef def)
        {
            if (!Ready || string.IsNullOrEmpty(def.steamId)) return;
            Steamworks.SteamUserStats.SetAchievement(def.steamId);
            Steamworks.SteamUserStats.StoreStats();
        }
        public void Clear(AchievementDef def)
        {
            if (!Ready || string.IsNullOrEmpty(def.steamId)) return;
            Steamworks.SteamUserStats.ClearAchievement(def.steamId);
            Steamworks.SteamUserStats.StoreStats();
        }
    }
#endif
}
