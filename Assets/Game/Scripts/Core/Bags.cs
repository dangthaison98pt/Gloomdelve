using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// How much the player can carry of the things that are counted: bombs, ropes, pebbles, and the embers in the
    /// Ember Pouch. Each has a bag of a first size (five bombs, five ropes, ten pebbles, five embers), and the
    /// elder of Brindlemoor makes any of them larger for embers: +5 a time, three times at most, dearer each time
    /// (2, 3, then 4 embers). What he has made larger stays so for good - it is kept with the save, like the
    /// embers themselves. A run sets out with its bags full (RunSession.FillBags), and a bag he has just let out
    /// is filled on the spot. (The Pebble Pouch bought on the mountain is something else: +10 pebbles while it is
    /// worn, for that run only.)
    /// While tests run nothing is read from or written to the save: they keep their own sizes in memory.
    /// </summary>
    public static class Bags
    {
        public const string Bombs = "bombs", Ropes = "ropes", Pebbles = "pebbles", Embers = "embers";
        public static readonly string[] All = { Bombs, Ropes, Pebbles, Embers };

        /// <summary>What one enlarging adds, and how often a bag can be enlarged.</summary>
        public const int Step = 5, MaxLevel = 3;

        static readonly Dictionary<string, int> testLevels = new Dictionary<string, int>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        /// <summary>Tests: every bag back to its first size.</summary>
        public static void ResetTestState() { testLevels.Clear(); }

        /// <summary>How many times this bag has been enlarged (0 - MaxLevel).</summary>
        public static int Level(string kind)
        {
            if (kind == Embers) return Mathf.Clamp((Villagers.PouchCapacity - Villagers.PouchStartCapacity) / Step, 0, MaxLevel);
            if (Testing) return testLevels.TryGetValue(kind, out int t) ? t : 0;
            var r = SaveSystem.Records;
            int v = kind == Bombs ? r.bagBombs : kind == Ropes ? r.bagRopes : kind == Pebbles ? r.bagPebbles : 0;
            return Mathf.Clamp(v, 0, MaxLevel);
        }

        /// <summary>What the elder's work adds to the first size of this bag.</summary>
        public static int Bonus(string kind) => Level(kind) * Step;

        public static bool CanEnlarge(string kind) => Level(kind) < MaxLevel && (kind != Embers || Villagers.HasKey(Villagers.KeyPouch));

        /// <summary>Embers the next enlarging of this bag costs: 2, then 3, then 4.</summary>
        public static int Price(string kind) => 2 + Level(kind);

        public static string Name(string kind)
            => kind == Bombs ? "Bomb Bag" : kind == Ropes ? "Rope Bag" : kind == Pebbles ? "Pebble Bag" : "Ember Pouch";

        /// <summary>How many this bag holds now, for the run in hand (the pouch holds the same in every run).</summary>
        public static int Holds(string kind, RunState run)
        {
            if (kind == Embers) return Villagers.PouchCapacity;
            if (run == null) return 0;
            return kind == Bombs ? run.BombCap : kind == Ropes ? run.RopeCap : run.PebbleCap;
        }

        /// <summary>
        /// The elder enlarges the bag: the embers are paid, the bag holds five more for good - and, for the run in
        /// hand, it is full at once (bombs, ropes, pebbles; the pouch holds what it held). False when it cannot be
        /// done or paid.
        /// </summary>
        public static bool Enlarge(string kind)
        {
            if (!CanEnlarge(kind)) return false;
            if (!Villagers.SpendEmbers(Price(kind))) return false;
            if (kind == Embers) { Villagers.ExpandPouch(Step); return true; }
            if (Testing) testLevels[kind] = Level(kind) + 1;
            else
            {
                var r = SaveSystem.Records;
                if (kind == Bombs) r.bagBombs = Level(kind) + 1;
                else if (kind == Ropes) r.bagRopes = Level(kind) + 1;
                else if (kind == Pebbles) r.bagPebbles = Level(kind) + 1;
                SaveSystem.SaveRecords();
            }
            var run = RunSession.Current;
            if (run != null)
            {
                if (kind == Bombs) run.bombs = Mathf.Max(run.bombs, run.BombCap);
                else if (kind == Ropes) run.ropes = Mathf.Max(run.ropes, run.RopeCap);
                else if (kind == Pebbles) run.AddResource(Res.Pebbles, run.PebbleCap);
                if (!Testing) SaveSystem.CheckpointInVillage(run);      // (what the run carries is saved with what was paid for it)
            }
            return true;
        }
    }
}
