using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>
    /// What the player has picked up before, in this save: the first time a thing is picked up the screen makes
    /// something of it (a card with its picture and what it is - HUD.Gain); every time after, it is one line in
    /// the list at the left edge. Kept with the save's records. While tests run nothing is written to the save:
    /// they keep their own list in memory.
    /// </summary>
    public static class ItemLog
    {
        static readonly HashSet<string> inMemory = new HashSet<string>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        public static bool Seen(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (Testing) return inMemory.Contains(id);
            var s = SaveSystem.Records.seenItems;
            return s != null && s.Contains(id);
        }

        /// <summary>True the first time this is asked about a thing (and remembers it).</summary>
        public static bool FirstTime(string id)
        {
            if (Seen(id)) return false;
            if (Testing) { inMemory.Add(id); return true; }
            if (SaveSystem.Records.seenItems == null) SaveSystem.Records.seenItems = new List<string>();
            SaveSystem.Records.seenItems.Add(id);
            SaveSystem.SaveRecords();
            return true;
        }

        /// <summary>Forgets a thing again (tests).</summary>
        public static void Forget(string id) { inMemory.Remove(id); }

        /// <summary>What a pickup is, in a sentence, by the resource it gives (the things that have no description of their own).</summary>
        public static string Describe(string resource, int value)
        {
            switch (resource)
            {
                case Res.Health: return "It mends a heart. Left where it lies while you are whole: it will keep.";
                case Res.Bombs: return "Bombs. They open walls, floors and most things that stand in the way - you among them.";
                case Res.Ropes: return "Rope. Thrown up, it hangs from the ceiling; let down from a ledge, it is a way back.";
                case "ember": return "An ember off one of the mountain's great beasts. It goes in the Ember Pouch, and Brindlemoor trades in them.";
                case "maxhealth": return "One more heart, for good.";
                default: return $"Treasure. Worth ${value} to any shopkeeper on the mountain.";
            }
        }

        /// <summary>"gold nugget" -> "Gold Nugget".</summary>
        public static string Title(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var c = name.ToCharArray();
            bool up = true;
            for (int i = 0; i < c.Length; i++) { if (up && char.IsLetter(c[i])) c[i] = char.ToUpperInvariant(c[i]); up = c[i] == ' '; }
            return new string(c);
        }
    }
}
