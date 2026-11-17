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
    ///   Gloomdelve.exe -scenarios [-autotestOut path]
    /// Each scenario builds a small arena, scripts the player's inputs (or places things), then checks the outcome.
    /// </summary>
    public static class ScenarioRunner
    {
        public static bool Active;
        static int index;
        static int pass, fail;
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

        static FrameInput F(int x = 0, bool up = false, bool down = false, bool jp = false, bool jh = false, bool atk = false, bool bomb = false, bool rope = false, bool act = false, bool use = false)
            => new FrameInput { x = x, up = up, down = down, jumpPressed = jp, jumpHeld = jh, attackPressed = atk, usePressed = bomb || rope || use, interactPressed = act };

        static void Set(char[,] m, int x, int y, char c) { if (x >= 0 && x < W && y >= 0 && y < H) m[x, y] = c; }

        static readonly List<Scenario> all = new List<Scenario>
        {
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

            new Scenario { name = "gear swap with real R1 presses: one tap after the wheel swaps back", map = m => { Set(m, 3, 1, 'S'); },
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
            c.Expect(c.run.gold == gold0 + 1200, $"idol paid (gold {c.run.gold})");
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
                inv.Move(idx - inv.Selected);
                inv.Toggle();
                c.Expect(run.IsWorn("iceboots"), "toggle on the screen wears the selected item");
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
            b.TakeDamage(new DamageInfo(99, DamageType.Explosion, c.p, new Vector2(20f, 20f), "test"));
            c.Expect(h0 - b.health == b.maxHitDamage && !b.Dead, $"a 99-damage hit only takes {b.maxHitDamage} (took {h0 - b.health})");
            c.Expect(Mathf.Abs(b.HealthFraction - b.health / (float)b.maxHealth) < 0.001f && b.HealthFraction < 1f, "health fraction feeds the bar");
            for (int i = 0; i < 40 && !b.Dead; i++)
            {
                b.TakeDamage(new DamageInfo(99, DamageType.Explosion, c.p, Vector2.zero, "test"));
                yield return c.Frames(2);
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
                bool acted = false, shots = false, rocks = false, balls = false;
                for (int i = 0; i < 1800; i++)
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
                        }
                    if (i == 200) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(OutDir, "boss_" + id + ".png"));
                }
                c.Expect(acted && !b.Dead, id + " moved or attacked and is still alive");
                // every boss has more than its two basic moves: the extra ones leave these traces within 30 seconds
                if (id == "broodmother") c.Expect(shots, "broodmother spat venom / sent a shockwave");
                if (id == "forgebrute") c.Expect(rocks && shots, $"forgebrute stomped rocks loose ({rocks}) and landed a leap with a shockwave ({shots})");
                if (id == "yetiking") c.Expect(balls && rocks, $"yetiking threw snowballs ({balls}) and shook icicles loose ({rocks})");
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
            c.Bot(Seq((1500, F())));
            var boss = c.Spawn<Broodmother>("broodmother", 16, 1);
            Bat minion = null;
            for (int i = 0; i < 1200 && minion == null; i++)
            {
                yield return c.Frames(1);
                foreach (var e in c.world.Entities) if (e is Bat bt && !bt.Dead) { minion = bt; break; }
            }
            c.Expect(minion != null && minion.summoned, "the boss called in a minion marked as summoned");
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
                boss.TakeDamage(new DamageInfo(99, DamageType.Explosion, c.p, Vector2.zero, "test"));
                yield return c.Frames(2);
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
            hud.ShowBanner("THE VAULT IS COLLAPSING!", "Carry the Heartstone back up to the Surface Gate!");
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
            w.QuickSwap();
            c.Expect(c.run.EquippedId == "bomb", "quick tap swaps back to bombs");
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

            // one quick tap right after: back to bomb (with 3 items, 'next' would have been glider)
            yield return Send(pad, r1, 0.09f);
            yield return Send(pad, none, 0.15f);
            log.Append("tap->" + c.run.EquippedId + " ");
            c.Expect(c.run.EquippedId == "bomb", "ONE tap after the wheel returns to bomb (got " + c.run.EquippedId + ")");

            // a slightly long tap (0.3 s: wheel pops open, nothing chosen) still swaps: back to rope
            yield return Send(pad, r1, 0.3f);
            yield return Send(pad, none, 0.15f);
            log.Append("longtap->" + c.run.EquippedId + " ");
            c.Expect(!w.IsOpen && c.run.EquippedId == "rope", "0.3 s tap still swaps (got " + c.run.EquippedId + ")");

            // holding without choosing for a long time just closes and keeps the item
            yield return Send(pad, r1, 0.7f);
            yield return Send(pad, none, 0.15f);
            c.Expect(!w.IsOpen && c.run.EquippedId == "rope", "long hold without choosing keeps rope (got " + c.run.EquippedId + ")");
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
            }
            if (!Active) return;
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
            int height = all[index].name.StartsWith("long fall") || all[index].name.StartsWith("rope into open air") || all[index].name.StartsWith("gliding down") || all[index].name.StartsWith("rope hook visibly") ? 16 : H;
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
            if (c.ok) pass++; else fail++;
            string line = $"{(c.ok ? "PASS" : "FAIL")}  {sc.name}  {c.detail}";
            report.AppendLine(line);
            Debug.Log("[Scenario] " + line);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"scenario_{index:00}.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            index++;
            if (index < all.Count)
            {
                RunSession.NewRun(1, true, dir.config.player);
                RunSession.Current.bombs = 4; RunSession.Current.ropes = 4;
                SceneManager.LoadScene(GameConst.SceneGame);
            }
            else
            {
                report.AppendLine($"\n{pass} passed, {fail} failed");
                File.WriteAllText(Path.Combine(outDir, "scenario_report.txt"), report.ToString());
                Application.Quit(fail == 0 ? 0 : 1);
            }
        }
    }
}
