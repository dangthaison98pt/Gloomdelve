using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// In-game automated play test (Windows build / editor play mode):
    ///   Gloomdelve.exe -autotest 5 [-autotestSeed 100] [-autotestOut path] [-autotestShots]
    /// For each seed the bot replays the validator's route through the real PlayerController (god mode, no damage
    /// or knockback so the route is exact) for every stage including the final level, then writes a report and quits.
    /// </summary>
    public static class AutoTest
    {
        public static bool Active;
        public static int SeedsTotal = 3;
        public static int FirstSeed = 1000;
        public static int SeedIndex;
        public static string OutDir;
        public static bool Screenshots;
        public static float TimeScale = 5f;
        public static int LevelsOk, LevelsFail, RunsChecked, StateLeaks;
        static readonly StringBuilder report = new StringBuilder();

        public static int CurrentSeed => FirstSeed + SeedIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-autotest":
                        Active = true;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n)) SeedsTotal = n;
                        break;
                    case "-autotestSeed":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int s)) FirstSeed = s;
                        break;
                    case "-autotestOut":
                        if (i + 1 < args.Length) OutDir = args[i + 1];
                        break;
                    case "-autotestShots":
                        Screenshots = true;
                        break;
                }
            }
            if (!Active) return;
            if (string.IsNullOrEmpty(OutDir)) OutDir = Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(OutDir);
            report.Clear();
            report.AppendLine($"Gloomdelve autotest {DateTime.Now:yyyy-MM-dd HH:mm:ss}  seeds {FirstSeed}..{FirstSeed + SeedsTotal - 1}");
            Debug.Log("[AutoTest] active, output " + OutDir);
        }

        public static void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[AutoTest] " + line);
        }

        public static void LevelResult(int seed, string stage, bool ok, string info)
        {
            if (ok) LevelsOk++; else LevelsFail++;
            Log($"seed {seed} {stage,-8} {(ok ? "PASS" : "FAIL")} {info}");
        }

        public static void CheckFreshRun(RunState run, PlayerConfig cfg, World world, int spawnCount)
        {
            RunsChecked++;
            var problems = new StringBuilder();
            if (run.stageIndex != 0) problems.Append("stageIndex ");
            if (run.health != cfg.startHealth) problems.Append("health ");
            if (run.bombs != cfg.startBombs) problems.Append("bombs ");
            if (run.ropes != cfg.startRopes) problems.Append("ropes ");
            if (run.gold != cfg.startGold) problems.Append("gold ");
            if (run.upgrades.Count > 0 || !run.stats.IsZero || run.counters.Count > 0) problems.Append("upgrades ");
            if (run.totalTime > 0.5f) problems.Append("time ");
            if (run.finished || run.victory || run.kills != 0) problems.Append("flags ");
            if (world.EntityCount != spawnCount + 1) problems.Append($"entities({world.EntityCount} vs {spawnCount + 1}) ");
            if (problems.Length > 0) { StateLeaks++; Log($"seed {run.seed} NEW RUN STATE LEAK: {problems}"); }
            else Log($"seed {run.seed} new run state fresh: OK");
        }

        public static bool NextSeed()
        {
            SeedIndex++;
            return SeedIndex < SeedsTotal;
        }

        public static void Finish()
        {
            Log($"SUMMARY levels passed {LevelsOk}, failed {LevelsFail}, runs checked {RunsChecked}, state leaks {StateLeaks}");
            try { File.WriteAllText(Path.Combine(OutDir, "autotest_report.txt"), report.ToString()); }
            catch (Exception e) { Debug.LogError(e); }
            Application.Quit(LevelsFail == 0 && StateLeaks == 0 ? 0 : 1);
        }
    }
}
