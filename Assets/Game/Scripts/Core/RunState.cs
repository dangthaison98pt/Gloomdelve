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

        public bool springBoots, ironWhip, compass, blastPowder;

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

        /// <summary>Remaining uses, or -1 for gear that is never used up (glider).</summary>
        public int Ammo(GearKind k) => k == GearKind.Bomb ? bombs : k == GearKind.Rope ? ropes : -1;

        public bool HasUpgrade(ShopEffect e)
        {
            switch (e)
            {
                case ShopEffect.SpringBoots: return springBoots;
                case ShopEffect.IronWhip: return ironWhip;
                case ShopEffect.Compass: return compass;
                case ShopEffect.BlastPowder: return blastPowder;
                default: return false;
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
