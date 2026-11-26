using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Gloomdelve
{
    /// <summary>
    /// Scripted interaction tests that run inside the real game (Game scene, real prefabs, real physics):
    ///   Brindlemoor.exe -scenarios [-autotestOut path] [-scenarioFilter "moth,lurecap"]
    /// -scenarioFilter runs only the scenarios whose name contains one of the comma-separated words (quick check
    /// of one feature); without it everything runs (do that before a commit).
    /// Each scenario builds a small arena, scripts the player's inputs (or places things), then checks the outcome.
    /// </summary>
    public static class ScenarioRunner
    {
        public static bool Active;

        static int index;
        static int pass, fail;
        static string[] filter;
        /// <summary>Only some scenarios were asked for (-scenarioFilter): the menu pictures are skipped too.</summary>
        public static bool Filtered => filter != null && filter.Length > 0;

        static bool Wanted(int i)
        {
            if (!Filtered) return true;
            foreach (var f in filter) if (all[i].name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void SkipUnwanted() { while (index < all.Count && !Wanted(index)) index++; }
        static string outDir;
        static readonly StringBuilder report = new StringBuilder();

        static int W = 26;
        const int H = 10;

        public class Ctx
        {
            public GameDirector dir;
            public World world;
            public PlayerController p;
            public RunState run;
            public bool ok = true;
            public string detail = "";

            public IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

            public void Bot(List<FrameInput> frames) { p.bot = new BotInput(frames); }

            public void Teleport(float x, float y)
            {
                p.motor = MotorState.At(new Vector2(x, y));
                p.motor.grounded = false;
                p.motor.apexY = y;
                p.pos = p.prevPos = p.motor.pos;
            }

            public T Spawn<T>(string id, int x, int y, int param = 0, string text = null, int dir = 0) where T : Entity
            {
                var prefab = world.config.prefabs.Get(id);
                return world.Spawn(prefab, new Vector2(x + 0.5f, y), new SpawnPoint(id, new Vector2Int(x, y), dir, param, text)) as T;
            }

            public int Count<T>() where T : Entity
            {
                int n = 0;
                foreach (var e in world.Entities) if (e is T && !e.Dead) n++;
                return n;
            }

            public void Expect(bool cond, string what)
            {
                if (!cond) { ok = false; detail += "[" + what + "] "; }
            }
        }

        class Scenario
        {
            public string name;
            public Action<char[,]> map;
            public Func<Ctx, IEnumerator> run;
        }

        static List<FrameInput> Seq(params (int frames, FrameInput input)[] parts)
        {
            var l = new List<FrameInput>();
            foreach (var (n, fi) in parts) for (int i = 0; i < n; i++) l.Add(fi);
            return l;
        }

        static FrameInput F(int x = 0, bool up = false, bool down = false, bool jp = false, bool jh = false, bool atk = false, bool bomb = false, bool rope = false, bool act = false, bool use = false, bool dash = false)
            => new FrameInput { x = x, up = up, down = down, jumpPressed = jp, jumpHeld = jh, attackPressed = atk, usePressed = bomb || rope || use, interactPressed = act, dashPressed = dash };

        static void Set(char[,] m, int x, int y, char c) { if (x >= 0 && x < W && y >= 0 && y < H) m[x, y] = c; }

        static readonly List<Scenario> all = new List<Scenario>
        {
            new Scenario { name = "player: a dash goes straight through what would hurt, stops at walls, is paid from an energy bar that fills again, and works once in the air", map = m => { Set(m, 3, 1, 'S'); },
                run = c => DashCase(c) },
            new Scenario { name = "whip kills an approaching snake", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 's'); },
                run = c => Run(c, Seq((2, F()), (1, F(atk: true)), (40, F())), 45, () => { c.Expect(c.Count<Snake>() == 0, "snake dead"); c.Expect(c.run.health == 4, "no damage"); }) },

            new Scenario { name = "stomping a snake kills it and bounces", map = m => { Set(m, 3, 1, 'S'); Set(m, 12, 0, '.'); Set(m, 12, 0, 's'); },
                run = c => StompCase(c) },

            new Scenario { name = "snake on a single tile stands still instead of flipping left-right",
                map = m => { Set(m, 3, 1, 'S'); Set(m, 10, 1, '#'); Set(m, 10, 2, 's'); Set(m, 14, 1, '#'); Set(m, 15, 1, '#'); Set(m, 14, 2, 's'); },
                run = c => PatrolCase(c) },

            new Scenario { name = "picked-up rock thrown at a snake kills it", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 'o'); Set(m, 8, 1, 's'); Set(m, 10, 1, 'X'); },
                run = c => Run(c, Seq((7, F(x: 1)), (12, F()), (1, F(down: true, atk: true)), (4, F()), (1, F(atk: true)), (60, F())), 80,
                    () => { c.Expect(c.Count<Snake>() == 0, "snake dead"); c.Expect(c.run.health == 4, "player unhurt"); }) },

            new Scenario { name = "bomb destroys dirt but not bedrock, player escapes", map = m => { Set(m, 3, 1, 'S'); for (int y = 1; y <= 3; y++) { Set(m, 8, y, '#'); Set(m, 9, y, 'X'); } },
                run = c => { c.run.Equip("bomb"); return Run(c, Seq((40, F(x: 1)), (1, F(down: true, bomb: true)), (80, F(x: -1)), (150, F())), 230, () =>
                {
                    var g = c.world.Grid;
                    c.Expect(g.Get(8, 1) == TileType.Empty, "dirt destroyed");
                    c.Expect(g.Get(9, 1) == TileType.Bedrock && g.Get(9, 2) == TileType.Bedrock, "bedrock intact");
                    c.Expect(c.run.health == 4 && !c.p.IsDead, "player safe");
                    c.Expect(c.run.bombs == 3, "bomb consumed");
                }); } },

            new Scenario { name = "bomb chain reaction is bounded", map = m => { Set(m, 2, 1, 'S'); },
                run = c => ChainCase(c) },

            new Scenario { name = "thrown rock triggers arrow trap; arrow hurts player (2)", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 'o'); Set(m, 15, 1, 'A'); for (int x = 16; x < W - 1; x++) for (int y = 1; y < H - 1; y++) Set(m, x, y, 'X'); },
                run = c => Run(c, Seq((7, F(x: 1)), (12, F()), (1, F(down: true, atk: true)), (4, F()), (1, F(atk: true)), (120, F())), 150, () =>
                {
                    c.Expect(c.world.Grid.Get(15, 1) == TileType.ArrowTrap, "trap block exists");
                    c.Expect(c.Count<Arrow>() == 1, "one arrow fired");
                    c.Expect(c.run.health == 2, $"arrow did 2 damage (health {c.run.health})");
                }) },

            new Scenario { name = "stalactite falls on a snake", map = m => { Set(m, 2, 1, 'S'); Set(m, 10, 4, '#'); Set(m, 10, 3, 'v'); Set(m, 15, 1, 's'); },
                run = c => Run(c, Seq((1, F())), 360, () => { c.Expect(c.Count<Snake>() == 0, "snake killed"); c.Expect(c.Count<Stalactite>() == 0, "stalactite shattered"); }) },

            new Scenario { name = "spikes wound (2 hearts, thrown clear) and only kill at low health", map = m => { Set(m, 2, 1, 'S'); Set(m, 12, 1, '^'); },
                run = c => SpikeCase(c) },

            new Scenario { name = "rope: throw, climb, step onto a high ledge", map = m => { Set(m, 11, 1, 'S'); for (int x = 12; x < 16; x++) for (int y = 1; y <= 6; y++) Set(m, x, y, '#'); },
                run = c => { c.run.Equip("rope"); return Run(c, Seq((2, F()), (1, F(rope: true)), (30, F()), (110, F(up: true)), (1, F(x: 1, jp: true, jh: true)), (40, F(x: 1, jh: true)), (30, F())), 220,
                    () => { c.Expect(c.p.pos.y >= 6.99f && c.p.pos.x > 12f, $"on top (pos {c.p.pos})"); c.Expect(c.run.ropes == 3, "rope used"); }); } },

            new Scenario { name = "rope hook visibly grabs a ledge lip / a wall face / the ceiling", map = m =>
                {
                    Set(m, 6, 1, 'S');
                    for (int x = 7; x <= 9; x++) for (int y = 1; y <= 8; y++) Set(m, x, y, '#');     // ledge top at row 8 (lip)
                    for (int x = 15; x <= 17; x++) for (int y = 1; y <= 13; y++) Set(m, x, y, '#');  // tall wall (face)
                    for (int x = 20; x <= 23; x++) Set(m, x, 6, 'X');                                  // low ceiling
                },
                run = c => RopeHoldCase(c) },

            new Scenario { name = "rope into open air finds no hold and drops back", map = m => { Set(m, 10, 1, 'S'); },
                run = c => RopeAirCase(c) },

            new Scenario { name = "hooked rope vanishes after its lifetime", map = m => { Set(m, 5, 1, 'S'); },
                run = c => RopeLifeCase(c, false) },

            new Scenario { name = "rope does not vanish while being climbed", map = m => { Set(m, 5, 1, 'S'); },
                run = c => RopeLifeCase(c, true) },

            new Scenario { name = "gear wheel freezes the game and equips the chosen gear", map = m => { Set(m, 3, 1, 'S'); },
                run = c => WheelCase(c) },

            new Scenario { name = "gear swap with real R1 presses: each tap goes on to the next item", map = m => { Set(m, 3, 1, 'S'); },
                run = c => WheelInputCase(c) },

            new Scenario { name = "use button uses only the equipped gear", map = m => { Set(m, 12, 1, 'S'); },
                run = c => UseGearCase(c) },

            new Scenario { name = "rope gate: 4-high cliff cannot be passed without the rope", map = RopeGateMap,
                run = c => RopeGateCase(c, false) },

            new Scenario { name = "rope gate: rope beside the cliff gets you over", map = RopeGateMap,
                run = c => RopeGateCase(c, true) },

            new Scenario { name = "bomb gate: one bomb clears the rubble, bedrock stays", map = m =>
                {
                    Set(m, 8, 1, 'S');
                    for (int y = 1; y <= 3; y++) { Set(m, 12, y, '%'); Set(m, 13, y, '%'); }
                    for (int y = 4; y < H - 1; y++) { Set(m, 12, y, 'X'); Set(m, 13, y, 'X'); }
                },
                run = c => BombGateCase(c) },

            new Scenario { name = "glider: gliding crosses a 6-wide thorn chasm", map = ChasmMap,
                run = c => GlideCase(c, true) },

            new Scenario { name = "glider gate: without the glider the thorns cost hearts like any spikes, never the whole run", map = ChasmMap,
                run = c => GlideCase(c, false) },

            new Scenario { name = "gliding down from high up causes no fall damage", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GlideFallCase(c) },

            new Scenario { name = "penguin slides at the player and hits a wall", map = m => { Set(m, 3, 1, 'S'); Set(m, 2, 1, '#'); Set(m, 2, 2, '#'); },
                run = c => PenguinCase(c) },

            new Scenario { name = "yeti snowball shoves and freezes the player for 0.5 s without damage", map = m => { Set(m, 3, 1, 'S'); },
                run = c => YetiCase(c) },

            new Scenario { name = "frost bat drops an ice chunk that can be thrown and shatters", map = m => { Set(m, 3, 1, 'S'); },
                run = c => FrostBatCase(c) },

            new Scenario { name = "equipment: slots, wearing and taking off; ice cleats stop the slide", map = m => { Set(m, 3, 1, 'S'); },
                run = c => EquipmentCase(c) },

            new Scenario { name = "fur coat: a snowball only slows the wearer by 30% for 1 s", map = m => { Set(m, 3, 1, 'S'); },
                run = c => FurCoatCase(c) },

            new Scenario { name = "boss: registered, damage capped per hit, defeat is reported", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BossCase(c) },

            new Scenario { name = "bosses: two stomps in a row count, a third is punished - each boss its own way", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GreedyStompCase(c) },

            new Scenario { name = "bosses: a counter next to a wall throws the player to the open side, never back onto the boss", map = m => { Set(m, 6, 1, 'S'); },
                run = c => CounterWallCase(c) },

            new Scenario { name = "bosses: the rot heart hangs from the ceiling, runs its moves, darkens the room, and is reached by a bounce mushroom",
                map = m => { for (int x = 1; x < W - 1; x++) Set(m, x, 1, '#'); Set(m, 3, 2, 'S'); },
                run = c => SporeHeartCase(c) },

            new Scenario { name = "boss test: winning shows the result screen and waits; storm hawk points along its flight", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BossTestCase(c) },

            new Scenario { name = "run plan: each run draws 4 areas by its seed, the first one too, ends with camp and vault", map = m => { Set(m, 3, 1, 'S'); },
                run = c => RunPlanCase(c) },

            new Scenario { name = "bosses: the forge brute's fire is five short-lived spots, each slam is a leap, and its charge tears through dirt in its way",
                map = m => { Set(m, 3, 1, 'S'); Set(m, 12, 1, '#'); Set(m, 12, 2, '#'); Set(m, 12, 3, '#'); },
                run = c => ForgeBruteCase(c) },

            new Scenario { name = "bosses: the yeti king's snow boulder hurts more the bigger it has grown", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BoulderCase(c) },

            new Scenario { name = "bosses: the yeti king's leap lands with a wave, his slide lies low and can be jumped, and enraged he comes back off the wall", map = m => { Set(m, 3, 1, 'S'); },
                run = c => YetiKingMovesCase(c) },
            new Scenario { name = "bosses: the yeti king runs in and punches a frozen player, once per freeze", map = m => { Set(m, 3, 1, 'S'); },
                run = c => YetiPunchCase(c) },

            new Scenario { name = "recall charm: use it to return to where the level began; it needs a rest between uses", map = m => { Set(m, 3, 1, 'S'); },
                run = c => RecallCase(c) },

            new Scenario { name = "hud: what to press shows as a tag at the right edge, spoken words in a box with a name, a sign as a card", map = m => { Set(m, 3, 1, 'S'); },
                run = c => PromptPlacesCase(c) },
            new Scenario { name = "village: people walk the street, children play tag, and a thing looked at is read like a sign", map = m => { Set(m, 3, 1, 'S'); },
                run = c => VillageLifeCase(c) },
            new Scenario { name = "field guide: the chest at the end of the tutorial hands it over, the bag opens it, and every page has its picture", map = m => { Set(m, 3, 1, 'S'); },
                run = c => CodexCase(c) },
            new Scenario { name = "quests: the elder's quest leads to his hall, he speaks before he gives, and the pouch comes on a card", map = m => { Set(m, 3, 1, 'S'); },
                run = c => QuestCase(c) },
            new Scenario { name = "story: the old signs are copied and read, the hammer's three parts make the hammer, and it strikes for three", map = m => { Set(m, 3, 1, 'S'); },
                run = c => StoryCase(c) },
            new Scenario { name = "map: with the Mapmaker's map in the bag the level is drawn as it is walked, and the sheet is up only while its button is held", map = m => { Set(m, 3, 1, 'S'); },
                run = c => MapCase(c) },
            new Scenario { name = "new wares: soft soles, hobnail boots, an iron cap, a spyglass, flares and the lost letter each do what the stall says", map = m => { Set(m, 3, 1, 'S'); },
                run = c => NewWaresCase(c) },
            new Scenario { name = "loot chest: a plain chest throws out three things, and now and then a free piece of equipment beside it", map = m => { Set(m, 3, 1, 'S'); },
                run = c => LootChestCase(c) },
            new Scenario { name = "more wares: the fire cloak, the antidote vial, the climbing gloves and the grappling hook each do what the stall says",
                map = m => { Set(m, 3, 1, 'S'); for (int y = 1; y <= 7; y++) Set(m, 16, y, 'H'); },
                run = c => MoreWaresCase(c) },
            new Scenario { name = "village rooms: the apothecary has its foreground, its little fire under the cauldron, and Maren behind the counter; every room has its foreground", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ApothecaryCase(c) },
            new Scenario { name = "room toys: the cat wakes when petted and moves to the armchair at the tenth poke, the lamps at home go out and on, Tam's wheel spins and slows, his hammock tips the player out, Maren's brew can be stirred",
                map = m => { Set(m, 3, 1, 'S'); for (int x = 4; x <= 14; x++) Set(m, x, 7, '#'); },
                run = c => RoomToysCase(c) },
            new Scenario { name = "room toys 2: bellows and anvil, globe and telescope, Pip's owl, clock and bird, Bera's lamps, the hall's candles, cup and banner, a nap in the bed at home, Tam's coil",
                map = m => { Set(m, 3, 1, 'S'); for (int x = 4; x <= 20; x++) Set(m, x, 7, '#'); },
                run = c => RoomToys2Case(c) },
            new Scenario { name = "ladder grab: standing half beside a ladder, up climbs it; a step away, up only looks up",
                map = m => { Set(m, 3, 1, 'S'); for (int y = 1; y <= 7; y++) Set(m, 10, y, 'H'); },
                run = c => LadderGrabCase(c) },
            new Scenario { name = "hearth wisp: worn, it flies at the shoulder; a death is turned away once - up with 5 health - and the wisp is gone", map = m => { Set(m, 3, 1, 'S'); },
                run = c => WispCase(c) },
            new Scenario { name = "villagers: a caged villager is freed with Enter, goes home for good, and helps every later run", map = m => { Set(m, 3, 1, 'S'); },
                run = c => VillagerCase(c) },

            new Scenario { name = "bosses: a summoned helper appears a few steps from the player, in whichever corner they stand", map = m => { Set(m, 3, 1, 'S'); },
                run = c => MinionSpotCase(c) },

            new Scenario { name = "all five bosses run their attack patterns", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BossPatternsCase(c) },

            new Scenario { name = "treasure chest lays out three free items", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ChestCase(c) },

            new Scenario { name = "shop: equipment sells once (no owned pieces, no twins on a shelf, gone once owned)", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ShopOnceCase(c) },

            new Scenario { name = "boss minions pay no gold or hearts; ordinary enemies pay a heart or gold, never both", map = m => { Set(m, 3, 1, 'S'); },
                run = c => MinionDropCase(c) },

            new Scenario { name = "max health: hearts never overfill; a boss drops a heart vessel (+1 max, full heal) and gold", map = m => { Set(m, 3, 1, 'S'); },
                run = c => MaxHealthCase(c) },

            new Scenario { name = "enemies only hurt while attacking: touching is safe, the snake's bite lands after its wind-up", map = m => { Set(m, 3, 1, 'S'); },
                run = c => AttackOnlyCase(c) },

            new Scenario { name = "boss shockwave runs along the floor: it hits a standing player for 2, a jump clears it", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ShockwaveCase(c) },

            new Scenario { name = "shopkeeper grudge: robbed -> ambush with two brothers; killed -> no more shopkeepers", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GrudgeCase(c) },

            new Scenario { name = "climb traps: rotten platform gives way and comes back; rock dropper drops on a climber",
                map = m => { Set(m, 3, 1, 'S'); for (int y = 1; y <= 6; y++) Set(m, 16, y, 'H'); for (int x = 14; x <= 18; x++) Set(m, x, 8, '#'); },
                run = c => ClimbTrapCase(c) },

            new Scenario { name = "area title card and event banner never show at the same time", map = m => { Set(m, 3, 1, 'S'); },
                run = c => TitleCardCase(c) },

            new Scenario { name = "keyboard keys can be changed: the new key works, a key in use swaps, defaults come back", map = m => { Set(m, 3, 1, 'S'); },
                run = c => KeyChangeCase(c) },

            new Scenario { name = "long fall onto a bounce mushroom: thrown high, no fall damage, higher with jump held", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BounceCapCase(c) },

            new Scenario { name = "puffshroom swells and puffs: spores swap left and right, a spore mask prevents it", map = m => { Set(m, 3, 1, 'S'); },
                run = c => PuffshroomCase(c) },

            new Scenario { name = "gloom moth drinks the player's light, killing it brings the light back", map = m => { Set(m, 3, 1, 'S'); },
                run = c => MothCase(c) },

            new Scenario { name = "lantern makes the player's light reach further, and takes nothing to wear", map = m => { Set(m, 3, 1, 'S'); },
                run = c => LanternCase(c) },

            new Scenario { name = "lurecap looks like a glowing plant until the player walks up, then leaps", map = m => { Set(m, 3, 1, 'S'); },
                run = c => LurecapCase(c) },

            new Scenario { name = "gearworks: a conveyor carries the player and creatures, heavy boots ignore it, it can be reversed", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ConveyorCase(c) },

            new Scenario { name = "gearworks: a sentry's shield stops hits from the front, not from behind; the magnet charm cuts through", map = m => { Set(m, 3, 1, 'S'); },
                run = c => SentryCase(c) },

            new Scenario { name = "gearworks: a drone warns, then fires a bolt; a scrap beetle eats a lit bomb and blows up", map = m => { Set(m, 3, 1, 'S'); },
                run = c => DroneBeetleCase(c) },

            new Scenario { name = "geode: a struck crystal rings and bursts into shards that cut the player and creatures alike, chain on, and grow back; a light lens burns on its beat", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GeodeTrapCase(c) },

            new Scenario { name = "geode: a shardling rushes and comes apart in two, a glint aims a line and fires along it, a mimic lies as a gem, a pot or a crate until someone comes close", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GeodeEnemyCase(c) },

            new Scenario { name = "bosses: the facet is a colossus on the back wall and its two fists are the fight - they slam and sweep, their shell turns the blade, a fist that lands on a crystal is cracked open; its light is thrown back by crystal as by a mirror, a sweeping fist smashes crystals and drives their shards on ahead", map = m => { Set(m, 3, 1, 'S'); },
                run = c => FacetCase(c) },

            new Scenario { name = "sandsunk: quicksand slows and swallows unless hopped out of or crossed in sand striders; a spear plate strikes on its beat", map = m => { Set(m, 3, 1, 'S'); },
                run = c => SandTrapCase(c) },

            new Scenario { name = "sandsunk: scarabs pounce as a pack, a mummy casts its wrapping and takes gold, a sand worm comes up to spit", map = m => { Set(m, 3, 1, 'S'); },
                run = c => SandEnemyCase(c) },

            new Scenario { name = "bosses: the dune queen floods her floor with quicksand, dives and breaches where the player stands, breaks the slab above her, spits and calls scarabs",
                map = m => { Set(m, 3, 1, 'S'); },
                run = c => DuneQueenCase(c) },

            new Scenario { name = "gearworks: a saw blade runs along its rail and cuts the player and creatures; it can be jumped", map = m => { Set(m, 3, 1, 'S'); },
                run = c => SawCase(c) },

            new Scenario { name = "bosses: the broodmother drops from the ceiling and gets stuck, lays web that slows, and eggs that hatch unless cut", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BroodmotherCase(c) },

            new Scenario { name = "bosses: the cogwyrm runs the floor, dives and sticks, spits bolts, rolls as a wheel, sheds segments as saws, and only its head can be hurt", map = m => { Set(m, 3, 1, 'S'); },
                run = c => GearWyrmCase(c) },

            new Scenario { name = "bosses: the eclipse king is a shell of stone that swipes, charges and sticks, leaps - then breaks, and the light inside fights on with a column of light no ledge stops, its two hands, a burst, and vanishing - and drops the Sunstone", map = m => { Set(m, 3, 1, 'S'); for (int x = 7; x <= 9; x++) Set(m, x, 3, '='); },
                run = c => EclipseKingCase(c) },

            new Scenario { name = "hawk dives at the player below it", map = m => { Set(m, 3, 1, 'S'); },
                run = c => HawkCase(c) },

            new Scenario { name = "ledge grab and climb (real controller)", map = m => { Set(m, 6, 1, 'S'); for (int x = 10; x < 15; x++) for (int y = 1; y <= 3; y++) Set(m, x, y, '#'); },
                run = c => Run(c, Seq((12, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (40, F(x: 1, jh: true)), (5, F()), (30, F(up: true)), (20, F())), 120,
                    () => c.Expect(Mathf.Abs(c.p.pos.y - 4f) < 0.01f && c.p.pos.x > 10f, $"standing on the ledge (pos {c.p.pos})")) },

            new Scenario { name = "ladder climb to the top", map = m => { Set(m, 4, 1, 'S'); for (int y = 1; y <= 5; y++) { Set(m, 8, y, 'H'); for (int x = 9; x < 14; x++) Set(m, x, y, '#'); } },
                run = c => LadderCase(c) },

            new Scenario { name = "jumping off a ladder under the ceiling stays inside the map", map = m =>
                {
                    Set(m, 4, 1, 'S');
                    for (int y = 1; y <= 5; y++) Set(m, 8, y, 'H');
                    for (int x = 5; x <= 12; x++) { Set(m, x, 6, '#'); Set(m, x, 7, '#'); Set(m, x, 8, 'X'); }
                },
                run = c => LadderCeilingCase(c) },

            new Scenario { name = "buy a bomb bag in a shop", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ShopCase(c, false) },

            new Scenario { name = "shopkeeper: 2 warnings without damage, 3rd hit angers him, items free", map = m => { Set(m, 3, 1, 'S'); },
                run = c => ShopCase(c, true) },

            new Scenario { name = "crusher hits the player for 3", map = m => { Set(m, 3, 1, 'S'); Set(m, 10, 5, '#'); Set(m, 10, 4, 'w'); },
                run = c => CrusherCase(c) },

            new Scenario { name = "flame vent burns the player and lights a bomb", map = m => { Set(m, 3, 1, 'S'); Set(m, 8, 0, 'f'); Set(m, 18, 0, 'f'); },
                run = c => VentCase(c) },

            new Scenario { name = "a heart pickup heals 1", map = m => { Set(m, 3, 1, 'S'); },
                run = c => HeartCase(c) },

            new Scenario { name = "bags: five bombs and five ropes can be carried, what does not fit stays; the elder lets a bag out by five for embers, dearer each time, three times", map = m => { Set(m, 3, 1, 'S'); },
                run = c => BagsCase(c) },

            new Scenario { name = "slingshot: carried from the start, it shoots pebbles that pots and crates give up; ten can be carried, twenty with the pouch", map = m => { Set(m, 3, 1, 'S'); },
                run = c => SlingshotCase(c) },

            new Scenario { name = "collectibles glow, pickups bob, hint arrow near a rock", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 'o'); Set(m, 8, 1, 'u'); Set(m, 10, 1, 'c'); },
                run = c => HighlightCase(c) },

            new Scenario { name = "waypoint: rides the screen edge, then floats above the exit", map = m => { Set(m, 3, 1, 'S'); Set(m, 60, 1, 'G'); },
                run = c => WaypointCase(c) },

            new Scenario { name = "whip breaks a pot", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 'u'); },
                run = c => Run(c, Seq((2, F()), (1, F(atk: true)), (40, F())), 45, () => c.Expect(c.Count<Breakable>() == 0, "pot broken")) },

            new Scenario { name = "long fall onto an enemy's head: no fall damage, the kill pays gold", map = m => { Set(m, 3, 1, 'S'); },
                run = c => StompFallCase(c) },

            new Scenario { name = "long fall hurts and stuns", map = m => { Set(m, 3, 1, 'S'); },
                run = c => FallCase(c) },

            new Scenario { name = "pause menu freezes the game", map = m => { Set(m, 3, 1, 'S'); Set(m, 9, 1, 's'); },
                run = c => PauseCase(c, false) },

            new Scenario { name = "settings panel opens from pause", map = m => { Set(m, 3, 1, 'S'); },
                run = c => PauseCase(c, true) },

            new Scenario { name = "results screen after death", map = m => { Set(m, 3, 1, 'S'); Set(m, 12, 1, '^'); },
                run = c => ResultsCase(c) },

            new Scenario { name = "the Reaper (time pressure) spawns and its touch is fatal", map = m => { Set(m, 3, 1, 'S'); },
                run = c => WraithCase(c) },

            new Scenario { name = "carrying a golden idol out pays gold", map = m => { Set(m, 3, 1, 'S'); Set(m, 4, 1, 'G'); },
                run = c => IdolCase(c) },

            new Scenario { name = "a golden idol can be sold to a shopkeeper on the spot; an angry one buys nothing", map = m => { Set(m, 3, 1, 'S'); },
                run = c => IdolSaleCase(c) },

            new Scenario { name = "exit door ends the level", map = m => { Set(m, 3, 1, 'S'); Set(m, 5, 1, 'G'); },
                run = c => Run(c, Seq((16, F(x: 1)), (20, F()), (1, F(act: true)), (10, F())), 50, () => c.Expect(c.dir.Ended, "level ended")) },
        };

        // --------------------------------------------------------------------------------------- cases

        static IEnumerator Run(Ctx c, List<FrameInput> inputs, int frames, Action check)
        {
            c.Bot(inputs);
            yield return c.Frames(frames);
            check();
        }

        static IEnumerator PatrolCase(Ctx c)
        {
            Snake single = null, pair = null;
            foreach (var e in c.world.Entities)
                if (e is Snake s) { if (s.pos.x < 12f) single = s; else pair = s; }
            c.Expect(single != null && pair != null, "two snakes spawned");
            if (single == null || pair == null) yield break;
            c.Bot(Seq((240, F())));
            int flips = 0, last = single.facing;
            float minX = pair.pos.x, maxX = pair.pos.x, startX = single.pos.x;
            for (int i = 0; i < 240; i++)
            {
                yield return c.Frames(1);
                if (single.facing != last) { flips++; last = single.facing; }
                minX = Mathf.Min(minX, pair.pos.x); maxX = Mathf.Max(maxX, pair.pos.x);
            }
            c.Expect(flips <= 1, $"single-tile snake turned {flips} times in 240 steps");
            c.Expect(Mathf.Abs(single.pos.x - startX) < 0.05f && !single.Dead, "single-tile snake stays on its tile");
            c.Expect(single.facing == -1, "it faces the player (to its left)");
            c.Expect(maxX - minX > 0.5f && !pair.Dead, $"snake on two tiles still patrols ({maxX - minX:0.00} tiles)");
        }

        static IEnumerator StompCase(Ctx c)
        {
            c.Teleport(12.5f, 5f);
            c.Bot(Seq((90, F())));
            yield return c.Frames(90);
            c.Expect(c.Count<Snake>() == 0, "snake dead");
            c.Expect(c.run.health >= 4, "player unhurt");   // 5 when the snake happened to drop a heart
        }

        static IEnumerator ChainCase(Ctx c)
        {
            for (int i = 0; i < 6; i++) c.Spawn<Bomb>("bomb", 8 + i, 1);
            for (int i = 0; i < 30; i++) c.Spawn<Bomb>("bomb", 18 + (i % 3), 1 + i / 10);
            yield return c.Frames(2);
            var lit = c.Spawn<Bomb>("bomb", 7, 1);
            lit.Light(null, 0f);
            var lit2 = c.Spawn<Bomb>("bomb", 17, 1);
            lit2.Light(null, 0f);
            c.Bot(Seq((400, F())));
            float t0 = Time.realtimeSinceStartup;
            yield return c.Frames(400);
            c.Expect(c.Count<Bomb>() == 0, "all bombs gone");
            c.Expect(c.world.ExplosionsThisLevel >= 7 && c.world.ExplosionsThisLevel <= 38, $"explosions {c.world.ExplosionsThisLevel}");
            c.Expect(Time.realtimeSinceStartup - t0 < 30f, "finished in time");
        }

        static IEnumerator SpikeCase(Ctx c)
        {
            c.Bot(Seq((600, F())));
            c.Teleport(12.5f, 4f);
            int h0 = c.run.health;
            float top = 0f;
            for (int i = 0; i < 60; i++) { yield return new WaitForFixedUpdate(); if (c.run.health < h0) top = Mathf.Max(top, c.p.pos.y); }
            c.Expect(!c.p.IsDead && h0 - c.run.health == c.p.cfg.spikeDamage, $"spikes cost {c.p.cfg.spikeDamage} hearts, not the whole run (health {h0} -> {c.run.health})");
            c.Expect(top > 1.8f, $"the player is thrown back up off the spikes (peak {top:0.00})");
            yield return c.Frames(120);
            c.Expect(!c.p.IsDead && c.run.health == h0 - c.p.cfg.spikeDamage, "one fall costs only one hit");
            // at 2 hearts or fewer the same fall is fatal
            c.run.health = 2;
            c.Teleport(12.5f, 4f);
            yield return c.Frames(60);
            c.Expect(c.p.IsDead, "player died");
            c.Expect(c.p.DeathCause != null && c.p.DeathCause.Contains("spikes"), "cause: " + c.p.DeathCause);
        }

        static IEnumerator LadderCase(Ctx c)
        {
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((3, F()), (150, F(up: true)), (10, F())));
            yield return c.Frames(165);
            c.Expect(c.p.grounded && Mathf.Abs(c.p.pos.y - 6f) < 0.01f, $"on top of ladder (pos {c.p.pos})");
        }

        static IEnumerator LadderCeilingCase(Ctx c)
        {
            c.Teleport(8.5f, 1f);
            var seq = Seq((3, F()), (100, F(up: true)));
            for (int i = 0; i < 4; i++) { seq.AddRange(Seq((1, F(jp: true, jh: true)), (20, F(jh: true)), (25, F()), (40, F(up: true)))); }
            seq.AddRange(Seq((1, F(x: 1, jp: true, jh: true)), (30, F(x: 1, jh: true)), (30, F())));
            c.Bot(seq);
            float maxHead = 0f; bool inside = false;
            var hw = c.p.size.x * 0.5f;
            for (int f = 0; f < seq.Count + 5; f++)
            {
                yield return new WaitForFixedUpdate();
                maxHead = Mathf.Max(maxHead, c.p.pos.y + c.p.size.y);
                if (BodyPhysics.OverlapsSolid(c.world.Grid, c.p.pos, hw, c.p.size.y)) inside = true;
            }
            c.Expect(!inside, "never inside a solid tile");
            c.Expect(maxHead <= 6.01f, $"head never above the ceiling (max {maxHead:0.00})");
            c.Expect(c.p.pos.y < 6f, $"still below the ceiling (pos {c.p.pos})");
        }

        static IEnumerator ShopCase(Ctx c, bool angry)
        {
            c.run.gold = 1000;
            c.run.bombs = 2;        // (room for the three in the bag: five can be carried)
            var keeper = c.Spawn<Shopkeeper>("shopkeeper", 12, 1, 0, null, -1);
            var item = c.Spawn<ShopItem>("shopitem", 6, 1, 0, "bombs");
            yield return c.Frames(2);
            keeper.AddWare(item);
            yield return c.Frames(2);
            c.Expect(item.priceTag.Text == "$350", $"calm keeper: price tag shows the price (got '{item.priceTag.Text}')");
            if (angry)
            {
                c.Teleport(11.3f, 1f);
                c.Bot(Seq((2, F(x: 1)), (2, F()), (1, F(atk: true)), (50, F()), (1, F(atk: true)), (50, F()), (1, F(atk: true)), (30, F())));
                int max = keeper.maxHealth;
                yield return c.Frames(30);
                c.Expect(keeper.Strikes == 1 && !keeper.angry && keeper.health == max, $"hit 1: warning only (strikes {keeper.Strikes}, hp {keeper.health}/{max}, angry {keeper.angry})");
                yield return c.Frames(51);
                c.Expect(keeper.Strikes == 2 && !keeper.angry && keeper.health == max, $"hit 2: last warning only (strikes {keeper.Strikes}, hp {keeper.health}/{max}, angry {keeper.angry})");
                c.Expect(c.run.health == 4, "calm keeper never hurts the player");
                yield return c.Frames(51);
                c.Expect(keeper.angry && keeper.health == max - 1, $"hit 3: angry and damaged (hp {keeper.health}/{max}, angry {keeper.angry})");
                c.Expect(item.priceTag.Text == "FREE", $"angry keeper: tag shows FREE (got '{item.priceTag.Text}')");
            }
            c.Teleport(6.5f, 1f);
            c.p.godMode = angry;   // the angry keeper would knock us around
            c.Bot(Seq((5, F()), (1, F(act: true)), (5, F())));
            yield return c.Frames(12);
            c.Expect(c.run.bombs == 5, $"bombs 5 (got {c.run.bombs})");
            c.Expect(c.run.gold == (angry ? 1000 : 650), $"gold {c.run.gold}");
            c.Expect(c.Count<ShopItem>() == 0, "item taken");
        }

        static IEnumerator CrusherCase(Ctx c)
        {
            c.Teleport(10.5f, 1f);
            c.Bot(Seq((120, F())));
            yield return c.Frames(120);
            c.Expect(c.run.health == 1, $"health 1 (got {c.run.health})");
        }

        static IEnumerator VentCase(Ctx c)
        {
            c.Spawn<Bomb>("bomb", 18, 1);
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((360, F())));
            yield return c.Frames(360);
            c.Expect(c.run.health < 4, $"burned (health {c.run.health})");
            c.Expect(c.world.ExplosionsThisLevel >= 1, "bomb on vent exploded");
        }

        static IEnumerator PauseCase(Ctx c, bool settings)
        {
            c.Bot(Seq((400, F())));
            yield return c.Frames(10);
            c.dir.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.1f);
            int f0 = c.world.Frame;
            yield return new WaitForSecondsRealtime(0.5f);
            c.Expect(c.world.Frame == f0, "simulation frozen while paused");
            c.Expect(c.dir.pauseMenu.root.activeSelf, "pause menu visible");
            if (settings)
            {
                c.dir.pauseMenu.settingsButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.3f);
                c.Expect(c.dir.pauseMenu.settingsPanel.gameObject.activeSelf, "settings visible");
                float before = SaveSystem.Settings.musicVolume;
                c.dir.pauseMenu.settingsPanel.music.value = 0.25f;
                c.Expect(Mathf.Abs(SaveSystem.Settings.musicVolume - 0.25f) < 0.001f, "music volume saved");
                c.dir.pauseMenu.settingsPanel.music.value = before;

                // a mouse click on empty space clears the selection: the menu takes back the entry it had, so a pad still has one
                var es = UnityEngine.EventSystems.EventSystem.current;
                var focus = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null);
                yield return null;
                yield return null;
                c.Expect(focus != null && es.currentSelectedGameObject == focus, $"after the selection is cleared (a stray click) the menu chooses its entry again ({(focus != null ? focus.name : "none")} -> {(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "none")})");

                // a gamepad's back button (real presses on a virtual pad): out of the open panel, then out of the pause
                var pm = c.dir.pauseMenu;
                var prevBg = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                var pad = InputSystem.AddDevice<Gamepad>("ScenarioBackPad");
                var none = new GamepadState();
                var east = new GamepadState().WithButton(GamepadButton.East);
                yield return Send(pad, none, 0.1f);
                yield return Send(pad, east, 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(pm.mainPanel.activeSelf && !pm.settingsPanel.gameObject.activeSelf && c.dir.Paused, "the pad's back button leaves Settings for the pause screen");
                c.run.isTutorial = false;
                pm.Show(c.run);
                pm.giveUpButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Send(pad, east, 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(pm.mainPanel.activeSelf && !pm.confirmPanel.activeSelf && c.dir.Paused && !c.dir.Ended, "it turns down the \"give up?\" question");
                c.run.bossPractice = true;
                pm.Show(c.run);
                pm.bossListButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                yield return Send(pad, east, 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(pm.mainPanel.activeSelf && !pm.bossPanel.activeSelf && c.dir.Paused, "it closes the boss list");
                c.run.bossPractice = false;
                c.run.isTutorial = true;
                yield return Send(pad, east, 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(!c.dir.Paused && !pm.root.activeSelf, "and on the pause screen itself it goes back into the game");
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.Select), 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(c.dir.Paused && pm.root.activeSelf && (c.dir.inventory == null || !c.dir.inventory.IsOpen), "the pad's Select button opens the pause screen (not the bag)");
                c.dir.SetPaused(false);
                // which face button says "yes": the bottom one on an ordinary pad ...
                var south = new GamepadState().WithButton(GamepadButton.South);
                c.dir.SetPaused(true);
                yield return new WaitForSecondsRealtime(0.1f);
                UIUtil.Select(pm.settingsButton);
                yield return Send(pad, south, 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(pm.settingsPanel.gameObject.activeSelf, "an ordinary pad's bottom face button confirms the entry in focus");
                InputSystem.RemoveDevice(pad);
                // ... and on a Nintendo pad the right-hand one, A; its bottom one, B, goes back (real presses on a virtual Pro Controller)
                var sw = InputSystem.AddDevice<UnityEngine.InputSystem.Switch.SwitchProControllerHID>("ScenarioSwitchPad");
                IEnumerator Press(UnityEngine.InputSystem.Controls.ButtonControl button)
                {
                    using (UnityEngine.InputSystem.LowLevel.StateEvent.From(sw, out var down)) { button.WriteValueIntoEvent(1f, down); InputSystem.QueueEvent(down); }
                    yield return new WaitForSecondsRealtime(0.15f);
                    using (UnityEngine.InputSystem.LowLevel.StateEvent.From(sw, out var up)) { button.WriteValueIntoEvent(0f, up); InputSystem.QueueEvent(up); }
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                yield return Press(sw.buttonSouth);
                c.Expect(pm.mainPanel.activeSelf && !pm.settingsPanel.gameObject.activeSelf && c.dir.Paused, "a Nintendo pad's B (the bottom button) goes back: out of Settings");
                UIUtil.Select(pm.settingsButton);
                yield return Press(sw.buttonSouth);
                c.Expect(!pm.settingsPanel.gameObject.activeSelf, "its B does not confirm");
                if (!c.dir.Paused) { c.dir.SetPaused(true); yield return new WaitForSecondsRealtime(0.1f); }
                UIUtil.Select(pm.settingsButton);
                yield return Press(sw.buttonEast);
                c.Expect(pm.settingsPanel.gameObject.activeSelf && c.dir.Paused, "its A (the right-hand button) confirms: Settings opens");
                yield return Press(sw.buttonSouth);
                yield return Press(sw.buttonSouth);
                c.Expect(!c.dir.Paused, "B, B: out of Settings and back into the game");
                InputSystem.RemoveDevice(sw);
                // a pad the Input System has no name for comes in as a plain joystick: it must play the game too
                var joy = InputSystem.AddDevice<Joystick>("ScenarioPlainPad");
                var jmap = c.dir.input.actions.FindActionMap("Player", true);
                InputAction jMove = jmap.FindAction("Move"), jAttack = jmap.FindAction("Attack");
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(joy, out var tilt)) { joy.stick.WriteValueIntoEvent(new Vector2(1f, 0f), tilt); joy.trigger.WriteValueIntoEvent(1f, tilt); InputSystem.QueueEvent(tilt); }
                yield return new WaitForSecondsRealtime(0.15f);
                c.Expect(jMove.ReadValue<Vector2>().x > 0.5f && jAttack.IsPressed(), $"a plain joystick moves the player and swings the blade (move {jMove.ReadValue<Vector2>().x:0.0}, blade {jAttack.IsPressed()})");
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(joy, out var rest)) { joy.stick.WriteValueIntoEvent(Vector2.zero, rest); joy.trigger.WriteValueIntoEvent(0f, rest); InputSystem.QueueEvent(rest); }
                yield return new WaitForSecondsRealtime(0.1f);
                c.dir.SetPaused(true);
                yield return new WaitForSecondsRealtime(0.1f);
                UIUtil.Select(pm.settingsButton);
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(joy, out var yes)) { joy.trigger.WriteValueIntoEvent(1f, yes); InputSystem.QueueEvent(yes); }
                yield return new WaitForSecondsRealtime(0.15f);
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(joy, out var off)) { joy.trigger.WriteValueIntoEvent(0f, off); InputSystem.QueueEvent(off); }
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(pm.settingsPanel.gameObject.activeSelf, "and confirms the entry in focus in a menu");
                c.dir.SetPaused(false);
                InputSystem.RemoveDevice(joy);
                InputSystem.settings.backgroundBehavior = prevBg;
            }
            else
            {
                // what the pause screen offers depends on the place (scenarios run as the tutorial: make it a run on the mountain)
                var pm = c.dir.pauseMenu;
                c.run.isTutorial = false;
                pm.Show(c.run);
                yield return new WaitForSecondsRealtime(0.15f);
                c.Expect(c.dir.CanGiveUp && pm.giveUpButton.gameObject.activeSelf && !pm.fightAgainButton.gameObject.activeSelf && !pm.bossListButton.gameObject.activeSelf,
                    "on the mountain: Give Up is offered, the Challenge buttons are not");
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "pause_mountain.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                pm.giveUpButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.15f);
                c.Expect(pm.confirmPanel.activeSelf && !pm.mainPanel.activeSelf && !c.dir.Ended && c.dir.Paused, "Give Up asks first: nothing has ended yet");
                var asked = UIOverlap.Find();
                c.Expect(asked.Count == 0, "the question is laid out cleanly" + (asked.Count > 0 ? " (" + asked[0] + ")" : ""));
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "pause_giveup_confirm.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                c.Expect(pm.HandleBack() && pm.mainPanel.activeSelf && !pm.confirmPanel.activeSelf, "Esc on the question goes back to the pause screen");
                pm.giveUpButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                pm.confirmNoButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(pm.mainPanel.activeSelf && !pm.confirmPanel.activeSelf && c.dir.Paused && !c.dir.Ended, "Keep Going: back on the pause screen, the run goes on");

                c.run.bossPractice = true;          // a Challenge fight
                pm.Show(c.run);
                yield return new WaitForSecondsRealtime(0.15f);
                c.Expect(!c.dir.CanGiveUp && !pm.giveUpButton.gameObject.activeSelf && pm.fightAgainButton.gameObject.activeSelf && pm.bossListButton.gameObject.activeSelf,
                    "in a Challenge fight: Fight Again and Boss List, no Give Up");
                var five = UIOverlap.Find();
                c.Expect(five.Count == 0, "five buttons fit the panel" + (five.Count > 0 ? " (" + five[0] + ")" : ""));
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "pause_challenge.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                // Boss List opens the list here, in the game: nothing is left, and Back returns to the pause screen
                var sceneBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                pm.bossListButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.3f);
                int rows = 0;
                if (pm.bossButtons != null) foreach (var bb in pm.bossButtons) if (bb != null && bb.gameObject.activeInHierarchy) rows++;
                c.Expect(pm.bossPanel != null && pm.bossPanel.activeSelf && !pm.mainPanel.activeSelf && c.dir.Paused && !c.dir.Ended && rows >= 9
                         && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneBefore && RunSession.Current == c.run,
                    $"Boss List opens the list of bosses inside the pause screen ({rows} bosses): still in the game, the fight still there");
                var listed = UIOverlap.Find();
                c.Expect(listed.Count == 0, "the boss list is laid out cleanly" + (listed.Count > 0 ? " (" + listed[0] + ")" : ""));
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "pause_boss_list.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                pm.bossBack.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(pm.mainPanel.activeSelf && !pm.bossPanel.activeSelf && c.dir.Paused, "Back returns to the pause screen of the same fight");
                pm.bossListButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(pm.HandleBack() && pm.mainPanel.activeSelf && !pm.bossPanel.activeSelf, "and so does Esc");
                c.run.bossPractice = false;

                c.run.inVillage = true;             // at home there is nothing to give up
                pm.Show(c.run);
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(!pm.giveUpButton.gameObject.activeSelf && !pm.fightAgainButton.gameObject.activeSelf && !pm.bossListButton.gameObject.activeSelf,
                    "in the village: only Resume, Settings and Main Menu");
                c.run.inVillage = false;
                c.run.isTutorial = true;
                pm.Show(c.run);
            }
        }

        static IEnumerator ResultsCase(Ctx c)
        {
            c.run.gold = 1234; c.run.kills = 3; c.run.reachedStageName = "1-2";
            c.run.health = 1;
            c.Teleport(12.5f, 4f);
            yield return c.Frames(40);
            c.Expect(c.p.IsDead, "died");
            c.dir.hud.ShowHurt(c.p.DeathCause, 99);
            yield return new WaitForSecondsRealtime(0.3f);
            c.run.deathCause = c.p.DeathCause;
            c.dir.results.ShowRunEnd(c.run, false, "NEW FASTEST WIN!", 1);
            yield return new WaitForSecondsRealtime(0.3f);
            c.Expect(c.dir.results.root.activeSelf, "results visible");
            var rs = c.dir.results;
            var back = rs.menuButton != null ? rs.menuButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            c.Expect(!rs.retrySeedButton.gameObject.activeSelf && rs.newRunLabel.text == "NEW RUN" && back != null && back.text == "BACK TO VILLAGE",
                "after a death there are two ways on: a new run, or back to the village");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "results_death.png"));
            yield return c.Frames(3);
        }

        static IEnumerator WraithCase(Ctx c)
        {
            c.dir.SpawnWraith();
            c.Expect(c.dir.WraithActive, "wraith announced");
            c.Bot(Seq((900, F())));
            for (int i = 0; i < 900 && !c.p.IsDead; i++) yield return new WaitForFixedUpdate();
            c.Expect(c.p.IsDead, "player caught");
            c.Expect(c.p.DeathCause != null && c.p.DeathCause.Contains("Reaper"), "cause: " + c.p.DeathCause);
        }

        static IEnumerator IdolSaleCase(Ctx c)
        {
            c.p.godMode = true;
            var keeper = c.Spawn<Shopkeeper>("shopkeeper", 8, 1);
            var idol = c.Spawn<Treasure>("idol", 3, 1);
            c.Expect(keeper != null && idol != null, "a shopkeeper and an idol spawned");
            if (keeper == null || idol == null) yield break;
            int worth = idol.value, gold0 = c.run.gold;
            yield return c.Frames(4);
            c.Teleport(keeper.pos.x, 1f);
            yield return c.Frames(4);
            c.Expect(keeper.GetPrompt(c.p) == null, "empty-handed he has nothing to buy");
            // pick the idol up, carry it to him, press the button
            c.Teleport(3.5f, 1f);
            c.Bot(Seq((3, F()), (1, F(down: true, atk: true)), (8, F())));
            yield return c.Frames(14);
            c.Expect(c.p.held == idol, "the idol is in hand");
            c.Teleport(keeper.pos.x, 1f);
            yield return c.Frames(6);
            string offer = keeper.GetPrompt(c.p);
            c.Expect(offer != null && offer.StartsWith("[enter]  Sell") && offer.Contains("$" + worth) && c.p.Prompt == offer, $"with the idol in hand he offers to buy it ({offer})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "idol_sale.png"));
            yield return c.Frames(3);
            c.Bot(Seq((2, F()), (1, F(act: true)), (30, F())));
            yield return c.Frames(12);
            c.Expect(c.p.held == null && idol.Dead && c.run.gold == gold0 + worth && !keeper.angry, $"sold: the idol is gone, {worth} gold paid, and he is still at peace (gold {gold0} -> {c.run.gold})");
            c.Expect(c.dir.hud.FeedAmount("sold:Golden Idol") == worth, "the sale shows in the list at the left");
            // an angry shopkeeper buys nothing
            var second = c.Spawn<Treasure>("idol", 3, 1);
            yield return c.Frames(4);
            c.Teleport(3.5f, 1f);
            c.Bot(Seq((3, F()), (1, F(down: true, atk: true)), (8, F())));
            yield return c.Frames(14);
            keeper.angry = true;
            c.Teleport(keeper.pos.x, 1f);
            yield return c.Frames(4);
            c.Expect(c.p.held == second && keeper.GetPrompt(c.p) == null, "an angry shopkeeper offers nothing");
            keeper.Despawn();
            if (second != null) second.Despawn();
            c.Bot(Seq((3000, F())));
        }

        static IEnumerator IdolCase(Ctx c)
        {
            c.Spawn<Treasure>("idol", 3, 1);
            int gold0 = c.run.gold;
            c.Bot(Seq((3, F()), (1, F(down: true, atk: true)), (5, F()), (10, F(x: 1)), (20, F()), (1, F(act: true)), (10, F())));
            yield return c.Frames(55);
            c.Expect(c.dir.Ended, "level ended");
            int idolWorth = c.world.config.prefabs.Get("idol") is Treasure idolPrefab ? idolPrefab.value : -1;
            c.Expect(idolWorth > 0 && c.run.gold == gold0 + idolWorth, $"idol paid what it is worth ({idolWorth}; gold {c.run.gold})");
        }

        static RopeProjectile FindRope(Ctx c)
        {
            foreach (var e in c.world.Entities) if (e is RopeProjectile r && !r.Dead) return r;
            return null;
        }

        static IEnumerator RopeAirCase(Ctx c)
        {
            c.run.Equip("rope");
            c.Bot(Seq((2, F()), (1, F(rope: true)), (150, F())));
            yield return c.Frames(40);
            var r = FindRope(c);
            c.Expect(r == null || !r.Hooked, "rope did not hook in mid-air");
            yield return c.Frames(110);
            bool anyRope = false;
            for (int y = 0; y < c.world.Grid.Height; y++) if (c.world.Grid.HasRope(10, y)) anyRope = true;
            c.Expect(!anyRope, "no climbable rope cells");
            c.Expect(c.Count<RopeProjectile>() == 0, "rope projectile gone");
            c.Expect(c.run.ropes == 4 || c.Count<Pickup>() == 1, $"rope recoverable (ropes {c.run.ropes}, pickups {c.Count<Pickup>()})");
        }

        static IEnumerator RopeLifeCase(Ctx c, bool climb)
        {
            c.run.Equip("rope");
            c.Bot(Seq((2, F()), (1, F(rope: true)), (40, F()), (climb ? 30 : 0, F(up: true)), (200, F())));
            yield return c.Frames(43);
            var r = FindRope(c);
            c.Expect(r != null && r.Hooked, "rope hooked under the ceiling");
            if (r == null) yield break;
            if (climb) yield return c.Frames(30);
            r.SetLife(0.4f);
            yield return c.Frames(150);
            bool exists = FindRope(c) != null;
            if (climb) c.Expect(exists && c.p.motor.mode == MotorMode.Climb, "rope kept while climbed");
            else
            {
                c.Expect(!exists, "rope vanished");
                c.Expect(!c.world.Grid.HasRope(5, 3), "rope cells removed");
            }
        }

        static void ChasmMap(char[,] m)
        {
            Set(m, 4, 2, 'S');
            for (int x = 1; x <= 6; x++) Set(m, x, 1, '#');
            for (int x = 7; x <= 12; x++) Set(m, x, 1, '^');
            for (int x = 13; x < W - 1; x++) Set(m, x, 1, '#');
        }

        static IEnumerator GlideCase(Ctx c, bool glider)
        {
            int h0 = c.run.health;
            if (!c.run.gear.Contains("glider")) c.run.gear.Add("glider");
            c.run.Equip(glider ? "glider" : "bomb");
            var seq = Seq((6, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (140, F(x: 1, jh: true)), (40, F()));
            c.Bot(seq);
            yield return c.Frames(seq.Count + 5);
            if (glider) c.Expect(!c.p.IsDead && c.p.pos.x > 13f && Mathf.Abs(c.p.pos.y - 2f) < 0.01f, $"crossed (pos {c.p.pos}, dead {c.p.IsDead})");
            else c.Expect(!c.p.IsDead && c.run.health < h0 && (h0 - c.run.health) % c.p.cfg.spikeDamage == 0, $"the thorns wound and do not kill (health {h0} -> {c.run.health}, dead {c.p.IsDead})");
        }

        static IEnumerator GlideFallCase(Ctx c)
        {
            if (!c.run.gear.Contains("glider")) c.run.gear.Add("glider");
            c.run.Equip("glider");
            c.Teleport(12.5f, 12.5f);
            c.Bot(Seq((400, F(jh: true))));
            bool stunned = false, glided = false;
            for (int i = 0; i < 400; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.IsStunned) stunned = true;
                if (c.p.motor.gliding) glided = true;
            }
            c.Expect(glided, "glided");
            c.Expect(c.run.health == 4 && !stunned && Mathf.Abs(c.p.pos.y - 1f) < 0.01f, $"landed safely (health {c.run.health}, stunned {stunned}, pos {c.p.pos})");
        }

        static IEnumerator PenguinCase(Ctx c)
        {
            c.Teleport(5.5f, 1f);
            var pg = c.Spawn<Penguin>("penguin", 12, 1);
            c.Expect(pg != null, "penguin spawned");
            if (pg == null) yield break;
            c.Bot(Seq((400, F())));
            bool slid = false, hit = false;
            for (int i = 0; i < 300 && !hit; i++)
            {
                yield return new WaitForFixedUpdate();
                if (pg.Sliding) slid = true;
                if (c.run.health < 4) hit = true;
            }
            c.Expect(slid, "the penguin started a belly slide");
            c.Expect(hit, "the sliding penguin hit the player");
            // A slide ends at the first wall, where the penguin is dazed. The player is taken out of the way
            // (the knocked-back player may land on the first penguin and stomp it, which is fair play).
            c.p.godMode = true;
            if (pg.Dead || !pg.Sliding) pg = c.Spawn<Penguin>("penguin", 14, 1);
            bool dazed = false;
            for (int i = 0; i < 420 && !dazed && !pg.Dead; i++) { yield return new WaitForFixedUpdate(); if (pg.stunTimer > 0.5f && !pg.Sliding) dazed = true; }
            c.p.godMode = false;
            c.Expect(dazed, "a sliding penguin slams into the wall and is dazed");
        }

        static IEnumerator YetiCase(Ctx c)
        {
            c.Teleport(6.5f, 1f);
            var yt = c.Spawn<Yeti>("yeti", 13, 1);
            c.Expect(yt != null, "yeti spawned");
            if (yt == null) yield break;
            c.Bot(Seq((500, F())));
            float x0 = 6.5f;
            bool thrown = false, shoved = false;
            for (int i = 0; i < 420 && !shoved; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.Count<Snowball>() > 0) thrown = true;
                if (c.p.pos.x < x0 - 0.6f) shoved = true;
            }
            c.Expect(thrown, "the yeti threw a snowball");
            c.Expect(shoved, $"the snowball pushed the player away from the yeti (x {c.p.pos.x:0.00})");
            c.Expect(c.run.health == 4, "no damage from the snowball (health " + c.run.health + ")");
            c.Expect(c.p.Frozen, "without a fur coat the snowball freezes the player");
        }

        static IEnumerator SlideAfterRun(Ctx c, System.Action<float> result)
        {
            c.Teleport(4.5f, 1f);
            c.Bot(Seq((45, F(x: 1)), (90, F())));
            yield return c.Frames(45);
            float x = c.p.pos.x;
            yield return c.Frames(90);
            result(c.p.pos.x - x);
        }

        static IEnumerator EquipmentCase(Ctx c)
        {
            var run = c.run;
            var shop = c.dir.config.shop;
            foreach (var id in new[] { "springboots", "iceboots", "warmcoat" }) run.RemoveUpgrade(id);
            run.AddUpgrade(shop.Find("springboots"));
            c.Expect(run.IsWorn("springboots") && run.stats.jumpBonus > 0f, "the first boots are put on at once");
            run.AddUpgrade(shop.Find("iceboots"));
            c.Expect(run.HasUpgrade("iceboots") && !run.IsWorn("iceboots") && run.stats.iceGrip == 0f, "a second pair goes in the bag, with no effect");
            run.Wear("iceboots");
            c.Expect(run.IsWorn("iceboots") && !run.IsWorn("springboots") && run.stats.jumpBonus == 0f && run.stats.iceGrip > 0f,
                "wearing the cleats takes the spring boots off (one pair of feet)");

            // on ice: cleats on -> stops quickly; cleats off -> slides
            float old = c.world.Level.grip;
            c.world.Level.grip = 0.3f;
            float slideOn = 0f, slideOff = 0f;
            yield return SlideAfterRun(c, v => slideOn = v);
            run.TakeOff("iceboots");
            c.Expect(!run.IsWorn("iceboots") && run.stats.iceGrip == 0f, "taken off: back in the bag");
            yield return SlideAfterRun(c, v => slideOff = v);
            c.world.Level.grip = old;
            c.Expect(slideOff > slideOn * 1.8f && slideOff > 0.6f, $"slide after releasing: {slideOff:0.00} tiles on bare ice, {slideOn:0.00} with cleats");

            // the screen itself: open freezes the game, toggle wears, close resumes
            var inv = c.dir.inventory;
            c.Expect(inv != null, "equipment screen exists");
            if (inv != null)
            {
                float scale = Time.timeScale;
                inv.Open();
                c.Expect(inv.IsOpen && Time.timeScale == 0f, "open + frozen");
                yield return new WaitForSecondsRealtime(0.3f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                int idx = run.upgrades.IndexOf("iceboots");
                c.Expect(inv.Tab == InventoryScreen.TabWear && inv.EntryCount == run.upgrades.Count, $"the first tab lists what can be worn ({inv.EntryCount})");
                inv.Move(idx - inv.Selected);
                inv.Toggle();
                c.Expect(run.IsWorn("iceboots"), "toggle on the screen wears the selected item");
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory_worn.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                // up from the first row stays on the things (tabs change with L1 / R1 only); the second tab is what is
                // used, the third what is kept for good
                inv.Move(-inv.Selected);
                inv.MoveRow(-1);
                c.Expect(!inv.OnTabs && inv.Selected == 0 && inv.Tab == InventoryScreen.TabWear, "up from the first row stays on the first thing, same tab");
                inv.SetTab(InventoryScreen.TabItems);
                c.Expect(inv.EntryCount == run.gear.Count && run.gear.Count >= 2, $"the items tab lists what is used ({inv.EntryCount} of {run.gear.Count})");
                run.Equip(run.gear[0]);
                inv.Move(1 - inv.Selected);
                inv.Toggle();
                c.Expect(!inv.OnTabs && run.EquippedId == run.gear[1], "confirm on an item takes it in hand (" + run.EquippedId + ")");
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory_items.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                var keptRescued = Villagers.testRescued; Villagers.testRescued = new HashSet<string>(); Villagers.ResetTestState();
                Villagers.GiveKey(Villagers.KeyPouch);
                inv.SetTab(InventoryScreen.TabKeys);
                c.Expect(inv.EntryCount == 1, $"the key items tab shows the Ember Pouch ({inv.EntryCount})");
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory_keys.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                Villagers.ResetTestState(); Villagers.testRescued = keptRescued;
                inv.Close();
                c.Expect(!inv.IsOpen && Mathf.Approximately(Time.timeScale, scale), "closed + resumed");

                // a gamepad (real presses on a virtual pad): Start opens the bag on the WEAR tab even though it was shut on
                // KEY ITEMS, R1 / L1 change tab, Select (the pause button) shuts it
                var prevBg = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                var pad = InputSystem.AddDevice<Gamepad>("ScenarioBagPad");
                var none = new GamepadState();
                yield return Send(pad, none, 0.1f);
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.Start), 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(inv.IsOpen && !c.dir.Paused && inv.Tab == InventoryScreen.TabWear, $"Start opens the bag, on the WEAR tab (open {inv.IsOpen}, paused {c.dir.Paused}, tab {inv.Tab})");
                foreach (var b in new[] { GamepadButton.DpadUp, GamepadButton.DpadLeft })
                {
                    yield return Send(pad, new GamepadState().WithButton(b), 0.15f);
                    yield return Send(pad, none, 0.15f);
                }
                c.Expect(inv.IsOpen && inv.Tab == InventoryScreen.TabWear && !inv.OnTabs, $"up / left on the first thing do not reach or change the tabs (tab {inv.Tab}, on tabs {inv.OnTabs})");
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.RightShoulder), 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(inv.IsOpen && inv.Tab == InventoryScreen.TabItems, $"R1 goes one tab right (tab {inv.Tab})");
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.RightShoulder), 0.15f);
                yield return Send(pad, none, 0.15f);
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder), 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(inv.IsOpen && inv.Tab == InventoryScreen.TabItems, $"R1 then L1: one right, one left (tab {inv.Tab})");
                yield return Send(pad, new GamepadState().WithButton(GamepadButton.Select), 0.15f);
                yield return Send(pad, none, 0.15f);
                c.Expect(!inv.IsOpen && !c.dir.Paused && c.dir.gearWheel != null && !c.dir.gearWheel.IsOpen, $"Select shuts the bag, and does not pause too (open {inv.IsOpen}, paused {c.dir.Paused})");
                InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = prevBg;
                if (c.dir.Paused) c.dir.SetPaused(false);
            }
            foreach (var id in new[] { "springboots", "iceboots", "warmcoat" }) run.RemoveUpgrade(id);
        }

        static IEnumerator FurCoatCase(Ctx c)
        {
            c.run.RemoveUpgrade("warmcoat");
            c.run.AddUpgrade(c.dir.config.shop.Find("warmcoat"));
            c.Expect(c.run.IsWorn("warmcoat") && c.run.stats.coldResist > 0f, "the coat is worn");
            c.Teleport(6.5f, 1f);
            var yt = c.Spawn<Yeti>("yeti", 13, 1);
            c.Bot(Seq((500, F())));
            bool thrown = false, gone = false, frozen = false, slowed = false;
            for (int i = 0; i < 420 && !gone; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.Count<Snowball>() > 0) thrown = true;
                else if (thrown) gone = true;
                if (c.p.Frozen) frozen = true;
                if (c.p.Slowed) slowed = true;
            }
            yield return c.Frames(10);
            c.Expect(thrown && gone, "the yeti's snowball flew and burst");
            c.Expect(!frozen && !c.p.Frozen, "the wearer is not frozen");
            c.Expect(slowed, "the wearer is slowed instead");
            c.Expect(c.run.health == 4, "and takes no damage (health " + c.run.health + ")");
            // the slow: 30% off the running speed, gone after a second
            if (yt != null) yt.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is Snowball) e.Despawn();
            yield return c.Frames(70);          // the slow from the snowball itself runs out first
            c.p.Slow(0.3f, 1f);
            c.Teleport(6.5f, 1f);
            c.Bot(Seq((40, F(x: 1)), (80, F()), (40, F(x: 1)), (10, F())));
            yield return c.Frames(38);
            float slowSpeed = Mathf.Abs(c.p.vel.x);
            yield return c.Frames(80);
            c.Expect(!c.p.Slowed, "the slow wears off after a second");
            yield return c.Frames(38);
            float fullSpeed = Mathf.Abs(c.p.vel.x);
            c.Expect(fullSpeed > 1f && Mathf.Abs(slowSpeed / fullSpeed - 0.7f) < 0.05f, $"slowed speed is 70% of normal ({slowSpeed:0.00} vs {fullSpeed:0.00})");
            if (yt != null) yt.Despawn();
            c.run.RemoveUpgrade("warmcoat");
        }

        static IEnumerator FrostBatCase(Ctx c)
        {
            c.Teleport(6.5f, 1f);
            var bat = c.Spawn<FrostBat>("frostbat", 12, 5);
            c.Expect(bat != null, "frost bat spawned");
            if (bat == null) yield break;
            bat.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(90);
            IceChunk chunk = null;
            foreach (var e in c.world.Entities) if (e is IceChunk ic && !ic.Dead) chunk = ic;
            c.Expect(bat.Dead && chunk != null, "the dead frost bat left an ice chunk");
            if (chunk == null) yield break;
            c.Expect(chunk.CanPickUp && c.run.health == 4, "the chunk survived the fall, can be picked up, and hurt nobody");
            var snake = c.Spawn<Snake>("snake", 16, 1);
            chunk.pos = chunk.prevPos = new Vector2(15.1f, 1.3f);
            chunk.Throw(c.p, new Vector2(12f, 2f));
            yield return c.Frames(60);
            c.Expect(snake.Dead, "thrown ice chunk kills a snake");
            c.Expect(chunk.Dead, "and shatters on the hit");
        }

        static void ClearFight(Ctx c)
        {
            foreach (var e in c.world.Entities)
                if (!e.Dead && (e is Enemy || e is Snowball || e is BossShot || e is Stalactite || (e is Item it && !(e is Treasure) && it.thrower is Boss)))
                    e.Despawn();
        }

        static IEnumerator BossCase(Ctx c)
        {
            c.Teleport(4.5f, 1f);
            c.p.godMode = true;
            c.Bot(Seq((600, F())));
            var b = c.Spawn<Broodmother>("broodmother", 16, 1);
            c.Expect(b != null && c.dir.Boss == b && c.dir.BossAlive, "the boss registered with the director");
            if (b == null) { c.p.godMode = false; yield break; }
            int h0 = b.health;
            b.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, new Vector2(20f, 20f), "test"));
            c.Expect(h0 - b.health == b.maxHitDamage && !b.Dead, $"a 99-damage blow of the player's only takes {b.maxHitDamage} (took {h0 - b.health})");
            yield return c.Frames(14);
            // bombs and traps: one point each, whatever they would do to the player
            int h1 = b.health;
            b.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, new Vector2(20f, 20f), "test"));
            int afterOne = h1 - b.health;
            b.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, new Vector2(20f, 20f), "test"));
            c.Expect(afterOne == 1 && h1 - b.health == 2, $"a bomb blast takes one point from a boss, however hard it is: one blast {afterOne}, two blasts {h1 - b.health}");
            yield return c.Frames(40);
            int h2 = b.health;
            b.TakeDamage(new DamageInfo(99, DamageType.Spikes, null, Vector2.zero, "spikes"));
            int trapOne = h2 - b.health;
            b.TakeDamage(new DamageInfo(99, DamageType.Crush, null, Vector2.zero, "test"));
            int trapSame = h2 - b.health;
            yield return c.Frames(40);
            b.TakeDamage(new DamageInfo(99, DamageType.Fire, null, Vector2.zero, "test"));
            c.Expect(trapOne == 1 && trapSame == 1 && h2 - b.health == 2, $"a trap takes one point too, and not twice in the same moment: {trapOne}, {trapSame}, then {h2 - b.health} after a second one");
            {
                // the Hollow Warden is not hurt by traps at all (a bomb still takes its point)
                var ghost = c.Spawn<Warden>("warden", 22, 3);
                yield return c.Frames(3);
                if (ghost != null)
                {
                    int g0 = ghost.health;
                    for (int i = 0; i < 4; i++) { ghost.TakeDamage(new DamageInfo(99, i % 2 == 0 ? DamageType.Spikes : DamageType.Fire, null, Vector2.zero, "test")); yield return c.Frames(40); }
                    int byTraps = g0 - ghost.health;
                    ghost.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, Vector2.zero, "test"));
                    ghost.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, Vector2.zero, "test"));
                    c.Expect(byTraps == 0 && g0 - ghost.health == 2, $"traps do nothing to the Warden ({byTraps}); two bombs take 2 ({g0 - ghost.health})");
                    ghost.Despawn();
                    foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
                    c.dir.RegisterBoss(b);
                }
                c.Expect(ghost != null, "the warden spawned for the trap check");
            }
            c.Expect(Mathf.Abs(b.HealthFraction - b.health / (float)b.maxHealth) < 0.001f && b.HealthFraction < 1f, "health fraction feeds the bar");
            for (int i = 0; i < 40 && !b.Dead; i++)
            {
                b.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(14);
            }
            c.Expect(b.Dead && !c.dir.BossAlive, "the boss dies and the director knows");
            yield return c.Frames(10);
            ClearFight(c);
            c.p.godMode = false;
        }

        static IEnumerator GreedyStompCase(Ctx c)
        {
            c.Bot(Seq((2400, F())));
            c.run.RemoveUpgrade("warmcoat");
            foreach (var id in new[] { "broodmother", "forgebrute", "yetiking", "stormhawk" })
            {
                c.run.health = 8;
                c.Teleport(6.5f, 1f);
                yield return c.Frames(80);              // any invulnerability from the last boss has run out
                var b = c.Spawn<Boss>(id, 17, 1);
                c.Expect(b != null, id + " spawned");
                if (b == null) continue;
                int hp = c.run.health;
                c.Expect(!b.CounterStomp(c.p) && !b.CounterStomp(c.p), id + ": two stomps in a row count");
                c.Teleport(b.pos.x, b.pos.y + b.size.y + 0.3f);
                Vector2 before = c.p.pos;
                bool countered = b.CounterStomp(c.p);
                c.Expect(countered, id + ": a third stomp right after is countered");
                c.Expect(b.Countering && b.counterFx != null && b.animCounter != null, id + ": its counter animation is playing");
                yield return c.Frames(3);
                switch (id)
                {
                    case "broodmother": c.Expect(c.run.health < hp, $"broodmother bites upward (health {hp} -> {c.run.health})"); break;
                    case "forgebrute": c.Expect(c.run.health == hp && Mathf.Abs(c.p.vel.x) > 8f, $"forge brute hurls the player away without damage (vx {c.p.vel.x:0.0})"); break;
                    case "yetiking": c.Expect(c.run.health == hp && c.p.Frozen, "yeti king freezes the player, no damage"); break;
                    case "stormhawk": c.Expect(c.run.health == hp && c.p.vel.y > 9f && c.Count<BossShot>() >= 3, $"storm hawk blows the player up and sends feathers after them (vy {c.p.vel.y:0.0}, shots {c.Count<BossShot>()})"); break;
                }
                yield return new WaitForSecondsRealtime(0.05f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "counter_" + id + ".png"));
                if (id == "broodmother")
                {
                    // still invulnerable from that bite: the next one cannot hurt, but must throw the player off all the same
                    int hp2 = c.run.health;
                    c.Teleport(b.pos.x, b.pos.y + b.size.y + 0.3f);
                    c.Expect(b.CounterStomp(c.p), "broodmother counters a stomp while the player is invulnerable");
                    yield return c.Frames(1);
                    c.Expect(c.run.health == hp2 && c.p.vel.y > 6f, $"no damage, but the player is still thrown off her (health {hp2} -> {c.run.health}, vy {c.p.vel.y:0.0})");
                }
                yield return new WaitForSecondsRealtime(0.05f);
                // leave its head alone for longer than the window: a stomp counts again
                b.stunTimer = 99f;
                c.Teleport(6.5f, 1f);
                c.p.godMode = true;
                yield return c.Frames(Mathf.CeilToInt(b.stompCounterWindow / GameConst.Dt) + 20);
                c.p.godMode = false;
                c.Expect(!b.Dead && !b.CounterStomp(c.p), id + ": after waiting, a stomp counts again");
                b.Despawn();
                ClearFight(c);
                yield return c.Frames(5);
            }
        }

        // The case from play: the boss stands against the wall, the player comes down on the wall side of its back.
        static IEnumerator CounterWallCase(Ctx c)
        {
            c.Bot(Seq((3000, F())));
            c.run.RemoveUpgrade("warmcoat");
            foreach (var id in new[] { "broodmother", "forgebrute", "yetiking", "stormhawk" })
            {
                c.run.health = 8;
                c.Teleport(12.5f, 1f);
                yield return c.Frames(80);
                var b = c.Spawn<Boss>(id, 2, 1);
                c.Expect(b != null, id + " spawned");
                if (b == null) continue;
                b.pos = b.prevPos = new Vector2(1f + b.size.x * 0.5f + 0.02f, 1f);      // flush against the left wall
                b.stunTimer = 99f;
                yield return c.Frames(3);
                // two stomps count, the third (and any after) is countered - all from the wall side of its back
                float px = Mathf.Max(b.pos.x - b.size.x * 0.3f, 1.45f);      // on the wall side of its back, clear of the wall itself
                c.Teleport(px, b.pos.y + b.size.y + 0.3f);
                b.CounterStomp(c.p); b.CounterStomp(c.p);
                bool countered = b.CounterStomp(c.p);
                c.Expect(countered, id + ": third stomp countered");
                yield return c.Frames(2);
                c.Expect(c.p.vel.x > 0.5f, $"{id}: thrown away from the wall (vx {c.p.vel.x:0.0})");
                // wherever the counter throws them, the player must end up on the floor beside the boss, not bouncing on it
                bool clear = false;
                for (int i = 0; i < 240 && !clear; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (c.p.grounded && c.p.pos.x > b.pos.x + b.size.x * 0.5f) clear = true;
                }
                c.Expect(clear, $"{id}: the player comes down on the floor on the open side (x {c.p.pos.x:0.0}, boss edge {b.pos.x + b.size.x * 0.5f:0.0})");
                b.Despawn();
                ClearFight(c);
                yield return c.Frames(5);
            }
        }

        // Same proportions as its arena: floor surface at y = 2, ceiling at y = 11.
        static IEnumerator SporeHeartCase(Ctx c)
        {
            c.Bot(Seq((4000, F())));
            c.run.RemoveUpgrade("sporemask");
            for (int i = 0; i < 4; i++) c.Spawn<Scenery>("glowshroom", 5 + i * 5, 2);
            var cap = c.Spawn<BounceCap>("bouncecap", 11, 2);
            var b = c.Spawn<SporeHeart>("sporeheart", 12, 9);
            c.Expect(b != null, "rot heart spawned");
            if (b == null) yield break;
            yield return c.Frames(3);
            float hangY = b.pos.y;
            c.Expect(Mathf.Abs(hangY - (11f - b.hangDepth - b.size.y)) < 0.05f, $"it hangs on its stalk under the ceiling (y {hangY:0.00})");
            c.Expect(!b.IsStompable && b.ContactDamage == 0, "it cannot be stomped and touching it does not hurt");
            // its round, watched from a safe corner
            c.p.godMode = true;
            c.Teleport(4.5f, 2f);
            bool lashLow = false, lashHigh = false, rain = false, lure = false, darts = false;
            for (int i = 0; i < 2200 && !(lashLow && lashHigh && rain && lure && darts); i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var e in c.world.Entities)
                {
                    if (e.Dead) continue;
                    if (e is BossShot bs && bs.unbreakable) { if (bs.pos.y < 2.6f) lashLow = true; else lashHigh = true; }
                    else if (e is BossShot) darts = true;
                    else if (e is SporeCloud) rain = true;
                    else if (e is Lurecap) lure = true;
                }
                if (i == 400) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_sporeheart.png"));
            }
            c.Expect(darts, "it fires a fan of spore darts at the player");
            c.Expect(lashLow, "a tendril lashes across the room low along the floor");
            c.Expect(lashHigh, "and another one at head height");
            c.Expect(rain, "it rains spore clouds on the floor");
            c.Expect(lure, "it wakes a lurecap among the plants");
            c.Expect(Mathf.Abs(b.pos.y - hangY) < 0.01f, "it never moves");
            // the lashes below are measured one at a time: the heart must throw nothing of its own meanwhile (a dart
            // or a low lash of its own would be counted as the lash under test). No more moves after the one it has begun.
            b.actionGap = b.actionGapLow = 9999f;
            yield return c.Frames(220);
            // clear what it threw - not the heart itself (ClearFight would remove it too)
            foreach (var e in c.world.Entities) if (!e.Dead && e != b && (e is SporeCloud || e is BossShot || e is Lurecap)) e.Despawn();
            c.Expect(!b.Dead, "the heart is still there");
            // the two lashes against a real player: the low one hits a standing player, the high one passes overhead
            {
                c.p.godMode = false;
                c.run.health = 8;
                c.Teleport(8.5f, 2f);
                yield return c.Frames(70);
                int h = c.run.health;
                var lo = c.world.SpawnById("tendril", new Vector2(3f, 2f + b.lashLowY)) as BossShot;
                lo.vel = new Vector2(b.lashSpeed, 0f);
                yield return c.Frames(70);
                c.Expect(c.run.health < h, $"the low lash catches a player who stands still (health {h} -> {c.run.health})");
                yield return c.Frames(80);
                c.Teleport(8.5f, 2f);
                h = c.run.health;
                var hi = c.world.SpawnById("tendril", new Vector2(3f, 2f + b.lashHighY)) as BossShot;
                hi.vel = new Vector2(b.lashSpeed, 0f);
                yield return c.Frames(70);
                c.Expect(c.run.health == h, $"the high lash passes over a player who stays on the ground (health {h} -> {c.run.health})");
                foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
                c.p.godMode = true;
                c.Teleport(4.5f, 2f);
            }
            // reached from a bounce mushroom: fall onto the cap beside it, slash at the top of the bounce
            c.p.godMode = false;
            b.stunTimer = 0f;
            int hp0 = b.health;
            c.Teleport(cap.pos.x, 4f);
            c.p.motor.facing = 1;
            var slash = new List<FrameInput>();
            for (int i = 0; i < 150; i++) slash.Add(F(atk: i % 6 == 0));
            c.Bot(slash);
            for (int i = 0; i < 150 && b.health == hp0; i++)
            {
                // this checks the reach only: whatever the heart throws meanwhile is cleared away (its moves come in random order)
                foreach (var e in c.world.Entities) if (!e.Dead && (e is BossShot || e is SporeCloud || e is Lurecap)) e.Despawn();
                yield return new WaitForFixedUpdate();
            }
            c.Expect(b.health < hp0, $"a slash at the top of a bounce hits it (health {hp0} -> {b.health})");
            c.p.godMode = true;
            c.Bot(Seq((2000, F())));
            c.Teleport(4.5f, 2f);
            // losing health puts lights out: one third down, half the plants; two thirds down, half of the rest
            int Lit() { int n = 0; foreach (var e in c.world.Entities) if (!e.Dead && e is Scenery) { var gl = e.GetComponent<GlowLight>(); if (gl != null && gl.IsOn) n++; } return n; }
            int lit0 = Lit();
            for (int i = 0; i < 40 && !b.Dead && b.HealthFraction > 2f / 3f; i++) { b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            int lit1 = Lit();
            c.Expect(b.LightsOut == 1 && lit1 < lit0, $"at two thirds of its health some plants go dark ({lit0} -> {lit1})");
            for (int i = 0; i < 40 && !b.Dead && b.HealthFraction > 1f / 3f; i++) { b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            int lit2 = Lit();
            c.Expect(b.LightsOut == 2 && lit2 < lit1, $"at one third more go dark ({lit1} -> {lit2})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_sporeheart_dark.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            for (int i = 0; i < 200 && !b.Dead; i++) { b.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            yield return c.Frames(5);
            c.Expect(b.Dead, "it can be killed");
            c.Expect(Lit() == lit0, $"and the room lights up again ({Lit()} of {lit0})");
            bool vessel = false;
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup pk && pk.resource == Res.MaxHealth) vessel = true;
            c.Expect(vessel || c.run.maxHealth > 10, "it leaves a heart vessel");
            ClearFight(c);
        }

        static IEnumerator BossTestCase(Ctx c)
        {
            c.p.godMode = true;
            c.Teleport(4.5f, 1f);
            c.Bot(Seq((4000, F())));
            // the hawk in flight: flapping it is level; in a sweep the folded picture is turned along the ground
            var hawk = c.Spawn<StormHawk>("stormhawk", 14, 1);
            c.Expect(hawk != null, "storm hawk spawned");
            if (hawk != null)
            {
                yield return c.Frames(30);
                yield return null;
                c.Expect(!hawk.Swooping && Mathf.Abs(hawk.SpriteAngle) < 0.1f, "hovering: the picture is level");
                bool dived = false; float diveAngle = 0f;
                for (int i = 0; i < 1200 && !dived; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (i % 4 == 0) yield return null;      // let LateUpdate pose the picture
                    if (hawk.Swooping && !hawk.Sweeping && hawk.vel.sqrMagnitude > 25f)       // a dive, not the low sweep (the moves come in random order)
                    {
                        yield return null;
                        dived = true; diveAngle = hawk.SpriteAngle;
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hawk_dive.png"));
                        yield return new WaitForEndOfFrame();
                    }
                }
                c.Expect(dived && Mathf.Abs(Mathf.DeltaAngle(diveAngle, 0f)) < 80f, $"in a dive the beak points down at the player (angle {diveAngle:0})");
                hawk.Despawn();
                ClearFight(c);
            }
            // a boss test that is won: the result screen comes up and stays
            c.run.bossPractice = true;
            var b = c.Spawn<Broodmother>("broodmother", 16, 1);
            for (int i = 0; i < 200 && !b.Dead; i++) { b.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            c.Expect(b.Dead && c.dir.PracticeEnding, "the boss falls and the boss test is over");
            c.Expect(!c.dir.results.Showing, "the result screen waits a moment so the fall can be seen");
            yield return new WaitForSecondsRealtime(2.8f);
            c.Expect(c.dir.results.Showing, "then the result screen is shown");
            c.Expect(c.dir.results.cause.text.Contains("NO DAMAGE") || c.run.damageTaken > 0, "a win without losing health shows the no-damage badge");
            {
                // a boss beaten in Challenge leaves nothing behind: no heart vessel, no gold
                int loot = 0;
                foreach (var e in c.world.Entities) if (!e.Dead && (e is Pickup || e is Treasure)) loot++;
                c.Expect(loot == 0, $"the boss drops nothing in Challenge ({loot} things on the floor)");
            }
            {
                // the record book (nothing is written by tests: save = false only compares)
                var kept = new List<BossRecord>(SaveSystem.Challenge.bosses);
                SaveSystem.Challenge.bosses.Clear();
                SaveSystem.RecordBossWin("Test Boss", 50f, false, false, out bool nb, out bool ff);
                c.Expect(nb && !ff && SaveSystem.BossRecordOf("Test Boss") == null, "compare only: a first win would be a new best, nothing stored");
                SaveSystem.Challenge.bosses.Add(new BossRecord { boss = "Test Boss", bestTime = 50f });
                SaveSystem.RecordBossWin("Test Boss", 60f, true, false, out nb, out ff);
                c.Expect(!nb && ff, "a slower win is no new best, but the first one without damage earns the badge");
                SaveSystem.RecordBossWin("Test Boss", 40f, false, false, out nb, out ff);
                c.Expect(nb && !ff, "a faster win is a new best");
                SaveSystem.Challenge.bosses.Clear(); SaveSystem.Challenge.bosses.AddRange(kept);
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_test_result.png"));
            yield return new WaitForSecondsRealtime(2.5f);
            c.Expect(c.dir.results.Showing && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameConst.SceneGame, "and it stays until the player chooses");
            c.dir.results.root.SetActive(false);
            c.run.bossPractice = false;
            ClearFight(c);
        }

        static IEnumerator DashCase(Ctx c)
        {
            var cfg = c.p.cfg;
            c.p.godMode = false;
            c.run.maxHealth = 10; c.run.health = 10;
            c.Teleport(6.5f, 1f);
            yield return c.Frames(10);
            // a dash: about dashSpeed x dashTime along the floor, and nothing hurts while it lasts
            float x0 = c.p.pos.x;
            int n0 = c.p.Dashes;
            c.Bot(Seq((1, F(x: 1)), (1, F(dash: true)), (400, F())));
            yield return c.Frames(4);
            c.Expect(c.p.Dashing && c.p.Dashes == n0 + 1, "the dash button starts a dash");
            bool hurtInDash = c.p.TakeDamage(new DamageInfo(2, DamageType.Contact, null, new Vector2(5f, 5f), "test"));
            c.Expect(!hurtInDash && c.run.health == 10, $"nothing hurts in a dash (health {c.run.health})");
            for (int i = 0; i < 60 && c.p.Dashing; i++) yield return new WaitForFixedUpdate();
            float went = c.p.pos.x - x0, want = cfg.dashSpeed * cfg.dashTime;
            c.Expect(Mathf.Abs(went - want) < 0.9f, $"it carries the player about {want:0.0} tiles ({went:0.0})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "player_dash.png"));
            // each dash is paid from the energy bar: 100 holds three, the fourth is refused
            c.Expect(Mathf.Abs(c.p.DashEnergy - (cfg.dashEnergyMax - cfg.dashCost)) < 0.5f, $"a dash costs {cfg.dashCost:0} of {cfg.dashEnergyMax:0} energy ({c.p.DashEnergy:0} left)");
            for (int k = 0; k < 2; k++)
            {
                c.Bot(Seq((1, F(dash: true)), (400, F())));
                yield return c.Frames(3);
                for (int i = 0; i < 60 && c.p.Dashing; i++) yield return new WaitForFixedUpdate();
            }
            float left = c.p.DashEnergy;
            int refused0 = c.p.DashRefusals;
            c.Bot(Seq((1, F(dash: true)), (400, F())));
            yield return c.Frames(4);
            c.Expect(c.p.Dashes == n0 + 3 && !c.p.Dashing && c.p.DashRefusals == refused0 + 1 && left < cfg.dashCost, $"three dashes empty the bar and a fourth is refused ({left:0} left, {c.p.Dashes - n0} dashes)");
            // not dashing, the energy comes back by itself: nothing for the first half second, then all of it
            float e0 = c.p.DashEnergy;
            yield return c.Frames(Mathf.CeilToInt(cfg.dashRegenDelay * 0.6f / GameConst.Dt));
            bool held = Mathf.Abs(c.p.DashEnergy - e0) < 0.01f;
            yield return c.Frames(Mathf.CeilToInt((cfg.dashRegenDelay + cfg.dashEnergyMax / cfg.dashRegenRate + 0.3f) / GameConst.Dt));
            c.Expect(held && Mathf.Abs(c.p.DashEnergy - cfg.dashEnergyMax) < 0.01f, $"left alone for {cfg.dashRegenDelay:0.0} s the bar fills again ({c.p.DashEnergy:0})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "player_dash_energy.png"));
            // once the moment of safety is over, a blow counts again
            bool hurtAfter = c.p.TakeDamage(new DamageInfo(1, DamageType.Contact, null, Vector2.zero, "test"));
            c.Expect(hurtAfter && c.run.health == 9, $"once it is over a blow counts again (health {c.run.health})");
            for (int i = 0; i < 200 && (c.p.InvulnTime > 0f || c.p.IsStunned); i++) yield return new WaitForFixedUpdate();
            // through something that flies at the player: a shockwave along the floor
            c.Teleport(6.5f, 1f);
            yield return c.Frames(10);
            c.run.health = 10;
            var wave = c.Spawn<BossShot>("shockwave", 11, 1);
            bool dashed = false;
            if (wave != null) { wave.pos = new Vector2(11.5f, 1.3f); wave.vel = new Vector2(-7f, 0f); }      // (clear of the floor: a shot that touches it bursts)
            for (int i = 0; i < 200 && wave != null && !wave.Dead && wave.pos.x > 2f; i++)
            {
                if (!dashed && wave.pos.x - c.p.pos.x < 1.6f) { dashed = true; c.Bot(Seq((1, F(x: 1)), (1, F(dash: true)), (400, F()))); }
                yield return new WaitForFixedUpdate();
            }
            c.Expect(wave != null && dashed && c.run.health == 10, $"dashed into, a shockwave does nothing (health {c.run.health}, dashed {dashed})");
            foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
            yield return c.Frames(Mathf.CeilToInt((cfg.dashRegenDelay + cfg.dashEnergyMax / cfg.dashRegenRate + 0.3f) / GameConst.Dt));
            // a wall stops it
            c.Teleport(2.4f, 1f);
            yield return c.Frames(10);
            c.Bot(Seq((1, F(x: -1)), (1, F(dash: true)), (400, F())));
            yield return c.Frames(3);
            bool started = c.p.Dashing || c.p.Dashes == n0 + 5;
            int f = 0;
            for (; f < 60 && c.p.Dashing; f++) yield return new WaitForFixedUpdate();
            c.Expect(started && c.p.pos.x >= 1.25f && c.p.pos.x < 2.0f && f * GameConst.Dt < cfg.dashTime * 0.7f, $"a wall stops it short (x {c.p.pos.x:0.00}, {f} more frames)");
            yield return c.Frames(Mathf.CeilToInt((cfg.dashRegenDelay + cfg.dashEnergyMax / cfg.dashRegenRate + 0.3f) / GameConst.Dt));
            // in the air: level, and only once until the feet are down again
            c.Teleport(6.5f, 1f);
            yield return c.Frames(10);
            int n1 = c.p.Dashes;
            c.Bot(Seq((1, F(jp: true, jh: true)), (12, F(jh: true)), (1, F(x: 1, jh: true, dash: true)), (400, F())));
            yield return c.Frames(16);
            float y0 = c.p.pos.y;
            bool airborne = c.p.Dashing && !c.p.grounded;
            yield return c.Frames(6);
            c.Expect(airborne && c.p.Dashes == n1 + 1 && Mathf.Abs(c.p.pos.y - y0) < 0.05f, $"in the air it goes level (y {y0:0.00} -> {c.p.pos.y:0.00})");
            for (int i = 0; i < 300 && !c.p.grounded; i++) yield return new WaitForFixedUpdate();
            c.p.godMode = false;
            c.run.health = 4;
        }

        static IEnumerator RunPlanCase(Ctx c)
        {
            var cfg = c.dir.config;
            var areas = new List<AreaConfig>();
            foreach (var st in cfg.stages) if ((st.kind == LevelKind.Area || st.kind == LevelKind.Boss) && !areas.Contains(st.area)) areas.Add(st.area);
            c.Expect(areas.Count > cfg.areasPerRun && cfg.areasPerRun == 4, $"the catalogue has more areas ({areas.Count}) than one run uses ({cfg.areasPerRun})");
            var seenSecond = new HashSet<AreaConfig>();
            var seenFirst = new HashSet<AreaConfig>();
            var seenAny = new HashSet<AreaConfig>();
            bool allOk = true; string bad = "";
            for (int seed = 1; seed <= 200; seed++)
            {
                RunPlan.Build(cfg, seed, out var order, out var names);
                var runAreas = new List<AreaConfig>();
                bool ok = order.Length == names.Length;
                for (int i = 0; i < order.Length && ok; i++)
                {
                    var st = cfg.stages[order[i]];
                    if (st.kind != LevelKind.Area && st.kind != LevelKind.Boss) continue;
                    if (!runAreas.Contains(st.area)) runAreas.Add(st.area);
                    // each area: its levels in order, then its boss, numbered by its place in this run
                    int place = runAreas.IndexOf(st.area) + 1;
                    if (!names[i].StartsWith(place + "-")) ok = false;
                    if (i > 0 && cfg.stages[order[i - 1]].area == st.area && cfg.stages[order[i - 1]].kind == LevelKind.Boss) ok = false;
                }
                ok &= runAreas.Count == cfg.areasPerRun;
                if (runAreas.Count > 0) seenFirst.Add(runAreas[0]);
                ok &= cfg.stages[order[order.Length - 1]].kind == LevelKind.Final && cfg.stages[order[order.Length - 2]].kind == LevelKind.Rest;
                foreach (var a in runAreas) { int n = 0; foreach (int i in order) if (cfg.stages[i].area == a && cfg.stages[i].kind == LevelKind.Boss) n++; if (n != 1) ok = false; }
                if (!ok) { allOk = false; bad += seed + " "; }
                if (runAreas.Count > 1) seenSecond.Add(runAreas[1]);
                foreach (var a in runAreas) seenAny.Add(a);
                // the same seed gives the same plan
                RunPlan.Build(cfg, seed, out var again, out _);
                for (int i = 0; i < order.Length; i++) if (again[i] != order[i]) { allOk = false; bad += seed + "(not repeatable) "; break; }
            }
            c.Expect(allOk, "200 seeds: 4 areas each (levels then boss), then camp and vault; repeatable " + bad);
            c.Expect(seenAny.Count == areas.Count, $"every area turns up in some run ({seenAny.Count} of {areas.Count})");
            c.Expect(!cfg.firstAreaFixed && seenFirst.Count == areas.Count, $"the first area differs from run to run: any of them can open a run ({seenFirst.Count} of {areas.Count})");
            c.Expect(seenSecond.Count == areas.Count, $"the second area differs from run to run ({seenSecond.Count} different ones)");
            yield return c.Frames(2);
        }

        static IEnumerator ForgeBruteCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((4000, F())));
            c.Teleport(5.5f, 1f);
            // a dirt wall (three tiles high) stands between the brute and the player
            c.Expect(c.world.Grid.IsSolid(12, 1) && c.world.Grid.IsSolid(12, 3), "a dirt wall stands in the arena");
            var b = c.Spawn<ForgeBrute>("forgebrute", 19, 1, 0, null, -1);
            c.Expect(b != null, "forge brute spawned");
            if (b == null) yield break;
            int maxFire = 0, burning = 0, maxWaves = 0, hops = 0; float longest = 0f, since = 0f, lastVy = 0f; bool wallGone = false, passed = false;
            var seenWaves = new HashSet<Entity>();
            for (int i = 0; i < 2400; i++)
            {
                yield return new WaitForFixedUpdate();
                int n = 0, lit = 0, waves = 0;
                foreach (var e in c.world.Entities)
                {
                    if (e.Dead) continue;
                    if (e is FirePatch f) { n++; if (f.Burning) lit++; }
                    else if (e is BossShot && e.displayName == "shockwave" && e.vel.x < 0f) { waves++; seenWaves.Add(e); }      // those running left
                }
                maxWaves = Mathf.Max(maxWaves, seenWaves.Count);      // every wave sent that way so far (one slam sends three)
                if (b.vel.y > 6f && lastVy <= 6f) hops++;             // it left the ground
                lastVy = b.vel.y;
                maxFire = Mathf.Max(maxFire, n);
                if (lit > 0) { since += GameConst.Dt; longest = Mathf.Max(longest, since); } else since = 0f;
                burning = Mathf.Max(burning, lit);
                if (!c.world.Grid.IsSolid(12, 1) && !c.world.Grid.IsSolid(12, 2)) wallGone = true;
                if (wallGone && b.pos.x < 11f) passed = true;
                if (longest > 0.5f && lit == 0 && wallGone && passed && maxWaves >= 3) break;      // a fire has burned and gone out, and the rest was seen
            }
            c.Expect(maxFire >= 3 && maxFire <= 5, $"its stomp lights five spots at a time, fewer near a wall (most seen: {maxFire})");
            c.Expect(hops >= maxWaves && hops >= 3, $"every slam is a leap of its own: {hops} leaps for {maxWaves} waves");
            c.Expect(longest > 0.5f && longest < 1.6f, $"and they burn only briefly ({longest:0.0}s)");
            c.Expect(maxWaves >= 3, $"its ground slam sends three shockwaves one after another (sent one way in all: {maxWaves})");
            c.Expect(wallGone, "charging into the dirt wall, it tears through it");
            c.Expect(passed, $"and carries on past where the wall stood (x {b.pos.x:0.0})");
            b.Despawn();
            ClearFight(c);
            foreach (var e in c.world.Entities) if (!e.Dead && e is FirePatch) e.Despawn();
        }

        static IEnumerator BoulderCase(Ctx c)
        {
            c.Bot(Seq((3000, F())));
            // hit early, while it is small
            c.run.health = 8;
            c.Teleport(6.5f, 1f);
            yield return c.Frames(70);
            var ball = c.world.SpawnById("snowboulder", new Vector2(3.5f, 1f)) as RollingBall;
            c.Expect(ball != null, "snow boulder spawned");
            if (ball == null) yield break;
            ball.Roll(1);
            int hp = c.run.health; float s1 = 0f;
            for (int i = 0; i < 200 && c.run.health == hp; i++) { if (!ball.Dead) s1 = ball.Scale; yield return new WaitForFixedUpdate(); }
            c.Expect(hp - c.run.health == 1, $"small (scale {s1:0.00}) it costs 1 (health {hp} -> {c.run.health})");
            c.Expect(!c.p.Frozen, "it no longer freezes");
            // hit late, fully grown
            yield return c.Frames(90);
            c.Teleport(20.5f, 1f);
            hp = c.run.health;
            var big = c.world.SpawnById("snowboulder", new Vector2(3.5f, 1f)) as RollingBall;
            big.Roll(1);
            float s2 = 0f; bool grew = false;
            for (int i = 0; i < 400 && c.run.health == hp; i++)
            {
                if (!big.Dead) { s2 = big.Scale; if (big.DamageNow == 2) grew = true; }
                if (i == 150) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "snow_boulder.png"));
                yield return new WaitForFixedUpdate();
            }
            c.Expect(grew, "on the way it passes through the middle size (2 damage)");
            c.Expect(hp - c.run.health == 3, $"fully grown (scale {s2:0.00}) it costs 3 (health {hp} -> {c.run.health})");
            // three strokes still break it
            yield return c.Frames(90);
            var cut = c.world.SpawnById("snowboulder", new Vector2(3.5f, 1f)) as RollingBall;
            cut.Roll(1);
            yield return c.Frames(20);
            cut.OnWhipped(c.p, 1, 1); cut.OnWhipped(c.p, 1, 1);
            c.Expect(!cut.Dead, "two strokes do not break it");
            cut.OnWhipped(c.p, 1, 1);
            c.Expect(cut.Dead, "the third does");
        }

        static IEnumerator RecallCase(Ctx c)
        {
            c.Bot(Seq((6000, F())));
            c.p.godMode = true;
            var def = c.dir.config.FindGear("recall");
            c.Expect(def != null && System.Array.IndexOf(c.dir.config.player.startingGear, "recall") >= 0, "the Recall Charm exists and every run starts with it");
            var charm = GearRegistry.Get("recall") as RecallGear;
            c.Expect(charm != null, "it has its behaviour registered");
            if (charm == null) yield break;
            charm.MakeReady();
            var start = c.dir.Level.entrance;
            c.Teleport(22.5f, 1f);
            yield return c.Frames(6);
            charm.Use(c.p, default);
            yield return new WaitForSecondsRealtime(0.8f);
            c.Expect(Mathf.Abs(c.p.pos.x - (start.x + 0.5f)) < 0.6f && Mathf.Abs(c.p.pos.y - start.y) < 1.2f,
                $"using it far from the entrance puts the player back there (at {c.p.pos.x:0.0},{c.p.pos.y:0.0}, entrance {start.x},{start.y})");
            c.Expect(charm.Wait > 0f, "then it needs a rest");
            c.Teleport(22.5f, 1f);
            yield return c.Frames(6);
            charm.Use(c.p, default);
            yield return new WaitForSecondsRealtime(0.8f);
            c.Expect(Mathf.Abs(c.p.pos.x - 22.5f) < 1.5f, "used again too soon it does nothing");
            charm.MakeReady();
        }

        static IEnumerator VillagerCase(Ctx c)
        {
            c.Bot(Seq((6000, F())));
            Villagers.testRescued = new HashSet<string>();       // the test keeps its own state: the save file is not touched
            Villagers.ResetTestState();
            var seen = new HashSet<string>();
            bool allFreed = true, prompts = true, gone = true;
            for (int i = 0; i < Villagers.All.Length; i++)
            {
                if (RunSession.Current != null) RunSession.Current.villagerFreed = false;      // (a run frees one villager: each turn of this loop stands for a new run)
                var cap = c.Spawn<Captive>("captive", 8, 1);
                yield return c.Frames(3);
                if (cap == null || cap.Dead || cap.Who == null) { allFreed = false; break; }
                seen.Add(cap.Who.id);
                c.Teleport(8.5f, 1f);
                yield return c.Frames(4);
                string pr = cap.GetPrompt(c.p);
                if (pr == null || !pr.Contains(cap.Who.name)) prompts = false;
                if (i == 0) { yield return c.Frames(20); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "villager_caged.png")); yield return c.Frames(3); }
                cap.Interact(c.p);
                if (!cap.Freed || !Villagers.IsRescued(cap.Who.id)) allFreed = false;
                yield return c.Frames(130);
                if (!cap.Dead) gone = false;
            }
            c.Expect(prompts, "standing at the cage shows who is in it");
            c.Expect(allFreed && seen.Count == Villagers.All.Length, $"each cage holds someone who is still lost, until all {Villagers.All.Length} are home ({seen.Count} different)");
            c.Expect(gone, "a freed villager leaves for the village");
            var empty = c.Spawn<Captive>("captive", 8, 1);
            yield return c.Frames(3);
            c.Expect(empty == null || empty.Dead, "with everyone home there is no cage any more");

            // one cage per run, never in the first area: the second, third or fourth (levels 4-6, 8-10, 12-14 of the run)
            {
                bool ok = true; var areas = new HashSet<int>();
                int without = 0;
                for (int seed = 1; seed <= 300; seed++)
                {
                    int st = Villagers.CaptiveStage(seed);
                    if (st < 0) { without++; continue; }
                    if (st < 4 || st > 14 || st % 4 == 3) ok = false;
                    areas.Add(st / 4);
                }
                c.Expect(ok && areas.Count == 3, $"the cage is in one level of area 2, 3 or 4 - never the first, never a boss room ({areas.Count} different areas over 300 runs)");
                c.Expect(without == 0, $"every run has a caged villager in it ({without} of 300 runs without)");
            }

            // embers: the money of the village. Only with the elder's pouch, and only as many as it holds
            var run = c.run;
            c.Expect(Villagers.AddEmbers(2) == 0 && Villagers.Embers == 0, "without the elder's pouch an ember cannot be kept");
            var elder = c.Spawn<Elder>("elder", 26, 1);
            yield return c.Frames(3);
            c.Expect(elder != null && elder.GetPrompt(c.p) != null && elder.GetPrompt(c.p).StartsWith("[enter]") && elder.GetPrompt(c.p).Contains(Elder.ElderName), "the elder has something to say");
            // (what he gives comes of his quest - see "quests:" - here it is handed over as that quest's end does)
            Villagers.GiveKey(Villagers.KeyPouch);
            c.Expect(Villagers.HasKey(Villagers.KeyPouch), "the Ember Pouch is a key item");
            c.Expect(Villagers.PouchCapacity == Villagers.PouchStartCapacity && Villagers.PouchStartCapacity == 5, "it holds 5 embers when he hands it over");
            int went = Villagers.AddEmbers(8);
            c.Expect(went == 5 && Villagers.Embers == 5, $"the pouch takes embers until it is full ({went} of 8 went in)");
            Villagers.ExpandPouch(3);
            went = Villagers.AddEmbers(8);
            c.Expect(Villagers.PouchCapacity == 8 && went == 3 && Villagers.Embers == 8, "an errand for the elder makes it hold more");
            c.Expect(Villagers.SpendEmbers(2) && Villagers.Embers == 6 && !Villagers.SpendEmbers(99) && Villagers.Embers == 6, "embers can be spent, never more than there are");
            {
                // an ember on the ground goes to the pouch, not to the run's gold
                {
                    // an ember lies on the ground like a gem does: not sunk into it, and no bigger
                    c.Teleport(13.5f, 1f);
                    var lying = c.Spawn<Pickup>("ember", 16, 1);
                    var gemBy = c.Spawn<Pickup>("gem", 17, 1);
                    yield return c.Frames(40);
                    var eb = lying != null && lying.GetComponentInChildren<SpriteRenderer>() != null ? lying.GetComponentInChildren<SpriteRenderer>().bounds : new Bounds();
                    var gb = gemBy != null && gemBy.GetComponentInChildren<SpriteRenderer>() != null ? gemBy.GetComponentInChildren<SpriteRenderer>().bounds : new Bounds();
                    c.Expect(lying != null && eb.min.y > 0.95f && eb.size.y <= gb.size.y + 0.13f, $"an ember rests on the floor (bottom {eb.min.y:0.00}) and is about a gem's size ({eb.size.y:0.00} vs {gb.size.y:0.00})");
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "ember_ground.png"));
                    yield return c.Frames(3);
                    if (lying != null) lying.Despawn();
                    if (gemBy != null) gemBy.Despawn();
                }
                int gold0 = run.gold;
                c.Teleport(30.5f, 1f);
                yield return c.Frames(4);
                var em = c.Spawn<Pickup>("ember", 30, 1);
                yield return c.Frames(14);
                if (em != null && !em.Dead) em.Collect(c.p);
                yield return c.Frames(2);
                // (a collected pickup is destroyed, so it cannot be asked anything any more: the pouch tells)
                c.Expect(Villagers.Embers == 7 && run.gold == gold0, $"picking up an ember: pouch 6 -> {Villagers.Embers}, gold {gold0} -> {run.gold}");
            }

            // the buildings: lit with their keeper outside and their wares set out; a ware costs gold and is paid from the pouch
            var home = c.Spawn<VillagerHome>("villagerhome", 12, 1, 2);
            yield return c.Frames(4);
            c.Expect(home != null && home.Home && home.Who == Villagers.All[2] && home.WareCount == Villagers.All[2].wares.Length,
                $"the Healer is home: her place is lit and her wares are out ({(home != null ? home.WareCount : -1)})");
            // nothing is laid out on the floor in the village: spoken to, she opens her shop as a screen
            int onFloor = 0;
            foreach (var e in c.world.Entities) if (e is ShopItem si && !si.Dead) onFloor++;
            var shopUi = c.dir.villageShop;
            // standing by her, the prompt has a row for a word and a row for her shop - no screen opens to choose;
            // down / up moves the chosen row (the one with the button on it), the rows stay where they are
            c.Teleport(home != null ? home.pos.x : 12.5f, 1f);
            c.Bot(Seq((6000, F())));
            yield return c.Frames(6);
            string[] offers = home != null ? home.Choices(c.p) : null;
            string tag0 = c.p.Prompt ?? "";
            string[] all0 = tag0.Split('\n');
            string[] rows0 = all0.Length == 3 ? new[] { all0[1], all0[2] } : new string[0];       // (the first line is her name)
            c.Expect(all0.Length == 3 && all0[0].Contains("Maren the Healer") && !all0[0].Contains("[enter]"), $"her name stands over the rows ('{(all0.Length > 0 ? all0[0] : "")}')");
            c.Expect(offers != null && offers.Length == 2 && shopUi != null && !shopUi.IsOpen && c.p.ChoiceIndex == 0 && rows0.Length == 2
                     && rows0[0].StartsWith("[updown] [enter]") && rows0[0].EndsWith(">Talk") && !rows0[1].Contains("[enter]") && rows0[1].Contains(">Shop<") && !tag0.Contains("choose"),
                $"by the Healer the prompt has two rows, Talk and Shop, the up/down mark and the button before the first, and no words about choosing ('{tag0.Replace("\n", " / ")}')");
            yield return new WaitForSecondsRealtime(0.4f);
            var tagFaults = UIOverlap.Find();
            c.Expect(tagFaults.Count == 0, "the rows at the prompt are laid out cleanly" + (tagFaults.Count > 0 ? " (" + tagFaults[0] + ")" : ""));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_choices.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            c.Bot(Seq((2, F()), (1, new FrameInput { interactPressed = true }), (20, F())));
            yield return c.Frames(10);
            c.Expect(shopUi != null && !shopUi.IsOpen && c.dir.hud.TalkUp, "the interact button does the chosen row: she says her piece, and no screen opens");
            c.dir.hud.AdvanceTalk(); c.dir.hud.AdvanceTalk(); c.dir.hud.AdvanceTalk(); c.dir.hud.AdvanceTalk();
            yield return c.Frames(6);
            float standH = c.p.Bounds.height;
            c.Bot(Seq((2, F()), (8, F(down: true)), (6, F())));
            yield return c.Frames(8);
            bool noCrouch = !c.p.Crouching;
            yield return c.Frames(6);
            string tag1 = c.p.Prompt ?? "";
            string[] all1 = tag1.Split('\n');
            string[] rows1 = all1.Length == 3 ? new[] { all1[1], all1[2] } : new string[0];
            c.Expect(c.p.ChoiceIndex == 1 && rows1.Length == 2 && rows1[0].Contains(">Talk<") && !rows1[0].Contains("[enter]") && rows1[1].StartsWith("[updown] [enter]") && rows1[1].EndsWith(">Shop") && noCrouch,
                $"down moves the button to the second row - the rows stay in place, and the player does not crouch for it ('{tag1.Replace("\n", " / ")}', crouched {!noCrouch})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_choices_2.png"));
            yield return c.Frames(3);
            c.Bot(Seq((2, F()), (4, F(up: true)), (4, F()), (4, F(up: true)), (4, F())));
            yield return c.Frames(20);
            c.Expect(c.p.ChoiceIndex == 1, $"up goes back to the first row, and up again round to the last ({c.p.ChoiceIndex})");
            c.Bot(Seq((2, F()), (1, new FrameInput { interactPressed = true }), (6000, F())));
            yield return new WaitForSecondsRealtime(0.5f);       // (the game stands still once the shop is open: no counting its steps)
            c.Expect(onFloor == 0 && shopUi != null && shopUi.IsOpen && shopUi.Count == Villagers.All[2].wares.Length && Time.timeScale == 0f,
                $"no wares on the floor ({onFloor}); spoken to, the Healer's shop opens as a screen with her wares, and the game stands still ({(shopUi != null ? shopUi.Count : -1)} rows)");
            if (shopUi != null && shopUi.IsOpen)
            {
                var shopFaults = UIOverlap.Find();
                c.Expect(shopFaults.Count == 0, "the shop screen is laid out cleanly" + (shopFaults.Count > 0 ? " (" + shopFaults[0] + ")" : ""));
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_shop.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                int row = -1;
                for (int i = 0; i < shopUi.Count; i++) if (shopUi.At(i).blueprint && shopUi.At(i).ware.id == "dampener") row = i;
                int hp = run.maxHealth, gold0 = run.gold;
                int price = Villagers.EmberPrice(c.dir.config.shop.Find("dampener"), Villagers.All[2]);
                Blueprints.Refresh();
                c.Expect(row >= 0 && !Blueprints.Known("dampener") && Blueprints.Locked("dampener") && Blueprints.Locked("ironwhip") && Blueprints.Locked("iceboots")
                         && !Blueprints.Locked("truthlens") && !Blueprints.Locked("compass") && !Blueprints.Locked("ironcap"),
                    "her stall sells the Dampener's blueprint; until it is bought the mountain does not carry it (nor the smith's blade, nor an area's stronger piece) - what is open from the start is never locked");
                // a blueprint lasts and is paid in embers: without them it is not sold, whatever sparks there are
                Villagers.SpendEmbers(Villagers.Embers);
                Villagers.AddSparks(20);
                int s0 = Villagers.Sparks;
                bool refused = row >= 0 && !shopUi.Buy(row) && !Blueprints.Known("dampener");
                c.Expect(refused, $"with no embers the blueprint is not sold, sparks or no sparks (sparks {Villagers.Sparks})");
                Villagers.AddEmbers(price + 1);
                int e0 = Villagers.Embers;
                bool bought = row >= 0 && shopUi.Buy(row);
                c.Expect(bought && Blueprints.Known("dampener") && !Blueprints.Locked("dampener") && Villagers.Embers == e0 - price && Villagers.Sparks == s0
                         && run.gold == gold0 && !run.HasUpgrade("dampener") && shopUi.IsOpen,
                    $"buying the blueprint: {price} embers paid ({e0} -> {Villagers.Embers}), no sparks, nothing in the bag - the mountain carries it from now on");
                c.Expect(row >= 0 && !shopUi.Buy(row) && Villagers.Embers == e0 - price, "a blueprint is bought once: the row says it is known");
                shopUi.Close();
                yield return new WaitForSecondsRealtime(0.1f);

                // Pip: a consumable whose blueprint is not known is offered as its blueprint; bought, the same row sells the thing itself, in sparks
                var pip = Villagers.All[Villagers.IndexOf("peddler")];
                shopUi.Open(pip.name, "", VillageShopPanel.WaresOf(pip, c.dir.config, run));
                c.Expect(c.dir.hud.StatsRaised, "while a stall's screen is open, health and money in the corner stay bright above it");
                // a row that cannot be paid for is greyed out, the whole of it; one that can is not
                {
                    int lr0 = -1, br0 = -1;
                    for (int i = 0; i < shopUi.Count; i++) { if (shopUi.At(i).ware.id == "lampoil") lr0 = i; if (shopUi.At(i).ware.id == "bombs") br0 = i; }
                    Villagers.SpendEmbers(Villagers.Embers);
                    Villagers.AddSparks(5);
                    shopUi.Open(pip.name, "", VillageShopPanel.WaresOf(pip, c.dir.config, run));      // (drawn again with no embers)
                    for (int i = 0; i < shopUi.Count; i++) { if (shopUi.At(i).ware.id == "lampoil") lr0 = i; if (shopUi.At(i).ware.id == "bombs") br0 = i; }
                    float poor = shopUi.RowBrightness(lr0), rich = shopUi.RowBrightness(br0);
                    bool bagRoom = run.RoomFor(Res.Bombs) > 0;
                    c.Expect(poor > 0f && poor < 0.6f && (!bagRoom || rich > 0.95f),
                        $"with no embers the Lamp Oil blueprint row is greyed out ({poor:0.00}); bombs, paid for in sparks, are not ({rich:0.00}, room in the bag {bagRoom})");
                }
                // the press that opened the screen must not buy what is in focus: a press in its first moments does nothing
                {
                    int lr = -1;
                    for (int i = 0; i < shopUi.Count; i++) if (shopUi.At(i).ware.id == "lampoil") lr = i;
                    Villagers.AddEmbers(3);
                    int emb = Villagers.Embers;
                    bool inGrace = shopUi.InGrace;
                    if (lr >= 0) shopUi.Press(lr);
                    c.Expect(inGrace && lr >= 0 && !Blueprints.Known("lampoil") && Villagers.Embers == emb,
                        $"a press the moment the screen opens buys nothing (the Lamp Oil blueprint unknown, embers {emb} -> {Villagers.Embers})");
                }
                yield return new WaitForSecondsRealtime(0.1f);
                int vr = -1, br = -1;
                for (int i = 0; i < shopUi.Count; i++) { if (shopUi.At(i).ware.id == "vigor") vr = i; if (shopUi.At(i).ware.id == "bombs") br = i; }
                var vEntry = shopUi.At(vr);
                c.Expect(vEntry != null && vEntry.blueprint && vEntry.lasting && br >= 0 && !shopUi.At(br).blueprint && !shopUi.At(br).lasting,
                    "at Pip's the Heart Draught is a blueprint (embers) until it is known; bombs, open from the start, are sold as they are (sparks)");
                int vPlan = vEntry != null ? vEntry.price : 0;
                Villagers.AddEmbers(vPlan);
                int e1 = Villagers.Embers;
                bool plan = vr >= 0 && shopUi.Buy(vr);
                var after = shopUi.At(vr);
                c.Expect(plan && Blueprints.Known("vigor") && Villagers.Embers == e1 - vPlan && run.maxHealth == hp && after != null && !after.blueprint && !after.lasting && after.ware.id == "vigor",
                    $"its blueprint bought ({vPlan} embers), the row now sells the Heart Draught itself, for sparks ({(after != null ? after.price : -1)})");
                Villagers.AddSparks(5);
                int s1 = Villagers.Sparks, draught = after != null ? after.price : 0;
                bool drunk = vr >= 0 && shopUi.Buy(vr);
                c.Expect(drunk && run.maxHealth == hp + 1 && Villagers.Sparks == s1 - draught,
                    $"bought there: +1 heart for this run, {draught} sparks paid ({s1} -> {Villagers.Sparks})");
                run.maxHealth = hp; run.health = Mathf.Min(run.health, hp);
                shopUi.Close();
                yield return new WaitForSecondsRealtime(0.1f);
                c.Expect(!shopUi.IsOpen && Time.timeScale > 0f && !c.dir.hud.StatsRaised, "closed, the game goes on (and the corner is back in its place)");

                // the Peddler's paper has the longest description of any ware: it stays on its one line in its row
                shopUi.Open(Villagers.All[4].name, "", VillageShopPanel.WaresOf(Villagers.All[4], c.dir.config, run));
                yield return new WaitForSecondsRealtime(0.1f);
                bool oneLine = true;
                for (int i = 0; i < shopUi.Count && i < shopUi.descs.Length; i++)
                {
                    var d = shopUi.descs[i]; d.ForceMeshUpdate();
                    if (d.textInfo.lineCount > 1 || d.isTextOverflowing) oneLine = false;
                }
                var pipFaults = UIOverlap.Find();
                c.Expect(oneLine && pipFaults.Count == 0, "every description in the Peddler's shop stays on one line inside its row" + (pipFaults.Count > 0 ? " (" + pipFaults[0] + ")" : ""));
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_shop_peddler.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                shopUi.Close();
                yield return new WaitForSecondsRealtime(0.1f);
                // the ember's first-time card (picked up above) has waited behind the shop screens: let it be read and go
                // now, so it is not on screen when the village's name comes up at the door below (in play the card stops
                // the game until it is put away, so the two never meet)
                float cardUntil = Time.realtimeSinceStartup + 10f;
                while (c.dir.hud.CardUp && Time.realtimeSinceStartup < cardUntil) yield return null;
            }
            yield return c.Frames(10);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "villager_homes.png"));
            yield return c.Frames(3);
            Villagers.testRescued.Clear();
            var darkHome = c.Spawn<VillagerHome>("villagerhome", 20, 1, 3);
            yield return c.Frames(3);
            c.Expect(darkHome == null || darkHome.Dead, "nobody keeps the shop of someone who is still lost");
            {
                // the street door of an open house leads into its room and back out; a lost keeper's house stays locked
                Villagers.testRescued.Add(Villagers.All[1].id);
                var front = c.Spawn<VillageBuilding>("building", 6, 1, 1);
                var locked = c.Spawn<VillageBuilding>("building", 9, 1, 4);
                var room = c.Spawn<VillageInterior>("interior", 24, 1, 1);
                yield return c.Frames(4);
                c.Expect(front != null && front.Open && locked != null && !locked.Open, "a freed keeper's house is open, a lost one's is locked");
                // the windows of a room show the hour of the street outside: a picture of its own for dawn, day and dusk
                if (room != null)
                {
                    var hourPics = new HashSet<Sprite>();
                    foreach (VillageClock.Part part in System.Enum.GetValues(typeof(VillageClock.Part))) { var pic = room.RoomPicture(1, part); if (pic != null) hourPics.Add(pic); }
                    var nowPart = VillageClock.PartOf(VillageClock.Hour);
                    c.Expect(hourPics.Count == 4 && room.Picture == room.RoomPicture(1, nowPart) && room.RoomPicture(1, VillageClock.Part.Night) == room.rooms[1],
                        $"a room has a picture for each hour of the village clock ({hourPics.Count} of 4) and wears the one for now ({nowPart})");
                }
                if (locked != null) locked.Interact(c.p);
                yield return c.Frames(4);
                c.Expect(locked != null && locked.GetPrompt(c.p).Contains("Look") && c.dir.hud.NoteUp, "the locked door offers a look, and says it is locked when looked at");
                c.dir.hud.DismissHeld();
                c.Teleport(6.5f, 1f);
                yield return c.Frames(4);
                if (locked != null) locked.Interact(c.p);
                yield return c.Frames(3);
                c.Expect(Mathf.Abs(c.p.pos.x - 6.5f) < 1f, "a locked door lets nobody in");
                if (front != null) front.Interact(c.p);
                yield return new WaitForSecondsRealtime(0.7f);       // through the fade
                c.Expect(room != null && Mathf.Abs(c.p.pos.x - (room.pos.x + 1f)) < 1f, $"the open door leads into the room (player x {c.p.pos.x:0.0})");
                Entity keeper = null;
                foreach (var e in c.world.Entities) if (e is VillagerHome vh && !vh.Dead && vh.Who == Villagers.All[1]) keeper = vh;
                c.Expect(keeper != null, "the keeper is inside, behind the counter");
                // the room has things worth a closer look, and looking tells the player something
                int curios = 0; Curio near = null;
                foreach (var e in c.world.Entities) if (e is Curio cu && !cu.Dead) { curios++; if (near == null || Mathf.Abs(cu.pos.x - c.p.pos.x) < Mathf.Abs(near.pos.x - c.p.pos.x)) near = cu; }
                c.Expect(curios >= 3, $"the room has things to look at ({curios})");
                c.Expect(near != null && (near.GetPrompt(c.p).Contains("Read") || near.GetPrompt(c.p).Contains("Open")) && !string.IsNullOrEmpty(near.text), "each is something to read and has something to say");
                if (near != null) near.Interact(c.p);
                yield return c.Frames(20);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_room.png"));
                yield return c.Frames(3);
                if (room != null) room.Interact(c.p);
                yield return new WaitForSecondsRealtime(0.7f);
                c.Expect(front != null && Mathf.Abs(c.p.pos.x - front.pos.x) < 1f, "the room's door leads back out to the street");
                c.dir.cameraFollow.SetBounds(new Rect(0, 0, c.world.Grid.Width, c.world.Grid.Height));
                if (front != null) front.Despawn();
                if (locked != null) locked.Despawn();
                if (room != null) room.Despawn();
                foreach (var e in c.world.Entities) if (e is VillagerHome vh && !vh.Dead) vh.Despawn();
                Villagers.testRescued.Clear();
            }
            foreach (var e in c.world.Entities) if (e is ShopItem si && !si.Dead) si.Despawn();
            if (home != null) home.Despawn();
            if (darkHome != null) darkHome.Despawn();
            if (elder != null) elder.Despawn();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
        }

        static IEnumerator MinionSpotCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((3000, F())));
            var b = c.Spawn<YetiKing>("yetiking", 12, 1, 0, null, -1);
            c.Expect(b != null, "yeti king spawned");
            if (b == null) yield break;
            bool allNear = true, bothSides = false; string where = "";
            int left = 0, right = 0;
            // the player stands in the left corner, then in the right one: the helper comes up close by either way
            foreach (float px in new[] { 3.5f, c.world.Grid.Width - 3.5f })
                for (int k = 0; k < 8; k++)
                {
                    c.Teleport(px, 1f);
                    b.pos = new Vector2(12.5f, 1f); b.vel = Vector2.zero;
                    yield return c.Frames(2);
                    Penguin before = null;
                    foreach (var e in c.world.Entities) if (e is Penguin q && !q.Dead) before = q;
                    if (before != null) before.Despawn();
                    yield return c.Frames(2);
                    b.SummonNow(c.p);
                    yield return c.Frames(2);
                    Penguin pg = null;
                    foreach (var e in c.world.Entities) if (e is Penguin q && !q.Dead) pg = q;
                    if (pg == null) { allNear = false; where += " none@" + px; continue; }
                    float d = Mathf.Abs(pg.pos.x - c.p.pos.x);
                    if (d < 3f || d > 8.5f) { allNear = false; where += $" {d:0.0}"; }
                    if (pg.pos.x < c.p.pos.x) left++; else right++;
                    c.Expect(pg.summoned, "the helper is marked as summoned");
                }
            bothSides = left > 0 && right > 0;
            c.Expect(allNear, "every helper comes up 4-7 cells from the player, in either corner of the arena" + where);
            c.Expect(bothSides, $"helpers come from both sides over time (left {left}, right {right})");
            ClearFight(c);
            b.Despawn();
        }

        static IEnumerator YetiKingMovesCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((6000, F())));
            c.Teleport(5.5f, 1f);
            var b = c.Spawn<YetiKing>("yetiking", 17, 1, 0, null, -1);
            c.Expect(b != null && b.maxHealth == 36, "the yeti king spawned with 36 health");
            if (b == null) yield break;
            yield return c.Frames(5);
            float tall = b.size.y;
            // his leap comes down with a wave of ice to either side
            b.LeapNow(c.p);
            for (int i = 0; i < 200 && b.Stomps == 0; i++) yield return new WaitForFixedUpdate();
            yield return c.Frames(2);
            c.Expect(b.Stomps == 1 && c.Count<BossShot>() >= 2, $"the landing of his leap sends a wave along the floor each way ({c.Count<BossShot>()} waves)");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is BossShot || e is Snowball || e is Penguin || e is RollingBall)) e.Despawn();
            // the slide: flat on his belly, low enough to jump, dazed by the wall
            yield return c.Frames(20);
            b.pos = new Vector2(17.5f, 1f); b.vel = Vector2.zero; b.facing = -1;
            c.Teleport(c.world.Grid.Width - 3.5f, 1f);      // out of his way, behind him
            b.SlideNow();
            for (int i = 0; i < 120 && !b.Sliding; i++) yield return new WaitForFixedUpdate();
            yield return c.Frames(3);
            c.Expect(b.Sliding && b.size.y < 1.5f, $"sliding, he lies {b.size.y:0.00} tall (standing {tall:0.00})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_yeti_slide.png"));
            for (int i = 0; i < 400 && b.Sliding; i++) yield return new WaitForFixedUpdate();
            c.Expect(!b.Sliding && b.Dazed && b.Rebounds == 0 && Mathf.Abs(b.size.y - tall) < 0.01f, $"the wall dazes him and he stands up again (rebounds {b.Rebounds}, {b.size.y:0.00} tall)");
            // at half health: the slide comes back off the first wall, the second one dazes him
            b.health = b.maxHealth / 2;
            for (int i = 0; i < 300 && b.Dazed; i++) yield return new WaitForFixedUpdate();
            foreach (var e in c.world.Entities) if (!e.Dead && (e is BossShot || e is Snowball || e is Penguin || e is RollingBall)) e.Despawn();
            b.pos = new Vector2(17.5f, 1f); b.vel = Vector2.zero; b.facing = -1;
            b.SlideNow();
            for (int i = 0; i < 120 && !b.Sliding; i++) yield return new WaitForFixedUpdate();
            bool came = false;
            for (int i = 0; i < 400 && b.Sliding && b.Rebounds == 0; i++) yield return new WaitForFixedUpdate();
            came = b.Rebounds == 1 && b.Sliding;
            for (int i = 0; i < 500 && b.Sliding; i++) yield return new WaitForFixedUpdate();
            c.Expect(b.Enraged && came && b.Rebounds == 1 && !b.Sliding && b.Dazed, $"enraged, he comes back off the first wall and the second dazes him (rebounds {b.Rebounds})");
            b.Despawn();
            ClearFight(c);
        }

        static IEnumerator YetiPunchCase(Ctx c)
        {
            c.run.RemoveUpgrade("warmcoat");
            c.Bot(Seq((3000, F())));
            c.run.health = 8;
            c.Teleport(8.5f, 1f);
            yield return c.Frames(70);
            var b = c.Spawn<YetiKing>("yetiking", 17, 1, 0, null, -1);
            c.Expect(b != null, "yeti king spawned");
            if (b == null) yield break;
            yield return c.Frames(10);
            float x0 = b.pos.x; int hp = c.run.health;
            c.p.Freeze(0.6f);
            bool rushed = false, hit = false;
            for (int i = 0; i < 120 && !hit; i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Punching) rushed = true;
                if (c.run.health < hp) hit = true;
                if (rushed && i % 10 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "yeti_punch.png"));
            }
            c.Expect(rushed, "the player is frozen: he runs in");
            c.Expect(hit && hp - c.run.health == b.punchDamage, $"and punches for {b.punchDamage} (health {hp} -> {c.run.health})");
            c.Expect(b.pos.x < x0 - 3f, $"he really crossed the room for it (x {x0:0.0} -> {b.pos.x:0.0})");
            // a player who is not frozen is not rushed
            b.Despawn();
            ClearFight(c);
            c.run.health = 8;
            c.Teleport(8.5f, 1f);
            yield return c.Frames(90);
            var b2 = c.Spawn<YetiKing>("yetiking", 17, 1, 0, null, -1);
            bool rushed2 = false;
            for (int i = 0; i < 80; i++) { yield return new WaitForFixedUpdate(); if (b2.Punching) rushed2 = true; }
            c.Expect(!rushed2, "a player who is not frozen is left to his usual round");
            b2.Despawn();
            ClearFight(c);
        }

        static IEnumerator BossPatternsCase(Ctx c)
        {
            c.p.godMode = true;
            foreach (var id in new[] { "broodmother", "forgebrute", "yetiking", "stormhawk", "warden" })
            {
                c.Teleport(5.5f, 1f);
                c.Bot(Seq((600, F())));
                var b = c.Spawn<Boss>(id, 17, 1);
                c.Expect(b != null, id + " spawned");
                if (b == null) continue;
                Vector2 p0 = b.pos;
                int n0 = c.world.EntityCount;
                bool acted = false, shots = false, rocks = false, balls = false, fire = false, boulder = false, wind = false, overheat = false;
                for (int i = 0; i < 3000; i++)       // long enough for every move to come up, whatever the (random) order
                {
                    yield return new WaitForFixedUpdate();
                    if ((b.pos - p0).sqrMagnitude > 0.5f || c.world.EntityCount > n0) acted = true;
                    if (i % 5 == 0)
                        foreach (var e in c.world.Entities)
                        {
                            if (e.Dead) continue;
                            if (e is BossShot) shots = true;
                            else if (e is Stalactite) rocks = true;
                            else if (e is Snowball) balls = true;
                            else if (e is FirePatch) fire = true;
                            else if (e is RollingBall) boulder = true;
                        }
                    if (i == 200 || (wind && i % 60 == 0) || (fire && i % 90 == 0)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_" + id + ".png"));
                }
                c.Expect(acted && !b.Dead, id + " moved or attacked and is still alive");
                // every boss has more than its two basic moves: the extra ones leave these traces within 30 seconds
                if (id == "forgebrute") c.Expect(fire && rocks && shots, $"forge brute brought fire up ({fire}), shook rocks loose on the wall ({rocks}) and landed a leap with a shockwave ({shots})");
                if (id == "yetiking") c.Expect(balls && rocks && boulder, $"yeti king threw snowballs ({balls}), shook icicles loose ({rocks}) and rolled a snow boulder ({boulder})");
                if (id == "stormhawk") c.Expect(shots, "stormhawk shot feathers");
                if (id == "warden") c.Expect(shots, "warden fired orbs");
                b.Despawn();
                ClearFight(c);
                yield return c.Frames(5);
            }
            c.p.godMode = false;
        }

        static IEnumerator ChestCase(Ctx c)
        {
            var owned = new System.Collections.Generic.HashSet<string>(c.run.upgrades);
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((200, F())));
            var chest = c.Spawn<Chest>("chest", 10, 1);
            c.Expect(chest != null && !chest.Opened, "chest spawned closed");
            if (chest == null) yield break;
            chest.Interact(c.p);
            yield return c.Frames(5);
            var items = new System.Collections.Generic.List<ShopItem>();
            foreach (var e in c.world.Entities) if (e is ShopItem si && !si.Dead) items.Add(si);
            c.Expect(chest.Opened && items.Count == 3, $"opening it lays out 3 items (got {items.Count})");
            bool free = true;
            foreach (var si in items) { var pr = si.GetPrompt(c.p); if (pr == null || !pr.Contains("free")) free = false; }
            c.Expect(free, "all three are free to take");
            int gold = c.run.gold;
            if (items.Count > 0) items[0].Interact(c.p);
            yield return c.Frames(3);
            c.Expect(items.Count > 0 && items[0].Dead && c.run.gold == gold, "taking one costs nothing");
            foreach (var si in items) if (!si.Dead) si.Despawn();
            chest.Despawn();
            foreach (var id in new System.Collections.Generic.List<string>(c.run.upgrades)) if (!owned.Contains(id)) c.run.RemoveUpgrade(id);
        }

        static IEnumerator ShopOnceCase(Ctx c)
        {
            var shop = c.world.config.shop;
            var had = new System.Collections.Generic.HashSet<string>(c.run.upgrades);
            c.Teleport(4.5f, 1f);
            c.Bot(Seq((200, F())));
            c.run.gold = 5000;
            var keeper = c.Spawn<Shopkeeper>("shopkeeper", 16, 1, 0, null, -1);
            var a = c.Spawn<ShopItem>("shopitem", 8, 1, 0, "springboots");
            var b = c.Spawn<ShopItem>("shopitem", 10, 1, 1, "springboots");
            yield return c.Frames(2);
            keeper.AddWare(a); keeper.AddWare(b);
            yield return c.Frames(3);
            c.Expect(a.offer.id == "springboots" && !b.Dead && b.offer.id != "springboots" && b.offer.isUpgrade,
                $"the twin on the shelf became another piece ({a.offer.id} / {b.offer?.id})");
            // owning a piece: the one on display leaves, and it is never stocked again
            var free = c.Spawn<ShopItem>("shopitem", 12, 1, 2, "compass");
            yield return c.Frames(2);
            c.run.AddUpgrade(shop.Find("compass"));
            yield return c.Frames(3);
            c.Expect(free.Dead, "an item bought elsewhere disappears from display");
            var again = c.Spawn<ShopItem>("shopitem", 12, 1, 2, "compass");
            yield return c.Frames(2);
            c.Expect(again != null && again.offer != null && again.offer.id != "compass", $"an owned piece is not stocked (shows {again?.offer?.id})");
            int gold = c.run.gold;
            a.Interact(c.p);
            yield return c.Frames(3);
            c.Expect(a.Dead && c.run.HasUpgrade("springboots") && c.run.gold == gold - shop.Find("springboots").price, "buying equipment charges once");
            // supplies stay on sale however many the run has
            var bombs = c.Spawn<ShopItem>("shopitem", 6, 1, 3, "bombs");
            yield return c.Frames(3);
            c.Expect(bombs != null && !bombs.Dead && bombs.offer.id == "bombs", "supplies can always be bought again");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is ShopItem || e is Shopkeeper)) e.Despawn();
            foreach (var id in new System.Collections.Generic.List<string>(c.run.upgrades)) if (!had.Contains(id)) c.run.RemoveUpgrade(id);
            c.run.shopkeeperDead = false; c.run.shopkeeperWanted = false;
            yield return c.Frames(2);
        }

        static IEnumerator MinionDropCase(Ctx c)
        {
            c.Teleport(4.5f, 1f);
            c.p.godMode = true;
            c.Bot(Seq((3000, F())));
            var boss = c.Spawn<Broodmother>("broodmother", 16, 1);
            // her brood: an egg sac left alone hatches a spiderling
            Spider minion = null;
            for (int i = 0; i < 2600 && minion == null; i++)
            {
                yield return c.Frames(1);
                foreach (var e in c.world.Entities) if (e is Spider sp && !sp.Dead) { minion = sp; break; }
            }
            c.Expect(minion != null && minion.summoned, "the boss's brood (a spiderling out of her egg) is marked as summoned");
            if (boss != null) boss.Despawn();
            if (minion != null)
            {
                c.Teleport(4.5f, 1f);
                int kills = c.run.kills, drops = c.Count<Pickup>();
                var at = minion.pos;
                for (int i = 0; i < 10 && !minion.Dead; i++)
                    minion.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(2);
                c.Expect(minion.Dead && c.run.kills == kills + 1, "the minion dies and counts as a kill");
                c.Expect(c.Count<Pickup>() == drops, $"it drops nothing (pickups {drops} -> {c.Count<Pickup>()}, killed at {at})");
            }
            ClearFight(c);
            yield return c.Frames(2);
            // control: the same creature met in a level still pays
            var wild = c.Spawn<Bat>("bat", 18, 4);
            if (wild != null) wild.goldChance = 1f;         // (a kill does not always pay: here it must, to tell it from a minion)
            yield return c.Frames(2);
            int before = c.Count<Pickup>();
            if (wild != null) for (int i = 0; i < 10 && !wild.Dead; i++) wild.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(2);
            c.Expect(!ReferenceEquals(wild, null) && wild.Dead && c.Count<Pickup>() > before, $"an ordinary bat still drops gold (pickups {before} -> {c.Count<Pickup>()})");
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup) e.Despawn();
            yield return c.Frames(2);
            // a kill pays a heart or gold, never both
            for (int pass = 0; pass < 2; pass++)
            {
                var bat = c.Spawn<Bat>("bat", 18, 4);
                bat.heartDropChance = pass == 0 ? 1f : 0f;
                bat.goldChance = 1f;           // (a kill does not always pay: the second pass must, to tell gold from a heart)
                yield return c.Frames(2);
                bat.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(2);
                int gold = 0, other = 0;
                foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup pk) { if (pk.resource == Res.Gold) gold++; else other++; }
                if (pass == 0) c.Expect(other == 1 && gold == 0, $"a heart drop comes without gold (hearts {other}, gold {gold})");
                else c.Expect(other == 0 && gold > 0, $"no heart: gold only (hearts {other}, gold {gold})");
                foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup) e.Despawn();
                yield return c.Frames(2);
            }
            ClearFight(c);
            c.p.godMode = false;
            yield return c.Frames(2);
        }

        static IEnumerator MaxHealthCase(Ctx c)
        {
            c.Expect(c.world.config.player.startHealth == 10, $"a run starts with 10 health (config says {c.world.config.player.startHealth})");
            c.run.maxHealth = 10; c.run.health = 10;
            c.Teleport(4.5f, 1f);
            // full health: the heart on the way is left lying, and the tonic is not sold
            var heart = c.Spawn<Pickup>("heart", 7, 1);
            c.Bot(Seq((40, F(x: 1)), (20, F())));
            yield return c.Frames(62);
            c.Expect(c.run.health == 10 && !heart.Dead, $"at full health a heart is not picked up (health {c.run.health})");
            c.run.gold = 2000;
            var tonic = c.Spawn<ShopItem>("shopitem", 12, 1, 0, "heart");
            yield return c.Frames(2);
            tonic.Interact(c.p);
            yield return c.Frames(2);
            c.Expect(c.run.gold == 2000 && c.run.health == 10 && !tonic.Dead, "a tonic cannot be bought at full health (no gold spent)");
            tonic.Despawn();
            // hurt: the same heart now heals, and never past the limit
            c.run.health = 9;
            c.Teleport(4.5f, 1f);
            c.Bot(Seq((40, F(x: 1)), (20, F())));
            yield return c.Frames(62);
            c.Expect(c.run.health == 10 && heart.Dead, $"hurt: the heart heals 1 (health {c.run.health})");
            c.run.AddResource(Res.Health, 5);
            c.Expect(c.run.health == 10, $"health never goes past the limit (health {c.run.health})");

            // a boss pays a heart vessel and gold, no plain heart
            c.Teleport(4.5f, 1f);
            c.p.godMode = true;
            c.Bot(Seq((400, F())));
            c.run.health = 3;
            var boss = c.Spawn<Broodmother>("broodmother", 18, 1);
            yield return c.Frames(2);
            for (int i = 0; i < 60 && !boss.Dead; i++)
            {
                boss.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(14);
            }
            yield return c.Frames(3);
            int vessels = 0, hearts = 0, gold = 0;
            Pickup vessel = null;
            foreach (var e in c.world.Entities)
                if (!e.Dead && e is Pickup pk)
                {
                    if (pk.resource == Res.MaxHealth) { vessels++; vessel = pk; }
                    else if (pk.resource == Res.Health) hearts++;
                    else if (pk.resource == Res.Gold) gold++;
                }
            c.Expect(boss.Dead && vessels == 1 && hearts == 0 && gold == boss.goldChunks, $"boss drop: 1 vessel, no heart, {boss.goldChunks} gold (got {vessels} / {hearts} / {gold})");
            ClearFight(c);
            if (vessel != null)
            {
                yield return c.Frames(20);
                vessel.Collect(c.p);
                yield return c.Frames(2);
                c.Expect(c.run.maxHealth == 11 && c.run.health == 11, $"the vessel adds 1 max health and heals fully (health {c.run.health}/{c.run.maxHealth})");
            }
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup) e.Despawn();
            c.run.maxHealth = 10; c.run.health = 4;
            c.p.godMode = false;
            yield return c.Frames(2);
        }

        static IEnumerator AttackOnlyCase(Ctx c)
        {
            c.Bot(Seq((600, F())));
            // a yeti has no close-range attack at all: standing inside it costs nothing
            var yeti = c.Spawn<Yeti>("yeti", 10, 1);
            yield return c.Frames(2);
            int h0 = c.run.health;
            c.Teleport(yeti.pos.x, 1f);
            yield return c.Frames(60);
            c.Expect(c.run.health == h0 && !yeti.Attacking, $"touching a yeti for a second is harmless (health {h0} -> {c.run.health})");
            yeti.Despawn();

            // a snake: nothing on touch, nothing during the wind-up, then the bite
            c.Teleport(14.5f, 1f);
            yield return c.Frames(2);
            var snake = c.Spawn<Snake>("snake", 15, 1);
            snake.heartDropChance = 0f;
            h0 = c.run.health;
            bool attackedEarly = false, struck = false;
            int hitFrame = -1, firstStrike = -1;
            for (int i = 0; i < 180 && hitFrame < 0; i++)
            {
                yield return c.Frames(1);
                if (snake.Attacking && firstStrike < 0) firstStrike = i;
                if (snake.Attacking) struck = true;
                if (c.run.health < h0) { hitFrame = i; if (!struck) attackedEarly = true; }
            }
            c.Expect(firstStrike >= 12, $"the bite comes after a wind-up, not on touch (first strike frame {firstStrike})");
            c.Expect(hitFrame >= 0 && !attackedEarly && h0 - c.run.health == snake.contactDamage,
                $"the bite itself costs {snake.contactDamage} (health {h0} -> {c.run.health}, hit frame {hitFrame}, before any strike: {attackedEarly})");
            snake.Despawn();
            c.run.health = 4;
            yield return c.Frames(2);
        }

        static IEnumerator ShockwaveCase(Ctx c)
        {
            // standing still in its path
            c.Teleport(14.5f, 1f);
            c.Bot(Seq((200, F())));
            yield return c.Frames(2);
            int h0 = c.run.health;
            var w = c.Spawn<BossShot>("shockwave", 8, 1);
            c.Expect(w != null, "shockwave prefab exists");
            if (w == null) yield break;
            w.pos = new Vector2(8.5f, 1.2f); w.vel = new Vector2(7.5f, 0f);
            yield return c.Frames(20);
            c.Expect(!ReferenceEquals(w, null) && !w.Dead && w.pos.x > 10f, $"it runs along the floor without bursting (x {w.pos.x:0.0}, dead {w.Dead})");
            yield return c.Frames(60);
            c.Expect(h0 - c.run.health == w.damage && w.damage == 2, $"standing in its way costs {w.damage} (health {h0} -> {c.run.health})");
            // jumping over the next one
            yield return c.Frames(90);            // let the invulnerability run out
            c.Teleport(14.5f, 1f);
            h0 = c.run.health;
            var w2 = c.Spawn<BossShot>("shockwave", 8, 1);
            w2.pos = new Vector2(8.5f, 1.2f); w2.vel = new Vector2(7.5f, 0f);
            // it needs ~0.7 s to arrive: jump just before
            c.Bot(Seq((34, F()), (1, F(jp: true, jh: true)), (30, F(jh: true)), (60, F())));
            yield return c.Frames(125);
            c.Expect(c.run.health == h0, $"a jump clears it (health {h0} -> {c.run.health})");
            foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
            c.run.health = 4;
            yield return c.Frames(2);
        }

        static IEnumerator GrudgeCase(Ctx c)
        {
            var run = c.run;
            run.shopkeeperWanted = false; run.shopkeeperDead = false;
            c.p.godMode = true;
            c.Bot(Seq((900, F())));
            c.Teleport(4.5f, 1f);

            // 1) anger him: the run is marked, he is still alive
            var k = c.Spawn<Shopkeeper>("shopkeeper", 14, 1, 0, null, -1);
            for (int i = 0; i < 3; i++)
            {
                k.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(50);
            }
            c.Expect(k.angry && run.shopkeeperWanted && !run.shopkeeperDead, "angering the shopkeeper marks the run as wanted");
            k.Despawn();                       // the player leaves the level; he survives
            yield return c.Frames(3);

            // 2) the next shop: calm from afar, an ambush with two brothers when you walk in
            var k2 = c.Spawn<Shopkeeper>("shopkeeper", 21, 1, 0, null, -1);
            yield return c.Frames(10);
            c.Expect(!k2.angry && c.Count<Shopkeeper>() == 1, "he waits while the player is far away");
            c.Teleport(17.5f, 1f);
            yield return c.Frames(20);
            c.Expect(k2.angry && c.Count<Shopkeeper>() == 3, $"walking in triggers the ambush: {c.Count<Shopkeeper>()} shopkeepers");

            // 3) kill him: no shopkeeper ever again in this run
            for (int i = 0; i < 20 && !k2.Dead; i++)
            {
                k2.TakeDamage(new DamageInfo(99, DamageType.Explosion, c.p, Vector2.zero, "test"));
                yield return c.Frames(2);
            }
            c.Expect(k2.Dead && run.shopkeeperDead && !run.shopkeeperWanted, "killing him closes the shops for good");
            foreach (var e in c.world.Entities) if (e is Shopkeeper && !e.Dead) e.Despawn();
            yield return c.Frames(3);
            var k3 = c.Spawn<Shopkeeper>("shopkeeper", 14, 1, 0, null, -1);
            yield return c.Frames(3);
            c.Expect(k3 == null || k3.Dead, "a later shop has no shopkeeper");

            run.shopkeeperWanted = false; run.shopkeeperDead = false;
            c.p.godMode = false;
        }

        static IEnumerator ClimbTrapCase(Ctx c)
        {
            // rotten platform two tiles up: stand on it -> it breaks -> the player drops -> it comes back
            c.Bot(Seq((900, F())));
            var cp = c.Spawn<CrumblePlatform>("crumble", 8, 3);
            c.Expect(cp != null && c.world.Grid.Get(8, 3) == TileType.Platform, "the rotten platform owns a platform tile");
            if (cp == null) yield break;
            c.Teleport(8.5f, 4f);
            yield return c.Frames(12);
            c.Expect(c.p.grounded && c.p.pos.y > 3.9f, "the player stands on it at first");
            yield return c.Frames(50);
            c.Expect(cp.Broken && c.world.Grid.IsEmpty(8, 3) && c.p.pos.y < 3.5f, "after a moment it gives way and the player falls");
            c.Teleport(4.5f, 1f);
            yield return c.Frames(260);
            c.Expect(!cp.Broken && c.world.Grid.Get(8, 3) == TileType.Platform, "a few seconds later it is back");
            cp.Despawn();
            c.world.Grid.Set(8, 3, TileType.Empty);

            // rock dropper in the ceiling above a ladder: quiet while nobody is below, drops when a climber is
            var rd = c.Spawn<RockDropper>("rockdropper", 16, 7);
            c.Expect(rd != null, "rock dropper spawned under the ceiling block");
            if (rd == null) yield break;
            yield return c.Frames(120);
            c.Expect(c.Count<Stalactite>() == 0, "nothing falls while the player is elsewhere");
            c.p.godMode = true;
            c.Teleport(16.5f, 1f);
            bool warned = false, dropped = false;
            for (int i = 0; i < 240 && !dropped; i++)
            {
                yield return new WaitForFixedUpdate();
                if (rd.Warning) warned = true;
                if (c.Count<Stalactite>() > 0) dropped = true;
            }
            c.Expect(warned && dropped, "with the player below it rattles, then drops a rock");
            rd.Despawn();
            foreach (var e in c.world.Entities) if (e is Stalactite && !e.Dead) e.Despawn();
            c.p.godMode = false;
        }

        static IEnumerator TitleCardCase(Ctx c)
        {
            var hud = c.dir.hud;
            // the case from the final vault: the title card is still up when the collapse banner arrives
            hud.ShowAreaIntro("The Sunken Vault");
            yield return new WaitForSecondsRealtime(0.8f);
            c.Expect(hud.introGroup.alpha > 0.9f && hud.bannerGroup.alpha < 0.01f, "the title card shows on its own");
            hud.ShowBanner("THE VAULT IS COLLAPSING!", "Carry the Sunstone back up to the Surface Gate!");
            yield return new WaitForSecondsRealtime(0.3f);
            c.Expect(hud.introGroup.alpha < 0.01f && hud.bannerGroup.alpha > 0.9f, $"the banner replaces the card (card {hud.introGroup.alpha:0.00}, banner {hud.bannerGroup.alpha:0.00})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "banner_over_card.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            // and the other way round
            hud.ShowAreaIntro("Mossroot Caverns");
            yield return new WaitForSecondsRealtime(0.8f);
            c.Expect(hud.introGroup.alpha > 0.9f && hud.bannerGroup.alpha < 0.01f, "a new title card clears the banner");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "title_card.png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static IEnumerator Keys(Keyboard kb, float wait, params Key[] down)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(down));
            yield return new WaitForSecondsRealtime(wait);
        }

        // Real key presses through the Input System (virtual keyboard) against the real action asset and the real screen.
        static IEnumerator KeyChangeCase(Ctx c)
        {
            var saved = new System.Collections.Generic.List<KeyBind>(SaveSystem.Settings.keys ?? new System.Collections.Generic.List<KeyBind>());
            var prevBg = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var kb = InputSystem.AddDevice<Keyboard>("ScenarioKeys");
            var map = c.dir.input.actions.FindActionMap("Player", true);
            InputAction jump = map.FindAction("Jump"), attack = map.FindAction("Attack"), use = map.FindAction("Use"), move = map.FindAction("Move");
            KeyBindings.ResetAll();

            yield return Keys(kb, 0.1f, Key.Space);
            c.Expect(jump.IsPressed(), "default: Space jumps");
            yield return Keys(kb, 0.1f);

            KeyBindings.Assign("jump", "k");
            yield return Keys(kb, 0.1f, Key.K);
            c.Expect(jump.IsPressed(), "after the change K jumps");
            yield return Keys(kb, 0.1f, Key.Space);
            c.Expect(!jump.IsPressed(), "and Space no longer does");
            yield return Keys(kb, 0.1f);
            c.Expect(KeyBindings.GlyphLabel("jump", out var lbl) && lbl == "K", "the [jump] picture now reads K (got " + lbl + ")");

            // a key that another action already has: the two swap
            var swapped = KeyBindings.Assign("attack", "k");
            c.Expect(swapped != null && swapped.id == "jump" && KeyBindings.KeyOf("jump") == "q" && KeyBindings.KeyOf("attack") == "k",
                $"giving K to the blade swaps: jump {KeyBindings.KeyOf("jump")}, blade {KeyBindings.KeyOf("attack")}");
            yield return Keys(kb, 0.1f, Key.Q);
            c.Expect(jump.IsPressed() && !attack.IsPressed(), "Q jumps now, it does not swing the blade");
            yield return Keys(kb, 0.1f);

            // P is an extra pause key until the player gives it another job
            var pause = map.FindAction("Pause");
            KeyBindings.Busy = true;            // (the action is read, the game itself is not paused by it)
            yield return Keys(kb, 0.1f, Key.P);
            c.Expect(pause.IsPressed(), "P still pauses as an extra key");
            yield return Keys(kb, 0.1f);
            KeyBindings.Busy = false;
            KeyBindings.Assign("use", "p");
            yield return Keys(kb, 0.1f, Key.P);
            c.Expect(use.IsPressed() && !pause.IsPressed(), "P given to 'use gear': it uses gear and no longer pauses");
            yield return Keys(kb, 0.1f);

            c.Expect(!KeyBindings.Allowed("escape") && KeyBindings.Assign("jump", "escape") == null && KeyBindings.KeyOf("jump") == "q", "Esc cannot be given to an action");

            // the screen itself, from the pause menu: choose the 'Up' row, press W
            c.dir.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.1f);
            var pm = c.dir.pauseMenu;
            pm.OpenKeys();
            var panel = pm.keysPanel;
            c.Expect(panel != null && panel.gameObject.activeInHierarchy, "the change-keys screen opens from the pause menu");
            if (panel != null)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                panel.Begin(0);
                yield return new WaitForSecondsRealtime(0.15f);
                c.Expect(panel.Listening && KeyBindings.Busy, "choosing a row waits for a key");
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "keys_listening.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                // Esc while it waits cancels the change - and must not close the pause menu
                yield return Keys(kb, 0.1f, Key.Escape);
                yield return Keys(kb, 0.2f);
                c.Expect(!panel.Listening && c.dir.Paused && panel.gameObject.activeInHierarchy && KeyBindings.KeyOf("up") == "upArrow", "Esc cancels and the screen stays open");
                panel.Begin(0);
                yield return new WaitForSecondsRealtime(0.15f);
                yield return Keys(kb, 0.1f, Key.W);
                yield return Keys(kb, 0.2f);
                c.Expect(KeyBindings.KeyOf("up") == "w" && !KeyBindings.Busy, "pressing W on the screen makes W the 'up' key (got " + KeyBindings.KeyOf("up") + ")");
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "keys_changed.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Keys(kb, 0.1f, Key.W);
                c.Expect(move.ReadValue<Vector2>().y > 0.5f, "W now moves up");
                yield return Keys(kb, 0.1f, Key.UpArrow);
                c.Expect(move.ReadValue<Vector2>().y < 0.5f, "the up arrow no longer does");
                yield return Keys(kb, 0.1f);
            }
            c.dir.SetPaused(false);

            KeyBindings.ResetAll();
            yield return Keys(kb, 0.1f, Key.Space);
            c.Expect(KeyBindings.AllDefault && jump.IsPressed(), "defaults: Space jumps again");
            yield return Keys(kb, 0.1f);

            KeyBindings.Restore(saved);         // whatever the player had set up on this machine
            InputSystem.RemoveDevice(kb);
            InputSystem.settings.backgroundBehavior = prevBg;
            InputDeviceTracker.ResetToKeyboard();
            yield return c.Frames(5);
        }

        static IEnumerator BounceCapCase(Ctx c)
        {
            var cap = c.Spawn<BounceCap>("bouncecap", 12, 1);
            c.Expect(cap != null, "bounce cap spawned");
            if (cap == null) yield break;
            c.Bot(Seq((400, F())));
            yield return c.Frames(2);
            int hp0 = c.run.health;
            // a fall that would hurt on bare floor (the plain long-fall case uses the same height)
            c.Teleport(cap.pos.x, 1f + 10.5f);
            float top = 0f; bool up = false;
            for (int i = 0; i < 200; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.vel.y > 15f) up = true;
                if (up) top = Mathf.Max(top, c.p.pos.y);
                if (up && c.p.vel.y < -1f && i > 60) break;
            }
            c.Expect(up, "the cap threw the player up");
            c.Expect(top > 5.2f && top < 7.5f, $"a bounce of about 4.6 tiles (peak y {top:0.00})");
            c.Expect(c.run.health == hp0, $"no fall damage (health {hp0} -> {c.run.health})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "bounce_cap.png"));
            yield return new WaitForEndOfFrame();       // the picture is taken at the end of this frame: nothing may move before
            yield return null;
            // the same with jump held: higher
            c.Bot(Seq((400, F(jh: true))));
            c.Teleport(cap.pos.x, 4f);
            float topHeld = 0f; up = false;
            for (int i = 0; i < 200; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.vel.y > 15f) up = true;
                if (up) topHeld = Mathf.Max(topHeld, c.p.pos.y);
                if (up && c.p.vel.y < -1f && i > 60) break;
            }
            c.Expect(topHeld > top + 1f, $"holding jump bounces higher ({topHeld:0.00} vs {top:0.00})");
            // walking into it from the side does nothing
            c.Bot(Seq((200, F())));
            c.Teleport(cap.pos.x - 0.3f, 1f);
            yield return c.Frames(40);
            c.Expect(c.p.grounded && c.p.pos.y < 1.2f, "standing next to / inside the cap does not bounce");
            cap.Despawn();
        }

        static IEnumerator PuffshroomCase(Ctx c)
        {
            c.run.RemoveUpgrade("sporemask");
            c.Teleport(9.2f, 1f);
            c.Bot(Seq((600, F())));
            var ps = c.Spawn<Puffshroom>("puffshroom", 10, 1);
            c.Expect(ps != null, "puffshroom spawned");
            if (ps == null) yield break;
            int hp0 = c.run.health;
            bool swelled = false, cloud = false, dizzy = false;
            for (int i = 0; i < 240 && !dizzy; i++)
            {
                yield return new WaitForFixedUpdate();
                if (ps.Swelling) swelled = true;
                if (c.Count<SporeCloud>() > 0) cloud = true;
                if (c.p.Confused) dizzy = true;
            }
            c.Expect(swelled, "it swells first (the tell)");
            c.Expect(cloud, "then bursts a spore cloud");
            c.Expect(dizzy, "the player in the cloud is dizzy");
            c.Expect(c.run.health == hp0, $"spores do no damage (health {hp0} -> {c.run.health})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "spore_cloud.png"));
            yield return new WaitForEndOfFrame();       // the picture is taken at the end of this frame: nothing may move before
            yield return null;
            // dizzy: pressing right walks left. Out of the cloud first, so the dizziness only runs down.
            ps.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is SporeCloud) e.Despawn();
            c.p.Confuse(1.2f);
            c.Teleport(12.5f, 1f);
            float x0 = 12.5f;
            c.Bot(Seq((30, F(x: 1)), (120, F())));
            yield return c.Frames(30);
            c.Expect(c.p.pos.x < x0 - 0.5f, $"dizzy: holding right moves left (x {x0:0.00} -> {c.p.pos.x:0.00})");
            yield return c.Frames(115);         // up to 2 s of dizziness from the cloud itself
            c.Expect(!c.p.Confused, "the dizziness wears off");
            float x1 = c.p.pos.x;
            c.Bot(Seq((30, F(x: 1)), (60, F())));
            yield return c.Frames(30);
            c.Expect(c.p.pos.x > x1 + 0.5f, "afterwards right is right again");
            // killing it also releases a cloud
            var ps2 = c.Spawn<Puffshroom>("puffshroom", 20, 1);
            ps2.heartDropChance = 0f;
            yield return c.Frames(3);
            ps2.TakeDamage(new DamageInfo(5, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(2);
            c.Expect(c.Count<SporeCloud>() > 0, "a puffshroom that dies puffs too");
            foreach (var e in c.world.Entities) if (!e.Dead && e is SporeCloud) e.Despawn();
            // with a spore mask the cloud does nothing
            c.run.AddUpgrade(c.dir.config.shop.Find("sporemask"));
            c.Expect(c.run.IsWorn("sporemask") && c.run.stats.sporeResist > 0f, "the spore mask is worn");
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((200, F())));
            c.world.SpawnById("sporecloud", c.p.Center);
            yield return c.Frames(40);
            c.Expect(!c.p.Confused, "with the mask on, standing in a cloud does not make the player dizzy");
            foreach (var e in c.world.Entities) if (!e.Dead && e is SporeCloud) e.Despawn();
            c.run.RemoveUpgrade("sporemask");
        }

        static IEnumerator MothCase(Ctx c)
        {
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((1200, F())));
            c.p.godMode = false;
            var moth = c.Spawn<GloomMoth>("moth", 11, 6);
            c.Expect(moth != null, "moth spawned");
            if (moth == null) yield break;
            int hp0 = c.run.health;
            bool woke = false, drank = false;
            float low = 1f;
            for (int i = 0; i < 360; i++)
            {
                yield return new WaitForFixedUpdate();
                if (moth.IsAwake) woke = true;
                if (moth.Draining) drank = true;
                low = Mathf.Min(low, LevelLighting.PlayerLightScale);
            }
            c.Expect(woke, "the moth wakes when the light comes near");
            c.Expect(drank, "it flutters around the player and drinks the light");
            c.Expect(low < 0.6f, $"the player's light shrinks (down to {low:0.00} of normal)");
            c.Expect(c.run.health == hp0, "the moth does no damage");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "moth_dim.png"));
            yield return new WaitForSecondsRealtime(0.1f);
            moth.heartDropChance = 0f;
            moth.TakeDamage(new DamageInfo(5, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(moth.Dead, "one hit kills it");
            yield return new WaitForSecondsRealtime(1.6f);
            c.Expect(LevelLighting.PlayerLightScale > 0.95f, $"the light comes back ({LevelLighting.PlayerLightScale:0.00})");
        }

        static IEnumerator LanternCase(Ctx c)
        {
            c.run.RemoveUpgrade("lantern");
            c.Bot(Seq((400, F())));
            yield return new WaitForSecondsRealtime(0.3f);
            float bare = LevelLighting.PlayerLightRadius;
            c.Expect(bare > 1f, $"the player carries a light (reach {bare:0.0})");
            c.run.AddUpgrade(c.dir.config.shop.Find("lantern"));
            c.Expect(c.run.IsWorn("lantern") && c.run.stats.lightBonus > 0f, "the lantern is worn");
            yield return new WaitForSecondsRealtime(0.3f);
            float lit = LevelLighting.PlayerLightRadius;
            c.Expect(lit > bare * 1.5f, $"the light reaches further with it ({bare:0.0} -> {lit:0.0})");
            // it shares the charm slots with the spore mask: both can be worn at once
            c.run.RemoveUpgrade("sporemask");
            c.run.AddUpgrade(c.dir.config.shop.Find("sporemask"));
            c.Expect(c.run.IsWorn("lantern") && c.run.IsWorn("sporemask"), "lantern and spore mask are worn together");
            c.run.RemoveUpgrade("lantern");
            c.run.RemoveUpgrade("sporemask");
            yield return new WaitForSecondsRealtime(0.3f);
            c.Expect(Mathf.Abs(LevelLighting.PlayerLightRadius - bare) < 0.2f, "taken off: the light is back to normal");
        }

        static IEnumerator LurecapCase(Ctx c)
        {
            c.Teleport(6.5f, 1f);
            c.Bot(Seq((900, F())));
            var lc = c.Spawn<Lurecap>("lurecap", 14, 1);
            c.Expect(lc != null, "lurecap spawned");
            if (lc == null) yield break;
            int hp0 = c.run.health;
            yield return c.Frames(90);
            c.Expect(!lc.IsAwake && !lc.IsStompable && lc.ContactDamage == 0, "far away it just sits there glowing: asleep, harmless");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "lurecap_asleep.png"));
            yield return new WaitForSecondsRealtime(0.1f);
            // touching it while it sleeps would be harmless too, but coming close wakes it
            c.Teleport(lc.pos.x - 1.5f, 1f);
            yield return c.Frames(4);
            c.Expect(lc.IsAwake, "it wakes when the player walks up");
            c.Expect(lc.ContactDamage == 0, "standing up is only the tell: no damage yet");
            bool leapt = false, hit = false;
            for (int i = 0; i < 300 && !hit; i++)
            {
                yield return new WaitForFixedUpdate();
                if (!lc.Dead && !lc.grounded && lc.IsAwake) leapt = true;
                if (c.run.health < hp0) hit = true;
            }
            c.Expect(leapt, "then it leaps after the player");
            c.Expect(hit, $"and the leap hurts (health {hp0} -> {c.run.health})");
            if (!lc.Dead) lc.Despawn();
        }

        static IEnumerator ConveyorCase(Ctx c)
        {
            c.run.RemoveUpgrade("heavyboots");
            var belts = new List<Conveyor>();
            for (int x = 8; x <= 13; x++) belts.Add(c.Spawn<Conveyor>("conveyor", x, 1, 0, null, 1));
            c.Expect(belts[0] != null && belts[0].dir == 1, "belt spawned, running right");
            c.Bot(Seq((2000, F())));
            c.Teleport(8.5f, 1f);
            var snake = c.Spawn<Snake>("snake", 10, 1);
            snake.stunTimer = 99f;                  // it does not walk: any movement is the belt's
            float sx = snake.pos.x;
            yield return c.Frames(50);
            c.Expect(c.p.pos.x > 8.5f + 1.5f, $"standing still on the belt, the player is carried along (x 8.50 -> {c.p.pos.x:0.00})");
            c.Expect(snake.pos.x > sx + 1.5f, $"so is a creature standing on it (x {sx:0.00} -> {snake.pos.x:0.00})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "conveyor.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            // off the end of the belt nothing moves any more
            yield return c.Frames(150);
            float rest = c.p.pos.x;
            yield return c.Frames(30);
            c.Expect(c.p.pos.x > 13.9f && Mathf.Abs(c.p.pos.x - rest) < 0.05f, $"past the end of the belt the player stays put (x {c.p.pos.x:0.00})");
            // reversed: it runs the other way
            foreach (var b in belts) b.Reverse();
            c.Teleport(12.5f, 1f);
            yield return c.Frames(50);
            c.Expect(c.p.pos.x < 12.5f - 1.5f, $"reversed, it carries the other way (x 12.50 -> {c.p.pos.x:0.00})");
            // heavy boots: not carried
            c.run.AddUpgrade(c.dir.config.shop.Find("heavyboots"));
            c.Expect(c.run.IsWorn("heavyboots") && c.run.stats.beltGrip > 0f, "heavy boots are worn");
            c.Teleport(11.5f, 1f);
            yield return c.Frames(50);
            c.Expect(Mathf.Abs(c.p.pos.x - 11.5f) < 0.05f, $"with heavy boots the belt does not move the player (x {c.p.pos.x:0.00})");
            c.run.RemoveUpgrade("heavyboots");
            snake.Despawn();
            foreach (var b in belts) b.Despawn();
        }

        static IEnumerator SentryCase(Ctx c)
        {
            c.run.RemoveUpgrade("magnetcharm");
            c.p.godMode = true;
            c.Bot(Seq((2000, F())));
            c.Teleport(9.5f, 1f);
            var s = c.Spawn<Sentry>("sentry", 12, 1, 0, null, -1);
            c.Expect(s != null && s.facing == -1, "sentry spawned, facing the player");
            if (s == null) yield break;
            s.heartDropChance = 0f;
            yield return c.Frames(3);
            int hp = s.health;
            bool hit = s.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(!hit && s.health == hp, "a blade from the front rings off the shield");
            c.Expect(!s.Blocks(new DamageInfo(1, DamageType.Stomp, c.p, Vector2.zero, "test")) && !s.Blocks(new DamageInfo(3, DamageType.Explosion, c.p, Vector2.zero, "test")),
                "a stomp or a blast is not stopped by the shield");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sentry.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            // behind it: the back is open, and it needs a moment to turn
            c.Teleport(14.5f, 1f);
            yield return c.Frames(4);
            c.Expect(s.facing == -1, "it does not turn at once");
            hit = s.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(hit && s.health == hp - 1, "a blade in its back hurts it");
            yield return c.Frames(90);
            c.Expect(s.facing == 1, "after a moment it has turned to face the player");
            // with the magnet charm the blade goes through the shield
            hit = s.TakeDamage(new DamageInfo(0, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(!hit, "facing the player again, the shield blocks again");
            c.run.AddUpgrade(c.dir.config.shop.Find("magnetcharm"));
            c.Expect(c.run.IsWorn("magnetcharm") && !s.Blocks(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")), "with the magnet charm the blade cuts through the shield");
            c.run.RemoveUpgrade("magnetcharm");
            // its jab hurts, after a wind-up
            c.p.godMode = false;
            int php = c.run.health;
            c.Teleport(s.pos.x + 1.0f, 1f);
            bool hurt = false;
            for (int i = 0; i < 200 && !hurt; i++) { yield return new WaitForFixedUpdate(); if (c.run.health < php) hurt = true; }
            c.Expect(hurt, "standing in front of it, the player gets jabbed");
            c.p.godMode = true;
            s.Despawn();
        }

        static IEnumerator DroneBeetleCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((3000, F())));
            c.Teleport(6.5f, 1f);
            var d = c.Spawn<Drone>("drone", 11, 5);
            c.Expect(d != null, "drone spawned");
            if (d != null)
            {
                d.heartDropChance = 0f;
                float y0 = d.pos.y;
                bool aimed = false, fired = false, aimedFirst = false;
                for (int i = 0; i < 400 && !fired; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (d.Aiming) aimed = true;
                    if (c.Count<BossShot>() > 0) { fired = true; aimedFirst = aimed; }
                    if (aimed && !fired && i % 30 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "drone.png"));
                }
                c.Expect(aimedFirst, "its eye turns red before it shoots (the tell)");
                c.Expect(fired, "then it fires a bolt");
                c.Expect(Mathf.Abs(d.pos.y - y0) < 0.4f && d.ContactDamage == 0, "it stays at its post and touching it is harmless");
                d.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
                c.Expect(d.Dead, "one hit brings it down");
                foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
            }
            // the beetle and the bomb
            var bt = c.Spawn<ScrapBeetle>("beetle", 15, 1, 0, null, -1);
            c.Expect(bt != null, "scrap beetle spawned");
            if (bt == null) yield break;
            bt.heartDropChance = 0f;
            yield return c.Frames(20);
            var bomb = c.Spawn<Bomb>("bomb", 12, 1);
            bomb.Light(c.p, 0f);
            bool ate = false;
            for (int i = 0; i < 140 && !ate; i++) { yield return new WaitForFixedUpdate(); if (bt.Primed) ate = true; }
            c.Expect(ate, "the beetle runs to the lit bomb and swallows it");
            c.Expect(bomb.Dead, "the bomb is gone");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "beetle_primed.png"));
            float bx = bt.pos.x;
            bool blew = false;
            for (int i = 0; i < 200 && !blew; i++) { yield return new WaitForFixedUpdate(); if (bt.Dead) blew = true; else bx = bt.pos.x; }
            c.Expect(blew, "a moment later it blows up");
            c.Expect(bx < 11.5f, $"having run at the player first (x {bx:0.0})");
        }

        static IEnumerator GeodeTrapCase(Ctx c)
        {
            c.Bot(Seq((9000, F())));
            var shop = c.dir.config.shop;
            c.run.maxHealth = 10; c.run.health = 10;
            // struck from a safe distance: it rings, then bursts; a creature beside it is cut down, the player out of reach is not
            c.Teleport(4.5f, 1f);
            var a = c.Spawn<ResonantCrystal>("crystal", 10, 1);
            c.Expect(a != null && a.Whole, "a resonant crystal stands in the floor");
            if (a == null) yield break;
            var snake = c.Spawn<Snake>("snake", 12, 1);
            snake.stunTimer = 99f; snake.heartDropChance = 0f; snake.goldChance = 0f;
            yield return c.Frames(5);
            int hp = c.run.health;
            a.OnWhipped(c.p, 1, 1);
            bool rang = a.Ringing, shards = false, snakeDead = false;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.Count<CrystalShard>() > 0) { if (!shards) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_crystal.png")); shards = true; }
                if (snake.Dead) snakeDead = true;
            }
            c.Expect(rang && a.Bursts == 1 && shards && a.Spent, $"struck, it rings and then bursts into shards, leaving a stump (bursts {a.Bursts})");
            c.Expect(snakeDead, "a creature standing beside it is cut down by the shards");
            c.Expect(c.run.health == hp, $"out of their reach the player is untouched (health {hp} -> {c.run.health})");
            // standing next to it when it bursts costs a point - unless a Dampener is worn
            var b = c.Spawn<ResonantCrystal>("crystal", 16, 1);
            c.Teleport(17.5f, 1f);
            yield return c.Frames(10);
            hp = c.run.health;
            b.Ring();
            for (int i = 0; i < 90; i++) { yield return new WaitForFixedUpdate(); if (Mathf.Abs(c.p.pos.x - 17.5f) > 2f) break; }
            c.Expect(b.Bursts == 1 && c.run.health == hp - 1, $"next to a bursting crystal the player is cut for 1 (health {hp} -> {c.run.health})");
            c.run.AddUpgrade(shop.Find("dampener"));
            var d = c.Spawn<ResonantCrystal>("crystal", 22, 1);
            yield return c.Frames(70);
            c.Teleport(23.5f, 1f);
            yield return c.Frames(10);
            hp = c.run.health;
            d.Ring();
            yield return c.Frames(90);
            c.Expect(c.run.IsWorn("dampener") && d.Bursts == 1 && c.run.health == hp, $"with a Dampener the shards pass through the wearer (health {hp} -> {c.run.health})");
            c.run.RemoveUpgrade("dampener");
            // a shard that reaches another crystal sets that one off too
            c.p.godMode = true;
            c.Teleport(4.5f, 4f);
            if (b != null) b.Despawn();
            if (d != null) d.Despawn();
            yield return c.Frames(3);
            var e1 = c.Spawn<ResonantCrystal>("crystal", 16, 1);
            var e2 = c.Spawn<ResonantCrystal>("crystal", 19, 1);
            yield return c.Frames(5);
            e1.Ring();
            for (int i = 0; i < 200 && e2.Bursts == 0; i++) yield return new WaitForFixedUpdate();
            c.Expect(e1.Bursts == 1 && e2.Bursts == 1, $"a shard that reaches another crystal sets it off in turn ({e1.Bursts}, {e2.Bursts})");
            // the stump grows back
            bool regrew = false;
            for (int i = 0; i < 500 && !regrew; i++) { yield return new WaitForFixedUpdate(); if (a.Whole) regrew = true; }
            c.Expect(regrew, "the stump grows back into a whole crystal");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is ResonantCrystal || e is CrystalShard)) e.Despawn();
            yield return c.Frames(3);

            // the light lens: dark, a thin line, then the beam - 2 damage to whoever stands in it; a crystal under it does not ring (light never rings one)
            c.Teleport(4.5f, 1f);
            var lens = c.Spawn<LightLens>("lightlens", 14, 5);
            var under = c.Spawn<ResonantCrystal>("crystal", 14, 1);
            c.Expect(lens != null, "a light lens hangs overhead");
            if (lens == null) yield break;
            bool dark = false, warn = false, burn = false;
            for (int i = 0; i < 300; i++)
            {
                yield return new WaitForFixedUpdate();
                if (lens.Phase == 0) dark = true; else if (lens.Phase == 1) warn = true; else { if (!burn) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_lens.png")); burn = true; }
            }
            c.Expect(dark && warn && burn, "it rests, shows a thin line, and burns - over and over");
            c.Expect(under != null && under.Bursts == 0 && under.Whole, $"its beam does not ring the crystal it falls on ({(under != null ? under.Bursts : -1)} bursts)");
            if (under != null) under.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is CrystalShard) e.Despawn();
            for (int i = 0; i < 400 && lens.Phase != 0; i++) yield return new WaitForFixedUpdate();
            c.p.godMode = false;
            c.run.health = 10; hp = c.run.health;
            c.Teleport(14.5f, 1f);
            bool burnt = false;
            for (int i = 0; i < 400 && !burnt; i++) { yield return new WaitForFixedUpdate(); if (c.run.health < hp) burnt = true; }
            c.Expect(burnt && c.run.health == hp - 2, $"standing under it until it burns: 2 damage (health {hp} -> {c.run.health})");
            lens.Despawn();
            c.run.health = 4;
            yield return c.Frames(40);
        }

        static IEnumerator GeodeEnemyCase(Ctx c)
        {
            c.Bot(Seq((9000, F())));
            var shop = c.dir.config.shop;
            c.p.godMode = true;
            // the shardling: a rush along the floor; broken, it comes apart in two that do not split again
            c.Teleport(8.5f, 1f);
            var crab = c.Spawn<Shardling>("shardling", 12, 1, 0, null, -1);
            c.Expect(crab != null && !crab.IsStompable, "a shardling spawned; its back cannot be stomped");
            if (crab == null) yield break;
            crab.heartDropChance = 0f; crab.goldChance = 0f;
            bool rushed = false;
            for (int i = 0; i < 300 && !rushed; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 8.5f) > 0.5f) c.Teleport(8.5f, 1f);
                if (crab.Attacking && Mathf.Abs(crab.vel.x) > 5f) { rushed = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_shardling.png")); }
            }
            c.Expect(rushed, "seeing the player along its floor it tucks in and rushes");
            for (int i = 0; i < 10 && !crab.Dead; i++) { crab.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            yield return c.Frames(3);
            int young = c.Count<Shardling>();
            c.Expect(crab.Dead && young == 2, $"broken, it comes apart into two small ones ({young})");
            var smalls = new List<Shardling>();
            foreach (var e in c.world.Entities) if (e is Shardling s && !s.Dead) smalls.Add(s);
            foreach (var s in smalls) { s.stunTimer = 0f; s.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test")); }
            yield return c.Frames(16);
            foreach (var s in smalls) if (!s.Dead) s.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(4);
            c.Expect(c.Count<Shardling>() == 0, $"the small ones do not split again ({c.Count<Shardling>()} left)");
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup) e.Despawn();

            // the glint: wakes, keeps its distance, aims a line at the player and fires along it
            c.p.godMode = false;
            c.run.maxHealth = 10; c.run.health = 10;
            int hp = c.run.health;
            c.Teleport(8.5f, 1f);
            var glint = c.Spawn<Glint>("glint", 13, 5);
            c.Expect(glint != null && glint.ContactDamage == 0, "a glint hangs overhead; its body does not hurt");
            if (glint == null) yield break;
            bool aimed = false;
            for (int i = 0; i < 700 && c.run.health == hp; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 8.5f) > 0.5f) c.Teleport(8.5f, 1f);
                if (glint.Aiming) { aimed = true; if (i % 4 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_glint.png")); }
            }
            c.Expect(aimed && glint.Beams >= 1 && c.run.health == hp - glint.beamDamage, $"it aims a line at the player and fires along it: a player who stays put is burnt for {glint.beamDamage} (beams {glint.Beams}, health {hp} -> {c.run.health})");
            glint.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(3);
            c.Expect(c.Count<Glint>() == 0, "one stroke breaks it");
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup) e.Despawn();

            // the mimic: a gem until someone comes close; a Truth Lens shows it; struck first, it has lost its spring
            c.p.godMode = true;
            c.Teleport(5.5f, 1f);
            var mimic = c.Spawn<Mimic>("mimic", 16, 1);
            c.Expect(mimic != null, "a mimic lies on the floor");
            if (mimic == null) yield break;
            mimic.heartDropChance = 0f;
            yield return c.Frames(60);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_mimic.png"));
            c.Expect(mimic.Dormant && !mimic.Attacking && !mimic.Revealed, "left alone it lies still, to all appearance a gem");
            c.run.AddUpgrade(shop.Find("truthlens"));
            c.Expect(c.run.IsWorn("truthlens") && mimic.Revealed, "a Truth Lens shows it for what it is");
            c.run.RemoveUpgrade("truthlens");
            c.Teleport(15.6f, 1f);
            bool sprang = false;
            for (int i = 0; i < 200 && !sprang; i++)
            {
                yield return new WaitForFixedUpdate();
                if (mimic.Attacking) { sprang = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_mimic_awake.png")); }
            }
            c.Expect(sprang && !mimic.Dormant, "when the player comes to take it, it springs at them");
            mimic.Despawn();
            c.Teleport(5.5f, 1f);
            var wary = c.Spawn<Mimic>("mimic", 20, 1);
            yield return c.Frames(10);
            bool struck = wary != null && wary.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(struck && wary != null && !wary.Dormant && !wary.Attacking, "a 'gem' struck before it is taken wakes without its spring");
            if (wary != null) wary.Despawn();
            // it has a disguise for everything worth walking up to
            string[] kinds = { "gem", "pot", "crate" };
            var looks = new HashSet<Sprite>();
            var posed = new List<Mimic>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var m = c.Spawn<Mimic>("mimic_" + kinds[i], 12 + i * 3, 1);
                if (m != null) { m.heartDropChance = 0f; posed.Add(m); }
            }
            yield return c.Frames(20);
            bool allStill = posed.Count == kinds.Length;
            foreach (var m in posed)
            {
                var msr = m.GetComponentInChildren<SpriteRenderer>();
                if (msr != null && msr.sprite != null) looks.Add(msr.sprite);
                if (!m.Dormant) allStill = false;
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_mimic_disguises.png"));
            c.Expect(allStill && looks.Count == kinds.Length, $"it poses as a gem, a pot, a crate: {posed.Count} lie still, {looks.Count} different pictures");
            var pot = posed.Count > 1 ? posed[1] : null;
            c.Teleport(14.6f, 1f);
            bool potSprang = false;
            for (int i = 0; i < 200 && !potSprang && pot != null; i++)
            {
                yield return new WaitForFixedUpdate();
                if (pot.Attacking) { potSprang = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "geode_mimic_pot_awake.png")); }
            }
            c.Expect(potSprang, "the 'pot' springs at whoever comes for it");
            foreach (var m in posed) if (!m.Dead) m.Despawn();
            c.p.godMode = false;
            c.run.health = 4;
            yield return c.Frames(5);
        }

        static IEnumerator FacetCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((20000, F())));
            c.Teleport(6.5f, 1f);
            var b = c.Spawn<FacetGolem>("facet", 13, 1);
            c.Expect(b != null, "the facet spawned");
            if (b == null) yield break;
            yield return c.Frames(5);
            c.Expect(c.Count<FacetFist>() == 2 && b.Left != null && b.Right != null, $"it has two fists in the hall ({c.Count<FacetFist>()})");
            if (b.Left == null || b.Right == null) { b.Despawn(); yield break; }
            c.Expect(b.pos.y > 6f && b.ContactDamage == 0 && !b.IsStompable, $"its head is high on the back wall, out of reach (y {b.pos.y:0.0})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet.png"));
            // neither the head nor a whole fist can be hurt with the blade
            int h0 = b.health;
            bool hurtHead = b.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            bool hurtFist = b.Left.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(!hurtHead && !hurtFist && b.health == h0 && !b.Cracked, $"the head cannot be hurt and a fist's shell turns the blade (health {h0} -> {b.health})");
            // its moves
            bool volley = false, rain = false, fell = false, swept = false, sweepBack = false, fallBack = false;
            for (int i = 0; i < 9000 && !(volley && rain && fell && swept); i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 6.5f) > 0.5f) c.Teleport(6.5f, 1f);
                foreach (var f in new[] { b.Left, b.Right })
                {
                    if (f.mode == FacetFist.Mode.Fall) { fallBack |= f.IsStompable; if (!fell) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_slam.png")); fell = true; }
                    if (f.mode == FacetFist.Mode.Sweep) { sweepBack |= f.IsStompable; if (!swept) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_sweep.png")); swept = true; }
                }
                foreach (var e in c.world.Entities)
                {
                    if (e.Dead) continue;
                    if (e is Stalactite) rain = true;
                }
                if (b.Burning) { if (!volley) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_light.png")); volley = true; }
            }
            c.Expect(fell && b.Slams >= 1, $"a fist follows the player, hangs and comes down ({b.Slams} slams)");
            c.Expect(swept && b.Sweeps >= 1, $"a fist sweeps the floor from one wall to the other ({b.Sweeps} sweeps)");
            c.Expect(sweepBack && !fallBack, "a sweeping fist can be landed on from above; one that is coming down cannot");
            c.Expect(volley && b.Volleys >= 1 && c.Count<BossShot>() == 0, $"three beams of light burn from the gem in its forehead, after thin lines have shown where ({b.Volleys} volleys)");
            c.Expect(rain, "it brings rock down from the ceiling");
            // a beam that falls on a crystal is thrown back as from a mirror, and the crystal does not ring for it
            var mirror = c.Spawn<ResonantCrystal>("crystal", 6, 1);
            int bounce0 = b.Bounces, v0 = b.Volleys;
            bool rangForLight = false, shotTaken = false;
            for (int i = 0; i < 9000 && !(b.Bounces > bounce0 && !b.Burning); i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 6.5f) > 0.5f) c.Teleport(6.5f, 1f);
                if (mirror == null || mirror.Dead) mirror = c.Spawn<ResonantCrystal>("crystal", 6, 1);
                if (b.Burning && b.Bounces > bounce0)
                {
                    if (!shotTaken) { shotTaken = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_mirror.png")); }
                    if (mirror.Ringing && !b.Left.Busy && !b.Right.Busy) rangForLight = true;
                }
            }
            c.Expect(b.Bounces > bounce0, $"a beam that falls on a crystal is thrown back up as from a mirror ({b.Bounces - bounce0} bounces in {b.Volleys - v0} volleys)");
            c.Expect(!rangForLight, "the light does not set the crystal ringing");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is ResonantCrystal || e is CrystalShard)) e.Despawn();
            for (int i = 0; i < 400 && b.Cracked; i++) yield return new WaitForFixedUpdate();
            // a sweeping fist smashes the crystals in its way: they burst at once, and only the shards ahead of the fist get away
            var inWay = c.Spawn<ResonantCrystal>("crystal", 15, 1);
            int ahead = 0, behind = 0, cracks0 = b.Cracks;
            bool smashedAtOnce = false;
            var counted = new HashSet<Entity>();
            for (int i = 0; i < 12000 && ahead == 0; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 6.5f) > 0.5f) c.Teleport(6.5f, 1f);
                if (inWay == null || inWay.Dead) inWay = c.Spawn<ResonantCrystal>("crystal", 15, 1);
                var sweeper = b.Left.mode == FacetFist.Mode.Sweep ? b.Left : b.Right.mode == FacetFist.Mode.Sweep ? b.Right : null;
                if (sweeper == null) continue;
                int dir = sweeper == b.Left ? 1 : -1;
                foreach (var e in c.world.Entities)
                    if (!e.Dead && e is CrystalShard cs && cs.swept && counted.Add(e))
                    {
                        if (cs.vel.x * dir > 0f) ahead++; else behind++;
                        if (inWay.Spent && sweeper.Overlaps(inWay)) smashedAtOnce = true;
                    }
                if (ahead > 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_sweep_crystal.png"));
            }
            yield return c.Frames(40);
            c.Expect(ahead == 2 && behind == 0 && smashedAtOnce, $"a sweeping fist smashes a crystal at once and only the shards ahead of it get away ({ahead} ahead, {behind} behind)");
            c.Expect(b.Cracks == cracks0, "those shards do not crack a fist");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is ResonantCrystal || e is CrystalShard)) e.Despawn();
            // a slam hurts: a player who stays under the fist pays for it
            c.p.godMode = false;
            c.run.maxHealth = 10; c.run.health = 10;
            int hp = c.run.health, slams0 = b.Slams;
            bool crushed = false;
            int burns0 = b.BeamHits;
            for (int i = 0; i < 9000 && !(crushed && b.BeamHits > burns0); i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.run.health < hp) { if (b.Left.Striking || b.Right.Striking) crushed = true; else hp = c.run.health; }
                if (c.run.health < 5) { c.run.health = 10; hp = 10; }
                if (Mathf.Abs(c.p.pos.x - 6.5f) > 1.5f && c.p.grounded) c.Teleport(6.5f, 1f);
            }
            c.Expect(crushed, "a fist that comes down on the player hurts them");
            c.Expect(b.BeamHits > burns0, $"a player who stays on the line is burnt by the beam ({b.BeamHits - burns0} times)");
            c.p.godMode = true;
            // a fist that lands on a crystal sets it ringing, and the shards crack the fist open; then the blade counts
            foreach (var e in c.world.Entities) if (!e.Dead && (e is BossShot || e is Stalactite)) e.Despawn();
            c.Teleport(6.5f, 1f);
            var cr = c.Spawn<ResonantCrystal>("crystal", 6, 1);
            bool cracked = false;
            for (int i = 0; i < 12000 && !cracked; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 6.5f) > 0.5f) c.Teleport(6.5f, 1f);
                if (b.Cracked) { cracked = true; }
            }
            c.Expect(cracked && b.Cracks >= 1 && b.OpenFist != null && b.OpenFist.IsOpen, $"lured down onto a crystal, the fist is cracked open by its shards ({b.Cracks} cracks)");
            if (!cracked || b.OpenFist == null) { b.Despawn(); ClearFight(c); yield break; }
            var open = b.OpenFist;
            for (int i = 0; i < 60 && open.pos.y > b.FloorY + 0.01f; i++) yield return new WaitForFixedUpdate();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_facet_cracked.png"));
            yield return c.Frames(14);
            int h1 = b.health;
            bool hurtNow = open.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(hurtNow && b.health == h1 - 3 && open.ContactDamage == 0, $"a blow on the open fist is a blow on the Facet (health {h1} -> {b.health})");
            bool closed = false;
            for (int i = 0; i < 400 && !closed; i++) { yield return new WaitForFixedUpdate(); if (!b.Cracked) closed = true; }
            int h2 = b.health;
            yield return c.Frames(14);
            bool hurtClosed = open.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(closed && !hurtClosed && b.health == h2, "after a few seconds the shell closes again");
            // when it goes, its fists go with it
            b.Despawn();
            yield return c.Frames(4);
            c.Expect(c.Count<FacetFist>() == 0, $"when it goes its fists go with it ({c.Count<FacetFist>()} left)");
            ClearFight(c);
            foreach (var e in c.world.Entities) if (!e.Dead && (e is ResonantCrystal || e is CrystalShard || e is Stalactite || e is Pickup)) e.Despawn();
            c.p.godMode = false;
            c.run.health = 4;
        }

        static IEnumerator SandTrapCase(Ctx c)
        {
            c.Bot(Seq((4000, F())));
            var shop = c.dir.config.shop;
            for (int x = 8; x <= 11; x++) c.Spawn<Quicksand>("quicksand", x, 1);
            yield return c.Frames(3);
            c.Expect(c.Count<Quicksand>() == 4, "a pool of quicksand lies in the floor");
            // standing in it: slowed at once, pulled under after a while
            int hp = c.run.health;
            c.Teleport(9.5f, 1f);
            bool slowed = false, swallowed = false; int took = 0;
            for (int i = 0; i < 260 && !swallowed; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.Slowed) slowed = true;
                if (c.run.health < hp) { swallowed = true; took = i; }
                if (i == 60) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sandsunk_quicksand.png"));
            }
            c.Expect(slowed, "walking in quicksand is slow");
            c.Expect(swallowed && c.run.health == hp - 1 && took > 90, $"staying in it, the player is pulled under for 1 (health {hp} -> {c.run.health} after {took} steps)");
            // sand striders: neither slowed nor swallowed
            c.run.AddUpgrade(shop.Find("sandstriders"));
            yield return c.Frames(60);
            hp = c.run.health;
            c.Teleport(9.5f, 1f);
            bool slowedShod = false;
            for (int i = 0; i < 220; i++) { yield return new WaitForFixedUpdate(); if (i > 30 && c.p.Slowed) slowedShod = true; }
            c.Expect(c.run.IsWorn("sandstriders") && !slowedShod && c.run.health == hp, $"in sand striders the sand holds like stone (slowed {slowedShod}, health {hp} -> {c.run.health})");
            c.run.RemoveUpgrade("sandstriders");
            foreach (var e in c.world.Entities) if (!e.Dead && e is Quicksand) e.Despawn();

            // the spear plate: harmless while down, 2 damage while the spears stand
            c.Teleport(5.5f, 1f);
            var plate = c.Spawn<SpearTrap>("speartrap", 14, 1);
            c.Expect(plate != null, "a spear plate lies in the floor");
            if (plate == null) yield break;
            bool sawDown = false, sawWarn = false, sawUp = false;
            for (int i = 0; i < 260; i++) { yield return new WaitForFixedUpdate(); if (plate.Phase == 0) sawDown = true; else if (plate.Phase == 1) sawWarn = true; else sawUp = true; }
            c.Expect(sawDown && sawWarn && sawUp, "it rests, shows its tips, and thrusts - over and over");
            for (int i = 0; i < 300 && plate.Phase != 0; i++) yield return new WaitForFixedUpdate();
            hp = c.run.health;
            c.Teleport(14.5f, 1f);
            yield return c.Frames(20);
            c.Expect(c.run.health == hp, "standing on it while the spears are down does nothing");
            bool speared = false;
            for (int i = 0; i < 300 && !speared; i++)
            {
                yield return new WaitForFixedUpdate();
                if (plate.Up && i % 4 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sandsunk_spears.png"));
                if (c.run.health < hp) speared = true;
            }
            c.Expect(speared && c.run.health == hp - 2, $"staying on it until they come up: speared for 2 (health {hp} -> {c.run.health})");
            plate.Despawn();
            yield return c.Frames(40);
            {
                // a bomb that lands on quicksand is swallowed: no blast
                for (int x = 8; x <= 11; x++) c.Spawn<Quicksand>("quicksand", x, 1);
                c.Teleport(4.5f, 1f);
                yield return c.Frames(3);
                int blasts = c.world.ExplosionsThisLevel;
                var bomb = c.world.SpawnById("bomb", new Vector2(9.5f, 3f)) as Bomb;
                c.Expect(bomb != null, "a bomb is dropped over the sand");
                bool gone = false;
                for (int i = 0; i < 200 && bomb != null && !gone; i++) { yield return new WaitForFixedUpdate(); if (bomb.Dead) gone = true; }
                yield return c.Frames(200);
                c.Expect(gone && c.world.ExplosionsThisLevel == blasts, $"quicksand swallows a bomb before it goes off (blasts {blasts} -> {c.world.ExplosionsThisLevel})");
                foreach (var e in c.world.Entities) if (!e.Dead && e is Quicksand) e.Despawn();
                yield return c.Frames(3);
            }
        }

        static IEnumerator SandEnemyCase(Ctx c)
        {
            c.Bot(Seq((9000, F())));
            var shop = c.dir.config.shop;
            c.p.godMode = true;
            // scarabs: one placed, a pack hatches; from a few steps away they pounce; a death sends the rest into a frenzy
            c.Teleport(12.5f, 1f);
            c.Spawn<Scarab>("scarab", 17, 1);
            yield return c.Frames(3);
            c.Expect(c.Count<Scarab>() == 3, $"where one scarab is placed a pack of three hatches ({c.Count<Scarab>()})");
            bool pounced = false;
            for (int i = 0; i < 500 && !pounced; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 12.5f) > 0.5f) c.Teleport(12.5f, 1f);
                foreach (var e in c.world.Entities)
                    if (e is Scarab s && !s.Dead && s.Pouncing && !s.grounded && s.Attacking) pounced = true;
                if (pounced) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sandsunk_scarabs.png"));
            }
            c.Expect(pounced, "a scarab crouches, then pounces through the air at the player");
            Scarab first = null;
            foreach (var e in c.world.Entities) if (e is Scarab s && !s.Dead && first == null) first = s;
            if (first != null) first.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            yield return c.Frames(2);
            int frenzied = 0;
            foreach (var e in c.world.Entities) if (e is Scarab s && !s.Dead && s.Frenzied) frenzied++;
            c.Expect(c.Count<Scarab>() == 2 && frenzied == 2, $"one dies, and the rest of the pack goes into a frenzy ({frenzied} of {c.Count<Scarab>()})");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is Scarab || e is Pickup)) e.Despawn();
            yield return c.Frames(3);
            c.p.godMode = false;

            // the mummy: from a few steps away it casts its wrapping and drags the player in; its grab takes gold, not health
            c.run.maxHealth = 10; c.run.health = 10;
            c.run.gold = 400;
            int hp = c.run.health;
            c.Teleport(6.5f, 1f);
            var mummy = c.Spawn<Mummy>("mummy", 10, 1, 0, null, -1);
            c.Expect(mummy != null, "a mummy spawned");
            if (mummy == null) yield break;
            mummy.heartDropChance = 0f; mummy.goldChance = 0f;
            bool caught = false; float pulledTo = 0f;
            for (int i = 0; i < 700 && c.run.gold == 400; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.Slowed && mummy.Casts > 0 && !caught) { caught = true; }
                if (caught && pulledTo == 0f && c.p.grounded) pulledTo = c.p.pos.x;
                if (i == 40) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sandsunk_mummy.png"));
            }
            c.Expect(mummy.Casts >= 1 && caught, $"from a few steps away it casts its wrapping and catches the player ({mummy.Casts} casts)");
            c.Expect(c.run.gold == 400 - mummy.stealGold && c.run.health == hp && mummy.Stolen == mummy.stealGold,
                $"its grab takes {mummy.stealGold} gold and no health (gold 400 -> {c.run.gold}, health {hp} -> {c.run.health}, it holds {mummy.Stolen})");
            // destroyed: the gold falls out, and so do the scarabs that nested in it
            c.p.godMode = true;
            int gold0 = c.Count<Pickup>();
            for (int i = 0; i < 20 && !mummy.Dead; i++) { mummy.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            yield return c.Frames(3);
            c.Expect(mummy.Dead && c.Count<Pickup>() - gold0 == mummy.stealGold / 30, $"destroyed, what it took falls out ({c.Count<Pickup>() - gold0} pieces)");
            c.Expect(c.Count<Scarab>() == mummy.broodOnDeath, $"and {mummy.broodOnDeath} scarabs come out of its wrappings ({c.Count<Scarab>()})");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is Pickup || e is Scarab || e is MummyWrap)) e.Despawn();
            yield return c.Frames(3);
            c.p.godMode = false;
            // the blade cuts the wrapping out of the air
            var strip = c.world.SpawnById("mummywrap", new Vector2(12.5f, 1.5f)) as MummyWrap;
            bool stripCut = false;
            if (strip != null) { strip.OnWhipped(c.p, 1, 1); stripCut = strip.Dead; }      // (asked at once: a despawned thing is gone a step later)
            yield return c.Frames(2);
            c.Expect(stripCut, "the blade cuts a wrapping out of the air");
            // penniless: now the grab draws blood
            c.run.gold = 0; c.run.health = 10; hp = c.run.health;
            c.Teleport(6.5f, 1f);
            var poor = c.Spawn<Mummy>("mummy", 8, 1, 0, null, -1);
            for (int i = 0; i < 700 && c.run.health == hp; i++) { yield return new WaitForFixedUpdate(); if (i % 30 == 0 && Mathf.Abs(c.p.pos.x - 6.5f) > 4f) c.Teleport(6.5f, 1f); }
            c.Expect(c.run.health == hp - 1, $"with no gold to take, its grab hurts instead (health {hp} -> {c.run.health})");
            if (poor != null) poor.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is MummyWrap) e.Despawn();
            // the warding scarab keeps its hands off
            c.run.AddUpgrade(shop.Find("wardscarab"));
            c.run.gold = 400; c.run.health = 10; hp = c.run.health;
            yield return c.Frames(80);
            c.Teleport(6.5f, 1f);
            var warded = c.Spawn<Mummy>("mummy", 8, 1, 0, null, -1);
            for (int i = 0; i < 480; i++) { yield return new WaitForFixedUpdate(); if (i % 30 == 0 && Mathf.Abs(c.p.pos.x - 6.5f) > 4f) c.Teleport(6.5f, 1f); }
            c.Expect(c.run.IsWorn("wardscarab") && c.run.gold == 400 && c.run.health == hp, $"with a warding scarab its grab takes nothing (gold {c.run.gold}, health {hp} -> {c.run.health})");
            if (warded != null) warded.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is MummyWrap) e.Despawn();
            c.run.RemoveUpgrade("wardscarab");
            c.run.gold = 0;

            // the sand worm: under the floor nothing reaches it; from a distance it comes up and spits, up close it lunges
            c.Teleport(5.5f, 1f);
            var worm = c.Spawn<SandWorm>("sandworm", 20, 1);
            c.Expect(worm != null, "a sand worm spawned");
            if (worm == null) yield break;
            yield return c.Frames(20);
            bool hitBuried = worm.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(worm.Buried && !hitBuried && worm.ContactDamage == 0 && worm.Shots == 0, "far off it stays buried: it cannot be cut and its mound is harmless");
            // within sight, a few steps away: it rises, aims and spits a clod at the player
            c.run.health = 10; hp = c.run.health;
            c.Teleport(15.5f, 1f);
            bool rose = false, aimed = false;
            for (int i = 0; i < 400 && c.run.health == hp; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 15.5f) > 0.5f) c.Teleport(15.5f, 1f);
                if (!worm.Buried) rose = true;
                if (worm.Aiming) { aimed = true; if (i % 3 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "sandsunk_worm.png")); }
            }
            c.Expect(rose && aimed && worm.Shots >= 1 && c.run.health == hp - 1, $"within sight it comes up, aims and spits: hit for 1 (shots {worm.Shots}, health {hp} -> {c.run.health})");
            c.p.godMode = true;
            // struck while it is up, it is hurt where it stands: a blow does not throw it into the air
            for (int i = 0; i < 400 && worm.Buried; i++) { yield return new WaitForFixedUpdate(); if (Mathf.Abs(c.p.pos.x - 15.5f) > 0.5f) c.Teleport(15.5f, 1f); }
            float wy = worm.pos.y; int wh = worm.health;
            bool struck = worm.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, new Vector2(5f, 9f), "test"));
            float top = worm.pos.y;
            for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); top = Mathf.Max(top, worm.pos.y); }
            c.Expect(struck && worm.health == wh - 1 && top - wy < 0.05f, $"cut while it is up it is hurt, and stays in its hole (rose {top - wy:0.00})");
            worm.health = wh;
            int shotsBefore = worm.Shots;
            for (int i = 0; i < 360; i++) { yield return new WaitForFixedUpdate(); if (Mathf.Abs(c.p.pos.x - 15.5f) > 0.5f) c.Teleport(15.5f, 1f); }
            c.Expect(worm.Shots > shotsBefore, $"and it keeps at it while the player stays in reach ({shotsBefore} -> {worm.Shots} shots)");
            foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
            // right at its mound: the lunge
            c.Teleport(5.5f, 4f);
            for (int i = 0; i < 400 && !worm.Buried; i++) { yield return new WaitForFixedUpdate(); if (c.p.pos.x > 9f || c.p.pos.y < 0.5f) c.Teleport(5.5f, 1f); }
            c.p.godMode = false;
            c.run.health = 10; hp = c.run.health;
            c.Teleport(20.5f, 1f);
            bool lunged = false;
            for (int i = 0; i < 500 && c.run.health == hp; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(c.p.pos.x - 20.5f) > 0.5f) c.Teleport(20.5f, 1f);
                if (worm.Attacking) lunged = true;
            }
            c.Expect(lunged && c.run.health == hp - worm.contactDamage, $"standing over it: its lunge bites for {worm.contactDamage} (health {hp} -> {c.run.health})");
            c.p.godMode = true;
            bool cut = false;
            for (int i = 0; i < 500 && !cut; i++)
            {
                yield return new WaitForFixedUpdate();
                if (!worm.Buried && !worm.Attacking) cut = worm.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            }
            c.Expect(cut, "out of the ground it can be cut");
            if (!worm.Dead) worm.Despawn();
            foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot) e.Despawn();
            c.p.godMode = false;
            c.run.health = 4;
            yield return c.Frames(5);
        }

        static IEnumerator DuneQueenCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((6000, F())));
            {
                // before she is there: a block comes up out of the sand and stays up by itself
                var alone = c.Spawn<SandPillar>("sandpillar", 24, 1);
                c.Teleport(6.5f, 1f);
                for (int i = 0; i < 400 && alone != null && !alone.IsUp; i++) yield return new WaitForFixedUpdate();
                yield return c.Frames(420);
                c.Expect(alone != null && alone.IsUp, "a block comes up and, left alone, stays up");
                // a wave along the sand breaks the first block it meets and ends there
                var second = c.Spawn<SandPillar>("sandpillar", 27, 1);
                for (int i = 0; i < 400 && second != null && !second.IsUp; i++) yield return new WaitForFixedUpdate();
                var wave = c.world.SpawnById("shockwave", new Vector2(20.5f, 1.2f));
                if (wave != null) wave.vel = new Vector2(8f, 0f);
                yield return c.Frames(90);
                c.Expect(alone != null && second != null && !alone.IsUp && second.IsUp && (wave == null || wave.Dead),
                    $"a wave breaks one block and is spent: the first is {(alone != null && alone.IsUp ? "standing" : "broken")}, the one behind it {(second != null && second.IsUp ? "standing" : "broken")}");
                if (second != null) second.Despawn();
                if (alone != null) alone.Despawn();
                yield return c.Frames(3);
            }
            c.Teleport(16.5f, 1f);
            var b = c.Spawn<DuneQueen>("dunequeen", 22, 1);
            c.Expect(b != null, "the dune queen spawned");
            if (b == null) yield break;
            yield return c.Frames(3);
            {
                // the whole floor of her chamber is quicksand
                var sandAt = new HashSet<int>(); int floorCells = 0;
                foreach (var e in c.world.Entities) if (e is Quicksand && !e.Dead) sandAt.Add(Mathf.FloorToInt(e.pos.x));
                for (int x = 1; x < c.world.Grid.Width - 1; x++) if (c.world.Grid.IsEmpty(x, 1) && c.world.Grid.IsSolid(x, 0)) floorCells++;
                c.Expect(floorCells > 10 && sandAt.Count == floorCells, $"when the fight begins the whole floor turns to quicksand ({sandAt.Count} of {floorCells} cells)");
            }
            bool hidden = false, hiddenSafe = true, breached = false, breachOnPlayer = false, spent = false, waves = false, spat = false;
            int diving = 0; float diveTop = 99f;
            for (int i = 0; i < 4000 && !(breached && spent && waves && spat); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Diving)
                {
                    diving++;
                    diveTop = Mathf.Min(diveTop, b.pos.y + b.size.y);
                    if (diving == 26) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_dunequeen_dive.png"));
                }
                if (b.Hidden)
                {
                    hidden = true;
                    if (b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"))) hiddenSafe = false;
                    if (i % 15 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_dunequeen_mound.png"));
                }
                if (b.Breaching)
                {
                    if (!breached) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_dunequeen_breach.png"));
                    breached = true;
                    if (Mathf.Abs(b.pos.x - c.p.pos.x) < 1.2f) breachOnPlayer = true;
                }
                if (b.Exposed) spent = true;
                foreach (var e in c.world.Entities)
                    if (e is BossShot bs && !bs.Dead) { if (bs.flipByVelocity) waves = true; else spat = true; }
            }
            c.Expect(hidden && hiddenSafe, "she dives under the sand, where nothing reaches her");
            c.Expect(diving > 20 && diveTop < 1.6f, $"she sinks into the floor bodily over {diving} steps (her head ends up at height {diveTop:0.0}, the floor is at 1)");
            c.Expect(breached && breachOnPlayer, "her mound runs to the player and she breaches there");
            c.Expect(spent && waves, "then she lies spent, and her fall sends a wave along the sand");
            c.Expect(spat, "she spits sand at the player");
            // a block of stone: it comes up out of the sand, is solid, holds the player; her attacks break it, and it comes back
            var slab = c.Spawn<SandPillar>("sandpillar", 9, 1);
            c.Expect(slab != null, "a block waits under the sand");
            if (slab != null)
            {
                // the player stands where it will come up: carried to the top, not shut inside the stone
                c.Teleport(10f, 1f);
                bool partly = false; int risingFor = 0;
                for (int i = 0; i < 400 && !slab.IsUp; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (Mathf.Abs(c.p.pos.x - 10f) > 0.7f) c.Teleport(10f, 1f);
                    if (slab.Rising) risingFor++;
                    if (risingFor == 26 && !partly) { partly = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_dunequeen_block_rising.png")); }
                }
                yield return c.Frames(6);
                c.Expect(slab.IsUp && c.world.Grid.IsSolid(9, 1) && c.world.Grid.IsSolid(10, 1), "it rises and is solid stone: nothing walks through it");
                c.Expect(c.p.grounded && Mathf.Abs(c.p.pos.y - 2f) < 0.06f, $"whoever stood there is carried to its top (player at height {c.p.pos.y:0.00})");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_dunequeen_slab.png"));
                // her breach breaks it (the player keeps to its place: on it while it stands, at its foot while it is down)
                bool broken = false, back = false; int breaches = 0; bool wasBreaching = false;
                int hpBefore = b.health, hpAfter = b.health, butts0 = b.Headbutts;
                for (int i = 0; i < 6000 && !(broken && back); i++)
                {
                    yield return new WaitForFixedUpdate();
                    // (once it is broken the player walks off, so that she follows and the place is free for it to come up again)
                    // (while the block is down - a clod of spit may have broken it first - the player waits a few steps away,
                    //  so that she does not keep coming up in its place and it can rise again)
                    float stand = broken ? 20f : slab.IsUp ? 10f : 17f;
                    if (Mathf.Abs(c.p.pos.x - stand) > 0.7f) c.Teleport(stand, !broken && slab.IsUp ? 2f : 1f);
                    if (b.Breaching && !wasBreaching) breaches++;
                    if (!broken && b.Headbutts > butts0) { broken = true; hpAfter = b.health; }
                    wasBreaching = b.Breaching;
                    if (broken && slab.IsUp) back = true;
                }
                c.Expect(broken, $"she comes up under it and breaks it ({breaches} breaches)");
                c.Expect(broken && hpBefore - hpAfter == b.headbuttDamage && b.Headbutts > butts0, $"ramming the stone with her head costs her {b.headbuttDamage} (health {hpBefore} -> {hpAfter})");
                c.Expect(back, "a broken block comes up again after a few seconds");
                // a clod of her spit breaks it too
                c.Teleport(16.5f, 1f);
                for (int i = 0; i < 900 && !slab.IsUp; i++) yield return new WaitForFixedUpdate();
                bool upBeforeShot = slab.IsUp;
                var clod = c.world.SpawnById("sandshot", new Vector2(6.5f, 1.4f));
                if (clod != null) clod.vel = new Vector2(7f, 0f);
                bool brokenByShot = false;
                for (int i = 0; i < 60 && !brokenByShot; i++) { yield return new WaitForFixedUpdate(); if (!slab.IsUp) brokenByShot = true; }
                c.Expect(upBeforeShot && brokenByShot, "a clod of her spit that hits it breaks it");
                slab.Despawn();
            }
            // her brood
            foreach (var e in c.world.Entities) if (!e.Dead && e is Scarab) e.Despawn();
            yield return c.Frames(2);
            c.Teleport(16.5f, 1f);
            yield return c.Frames(4);
            b.BroodNow(c.p);
            yield return c.Frames(3);
            int brood = c.Count<Scarab>();
            c.Expect(brood == b.minionsPerCall, $"her call shakes {b.minionsPerCall} scarabs out of the sand, each on its own ({brood})");
            b.Despawn();
            ClearFight(c);
            foreach (var e in c.world.Entities) if (!e.Dead && (e is Scarab || e is Quicksand || e is SandPillar)) e.Despawn();
            c.p.godMode = false;
        }

        static IEnumerator SawCase(Ctx c)
        {
            c.Bot(Seq((3000, F())));
            c.Teleport(5.5f, 1f);
            var saw = c.Spawn<SawBlade>("sawblade", 10, 1, 4);
            c.Expect(saw != null && !saw.Loose, "saw spawned on its rail");
            if (saw == null) yield break;
            float lo = 99f, hi = -99f;
            for (int i = 0; i < 260; i++) { yield return new WaitForFixedUpdate(); lo = Mathf.Min(lo, saw.pos.x); hi = Mathf.Max(hi, saw.pos.x); }
            c.Expect(Mathf.Abs(lo - 10.5f) < 0.05f && Mathf.Abs(hi - 13.5f) < 0.05f, $"it runs the whole 4-cell rail and no further (x {lo:0.00}..{hi:0.00})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "saw.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            // standing in its way hurts and throws the player clear
            int hp = c.run.health;
            c.Teleport(12f, 1f);
            bool cut = false;
            for (int i = 0; i < 200 && !cut; i++) { yield return new WaitForFixedUpdate(); if (c.run.health < hp) cut = true; }
            c.Expect(cut && c.run.health == hp - 1, $"standing on the rail: cut for 1 (health {hp} -> {c.run.health})");
            // a creature on the rail is cut too
            var snake = c.Spawn<Snake>("snake", 12, 1);
            snake.stunTimer = 99f; snake.heartDropChance = 0f;
            bool dead = false;
            for (int i = 0; i < 260 && !dead; i++) { yield return new WaitForFixedUpdate(); if (snake.Dead) dead = true; }
            c.Expect(dead, "a creature standing on the rail is cut down");
            // the blade does nothing to it
            saw.OnWhipped(c.p, 1, 1);
            c.Expect(!saw.Dead, "it cannot be broken with the blade");
            // jumped: a running jump clears the saw wherever it is on the rail
            c.run.health = 8;
            yield return c.Frames(80);
            c.Teleport(8.3f, 1f);
            int hp2 = c.run.health;
            c.Bot(Seq((6, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (40, F(x: 1, jh: true)), (60, F(x: 1)), (60, F())));
            yield return c.Frames(160);
            // the saw may or may not have been under the jump; what matters: a jump from before the rail lands past it unhurt when timed, so check the arc clears its height
            c.Expect(c.p.pos.x > 14f, $"a running jump from before the rail lands beyond it (x {c.p.pos.x:0.0})");
            saw.Despawn();
        }

        static IEnumerator BroodmotherCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((4000, F())));
            c.Teleport(6.5f, 1f);
            var b = c.Spawn<Broodmother>("broodmother", 16, 1);
            c.Expect(b != null, "broodmother spawned");
            if (b == null) yield break;
            bool ceiling = false, fell = false, stuck = false, web = false, eggs = false, hatched = false, shots = false, spatFromCeiling = false, shotElsewhere = false, lowered = false, landingWaves = false;
            int shotCount = 0;
            float ceilX = 0f, dropX = 0f; int spiders0 = c.Count<Spider>();
            for (int i = 0; i < 3000 && !(ceiling && stuck && web && eggs && hatched && spatFromCeiling && lowered); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.OnCeiling && !b.Spitting) { ceiling = true; ceilX = b.pos.x; if (i % 20 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_broodmother_ceiling.png")); }
                // venom only: the shockwaves of her landing are shots too (their picture flips with the direction)
                int nShots = 0, waves = 0, wavesL = 0, wavesR = 0;
                foreach (var e in c.world.Entities)
                    if (e is BossShot bs && !bs.Dead) { if (bs.flipByVelocity) { waves++; if (bs.vel.x < 0f) wavesL++; else wavesR++; } else nShots++; }
                if (b.Stuck && wavesL > 0 && wavesR > 0) landingWaves = true;
                if (nShots > shotCount) { if (b.Spitting) { spatFromCeiling = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_broodmother_spit.png")); } else shotElsewhere = true; }
                shotCount = nShots;
                if (spatFromCeiling && !b.OnCeiling && b.grounded && !b.Stuck && !b.Attacking) lowered = true;
                if (b.Attacking && !fell) { fell = true; dropX = ceilX; }       // where she was on the ceiling when she let go
                if (b.Stuck) stuck = true;
                if (c.Count<WebPatch>() > 0) web = true;
                if (c.Count<EggSac>() > 0) eggs = true;
                if (eggs && c.Count<Spider>() > spiders0) hatched = true;
                if (c.Count<BossShot>() > 0) shots = true;
            }
            c.Expect(ceiling, "she climbs to the ceiling");
            c.Expect(fell && Mathf.Abs(dropX - c.p.pos.x) < 1.2f, $"and crawls along it until she is above the player before she lets go (her x {dropX:0.0}, player {c.p.pos.x:0.0})");
            c.Expect(fell && stuck, "she drops and lands stuck for a moment (the opening)");
            c.Expect(landingWaves, "her landing sends a shockwave along the floor to each side");
            c.Expect(web, "she lays web on the floor");
            c.Expect(eggs, "she leaves egg sacs");
            c.Expect(hatched, "an egg sac left alone hatches a spiderling");
            c.Expect(spatFromCeiling && !shotElsewhere, $"she spits venom only from the ceiling (from the ceiling {spatFromCeiling}, elsewhere {shotElsewhere})");
            c.Expect(lowered, "after spitting she lowers herself to the floor without a drop attack");
            // only the fall hurts
            c.Expect(b.ContactDamage == 0 || b.Attacking, "on the ground or on the ceiling touching her is harmless");
            {
                // a cocoon hangs over the player: her next drop goes through it, and a spiderling falls out
                foreach (var e in c.world.Entities) if (!e.Dead && (e is EggSac || e is Spider)) e.Despawn();
                yield return c.Frames(2);
                var co = c.Spawn<Cocoon>("cocoon", Mathf.FloorToInt(c.p.pos.x), 6);
                c.Expect(co != null, "a cocoon hangs in the den");
                bool torn = false, spiderOut = false, tornByHer = false;
                for (int i = 0; i < 3000 && co != null && !(torn && spiderOut); i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (co.Dead && !torn) { torn = true; tornByHer = b.Attacking || b.Stuck; }
                    if (torn && c.Count<Spider>() > 0) spiderOut = true;
                    if (i == 30) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_broodmother_cocoon.png"));
                }
                c.Expect(torn && tornByHer, "dropping through the cocoon she tears it open");
                c.Expect(spiderOut, "and the spiderling inside falls out");
            }
            // web: slow on the ground, gone in time
            b.Despawn();
            ClearFight(c);
            foreach (var e in c.world.Entities) if (!e.Dead && (e is WebPatch || e is EggSac || e is Spider)) e.Despawn();
            c.p.godMode = false;
            yield return c.Frames(70);
            c.world.SpawnById("webpatch", new Vector2(10.5f, 1f));
            c.Teleport(10.5f, 1f);
            yield return c.Frames(20);
            c.Expect(c.p.Slowed, "standing in the web slows the player");
            c.Teleport(10.5f, 3.2f);
            c.p.motor.vel = Vector2.zero;
            yield return c.Frames(4);
            // an egg sac bursts at one stroke, before it hatches
            foreach (var e in c.world.Entities) if (!e.Dead && e is WebPatch) e.Despawn();
            var egg = c.world.SpawnById("eggsac", new Vector2(16.5f, 1f)) as EggSac;
            c.Expect(egg != null, "egg sac spawned");
            if (egg != null)
            {
                int sp0 = c.Count<Spider>();
                yield return c.Frames(10);
                egg.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(330);
                c.Expect(egg.Dead && c.Count<Spider>() == sp0, "cut in time, nothing hatches");
            }
            c.p.godMode = true;
        }

        static IEnumerator VillageLifeCase(Ctx c)
        {
            c.Bot(Seq((3000, F())));
            c.Teleport(4.5f, 1f);
            int n = Townsfolk.Populate(c.world);
            yield return c.Frames(2);       // (what is spawned joins the world at its next step)
            var folk = new List<Townsfolk>();
            foreach (var e in c.world.Entities) if (!e.Dead && e is Townsfolk t) folk.Add(t);
            int kids = 0; foreach (var t in folk) if (t.IsKid) kids++;
            c.Expect(n == 10 && folk.Count == 10 && kids == 4, $"six grown-ups and four children are out in the street ({folk.Count}, {kids} children)");
            var x0 = new List<float>(); foreach (var t in folk) x0.Add(t.pos.x);
            int hp = c.run.health, chasers0 = 0, swaps = 0;
            foreach (var t in folk) if (t.Chasing) chasers0++;
            var was = new List<bool>(); foreach (var t in folk) was.Add(t.Chasing);
            for (int i = 0; i < 900; i++)
            {
                yield return new WaitForFixedUpdate();
                for (int k = 0; k < folk.Count; k++) if (folk[k].Chasing != was[k]) { was[k] = folk[k].Chasing; swaps++; }
                if (i == 200) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "village_life.png"));
            }
            int moved = 0; for (int k = 0; k < folk.Count; k++) if (Mathf.Abs(folk[k].pos.x - x0[k]) > 0.5f) moved++;
            c.Expect(moved >= 7, $"they walk about ({moved} of {folk.Count} are somewhere else after a while)");
            c.Expect(chasers0 == 2 && swaps >= 2, $"the children play tag: one of each pair chases, and a touch swaps them ({swaps} swaps)");
            c.Expect(c.run.health == hp, "they are scenery: walking among them costs nothing");
            foreach (var t in folk) t.Despawn();
            // a thing looked at is read the way a sign is: a card, at the press of the button, gone when one walks off
            var hud = c.dir.hud;
            var cur = c.Spawn<Curio>("curio", 8, 1, 0, "the letter|A letter nobody sent.");
            c.Expect(cur != null, "a curio spawned");
            if (cur == null) yield break;
            c.Teleport(8.5f, 1f);
            yield return c.Frames(36);
            c.Expect(!hud.NoteUp && !hud.noteRoot.gameObject.activeSelf && hud.tagRoot.gameObject.activeSelf, "standing by it only offers a look");
            cur.Interact(c.p);
            yield return c.Frames(36);
            c.Expect(hud.NoteUp && hud.noteRoot.gameObject.activeSelf && hud.noteText.text.Contains("nobody sent"), "looked at, it is read on a sign's card");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_curio_note.png"));
            yield return c.Frames(3);
            // the same button again puts it down, and does not start it over
            c.Bot(Seq((2, F()), (1, F(act: true)), (60, F())));
            yield return c.Frames(36);
            c.Expect(!hud.NoteUp && !hud.noteRoot.gameObject.activeSelf, "the button again puts the card down");
            c.Bot(Seq((2, F()), (1, F(act: true)), (60, F())));
            yield return c.Frames(36);
            c.Expect(hud.NoteUp, "and once more reads it again");
            c.Bot(Seq((3000, F())));
            c.Teleport(14.5f, 1f);
            yield return c.Frames(36);
            c.Expect(!hud.NoteUp && !hud.noteRoot.gameObject.activeSelf, "walking away puts it down");
            cur.Despawn();
        }

        static IEnumerator CodexCase(Ctx c)
        {
            c.Bot(Seq((3000, F())));
            Codex.Forget();
            c.Expect(!Codex.Owned, "no book to begin with");
            // the book itself: a page for every place, creature and boss, each creature with a picture
            string missing = "";
            int places = 0;
            foreach (var pg in Codex.Pages)
            {
                if (pg.place) places++;
                if (string.IsNullOrEmpty(pg.text) || pg.text.Length < 40) missing += " (no text: " + pg.name + ")";
                if (!string.IsNullOrEmpty(pg.prefab))
                {
                    var pf = c.world.config.prefabs.Get(pg.prefab);
                    var r = pf != null ? pf.GetComponentInChildren<SpriteRenderer>(true) : null;
                    if (r == null || r.sprite == null) missing += " " + pg.prefab;
                }
            }
            int traps = 0; foreach (var pg in Codex.Pages) if (pg.info && !pg.place) traps++;
            c.Expect(Codex.Pages.Count >= 58 && places == 10 && traps >= 17 && missing == "", $"the book has {Codex.Pages.Count} pages over {places} places, {traps} of them on traps and the like, every creature with its picture{missing}");
            // the chest
            var ch = c.Spawn<Chest>("codexchest", 8, 1, 0, Codex.Key);
            c.Expect(ch != null && ch.gearId == Codex.Key, "the chest that holds it spawned");
            if (ch == null) yield break;
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((2, F()), (1, F(act: true)), (60, F())));
            yield return c.Frames(20);
            c.Expect(ch.Opened && Codex.Owned && c.dir.hud.CardUp && c.run.gear.Contains("recall") && GameDirector.tutorialChestOpened, "opened, it hands over the Field Guide and the Recall Charm, each on a card");
            c.Bot(Seq((3000, F())));
            // read from the bag
            var inv = c.dir.inventory;
            var book = c.dir.codex;
            c.Expect(book != null, "the game has a Field Guide screen");
            if (book == null) yield break;
            float scale = Time.timeScale;
            inv.Open();
            inv.SetTab(InventoryScreen.TabKeys);
            yield return new WaitForSecondsRealtime(0.1f);
            c.Expect(inv.IsOpen && inv.EntryCount >= 1, $"the bag lists it under KEY ITEMS ({inv.EntryCount} there)");
            inv.Toggle();
            yield return new WaitForSecondsRealtime(0.15f);
            // no page is without a picture: creatures and traps show themselves, places a view of their own stone
            string blank = "";
            for (int i = 0; i < Codex.Pages.Count; i++)
            {
                if (i > 0) book.Move(1);
                if (!book.PictureShown) blank += " " + Codex.Pages[i].name;
                if (Codex.Pages[i].info && !Codex.Pages[i].place) { yield return new WaitForSecondsRealtime(0.12f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex_info_" + i + ".png")); yield return new WaitForSecondsRealtime(0.12f); }
            }
            book.Move(-Codex.Pages.Count);
            // the mouse: a click on a line of the list opens that page
            book.Click(2);
            c.Expect(book.Selected == 2 && book.SelectedName == Codex.Pages[2].name, $"a click on the third line of the list opens the third page ({book.SelectedName})");
            book.Move(-Codex.Pages.Count);
            int arrow = Codex.Pages.FindIndex(pg => pg.name == "Arrow Trap");
            book.Move(arrow);
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex_trap.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            book.Move(-Codex.Pages.Count);
            int frost = Codex.Pages.FindIndex(pg => pg.name == "Frostdeep Hollows");
            book.Move(frost);
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex_place.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            book.Move(1);
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex_ice.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            book.Move(-Codex.Pages.Count);
            c.Expect(blank == "", "every page of the book shows a picture" + (blank != "" ? " - none on:" + blank : ""));
            c.Expect(book.IsOpen && !inv.IsOpen && Time.timeScale == 0f && book.SelectedName == Codex.Pages[0].name, $"confirm on it shuts the bag and opens the book at its first page; the game stands still (book {book.IsOpen}, bag {inv.IsOpen}, scale {Time.timeScale}, page {book.SelectedName})");
            c.Expect(GameDirector.tutorialBookRead, "the tutorial's gate knows the book has been opened");
            book.Jump(1);
            string place = book.SelectedName;
            book.Move(1);
            c.Expect(place == "Mossroot Caverns" && book.SelectedName == "Stalactite" && book.SelectedKnown, $"right jumps to the next place; a trap's page can be read from the start ({place} -> {book.SelectedName})");
            // a creature's page is blank until one has been killed; then it is written, and counts them
            Codex.ForgetKills();
            book.Move(3);
            yield return new WaitForSecondsRealtime(0.1f);
            c.Expect(book.SelectedName == "Snake" && !book.SelectedKnown && book.ShownName == "? ? ?" && book.killsText.text.EndsWith(" 0"), $"a creature nobody has killed: no name, nothing written ({book.ShownName}, {book.killsText.text})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex_unknown.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            book.Close();
            yield return new WaitForSecondsRealtime(0.1f);
            c.p.godMode = true;
            for (int k = 0; k < 2; k++)
            {
                var sn = c.Spawn<Enemy>("snake", 12, 1);
                yield return c.Frames(4);
                if (sn != null) sn.TakeDamage(new DamageInfo(9, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(4);
            }
            c.Expect(Codex.Kills("snake") == 2, $"two snakes killed by the player are counted ({Codex.Kills("snake")})");
            c.dir.OpenCodex();
            yield return new WaitForSecondsRealtime(0.15f);
            c.Expect(book.IsOpen && book.SelectedName == "Snake" && book.SelectedKnown && book.ShownName == "Snake" && book.killsText.text.EndsWith(" 2"), $"now its page is written, and says how many ({book.ShownName}, {book.killsText.text})");
            book.Move(3);
            yield return new WaitForSecondsRealtime(0.2f);
            c.Expect(book.SelectedName == "Broodmother" && !book.SelectedKnown, "the boss of the place is still unknown");
            book.Move(-3);
            yield return new WaitForSecondsRealtime(0.1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "codex.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            var over = UIOverlap.Find();
            c.Expect(over.Count == 0, "the book is laid out cleanly" + (over.Count > 0 ? " (" + over[0] + ")" : ""));
            book.Close();
            yield return new WaitForSecondsRealtime(0.1f);
            c.Expect(!book.IsOpen && Time.timeScale == scale, $"shut, the game goes on (book {book.IsOpen}, scale {Time.timeScale}, was {scale})");
            ch.Despawn();
        }

        static IEnumerator QuestCase(Ctx c)
        {
            c.Bot(Seq((6000, F())));
            var hud = c.dir.hud;
            var cat = c.dir.config.quests;
            var q = cat != null ? cat.Find("elder_word") : null;
            c.Expect(cat != null && q != null && q.steps.Length == 2 && q.rewards.Length == 1, "the catalogue has the elder's quest: two steps, one reward");
            if (q == null) yield break;
            Villagers.testRescued = new HashSet<string>();
            Villagers.ResetTestState();
            QuestLog.ResetForTests();
            QuestLog.Use(cat, false);
            c.Expect(QuestLog.Tracked == null && !QuestLog.GateHeld(out _), "nothing is open to begin with");
            QuestLog.Refresh();
            yield return c.Frames(40);
            c.Expect(QuestLog.IsActive("elder_word") && QuestLog.Tracked == q && hud.QuestShown == q.title && hud.QuestStepShown == q.steps[0].text,
                $"it starts by itself and the screen shows it and its first step ({hud.QuestShown} / {hud.QuestStepShown})");
            c.Expect(QuestLog.GateHeld(out string why) && why == q.gateMessage, "while it is open the village gate is held shut, and says why");
            // the marker points at the hall's door
            var hall = c.Spawn<VillageBuilding>("building", 14, 1, VillageBuilding.Hall);
            var other = c.Spawn<VillageBuilding>("building", 8, 1, 2);
            yield return c.Frames(6);
            bool pointed = QuestLog.Waypoint(c.world, c.p.Center, out Vector2 at);
            c.Expect(hall != null && hall.QuestId == "hall" && pointed && Mathf.Abs(at.x - hall.pos.x) < 0.1f && hud.WaypointVisible, $"the marker points at the Village Hall, not at the nearer house (x {at.x:0.0})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "quest_tracker.png"));
            yield return c.Frames(3);
            // something else happening does not move it on; going into the hall does
            QuestLog.Notify(QuestTrigger.Enter, "forge");
            c.Expect(QuestLog.StateOf("elder_word").step == 0, "going into another house is not the step");
            QuestLog.Notify(QuestTrigger.Enter, "hall");
            yield return c.Frames(36);
            c.Expect(QuestLog.StateOf("elder_word").step == 1 && hud.QuestStepShown == q.steps[1].text, $"going into the hall is: now it says to speak with him ({hud.QuestStepShown})");
            // the elder: he speaks first, and only when he has been heard out does the pouch change hands
            var elder = c.Spawn<Elder>("elder", 10, 1);
            yield return c.Frames(4);
            c.Teleport(10.5f, 1f);
            yield return c.Frames(6);
            pointed = QuestLog.Waypoint(c.world, c.p.Center, out at);
            c.Expect(pointed && Mathf.Abs(at.x - elder.pos.x) < 0.1f, "now the marker points at the elder himself");
            elder.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(hud.TalkUp && hud.TalkPages >= 4 && !Villagers.HasKey(Villagers.KeyPouch) && QuestLog.IsActive("elder_word"), $"spoken to, he talks ({hud.TalkPages} sentences) - and has given nothing yet");
            for (int i = 0; i < 40 && hud.TalkUp; i++) { hud.AdvanceTalk(); yield return c.Frames(2); }
            yield return c.Frames(6);
            c.Expect(!hud.TalkUp && Villagers.HasKey(Villagers.KeyPouch) && QuestLog.IsDone("elder_word") && hud.CardUp, "heard out, he hands over the Ember Pouch - on an item card - and the quest is done");
            c.Expect(!QuestLog.GateHeld(out _) && QuestLog.Tracked == null, "the gate is free again and nothing more is asked");
            yield return c.Frames(36);
            c.Expect(hud.QuestShown == null, "the quest leaves the screen");
            // after that he has his usual word, and nothing to give
            for (int i = 0; i < 600 && hud.CardUp; i++) yield return new WaitForFixedUpdate();
            elder.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(hud.TalkUp, "spoken to again, he says how the village stands");
            for (int i = 0; i < 20 && hud.TalkUp; i++) { hud.AdvanceTalk(); yield return c.Frames(2); }
            // a save that had the pouch before there were quests: the quest counts as done
            QuestLog.ResetForTests();
            QuestLog.Refresh();
            c.Expect(QuestLog.IsDone("elder_word") && QuestLog.Tracked == null, "a save that already has the pouch is not asked to fetch it again");
            QuestLog.ResetForTests();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            if (hall != null) hall.Despawn();
            if (other != null) other.Despawn();
            if (elder != null) elder.Despawn();
            hud.DismissHeld();
            yield return c.Frames(45);       // (his words have faded from the screen)
        }

        static IEnumerator MapCase(Ctx c)
        {
            c.p.godMode = true;
            var map = c.dir.GetComponent<MapScreen>();
            c.Expect(map != null, "every level has a map screen waiting");
            if (map == null) yield break;
            Villagers.testRescued = new HashSet<string>();       // the test keeps its own state: the save file is not touched
            Villagers.ResetTestState();
            // without the map in the bag: nothing is drawn, and the button does nothing
            c.Teleport(4.5f, 1f);
            yield return c.Frames(6);
            map.forceHeld = true;
            yield return new WaitForSecondsRealtime(0.15f);
            c.Expect(!MapScreen.Carried && !map.IsOpen && !map.Seen(4, 1), "with no map in the bag the button unfolds nothing, and nothing is put down");
            map.forceHeld = false;
            // with it: where the player has been is on the sheet, the far end of the level is not
            // (bought at the Mapmaker's it is a key item: kept with the save, nothing counted in the run)
            ShopItem.Apply(c.run, c.dir.config.shop.Find(MapScreen.ItemId));
            c.Expect(Villagers.HasKey(MapScreen.ItemId) && c.run.GetResource(MapScreen.ItemId) == 0, "the map bought is a key item, kept for good - not a thing of the run");
            yield return c.Frames(6);
            int far = c.world.Grid.Width - 3;
            c.Expect(MapScreen.Carried && map.Seen(4, 1) && map.Seen(8, 2) && !map.Seen(far, 1) && !map.IsOpen, $"with the map carried the level around the player is put down - not the far end (x {far}) - and the sheet stays folded");
            c.Teleport(20.5f, 1f);
            yield return c.Frames(6);
            c.Expect(map.Seen(20, 1) && map.Seen(4, 1), "walked further, more is put down - and what was drawn stays drawn");
            // held, the sheet is up; let go, it is gone
            map.forceHeld = true;
            yield return new WaitForSecondsRealtime(0.3f);
            c.Expect(map.IsOpen, "while the map button is held the sheet is up");
            var over = UIOverlap.Find();
            c.Expect(over.Count == 0, "the sheet is laid out cleanly" + (over.Count > 0 ? " (" + over[0] + ")" : ""));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "map_open.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            map.forceHeld = false;
            yield return new WaitForSecondsRealtime(0.15f);
            c.Expect(!map.IsOpen, "let go, it is folded away");
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            c.Teleport(3.5f, 1f);
            yield return c.Frames(4);
        }

        static IEnumerator MoreWaresCase(Ctx c)
        {
            var run = c.run;
            var shop = c.dir.config.shop;
            c.p.godMode = false;
            c.Bot(Seq((2000, F())));
            run.maxHealth = 10; run.health = 10;

            // the Fire Cloak: burning ground does nothing to the wearer; without it, it burns
            IEnumerator Burn()
            {
                c.Teleport(8.5f, 1f);
                yield return c.Frames(90);                         // (clear of the last hurt's grace)
                c.Spawn<FirePatch>("firepatch", 8, 1);
                yield return c.Frames(40);
            }
            run.AddUpgrade(shop.Find("firecloak"));
            int hp0 = run.health;
            yield return Burn();
            int cloaked = hp0 - run.health;
            run.RemoveUpgrade("firecloak");
            hp0 = run.health;
            yield return Burn();
            int bare = hp0 - run.health;
            c.Expect(cloaked == 0 && bare > 0, $"burning ground: nothing through the fire cloak, {bare} without it");
            run.health = run.maxHealth;
            yield return c.Frames(60);

            // the Antidote Vial: a web does not hold the wearer
            IEnumerator Web(bool vial)
            {
                if (vial) run.AddUpgrade(shop.Find("antidote")); else run.RemoveUpgrade("antidote");
                c.Teleport(10.5f, 1f);
                var w = c.Spawn<WebPatch>("webpatch", 10, 1);
                yield return c.Frames(10);
                bool slowed = c.p.Slowed;
                if (w != null && !w.Dead) w.Despawn();
                yield return c.Frames(20);
                c.Expect(vial ? !slowed : slowed, vial ? "with the antidote vial a web does not hold you" : "without it, the web holds you");
            }
            yield return Web(true);
            yield return Web(false);

            // the Climbing Gloves: up a ladder half as fast again
            IEnumerator Climb(bool gloves, System.Action<float> got)
            {
                if (gloves) run.AddUpgrade(shop.Find("climbgloves")); else run.RemoveUpgrade("climbgloves");
                c.Teleport(16.5f, 1f);
                yield return c.Frames(10);                       // (standing at the foot of the ladder first)
                c.Bot(Seq((600, F(up: true))));
                // measured once the player is on the ladder and going up (not while getting hold of it)
                for (int i = 0; i < 60 && !(c.p.pos.y > 1.6f && c.p.vel.y > 0.1f); i++) yield return c.Frames(1);
                float y0 = c.p.pos.y;
                yield return c.Frames(15);
                got(c.p.pos.y - y0);
                c.Bot(Seq((2000, F())));
                c.Teleport(8.5f, 1f);
                yield return c.Frames(10);
            }
            float bareUp = 0f, glovedUp = 0f;
            yield return Climb(false, v => bareUp = v);
            yield return Climb(true, v => glovedUp = v);
            run.RemoveUpgrade("climbgloves");
            c.Expect(bareUp > 0.5f && glovedUp > bareUp * 1.35f, $"up the ladder in the same time: {bareUp:0.00} bare-handed, {glovedUp:0.00} with the gloves");

            // the Grappling Hook: a rope thrown flies higher and hangs longer
            run.AddUpgrade(shop.Find("grapplinghook"));
            int ri = run.gear.IndexOf("rope");
            if (ri >= 0) run.SetEquipped(ri);
            run.ropes = Mathf.Max(run.ropes, 2);
            c.Teleport(8.5f, 1f);
            yield return c.Frames(4);
            c.Bot(Seq((1, F(use: true)), (2000, F())));
            yield return c.Frames(6);
            RopeProjectile rope = null;
            foreach (var e in c.world.Entities) if (e is RopeProjectile r && !r.Dead) rope = r;
            var pc = c.dir.config.player;
            c.Expect(rope != null && rope.maxRise == pc.ropeThrowHeight + 3 && rope.length == pc.ropeLength + 3,
                $"with the grappling hook a rope rises {rope?.maxRise} (not {pc.ropeThrowHeight}) and hangs {rope?.length} (not {pc.ropeLength})");
            run.RemoveUpgrade("grapplinghook");
            c.p.godMode = true;
            yield return c.Frames(4);
        }

        static IEnumerator ApothecaryCase(Ctx c)
        {
            c.p.godMode = true;
            Villagers.testRescued = new HashSet<string> { Villagers.All[2].id };
            Villagers.ResetTestState();
            var room = c.Spawn<VillageInterior>("interior", 4, 1, 2);
            yield return c.Frames(4);
            if (room == null) { c.Expect(false, "the apothecary is there"); yield break; }
            room.Enter(c.p);
            yield return c.Frames(4);
            VillagerHome keeper = null;
            foreach (var e in c.world.Entities) if (e is VillagerHome vh && !vh.Dead && vh.Who == Villagers.All[2]) keeper = vh;
            c.Expect(room.foreground != null && room.foreground.enabled && room.foreground.sprite != null, "the apothecary has a foreground: herbs hanging close to the eye");
            c.Expect(room.fire != null && room.fire.gameObject.activeSelf && room.fire.localScale.x < 0.9f, "a little fire burns under the cauldron");
            c.Expect(keeper != null, "Maren stands behind her counter");
            yield return new WaitForSecondsRealtime(1.5f);          // (the steam rising, the dust in the moonlight)
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "room_apothecary.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            foreach (var e in c.world.Entities) if (!e.Dead && (e is VillagerHome || e is Curio || e is VillageInterior || e is RoomToy)) e.Despawn();
            // every other room has its foreground too; a picture of each
            for (int k = 0; k < 8; k++)
            {
                if (k == 2) continue;
                Villagers.testRescued = new HashSet<string>(); foreach (var v in Villagers.All) Villagers.testRescued.Add(v.id);
                yield return c.Frames(2);
                var r = c.Spawn<VillageInterior>("interior", 4, 1, k);
                yield return c.Frames(4);
                if (r == null) { c.Expect(false, $"room {k} is there"); continue; }
                r.Enter(c.p);
                yield return new WaitForSecondsRealtime(0.8f);
                c.Expect(r.foreground != null && r.foreground.enabled, $"room {k} has its foreground");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, $"room_{k}.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                foreach (var e in c.world.Entities) if (!e.Dead && (e is VillagerHome || e is Curio || e is VillageInterior || e is Elder || e is RoomToy)) e.Despawn();
            }
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            c.dir.cameraFollow.SetBounds(new Rect(0, 0, c.world.Grid.Width, c.world.Grid.Height));
            yield return c.Frames(4);
        }

        static IEnumerator RoomToysCase(Ctx c)
        {
            c.p.godMode = true;
            Villagers.testRescued = new HashSet<string> { Villagers.All[1].id, Villagers.All[2].id };
            Villagers.ResetTestState();
            RoomToy Toy(RoomToy.Kind k) { foreach (var e in c.world.Entities) if (e is RoomToy t && !t.Dead && t.kind == k) return t; return null; }
            void Clear() { foreach (var e in c.world.Entities) if (!e.Dead && (e is VillagerHome || e is Curio || e is VillageInterior || e is RoomToy)) e.Despawn(); }

            // home: the cat, the lamps
            var home = c.Spawn<VillageInterior>("interior", 4, 1, VillageInterior.Home);
            yield return c.Frames(4);
            if (home == null) { c.Expect(false, "home is there"); yield break; }
            home.Enter(c.p);
            yield return c.Frames(20);
            var cat = Toy(RoomToy.Kind.Cat); var lamps = Toy(RoomToy.Kind.Lamps);
            c.Expect(cat != null && lamps != null, "home has its cat and its lamps");
            // no glints anywhere: what can be used is outlined while the player is by it, and only then
            Curio chest = null;
            foreach (var e in c.world.Entities) if (e is Curio cu && !cu.Dead && cu.form == Curio.Form.Chest) chest = cu;
            c.Expect(chest != null && !chest.IsOpen, "home has a chest to open");
            if (cat != null)
            {
                c.Expect(!cat.glow.enabled, "away from the cat, it has no outline");
                c.Teleport(cat.pos.x, 1f);
                yield return c.Frames(15);
                c.Expect(cat.glow.enabled && cat.glow.sprite != null, "standing by the cat, it is outlined");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "toys_cat_outline.png"));
                yield return c.Frames(3);
            }
            if (chest != null)
            {
                c.Teleport(chest.pos.x, chest.pos.y + 0.2f);
                yield return c.Frames(30);
                c.Expect(chest.glow.enabled && chest.GetPrompt(c.p).Contains("Open"), "by the chest upstairs: outlined, it offers to be opened");
                chest.Interact(c.p);
                yield return c.Frames(20);
                c.Expect(chest.IsOpen && c.dir.hud.NoteUp && chest.GetPrompt(c.p).Contains("Read"), "opened, the scrap inside is read, and it stays open");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "read_chest_open.png"));
                yield return c.Frames(3);
                c.dir.hud.DismissHeld();
                c.Teleport(home.pos.x + 1f, 1f);
                yield return c.Frames(30);
            }
            if (cat != null)
            {
                cat.Interact(c.p);
                yield return c.Frames(10);
                c.Expect(cat.Busy, "petted, the cat wakes up");
                for (int i = 0; i < 300 && cat.Busy; i++) yield return c.Frames(1);
                c.Expect(!cat.Busy && cat.CatSpot == 0, "then it goes back to sleep by the fire");
                float y0 = cat.pos.y;
                cat.pokes = 9;
                cat.Interact(c.p);
                for (int i = 0; i < 400 && cat.Busy; i++) yield return c.Frames(1);
                c.Expect(cat.CatSpot == 1 && cat.pos.y > y0 + 0.8f, $"the tenth poke: it moves to the armchair (up {cat.pos.y - y0:0.00}; player grounded {c.p.motor.grounded}, {c.p.motor.mode}, at {c.p.pos})");
            }
            if (lamps != null)
            {
                var lit = home.Picture;
                lamps.Interact(c.p);
                yield return c.Frames(4);
                c.Expect(home.LampsOut && home.Picture != lit && home.Picture != null, "the lamps go out: the room is the dark picture");
                yield return new WaitForSecondsRealtime(0.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "toys_home_dark.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                lamps.Interact(c.p);
                yield return c.Frames(4);
                c.Expect(!home.LampsOut && home.Picture == lit, "and lit again");
            }
            Clear();
            yield return c.Frames(4);

            // the ropewalk: the wheel, the hammock
            var rope = c.Spawn<VillageInterior>("interior", 4, 1, 1);
            yield return c.Frames(4);
            rope.Enter(c.p);
            yield return c.Frames(20);
            var wheel = Toy(RoomToy.Kind.Wheel); var ham = Toy(RoomToy.Kind.Hammock);
            c.Expect(wheel != null && ham != null, "the ropewalk has its wheel and its hammock");
            if (wheel != null)
            {
                wheel.Interact(c.p);
                yield return c.Frames(25);
                float a1 = wheel.Angle;
                c.Expect(a1 > 60f, $"the wheel spins ({a1:0} degrees)");
                yield return new WaitForSecondsRealtime(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "toys_ropewalk.png"));
                yield return new WaitForSecondsRealtime(6f);
                float a2 = wheel.Angle;
                yield return c.Frames(20);
                c.Expect(Mathf.Abs(wheel.Angle - a2) < 0.5f, "and slows to a stop");
            }
            if (ham != null)
            {
                c.Teleport(ham.pos.x, ham.pos.y + 0.2f);
                yield return c.Frames(30);
                ham.Interact(c.p);
                yield return c.Frames(40);
                c.Expect(c.p.restingIn == ham && c.p.pos.y > ham.pos.y + 1.2f, "the player lies in the hammock, off the floor");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "toys_hammock.png"));
                for (int i = 0; i < 400 && c.p.restingIn != null; i++) yield return c.Frames(1);
                yield return c.Frames(80);
                c.Expect(c.p.restingIn == null && c.p.motor.grounded && Mathf.Abs(c.p.pos.y - ham.pos.y) < 0.2f, "and is tipped out onto the floor");
            }
            Clear();
            yield return c.Frames(4);

            // the apothecary: the brew
            var apo = c.Spawn<VillageInterior>("interior", 4, 1, 2);
            yield return c.Frames(4);
            apo.Enter(c.p);
            yield return c.Frames(20);
            var pot = Toy(RoomToy.Kind.Cauldron);
            c.Expect(pot != null, "Maren's cauldron can be stirred");
            if (pot != null) { pot.Interact(c.p); yield return c.Frames(4); c.Expect(pot.Stirring, "stirred, it bubbles up"); }
            Clear();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            c.dir.cameraFollow.SetBounds(new Rect(0, 0, c.world.Grid.Width, c.world.Grid.Height));
            yield return c.Frames(4);
        }

        static IEnumerator RoomToys2Case(Ctx c)
        {
            c.p.godMode = true;
            Villagers.testRescued = new HashSet<string>(); foreach (var v in Villagers.All) Villagers.testRescued.Add(v.id);
            Villagers.ResetTestState();
            RoomToy Toy(RoomToy.Kind k) { foreach (var e in c.world.Entities) if (e is RoomToy t && !t.Dead && t.kind == k) return t; return null; }
            void Clear() { foreach (var e in c.world.Entities) if (!e.Dead && (e is VillagerHome || e is Curio || e is VillageInterior || e is RoomToy || e is Elder)) e.Despawn(); }
            VillageInterior room = null;
            IEnumerator Enter(int k) { Clear(); yield return c.Frames(3); room = c.Spawn<VillageInterior>("interior", 4, 1, k); yield return c.Frames(4); room.Enter(c.p); yield return c.Frames(25); }
            IEnumerator StandBy(RoomToy t) { c.Teleport(t.pos.x, t.pos.y + 0.2f); yield return c.Frames(30); }
            IEnumerator Shot(string n) { yield return new WaitForSecondsRealtime(0.25f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, n)); yield return new WaitForSecondsRealtime(0.2f); }

            // the forge
            yield return Enter(0);
            var bel = Toy(RoomToy.Kind.Bellows); var anv = Toy(RoomToy.Kind.Anvil);
            c.Expect(bel != null && anv != null, "the forge has its bellows and anvil");
            if (bel != null) { yield return StandBy(bel); c.Expect(bel.glow.enabled, "by the bellows, they are outlined"); yield return Shot("toys2_forge_outline.png"); bel.Interact(c.p); yield return c.Frames(15); c.Expect(room.FireBoost > 0.2f, "worked, the bellows make the fire roar"); }
            if (anv != null) { yield return StandBy(anv); for (int i = 0; i < 3; i++) { anv.Interact(c.p); yield return c.Frames(8); } for (int i = 0; i < 120 && !anv.Answered; i++) yield return c.Frames(1); c.Expect(anv.Answered, "three quick strikes on the anvil: Hilde strikes back"); }

            // the map room
            yield return Enter(3);
            var globe = Toy(RoomToy.Kind.Globe); var scope = Toy(RoomToy.Kind.Telescope);
            c.Expect(globe != null && scope != null, "the map room has its globe and telescope");
            if (globe != null)
            {
                yield return StandBy(globe); globe.Interact(c.p); yield return c.Frames(20);
                c.Expect(globe.Busy, "the globe spins");
                for (int i = 0; i < 400 && globe.Busy; i++) yield return c.Frames(1);
                c.Expect(!globe.Busy && globe.GlobeTurns == 1 && globe.GlobeFrame == 0, "and comes to rest with the burned hole facing out");
            }
            if (scope != null)
            {
                yield return StandBy(scope); scope.Interact(c.p); yield return c.Frames(40);
                c.Expect(scope.Viewing && c.p.restingIn == scope, "looking through the telescope: the view, the player still");
                yield return Shot("toys2_telescope.png");
                for (int i = 0; i < 400 && scope.Viewing; i++) yield return c.Frames(1);
                c.Expect(!scope.Viewing && c.p.restingIn == null, "and then back to the room");
                yield return c.Frames(10);
                scope.Interact(c.p); yield return c.Frames(40);
                yield return Shot("toys2_telescope_climber.png");
                for (int i = 0; i < 400 && scope.Viewing; i++) yield return c.Frames(1);
            }

            // Pip's
            yield return Enter(4);
            var owl = Toy(RoomToy.Kind.Owl); var clk = Toy(RoomToy.Kind.Clock); var cage = Toy(RoomToy.Kind.Birdcage);
            c.Expect(owl != null && clk != null && cage != null, "Pip's has an owl, a clock and a birdcage");
            yield return Shot("toys2_pip.png");
            if (owl != null) { yield return StandBy(owl); for (int i = 0; i < 5; i++) { owl.Interact(c.p); for (int j = 0; j < 80 && owl.Busy; j++) yield return c.Frames(1); } c.Expect(owl.Hooed, "the fifth poke: the stuffed owl hoots"); }
            if (clk != null) { yield return StandBy(clk); clk.Interact(c.p); yield return c.Frames(10); c.Expect(clk.Busy, "the clock's hands run backwards"); for (int i = 0; i < 200 && clk.Busy; i++) yield return c.Frames(1); c.Expect(!clk.Busy, "and stop where they were"); }
            if (cage != null)
            {
                yield return StandBy(cage); cage.Interact(c.p); yield return c.Frames(60);
                c.Expect(cage.BirdOut > 1f, $"the bird flies out round the room ({cage.BirdOut:0.0} out)");
                yield return Shot("toys2_bird.png");
                for (int i = 0; i < 300 && cage.Busy; i++) yield return c.Frames(1);
                c.Expect(cage.BirdOut < 0.1f, "and back into its cage");
            }

            // the lamp house
            yield return Enter(5);
            var row = Toy(RoomToy.Kind.LampRow); var great = Toy(RoomToy.Kind.GreatLamp);
            c.Expect(row != null && great != null, "the lamp house has its row of lamps and the great lamp");
            if (row != null)
            {
                yield return StandBy(row); row.Interact(c.p); yield return c.Frames(60);
                c.Expect(row.LampsDark >= 3, $"snuffed, the lamps go out one by one ({row.LampsDark} out)");
                yield return Shot("toys2_lamprow.png");
                for (int i = 0; i < 300 && row.Busy; i++) yield return c.Frames(1);
                c.Expect(row.LampsDark == 0, "and are lit again");
            }
            if (great != null) { yield return StandBy(great); great.Interact(c.p); yield return c.Frames(5); c.Expect(great.Flashing, "touched, the great lamp flares"); for (int i = 0; i < 100 && great.Busy; i++) yield return c.Frames(1); }

            // the hall
            yield return Enter(6);
            var cup = Toy(RoomToy.Kind.Cup); var ban = Toy(RoomToy.Kind.Banner); var can = Toy(RoomToy.Kind.Candles);
            c.Expect(cup != null && ban != null && can != null, "the hall has a cup, the banner and the candles");
            if (cup != null) { yield return StandBy(cup); cup.Interact(c.p); yield return c.Frames(5); c.Expect(cup.Drunk == 1, "the cup is drunk from"); }
            if (ban != null) { yield return StandBy(ban); ban.Interact(c.p); yield return c.Frames(10); c.Expect(ban.Busy, "touched, the banner stirs"); yield return Shot("toys2_banner.png"); }
            if (can != null)
            {
                yield return StandBy(can);
                for (int i = 0; i < 3; i++) { can.Interact(c.p); yield return c.Frames(10); }
                c.Expect(!can.CandleLit(0) && !can.CandleLit(1), "two candles blown out");
                for (int i = 0; i < 150 && !can.CandleLit(2); i++) yield return c.Frames(1);
                c.Expect(can.CandleLit(2) && !can.CandleLit(0), "the third lights itself again");
            }

            // home: the bed
            yield return Enter(VillageInterior.Home);
            var bed = Toy(RoomToy.Kind.Bed);
            c.Expect(bed != null, "home has its bed to sleep in");
            if (bed != null)
            {
                yield return StandBy(bed);
                int hour = VillageClock.Hour;
                bed.Interact(c.p);
                yield return c.Frames(10);
                c.Expect(bed.Napping && c.p.restingIn == bed, "lying down for a nap");
                for (int i = 0; i < 300 && bed.Napping; i++) yield return c.Frames(1);
                yield return c.Frames(5);
                c.Expect(!bed.Napping && c.p.restingIn == null && !string.IsNullOrEmpty(bed.LastDream) && c.dir.hud.NoteUp, "waking, a dream is remembered");
                c.Expect(VillageClock.Hour == hour, "the hour stays the player's own");
                c.dir.hud.DismissHeld();
            }

            // Tam's coil
            yield return Enter(1);
            var coil = Toy(RoomToy.Kind.Coil);
            c.Expect(coil != null, "the ropewalk has a coil on the floor");
            if (coil != null) { float x0 = coil.pos.x; c.Teleport(coil.pos.x - 0.6f, 1.2f); yield return c.Frames(30); coil.Interact(c.p); for (int i = 0; i < 60 && coil.Busy; i++) yield return c.Frames(1); c.Expect(Mathf.Abs(coil.pos.x - x0) > 1f, $"kicked, the coil rolls away ({coil.pos.x - x0:0.0})"); }

            Clear();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            c.dir.cameraFollow.SetBounds(new Rect(0, 0, c.world.Grid.Width, c.world.Grid.Height));
            yield return c.Frames(4);
        }

        static IEnumerator LadderGrabCase(Ctx c)
        {
            c.p.godMode = true;
            IEnumerator Try(float x, System.Action<float, float> got)
            {
                c.Bot(Seq((600, F())));
                c.Teleport(x, 1f);
                yield return c.Frames(10);
                c.Bot(Seq((40, F(up: true)), (600, F())));
                yield return c.Frames(30);
                got(c.p.pos.y, c.p.pos.x);
                c.Bot(Seq((600, F())));
                c.Teleport(4.5f, 1f);
                yield return c.Frames(20);
            }
            float yTouch = 0f, xTouch = 0f, yAway = 0f;
            yield return Try(10.5f + 0.68f, (y, x) => { yTouch = y; xTouch = x; });      // the body overlaps the ladder by a little
            yield return Try(10.5f + 1.1f, (y, x) => { yAway = y; });                   // a step away: not touching it
            c.Expect(yTouch > 2f && Mathf.Abs(xTouch - 10.5f) < 0.05f, $"half beside the ladder, up climbs it (up to {yTouch:0.00}, drawn onto it at x {xTouch:0.00})");
            c.Expect(yAway < 1.2f, $"a step away from it, up does not climb (height {yAway:0.00})");
            // a word with a keeper first (up and down choose the row while beside them): walked away, up climbs again
            Villagers.testRescued = new HashSet<string> { Villagers.All[2].id };
            Villagers.ResetTestState();
            var keeper = c.Spawn<VillagerHome>("villagerhome", 6, 1, 2);
            yield return c.Frames(4);
            c.Bot(Seq((600, F())));
            c.Teleport(6.5f, 1f);
            yield return c.Frames(10);
            float yAfter = 0f;
            yield return Try(10.5f, (y, x) => { yAfter = y; });
            c.Expect(keeper != null && yAfter > 2f, $"after standing by a keeper (up and down choose Talk or Shop there), up climbs the ladder again (up to {yAfter:0.00})");
            if (keeper != null && !keeper.Dead) keeper.Despawn();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
        }

        static IEnumerator WispCase(Ctx c)
        {
            var run = c.run;
            var shop = c.dir.config.shop;
            var wisp = shop.Find("wisp");
            c.Expect(wisp != null && wisp.isUpgrade && wisp.slot == "charm" && wisp.modifiers.revive == 5 && wisp.icon != null && System.Array.IndexOf(Villagers.All[2].wares, "wisp") >= 0,
                "the Hearth Wisp is a charm that turns a death away with 5 health, and Maren draws its blueprint");
            if (wisp == null) yield break;
            c.p.godMode = false;
            c.Bot(Seq((600, F())));
            c.Teleport(8.5f, 1f);
            yield return c.Frames(4);
            c.Expect(c.p.wispRenderer != null && !c.p.wispRenderer.enabled, "no wisp at the shoulder while none is worn");
            run.AddUpgrade(wisp);
            yield return c.Frames(4);
            c.Expect(run.IsWorn("wisp") && run.stats.revive == 5 && c.p.wispRenderer != null && c.p.wispRenderer.enabled, "worn, the wisp flies at the shoulder");
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "wisp.png"));
            yield return c.Frames(4);
            run.maxHealth = 10; run.health = 2;
            c.p.TakeDamage(new DamageInfo(3, DamageType.Generic, null, Vector2.zero, "a test"));
            yield return c.Frames(4);
            c.Expect(!c.p.IsDead && run.health == 5 && !run.HasUpgrade("wisp") && c.p.Revived == 1 && !c.p.wispRenderer.enabled,
                $"a blow that would kill: up again with 5 health (health {run.health}), the wisp spent and gone");
            // the Reaper is not turned away (not tried here: it would end the scenario) - nor a second death without a wisp
            run.health = run.maxHealth;
            c.p.godMode = true;
        }

        static IEnumerator LootChestCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((600, F())));
            var chest = c.Spawn<Chest>("lootchest", 10, 1);
            yield return c.Frames(3);
            c.Expect(chest != null && chest.loot && !chest.Opened && chest.GetPrompt(c.p) != null, "a loot chest stands there, closed, and can be opened");
            if (chest == null) yield break;
            // eight chests in a row without equipment: 25% + 8 x 10% - this one surely has a piece
            c.run.lootMisses = 8;
            c.Expect(Mathf.Approximately(chest.GearChanceFor(c.run), 1f) && Mathf.Approximately(chest.GearChanceFor(null), chest.lootGearChance),
                $"the chance of equipment grows by a step for every chest without one ({chest.GearChanceFor(null):0.00} at first, {chest.GearChanceFor(c.run):0.00} after eight)");
            int pickups0 = 0;
            foreach (var e in c.world.Entities) if (e is Pickup pk && !pk.Dead) pickups0++;
            chest.Interact(c.p);
            yield return c.Frames(90);
            int pickups = 0; bool allDown = true;
            foreach (var e in c.world.Entities)
                if (e is Pickup pk && !pk.Dead) { pickups++; if (pk.pos.y > 1.6f) allDown = false; }
            c.Expect(chest.Opened && Chest.lastLoot.Count == chest.lootRolls && pickups - pickups0 == chest.lootRolls && allDown,
                $"opened, it throws out {Chest.lastLoot.Count} things ({string.Join(", ", Chest.lastLoot)}) that land on the floor ({pickups - pickups0} lying there)");
            ShopItem gear = null;
            foreach (var e in c.world.Entities) if (e is ShopItem si && !si.Dead && si.offer != null && si.offer.id == Chest.lastLootGear) gear = si;
            string prompt = gear != null ? gear.GetPrompt(c.p) : null;
            c.Expect(Chest.lastLootGear != null && gear != null && gear.offer.isUpgrade && !c.run.HasUpgrade(gear.offer.id) && c.run.lootMisses == 0,
                $"beside it lies a piece of equipment the run does not own yet ({Chest.lastLootGear}), and the chance is back to the base ({c.run.lootMisses} misses)");
            c.Teleport(gear != null ? gear.pos.x : 10.5f, 1f);
            yield return c.Frames(4);
            prompt = gear != null ? gear.GetPrompt(c.p) : null;
            c.Expect(prompt != null && prompt.Contains("free"), $"and it is free to take: \"{prompt}\"");
            c.Expect(chest.GetPrompt(c.p) == null, "an opened chest asks nothing more");
            // a chest without equipment counts a miss for the next one
            var second = c.Spawn<Chest>("lootchest", 18, 1);
            yield return c.Frames(3);
            if (second != null)
            {
                second.lootGearChance = 0f; second.lootGearStep = 0f;
                second.Interact(c.p);
                yield return c.Frames(10);
                c.Expect(Chest.lastLootGear == null && c.run.lootMisses == 1, $"a chest with no equipment raises the count by one ({c.run.lootMisses})");
            }
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "loot_chest.png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static IEnumerator NewWaresCase(Ctx c)
        {
            var shop = c.dir.config.shop;
            var run = c.run;
            Villagers.testRescued = new HashSet<string>();       // the test keeps its own state: the save file is not touched
            Villagers.ResetTestState();
            // the stalls: no bombs or ropes in the village (every climb sets out with its bags full), and the new wares
            bool bagsInVillage = false;
            foreach (var v in Villagers.All) if (v.id != "peddler") foreach (var w in v.wares) if (w == "bombs" || w == "ropes") bagsInVillage = true;
            bool Sells(int who, string id) => System.Array.IndexOf(Villagers.All[who].wares, id) >= 0 && shop.Find(id) != null;
            c.Expect(!bagsInVillage && Sells(4, "bombs") && Sells(4, "ropes") && Sells(4, "pebbles") && Sells(0, "hobnailboots") && !Blueprints.VillageWares.Contains("ironcap") && !Blueprints.VillageWares.Contains("softsoles") && !Blueprints.VillageWares.Contains("compass") && Sells(3, "spyglass") && Sells(3, Villagers.KeyLetter) && Sells(4, FlareGear.OfferId),
                "bombs, ropes and pebbles only at Pip's; Hilde has hobnail boots, Oswin a spyglass and his letter, Pip flares; the iron cap, soft soles and compass are open from the start (no stall)");
            c.Expect(Villagers.PaidInEmbers(shop.Find(Villagers.KeyLetter)) && Villagers.PaidInEmbers(shop.Find("spyglass")) && Villagers.PaidInEmbers(shop.Find(FlareGear.OfferId)),
                "the letter and the blueprints of the rest last, and are paid in embers");

            // soft soles: a 10-tile fall (that hurts bare-footed: 'long fall hurts and stuns') costs nothing
            run.AddUpgrade(shop.Find("softsoles"));
            c.Expect(run.IsWorn("softsoles") && run.stats.fallBonus >= 3f, "soft soles are worn at once (feet)");
            c.Bot(Seq((600, F())));
            int hp0 = run.health;
            c.Teleport(12.5f, 1f + 10.5f);
            yield return c.Frames(90);
            c.Expect(run.health == hp0 && !c.p.IsStunned, $"with soft soles a 10-tile fall does not hurt (health {hp0} -> {run.health})");
            run.RemoveUpgrade("softsoles");

            // hobnail boots: a stomp does two
            run.AddUpgrade(shop.Find("hobnailboots"));
            c.Expect(run.IsWorn("hobnailboots") && run.stats.stompBonus == 1, "hobnail boots are worn (feet)");
            var snake = c.Spawn<Snake>("snake", 12, 1);
            yield return c.Frames(2);
            if (snake != null)
            {
                snake.heartDropChance = 0f; snake.stunTimer = 99f;
                snake.maxHealth = 5; snake.health = 5;
                c.Teleport(snake.pos.x, 4.5f);
                int first = -1;
                for (int i = 0; i < 90 && first < 0; i++) { yield return new WaitForFixedUpdate(); if (snake.health < 5) first = 5 - snake.health; }
                c.Expect(first == 2, $"with hobnail boots one stomp takes 2 (took {first})");
                if (!snake.Dead) snake.Despawn();
            }
            run.RemoveUpgrade("hobnailboots");

            // an iron cap: a stalactite on the head does one less
            int StalDamage() { var pf = c.dir.config.prefabs.Get("stalactite") as Stalactite; return pf != null ? pf.damage : 2; }
            IEnumerator DropOnHead()
            {
                c.Teleport(8.5f, 1f);
                yield return c.Frames(80);                       // (clear of the last hurt's grace)
                c.Spawn<Stalactite>("stalactite", 8, 6, 1);      // param 1: already falling
                yield return c.Frames(60);
            }
            run.AddUpgrade(shop.Find("ironcap"));
            c.Expect(run.IsWorn("ironcap") && run.stats.headGuard > 0f, "the iron cap is worn (body)");
            hp0 = run.health;
            yield return DropOnHead();
            int capped = hp0 - run.health;
            run.RemoveUpgrade("ironcap");
            hp0 = run.health;
            yield return DropOnHead();
            int bare = hp0 - run.health;
            c.Expect(bare == StalDamage() && capped == bare - 1, $"a stalactite costs {bare} bare-headed and {capped} under the iron cap");
            run.health = run.maxHealth;

            // the spyglass: looking up goes twice as far
            c.Teleport(8.5f, 1f);
            c.Bot(Seq((90, F(up: true)), (600, F())));
            yield return c.Frames(85);
            float plain = c.p.LookOffset;
            c.Bot(Seq((30, F()), (90, F(up: true)), (600, F())));
            run.AddUpgrade(shop.Find("spyglass"));
            yield return c.Frames(30 + 85);
            float far = c.p.LookOffset;
            c.Expect(plain > 3f && Mathf.Abs(far - 2f * plain) < 0.3f, $"looking up: {plain:0.0} tiles bare, {far:0.0} with the spyglass (twice as far)");
            run.RemoveUpgrade("spyglass");
            c.Bot(Seq((600, F())));
            yield return c.Frames(30);

            // flares: bought three at a time with the gear; one thrown lights up, burns, then is gone
            ShopItem.Apply(run, shop.Find(FlareGear.OfferId));
            int gi = run.gear.IndexOf(FlareGear.Id);
            c.Expect(gi >= 0 && run.GetResource(FlareGear.AmmoId) == 3, $"bought, the flares come with the gear ({run.GetResource(FlareGear.AmmoId)} to throw)");
            if (gi >= 0)
            {
                run.SetEquipped(gi);
                c.Bot(Seq((1, F(use: true)), (600, F())));
                yield return c.Frames(40);
                Flare lit = null;
                foreach (var e in c.world.Entities) if (e is Flare fl && !fl.Dead) lit = fl;
                var glow = lit != null ? lit.GetComponent<GlowLight>() : null;
                c.Expect(lit != null && lit.Burning && glow != null && glow.IsOn && run.GetResource(FlareGear.AmmoId) == 2,
                    $"use throws a burning flare with its light on ({run.GetResource(FlareGear.AmmoId)} left)");
                int hpF = run.health;
                if (lit != null)
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "flare_lit.png"));
                    yield return c.Frames(5);
                    lit.BurnFor(0.3f);
                    yield return c.Frames(40);
                    c.Expect(lit.Dead && run.health == hpF, "it burns out and is gone, having hurt nobody");
                }
            }

            // the lost letter: a key item; the level with a cage says so, and the map marks it
            ShopItem.Apply(run, shop.Find(Villagers.KeyLetter));
            c.Expect(Villagers.HasKey(Villagers.KeyLetter) && run.GetResource(Villagers.KeyLetter) == 0, "the letter bought is a key item, kept for good");
            c.Expect(c.dir.LetterNote() == null, "with nobody held in the level the letter says nothing");
            var cage = c.Spawn<Captive>("captive", 18, 1);
            yield return c.Frames(3);
            string note = c.dir.LetterNote();
            c.Expect(cage != null && !cage.Dead && note != null && note.Contains(cage.Who.name), $"with a villager held here the letter names them: \"{note}\"");
            ShopItem.Apply(run, shop.Find(MapScreen.ItemId));
            var map = c.dir.GetComponent<MapScreen>();
            if (map != null)
            {
                map.forceHeld = true;
                yield return new WaitForSecondsRealtime(0.3f);
                c.Expect(map.IsOpen && map.MarksCage(18, 1), "on the map the cage is marked, though nobody has been near it");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "map_letter.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                map.forceHeld = false;
                yield return new WaitForSecondsRealtime(0.1f);
            }
            if (cage != null && !cage.Dead) cage.Despawn();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            c.Teleport(3.5f, 1f);
            yield return c.Frames(4);
        }

        static IEnumerator StoryCase(Ctx c)
        {
            c.p.godMode = true;
            var hud = c.dir.hud;
            var run = c.run;
            Villagers.testRescued = new HashSet<string>();       // the test keeps its own state: the save file is not touched
            Villagers.ResetTestState();
            Villagers.GiveKey(Villagers.KeyPouch);
            QuestLog.ResetForTests();
            QuestLog.Refresh();
            IEnumerator PutAway() { for (int i = 0; i < 60 && (hud.CardModal || hud.CardUp); i++) { hud.DismissCard(); yield return c.Frames(2); } hud.DismissHeld(); yield return c.Frames(2); }

            // a pillar: looked at with empty hands it cannot be read - and the task begins
            var pillar = c.Spawn<RunePillar>(Story.PillarId, 6, 1);
            yield return c.Frames(3);
            c.Expect(pillar != null && pillar.GetPrompt(c.p).Contains("Look") && !QuestLog.IsActive(Story.QuestSigns), "a pillar stands there to be looked at; nothing is asked yet");
            if (pillar == null) yield break;
            pillar.Interact(c.p);
            yield return c.Frames(4);
            yield return c.Frames(40);
            {
                // its first step is a long sentence: the frame grows to hold both lines of it
                var box = hud.questRoot.rect; var words = hud.questStep;
                float bottom = -words.rectTransform.anchoredPosition.y + words.preferredHeight;
                c.Expect(hud.QuestStepShown != null && words.textInfo.lineCount >= 2 && bottom <= box.height - 8f, $"a step two lines long stays inside its frame (text ends at {bottom:0}, frame is {box.height:0} high)");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "story_task.png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }
            c.Expect(!Story.HasRubbing && QuestLog.IsActive(Story.QuestSigns) && QuestLog.StateOf(Story.QuestSigns).step == 0,
                "looked at with nothing to copy onto: a note says so, and the task of the old signs begins");
            yield return PutAway();
            // with a sheet of paper it is copied: the sheet is spent, the copy is kept
            run.AddResource(Story.Paper, 1);
            c.Expect(pillar.GetPrompt(c.p).Contains("Copy"), "with paper in the bag the pillar offers to be copied");
            pillar.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(Story.HasRubbing && run.GetResource(Story.Paper) == 0 && hud.CardUp && QuestLog.StateOf(Story.QuestSigns).step == 2,
                $"the signs are copied: the paper is spent, the copy is a key item on a card, and the task says to take it home (step {QuestLog.StateOf(Story.QuestSigns).step})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "story_rubbing.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return PutAway();
            c.Expect(!Story.StrangerIn(run) && !Story.Decoded, "nothing of the hammer is on the mountain before the signs are read");
            // the elder reads it
            var elder = c.Spawn<Elder>("elder", 10, 1);
            yield return c.Frames(4);
            elder.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(hud.TalkUp && !Story.Decoded, "the elder speaks first");
            for (int i = 0; i < 60 && hud.TalkUp; i++) { hud.AdvanceTalk(); yield return c.Frames(2); }
            yield return c.Frames(6);
            c.Expect(Story.Decoded && QuestLog.IsDone(Story.QuestSigns), "heard out, he has read the signs: the task is done and the reading is kept");
            yield return PutAway();
            c.Expect(pillar.GetPrompt(c.p).Contains("Read"), "a pillar can be read from then on");
            {
                // the copy in the bag: a short word about it, inside its frame - what the signs say is read with its button
                // (a card still being read goes first)
                hud.ClearCards();
                yield return new WaitForSecondsRealtime(0.8f);           // (the card fades out)
                var inv = c.dir.inventory;
                inv.Open();
                inv.SetTab(InventoryScreen.TabKeys);
                for (int i = 0; i < 6 && inv.descText.text.IndexOf("rubbed onto paper") < 0; i++) { inv.Move(1); yield return new WaitForSecondsRealtime(0.05f); }
                yield return new WaitForSecondsRealtime(0.15f);
                var over = UIOverlap.Find();
                c.Expect(inv.IsOpen && inv.descText.text.Contains("rubbed onto paper") && !inv.descText.text.Contains("STOPPED DAY") && over.Count == 0,
                    $"the bag says in two lines what the copy is, and nothing runs out of the frame ({(over.Count > 0 ? over[0] : "clean")})");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "story_bag.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                if (inv.IsOpen) inv.Toggle();
                yield return new WaitForSecondsRealtime(0.15f);
            }
            if (elder != null) elder.Despawn();
            // where the parts are in a run
            int keep = run.stageIndex;
            run.stageIndex = 5; bool s5 = Story.StrangerIn(run) && !Story.HeadIn(run) && !Story.GhostIn(run);
            run.stageIndex = 9; bool s9 = Story.HeadIn(run) && !Story.StrangerIn(run);
            run.stageIndex = 12; bool s12 = Story.GhostIn(run); run.stageIndex = 13; bool s13 = Story.GhostIn(run); run.stageIndex = 14; bool s14 = Story.GhostIn(run);
            run.stageIndex = 15; bool s15 = !Story.GhostIn(run);
            run.stageIndex = 4; bool s4 = Story.PillarIn(4) && !Story.StrangerIn(run);
            c.Expect(s5 && s9 && s12 && s13 && s14 && s15 && s4, $"the stranger is in the second area, the chest in the third, a spirit in each level of the fourth (5:{s5} 9:{s9} 12:{s12} 13:{s13} 14:{s14} boss:{s15} pillar:{s4})");
            // the stranger: heard out, he hands over the haft
            run.stageIndex = keep;
            var stranger = c.Spawn<Stranger>(Story.StrangerId, 9, 1);
            yield return c.Frames(3);
            stranger.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(hud.TalkUp && !run.hammerHandle, "the stranger sells nothing: he talks");
            for (int i = 0; i < 60 && hud.TalkUp; i++) { hud.AdvanceTalk(); yield return c.Frames(2); }
            yield return c.Frames(6);
            c.Expect(run.hammerHandle && hud.CardUp && !run.gear.Contains(Story.HammerGear), "heard out, he hands over the haft - one of three");
            // and goes: a moment's stillness, then he comes apart into motes of light, and nothing is left of him
            yield return c.Frames(Mathf.CeilToInt((Stranger.HoldTime + Stranger.FadeTime * 0.45f) / GameConst.Dt));
            var ssr = stranger.GetComponentInChildren<SpriteRenderer>();
            bool fading = !stranger.Dead && ssr != null && ssr.color.a < 0.95f;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "stranger_leaving.png"));
            for (int i = 0; i < 300 && !stranger.Dead; i++) yield return c.Frames(1);
            c.Expect(fading && stranger.Dead, "he fades into motes of light as he goes, and is gone");
            yield return PutAway();
            // the chest: the head
            var chest = c.Spawn<Chest>("gearchest", 12, 1, 0, Story.HeadChest);
            yield return c.Frames(3);
            chest.Interact(c.p);
            yield return c.Frames(4);
            c.Expect(run.hammerHead && chest.Opened && hud.CardUp && !run.gear.Contains(Story.HammerGear), "the chest holds the head - two of three");
            yield return PutAway();
            // three spirits, one in each level of the fourth area: the third freed, they bring the heart
            for (int lvl = 0; lvl < 3; lvl++)
            {
                run.stageIndex = 12 + lvl;
                var ghost = c.Spawn<Ghost>(Story.GhostId, 14, 1);
                yield return c.Frames(3);
                ghost.Interact(c.p);
                yield return c.Frames(6);
                c.Expect(ghost.Freed && (run.ghostMask & (1 << lvl)) != 0 && !Story.GhostIn(run), $"spirit {lvl + 1} is freed, and is not bound there again");
            }
            run.stageIndex = keep;
            for (int i = 0; i < 400 && !run.hammerCore; i++) yield return c.Frames(2);
            yield return c.Frames(6);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "story_hammer.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            c.Expect(run.hammerCore && run.gear.Contains(Story.HammerGear) && c.dir.config.FindGear(Story.HammerGear) != null,
                $"the three together hand over the heart - and haft, head and heart make the hammer (core {run.hammerCore}, gear {string.Join(",", run.gear)})");
            for (int i = 0; i < 10; i++) yield return PutAway();
            // the hammer in hand: one blow, three damage
            var brute = c.Spawn<Brute>("brute", 5, 1);
            yield return c.Frames(3);
            c.Teleport(3.6f, 1f);
            yield return c.Frames(3);
            c.p.facing = 1;
            if (brute != null) brute.health = 5;        // (more than one blow's worth, so that the blow can be measured)
            int before = brute != null ? brute.health : 0;
            var hammer = GearRegistry.Get(Story.HammerGear);
            if (hammer != null) hammer.Use(c.p, default);
            yield return c.Frames(3);
            bool swung = GameObject.Find("HammerSwing") != null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "story_swing.png"));
            yield return c.Frames(30);
            c.Expect(swung && GameObject.Find("HammerSwing") == null, "the hammer is seen in the hand for the length of the blow, and put away after it");
            c.Expect(hammer != null && brute != null && (brute.Dead || brute.health == before - HammerGear.Damage), $"a blow of the hammer does three damage ({before} -> {(brute != null ? brute.health : -1)})");
            if (brute != null && !brute.Dead) brute.Despawn();
            if (pillar != null) pillar.Despawn();
            if (chest != null) chest.Despawn();
            run.gear.Remove(Story.HammerGear);
            run.hammerHandle = run.hammerHead = run.hammerCore = false; run.ghostMask = 0;
            QuestLog.ResetForTests();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            yield return PutAway();
            yield return c.Frames(30);
        }

        static IEnumerator PromptPlacesCase(Ctx c)
        {
            var hud = c.dir.hud;
            c.Bot(Seq((2000, F())));
            // the line a thing hands over is taken apart
            HUD.SplitPrompt("[enter]  open the chest", false, out var act, out var sub, out var who, out var said, out var note);
            c.Expect(act == "[enter]  open the chest" && sub == null && who == null && said == null && note == null, "a thing to do is an action only");
            HUD.SplitPrompt("Elder Aldous:  \"Everyone is home.\"", false, out act, out sub, out who, out said, out note);
            c.Expect(act == null && who == "Elder Aldous" && said == "Everyone is home.", $"somebody speaking: a name and their words ({who} / {said})");
            HUD.SplitPrompt("[enter]  free Tam the baker      Tam:  \"Get me out!\"", false, out act, out sub, out who, out said, out note);
            c.Expect(act == "[enter]  free Tam the baker" && who == "Tam" && said == "Get me out!", $"a caged villager: what to press, and what they say ({act} / {who} / {said})");
            HUD.SplitPrompt("[enter]  buy rope for $20  -  A rope to climb.", false, out act, out sub, out who, out said, out note);
            c.Expect(act == "[enter]  buy rope for $20" && sub == "A rope to climb.", $"a ware: its price, and under it what it does ({act} / {sub})");
            HUD.SplitPrompt("The gate is sealed.", false, out act, out sub, out who, out said, out note);
            c.Expect(act == null && who == null && said == "The gate is sealed.", "told by nobody: words without a name");
            // on the screen
            var chest = c.Spawn<Entity>("chest", 8, 1);
            c.Teleport(8.5f, 1f);
            yield return c.Frames(30);
            c.Expect(hud.tagRoot != null && hud.tagRoot.gameObject.activeSelf && !hud.talkRoot.gameObject.activeSelf && !hud.noteRoot.gameObject.activeSelf, "at a chest: only the tag at the right edge");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_prompt_tag.png"));
            yield return c.Frames(3);
            var sign = c.Spawn<Sign>("sign", 14, 1, 0, "Hold [walk] near a ledge to walk slowly, and press [jump] to jump. A sign can say a good deal more than fits on one line, so it wraps.");
            c.Teleport(14.5f, 1f);
            yield return c.Frames(30);
            c.Expect(hud.noteRoot.gameObject.activeSelf && !hud.tagRoot.gameObject.activeSelf && !hud.talkRoot.gameObject.activeSelf, "at a sign: only the card");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_prompt_note.png"));
            yield return c.Frames(3);
            // the button puts a sign's card away; it is back when the player comes back to the sign
            c.Bot(Seq((2, F()), (1, F(act: true)), (60, F())));
            yield return c.Frames(36);
            c.Expect(!hud.noteRoot.gameObject.activeSelf && !hud.SignUp, "the button puts the sign's card away");
            c.Teleport(4.5f, 1f);
            yield return c.Frames(36);
            c.Teleport(14.5f, 1f);
            yield return c.Frames(36);
            c.Expect(hud.noteRoot.gameObject.activeSelf && hud.SignUp, "back at the sign, the card is there again");
            c.Bot(Seq((3000, F())));
            // somebody's words are written out a sentence at a time; the button finishes a sentence, brings the next, ends it
            hud.ShowTalk("Tam the Ropemaker", "A rope is a promise. Mind the frayed ones! Come back when you need more.");
            yield return c.Frames(30);      // (the sign's card fades out under it)
            c.Expect(hud.TalkUp && hud.TalkPages == 3 && hud.TalkPage == 0 && hud.TalkTyping && hud.talkRoot.gameObject.activeSelf && !hud.noteRoot.gameObject.activeSelf,
                $"spoken to: the first of three sentences is being written out ({hud.TalkPages} sentences)");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_talk_typing.png"));
            yield return c.Frames(3);
            hud.AdvanceTalk();
            yield return c.Frames(3);
            c.Expect(hud.TalkPage == 0 && !hud.TalkTyping && hud.talkMore.activeSelf, "the button while it is written: the whole sentence at once");
            hud.AdvanceTalk();
            yield return c.Frames(3);
            c.Expect(hud.TalkPage == 1 && hud.TalkTyping, "the button on a finished sentence: the next one");
            for (int i = 0; i < 400 && hud.TalkTyping; i++) yield return new WaitForFixedUpdate();
            c.Expect(!hud.TalkTyping && hud.TalkUp, "left alone, a sentence writes itself out and waits");
            hud.AdvanceTalk();
            yield return c.Frames(2);
            hud.AdvanceTalk(); hud.AdvanceTalk();
            yield return c.Frames(36);
            c.Expect(!hud.TalkUp && !hud.talkRoot.gameObject.activeSelf, "after the last sentence the button closes the box");
            c.Teleport(4.5f, 1f);
            yield return c.Frames(36);
            c.Expect(!hud.noteRoot.gameObject.activeSelf && !hud.tagRoot.gameObject.activeSelf && !hud.talkRoot.gameObject.activeSelf, "away from everything: nothing shown");
            if (chest != null) chest.Despawn();
            if (sign != null) sign.Despawn();
            // an item picked up: a card the first time, a line at the left edge after that - one line each, two of a kind included;
            // gold is no item: it only ever gets a line
            ItemLog.Forget("bombs"); ItemLog.Forget("gold_small");
            c.Spawn<Pickup>("bombbag", 6, 1);
            yield return c.Frames(20);
            c.Teleport(6.5f, 1f);
            for (int i = 0; i < 1500 && !hud.cardRoot.gameObject.activeSelf; i++) yield return new WaitForFixedUpdate();      // (it waits for the level's title to go)
            yield return c.Frames(6);
            c.Expect(hud.CardUp && hud.CardName == "Bomb Bag" && hud.FeedCount == 0 && hud.cardRoot.gameObject.activeSelf, $"the first bomb bag of a save is shown on a card ({hud.CardName})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_item_card.png"));
            yield return c.Frames(3);
            int gold0 = c.run.gold;
            c.Teleport(3.5f, 1f);
            ItemLog.FirstTime("ropes");       // (seen before: it goes to the list)
            c.run.ropes = 0;                  // (room for five: of the two piles of three, three are taken and then two)
            c.Spawn<Pickup>("ropepile", 6, 1); c.Spawn<Pickup>("ropepile", 6, 1);
            c.Spawn<Pickup>("gold_small", 7, 1); c.Spawn<Pickup>("gold_big", 7, 1);
            yield return c.Frames(20);
            c.Teleport(6.5f, 1f);
            yield return c.Frames(14);
            c.Teleport(7.5f, 1f);
            yield return c.Frames(14);
            c.Expect(c.run.gold == gold0 + 40 && hud.FeedCount == 4 && hud.FeedLines("ropes") == 2 && hud.FeedAmount("ropes") == 5 && hud.FeedAmount("gold_small") == 10 && hud.FeedAmount("gold_big") == 30 && hud.CardName == "Bomb Bag",
                $"picked up again an item is a line at the left edge, each with a line of its own - two rope piles two lines, a nugget +10, a chunk +30 - and gold never a card (lines {hud.FeedCount}, rope lines {hud.FeedLines("ropes")}, nugget +{hud.FeedAmount("gold_small")}, chunk +{hud.FeedAmount("gold_big")})");
            // one id for a thing, however it is come by: the bomb bag found on the ground has had its card, so the
            // Bomb Bag a shop sells afterwards (the same id, "bombs") is only a line - and a save from before the ids
            // were one, which knows the glider as "gear:glider", is still believed
            var bagPrefab = c.world.config.prefabs.Get("bombbag") as Pickup;
            ShopOffer bagOffer = null;
            foreach (var o in c.world.config.shop.offers) if (o.resource == Res.Bombs) bagOffer = o;
            c.Expect(bagPrefab != null && bagOffer != null && bagPrefab.ItemId == bagOffer.id, $"the bomb bag on the ground and the one on the shelf have one id ({(bagPrefab != null ? bagPrefab.ItemId : "?")} / {(bagOffer != null ? bagOffer.id : "?")})");
            if (bagOffer != null) hud.Gain(bagOffer.id, bagOffer.displayName, bagOffer.icon, 3, bagOffer.description);
            ItemLog.Forget("glider"); ItemLog.Forget("ropes");
            ItemLog.FirstTime("gear:glider"); ItemLog.FirstTime("rope pile");
            bool oldSave = ItemLog.Seen("glider") && ItemLog.Seen("ropes");
            ItemLog.Forget("glider");
            c.Expect(bagOffer != null && hud.FeedLines(bagOffer.id) == 1 && hud.CardName == "Bomb Bag" && oldSave,
                $"a bomb bag bought after one was found is a line, not a second card; what an old save wrote down still counts (lines {(bagOffer != null ? hud.FeedLines(bagOffer.id) : -1)}, old save {oldSave})");
            yield return new WaitForSecondsRealtime(0.5f);       // (the lines have slid in)
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "hud_item_feed.png"));
            yield return c.Frames(3);
            // what a save remembers survives leaving the game: written to its file and read back
            {
                string keep = SaveSystem.DirOverride;
                string scratch = System.IO.Path.Combine(OutDir, "save_roundtrip");
                System.IO.Directory.CreateDirectory(scratch);
                foreach (var f in System.IO.Directory.GetFiles(scratch)) System.IO.File.Delete(f);
                SaveSystem.DirOverride = scratch;
                SaveSystem.Reload();
                var rec = SaveSystem.Records;
                rec.seenItems.Add("bomb bag"); rec.seenItems.Add("ware:compass");
                rec.killCounts.Add(new KillCount { name = "snake", count = 3 });
                rec.quests.Add(new QuestState { id = "elder_word", step = 1, done = false });
                rec.tutorialDone = true;
                SaveSystem.SaveRecords();
                SaveSystem.Reload();
                rec = SaveSystem.Records;
                c.Expect(rec.seenItems.Count == 2 && rec.seenItems.Contains("bomb bag") && rec.killCounts.Count == 1 && rec.killCounts[0].count == 3
                         && rec.quests.Count == 1 && rec.quests[0].step == 1 && rec.tutorialDone,
                    $"the things picked up, the kills, the quests and the tutorial are all in the save file after a restart (seen {rec.seenItems.Count}, kills {rec.killCounts.Count}, quests {rec.quests.Count}, tutorial {rec.tutorialDone})");
                SaveSystem.DirOverride = keep;
                SaveSystem.Reload();
            }
            // the chest by the gate of an area that asks for gear: opened, it hands that gear over
            c.run.gear.Remove("glider"); ItemLog.Forget("glider");
            var gc = Chest.SpawnGearChest(c.world, "glider", new Vector2(8.5f, 1f));
            yield return c.Frames(3);
            c.Expect(gc != null && gc.gearId == "glider" && Mathf.Abs(gc.pos.x - 8.5f) <= 4f && !c.run.gear.Contains("glider"), "a gear chest stands beside the gate, and the gear is not yet the player's");
            if (gc != null)
            {
                c.Teleport(gc.pos.x, 1f);
                c.Bot(Seq((2, F()), (1, F(act: true)), (60, F())));
                yield return c.Frames(20);
                c.Expect(gc.Opened && c.run.gear.Contains("glider") && hud.CardUp, "opened with the button, it hands the glider over, on a card the first time");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "gear_chest.png"));
                yield return c.Frames(3);
                gc.Despawn();
            }
            c.Bot(Seq((3000, F())));
        }

        static IEnumerator EclipseKingCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((40000, F())));
            c.Teleport(4.5f, 1f);
            var b = c.Spawn<EclipseKing>("eclipseking", 16, 1, 0, null, -1);
            c.Expect(b != null && c.dir.Boss == b && b.Form == 1 && b.maxHealth == 35 && !b.IsStompable, "the eclipse king spawned as the boss: a shell of 35 health, not to be stomped");
            if (b == null) yield break;
            yield return c.Frames(5);
            IEnumerator Until(System.Func<bool> done, int frames) { for (int i = 0; i < frames && !done(); i++) yield return new WaitForFixedUpdate(); }
            IEnumerator Settle() { yield return Until(() => b.Doing == EclipseKing.Act.Idle, 1800); }

            // ---- the shell
            // the swipe: a standing player in front of it is struck for sweepDamage
            c.Teleport(b.pos.x - 3f, 1f);
            yield return c.Frames(6);
            c.p.godMode = false; c.run.health = c.run.maxHealth;
            int h = c.run.health;
            b.ForceMove(EclipseKing.MoveSweep);
            yield return Until(() => b.Sweeps >= 1, 600);
            yield return c.Frames(2);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_sweep.png"));       // (fire out of its chest, down to the floor and along it)
            yield return c.Frames(18);
            c.Expect(b.Sweeps == 1 && c.run.health == h - b.sweepDamage, $"its swipe strikes a player who stands in front of it (swipes {b.Sweeps}, health {h} -> {c.run.health})");
            c.p.godMode = true;
            yield return Settle();

            // the charge: to the wall, where its horns stick and its chest is open - blows count double there, and only there
            b.ForceMove(EclipseKing.MoveCharge);
            yield return Until(() => b.Doing == EclipseKing.Act.ChargeTell, 600);
            yield return c.Frames(14);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_ignite.png"));      // (catching fire)
            yield return Until(() => b.Doing == EclipseKing.Act.Charge, 600);
            yield return c.Frames(8);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_charge.png"));      // (a ball of fire)
            yield return Until(() => b.Stucks >= 1, 1200);
            c.Expect(b.Stucks == 1 && b.Exposed && b.Doing == EclipseKing.Act.Stuck, $"its charge ends with the horns in the wall and its chest open ({b.Doing})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_stuck.png"));
            int bh = b.health;
            b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(bh - b.health == 2, $"a blow on the open chest counts double ({bh - b.health})");
            yield return Settle();
            yield return c.Frames(20);
            bh = b.health;
            b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(bh - b.health == 1, $"a blow on its stone counts once ({bh - b.health})");

            // the leap: down with a wall of light along the floor
            yield return Settle();
            b.ForceMove(EclipseKing.MoveLeap);
            yield return Until(() => b.Leaps >= 1, 900);
            yield return c.Frames(6);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_wave.png"));        // (the two walls of light, out of its chest, down to the floor)
            yield return c.Frames(2);
            BossShot wave = null;
            foreach (var e in c.world.Entities) if (!e.Dead && e is BossShot ws) wave = ws;
            c.Expect(b.Leaps == 1 && wave != null && wave.size.y >= 3.5f && wave.unbreakable, $"its leap comes down with a wall of light along the floor: too tall to jump, and the blade does nothing to it (height {(wave != null ? wave.size.y : 0f)})");
            yield return Settle();
            // half broken, the cracks stand wide
            b.health = b.maxHealth / 2;
            c.Expect(b.Cracked, "at half its health the cracks stand wide");

            // ---- the shell breaks: it does not die - the light inside rises, with its health full
            yield return c.Frames(20);
            b.health = 1;
            b.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(!b.Dead && c.dir.BossAlive && b.Doing == EclipseKing.Act.Shatter, $"its health gone, the shell does not die: it comes apart ({b.Doing})");
            bool touched = b.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(!touched, "nothing touches it while it comes apart");
            yield return Until(() => b.Form == 2, 600);
            c.Expect(b.Form == 2 && b.health == b.coreHealth && b.maxHealth == b.coreHealth && b.Darkened && !b.Dead, $"the core rises: health full again ({b.health} / {b.maxHealth}), the hall dark");
            c.Expect(!c.dir.Ended, "and the fight goes on");
            yield return Settle();
            c.Expect(b.pos.y > 1.5f && b.ContactDamage == 0, $"it hangs in the air, and touching it does not hurt (y {b.pos.y:0.0})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_core.png"));

            // ---- the core
            // the column of light: a wooden ledge does not stop it, nor a hole in the floor
            c.Teleport(8.5f, 1f);       // under the ledge
            b.ForceMove(EclipseKing.MoveBeam);
            yield return Until(() => b.Doing == EclipseKing.Act.Cast, 900);
            yield return c.Frames(3);
            // four stakes of light stand in the hall: breaking one costs it stakeBreakDamage
            {
                var stakes = new List<SunStake>();
                foreach (var e in c.world.Entities) if (!e.Dead && e is SunStake st) stakes.Add(st);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_stakes.png"));
                int before = b.health;
                foreach (var st in stakes) st.OnWhipped(c.p, 1, 1);
                c.Expect(stakes.Count == b.stakes && b.StakesBroken == b.stakes && before - b.health == b.stakes * b.stakeBreakDamage,
                    $"it plants {b.stakes} stakes of light, and each one broken wounds it for {b.stakeBreakDamage} ({stakes.Count} stakes, {b.StakesBroken} broken, health {before} -> {b.health})");
            }
            yield return Until(() => b.BeamWalking, 900);
            c.Expect(Mathf.Abs(b.BeamBottom(8.5f) - 1f) < 0.01f && Mathf.Abs(b.BeamBottom(13.5f) - 1f) < 0.01f, $"the light goes through a wooden ledge down to the floor, as it does elsewhere ({b.BeamBottom(8.5f)}, {b.BeamBottom(13.5f)})");
            {
                var was = c.world.Grid.Get(20, 0);
                c.world.Grid.SetRaw(20, 0, TileType.Empty);
                float pit = b.BeamBottom(20.5f);
                c.world.Grid.SetRaw(20, 0, was);
                c.Expect(pit < 0.5f, $"the light reaches the bottom of a hole in the floor (stops at {pit})");
            }
            c.p.godMode = false; c.run.health = c.run.maxHealth; h = c.run.health;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_sun.png"));
            yield return Until(() => !b.BeamWalking, 900);
            c.Expect(b.Beams == 1 && c.run.health == h - b.beamDamage, $"a ledge is no shelter: under it the light burns the player all the same (health {h} -> {c.run.health})");
            c.p.godMode = true;
            bool embers = false;
            for (int i = 0; i < 900 && b.Doing == EclipseKing.Act.Cast; i++) { yield return new WaitForFixedUpdate(); if (c.Count<BossShot>() > 0) embers = true; }
            c.Expect(embers, "then embers rain where the player stands");
            yield return Until(() => b.Doing == EclipseKing.Act.Open, 600);
            c.Expect(b.Exposed && b.pos.y < 1.5f, $"after the move it sinks to the floor, chest unguarded ({b.Doing}, y {b.pos.y:0.0})");
            bh = b.health;
            b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(bh - b.health == 2, $"a blow counts double then ({bh - b.health})");
            yield return Settle();
            // in the open the same light burns
            c.Teleport(14.5f, 1f);
            b.ForceMove(EclipseKing.MoveBeam);
            yield return Until(() => b.BeamWalking, 1500);
            c.p.godMode = false; c.run.health = c.run.maxHealth; h = c.run.health;
            yield return Until(() => b.StakesBurst >= 1 || !b.BeamWalking, 900);
            yield return c.Frames(4);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_blast.png"));       // (a stake bursting: a ring of light)
            yield return Until(() => !b.BeamWalking, 900);
            c.Expect(c.run.health <= h - b.beamDamage, $"in the open the light burns for beamDamage (health {h} -> {c.run.health})");
            c.Expect(b.StakesBurst == b.stakes && c.Count<SunStake>() == 0, $"and every stake left standing bursts as the light reaches it ({b.StakesBurst} burst, {c.Count<SunStake>()} left)");
            c.p.godMode = true;
            yield return Until(() => b.Doing == EclipseKing.Act.Open, 1500);
            yield return Settle();

            // a hand comes down on a player who stays where they are
            c.Teleport(12.5f, 1f);
            yield return c.Frames(4);
            c.p.godMode = false; c.run.health = c.run.maxHealth; h = c.run.health;
            b.ForceMove(EclipseKing.MoveSlam);
            yield return Until(() => b.Slams >= 1, 900);
            yield return c.Frames(3);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_slam.png"));        // (fire out of the floor where the hand struck)
            c.Expect(b.Slams == 1 && c.run.health == h - b.slamDamage, $"a hand comes down on a player who stays under it (slams {b.Slams}, health {h} -> {c.run.health})");
            c.p.godMode = true;
            yield return Until(() => b.Doing == EclipseKing.Act.Open, 600);
            yield return Settle();

            // both hands sweep the floor from the walls until they meet
            b.ForceMove(EclipseKing.MoveClap);
            yield return Until(() => b.Doing == EclipseKing.Act.Clap, 900);
            c.Expect(b.Doing == EclipseKing.Act.Clap && b.HandAt(0).x < 3.5f && b.HandAt(1).x > 21f && b.HandAt(0).y < 1.1f, $"its hands go out to the walls, down on the floor ({b.HandAt(0).x:0.0}, {b.HandAt(1).x:0.0})");
            yield return Until(() => b.Claps >= 1, 900);
            c.Expect(b.Claps == 1 && Mathf.Abs(b.HandAt(0).x - b.pos.x) < 1.5f && Mathf.Abs(b.HandAt(1).x - b.pos.x) < 1.5f, "and come together under it");
            yield return c.Frames(2);
            int ring = c.Count<BossShot>();
            yield return c.Frames(10);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_clap.png"));        // (a ring of embers)
            c.Expect(ring >= b.clapShots / 2, $"where they meet a ring of embers flies out all round ({ring} in the air of {b.clapShots})");
            yield return Until(() => b.Doing == EclipseKing.Act.Open, 600);
            yield return Settle();

            // it vanishes, and comes back over the player with a hand raised
            int slams = b.Slams;
            b.ForceMove(EclipseKing.MoveBlink);
            yield return Until(() => b.Doing == EclipseKing.Act.BlinkOut, 900);
            bh = b.health;
            bool hit = b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(b.Blinks == 1 && !hit && b.health == bh && Mathf.Abs(b.pos.x - c.p.pos.x) < 4f, $"it vanishes: nothing touches it, and it will be over the player ({Mathf.Abs(b.pos.x - c.p.pos.x):0.0} away)");
            yield return Until(() => b.Slams > slams, 900);
            c.Expect(b.Slams == slams + 1, "it comes back with a hand already coming down");
            yield return Until(() => b.Doing == EclipseKing.Act.Open, 600);
            yield return Settle();

            // the burst: the wall of light again, embers into the air - and it is on the floor
            b.ForceMove(EclipseKing.MoveNova);
            yield return Until(() => b.Novas >= 1, 1500);
            yield return c.Frames(2);
            c.Expect(b.Novas == 1 && b.Exposed && c.Count<BossShot>() >= 3, $"the stone bursts along the floor and into the air, and leaves it open (shots {c.Count<BossShot>()})");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_eclipseking_open.png"));

            // ---- its end: the Sunstone drops where it hung and can be lifted - and nothing comes down
            for (int i = 0; i < 80 && !b.Dead; i++)
            {
                b.TakeDamage(new DamageInfo(99, DamageType.Whip, c.p, Vector2.zero, "test"));
                yield return c.Frames(14);
            }
            yield return c.Frames(10);
            Treasure stone = null;
            foreach (var e in c.world.Entities) if (!e.Dead && e is Treasure t && t.isHeartstone) stone = t;
            c.Expect(b.Dead && !c.dir.BossAlive && stone != null && stone.CanPickUp, $"the core is put out and the Sunstone drops, free to lift (dead {b.Dead}, stone {stone != null})");
            if (stone != null)
            {
                yield return c.Frames(90);
                c.Teleport(stone.pos.x, 1f);
                c.Bot(Seq((3, F()), (1, F(down: true, atk: true)), (3000, F())));
                yield return c.Frames(16);
                c.Expect(c.p.held == stone && !c.dir.Collapsing, $"the stone is lifted and nothing comes down: the hall stands (held {c.p.held == stone}, collapsing {c.dir.Collapsing})");
            }
        }

        static IEnumerator GearWyrmCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((9000, F())));
            c.Teleport(6.5f, 1f);
            var b = c.Spawn<GearWyrm>("gearwyrm", 14, 1, 0, null, -1);
            c.Expect(b != null && c.dir.Boss == b && b.maxHealth == 36 && b.SegmentCount == 6, "the cogwyrm spawned as the boss: 36 health, six segments");
            if (b == null) yield break;
            for (int i = 0; i < 400 && !b.Patrolling; i++) yield return new WaitForFixedUpdate();
            c.Expect(b.Patrolling && b.HeadCentre.y > b.FloorLevel + 3f, $"it climbs to its track, out of reach (head {b.HeadCentre.y - b.FloorLevel:0.0} above the floor)");
            // the dive: head first into the floor, and stuck there - the opening
            b.ForceMove(1);
            for (int i = 0; i < 600 && !b.Stuck; i++) yield return new WaitForFixedUpdate();
            int hp = b.health;
            bool hurt = b.Stuck && b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test"));
            c.Expect(b.Stuck && hurt && b.health == hp - 1 && b.HeadCentre.y < b.FloorLevel + 1f, "it dives at the player and its head sticks in the floor, where a blade hurts it");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_stuck.png"));
            for (int i = 0; i < 600 && !b.Patrolling; i++) yield return new WaitForFixedUpdate();
            // the floor run
            b.ForceMove(0);
            bool low = false;
            for (int i = 0; i < 900 && !(low && b.Patrolling); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Running && b.HeadCentre.y < b.FloorLevel + 1f) { if (!low) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_run.png")); low = true; }
            }
            c.Expect(low && b.Patrolling, "it runs down the wall, along the floor and up the other side");
            // bolts
            b.ForceMove(2);
            bool bolts = false;
            for (int i = 0; i < 900 && !(bolts && b.Patrolling); i++) { yield return new WaitForFixedUpdate(); if (c.Count<BossShot>() >= 3) bolts = true; }
            c.Expect(bolts, "it lowers its head and spits a fan of bolts");
            // ... and then lowers it further to load again: a jump reaches it, a blow counts
            b.ForceMove(2);
            bool reload = false, reloadHurt = false; float reloadAt = 99f;
            for (int i = 0; i < 900 && !(reload && b.Patrolling); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Reloading && !reload)
                {
                    reload = true; reloadAt = b.HeadCentre.y - b.FloorLevel;
                    int h = b.health;
                    reloadHurt = b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")) && b.health == h - 1;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_reload.png"));
                }
            }
            c.Expect(reload && reloadHurt && reloadAt < 2.6f && b.Patrolling, $"after its fans its head hangs low to load again, within reach of a jump ({reloadAt:0.0} above the floor), and a blow hurts it");
            // the wheel: nothing gets through
            b.ForceMove(3);
            bool rolled = false, proof = false;
            for (int i = 0; i < 1500 && !(rolled && b.Patrolling); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Rolling && !rolled)
                {
                    rolled = true;
                    int h = b.health;
                    proof = !b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")) && b.health == h;
                }
                if (b.Rolling && i % 20 == 0) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_wheel.png"));
            }
            c.Expect(rolled && proof && b.Patrolling, "it curls up into a wheel that nothing hurts, rolls there and back, and climbs again");
            // the wheel's last bounce throws it against the wall: dazed, flat on the floor, its head out in the hall
            b.ForceMove(3);
            bool dazed = false, dazedHurt = false, flat = false, headOut = false;
            for (int i = 0; i < 1500 && !(dazed && b.Patrolling); i++)
            {
                yield return new WaitForFixedUpdate();
                if (b.Dazed && !dazed)
                {
                    dazed = true;
                    for (int k = 0; k < 24; k++) yield return new WaitForFixedUpdate();      // (it has fallen apart by now)
                    int h = b.health;
                    dazedHurt = b.Dazed && b.TakeDamage(new DamageInfo(1, DamageType.Whip, c.p, Vector2.zero, "test")) && b.health == h - 1;
                    flat = true; headOut = true;
                    float wallSide = b.SegmentAt(b.SegmentCount - 1).x - b.HeadCentre.x;      // the tail lies towards the wall
                    for (int k = 0; k < b.SegmentCount; k++)
                    {
                        Vector2 sg = b.SegmentAt(k);
                        if (sg.y > b.FloorLevel + 1f) flat = false;
                        if ((sg.x - b.HeadCentre.x) * wallSide <= 0f) headOut = false;
                    }
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_dazed.png"));
                }
            }
            c.Expect(dazed && dazedHurt && flat && headOut && b.Patrolling, $"the wheel's last bounce throws it against the wall: it lies dazed along the floor, head out in the hall, and a blow hurts it (hurt {dazedHurt}, flat {flat}, head out {headOut})");
            // a segment is a saw
            yield return c.Frames(50);         // (it may just have brushed the player, who cannot be hurt in this test)
            c.p.godMode = false;
            c.run.health = 8;
            Vector2 seg = b.SegmentAt(2);
            c.Teleport(seg.x, seg.y - 0.4f);
            yield return c.Frames(4);
            c.Expect(c.run.health < 8, $"touching a segment hurts (health 8 -> {c.run.health})");
            c.p.godMode = true;
            c.Teleport(6.5f, 1f);
            // a quarter of its health gone while the player is at its stuck head: nothing comes off yet
            b.ForceMove(1);
            for (int i = 0; i < 600 && !b.Stuck; i++) yield return new WaitForFixedUpdate();
            b.health = Mathf.FloorToInt(b.maxHealth * 0.75f);
            yield return c.Frames(20);
            SawBlade Loose() { foreach (var e in c.world.Entities) if (!e.Dead && e is SawBlade sb && sb.Loose) return sb; return null; }
            c.Expect(b.Shed == 0 && !b.ShedWarning && Loose() == null, "a quarter of its health gone while its head is stuck: no segment comes off beside the player");
            // back on its track the segment glows first, then falls as itself, lies a moment and rolls at the player
            for (int i = 0; i < 900 && !b.ShedWarning; i++) yield return new WaitForFixedUpdate();
            Vector2 glow = b.SegmentAt(b.SegmentCount - 2);
            c.Expect(b.ShedWarning && b.Patrolling && b.Shed == 0 && glow.y > b.FloorLevel + 2.5f && Mathf.Abs(glow.x - c.p.pos.x) >= 2.4f,
                $"back under the ceiling, and not over the player's head, the segment glows and sparks before it comes off ({glow.y - b.FloorLevel:0.0} up, {Mathf.Abs(glow.x - c.p.pos.x):0.0} to the side)");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_shed_tell.png"));
            int warned = 0;
            for (int i = 0; i < 300 && b.Shed == 0; i++) { yield return new WaitForFixedUpdate(); warned++; }
            var piece = Loose();
            float fellFrom = piece != null ? piece.pos.y - b.FloorLevel : 0f;
            c.Expect(b.Shed == 1 && b.SegmentCount == 5 && warned >= 25 && piece != null && !piece.Rolling && fellFrom > 2f && piece.Picture == b.segmentSprite,
                $"after {warned} frames of warning the segment comes off where it was ({fellFrom:0.0} above the floor), wearing the segment's own picture ({b.SegmentCount} left)");
            yield return c.Frames(6);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_cogwyrm_shed_fall.png"));
            int landedAt = -1, rolledAt = -1; float landX = 0f;
            for (int i = 0; i < 400 && piece != null && !piece.Dead && rolledAt < 0; i++)
            {
                yield return new WaitForFixedUpdate();
                if (landedAt < 0 && piece.grounded) { landedAt = i; landX = piece.pos.x; }
                if (piece.Rolling) rolledAt = i;
            }
            yield return c.Frames(10);
            bool toPlayer = piece != null && !piece.Dead && Mathf.Abs(piece.pos.x - c.p.pos.x) < Mathf.Abs(landX - c.p.pos.x);
            c.Expect(landedAt >= 0 && rolledAt - landedAt >= 20 && toPlayer, $"it falls to the floor, lies there {rolledAt - landedAt} frames, then rolls at the player");
            for (int i = 0; i < 400 && !b.Dead; i++) { b.TakeDamage(new DamageInfo(3, DamageType.Whip, c.p, Vector2.zero, "test")); yield return c.Frames(14); }
            c.Expect(b.Dead, "it can be killed");
            foreach (var e in c.world.Entities) if (!e.Dead && (e is SawBlade || e is BossShot)) e.Despawn();
            ClearFight(c);
        }

        static IEnumerator HawkCase(Ctx c)
        {
            c.Teleport(12.5f, 1f);
            c.Spawn<Hawk>("hawk", 12, 7);
            c.Bot(Seq((300, F())));
            bool hit = false;
            for (int i = 0; i < 300 && !hit; i++) { yield return new WaitForFixedUpdate(); if (c.run.health < 4) hit = true; }
            c.Expect(hit, "the hawk dived and hit the player");
        }

        static void RopeGateMap(char[,] m)
        {
            Set(m, 11, 1, 'S');
            for (int x = 1; x < W - 1; x++) Set(m, x, 7, 'X');
            for (int y = 1; y <= 4; y++) { Set(m, 12, y, 'X'); Set(m, 13, y, 'X'); }
        }

        static IEnumerator WheelCase(Ctx c)
        {
            var w = c.dir.gearWheel;
            c.Expect(w != null, "wheel exists");
            if (w == null) yield break;
            c.run.Equip("bomb");
            float scale = Time.timeScale;
            w.Open();
            yield return new WaitForSecondsRealtime(0.1f);
            int f0 = c.world.Frame;
            yield return new WaitForSecondsRealtime(0.4f);
            c.Expect(w.IsOpen && Time.timeScale == 0f && c.world.Frame == f0, "open + frozen");
            w.Step(1);
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "gear_wheel.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            w.Confirm();
            c.Expect(!w.IsOpen && Mathf.Approximately(Time.timeScale, scale), "closed + resumed");
            c.Expect(c.run.EquippedId == "rope", "rope equipped (got " + c.run.EquippedId + ")");
            // a quick tap goes on to the next item, and as many taps as there are items come back round to the start
            var seenByTap = new HashSet<string>();
            for (int i = 0; i < c.run.gear.Count; i++) { w.QuickSwap(); seenByTap.Add(c.run.EquippedId); }
            c.Expect(seenByTap.Count == c.run.gear.Count && c.run.EquippedId == "rope", $"quick taps go round every item ({seenByTap.Count} of {c.run.gear.Count}) and back to the rope");
            yield return c.Frames(5);
        }

        static IEnumerator Send(Gamepad pad, GamepadState s, float wait)
        {
            InputSystem.QueueStateEvent(pad, s);
            yield return new WaitForSecondsRealtime(wait);
        }

        // Real button presses through the Input System (virtual gamepad): hold R1 + tilt, release, then taps.
        static IEnumerator WheelInputCase(Ctx c)
        {
            var w = c.dir.gearWheel;
            // the test window may not have focus when run in the background: let the virtual pad through anyway
            var prevBg = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var pad = InputSystem.AddDevice<Gamepad>("ScenarioPad");
            if (!c.run.gear.Contains("glider")) c.run.gear.Add("glider");
            c.run.Equip("bomb");
            c.run.previousEquipped = -1;
            float scale0 = Time.timeScale;
            var log = new StringBuilder();
            var none = new GamepadState();
            var r1 = new GamepadState().WithButton(GamepadButton.RightShoulder);
            var r1Right = r1; r1Right.leftStick = new Vector2(1f, 0f);

            // hold R1, tilt right once (bomb -> rope), release stick, release R1
            yield return Send(pad, r1, 0.45f);
            c.Expect(w.IsOpen, "holding R1 opens the wheel");
            c.Expect(InputDeviceTracker.UsingGamepad, "prompts switch to gamepad button pictures after a pad press");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "gear_wheel_pad.png"));
            yield return Send(pad, r1Right, 0.12f);
            yield return Send(pad, r1, 0.12f);
            yield return Send(pad, none, 0.15f);
            log.Append("wheel->" + c.run.EquippedId + " ");
            c.Expect(!w.IsOpen && c.run.EquippedId == "rope", "release equips rope (got " + c.run.EquippedId + ")");

            // one quick tap right after: on to the item after the rope
            string next1 = c.run.gear[(c.run.equipped + 1) % c.run.gear.Count];
            yield return Send(pad, r1, 0.09f);
            yield return Send(pad, none, 0.15f);
            log.Append("tap->" + c.run.EquippedId + " ");
            c.Expect(c.run.EquippedId == next1 && next1 != "bomb", "ONE tap goes on to the next item, " + next1 + " (got " + c.run.EquippedId + ")");

            // a slightly long tap (0.3 s: wheel pops open, nothing chosen) still steps on
            string next2 = c.run.gear[(c.run.equipped + 1) % c.run.gear.Count];
            yield return Send(pad, r1, 0.3f);
            yield return Send(pad, none, 0.15f);
            log.Append("longtap->" + c.run.EquippedId + " ");
            c.Expect(!w.IsOpen && c.run.EquippedId == next2, "0.3 s tap still steps on to " + next2 + " (got " + c.run.EquippedId + ")");

            // holding without choosing for a long time just closes and keeps the item
            yield return Send(pad, r1, 0.7f);
            yield return Send(pad, none, 0.15f);
            c.Expect(!w.IsOpen && c.run.EquippedId == next2, "long hold without choosing keeps " + next2 + " (got " + c.run.EquippedId + ")");
            c.Expect(Mathf.Approximately(Time.timeScale, scale0), "game resumed at normal speed (" + Time.timeScale + ")");
            c.detail += log.ToString();
            InputSystem.RemoveDevice(pad);
            InputDeviceTracker.ResetToKeyboard();
            InputSystem.settings.backgroundBehavior = prevBg;
            yield return c.Frames(3);
        }

        static IEnumerator UseGearCase(Ctx c)
        {
            c.run.Equip("rope");
            c.Bot(Seq((2, F()), (1, F(use: true)), (30, F())));
            yield return c.Frames(33);
            c.Expect(c.run.ropes == 3 && c.run.bombs == 4, $"rope used only (ropes {c.run.ropes}, bombs {c.run.bombs})");
            c.run.Equip("bomb");
            c.Bot(Seq((1, F(x: 1)), (1, F(use: true)), (90, F(x: -1)), (120, F())));
            yield return c.Frames(212);
            c.Expect(c.run.bombs == 3 && c.run.ropes == 3, $"bomb used only (ropes {c.run.ropes}, bombs {c.run.bombs})");
            c.Expect(c.world.ExplosionsThisLevel == 1, "the bomb exploded");
        }

        static IEnumerator RopeGateCase(Ctx c, bool withRope)
        {
            c.run.Equip("rope");
            var seq = withRope
                ? Seq((2, F()), (1, F(use: true)), (30, F()), (90, F(up: true)), (1, F(x: 1, jp: true, jh: true)), (30, F(x: 1, jh: true)), (60, F(x: 1)), (30, F()))
                : Seq((3, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (40, F(x: 1, jh: true)), (10, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (40, F(x: 1, jh: true)), (5, F(up: true)), (40, F(up: true)), (40, F()));
            c.Bot(seq);
            yield return c.Frames(seq.Count + 5);
            if (withRope) c.Expect(c.p.pos.x > 14f && Mathf.Abs(c.p.pos.y - 1f) < 0.01f, $"crossed the cliff (pos {c.p.pos})");
            else c.Expect(c.p.pos.x < 12f, $"stopped by the cliff (pos {c.p.pos})");
        }

        static IEnumerator BombGateCase(Ctx c)
        {
            c.run.Equip("bomb");
            var seq = Seq((40, F(x: 1)), (1, F(down: true, use: true)), (70, F(x: -1)), (160, F()), (150, F(x: 1)), (20, F()));
            c.Bot(seq);
            yield return c.Frames(seq.Count + 5);
            var g = c.world.Grid;
            c.Expect(g.Get(12, 1) == TileType.Empty && g.Get(12, 2) == TileType.Empty, "rubble cleared");
            c.Expect(g.Get(12, 5) == TileType.Bedrock, "bedrock intact");
            c.Expect(c.p.pos.x > 14f, $"walked through (pos {c.p.pos})");
            c.Expect(!c.p.IsDead && c.run.bombs == 3, "safe, one bomb used");
        }

        static IEnumerator SlingshotCase(Ctx c)
        {
            c.p.godMode = true;
            var run = c.run;
            var sling = GearRegistry.Get("slingshot") as SlingshotGear;
            // the use button down for a few steps (the band is drawn), then let go (the pebble flies)
            IEnumerator Shoot(bool up = false, int holdFrames = 6)
            {
                sling.Use(c.p, new FrameInput { usePressed = true, useHeld = true, up = up });
                for (int i = 0; i < holdFrames; i++) { sling.Hold(c.p, new FrameInput { useHeld = true, up = up }); yield return new WaitForFixedUpdate(); }
                sling.Hold(c.p, new FrameInput { useHeld = false, up = up });
            }
            var def = c.world.config.FindGear("slingshot");
            c.Expect(run.gear.Contains("slingshot") && sling != null && def != null && def.icon != null && run.Pebbles == 0 && run.PebbleCap == 10,
                $"every run starts with the slingshot, no pebbles, and room for ten (gear {run.gear.Contains("slingshot")}, pebbles {run.Pebbles}, room {run.PebbleCap})");
            var kit = c.dir.config.player.startingGear;
            c.Expect(kit != null && kit.Length > 0 && kit[0] == "slingshot" && System.Array.IndexOf(kit, "bomb") > 0 && run.EquippedId == "bomb",
                $"it is the first of the starting gear, so a run begins with it in hand and the bombs behind it - but the tutorial, which teaches the bombs, begins with those (first {(kit != null && kit.Length > 0 ? kit[0] : "?")}, in hand here {run.EquippedId})");
            c.Teleport(3.5f, 1f);
            c.p.facing = 1;
            yield return Shoot();
            yield return c.Frames(3);
            c.Expect(c.Count<PebbleShot>() == 0 && run.Pebbles == 0 && !sling.Drawn, "without pebbles the band is not even drawn, and nothing is shot");

            // pots and crates can give pebbles up
            bool inPot = false, inCrate = false;
            var pot = c.world.config.prefabs.Get("pot") as Breakable; var crate = c.world.config.prefabs.Get("crate") as Breakable;
            if (pot != null) foreach (var w in pot.contents) if (w.id == "pebbles" && w.weight > 0f) inPot = true;
            if (crate != null) foreach (var w in crate.contents) if (w.id == "pebbles" && w.weight > 0f) inCrate = true;
            c.Expect(inPot && inCrate, $"pebbles are among what a pot and a crate may hold (pot {inPot}, crate {inCrate})");

            // three to a pile; four piles are twelve, and only ten can be carried: the rest stays where it lies
            ItemLog.FirstTime("pebbles");        // (seen before: no card over the test)
            for (int i = 0; i < 4; i++) c.Spawn<Pickup>("pebbles", 6, 1);
            yield return c.Frames(20);
            c.Teleport(6.5f, 1f);
            yield return c.Frames(20);
            int left = 0, leftValue = 0;
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup pk && pk.resource == Res.Pebbles) { left++; leftValue += pk.value; }
            c.Expect(run.Pebbles == 10 && left == 1 && leftValue == 2, $"ten are picked up and the two that do not fit stay on the ground (carried {run.Pebbles}, piles left {left} holding {leftValue})");

            // a shot: one pebble spent, one point to what it meets
            c.Teleport(3.5f, 1f);
            c.p.facing = 1;
            var snake = c.Spawn<Snake>("snake", 9, 1);
            yield return c.Frames(4);
            // held, nothing flies yet; let go, it does - from the hand, at the height of the arm
            sling.Use(c.p, new FrameInput { usePressed = true, useHeld = true });
            for (int i = 0; i < 20; i++) { sling.Hold(c.p, new FrameInput { useHeld = true }); yield return new WaitForFixedUpdate(); }
            c.Expect(sling.Drawn && c.Count<PebbleShot>() == 0 && run.Pebbles == 10, $"while the button is held the band is drawn and nothing flies (drawn {sling.Drawn}, pebbles {run.Pebbles})");
            sling.Hold(c.p, new FrameInput { useHeld = false });
            yield return new WaitForFixedUpdate();
            PebbleShot inAir = null;
            foreach (var e in c.world.Entities) if (!e.Dead && e is PebbleShot ps) inAir = ps;
            bool flew = inAir != null;
            float shotHeight = inAir != null ? inAir.pos.y - c.p.pos.y : -1f;
            c.Expect(flew && !sling.Drawn && shotHeight > 0.35f && shotHeight < 0.6f, $"let go, the pebble flies from the hand, at arm height ({shotHeight:0.00} above the feet)");
            for (int i = 0; i < 90 && snake != null && !snake.Dead; i++) yield return new WaitForFixedUpdate();
            c.Expect(flew && run.Pebbles == 9 && snake != null && snake.Dead, $"a shot spends one pebble and kills a snake across the room (flew {flew}, pebbles {run.Pebbles}, snake dead {snake != null && snake.Dead})");
            yield return c.Frames(30);
            c.Expect(c.Count<PebbleShot>() == 0, "the pebble is gone after it has hit: it is not picked up again");

            // a pot in its way is broken open
            foreach (var e in c.world.Entities) if (!e.Dead && e is Pickup pk2 && pk2.resource != Res.Pebbles) e.Despawn();
            var jar = c.Spawn<Breakable>("pot", 8, 1);
            yield return c.Frames(10);
            yield return Shoot();
            for (int i = 0; i < 90 && jar != null && !jar.Dead; i++) yield return new WaitForFixedUpdate();
            c.Expect(run.Pebbles == 8 && jar != null && jar.Dead, $"a pebble breaks a pot from afar (pebbles {run.Pebbles}, pot broken {jar != null && jar.Dead})");
            yield return c.Frames(20);
            foreach (var e in c.world.Entities) if (!e.Dead && (e is Enemy || (e is Pickup pk3 && pk3.resource != Res.Pebbles))) e.Despawn();      // (whatever the pot held)

            // crouched, the pebble leaves low and flies on along the floor (for what is too small to hit standing)
            yield return c.Frames(30);
            c.Bot(Seq((200, F(down: true))));
            yield return c.Frames(12);
            sling.Use(c.p, new FrameInput { usePressed = true, useHeld = true, down = true });
            for (int i = 0; i < 6; i++) { sling.Hold(c.p, new FrameInput { useHeld = true, down = true }); yield return new WaitForFixedUpdate(); }
            bool crouched = c.p.Crouching;
            sling.Hold(c.p, new FrameInput { useHeld = false, down = true });
            float lowHeight = -1f, lowLater = -1f, flown = 0f;
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var e in c.world.Entities)
                    if (!e.Dead && e is PebbleShot ps2)
                    {
                        if (lowHeight < 0f) lowHeight = ps2.pos.y - c.p.pos.y;
                        lowLater = ps2.pos.y - c.p.pos.y; flown = ps2.pos.x - c.p.pos.x;
                    }
            }
            c.Expect(crouched && lowHeight >= 0.05f && lowHeight < 0.3f && Mathf.Abs(lowLater - lowHeight) < 0.01f && flown > 2f && run.Pebbles == 7,
                $"let go while crouched, the pebble leaves low ({lowHeight:0.00} above the feet, against 0.42 standing) and flies on level along the floor ({flown:0.0} tiles on, still at {lowLater:0.00})");
            c.Bot(Seq((9000, F())));
            yield return c.Frames(40);
            run.AddResource(Res.Pebbles, 8 - run.Pebbles);        // (back to what the next part counts from)

            // a draw that is dropped (the button is no longer seen: stunned, a menu, other gear in hand) spends nothing
            yield return c.Frames(30);
            sling.Use(c.p, new FrameInput { usePressed = true, useHeld = true });
            yield return c.Frames(10);         // (ten steps without a word from the button)
            sling.Hold(c.p, new FrameInput { useHeld = false });
            yield return c.Frames(3);
            c.Expect(!sling.Drawn && c.Count<PebbleShot>() == 0 && run.Pebbles == 8, $"a draw that was interrupted is dropped and no pebble is spent (pebbles {run.Pebbles})");

            // the Pebble Pouch: ten more
            var pouch = c.world.config.shop.Find("pebblepouch");
            c.Expect(pouch != null && pouch.icon != null && pouch.isUpgrade, "shops sell a Pebble Pouch");
            if (pouch != null)
            {
                ShopItem.Apply(run, pouch);
                c.Expect(run.PebbleCap == 20, $"worn, it makes room for twenty (room {run.PebbleCap})");
                c.Teleport(6.5f, 1f);
                yield return c.Frames(20);
                c.Expect(run.Pebbles == 10, $"and the pile left behind is picked up now (carried {run.Pebbles})");
            }
            c.Teleport(3.5f, 1f);
        }

        static IEnumerator BagsCase(Ctx c)
        {
            c.p.godMode = true;
            var run = c.run;
            Villagers.testRescued = new HashSet<string>();       // the test keeps its own state: the save file is not touched
            Villagers.ResetTestState();
            Bags.ResetTestState();
            Villagers.GiveKey(Villagers.KeyPouch);
            ItemLog.FirstTime("bombs"); ItemLog.FirstTime("ropes");       // (seen before: no cards over the test)
            run.bombs = 4; run.ropes = 5;
            c.Expect(run.BombCap == 5 && run.RopeCap == 5 && run.PebbleCap == 10 && Villagers.PouchCapacity == 5,
                $"at first: five bombs, five ropes, ten pebbles, five embers (bombs {run.BombCap}, ropes {run.RopeCap}, pebbles {run.PebbleCap}, pouch {Villagers.PouchCapacity})");

            // a bomb bag (three) with room for one: one is taken, two stay; a rope pile with no room at all stays whole
            c.Teleport(3.5f, 1f);
            c.Spawn<Pickup>("bombbag", 6, 1);
            c.Spawn<Pickup>("ropepile", 6, 1);
            yield return c.Frames(20);
            c.Teleport(6.5f, 1f);
            yield return c.Frames(20);
            int bombsLeft = -1, ropesLeft = -1;
            foreach (var e in c.world.Entities)
                if (!e.Dead && e is Pickup pk) { if (pk.resource == Res.Bombs) bombsLeft = pk.value; else if (pk.resource == Res.Ropes) ropesLeft = pk.value; }
            c.Expect(run.bombs == 5 && run.ropes == 5 && bombsLeft == 2 && ropesLeft == 3,
                $"only what fits is picked up, the rest stays on the ground (bombs {run.bombs} with {bombsLeft} left lying, ropes {run.ropes} with {ropesLeft} left lying)");

            // a shop does not sell what cannot be carried
            c.Teleport(3.5f, 1f);
            run.gold = 2000;
            var ware = c.Spawn<ShopItem>("shopitem", 9, 1, 0, "bombs");
            yield return c.Frames(3);
            if (ware != null) ware.Interact(c.p);
            yield return c.Frames(3);
            c.Expect(ware != null && !ware.Dead && run.gold == 2000 && run.bombs == 5, $"with a full bag a shop sells no more bombs and takes no gold (gold {run.gold}, bombs {run.bombs})");
            if (ware != null) ware.Despawn();

            // the elder: spoken to, he opens a screen with his work on the four bags (nothing on the floor)
            var elder = c.Spawn<Elder>("elder", 11, 1);
            yield return c.Frames(4);
            var ui = c.dir.villageShop;
            int laidOut = 0;
            foreach (var e in c.world.Entities) if (!e.Dead && e is ShopItem) laidOut++;
            string[] elderOffers = elder != null ? elder.Choices(c.p) : null;
            c.Expect(elderOffers != null && elderOffers.Length == 2 && elderOffers[0] == "Talk" && elderOffers[1] == "Upgrade" && ui != null && !ui.IsOpen,
                "by the elder the prompt offers a word with him or his work on the bags; nothing opens yet");
            if (elder != null) elder.Choose(c.p, 1);
            yield return new WaitForSecondsRealtime(0.3f);
            // every picture in the list is drawn at the same size of pixel
            {
                float unit = -1f; bool same = true; string sizes = "";
                for (int i = 0; ui != null && i < ui.icons.Length && i < ui.Count; i++)
                {
                    var im = ui.icons[i];
                    if (im == null || im.sprite == null) continue;
                    float per = im.rectTransform.sizeDelta.x / im.sprite.rect.width;      // screen units to one pixel of the picture
                    sizes += $"{per:0.0} ";
                    if (unit < 0f) unit = per; else if (Mathf.Abs(per - unit) > 0.01f) same = false;
                }
                c.Expect(unit > 0f && same, $"the pictures of the wares all have the same size of pixel ({sizes.Trim()})");
            }
            int Row(string kind) { for (int i = 0; ui != null && i < ui.Count; i++) if (ui.At(i).bag == kind) return i; return -1; }
            c.Expect(laidOut == 0 && ui != null && ui.IsOpen && ui.Count == 4 && Row(Bags.Bombs) >= 0 && Row(Bags.Ropes) >= 0 && Row(Bags.Pebbles) >= 0 && Row(Bags.Embers) >= 0,
                $"the elder's work on all four bags is a screen, not wares on the floor (rows {(ui != null ? ui.Count : -1)}, on the floor {laidOut})");
            var uio = UIOverlap.Find();
            c.Expect(uio.Count == 0, "the screen is laid out cleanly" + (uio.Count > 0 ? " (" + uio[0] + ")" : ""));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "elder_bags.png"));
            yield return new WaitForSecondsRealtime(0.3f);

            // 1 ember, then 2, then 3: five more each time, three times; without the embers nothing happens
            Villagers.AddEmbers(5);
            int bombs0 = run.bombs, ropes0 = run.ropes, pebbles0 = run.Pebbles;
            ui.Buy(Row(Bags.Bombs));
            c.Expect(run.BombCap == 10 && run.bombs == bombs0 && Villagers.Embers == 4 && Bags.Price(Bags.Bombs) == 2, $"the first enlarging costs 1 ember and the bag holds ten - but nothing is put in it (holds {run.BombCap}, bombs {bombs0} -> {run.bombs}, embers {Villagers.Embers}, next {Bags.Price(Bags.Bombs)})");
            ui.Buy(Row(Bags.Bombs));
            c.Expect(run.BombCap == 15 && Villagers.Embers == 2 && Bags.Price(Bags.Bombs) == 3, $"the second costs 2 and it holds fifteen (holds {run.BombCap}, embers {Villagers.Embers})");
            bool refused = !ui.Buy(Row(Bags.Bombs));
            c.Expect(refused && run.BombCap == 15 && Villagers.Embers == 2, "with too few embers left nothing is enlarged, and none are taken");
            Villagers.AddEmbers(5);
            ui.Buy(Row(Bags.Bombs));
            bool atMost = !ui.Buy(Row(Bags.Bombs));
            c.Expect(run.BombCap == 20 && Villagers.Embers == 2 && !Bags.CanEnlarge(Bags.Bombs) && atMost,
                $"the third costs 3, it holds twenty, and that is as large as it gets (holds {run.BombCap}, embers {Villagers.Embers})");

            // the pouch itself, the ropes and the pebbles go the same way
            Villagers.AddEmbers(5);
            ui.Buy(Row(Bags.Embers));
            c.Expect(Villagers.PouchCapacity == 10 && Villagers.Embers == 4, $"the Ember Pouch is let out the same way (holds {Villagers.PouchCapacity}, embers {Villagers.Embers})");
            ui.Buy(Row(Bags.Ropes));
            Villagers.AddEmbers(9);
            ui.Buy(Row(Bags.Pebbles));
            var pouchItem = c.world.config.shop.Find("pebblepouch");
            if (pouchItem != null && !run.HasUpgrade("pebblepouch")) ShopItem.Apply(run, pouchItem);
            c.Expect(run.RopeCap == 10 && run.PebbleCap == 25, $"ropes ten; pebbles fifteen from the elder and ten more from the Pebble Pouch worn on top (ropes {run.RopeCap}, pebbles {run.PebbleCap})");
            yield return new WaitForSecondsRealtime(0.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "elder_bags_after.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            ui.Close();
            yield return c.Frames(3);

            // a bag let out is not filled: what goes in it is bought (Pip) or found
            c.Expect(run.bombs == bombs0 && run.ropes == ropes0 && run.Pebbles == pebbles0, $"no bag he let out was filled on the spot (bombs {run.bombs}, ropes {run.ropes}, pebbles {run.Pebbles})");
            // what a run ends with is kept for the next one; a save's very first run sets out with full bags
            {
                Supplies.ResetTestState();
                RunState Fresh() => new RunState { bombBase = 5, ropeBase = 5, pebbleBase = 10 };
                var first = Fresh();
                Supplies.TakeInto(first);
                bool firstFull = first.bombs == first.BombCap && first.ropes == first.RopeCap && first.Pebbles == first.PebbleCap;
                var ended = Fresh();
                ended.bombs = 3; ended.ropes = 1; ended.AddResource(Res.Pebbles, 7); ended.AddResource(FlareGear.AmmoId, 2); ended.AddResource(Story.Paper, 1);
                Supplies.KeepFrom(ended);
                var next = Fresh();
                Supplies.TakeInto(next);
                c.Expect(firstFull && next.bombs == 3 && next.ropes == 1 && next.Pebbles == 7 && next.GetResource(FlareGear.AmmoId) == 2 && next.gear.Contains(FlareGear.Id)
                         && next.GetResource(Story.Paper) == 1,
                    $"a save's first run has full bags ({first.bombs}/{first.ropes}/{first.Pebbles}); after that a run takes what the last one ended with - bombs {next.bombs}, ropes {next.ropes}, pebbles {next.Pebbles}, flares {next.GetResource(FlareGear.AmmoId)}, paper {next.GetResource(Story.Paper)}");
                Supplies.ResetTestState();
            }
            // and with room again, what was left lying is picked up
            run.bombs = 18; run.ropes = 7;
            c.Teleport(6.5f, 1f);
            yield return c.Frames(20);
            c.Expect(run.bombs == 20 && run.ropes == 10, $"with room in the bags the bombs and ropes left behind are picked up (bombs {run.bombs}, ropes {run.ropes})");

            // a Challenge fight is the same for every save: none of the elder's work goes into it (this is the
            // last thing the scenario does - it leaves that fight as the run in hand)
            var ch = RunSession.NewBossPractice(0, c.world.config.player);
            var pc = c.world.config.player;
            c.Expect(ch.bossPractice && ch.BombCap == pc.bombCapacity && ch.RopeCap == pc.ropeCapacity && ch.PebbleCap == pc.pebbleCapacity
                     && ch.bombs == pc.bombCapacity && ch.ropes == 0 && ch.Pebbles == pc.pebbleCapacity,
                $"a Challenge fight sets out with the bags at their first size, whatever the elder did (bombs {ch.bombs}/{ch.BombCap}, ropes {ch.ropes}/{ch.RopeCap}, pebbles {ch.Pebbles}/{ch.PebbleCap})");
            c.Expect(ch.gear.Count == 2 && ch.gear.Contains("slingshot") && ch.gear.Contains("bomb"),
                $"and with the slingshot and the bombs alone: no rope, no Recall Charm ({string.Join(", ", ch.gear)})");

            foreach (var e in c.world.Entities) if (!e.Dead && (e is ShopItem || e is Elder)) e.Despawn();
            if (run.HasUpgrade("pebblepouch")) run.RemoveUpgrade("pebblepouch");
            c.Teleport(3.5f, 1f);
            Bags.ResetTestState();
            Villagers.ResetTestState();
            Villagers.testRescued = null;
            yield return c.Frames(2);
        }

        static IEnumerator HeartCase(Ctx c)
        {
            c.run.health = 2;
            var h = c.Spawn<Pickup>("heart", 5, 1);
            c.Expect(h != null, "heart prefab exists");
            c.Bot(Seq((40, F(x: 1)), (10, F())));
            yield return c.Frames(55);
            c.Expect(c.run.health == 3, $"healed to 3 (got {c.run.health})");
        }

        static RopeProjectile RopeAt(Ctx c, int column)
        {
            foreach (var e in c.world.Entities)
                if (e is RopeProjectile r && !r.Dead && r.Hooked && r.Column == column) return r;
            return null;
        }

        static IEnumerator RopeHoldCase(Ctx c)
        {
            c.run.Equip("rope");
            c.run.ropes = 6;
            // beside the ledge (wall on the right): hook goes over the lip
            c.Bot(Seq((3, F()), (1, F(rope: true)), (40, F())));
            yield return c.Frames(44);
            var a = RopeAt(c, 6);
            c.Expect(a != null && a.Hold == RopeProjectile.HoldKind.Lip && a.HoldSide == 1, "ledge: lip hold on the right (got " + (a != null ? a.Hold + "/" + a.HoldSide : "no rope") + ")");
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "rope_lip.png"));
            yield return new WaitForSecondsRealtime(0.3f);

            // beside the tall wall on the left: piton in the wall face
            c.Teleport(18.5f, 1f);
            c.Bot(Seq((10, F()), (1, F(rope: true)), (40, F())));
            yield return c.Frames(51);
            var b = RopeAt(c, 18);
            c.Expect(b != null && b.Hold == RopeProjectile.HoldKind.Face && b.HoldSide == -1, "wall: piton on the left (got " + (b != null ? b.Hold + "/" + b.HoldSide : "no rope") + ")");
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "rope_piton.png"));
            yield return new WaitForSecondsRealtime(0.3f);

            // under the low ceiling: classic hook on the ceiling
            c.Teleport(21.5f, 1f);
            c.Bot(Seq((10, F()), (1, F(rope: true)), (40, F())));
            yield return c.Frames(51);
            var d = RopeAt(c, 21);
            c.Expect(d != null && d.Hold == RopeProjectile.HoldKind.Ceiling, "ceiling hold (got " + (d != null ? d.Hold.ToString() : "no rope") + ")");
        }

        static IEnumerator WaypointCase(Ctx c)
        {
            var hud = c.dir.hud;
            yield return c.Frames(5);
            c.Expect(!hud.WaypointVisible, "no marker without the Compass upgrade");
            c.run.AddUpgrade(c.dir.config.shop.Find(UpgradeIds.Compass));
            c.Bot(Seq((40, F())));
            yield return c.Frames(40);
            yield return new WaitForSecondsRealtime(0.4f);
            var half = ((RectTransform)hud.compassRoot.transform).rect.size * 0.5f;
            c.Expect(hud.WaypointVisible && !hud.WaypointOnScreen, $"marker shown at the edge (visible {hud.WaypointVisible}, onScreen {hud.WaypointOnScreen})");
            c.Expect(hud.WaypointPos.x > half.x - 140f, $"edge marker on the right edge (x {hud.WaypointPos.x:0} / {half.x:0})");
            c.Expect(hud.wpChevron.color.a > 0.9f && hud.wpTail.color.a < 0.1f, "chevron shown, pin tail hidden");
            c.Expect(hud.compassText.text.EndsWith(" m"), "distance label: " + hud.compassText.text);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "waypoint_edge.png"));
            yield return new WaitForSecondsRealtime(0.3f);

            c.Teleport(53.5f, 1f);
            c.Bot(Seq((90, F())));
            yield return c.Frames(90);
            yield return new WaitForSecondsRealtime(0.6f);
            Vector2 d = hud.WaypointPos - hud.WaypointTarget;
            c.Expect(hud.WaypointOnScreen, "marker switches to on-screen mode near the exit");
            c.Expect(Mathf.Abs(d.x) < 12f && d.y > 55f && d.y < 95f, $"marker floats just above the door (offset {d.x:0},{d.y:0})");
            c.Expect(hud.wpTail.color.a > 0.9f && hud.wpChevron.color.a < 0.1f, "pin tail shown, chevron hidden");
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "waypoint_onscreen.png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static IEnumerator HighlightCase(Ctx c)
        {
            c.Spawn<Pickup>("gold_big", 13, 1);
            c.Spawn<Pickup>("gem", 15, 1);
            c.Spawn<Pickup>("heart", 17, 1);
            c.Spawn<Pickup>("bombbag", 19, 1);
            c.Bot(Seq((60, F())));
            yield return c.Frames(40);
            int glows = 0, withHighlight = 0;
            bool hintShown = false;
            foreach (var e in c.world.Entities)
            {
                if (!(e is Item) && !(e is Pickup)) continue;
                var h = e.GetComponent<Highlight>();
                if (h == null) continue;
                withHighlight++;
                var g = e.transform.Find("Glow");
                if (g != null && g.GetComponent<SpriteRenderer>().enabled) glows++;
                var hint = e.transform.Find("Hint");
                if (hint != null && hint.GetComponent<SpriteRenderer>().enabled) hintShown = true;
            }
            c.Expect(withHighlight >= 7 && glows >= 7, $"all collectibles highlighted ({withHighlight} with highlight, {glows} glowing)");
            c.Expect(hintShown, "hint arrow shown over the rock next to the player");

            // bobbing pickups only ever rise above their resting spot (never sink into the ground)
            float minY = 99f, maxY = -99f;
            for (int i = 0; i < 90; i++)
            {
                foreach (var e in c.world.Entities)
                {
                    if (!(e is Pickup) || e.GetComponent<Highlight>() == null || !e.GetComponent<Highlight>().bob) continue;
                    var sp = e.transform.Find("Sprite");
                    if (sp == null) continue;
                    minY = Mathf.Min(minY, sp.localPosition.y); maxY = Mathf.Max(maxY, sp.localPosition.y);
                }
                yield return null;
            }
            c.Expect(minY >= -0.001f && maxY > 0.05f, $"bob stays above the ground (sprite y {minY:0.000}..{maxY:0.000})");
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "collectibles.png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static IEnumerator StompFallCase(Ctx c)
        {
            // same height as the plain long-fall case, but a snake is standing where the player lands
            var snake = c.Spawn<Snake>("snake", 12, 1);
            snake.heartDropChance = 0f;
            snake.goldChance = 1f;             // (a kill does not always pay: this one must, the test is about collecting it)
            snake.stunTimer = 99f;             // it stays put (a stunned snake does not walk) and is still stompable
            c.Bot(Seq((200, F())));
            yield return c.Frames(2);
            int hp0 = c.run.health, gold0 = c.run.gold;
            c.Teleport(snake.pos.x, 1f + 10.5f);
            bool stunned = false;
            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForFixedUpdate();
                if (c.p.IsStunned) stunned = true;
            }
            c.Expect(snake.Dead, "the snake was stomped");
            c.Expect(c.run.health == hp0 && !stunned, $"no fall damage or stun (health {hp0} -> {c.run.health})");
            c.Expect(c.run.gold > gold0, $"the kill dropped gold and it was collected (gold {gold0} -> {c.run.gold})");
        }

        static IEnumerator FallCase(Ctx c)
        {
            c.Teleport(12.5f, 1f + 10.5f);
            c.Bot(Seq((90, F())));
            int hp0 = c.run.health;
            bool stunned = false;
            for (int i = 0; i < 90; i++) { yield return new WaitForFixedUpdate(); if (c.p.IsStunned) stunned = true; }
            c.Expect(c.run.health == hp0 - c.p.cfg.fallDamage, $"took {c.p.cfg.fallDamage} fall damage (health {hp0} -> {c.run.health})");
            c.Expect(stunned, "stunned after landing");
        }

        // --------------------------------------------------------------------------------------- flow

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-scenarios") Active = true;

                if (args[i] == "-autotestOut" && i + 1 < args.Length) outDir = args[i + 1];
                if (args[i] == "-scenarioFilter" && i + 1 < args.Length)
                    filter = args[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
            if (!Active) return;
            if (filter != null) for (int i = 0; i < filter.Length; i++) filter[i] = filter[i].Trim();
            SkipUnwanted();
            if (index >= all.Count) { Debug.LogError("[Scenario] -scenarioFilter matches no scenario"); Active = false; return; }
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(outDir);
            report.Clear();
            report.AppendLine($"Brindlemoor in-game scenario tests {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public static string CurrentName => all[index].name;
        public static string OutDir => outDir;

        /// <summary>Called by GameDirector instead of the normal level creation.</summary>
        public static LevelData BuildLevel()
        {
            int height = all[index].name.StartsWith("bosses: the rot heart") ? 12 : all[index].name.StartsWith("long fall") || all[index].name.StartsWith("new wares") || all[index].name.StartsWith("rope into open air") || all[index].name.StartsWith("gliding down") || all[index].name.StartsWith("rope hook visibly") || all[index].name.StartsWith("room toys") ? 16 : H;
            W = all[index].name.StartsWith("waypoint") ? 64 : 26;
            var m = new char[W, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < W; x++)
                    m[x, y] = (x == 0 || y == 0 || x == W - 1 || y == height - 1) ? 'X' : '.';
            all[index].map(m);
            var sb = new StringBuilder("name: scenario\n---\n");
            bool hasG = false;
            foreach (var ch in m) if (ch == 'G') hasG = true;
            if (!hasG) m[W - 2, 1] = 'G';
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < W; x++) sb.Append(m[x, y]);
                sb.Append('\n');
            }
            return HandcraftedLevel.Parse(sb.ToString(), "SCENARIO", LevelKind.Tutorial, 1);
        }

        public static IEnumerator RunCurrent(GameDirector dir)
        {
            yield return new WaitForFixedUpdate();
            var c = new Ctx { dir = dir, world = dir.world, p = dir.Player, run = RunSession.Current };
            var sc = all[index];
            Exception err = null;
            var it = sc.run(c);
            while (true)
            {
                object cur;
                try
                {
                    if (!it.MoveNext()) break;
                    cur = it.Current;
                }
                catch (Exception e) { err = e; break; }
                yield return cur;
            }
            if (err != null) { c.ok = false; c.detail += "EXCEPTION " + err.Message; }
            // whatever is on screen at the end of a scenario must be laid out cleanly: no text over another,
            // none sticking out of its frame or off the screen
            yield return null;
            {
                var faults = UIOverlap.Find();
                if (faults.Count > 0) { c.ok = false; c.detail += " UI LAYOUT: " + string.Join(" | ", faults.GetRange(0, Mathf.Min(4, faults.Count))) + (faults.Count > 4 ? $" (+{faults.Count - 4} more)" : ""); }
            }
            if (c.ok) pass++; else fail++;
            string line = $"{(c.ok ? "PASS" : "FAIL")}  {sc.name}  {c.detail}";
            report.AppendLine(line);
            Debug.Log("[Scenario] " + line);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"scenario_{index:00}.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            index++;
            SkipUnwanted();
            if (index < all.Count)
            {
                RunSession.NewRun(1, true, dir.config.player);
                RunSession.Current.bombs = 4; RunSession.Current.ropes = 4;
                SceneManager.LoadScene(GameConst.SceneGame);
            }
            else
            {
                report.AppendLine($"\n{pass} passed, {fail} failed" + (Filtered ? $"   (only: {string.Join(", ", filter)})" : ""));
                File.WriteAllText(Path.Combine(outDir, Filtered ? "scenario_report_partial.txt" : "scenario_report.txt"), report.ToString());
                Application.Quit(fail == 0 ? 0 : 1);
            }
        }
    }
}
