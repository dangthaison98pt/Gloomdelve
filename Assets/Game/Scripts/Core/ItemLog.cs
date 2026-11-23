using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>
    /// What the player has picked up before, in this save: the first time a thing is picked up the screen makes
    /// something of it (a card with its picture and what it is - HUD.Gain); every time after, it is one line in
    /// the list at the left edge. Kept with the save's records. While tests run nothing is written to the save:
    /// they keep their own list in memory.
    /// Every thing has one id, whatever the way it was come by: a bomb bag lying on the ground and the Bomb Bag on
    /// a shop's shelf are both "bombs" (Pickup.itemId is the shop offer's id for a thing that is also sold; gear goes
    /// by its gear id). So the card comes once, for the first of them.
    /// </summary>
    public static class ItemLog
    {
        static readonly HashSet<string> inMemory = new HashSet<string>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        /// <summary>What the things lying about were called by saves from before every thing had one id: their names.</summary>
        static readonly Dictionary<string, string> OldNames = new Dictionary<string, string>
        {
            { "bombs", "bomb bag" }, { "ropes", "rope pile" }, { "heart_pickup", "heart" }, { "heartvessel", "heart vessel" },
            { "gem", "emerald" }, { "gold_small", "gold nugget" }, { "gold_big", "gold chunk" },
        };

        /// <summary>
        /// How the same thing was written down by saves from before every thing had one id: under its name on the
        /// ground ("bomb bag"), or with the way it was got in front ("ware:bombs", "gear:glider"). They still count.
        /// </summary>
        static IEnumerable<string> Forms(string id)
        {
            yield return id;
            yield return "ware:" + id;
            yield return "gear:" + id;
            if (OldNames.TryGetValue(id, out var old)) yield return old;
        }

        /// <summary>Picked up before? 'id' is the thing's id (one for a thing, wherever it turns up).</summary>
        public static bool Seen(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (!Testing && SaveSystem.Apart) return true;      // a Challenge fight has no save to learn things in: no cards over the fight
            if (Testing) { foreach (var f in Forms(id)) if (inMemory.Contains(f)) return true; return false; }
            var s = SaveSystem.Records.seenItems;
            if (s == null) return false;
            foreach (var f in Forms(id)) if (s.Contains(f)) return true;
            return false;
        }

        /// <summary>True the first time this is asked about a thing (and remembers it, by its id).</summary>
        public static bool FirstTime(string id)
        {
            if (Seen(id)) return false;
            if (Testing) { inMemory.Add(id); return true; }
            if (SaveSystem.Records.seenItems == null) SaveSystem.Records.seenItems = new List<string>();
            SaveSystem.Records.seenItems.Add(id);
            SaveSystem.SaveRecords();
            return true;
        }

        /// <summary>Forgets a thing again, in every way it may have been written down (tests).</summary>
        public static void Forget(string id) { foreach (var f in Forms(id)) inMemory.Remove(f); }

        /// <summary>What a pickup is, in a sentence, by the resource it gives (the things that have no description of their own).</summary>
        public static string Describe(string resource, int value)
        {
            switch (resource)
            {
                case Res.Health: return "It mends a heart. Left where it lies while you are whole: it will keep.";
                case Res.Bombs: return "Bombs. They open walls, floors and most things that stand in the way - you among them.";
                case Res.Ropes: return "Rope. Thrown up, it hangs from the ceiling; let down from a ledge, it is a way back.";
                case "ember": return "An ember off one of the mountain's great beasts: each has one. It goes in the Ember Pouch, and pays for what lasts - the elder's work on your bags.";
                case "spark": return "A spark off one of the mountain's great beasts: they come by the handful. Brindlemoor's people take them for what they sell for a climb.";
                case "maxhealth": return "One more heart, for good.";
                case Res.Pebbles: return "Pebbles: shot for your slingshot. You can only carry so many - a Pebble Pouch holds more.";
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
