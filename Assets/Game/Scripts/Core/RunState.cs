using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>All data of one run. A brand-new instance is created for every new run, so nothing leaks between runs.</summary>
    [Serializable]
    public class RunState
    {
        public int seed;
        public int stageIndex;
        public bool isTutorial;

        public int health;
        public int bombs;
        public int ropes;
        public int gold;

        public float totalTime;
        public float levelTime;

        /// <summary>Owned upgrade ids (ShopOffer.id), in purchase order.</summary>
        public readonly List<string> upgrades = new List<string>();
        /// <summary>Sum of the modifiers of every worn upgrade.</summary>
        public readonly StatModifiers stats = new StatModifiers();
        /// <summary>Custom resources (ammo of new gear, keys, ...) by id.</summary>
        public readonly Dictionary<string, int> counters = new Dictionary<string, int>();

        /// <summary>The player robbed or attacked a shopkeeper who lived: later shops sell nothing and he waits with two brothers.</summary>
        public bool shopkeeperWanted;
        /// <summary>The shopkeeper was killed: there are no more shops in this run.</summary>
        public bool shopkeeperDead;

        public int kills;
        public int levelsCompleted;
        public int goldAtLevelStart;
        public string deathCause;
        public bool finished;
        public bool victory;
        public string reachedStageName;
        public readonly List<string> purchases = new List<string>();

        /// <summary>Owned equipment ids, in wheel order.</summary>
        public readonly List<string> gear = new List<string>();
        /// <summary>Index into gear of the equipped item.</summary>
        public int equipped;
        /// <summary>Index of the item equipped before the current one (quick swap toggles back to it); -1 = none.</summary>
        public int previousEquipped = -1;

        public string EquippedId => gear.Count > 0 ? gear[Mathf.Clamp(equipped, 0, gear.Count - 1)] : null;

        public void Equip(string id)
        {
            int i = gear.IndexOf(id);
            if (i >= 0) SetEquipped(i);
        }

        /// <summary>Equips gear[i], remembering the old item for quick swap.</summary>
        public void SetEquipped(int i)
        {
            if (i < 0 || i >= gear.Count || i == equipped) return;
            previousEquipped = equipped;
            equipped = i;
        }

        /// <summary>Remaining uses of a piece of gear, or -1 for gear that is never used up (glider).</summary>
        public int Ammo(string gearId) => GearRegistry.Get(gearId)?.Ammo(this) ?? -1;

        /// <summary>Owned (in the bag or worn).</summary>
        public bool HasUpgrade(string id) => upgrades.Contains(id);

        /// <summary>Ids of the upgrades being worn: only these have an effect.</summary>
        public readonly List<string> worn = new List<string>();
        readonly Dictionary<string, ShopOffer> owned = new Dictionary<string, ShopOffer>();

        /// <summary>How many items of one slot can be worn at once.</summary>
        public static int SlotCapacity(string slot) => slot == "charm" ? 2 : 1;

        public ShopOffer Owned(string id) => id != null && owned.TryGetValue(id, out var o) ? o : null;
        public bool IsWorn(string id) => worn.Contains(id);
        /// <summary>Owned and in effect (worn). Use this for behaviour, HasUpgrade for ownership.</summary>
        public bool HasActive(string id) => worn.Contains(id);

        int WornInSlot(string slot)
        {
            int n = 0;
            foreach (var id in worn) { var o = Owned(id); if (o != null && o.slot == slot) n++; }
            return n;
        }

        /// <summary>Owns the upgrade from now on. It is put on straight away when its slot is free, else it goes in the bag.</summary>
        public void AddUpgrade(ShopOffer o)
        {
            if (o == null || string.IsNullOrEmpty(o.id) || upgrades.Contains(o.id)) return;
            upgrades.Add(o.id);
            owned[o.id] = o;
            if (string.IsNullOrEmpty(o.slot) || WornInSlot(o.slot) < SlotCapacity(o.slot)) worn.Add(o.id);
            RecomputeStats();
        }

        /// <summary>Removes an owned upgrade completely (tests, curses...).</summary>
        public void RemoveUpgrade(string id)
        {
            upgrades.Remove(id); worn.Remove(id); owned.Remove(id);
            RecomputeStats();
        }

        /// <summary>Puts an owned item on; whatever fills its slot goes back in the bag.</summary>
        public bool Wear(string id)
        {
            var o = Owned(id);
            if (o == null || worn.Contains(id)) return false;
            if (!string.IsNullOrEmpty(o.slot))
                while (WornInSlot(o.slot) >= SlotCapacity(o.slot))
                    worn.Remove(worn.Find(w => Owned(w) != null && Owned(w).slot == o.slot));
            worn.Add(id);
            RecomputeStats();
            return true;
        }

        public bool TakeOff(string id)
        {
            var o = Owned(id);
            if (o == null || string.IsNullOrEmpty(o.slot) || !worn.Remove(id)) return false;   // slot-less upgrades are always on
            RecomputeStats();
            return true;
        }

        public bool ToggleWorn(string id) => worn.Contains(id) ? TakeOff(id) : Wear(id);

        void RecomputeStats()
        {
            stats.Clear();
            foreach (var id in worn) stats.Add(Owned(id)?.modifiers);
        }

        public int GetResource(string id)
        {
            switch (id)
            {
                case Res.Gold: return gold;
                case Res.Health: return health;
                case Res.Bombs: return bombs;
                case Res.Ropes: return ropes;
                default: return id != null && counters.TryGetValue(id, out int v) ? v : 0;
            }
        }

        /// <summary>Adds (or with a negative amount, spends) a resource. Unknown ids become custom counters.</summary>
        public void AddResource(string id, int amount)
        {
            switch (id)
            {
                case Res.Gold: gold += amount; break;
                case Res.Health: health += amount; break;
                case Res.Bombs: bombs += amount; break;
                case Res.Ropes: ropes += amount; break;
                default:
                    if (string.IsNullOrEmpty(id)) break;
                    counters.TryGetValue(id, out int v);
                    counters[id] = Mathf.Max(0, v + amount);
                    break;
            }
        }
    }

    public static class RunSession
    {
        public static RunState Current { get; private set; }

        /// <summary>Unique id that increases with every new run (used by tests to detect stale state).</summary>
        public static int RunCounter { get; private set; }

        public static RunState NewRun(int seed, bool tutorial, PlayerConfig cfg)
        {
            RunCounter++;
            Current = new RunState
            {
                seed = seed,
                stageIndex = 0,
                isTutorial = tutorial,
                health = cfg != null ? cfg.startHealth : 4,
                bombs = cfg != null ? cfg.startBombs : 4,
                ropes = cfg != null ? cfg.startRopes : 4,
                gold = cfg != null ? cfg.startGold : 0,
            };
            if (cfg != null && cfg.startingGear != null) Current.gear.AddRange(cfg.startingGear);
            else { Current.gear.Add("bomb"); Current.gear.Add("rope"); }
            if (tutorial) { Current.bombs = 3; Current.ropes = 3; }
            return Current;
        }

        public static void Clear() => Current = null;

        public static int RandomSeed()
        {
            unchecked
            {
                int s = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
                s &= 0x7FFFFFFF;
                return (s % 99999999) + 1;
            }
        }
    }
}
