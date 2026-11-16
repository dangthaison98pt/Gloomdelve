using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>Debug helper: dumps the first attempts of one stage with their validation result.</summary>
    public static class GenDebug
    {
        public static void DumpStage()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Game/Data/GameConfig.asset");
            var args = System.Environment.GetCommandLineArgs();
            int stage = 6, seed = 1, attempts = 4;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-stage") int.TryParse(args[i + 1], out stage);
                if (args[i] == "-seed") int.TryParse(args[i + 1], out seed);
                if (args[i] == "-attempts") int.TryParse(args[i + 1], out attempts);
            }
            var st = cfg.stages[stage];
            var req = new GenRequest
            {
                seed = LevelFactory.LevelSeed(seed, stage), area = st.area, levelNumber = st.levelNumber, name = st.displayName, config = cfg,
                templates = RoomTemplates.Parse(cfg.roomTemplates.text)
            };
            var opts = LevelValidator.DefaultOptions(cfg.player);
            var sb = new StringBuilder();
            for (int a = 0; a < attempts; a++)
            {
                var rng = new Rng(Rng.Hash(req.seed, a * 7919 + 1));
                var d = LevelGenerator.Build(req, rng);
                var v = LevelValidator.Validate(d, opts);
                sb.AppendLine($"attempt {a + 1}: gates {d.gateCount}/{d.gatesWanted} gear {d.gateGear} ok {v.ok} reason {v.reason} bypassed {v.bypassed} nodes {v.nodes}");
                var rows = new StringBuilder();
                for (int y = 0; y < d.roomTypes.GetLength(1); y++)
                {
                    for (int x = 0; x < d.roomTypes.GetLength(0); x++) rows.Append($"{d.roomTypes[x, y]}:{d.roomTemplates[x, y],-18} ");
                    rows.AppendLine();
                }
                sb.Append(rows);
                sb.AppendLine(GameTests.Ascii(d, "attempt " + (a + 1)));
            }
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Logs/gen_debug.txt"), sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
