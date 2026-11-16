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

            new Scenario { name = "falling onto spikes kills", map = m => { Set(m, 2, 1, 'S'); Set(m, 12, 1, '^'); },
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

            new Scenario { name = "long fall hurts and stuns", map = m => { Set(m, 3, 1, 'S'); },
                run = c => FallCase(c) },

            new Scenario { name = "pause menu freezes the game", map = m => { Set(m, 3, 1, 'S'); Set(m, 9, 1, 's'); },
                run = c => PauseCase(c, false) },

            new Scenario { name = "settings panel opens from pause", map = m => { Set(m, 3, 1, 'S'); },
                run = c => PauseCase(c, true) },

            new Scenario { name = "results screen after death", map = m => { Set(m, 3, 1, 'S'); Set(m, 12, 1, '^'); },
                run = c => ResultsCase(c) },

            new Scenario { name = "time-pressure wraith spawns and its touch is fatal", map = m => { Set(m, 3, 1, 'S'); },
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

        static IEnumerator StompCase(Ctx c)
        {
            c.Teleport(12.5f, 5f);
            c.Bot(Seq((90, F())));
            yield return c.Frames(90);
            c.Expect(c.Count<Snake>() == 0, "snake dead");
            c.Expect(c.run.health == 4, "player unhurt");
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
            c.Expect(c.p.DeathCause != null && c.p.DeathCause.Contains("Wraith"), "cause: " + c.p.DeathCause);
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

        static IEnumerator FallCase(Ctx c)
        {
            c.Teleport(12.5f, 1f + 10.5f);
            c.Bot(Seq((90, F())));
            int hp0 = c.run.health;
            bool stunned = false;
            for (int i = 0; i < 90; i++) { yield return new WaitForFixedUpdate(); if (c.p.IsStunned) stunned = true; }
            c.Expect(c.run.health == hp0 - 1, $"took fall damage (health {c.run.health})");
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
