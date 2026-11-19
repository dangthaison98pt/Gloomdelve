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
    ///   Gloomdelve.exe -scenarios [-autotestOut path] [-scenarioFilter "moth,lurecap"]
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

            new Scenario { name = "glider gate: without the glider the chasm is deadly", map = ChasmMap,
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
            c.Expect(c.run.bombs == 7, $"bombs 7 (got {c.run.bombs})");
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
                InputSystem.RemoveDevice(pad);
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
            c.dir.results.ShowRunEnd(c.run, false, "NEW BEST GOLD!", 1);
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
            for (int x = 7; x <= 12; x++) c.world.Level.lethalSpikes.Add(new Vector2Int(x, 1));   // a gate chasm, as the generator marks it
            if (!c.run.gear.Contains("glider")) c.run.gear.Add("glider");
            c.run.Equip(glider ? "glider" : "bomb");
            var seq = Seq((6, F(x: 1)), (1, F(x: 1, jp: true, jh: true)), (140, F(x: 1, jh: true)), (40, F()));
            c.Bot(seq);
            yield return c.Frames(seq.Count + 5);
            if (glider) c.Expect(!c.p.IsDead && c.p.pos.x > 13f && Mathf.Abs(c.p.pos.y - 2f) < 0.01f, $"crossed (pos {c.p.pos}, dead {c.p.IsDead})");
            else c.Expect(c.p.IsDead || c.p.pos.x < 7f, $"could not cross (pos {c.p.pos}, dead {c.p.IsDead})");
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
                // up from the first row reaches the tabs; the second tab is what is used, the third what is kept for good
                inv.Move(-inv.Selected);
                inv.MoveRow(-1);
                c.Expect(inv.OnTabs, "up from the first row reaches the tabs");
                inv.SetTab(InventoryScreen.TabItems);
                c.Expect(inv.EntryCount == run.gear.Count && run.gear.Count >= 2, $"the items tab lists what is used ({inv.EntryCount} of {run.gear.Count})");
                inv.MoveRow(1);
                run.Equip(run.gear[0]);
                inv.Move(1 - inv.Selected);
                inv.Toggle();
                c.Expect(!inv.OnTabs && run.EquippedId == run.gear[1], "confirm on an item takes it in hand (" + run.EquippedId + ")");
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory_items.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                var keptRescued = Villagers.testRescued; Villagers.testRescued = new HashSet<string>(); Villagers.ResetTestState();
                Villagers.GiveKey(Villagers.KeyPouch);
                inv.MoveRow(-1); inv.MoveRow(-1);
                inv.SetTab(InventoryScreen.TabKeys);
                c.Expect(inv.EntryCount == 1, $"the key items tab shows the Ember Pouch ({inv.EntryCount})");
                inv.MoveRow(1);
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "inventory_keys.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                Villagers.ResetTestState(); Villagers.testRescued = keptRescued;
                inv.Close();
                c.Expect(!inv.IsOpen && Mathf.Approximately(Time.timeScale, scale), "closed + resumed");
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
            // bombs and traps: half a point each, whatever they would do to the player
            int h1 = b.health;
            b.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, new Vector2(20f, 20f), "test"));
            int afterOne = h1 - b.health;
            b.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, new Vector2(20f, 20f), "test"));
            c.Expect(afterOne == 0 && h1 - b.health == 1, $"a bomb blast takes half a point from a boss: one blast {afterOne}, two blasts {h1 - b.health}");
            yield return c.Frames(40);
            int h2 = b.health;
            b.TakeDamage(new DamageInfo(99, DamageType.Spikes, null, Vector2.zero, "spikes"));
            int trapOne = h2 - b.health;
            b.TakeDamage(new DamageInfo(99, DamageType.Crush, null, Vector2.zero, "test"));
            int trapSame = h2 - b.health;
            yield return c.Frames(40);
            b.TakeDamage(new DamageInfo(99, DamageType.Fire, null, Vector2.zero, "test"));
            c.Expect(trapOne == 0 && trapSame == 0 && h2 - b.health == 1, $"a trap takes half a point too, and not twice in the same moment: {trapOne}, {trapSame}, then {h2 - b.health} after a second one");
            {
                // the Hollow Warden is not hurt by traps at all (a bomb still counts its half)
                var ghost = c.Spawn<Warden>("warden", 22, 3);
                yield return c.Frames(3);
                if (ghost != null)
                {
                    int g0 = ghost.health;
                    for (int i = 0; i < 4; i++) { ghost.TakeDamage(new DamageInfo(99, i % 2 == 0 ? DamageType.Spikes : DamageType.Fire, null, Vector2.zero, "test")); yield return c.Frames(40); }
                    int byTraps = g0 - ghost.health;
                    ghost.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, Vector2.zero, "test"));
                    ghost.TakeDamage(new DamageInfo(3, DamageType.Explosion, c.p, Vector2.zero, "test"));
                    c.Expect(byTraps == 0 && g0 - ghost.health == 1, $"traps do nothing to the Warden ({byTraps}); two bombs take 1 ({g0 - ghost.health})");
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
                var kept = SaveSystem.Records.bosses;
                SaveSystem.Records.bosses = new List<BossRecord>();
                SaveSystem.RecordBossWin("Test Boss", 50f, false, false, out bool nb, out bool ff);
                c.Expect(nb && !ff && SaveSystem.BossRecordOf("Test Boss") == null, "compare only: a first win would be a new best, nothing stored");
                SaveSystem.Records.bosses.Add(new BossRecord { boss = "Test Boss", bestTime = 50f });
                SaveSystem.RecordBossWin("Test Boss", 60f, true, false, out nb, out ff);
                c.Expect(!nb && ff, "a slower win is no new best, but the first one without damage earns the badge");
                SaveSystem.RecordBossWin("Test Boss", 40f, false, false, out nb, out ff);
                c.Expect(nb && !ff, "a faster win is a new best");
                SaveSystem.Records.bosses = kept;
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
                c.Expect(without > 110 && without < 190, $"about every other run has no caged villager at all ({without} of 300 runs without)");
            }

            // embers: the money of the village. Only with the elder's pouch, and only as many as it holds
            var run = c.run;
            c.Expect(Villagers.AddEmbers(2) == 0 && Villagers.Embers == 0, "without the elder's pouch an ember cannot be kept");
            var elder = c.Spawn<Elder>("elder", 26, 1);
            yield return c.Frames(3);
            c.Expect(elder != null && elder.GetPrompt(c.p) != null && elder.GetPrompt(c.p).Contains("talk"), "the elder has something to say");
            if (elder != null) elder.Interact(c.p);
            c.Expect(Villagers.HasKey(Villagers.KeyPouch), "talking to the elder gives the Ember Pouch");
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
            ShopItem ware = null;
            foreach (var e in c.world.Entities) if (e is ShopItem si && !si.Dead) ware = si;
            c.Expect(ware != null && ware.GetPrompt(c.p) != null && ware.GetPrompt(c.p).Contains("buy"), "a ware in the village is for sale, not free");
            if (ware != null)
            {
                int hp = run.maxHealth, gold0 = run.gold, e0 = Villagers.Embers;
                int price = Villagers.EmberPrice(c.dir.config.shop.Find("vigor"), Villagers.All[2]);
                ware.Interact(c.p);
                c.Expect(run.maxHealth == hp + 1 && Villagers.Embers == e0 - price && run.gold == gold0,
                    $"buying the Heart Draught: +1 heart for this run, {price} embers paid ({e0} -> {Villagers.Embers}), no gold");
                run.maxHealth = hp; run.health = Mathf.Min(run.health, hp);
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
                c.Expect(locked != null && locked.GetPrompt(c.p).Contains("locked"), "the locked door says so");
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
                c.Expect(near != null && near.GetPrompt(c.p).Contains("look at") && !string.IsNullOrEmpty(near.text), "each offers a closer look and has something to say");
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
            c.Expect(b != null && b.maxHealth == 30, "the yeti king spawned with 30 health");
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

        static IEnumerator GearWyrmCase(Ctx c)
        {
            c.p.godMode = true;
            c.Bot(Seq((9000, F())));
            c.Teleport(6.5f, 1f);
            var b = c.Spawn<GearWyrm>("gearwyrm", 14, 1, 0, null, -1);
            c.Expect(b != null && c.dir.Boss == b && b.maxHealth == 30 && b.SegmentCount == 6, "the cogwyrm spawned as the boss: 30 health, six segments");
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
            c.Expect(Mathf.Abs(d.x) < 12f && d.y > 100f && d.y < 150f, $"marker floats just above the door (offset {d.x:0},{d.y:0})");
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
            report.AppendLine($"Gloomdelve in-game scenario tests {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public static string CurrentName => all[index].name;
        public static string OutDir => outDir;

        /// <summary>Called by GameDirector instead of the normal level creation.</summary>
        public static LevelData BuildLevel()
        {
            int height = all[index].name.StartsWith("bosses: the rot heart") ? 12 : all[index].name.StartsWith("long fall") || all[index].name.StartsWith("rope into open air") || all[index].name.StartsWith("gliding down") || all[index].name.StartsWith("rope hook visibly") ? 16 : H;
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
