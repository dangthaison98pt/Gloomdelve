using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>
    /// Which stages a run goes through. GameConfig.stages is the catalogue of everything there is: one block of
    /// stages per area (its levels and its boss arena), then the stages every run ends with (camp, final vault).
    /// Each run draws GameConfig.areasPerRun of the area blocks by its seed and plays them in that drawn order,
    /// then the ending stages. The same seed always gives the same run.
    /// To add an area: add its block of stages to GameConfig.stages - it joins the draw by itself.
    /// </summary>
    public static class RunPlan
    {
        /// <summary>Fills run.stageOrder / run.stageNames if the run has no plan yet.</summary>
        public static void Ensure(RunState run, GameConfig cfg)
        {
            if (run.stageOrder != null && run.stageOrder.Length > 0) return;
            Build(cfg, run.seed, out run.stageOrder, out run.stageNames);
        }

        /// <summary>Every stage of the catalogue in its written order (editor play-tests and debug starts).</summary>
        public static void UseWholeCatalogue(RunState run, GameConfig cfg)
        {
            run.stageOrder = new int[cfg.stages.Length];
            run.stageNames = new string[cfg.stages.Length];
            for (int i = 0; i < cfg.stages.Length; i++) { run.stageOrder[i] = i; run.stageNames[i] = cfg.stages[i].displayName; }
        }

        /// <summary>A run that is just one stage of the catalogue (Challenge: a single boss fight).</summary>
        public static void UseSingleStage(RunState run, int catalogueIndex, string name)
        {
            run.stageOrder = new[] { catalogueIndex };
            run.stageNames = new[] { name ?? "" };
        }

        public static void Build(GameConfig cfg, int seed, out int[] order, out string[] names)
        {
            // area blocks: runs of area / boss stages that share an area; everything else is the ending
            var blocks = new List<List<int>>();
            var ending = new List<int>();
            AreaConfig prev = null;
            for (int i = 0; i < cfg.stages.Length; i++)
            {
                var st = cfg.stages[i];
                if (st.kind == LevelKind.Area || st.kind == LevelKind.Boss)
                {
                    if (blocks.Count == 0 || st.area != prev) blocks.Add(new List<int>());
                    blocks[blocks.Count - 1].Add(i);
                    prev = st.area;
                }
                else { ending.Add(i); prev = null; }
            }

            var picked = new List<List<int>>();
            int want = cfg.areasPerRun <= 0 ? blocks.Count : System.Math.Min(cfg.areasPerRun, blocks.Count);
            if (cfg.areasPerRun <= 0) picked.AddRange(blocks);      // 0 = every area, in the written order
            else
            {
                var pool = new List<List<int>>(blocks);
                var rng = new Rng(Rng.Hash(seed, 9173));
                if (cfg.firstAreaFixed && pool.Count > 0) { picked.Add(pool[0]); pool.RemoveAt(0); }
                while (picked.Count < want && pool.Count > 0)
                {
                    int k = rng.Range(0, pool.Count);
                    picked.Add(pool[k]);
                    pool.RemoveAt(k);
                }
            }

            var o = new List<int>();
            var n = new List<string>();
            for (int b = 0; b < picked.Count; b++)
                foreach (int i in picked[b])
                {
                    var st = cfg.stages[i];
                    o.Add(i);
                    n.Add($"{b + 1}-{(st.kind == LevelKind.Boss ? picked[b].Count : st.levelNumber)}");     // numbered by place in this run
                }
            foreach (int i in ending) { o.Add(i); n.Add(cfg.stages[i].displayName); }
            order = o.ToArray();
            names = n.ToArray();
        }
    }
}
