using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// How much the player can carry of the things that are counted: bombs, ropes, pebbles, and the embers in the
    /// Ember Pouch. Each has a bag of a first size (five bombs, five ropes, ten pebbles, five embers), and the
    /// elder of Brindlemoor makes any of them larger for embers: +2 a time for bombs and ropes, +5 for pebbles and
    /// embers, three times at most, dearer each time (1, 2, then 3 embers). What he has made larger stays so for
    /// good - it is kept with the save, like the embers themselves. A bag let out is not filled: what goes in it
    /// is bought (Supplies, Pip). (The Pebble Pouch bought on the mountain is something else: +10 pebbles while
    /// it is worn, for that run only.)
    /// While tests run nothing is read from or written to the save: they keep their own sizes in memory.
    /// </summary>
    public static class Bags
    {
        public const string Bombs = "bombs", Ropes = "ropes", Pebbles = "pebbles", Embers = "embers";
        public static readonly string[] All = { Bombs, Ropes, Pebbles, Embers };

        /// <summary>What one enlarging adds to the pebble bag and the Ember Pouch, and how often a bag can be enlarged.</summary>
        public const int Step = 5, MaxLevel = 3;
        /// <summary>What one enlarging adds to the bomb bag and the rope bag.</summary>
        public const int SmallStep = 2;

        /// <summary>What one enlarging adds to this bag: 2 bombs or ropes, 5 pebbles or embers.</summary>
        public static int StepOf(string kind) => kind == Bombs || kind == Ropes ? SmallStep : Step;

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
        public static int Bonus(string kind) => Level(kind) * StepOf(kind);

        public static bool CanEnlarge(string kind) => Level(kind) < MaxLevel && (kind != Embers || Villagers.HasKey(Villagers.KeyPouch));

        /// <summary>Embers the next enlarging of this bag costs: 1, then 2, then 3 (a boss leaves one ember).</summary>
        public static int Price(string kind) => 1 + Level(kind);

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
        /// The elder enlarges the bag: the embers are paid, the bag holds a little more for good (StepOf). It is not filled:
        /// what goes in it is bought (Supplies). False when it cannot be done or paid.
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
            // (the bag only holds more: what goes in it is bought - Supplies, Pip)
            return true;
        }
    }
}
