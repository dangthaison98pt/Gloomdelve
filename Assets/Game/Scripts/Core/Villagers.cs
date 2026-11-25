using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>One villager of Brindlemoor who is lost on the mountain until the player finds and frees them.</summary>
    public class VillagerDef
    {
        public string id, name, role;
        /// <summary>What their house in the village is (shown when they are freed).</summary>
        public string place;
        /// <summary>What they say from the cage, and at home in the village.</summary>
        public string cagedLine, homeLine;
        /// <summary>Shop offer ids on sale outside their house once they are home. "?" = a different piece of equipment every run.</summary>
        public string[] wares;
        /// <summary>Price of their wares compared with the shops on the mountain (1 = the same).</summary>
        public float priceFactor = 1f;
        /// <summary>What they sell are blueprints (Blueprints): bought once, the mountain's shops carry it. False: the thing itself, for one climb, in sparks (Pip) - though a consumable whose blueprint is not known yet is offered as its blueprint first.</summary>
        public bool blueprints = true;
    }

    /// <summary>
    /// The people of Brindlemoor and what the player keeps between runs.
    ///  - Lost villagers: in every run, while any of them is still lost, one is caged somewhere off the main path
    ///    in ONE level, in the second, third or fourth area (which level: by the run seed). Freed villagers are home for good; each keeps their own kind of shop outside
    ///    their house in the village, where the player buys things for the coming run.
    ///  - Key items: the few things that are not lost when a run ends (kept in the save file). The first one is
    ///    the Ember Pouch the village elder gives.
    ///  - Three kinds of money. GOLD is found all over the mountain and spent in the shops up there; it is gone
    ///    when the run ends. The other two drop only from bosses and are never lost. EMBERS are rare - one from
    ///    each boss - and pay for what lasts: the elder's work on the bags, the Mapmaker's map. They go in the pouch,
    ///    as many as it holds (five at first). SPARKS come by the handful - five from each boss - and are what the
    ///    villagers take for their wares, which are for one climb. So nothing can be earned for the village by
    ///    walking into the first level and dying.
    /// </summary>
    public static class Villagers
    {
        public const string KeyPouch = "pouch";
        /// <summary>Oswin's lost letter (a key item, kept for good): the level that holds a caged villager says so, and the map marks the cage.</summary>
        public const string KeyLetter = "lostletter";
        /// <summary>How many embers the pouch holds when the elder first hands it over. His errands make it larger (ExpandPouch).</summary>
        public const int PouchStartCapacity = 5;

        /// <summary>How many embers the pouch holds now.</summary>
        public static int PouchCapacity
        {
            get
            {
                if (TestState) return testCapacity;
                int c = Testing ? 0 : SaveSystem.Records.pouchCapacity;
                return c > 0 ? c : PouchStartCapacity;
            }
        }

        /// <summary>Makes the pouch hold more, for good (a reward for the elder's errands).</summary>
        public static void ExpandPouch(int extra)
        {
            if (extra <= 0 || !HasKey(KeyPouch)) return;
            if (TestState) { testCapacity += extra; return; }
            if (Testing) return;
            SaveSystem.Records.pouchCapacity = PouchCapacity + extra;
            SaveSystem.SaveRecords();
        }

        public static readonly VillagerDef[] All =
        {
            new VillagerDef { id = "smith", name = "Hilde", role = "the Smith", place = "The forge is lit again",
                cagedLine = "Break this open and my forge is yours!", homeLine = "Steel and powder. Nothing leaves my forge dull.",
                wares = new[] { "ironwhip", "blastpowder", "hobnailboots", "magnetcharm" } },
            new VillagerDef { id = "roper", name = "Tam", role = "the Ropemaker", place = "The ropewalk is open again",
                cagedLine = "I tied my own rope too short. Get me out!", homeLine = "Boots with a spring in them. Climb well.",
                wares = new[] { "springboots", "grapplinghook", "climbgloves", "iceboots", "sandstriders" } },       // (no rope: every climb sets out with its bags full)
            new VillagerDef { id = "healer", name = "Maren", role = "the Healer", place = "The apothecary is open again",
                cagedLine = "Careful - but please hurry.", homeLine = "One draught and your heart beats stronger till you sleep.",
                wares = new[] { "wisp", "mendingcharm", "antidote", "dampener" } },
            new VillagerDef { id = "mapper", name = "Oswin", role = "the Mapmaker", place = "The map room is open again",
                cagedLine = "I drew the way in. I never drew the way out.", homeLine = "I know that mountain. Let my needle show you the way.",
                wares = new[] { "spyglass", MapScreen.ItemId, KeyLetter } },
            new VillagerDef { id = "peddler", name = "Pip", role = "the Peddler", place = "The market stall is back",
                cagedLine = "Everything I own for an open door!", homeLine = "Oil, flares, a draught for the heart, paper. All you need for one climb!",
                wares = new[] { "bombs", "ropes", "pebbles", "lampoil", FlareGear.OfferId, "vigor", Story.Paper }, blueprints = false },       // every consumable for a climb, in sparks (and paper for the old signs - Story)
            new VillagerDef { id = "lamplighter", name = "Bera", role = "the Lamplighter", place = "The lamp tower burns again",
                cagedLine = "My lamp went out. Then it came for me.", homeLine = "Take oil. While your lamp burns, the dark is slow to find you.",
                wares = new[] { "lantern", "firecloak" } },       // (no bombs: every climb sets out with its bags full)
        };

        // ------------------------------------------------------------------ tests keep their own state

        /// <summary>Tests: when set, this is who has been rescued (the save file is left alone and nothing is written).</summary>
        public static HashSet<string> testRescued;
        static readonly HashSet<string> testKeys = new HashSet<string>();
        static int testPouch, testSparks, testCapacity = PouchStartCapacity;

        static bool Testing => ScenarioRunner.Active || AutoTest.Active;
        static bool TestState => testRescued != null;

        /// <summary>The village and what is kept between runs belong to real runs only; automated runs keep the plain game.</summary>
        public static bool Enabled => !Testing || TestState;

        public static void ResetTestState() { testKeys.Clear(); testPouch = 0; testSparks = 0; testCapacity = PouchStartCapacity; }

        // ------------------------------------------------------------------ rescued villagers

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
            return -1;
        }

        public static bool IsRescued(string id)
        {
            if (TestState) return testRescued.Contains(id);
            if (Testing) return false;
            var r = SaveSystem.Records.rescued;
            return r != null && r.Contains(id);
        }

        /// <summary>Who is in the cage of this level: one of those still lost, picked by the level seed. Null when everyone is home.</summary>
        /// <summary>The chance that a run has a caged villager in it at all.</summary>
        public const float CaptiveChance = 1f;

        public static VillagerDef NextCaptive(int seed)
        {
            var lost = new List<VillagerDef>();
            foreach (var v in All) if (!IsRescued(v.id)) lost.Add(v);
            if (lost.Count == 0) return null;
            return lost[(int)((uint)Rng.Hash(seed, 4177) % (uint)lost.Count)];
        }

        /// <summary>Marks a villager as rescued and saves it at once: the rescue counts even if the run ends badly.</summary>
        public static bool Rescue(string id)
        {
            if (IndexOf(id) < 0 || IsRescued(id)) return false;
            if (TestState) { testRescued.Add(id); return true; }
            if (Testing) return true;
            if (SaveSystem.Records.rescued == null) SaveSystem.Records.rescued = new List<string>();
            SaveSystem.Records.rescued.Add(id);
            SaveSystem.SaveRecords();
            return true;
        }

        /// <summary>
        /// The one level of a run that hides a caged villager: a level of the second, third or fourth area (never
        /// the first), picked by the run seed. The number is the position of the level in the run (each area is
        /// three levels and a boss). Every run has one (CaptiveChance is 1; below that, -1 means this run has none).
        /// </summary>
        public static int CaptiveStage(int runSeed)
        {
            if ((uint)Rng.Hash(runSeed, 9263) % 1000u >= (uint)(CaptiveChance * 1000f)) return -1;
            int area = 1 + (int)((uint)Rng.Hash(runSeed, 9241) % 3u);
            int level = (int)((uint)Rng.Hash(runSeed, 9257) % 3u);
            return area * 4 + level;
        }

        // ------------------------------------------------------------------ key items and the gold pouch

        public static bool HasKey(string key)
        {
            if (TestState) return testKeys.Contains(key);
            if (Testing) return false;
            var k = SaveSystem.Records.keyItems;
            return k != null && k.Contains(key);
        }

        public static void GiveKey(string key)
        {
            if (HasKey(key)) return;
            if (TestState) { testKeys.Add(key); return; }
            if (Testing) return;
            if (SaveSystem.Records.keyItems == null) SaveSystem.Records.keyItems = new List<string>();
            SaveSystem.Records.keyItems.Add(key);
            SaveSystem.SaveRecords();
        }

        /// <summary>Embers in the pouch (0 without the pouch).</summary>
        public static int Embers
        {
            get
            {
                if (!HasKey(KeyPouch)) return 0;
                return Mathf.Clamp(TestState ? testPouch : (Testing ? 0 : SaveSystem.Records.embers), 0, PouchCapacity);
            }
        }

        /// <summary>Puts embers in the pouch and saves at once: they are the player's whatever happens next.
        /// Returns how many went in (none without the pouch, and no more than it has room for).</summary>
        public static int AddEmbers(int n)
        {
            if (n <= 0 || !Enabled || !HasKey(KeyPouch)) return 0;
            int have = Embers, got = Mathf.Min(n, PouchCapacity - have);
            if (got <= 0) return 0;
            if (TestState) { testPouch = have + got; return got; }
            SaveSystem.Records.embers = have + got;
            SaveSystem.SaveRecords();
            return got;
        }

        /// <summary>Pays embers; false (and nothing taken) when there are not enough.</summary>
        public static bool SpendEmbers(int n)
        {
            if (n <= 0) return true;
            int have = Embers;
            if (!Enabled || have < n) return false;
            if (TestState) { testPouch = have - n; return true; }
            SaveSystem.Records.embers = have - n;
            SaveSystem.SaveRecords();
            return true;
        }

        // ------------------------------------------------------------------ sparks: what the villagers' wares cost

        /// <summary>What a boss leaves behind for the village: one ember, and a handful of sparks.</summary>
        public const int EmbersPerBoss = 1, SparksPerBoss = 5;

        /// <summary>Sparks in the pouch (0 without the pouch). There is no limit to them.</summary>
        public static int Sparks
        {
            get
            {
                if (!HasKey(KeyPouch)) return 0;
                return Mathf.Max(0, TestState ? testSparks : (Testing ? 0 : SaveSystem.Records.sparks));
            }
        }

        /// <summary>Puts sparks in the pouch and saves at once. Returns how many went in (none without the pouch).</summary>
        public static int AddSparks(int n)
        {
            if (n <= 0 || !Enabled || !HasKey(KeyPouch)) return 0;
            if (TestState) { testSparks += n; return n; }
            SaveSystem.Records.sparks = Sparks + n;
            SaveSystem.SaveRecords();
            return n;
        }

        /// <summary>Pays sparks; false (and nothing taken) when there are not enough.</summary>
        public static bool SpendSparks(int n)
        {
            if (n <= 0) return true;
            int have = Sparks;
            if (!Enabled || have < n) return false;
            if (TestState) { testSparks = have - n; return true; }
            SaveSystem.Records.sparks = have - n;
            SaveSystem.SaveRecords();
            return true;
        }

        /// <summary>What lasts is paid for in embers; what is for one climb, in sparks.</summary>
        public static bool PaidInEmbers(ShopOffer o) => o != null && (o.id == MapScreen.ItemId || o.id == KeyLetter || Blueprints.IsBlueprintWare(o.id) && Blueprints.VillageWares.Contains(o.id));

        /// <summary>What a villager asks for a ware, in sparks (in embers for what lasts): about one for every 250 gold it would cost on the
        /// mountain, one less at the Peddler's (never under one).</summary>
        public static int EmberPrice(ShopOffer o, VillagerDef v)
        {
            if (o == null) return 1;
            if (o.blueprintPrice > 0 && (v == null || v.blueprints) && Blueprints.VillageWares.Contains(o.id)) return o.blueprintPrice;      // a blueprint's own price (v = null: priced as a blueprint)
            int e = Mathf.RoundToInt(o.price / 250f);
            if (v != null && v.priceFactor < 1f) e--;
            return Mathf.Max(1, e);
        }

        /// <summary>The offers a villager has out today. "?" wares are pieces of equipment drawn by the run seed.</summary>
        public static List<ShopOffer> WaresOf(VillagerDef v, ShopConfig shop, int runSeed)
        {
            var list = new List<ShopOffer>();
            if (v == null || shop == null || v.wares == null) return list;
            var pool = new List<ShopOffer>();
            foreach (var o in shop.offers) if (o.isUpgrade) pool.Add(o);
            var rng = new Rng(Rng.Hash(runSeed, 7001 + IndexOf(v.id)));
            foreach (var w in v.wares)
            {
                ShopOffer o = null;
                if (w == "?")
                {
                    for (int tries = 0; tries < 20 && pool.Count > 0 && o == null; tries++)
                    {
                        var pick = pool[rng.Range(0, pool.Count)];
                        if (!list.Contains(pick)) o = pick;
                    }
                }
                else o = shop.Find(w);
                if (o != null) list.Add(o);
            }
            return list;
        }
    }
}
