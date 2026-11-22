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
        /// <summary>Position in this run's plan (stageOrder), not in GameConfig.stages.</summary>
        public int stageIndex;
        /// <summary>This run's stages as indices into GameConfig.stages, and the name each has in this run. Filled by RunPlan.</summary>
        public int[] stageOrder;
        public string[] stageNames;
        public bool isTutorial;
        /// <summary>Boss test from the main menu: the run is just one boss arena. Dying starts it again, winning goes
        /// back to the boss list, and nothing is written to the records.</summary>
        public bool bossPractice;
        [Tooltip("The run has not left Brindlemoor yet: the player is walking through the village, before the first level.")]
        public bool inVillage;
        /// <summary>The night before ended badly (or was given up): this run begins waking in bed, upstairs at home.
        /// A game started or continued from the menu begins downstairs instead, ready to go out.</summary>
        public bool wakeInBed;
        [Tooltip("The rescued villagers' gifts have been handed out for this run.")] public bool perksApplied;
        [Tooltip("Extra seconds in every level before the Reaper comes (the Lamplighter's lamp oil).")] public float extraTime;
        /// <summary>The caged villager of this run has been freed (the cage is not filled again if the level is entered again).</summary>
        public bool villagerFreed;
        /// <summary>Position in the run of the boss level whose boss has fallen, -1 = none: entered again, that arena is empty.</summary>
        public int bossDownStage = -1;
        /// <summary>Health lost in this run (shown on the boss test result).</summary>
        public int damageTaken;

        public int health;
        /// <summary>Health can never go above this; only a max-health item raises it. 0 = no limit (tests).</summary>
        public int maxHealth;
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

        /// <summary>The parts of the hammer this run has found (Story): haft, head, heart. With all three the hammer is in the bag.</summary>
        public bool hammerHandle, hammerHead, hammerCore;
        /// <summary>Which of the three bound spirits of the run's fourth area have been freed: one bit for each of its levels.</summary>
        public int ghostMask;

        public int kills;
        /// <summary>How many of the kills were bosses.</summary>
        public int bossKills;
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

        /// <summary>Puts back the upgrades of a saved run: what was owned, and which of it was being worn.</summary>
        public void RestoreUpgrades(IEnumerable<string> ids, IEnumerable<string> wornIds, ShopConfig shop)
        {
            if (ids != null && shop != null)
                foreach (var id in ids)
                {
                    var o = shop.Find(id);
                    if (o == null || upgrades.Contains(id)) continue;
                    upgrades.Add(id);
                    owned[id] = o;
                }
            if (wornIds != null) foreach (var w in wornIds) if (owned.ContainsKey(w) && !worn.Contains(w)) worn.Add(w);
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

        /// <summary>Health is at its limit: hearts and tonics would do nothing.</summary>
        public bool HealthFull => maxHealth > 0 && health >= maxHealth;

        /// <summary>What can be carried before any bag is enlarged (PlayerConfig).</summary>
        public int pebbleBase = 10, bombBase = 5, ropeBase = 5;
        /// <summary>What the elder's work adds to a bag in this run. Nothing in a Challenge fight: its times are
        /// compared boss by boss, so every save goes in with the same bags.</summary>
        int KeptBonus(string kind) => bossPractice ? 0 : Bags.Bonus(kind);
        /// <summary>How many pebbles can be carried now: the first size of the bag, what the elder has added to it
        /// for good (Bags), and what the Pebble Pouch adds while it is worn.</summary>
        public int PebbleCap => Mathf.Max(0, pebbleBase + KeptBonus(Bags.Pebbles) + stats.pebbleBonus);
        /// <summary>How many bombs / ropes can be carried: the first size of the bag and what the elder has added to it.</summary>
        public int BombCap => Mathf.Max(0, bombBase + KeptBonus(Bags.Bombs));
        public int RopeCap => Mathf.Max(0, ropeBase + KeptBonus(Bags.Ropes));

        /// <summary>The most that can be carried of a resource; int.MaxValue for what has no bag (gold, health...).</summary>
        public int CapOf(string id)
            => id == Res.Bombs ? BombCap : id == Res.Ropes ? RopeCap : id == Res.Pebbles ? PebbleCap : int.MaxValue;
        /// <summary>How many more of it there is room for.</summary>
        public int RoomFor(string id) { int cap = CapOf(id); return cap == int.MaxValue ? int.MaxValue : Mathf.Max(0, cap - GetResource(id)); }
        public int Pebbles => GetResource(Res.Pebbles);

        public int GetResource(string id)
        {
            switch (id)
            {
                case Res.Gold: return gold;
                case Res.Health: return health;
                case Res.MaxHealth: return maxHealth;
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
                case Res.Health:
                    health += amount;
                    if (maxHealth > 0 && health > maxHealth) health = maxHealth;
                    break;
                case Res.MaxHealth: maxHealth += amount; health = maxHealth; break;
                // (what has a bag never goes over what the bag holds; what is carried above it already is left alone)
                case Res.Bombs: bombs = amount > 0 ? Mathf.Max(bombs, Mathf.Min(bombs + amount, BombCap)) : Mathf.Max(0, bombs + amount); break;
                case Res.Ropes: ropes = amount > 0 ? Mathf.Max(ropes, Mathf.Min(ropes + amount, RopeCap)) : Mathf.Max(0, ropes + amount); break;
                default:
                    if (string.IsNullOrEmpty(id)) break;
                    counters.TryGetValue(id, out int v);
                    int next = Mathf.Max(0, v + amount);
                    if (amount > 0 && id == Res.Pebbles) next = Mathf.Max(v, Mathf.Min(next, PebbleCap));
                    counters[id] = next;
                    break;
            }
        }
    }

    public static class RunSession
    {
        public static RunState Current { get; private set; }

        /// <summary>Unique id that increases with every new run (used by tests to detect stale state).</summary>
        public static int RunCounter { get; private set; }

        /// <param name="challenge">A Challenge fight (bossPractice): set before the bags are filled, so they are
        /// filled to their first size and not to what the elder has made of them.</param>
        public static RunState NewRun(int seed, bool tutorial, PlayerConfig cfg, bool challenge = false)
        {
            RunCounter++;
            Current = new RunState
            {
                seed = seed,
                stageIndex = 0,
                isTutorial = tutorial,
                bossPractice = challenge,
                health = cfg != null ? cfg.startHealth : 10,
                maxHealth = cfg != null ? cfg.startHealth : 10,
                bombs = cfg != null ? cfg.startBombs : 4,
                ropes = cfg != null ? cfg.startRopes : 4,
                gold = cfg != null ? cfg.startGold : 0,
                pebbleBase = cfg != null ? cfg.pebbleCapacity : 10,
                bombBase = cfg != null ? cfg.bombCapacity : 5,
                ropeBase = cfg != null ? cfg.ropeCapacity : 5,
            };
            if (cfg != null && cfg.startingGear != null) Current.gear.AddRange(cfg.startingGear);
            else { Current.gear.Add("bomb"); Current.gear.Add("rope"); }
            // A Challenge fight is fought with the slingshot and the bombs, and nothing else: no rope to hang out
            // of reach on, no Recall Charm to leave by.
            if (challenge) Current.gear.RemoveAll(g => g != "slingshot" && g != "bomb");
            // A run sets out with its bags full: as many bombs, ropes and pebbles as they hold (Bags). The tutorial
            // hands out its own few.
            if (tutorial) { Current.bombs = 3; Current.ropes = 3; }
            else FillBags(Current);
            if (challenge) Current.ropes = 0;       // (no rope in the bag, so none counted on the screen)
            // A run begins with the first of its gear in hand: the slingshot. The tutorial teaches the bombs first
            // ("Your GEAR is BOMBS"), so there the bombs are in hand.
            if (tutorial) Current.equipped = Mathf.Max(0, Current.gear.IndexOf("bomb"));
            // (the Recall Charm is found in the tutorial's chest, not carried into it - except by the test bots, whose room is a tutorial too)
            if (tutorial && !ScenarioRunner.Active && !AutoTest.Active) Current.gear.Remove("recall");
            Current.inVillage = !tutorial && Villagers.Enabled;       // a real run starts in the village
            return Current;
        }

        /// <summary>Starts a boss test: a fresh explorer dropped straight into the boss arena at this stage of the run.
        /// It belongs to no save (SaveSystem.Apart): the same fight whoever starts it.</summary>
        public static RunState NewBossPractice(int catalogueIndex, PlayerConfig cfg)
        {
            SaveSystem.BeginApart();
            var run = NewRun(RandomSeed(), false, cfg, challenge: true);
            RunPlan.UseSingleStage(run, catalogueIndex, "CHALLENGE");
            run.stageIndex = 0;
            return run;
        }

        public static void Clear() => Current = null;

        /// <summary>Fills the bomb bag, the rope bag and the pebble bag of a run to what they hold now.</summary>
        public static void FillBags(RunState r)
        {
            if (r == null) return;
            r.bombs = Mathf.Max(r.bombs, r.BombCap);
            r.ropes = Mathf.Max(r.ropes, r.RopeCap);
            r.AddResource(Res.Pebbles, r.PebbleCap);        // (it never goes over what the bag holds)
        }

        /// <summary>What has to be kept of a run to go on with it later, from the start of the level it is in.</summary>
        public static RunSave Capture(RunState r, string stageName)
        {
            var s = new RunSave
            {
                active = true, seed = r.seed, stageIndex = r.stageIndex, stageOrder = r.stageOrder, stageNames = r.stageNames,
                inVillage = r.inVillage, extraTime = r.extraTime, damageTaken = r.damageTaken,
                health = r.health, maxHealth = r.maxHealth, bombs = r.bombs, ropes = r.ropes, gold = r.gold, totalTime = r.totalTime,
                equipped = r.equipped, previousEquipped = r.previousEquipped,
                shopkeeperWanted = r.shopkeeperWanted, shopkeeperDead = r.shopkeeperDead, kills = r.kills, bossKills = r.bossKills, levelsCompleted = r.levelsCompleted,
                villagerFreed = r.villagerFreed, bossDownStage = r.bossDownStage, stageName = stageName ?? "",
                hammerHandle = r.hammerHandle, hammerHead = r.hammerHead, hammerCore = r.hammerCore, ghostMask = r.ghostMask,
            };
            s.upgrades.AddRange(r.upgrades); s.worn.AddRange(r.worn); s.purchases.AddRange(r.purchases); s.gear.AddRange(r.gear);
            foreach (var kv in r.counters) { s.counterIds.Add(kv.Key); s.counterValues.Add(kv.Value); }
            return s;
        }

        /// <summary>Goes on with a saved run: the same night, the same level (it is built again from the same seed),
        /// everything the player had when they entered it.</summary>
        public static RunState Restore(RunSave s, GameConfig cfg)
        {
            RunCounter++;
            var r = new RunState
            {
                seed = s.seed, stageIndex = s.stageIndex, stageOrder = s.stageOrder, stageNames = s.stageNames,
                inVillage = s.inVillage, extraTime = s.extraTime, damageTaken = s.damageTaken, perksApplied = true,
                health = s.health, maxHealth = s.maxHealth, bombs = s.bombs, ropes = s.ropes, gold = s.gold, totalTime = s.totalTime,
                shopkeeperWanted = s.shopkeeperWanted, shopkeeperDead = s.shopkeeperDead, kills = s.kills, bossKills = s.bossKills, levelsCompleted = s.levelsCompleted,
                villagerFreed = s.villagerFreed, bossDownStage = s.bossDownStage,
                hammerHandle = s.hammerHandle, hammerHead = s.hammerHead, hammerCore = s.hammerCore, ghostMask = s.ghostMask,
            };
            if (s.gear != null) r.gear.AddRange(s.gear);
            r.equipped = Mathf.Clamp(s.equipped, 0, Mathf.Max(0, r.gear.Count - 1));
            r.previousEquipped = s.previousEquipped;
            if (s.purchases != null) r.purchases.AddRange(s.purchases);
            for (int i = 0; s.counterIds != null && s.counterValues != null && i < s.counterIds.Count && i < s.counterValues.Count; i++)
                r.counters[s.counterIds[i]] = s.counterValues[i];
            r.RestoreUpgrades(s.upgrades, s.worn, cfg != null ? cfg.shop : null);
            Current = r;
            return r;
        }

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
