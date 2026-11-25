using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>
    /// Blueprints: what the villagers of Brindlemoor sell is not the thing itself but how it is made. Bought once
    /// (in embers, kept with the save as a key item "bp:&lt;id&gt;"), the shops on the mountain carry it from then on,
    /// for gold - and the loot chests and the camp's chest may hold it. Until then it is not seen on the mountain.
    ///   - a piece of equipment a villager sells (the smith's blade, the ropemaker's boots...) is locked until its
    ///     blueprint is bought; equipment no villager sells (each area's own pieces, the Pebble Pouch) never is;
    ///   - the two consumables of a mountain shelf are drawn from bombs, ropes, pebbles and every consumable whose
    ///     blueprint is known (the lamp oil, the flares, the heart draught): with none known, two of those three.
    ///   - Pip sells the consumables themselves, for one climb, in sparks - but one that wants a blueprint
    ///     (ConsumablePlans) is offered as its blueprint, in embers, until that is bought.
    /// The map, the lost letter and paper are not blueprints: they are bought as they are.
    /// Levels are generated on a worker thread, so what is known is read from a snapshot (Refresh, on the main
    /// thread: at the start of every level and when a blueprint is bought).
    /// Automated runs that keep no village state (the plain test bot) lock nothing.
    /// </summary>
    public static class Blueprints
    {
        public const string Prefix = "bp:";

        /// <summary>The consumables that want a blueprint (Pip sells it until it is known, the thing itself after).</summary>
        public static readonly string[] ConsumablePlans = { "lampoil", FlareGear.OfferId, "vigor" };

        /// <summary>What the mountain shelves always may carry in their two consumable places.</summary>
        public static readonly string[] BaseConsumables = { "bombs", "ropes", "pebbles" };

        static HashSet<string> villageWares;
        static volatile HashSet<string> known = new HashSet<string>();
        static volatile bool locking;

        /// <summary>The ids the villagers sell as blueprints (everything on their stalls but the map, the letter and paper).</summary>
        public static HashSet<string> VillageWares
        {
            get
            {
                if (villageWares != null) return villageWares;
                var s = new HashSet<string>();
                foreach (var v in Villagers.All)
                    if (v.wares != null && v.blueprints)
                        foreach (var w in v.wares)
                            if (IsBlueprintWare(w)) s.Add(w);
                foreach (var id in ConsumablePlans) s.Add(id);
                return villageWares = s;
            }
        }

        public static bool IsBlueprintWare(string id) =>
            !string.IsNullOrEmpty(id) && id != "?" && id != MapScreen.ItemId && id != Villagers.KeyLetter && id != Story.Paper;

        /// <summary>The blueprint of this ware has been bought (read from the save; main thread).</summary>
        public static bool Known(string id) => Villagers.HasKey(Prefix + id);

        /// <summary>Learns a blueprint (kept with the save) and refreshes the snapshot.</summary>
        public static void Learn(string id)
        {
            Villagers.GiveKey(Prefix + id);
            Refresh();
            LevelFactory.ForgetAhead();
        }

        /// <summary>Takes a fresh snapshot of what is known (main thread).</summary>
        public static void Refresh()
        {
            var s = new HashSet<string>();
            foreach (var id in VillageWares) if (Known(id)) s.Add(id);
            known = s;
            locking = Villagers.Enabled;
        }

        /// <summary>Not to be seen on the mountain yet: a villager's ware whose blueprint is not known (any thread).</summary>
        public static bool Locked(string id) => locking && VillageWares.Contains(id) && !known.Contains(id);

        /// <summary>How many of the villagers' blueprints are known (snapshot).</summary>
        public static int KnownCount => known.Count;

        /// <summary>The two consumables of a mountain shelf: two different ones out of bombs, ropes and every known consumable blueprint.</summary>
        public static List<string> ShelfConsumables(ShopConfig shop, Rng rng, int count = 2)
        {
            var pool = new List<string>(BaseConsumables);
            if (shop != null)
                foreach (var id in VillageWares)
                {
                    var o = shop.Find(id);
                    if (o != null && !o.isUpgrade && !Locked(id) && !pool.Contains(id)) pool.Add(id);
                }
            pool.Sort(System.StringComparer.Ordinal);          // (the same order every time: the seed alone decides)
            var picks = new List<string>();
            while (picks.Count < count && pool.Count > 0)
            {
                int k = rng.Range(0, pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return picks;
        }
    }
}
