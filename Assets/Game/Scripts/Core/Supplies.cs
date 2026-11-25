using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// What a save keeps of the consumables between climbs: the bombs, ropes, pebbles, flares and sheets of paper
    /// a run ends with - by death, by giving up, or by winning - are there when the next one begins. Nothing fills
    /// the bags by itself any more: more is bought from Pip, for sparks (or found and bought on the mountain).
    /// A save that has never set out gets one full set of bags to begin with.
    /// The tutorial, a Challenge fight and the automated runs that keep no village state fill their own bags as
    /// before (RunSession.FillBags) and leave this alone.
    /// </summary>
    public static class Supplies
    {
        /// <summary>A real run of a save (not the tutorial, not a Challenge fight, not a plain test run).</summary>
        public static bool Applies(RunState run) => run != null && !run.isTutorial && !run.bossPractice && Villagers.Enabled && !SaveSystem.Apart;

        // tests that keep a village state of their own keep these here, not in the save
        static bool testInit;
        static int tBombs, tRopes, tPebbles, tFlares, tPaper;
        static bool TestState => Villagers.testRescued != null;
        public static void ResetTestState() { testInit = false; tBombs = tRopes = tPebbles = tFlares = tPaper = 0; }

        /// <summary>A new run takes what the save has kept (a save's first run: full bags).</summary>
        public static void TakeInto(RunState run)
        {
            if (!Applies(run)) return;
            bool init; int b, r, p, f, s;
            if (TestState) { init = testInit; b = tBombs; r = tRopes; p = tPebbles; f = tFlares; s = tPaper; }
            else
            {
                SaveSystem.EnsureLoaded();
                var rec = SaveSystem.Records;
                init = rec.stockKept; b = rec.stockBombs; r = rec.stockRopes; p = rec.stockPebbles; f = rec.stockFlares; s = rec.stockPaper;
            }
            if (!init) { RunSession.FillBags(run); return; }      // a save that never set out: one full set of bags
            run.bombs = Mathf.Clamp(b, 0, run.BombCap);
            run.ropes = Mathf.Clamp(r, 0, run.RopeCap);
            run.AddResource(Res.Pebbles, -run.Pebbles);
            run.AddResource(Res.Pebbles, Mathf.Max(0, p));        // (never more than the bag holds)
            if (f > 0)
            {
                run.AddResource(FlareGear.AmmoId, f);
                if (!run.gear.Contains(FlareGear.Id)) run.gear.Add(FlareGear.Id);
            }
            if (s > 0) run.AddResource(Story.Paper, s);
        }

        /// <summary>The run is over: what it carried is kept for the next one.</summary>
        public static void KeepFrom(RunState run)
        {
            if (!Applies(run)) return;
            int b = run.bombs, r = run.ropes, p = run.Pebbles, f = run.GetResource(FlareGear.AmmoId), s = run.GetResource(Story.Paper);
            if (TestState) { testInit = true; tBombs = b; tRopes = r; tPebbles = p; tFlares = f; tPaper = s; return; }
            var rec = SaveSystem.Records;
            rec.stockKept = true; rec.stockBombs = b; rec.stockRopes = r; rec.stockPebbles = p; rec.stockFlares = f; rec.stockPaper = s;
        }

        /// <summary>What is kept now (tests and the bag screen).</summary>
        public static int Kept(string id)
        {
            if (TestState) return id == Res.Bombs ? tBombs : id == Res.Ropes ? tRopes : id == Res.Pebbles ? tPebbles : id == FlareGear.AmmoId ? tFlares : id == Story.Paper ? tPaper : 0;
            var rec = SaveSystem.Records;
            return id == Res.Bombs ? rec.stockBombs : id == Res.Ropes ? rec.stockRopes : id == Res.Pebbles ? rec.stockPebbles : id == FlareGear.AmmoId ? rec.stockFlares : id == Story.Paper ? rec.stockPaper : 0;
        }
    }
}
