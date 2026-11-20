using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>How many of one kind of creature the player has killed (GameRecords.killCounts; see Codex).</summary>
    [System.Serializable]
    public class KillCount { public string name; public int count; }

    /// <summary>
    /// The Field Guide: the book found in the chest at the end of the tutorial (or handed over when the tutorial is
    /// skipped). It says what every place on the mountain asks of the player and how every creature in it fights.
    /// A key item: once got it is never lost, and it is read from the bag (KEY ITEMS) - see CodexScreen.
    /// The book itself is the list below: one page for each place, then its creatures, then whoever rules it.
    /// A page's picture is that of the prefab named in 'prefab'.
    /// </summary>
    public static class Codex
    {
        public const string Key = "codex";
        public const string Title = "Field Guide";
        public const string About = "Everything known of the mountain: what each place asks of you, and how each creature in it fights. Read it from the bag, under KEY ITEMS.";

        public class Page
        {
            public string section, name, prefab, text;
            [UnityEngine.Tooltip("(Two names joined with a plus are two pictures, the second laid over the first: the arrow trap is a block and its face.)  A page with no prefab to take its picture from: a picture by name (\"Tiles/Frost/spikes\"), or \"scene:Frost\" - a little view of that place made of its own tiles.")]
            public string art;
            public bool place;
            [UnityEngine.Tooltip("Always readable: a place, a trap, something that cannot be killed. A creature's page is blank until one has been killed.")]
            public bool info;
        }

        // ---- what has been killed, by the name the creature goes by (kept with the save's records)
        static readonly Dictionary<string, int> killsInMemory = new Dictionary<string, int>();

        /// <summary>How many of these the player has killed in this save.</summary>
        public static int Kills(string creature)
        {
            if (string.IsNullOrEmpty(creature)) return 0;
            if (Testing) return killsInMemory.TryGetValue(creature, out int n) ? n : 0;
            var l = SaveSystem.Records.killCounts;
            if (l != null) foreach (var k in l) if (k.name == creature) return k.count;
            return 0;
        }

        /// <summary>One more of them killed by the player. The first of a kind is written to the save at once (its page opens).</summary>
        public static void CountKill(string creature)
        {
            if (string.IsNullOrEmpty(creature)) return;
            if (Testing) { killsInMemory[creature] = Kills(creature) + 1; return; }
            var rec = SaveSystem.Records;
            if (rec.killCounts == null) rec.killCounts = new List<KillCount>();
            foreach (var k in rec.killCounts) if (k.name == creature) { k.count++; return; }       // (goes out with the next save of the records)
            rec.killCounts.Add(new KillCount { name = creature, count = 1 });
            SaveSystem.SaveRecords();
        }

        /// <summary>Forgets every kill (tests).</summary>
        public static void ForgetKills() { killsInMemory.Clear(); }

        static readonly HashSet<string> inMemory = new HashSet<string>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        /// <summary>The player has the book (in tests: only once it was given in this test run).</summary>
        public static bool Owned
        {
            get
            {
                if (Testing) return inMemory.Contains(Key);
                var k = SaveSystem.Records.keyItems;
                return k != null && k.Contains(Key);
            }
        }

        /// <summary>Hands the book over. True if it was not owned before.</summary>
        public static bool Give()
        {
            if (Owned) return false;
            if (Testing) { inMemory.Add(Key); return true; }
            if (SaveSystem.Records.keyItems == null) SaveSystem.Records.keyItems = new List<string>();
            SaveSystem.Records.keyItems.Add(Key);
            SaveSystem.SaveRecords();
            return true;
        }

        /// <summary>Takes it away again (tests).</summary>
        public static void Forget() { inMemory.Remove(Key); }

        static List<Page> pages;
        public static List<Page> Pages => pages ?? (pages = Build());

        static List<Page> Build()
        {
            var l = new List<Page>();
            string sec = "";
            void Place(string name, string text) { sec = name; l.Add(new Page { section = name, name = name, text = text, place = true }); }
            void One(string name, string prefab, string text) { l.Add(new Page { section = sec, name = name, prefab = prefab, text = text }); }
            // (a trap, or anything else that is known without having to be killed first)
            void Info(string name, string prefab, string text) { l.Add(new Page { section = sec, name = name, prefab = prefab, text = text, info = true }); }

            Place("The Mountain",
                "Every climb is new: the areas come in a different order, and their levels are never laid the same way twice. " +
                "Each area ends in the hall of whatever rules it; past the last of them lies the Sunstone.\n\n" +
                "What you carry is lost when a climb ends. Embers, freed villagers and this book are not.");
            Info("The Reaper", "wraith",
                "Stay too long in one level and it comes. It drifts through walls straight at you, nothing can hurt it, and its touch is the end of the climb. " +
                "When the clock turns red, find the door.");
            Info("Shopkeeper", "shopkeeper",
                "He sells for gold and wants no trouble. Strike him or his wares and he warns you - twice. After that he is your enemy, his wares are free, " +
                "and his brothers remember you for the rest of the climb.");
            Info("Arrow Trap", "arrowtrap",
                "A stone face in the wall. It looses one arrow at the first thing that moves in front of it - you, a creature, or a rock you threw to spend it.");
            Info("Spikes", null,
                "Walking into them does nothing. Falling onto them costs two hearts. Creatures knocked onto them die of it too.");

            Place("Mossroot Caverns",
                "Wet stone and old roots. Shafts drop a long way here, and the rope is how you come back up: throw it under a ledge and climb.\n\n" +
                "Arrow traps watch the corridors and stalactites shake before they fall.");
            Info("Stalactite", "stalactite", "Trap. It hangs from the roof and shakes when something passes below - then it falls. Walk on, or wait for it to break on the floor.");
            Info("Rotten Platform", "crumble", "Trap of the climbs. It holds for a moment when stood on, then gives way; a few seconds later it is back. Keep climbing - do not rest on it.");
            Info("Rock Dropper", "rockdropper", "Trap of the shafts. A stone mouth over a ladder: while someone is below, it rattles and lets a rock fall, again and again. Wait for a rock, then go.");
            One("Snake", "snake", "Walks its ledge back and forth. Close up it rears back for a moment, then lunges - only the lunge hurts. One stroke or a stomp.");
            One("Bat", "bat", "Sleeps on the ceiling until it sees you. It flies up, hangs back a moment and swoops; only the swoop hurts. Strike it as it comes in.");
            One("Spider", "spider", "Hangs on the ceiling and drops when you pass below, then hops after you. Look up before you walk under a low roof.");
            One("Broodmother", "broodmother",
                "She fights from the ceiling. Grit trickles down under her as she crawls: when she stops above you, step aside - she lands stuck for a moment, and that is your opening. " +
                "Her egg sacs hatch if they are left, her web slows your feet, and her venom comes in fans. Burst the eggs. Keep moving.");

            Place("Emberdeep Mines",
                "Old workings, and warm. Rubble chokes the tunnels: a bomb opens them, and takes the dirt around it too.\n\n" +
                "Flame vents sputter before they blast. Crushers fall on whatever passes under them, then winch slowly back up.");
            Info("Flame Vent", "flamevent", "Trap. It sputters as a warning, then throws a column of flame two tiles high. It burns everything - creatures too - and lights a bomb left near it.");
            Info("Crusher", "crusher", "Trap. A block in the roof that drops when something passes under it, then winches slowly back up. Go while it is climbing.");
            One("Toad", "toad", "Sits, then leaps at you in an arc. Step under the leap or strike it as it lands.");
            One("Brute", "brute", "Slow until it sees you on its level; then it charges. Jump it: it runs into the wall and stands dazed. It takes more than one stroke.");
            One("Ember Bat", "emberbat", "A bat of the forges. It hunts as its cousins do - the wait, then the swoop - and it is the swoop that burns.");
            One("Forge Brute", "forgebrute",
                "It charges the whole width of its hall: jump it and let the wall stop it - dazed, it is yours, though rocks shake loose from the roof. " +
                "When it pounds the floor, fire comes up in five places: they glow first. Its leaps land with a wave along the ground - jump that too.");

            Place("Sunfall Cliffs",
                "The open face of the mountain, and a long way down. Nothing here can be crossed on foot: a chest by the first gate holds the glider. " +
                "Jump, then hold the jump button while you fall.");
            One("Hawk", "hawk", "Circles its perch, dives at whoever is below it, and climbs away again. Strike it at the bottom of the dive.");
            One("Storm Hawk", "stormhawk",
                "It circles out of reach. It dives - miss you, and it sits on the ground for a moment: strike then. It shoots fans of feathers, drops rocks that can be thrown back, " +
                "and sweeps the whole hall low: jump that.");

            Place("Frostdeep Hollows",
                "The ground is ice. You slide before you stop, so slow down before a ledge: hold the walk button. Cleats give your boots their grip back.\n\n" +
                "The cold freezes whoever it catches for a moment; a fur coat only lets it slow you.");
            Info("Ice", null, "Not a trap, but it kills like one. On ice you slide before you stop, and a slide carries you off the ledge you meant to stand on. Walk near edges; cleats give your boots their grip back.");
            One("Penguin", "penguin", "Stands still until it sees you on its level; then it slides on its belly in a straight line - off ledges too - until a wall stops it. Jump it, or stomp it.");
            One("Yeti", "yeti", "Holds its ground and lobs snowballs. They do no harm by themselves: they shove you and freeze you - next to a drop, that is harm enough.");
            One("Frost Bat", "frostbat", "A bat of the frozen caves. Killed, it freezes solid and drops as a chunk of ice you can pick up and throw.");
            One("Yeti King", "yetiking",
                "Snowballs in fans, a snow boulder that grows as it rolls (jump it early, or cut it down), icicles shaken loose above you, and a belly slide across the whole hall: " +
                "jump it, and the wall dazes him. His leap lands with a wave of ice. Never stand frozen in front of him: he runs in and punches. At half his health he is angrier and faster.");

            Place("Sporegloom",
                "It is dark, and your lantern is small. The glowing mushrooms light the way - and the wide caps throw you high into the air and break your fall.\n\n" +
                "Spore clouds do no harm, but in one your left is your right. A spore mask makes them nothing.");
            Info("Bounce Cap", "bouncecap", "A wide glowing cap. Fall onto it and it throws you high into the air - higher with the jump held - and the fall does you no harm. A way up, and a light.");
            Info("Spore Cloud", "sporecloud", "What a puffshroom leaves. It does no harm: while you stand in it, and for a moment after, your left is your right. A spore mask makes it nothing.");
            One("Puffshroom", "puffshroom", "A walking mushroom that never bites. Come close and it swells, then bursts a cloud of spores - and it bursts when it dies. Bait the puff, step back, then strike.");
            One("Lurecap", "lurecap", "It looks like one of the mushrooms that light the way. Walk up to it and its glow turns red: it stands up and hops after you. Strike it while it still sits.");
            One("Gloom Moth", "moth", "Drawn to your light. It never hurts you: it drinks the light, and while it flutters round your head your lantern shrinks. One stroke gives the light back.");
            One("Rot Heart", "sporeheart",
                "It hangs from the roof and never moves, too high to reach from the floor. The bounce caps carry you up onto the platforms beside it: jump from there and strike, or throw things. " +
                "It answers with fans of darts, tendrils and spores.");

            Place("Gearworks",
                "Everything here turns. Belts in the floor carry you, the creatures and whatever lies on them: a jump starts where the belt has taken you. Heavy boots ignore them.\n\n" +
                "Saws run their rails for ever and cut creatures as gladly as you. They cannot be broken, only jumped.");
            Info("Conveyor", "conveyor", "A belt in the floor. It carries whatever stands on it - you, creatures, a bomb. It does no harm by itself; it changes where your jump starts. Heavy boots ignore it.");
            Info("Saw", "sawblade", "Trap. It runs its rail back and forth for ever and cannot be broken: jump it. It cuts creatures as gladly as you, so a sentry can be led into it.");
            One("Sentry", "sentry", "A wind-up guard behind a shield: nothing gets through from the front. It turns slowly - jump over it and strike its back, stomp it, or lead it into a saw.");
            One("Scrap Beetle", "beetle", "It eats metal. A lit bomb nearby is a meal: it swallows the bomb, glows red and runs at you with the fuse still burning. Mind where you put a bomb down.");
            One("Drone", "drone", "A turret that hangs under the roof. When its eye turns red it has seen you: one slow bolt comes at where you stood. Walk on, or cut the bolt with your blade.");
            One("Cogwyrm", "gearwyrm",
                "A centipede of gears on a track round the hall. Only the head can be hurt; every segment is a saw. It runs the floor (jump against it, or dash through), spits bolts, " +
                "rolls up into a wheel nothing can hurt (run under it while it is high) - and dives head first into the floor, where it sticks. That is when you strike. " +
                "A segment drops off at every quarter of its health and rolls away as a saw.");

            Place("Sandsunk Tombs",
                "Sandstone, and sand that moves. Quicksand slows you and then swallows you: cross a pool in hops, never on foot.\n\n" +
                "Spear plates in the floor keep a steady beat: the tips show first. Cross between two beats.");
            Info("Quicksand", "quicksand", "Trap. Walking through it is slow, and whoever stays in it is pulled under, hurt and thrown back out. A jump gets you free - cross a pool in hops. It swallows bombs.");
            Info("Spear Plate", "speartrap", "Trap. Spears come up through it on a steady beat: the tips show first, then they stand for a moment. Cross between two beats, or jump it. Creatures are speared too.");
            One("Scarab", "scarab", "Small, quick and never alone. They run at you and, from a few steps off, crouch and pounce. One stroke each - but count them first.");
            One("Mummy", "mummy", "Slow and hard to put down. It throws a length of wrapping that drags you in, and its grab takes your gold, not your blood. Destroy it and the gold falls out again.");
            One("Sand Worm", "sandworm", "A mound in the floor, and nothing but a blast hurts it down there. Come near and it rises to spit a clod of sand. Up, it can be struck.");
            One("Dune Queen", "dunequeen",
                "Her whole hall is quicksand. Three blocks of stone rise out of it - the only firm ground - and whatever she does breaks them, so no one stands still for long. " +
                "She breaches from below, spits, and calls her scarabs.");

            Place("Shardlight Geode",
                "A hollow lined with crystal. Strike a cluster - or let anything strike it - and it rings, then bursts: shards fly to both sides and cut whatever they meet, creatures too. " +
                "A dampener makes you proof against them.\n\nLenses in the roof gather light on a beat and let it fall as a burning column: wait for the dark.");
            Info("Resonant Crystal", "crystal", "Left alone it does nothing. Struck - by a blade, a blast, a flying shard - it rings, and a moment later bursts: shards fly to both sides and up at a slant. Use it, or stand clear of it.");
            Info("Light Lens", "lightlens", "Trap. A lens in the roof gathers light on a beat - a thin line to the floor shows where - then lets it fall as a column that burns whoever stands in it. It cannot be jumped: wait for the dark.");
            One("Shardling", "shardling", "A crab grown over with crystal. It tucks in, then rushes; only the rush hurts. Its back is all points - it cannot be stomped - and broken, it comes apart into smaller ones.");
            One("Mimic", "mimic_gem", "It lies there looking like a gem, a pot or a crate. Reach for it and it shudders, then snaps. A truth lens shows a mimic red.");
            One("Glint", "glint", "A bat of living prism. It keeps its distance and shoots light: a thin line reaches for you, locks, and a moment later burns. Step out of the line once it has locked.");
            One("The Facet", "facet",
                "A colossus too big for its hall: only its fists come down, and their shell turns every stroke. What breaks a shell is crystal - a shard from the clusters in the floor. " +
                "A cracked fist lies open: every blow on it is a blow on the Facet. Its light bends off the crystals like off mirrors.");

            Place("The Sunken Vault",
                "The last hall, under all the others. The Sunstone lies here, and it cannot be lifted while its keeper stands.");
            One("Hollow Warden", "warden",
                "A robe with nothing in it. It drifts after you: fans of gloom orbs, a rain of them from above, a lunge, and a blink to your other side. " +
                "Below half its health it is faster and throws rings of orbs.");
            // pictures for the pages that have no creature or trap to show: every place a view of its own stone,
            // and the two things that are part of the floor
            foreach (var pg in l)
            {
                if (pg.place && TileSets.TryGetValue(pg.name, out var set)) pg.art = "scene:" + set;
                else if (pg.name == "Spikes") pg.art = "Tiles/Mossroot/spikes";
                else if (pg.name == "Ice") pg.art = "Tiles/Frost/dirt_15_0";
                else if (TrapArt.TryGetValue(pg.name, out var art)) pg.art = art;
            }
            return l;
        }

        /// <summary>
        /// Traps shown by a picture of their own rather than by whatever their prefab happens to draw first (an
        /// arrow trap's prefab starts with the little mark that says it is loaded, not with the trap).
        /// </summary>
        static readonly Dictionary<string, string> TrapArt = new Dictionary<string, string>
        {
            { "Arrow Trap", "Tiles/Mossroot/trapblock+Sprites/Traps/arrowtrap_armed" }, { "Stalactite", "Sprites/Traps/stalactite" }, { "Rotten Platform", "Sprites/Traps/crumble" },
            { "Rock Dropper", "Sprites/Traps/dropper" }, { "Flame Vent", "Tiles/Emberdeep/ventblock" }, { "Crusher", "Sprites/Traps/crusher" },
            { "Saw", "Sprites/Traps/saw" }, { "Spear Plate", "Sprites/Traps/spear_2" }, { "Resonant Crystal", "Sprites/Traps/crystal_0" }, { "Light Lens", "Sprites/Traps/lens" },
        };

        /// <summary>Which tiles each place is built of (the name of its tile set).</summary>
        public static readonly Dictionary<string, string> TileSets = new Dictionary<string, string>
        {
            { "The Mountain", "Sunfall" }, { "Mossroot Caverns", "Mossroot" }, { "Emberdeep Mines", "Emberdeep" }, { "Sunfall Cliffs", "Sunfall" },
            { "Frostdeep Hollows", "Frost" }, { "Sporegloom", "Sporegloom" }, { "Gearworks", "Gearworks" }, { "Sandsunk Tombs", "Sandsunk" },
            { "Shardlight Geode", "Geode" }, { "The Sunken Vault", "Vault" },
        };
    }
}
