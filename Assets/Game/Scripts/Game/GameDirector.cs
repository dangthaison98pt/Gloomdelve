using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gloomdelve
{
    /// <summary>Level flow: build level, spawn, pause, time pressure, exits, death, victory, restart.</summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        public GameConfig config;
        public World world;
        public CameraFollow cameraFollow;
        public Camera cam;
        public PlayerInputReader input;
        public HUD hud;
        public PauseMenu pauseMenu;
        public ResultsScreen results;
        public TransitionScreen transition;
        public EquipmentWheel gearWheel;
        public InventoryScreen inventory;
        [Tooltip("The Field Guide, open (read from the bag's KEY ITEMS).")] public CodexScreen codex;
        [Tooltip("A villager's shop (and the elder's work on the bags), open.")] public VillageShopPanel villageShop;
        public SkyBackdrop sky;
        public CaveBackdrop caveBackdrop;

        [Header("Debug (play Game.unity directly)")]
        [Tooltip("0 = random seed")] public int debugSeed;
        public int debugStartStage;
        public bool debugStartTutorial;
        public bool debugGodMode;

        public LevelData Level { get; private set; }
        public StageDef Stage { get; private set; }
        public PlayerController Player { get; private set; }
        public bool Ended { get; private set; }
        public bool Paused { get; private set; }
        /// <summary>The boss of this level, if any (boss arenas and the final vault).</summary>
        public Boss Boss { get; private set; }
        public bool BossAlive => Boss != null && !Boss.Dead;
        public bool PressureWarned => warned;
        public bool WraithActive => wraiths > 0;
        public bool Collapsing => collapsing;

        RunState Run => RunSession.Current;
        Door exitDoor;
        bool warned, collapsing, linkPending;
        int wraiths;
        float nextWraith, collapseTimer;
        BotInput bot;
        float autoTimer, autoDeadline;
        bool shotTaken;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            Time.timeScale = AutoTest.Active ? AutoTest.TimeScale : 1f;
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1f);
            var run = RunSession.Current;
            bool freshRun = run == null || (run.stageIndex == 0 && run.totalTime <= 0f && !run.isTutorial);
            if (run == null)
            {
                // Game scene started directly: debug options of this object, or a "play this room" request from the Room Template Editor.
                bool playtest = EditorPlaytest.Active;
                int seed = playtest ? EditorPlaytest.Seed : (debugSeed != 0 ? debugSeed : RunSession.RandomSeed());
                run = RunSession.NewRun(seed, !playtest && debugStartTutorial, config.player);
                // a play-test or debug start names a stage of the catalogue: this run walks the whole catalogue in order
                RunPlan.UseWholeCatalogue(run, config);
                run.stageIndex = Mathf.Clamp(playtest ? EditorPlaytest.StageIndex : debugStartStage, 0, config.stages.Length - 1);
                run.inVillage = false;
            }
            if (run.inVillage && (config.village == null || config.village.handcrafted == null || run.isTutorial || run.bossPractice)) run.inVillage = false;
            // every run has its own plan: which areas, in which order (drawn by the seed)
            RunPlan.Ensure(run, config);
            int at = Mathf.Clamp(run.stageIndex, 0, run.stageOrder.Length - 1);
            Stage = run.inVillage ? config.village : run.isTutorial ? config.tutorial : config.stages[Mathf.Clamp(run.stageOrder[at], 0, config.stages.Length - 1)].Named(run.stageNames[at]);
            // (the scene after a run that was won is played in the village at night, whatever the hour: EndingScene)
            bool ending = EndingScene.Pending && run.inVillage && !ScenarioRunner.Active && !AutoTest.Active;
            bool trueEnd = ending && EndingScene.PendingTrue;      // (the morning after the stone is broken: by day, and no snow)
            if (run.inVillage) Stage = VillageClock.For(Stage, trueEnd ? 12 : ending ? 23 : VillageClock.Hour);      // day or night there, by the player's own clock
            if (trueEnd && Stage.area != null && Stage.area != config.village.area) Stage.area.ambientParticles = AmbientParticles.Pollen;
            run.levelTime = 0f;
            run.goldAtLevelStart = run.gold;
            run.reachedStageName = Stage.displayName;

            if (ScenarioRunner.Active)
            {
                Stage = config.tutorial;
                run.health = 4; run.maxHealth = 10; run.bombs = 4; run.ropes = 4;
                Level = ScenarioRunner.BuildLevel();
                Time.timeScale = 3f;
            }
            else Level = LevelFactory.Create(config, Stage, run.seed, run.inVillage ? 998 : run.isTutorial ? 999 : run.stageIndex);
            world.Setup(Level, Stage.area);
            if (sky != null)
            {
                sky.gameObject.SetActive(Stage.area != null && Stage.area.outdoor);
                sky.Setup(Level.Height);
                sky.SetTint(Stage.area != null ? Stage.area.skyTint : Color.white);
            }
            if (caveBackdrop != null)
            {
                bool indoor = Stage.area == null || !Stage.area.outdoor;
                caveBackdrop.gameObject.SetActive(indoor);
                if (indoor && Stage.area != null) caveBackdrop.SetTint(Stage.area.backdropTint);
            }
            if (LevelLighting.Instance != null) LevelLighting.Instance.Setup(Level, Stage.area);
            if (Ambience.Instance != null) Ambience.Instance.Setup(Stage.area);
            bool grantedGear = false;
            string chestGear = null;       // gear this area asks for and the run has not got: it waits in a chest by the gate
            if (Stage.area != null && !string.IsNullOrEmpty(Stage.area.grantGear) && !run.gear.Contains(Stage.area.grantGear) && config.FindGear(Stage.area.grantGear) != null)
            {
                // (the test bots cannot open a chest: they are handed it, as everybody used to be)
                if (ScenarioRunner.Active || AutoTest.Active) { run.gear.Add(Stage.area.grantGear); grantedGear = true; }
                else chestGear = Stage.area.grantGear;
            }
            // a saved game that goes on in an arena whose boss has already fallen: the arena is empty, the gate open
            if (Stage.kind == LevelKind.Boss && run.bossDownStage == run.stageIndex && !run.bossPractice)
                Level.spawns.RemoveAll(sp => config.prefabs.Get(sp.id) is Boss);
            SaveSystem.Checkpoint(run, Stage.displayName);      // the game is saved at the start of every level
            LevelFactory.SpawnAll(world, Level, config.prefabs);
            Story.Populate(world, Level, Stage, run, config);       // the old pillars, and - once they are read - the parts of the hammer
            QuestLog.Use(config.quests, !run.isTutorial && !run.bossPractice);      // (quests begin once the tutorial is behind; a Challenge fight is not part of the story)
            if (run.inVillage)
            {
                // people in the street, children at play: a full street by day, a few at dawn and dusk, and at night
                // only a couple of grown-ups still about (the children are in bed)
                var part = VillageClock.PartOf(VillageClock.Hour);
                if (ending) Townsfolk.Populate(world, 8, trueEnd ? 4 : 3);        // (the whole village is out: to see the stone come home, or the morning)
                else Townsfolk.Populate(world, part == VillageClock.Part.Day ? 8 : part == VillageClock.Part.Night ? 2 : 5,
                                               part == VillageClock.Part.Day ? 4 : part == VillageClock.Part.Night ? 0 : 2);
            }
            // the level after this one is built in the background while this one is played
            // (in the village that is the first level of the run)
            int aheadAt = run.inVillage ? at : at + 1;
            if (!ScenarioRunner.Active && !run.isTutorial && run.stageOrder != null && aheadAt < run.stageOrder.Length)
                LevelFactory.Pregenerate(config, config.stages[Mathf.Clamp(run.stageOrder[aheadAt], 0, config.stages.Length - 1)].Named(run.stageNames[aheadAt]), run.seed, run.inVillage ? run.stageIndex : run.stageIndex + 1);
            int spawnCount = world.EntityCount;
            var spawnAt = new Vector2(Level.entrance.x + 0.5f, Level.entrance.y);
            if (trueEnd && VillageClock.Street(world.Grid, out var square)) spawnAt = square;
            else if (ending && EndingScene.Entrance(world.Grid, out var comeHome)) spawnAt = comeHome;
            else if (run.inVillage && VillageClock.testHour >= 0 && VillageClock.Street(world.Grid, out var street)) spawnAt = street;      // (looking at the village from the command line)
            else if (run.inVillage && !run.wakeInBed)
            {
                // from the menu the player stands downstairs at home, a few steps from the door (the level's own
                // entrance is the bed upstairs: that is where one wakes after a night that ended badly)
                foreach (var sp in Level.spawns)
                    if (sp.id == "interior" && sp.param == VillageBuilding.HomeOfPlayer) { spawnAt = new Vector2(sp.cell.x + 4.5f, sp.cell.y); break; }
            }
            Player = world.Spawn(config.prefabs.player, spawnAt);
            if (chestGear != null && Chest.SpawnGearChest(world, chestGear, spawnAt) == null) { run.gear.Add(chestGear); grantedGear = true; chestGear = null; }      // (no room for a chest: handed over)
            world.Player = Player;
            Player.input = input;
            Player.godMode = debugGodMode || (EditorPlaytest.Active && EditorPlaytest.GodMode);
            cameraFollow.SetTarget(Player, new Rect(0f, 0f, Level.Width, Level.Height), config.cameraOrthoSize);
            if (cam != null && Stage.area != null) cam.backgroundColor = Stage.area.cameraBackground;

            foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None)) if (d.isExit) exitDoor = d;
            // (the test bot walks a hall whose keeper is not there: nobody will drop the stone for it)
            if (Stage.kind == LevelKind.Final && exitDoor != null && !(AutoTest.Active && !Level.goal.HasValue)) exitDoor.SetLocked(true);
            if (Stage.kind == LevelKind.Boss && BossAlive && exitDoor != null)
            {
                exitDoor.lockedPrompt = "Sealed. It opens when the boss falls.";
                exitDoor.SetLocked(true);
            }
            linkPending = true;
            nextWraith = config.pressureSpawnTime + run.extraTime;

            if (AudioManager.Instance != null && Stage.area != null)
                AudioManager.Instance.PlayMusic(Stage.kind == LevelKind.Final || Stage.kind == LevelKind.Boss ? "final" : Stage.area.musicId);

            hud.Bind(this);
            var gateGear = Level.gateCount > 0 ? config.FindGear(Level.gateGear) : null;
            // Area introduction: just the name of the place, and only when a new place begins - the first level
            // of an area, the camp, the final vault. Later levels and boss arenas start without a card.
            bool newPlace = Stage.kind == LevelKind.Area ? Stage.levelNumber == 1 : Stage.kind != LevelKind.Boss;
            if (newPlace)
                if (run.inVillage) { if (!ending) VillageBuilding.NewVisit(); }      // the village names itself when the player steps out of the house
                else hud.ShowAreaIntro(Stage.kind == LevelKind.Area ? Stage.area.displayName : Stage.displayName);
            if (gateGear != null && !ScenarioRunner.Active && !AutoTest.Active) SupplyCache(gateGear, run);
            if (Level.grip < 1f && Stage.levelNumber == 1 && !grantedGear)
                hud.ShowMessage("The ground is frozen: you slide before you stop. Slow down before a ledge - hold [walk] to walk.", 7f);
            if (run.isTutorial) { tutorialChestOpened = false; tutorialBookRead = false; }
            if (run.isTutorial && !ScenarioRunner.Active && !AutoTest.Active)
                hud.ShowMessage("The signs along the way say what every button does.  Know it already?  [pause], then SKIP TUTORIAL.", 9f);
            if (chestGear != null)
                hud.ShowMessage("A chest stands by the gate. This place asks for what is in it: open it before you go on.", 7f);
            if (grantedGear)
            {
                var g = config.FindGear(Stage.area.grantGear);
                hud.ShowMessage($"New gear: {g.displayName.ToUpperInvariant()}!  Hold [gear] to equip it.  {g.description}", 7f);
                Sfx.PlayUI("idol");
            }
            string line = $"seed {run.seed} stage {Stage.displayName} levelSeed {Level.seed} attempts {Level.attempts} ok={Level.validation?.ok}";
            Debug.Log("[Game] " + line);
            SaveSystem.AppendRunLog(line);

            if (ending) { StartCoroutine(trueEnd ? EndingScene.PlayTrue(this) : EndingScene.Play(this)); return; }
            // waking in bed after a night that ended - won or lost - is the same day again, and only Brann knows it:
            // a line says so, and the first death of a save gets a short scene of its own (EndingScene)
            var lastNight = EndingScene.Last;
            EndingScene.Last = EndingScene.Night.None;
            if (run.inVillage && run.wakeInBed && !ScenarioRunner.Active && !AutoTest.Active)
            {
                if (EndingScene.FirstDeathDue(lastNight)) StartCoroutine(EndingScene.PlayWaking(this));
                else { string words = EndingScene.WakingWords(lastNight, SaveSystem.Totals.deaths); if (words.Length > 0) hud.ShowMessage(words, 9f); }
            }
            if (ScenarioRunner.Active) { StartCoroutine(ScenarioRunner.RunCurrent(this)); return; }
            if (AutoTest.Active)
            {
                if (freshRun && run.stageIndex == 0) AutoTest.CheckFreshRun(run, config.player, world, spawnCount);
                SetupBot();
            }
        }

        // ------------------------------------------------------------------ autotest bot

        /// <summary>
        /// Anti soft-lock: if the player does not carry enough of the required gear for this level's gates,
        /// a small supply cache appears next to the entrance.
        /// </summary>
        void SupplyCache(EquipmentDef gear, RunState run)
        {
            if (!run.gear.Contains(gear.id)) run.gear.Add(gear.id);
            int have = run.Ammo(gear.id);
            int need = Level.gateCount;
            if (have < 0 || have >= need) return;   // unlimited gear (glider) or enough ammo
            if (string.IsNullOrEmpty(gear.supplyPickupId)) return;
            var p = world.SpawnById(gear.supplyPickupId, new Vector2(Level.entrance.x + 1.5f, Level.entrance.y)) as Pickup;
            if (p != null) p.value = need - have;
            string what = string.IsNullOrEmpty(gear.ammoLabel) ? gear.displayName.ToLowerInvariant() : gear.ammoLabel;
            hud.ShowMessage($"A supply cache: {need - have} {what} for the obstacles ahead.", 5f);
        }

        /// <summary>Autotest: apply the gear exactly where the validator assumed it (ropes hooked at rope spots, rubble cleared).</summary>
        void ApplyGatesForBot()
        {
            if (Level.gateCount <= 0) return;
            var run = RunSession.Current;
            if (!run.gear.Contains(Level.gateGear)) run.gear.Add(Level.gateGear);
            var gateBehaviour = GearRegistry.Get(Level.gateGear);
            if (gateBehaviour != null && gateBehaviour.AffectsMovement) run.Equip(Level.gateGear);
            var g = world.Grid;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.Get(x, y) == TileType.Rubble) world.DestroyTile(x, y, false);
            var prefab = config.prefabs.Get("rope") as RopeProjectile;
            foreach (var s in Level.ropeSpots)
            {
                int anchor = RopeProjectile.SimulateThrow(g, s.x, s.y, config.player.ropeThrowHeight);
                if (anchor < 0) continue;
                var r = world.Spawn(prefab, new Vector2(s.x + 0.5f, s.y));
                if (r == null) continue;
                r.DeployDown(s.x, anchor);
                r.SetLife(1e6f);
            }
        }

        void SetupBot()
        {
            // The validator plans without movement gear (glider) unless the level's gates need it: unequip it otherwise,
            // or holding jump while falling would glide and the replay would drift off the planned route.
            var r = RunSession.Current;
            var equippedGear = GearRegistry.Get(r.EquippedId);
            if (equippedGear != null && equippedGear.AffectsMovement && !(Level.gateCount > 0 && Level.gateGear == r.EquippedId))
                foreach (var id in r.gear)
                {
                    var other = GearRegistry.Get(id);
                    if (other == null || !other.AffectsMovement) { r.Equip(id); break; }
                }
            ApplyGatesForBot();
            var opts = LevelValidator.DefaultOptions(config.player);
            List<FrameInput> plan = null;
            if (Stage.kind == LevelKind.Final) plan = LevelValidator.PlanFinal(Level, opts, out _);
            else if (Stage.kind == LevelKind.Area || Stage.kind == LevelKind.Boss || Stage.kind == LevelKind.Rest) plan = LevelValidator.PlanToExit(Level, opts);
            Player.godMode = true;
            if (plan == null)
            {
                AutoTest.LevelResult(Run.seed, Stage.displayName, false, "validator found no plan");
                StartCoroutine(AutoAdvance());
                return;
            }
            bot = new BotInput(plan);
            Player.bot = bot;
            autoDeadline = plan.Count * GameConst.Dt + 6f;
        }

        IEnumerator AutoAdvance()
        {
            yield return null;
            LevelComplete();
        }

        void AutoTestTick(float dt)
        {
            if (!AutoTest.Active || bot == null || Ended) return;
            autoTimer += dt;
            if (AutoTest.Screenshots && !shotTaken && autoTimer > 0.6f)
            {
                shotTaken = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(AutoTest.OutDir, $"shot_{Run.seed}_{Stage.displayName}.png"));
            }
            if (autoTimer > autoDeadline)
            {
                AutoTest.LevelResult(Run.seed, Stage.displayName, false,
                    $"bot did not reach the exit (frame {bot.Index}/{bot.Count}, player at {Player.pos}, exit {Level.exit})");
                bot = null;
                LevelComplete();
            }
        }

        // ------------------------------------------------------------------ loop

        // "-shotEvery N dir" on the command line: a picture of the game every N rendered frames, for looking at a real
        // fight afterwards (with -bossTest). The game quits by itself after 'shotMax' pictures.
        static int shotEvery = -1, shotCount;
        static string shotDir;
        const int shotMax = 110;

        void DebugShots()
        {
            if (shotEvery < 0)
            {
                shotEvery = 0;
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 2 < args.Length; i++)
                    if (args[i] == "-shotEvery" && int.TryParse(args[i + 1], out int n) && n > 0) { shotEvery = n; shotDir = args[i + 2]; }
            }
            if (shotEvery == 0 || Time.frameCount % shotEvery != 0) return;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, $"shot_{shotCount:000}.png"));
            if (++shotCount > shotMax) QuitGame();
        }

        void Update()
        {
            DebugShots();
            if (EndingScene.Playing) return;        // (the scene listens to the buttons itself)
            if (linkPending && world.Frame > 0) { LevelFactory.LinkShops(world, Level); linkPending = false; }
            // a new item's card is up: the game waits, and any of the usual buttons puts it away
            if (hud != null && hud.CardModal)
            {
                if (input != null && (input.ConfirmPressed || input.PausePressed || input.BackPressed)) { hud.DismissCard(); input.ClearLatches(); }       // (the press that puts a card away is nobody else's: not a dash, not a jump)
                return;
            }
            // a villager's shop: Pause or a gamepad's back button shuts it (its own Close button too)
            if (villageShop != null && villageShop.IsOpen)
            {
                if (Ended || input == null) villageShop.Close();
                else if (input.PausePressed || input.BackPressed) villageShop.Back();      // (from a list to the choices, from the choices out)
                return;
            }
            // the Field Guide: Pause, the bag's button or a gamepad's back button shuts it
            if (codex != null && codex.IsOpen)
            {
                if (Ended || input == null || input.InventoryPressed || input.PausePressed || input.BackPressed) codex.Close();
                return;
            }
            // the bag: its own button opens it; the same button, Pause, or a gamepad's back button closes it
            if (inventory != null && !Paused)
            {
                if (inventory.IsOpen)
                {
                    if (Ended || input == null || input.InventoryPressed || input.PausePressed || input.BackPressed) inventory.Close();
                    return;
                }
                if (!Ended && !AutoTest.Active && input != null && input.InventoryPressed && Player != null && !Player.IsDead &&
                    (gearWheel == null || !gearWheel.IsOpen))
                {
                    inventory.Open();
                    return;
                }
            }
            // (the pause button first puts down a note or words that are up, as "back" would)
            if (!Ended && !AutoTest.Active && input != null && input.PausePressed && hud != null && (hud.HeldUp || hud.SignUp))
            {
                if (hud.TalkUp) hud.AdvanceTalk(); else if (hud.NoteUp) hud.DismissHeld(); else hud.HideSign();
            }
            else if (!Ended && !AutoTest.Active && input != null && input.PausePressed && (gearWheel == null || !gearWheel.IsOpen))
            {
                if (Paused) { if (!pauseMenu.HandleBack()) SetPaused(false); }
                else SetPaused(true);
            }
            // a gamepad's back button does what Esc does on the pause screen: out of the panel that is open (settings,
            // keys, the boss list, the "give up?" question), and from the pause screen itself back into the game
            else if (Paused && !Ended && input != null && input.BackPressed)
            {
                if (!pauseMenu.HandleBack()) SetPaused(false);
            }
            if (Paused || Ended) return;
            float dt = Time.deltaTime;
            var run = Run;
            if (Player != null && !Player.IsDead && !run.inVillage)       // the walk through the village is not on the clock
            {
                run.levelTime += dt;
                run.totalTime += dt;
            }
            Pressure();
            Collapse(dt);
            AutoTestTick(dt);
        }

        void Pressure()
        {
            var run = Run;
            if (!config.pressureEnabled || run.isTutorial || AutoTest.Active || Player == null || Player.IsDead) return;
            if (Stage.kind == LevelKind.Boss || Stage.kind == LevelKind.Rest) return;   // no clock in a boss fight or at the camp
            if (!warned && run.levelTime >= config.pressureWarnTime + run.extraTime)
            {
                warned = true;
                hud.ShowBanner("You have lingered too long...", "Death is coming for you. Find the exit!");
                Sfx.PlayUI("warning");
            }
            if (run.levelTime >= nextWraith)
            {
                nextWraith += Mathf.Max(10f, config.pressureRepeat);
                SpawnWraith();
            }
        }

        public void SpawnWraith()
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            int side = Random.value < 0.5f ? -1 : 1;
            var p = Player.Center + new Vector2(side * (w + 1.5f), Random.Range(-h * 0.5f, h * 0.5f));
            p.x = Mathf.Clamp(p.x, 1f, Level.Width - 1f);
            p.y = Mathf.Clamp(p.y, 1f, Level.Height - 1f);
            world.SpawnById("wraith", p);
            wraiths++;
            hud.ShowBanner("THE REAPER HAS COME", "It passes through walls. Do not let it touch you!");
            Sfx.PlayUI("wraith");
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(0.85f);
        }

        void Collapse(float dt)
        {
            if (!collapsing || Player == null || Player.IsDead) return;
            collapseTimer -= dt;
            if (collapseTimer > 0f) return;
            collapseTimer = config.collapseInterval * Random.Range(0.6f, 1.4f);
            var g = world.Grid;
            float h = cam.orthographicSize;
            int x = Mathf.Clamp(Mathf.FloorToInt(Player.pos.x + Random.Range(-9f, 9f)), 1, g.Width - 2);
            int top = Mathf.Min(g.Height - 2, Mathf.FloorToInt(cam.transform.position.y + h + 1f));
            for (int y = top; y > 1; y--)
            {
                if (g.IsEmpty(x, y) && g.IsSolid(x, y + 1))
                {
                    var prefab = config.prefabs.Get("stalactite");
                    world.Spawn(prefab, new Vector2(x + 0.5f, y), new SpawnPoint("stalactite", new Vector2Int(x, y), 0, 1));
                    break;
                }
            }
            Fx.Shake(0.12f);
            Sfx.PlayUI("rumble", 0.5f);
        }

        // ------------------------------------------------------------------ events

        bool stoneLifted;

        public void OnHeartstoneLifted()
        {
            if (collapsing || stoneLifted) return;
            stoneLifted = true;
            // A vault with the stone on an altar ('Y' in its map) comes down once the stone is lifted. In the Eclipse
            // King's hall the stone was in his chest: the fight was the price, and the gate simply opens.
            if (Level == null || !Level.goal.HasValue)
            {
                if (exitDoor != null) exitDoor.SetLocked(false);
                hud.ShowBanner("THE SUNSTONE IS YOURS", "Carry it out through the Surface Gate.", 4.5f);
                Sfx.PlayUI("idol");
                return;
            }
            collapsing = true;
            collapseTimer = 1.2f;
            if (exitDoor != null) exitDoor.SetLocked(false);
            hud.ShowBanner("THE VAULT IS COLLAPSING!", "Carry the Sunstone back up to the Surface Gate!");
            Sfx.PlayUI("collapse");
            Fx.Shake(0.5f);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1.12f);
        }

        public void RegisterBoss(Boss b) { Boss = b; }

        public void OnBossDefeated(Boss b)
        {
            if (b != Boss) return;
            bool final = Stage != null && Stage.kind == LevelKind.Final;
            if (!final && exitDoor != null) exitDoor.SetLocked(false);
            if (Run != null && !Run.bossPractice && !final)
            {
                Run.bossDownStage = Run.stageIndex;
                SaveSystem.CheckpointFlags(Run);
            }
            if (Run != null && Run.bossPractice)
            {
                hud.ShowBanner($"{b.displayName.ToUpperInvariant()} DEFEATED", "", 2.2f);
                Sfx.PlayUI("victory");
                if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1f);
                PracticeEnding = true;
                practiceBoss = b.displayName;
                practiceTime = Run.levelTime;
                if (Player != null) Player.godMode = true;         // nothing left in the air may spoil the win
                StartCoroutine(EndPractice(2.2f));
                return;
            }
            // (with the hammer in the bag there are two things to do with the stone, and the screen says so)
            bool hammer = final && Run != null && Run.gear.Contains(Story.HammerGear);
            hud.ShowBanner($"{b.displayName.ToUpperInvariant()} DEFEATED",
                hammer ? "The Sunstone lies where he fell. Lift it - or take the hammer to it."
                : final ? (Level != null && Level.goal.HasValue ? "The Sunstone is yours. Lift it and run!" : "The Sunstone lies where he fell. Lift it.") : "The way on is open.", hammer ? 6.5f : 4.5f);
            Sfx.PlayUI("victory");
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1f);
        }

        /// <summary>A boss test was won: the game is on its way back to the boss list.</summary>
        public bool PracticeEnding { get; private set; }

        string practiceBoss = "";
        float practiceTime;

        // A moment to watch the boss fall, then the result screen - it stays until the player chooses.
        IEnumerator EndPractice(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ShowPracticeResult();
        }

        void ShowPracticeResult()
        {
            if (Ended) return;
            Ended = true;
            if (!ScenarioRunner.Active) world.enabled = false;      // tests keep their arena running
            Run.finished = true;
            bool flawless = Run.damageTaken == 0;
            // tests fight in the player's save folder: they compare but never write
            SaveSystem.RecordBossWin(practiceBoss, practiceTime, flawless, !ScenarioRunner.Active && !AutoTest.Active, out bool newBest, out bool firstFlawless);
            var rec = SaveSystem.BossRecordOf(practiceBoss);
            results.ShowBossTest(Run, practiceBoss, practiceTime, newBest, firstFlawless, rec != null ? rec.bestTime : practiceTime, rec != null && rec.noDamage);
        }

        /// <summary>Challenge, from the pause screen: leaves this fight for another boss (or the same one) without going out to the menu.</summary>
        public void SwitchBoss(int catalogueStage)
        {
            if (Run == null || !Run.bossPractice || catalogueStage < 0 || catalogueStage >= config.stages.Length) return;
            Time.timeScale = 1f;
            RunSession.NewBossPractice(catalogueStage, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        public void BackToBossList()
        {
            Time.timeScale = 1f;
            MainMenuController.OpenBossListOnLoad = true;
            RunSession.Clear();
            SceneManager.LoadScene(GameConst.SceneMenu);
        }

        public void OnPlayerHurt(in DamageInfo d, string description)
        {
            hud.ShowHurt(description, d.amount);
        }

        public void ShowMessage(string msg) => hud.ShowMessage(msg);

        public void OnPlayerEnterExit(PlayerController p, Door door)
        {
            if (Ended || p.IsDead) return;
            var run = Run;
            if (run.isTutorial && !ScenarioRunner.Active && !AutoTest.Active && !(tutorialChestOpened && tutorialBookRead))
            {
                // the last lesson is not done yet
                hud.ShowMessage(!tutorialChestOpened
                    ? "The gate stays shut.  Open the chest first: [enter]."
                    : "One thing more.  Open your bag with [inventory], go to Key items and read the Field Guide.  Then the gate opens.", 6f);
                Sfx.Play("error", p.pos);
                return;
            }
            if (run.inVillage && Villagers.Enabled && QuestLog.GateHeld(out string held))
            {
                // an open quest keeps the gate shut (the very first one: the elder wants a word)
                hud.ShowMessage(held);
                Sfx.Play("error", p.pos);
                return;
            }
            if (run.inVillage)
            {
                // out of the village gate: the night on the mountain begins (the first level was built meanwhile)
                // ... through the same short fade as a house door, not a hard cut
                if (ScreenFade.Busy) return;
                Sfx.Play("land", p.pos);
                ScreenFade.Through(() =>
                {
                    run.inVillage = false;
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(GameConst.SceneGame);
                }, 0.22f, 0.18f, 0.35f);
                return;
            }
            if (run.bossPractice && !BossAlive) { ShowPracticeResult(); return; }      // boss test: nothing beyond the arena
            if (Stage.kind == LevelKind.Boss && door.locked)
            {
                hud.ShowMessage("The gate is sealed until the boss falls!");
                Sfx.Play("error", p.pos);
                return;
            }
            if (Stage.kind == LevelKind.Final)
            {
                if (!(p.held is Treasure t && t.isHeartstone) && !(AutoTest.Active && !Level.goal.HasValue))
                {
                    hud.ShowMessage("The Surface Gate only opens for the Sunstone!");
                    Sfx.Play("error", p.pos);
                    return;
                }
                Victory();
                return;
            }
            if (p.held is Treasure tr && !tr.isHeartstone)
            {
                run.gold += tr.value;
                hud.ShowMessage($"Golden idol sold for ${tr.value}!");
                Sfx.PlayUI("buy");
            }
            LevelComplete();
        }

        void LevelComplete()
        {
            if (Ended) return;
            Ended = true;
            var run = Run;
            world.enabled = false;
            Sfx.PlayUI("door");
            if (Player != null) Player.gameObject.SetActive(false);
            if (AutoTest.Active && bot != null) AutoTest.LevelResult(run.seed, Stage.displayName, true, $"{run.levelTime:0.0}s game time");
            if (run.isTutorial)
            {
                run.finished = true;
                SaveSystem.MarkTutorialDone();      // the next "Start" of this save goes to the village
                Codex.Give();                        // (walked past the chest: the book comes along all the same)
                results.ShowTutorialComplete(run);
                return;
            }
            run.levelsCompleted++;
            transition.Show(Stage, run, run.gold - run.goldAtLevelStart, AutoTest.Active ? 0.1f : 0f, ContinueToNextStage);
        }

        void ContinueToNextStage()
        {
            var run = Run;
            run.stageIndex++;
            if (run.stageIndex >= run.stageOrder.Length)
            {
                // Should not happen (the final stage ends with Victory) – treat as victory.
                Victory();
                return;
            }
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void Victory()
        {
            if (Ended && Run.finished) return;
            Ended = true;
            world.enabled = false;
            var run = Run;
            run.victory = true;
            run.finished = true;
            run.gold += config.heartstoneBonusGold;
            Sfx.PlayUI("victory");
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1f);
            if (AutoTest.Active)
            {
                AutoTest.LevelResult(run.seed, Stage.displayName, true, "VICTORY reached");
                NextAutoRun();
                return;
            }
            string news = SaveSystem.RecordRunEnd(run);
            SaveSystem.AppendRunLog($"seed {run.seed} VICTORY time {run.totalTime:0.0} gold {run.gold}");
            // the stone is carried home first (EndingScene); the run's numbers come at the end of that
            if (EndingScene.Begin(this, run, news)) return;
            results.ShowRunEnd(run, true, news, run.stageOrder.Length);
        }

        /// <summary>
        /// The hammer has come down on the Sunstone, its keeper fallen: the stone breaks. This is the real end of
        /// the game - the run is won, the loop is over (Story.KeyTrueEnd), and the morning after is shown.
        /// </summary>
        public void BreakSunstone(Treasure stone)
        {
            if (Ended || Stage == null || Stage.kind != LevelKind.Final || BossAlive || Run == null || stone == null) return;
            Ended = true;
            var run = Run;
            run.victory = true;
            run.finished = true;
            if (Player != null) { Player.godMode = true; if (Player.held == stone) Player.held = null; }
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicPitch(1f);
            string news = SaveSystem.RecordRunEnd(run);
            SaveSystem.AppendRunLog($"seed {run.seed} TRUE END time {run.totalTime:0.0}");
            Villagers.GiveKey(Story.KeyTrueEnd);
            if (EndingScene.BeginTrue(this, run, news, stone)) return;
            world.enabled = false;
            results.ShowTrueEnd(run, news);
        }

        /// <summary>The end of the morning after: the numbers of the run that broke the stone.</summary>
        public void ShowTrueEnd(RunState won, string news)
        {
            Ended = true;
            world.enabled = false;
            if (won == null) { QuitToMenu(); return; }
            results.ShowTrueEnd(won, news);
        }

        /// <summary>From the real end's numbers: the credits, then the title screen.</summary>
        public void RollCredits()
        {
            if (EndingScene.Playing) return;
            StartCoroutine(EndingScene.Credits(this));
        }

        /// <summary>The end of the scene in the village: the numbers of the run that was won (not of the run the scene was played on).</summary>
        public void ShowWonRun(RunState won, string news)
        {
            Ended = true;
            world.enabled = false;
            if (won == null) { RestartRun(false); return; }
            results.ShowRunEnd(won, true, news, won.stageOrder != null ? won.stageOrder.Length : 0);
        }

        public void OnPlayerDied(string cause)
        {
            var run = Run;
            run.deathCause = cause;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.DuckMusic(0.7f);
            yield return new WaitForSecondsRealtime(1.6f);
            if (Ended || ScenarioRunner.Active) yield break;
            Ended = true;
            var run = Run;
            run.finished = true;
            if (run.isTutorial)
            {
                hud.ShowMessage("Ouch! Try again.");
                yield return new WaitForSecondsRealtime(0.6f);
                RunSession.NewRun(run.seed, true, config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
                yield break;
            }
            if (run.bossPractice)
            {
                // boss test: straight back into the same fight
                hud.ShowMessage("Again!");
                yield return new WaitForSecondsRealtime(0.6f);
                RunSession.NewBossPractice(run.stageOrder[0], config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
                yield break;
            }
            if (AutoTest.Active)
            {
                AutoTest.LevelResult(run.seed, Stage.displayName, false, "player died: " + run.deathCause);
                NextAutoRun();
                yield break;
            }
            string news = SaveSystem.RecordRunEnd(run);
            EndingScene.Last = EndingScene.Night.Died;      // (the next waking in bed knows what it follows)
            SaveSystem.AppendRunLog($"seed {run.seed} DIED at {Stage.displayName}: {run.deathCause}");
            results.ShowRunEnd(run, false, news, run.stageIndex);
        }

        void NextAutoRun()
        {
            if (!AutoTest.NextSeed()) { AutoTest.Finish(); return; }
            RunSession.NewRun(AutoTest.CurrentSeed, false, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        // ------------------------------------------------------------------ menu actions

        public void SetPaused(bool p)
        {
            if (Ended && p) return;
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            if (p) { pauseMenu.Show(Run); Sfx.PlayUI("menu_select"); }
            else pauseMenu.Hide();
            input?.ClearLatches();
        }

        /// <summary>
        /// A new run. toVillage: wake in bed at home in Brindlemoor (the default); otherwise the run begins at once in
        /// the first level, behind the loading screen - the "new run" of the screen shown after a death.
        /// </summary>
        public void RestartRun(bool sameSeed, bool toVillage = true)
        {
            if (ScreenFade.Busy) return;        // already on the way home
            var old = Run;
            Time.timeScale = 1f;
            if (old != null && old.bossPractice)        // in a boss test "new run" means: this boss again
            {
                RunSession.NewBossPractice(old.stageOrder[0], config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
                return;
            }
            RunSession.NewRun(sameSeed && old != null ? old.seed : RunSession.RandomSeed(), false, config.player);
            if (!toVillage && RunSession.Current.inVillage)
            {
                if (LoadingScreen.Busy) return;
                RunSession.Current.inVillage = false;
                LoadingScreen.Load(GameConst.SceneGame);
                return;
            }
            RunSession.Current.wakeInBed = true;        // the night is over: wake in bed
            if (ScenarioRunner.Active || AutoTest.Active) { SceneManager.LoadScene(GameConst.SceneGame); return; }
            // the screen goes dark slowly, and it is morning in Brindlemoor when it clears
            ScreenFade.Through(() => SceneManager.LoadScene(GameConst.SceneGame), 0.7f, 0.45f, 1.1f);
        }

        /// <summary>The tutorial's last lesson: its gate stays shut until the chest has been opened and the Field Guide read from the bag.</summary>
        public static bool tutorialChestOpened, tutorialBookRead;

        /// <summary>Out of the bag and into the Field Guide.</summary>
        public void OpenCodex()
        {
            if (codex == null || !Codex.Owned) return;
            if (inventory != null) inventory.Close();
            if (villageShop != null) villageShop.Close();
            codex.Open();
            tutorialBookRead = true;
        }

        /// <summary>The tutorial's pause screen: enough of this - on to the village. The book is handed over as if it had been found.</summary>
        public void SkipTutorial()
        {
            if (Run == null || !Run.isTutorial || LoadingScreen.Busy) return;
            SaveSystem.MarkTutorialDone();
            Codex.Give();
            Time.timeScale = 1f;
            RunSession.NewRun(RunSession.RandomSeed(), false, config.player);
            LoadingScreen.Load(GameConst.SceneGame);
        }

        /// <summary>A run on the mountain can be given up from the pause screen: not the village, the tutorial or a Challenge fight.</summary>
        public bool CanGiveUp => Run != null && !Ended && !Run.inVillage && !Run.isTutorial && !Run.bossPractice;

        /// <summary>
        /// The player gives the run up (pause screen, after being asked): it ends here as a run that was not won - no
        /// death is counted - and the player wakes at home in Brindlemoor, as after a death.
        /// </summary>
        public void GiveUp()
        {
            if (!CanGiveUp || ScreenFade.Busy) return;
            var run = Run;
            Ended = true;
            run.finished = true;
            Paused = false;
            pauseMenu.Hide();
            if (!ScenarioRunner.Active && !AutoTest.Active)
            {
                SaveSystem.RecordRunEnd(run, true);
                SaveSystem.AppendRunLog($"seed {run.seed} GIVEN UP at {Stage.displayName}");
            }
            RestartRun(false, true);
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            RunSession.Clear();
            SceneManager.LoadScene(GameConst.SceneMenu);
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
