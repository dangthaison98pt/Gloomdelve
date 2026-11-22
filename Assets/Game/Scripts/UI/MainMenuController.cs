using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class MainMenuController : MonoBehaviour
    {
        public GameConfig config;
        public GameObject mainPanel;
        public GameObject recordsPanel;
        public SettingsPanel settingsPanel;
        public Button startButton, tutorialButton, settingsButton, recordsButton, quitButton;
        public Button recordsBack;
        [Tooltip("Settings > Keys: the controls list, where keyboard keys can be changed.")] public KeyBindPanel keysPanel;
        public TMPro.TMP_Text recordsText;
        public TMPro.TMP_Text versionText;
        [Tooltip("The title, its subtitle and the story line: shown on the main list only, hidden behind a sub panel.")] public GameObject[] titleObjects;
        [Header("Boss test")]
        public Button bossButton, bossBack;
        public GameObject bossPanel;
        [Tooltip("One button per boss ...")] public Button[] bossButtons;
        [Tooltip("... and the stage of the run (GameConfig.stages index) each one starts.")] public int[] bossStages;
        [Tooltip("Display name of each boss (the key of its Challenge record).")] public string[] bossNames;
        [Tooltip("Best time / no-damage badge shown on each boss button.")] public TMPro.TMP_Text[] bossBest;
        [Tooltip("Records screen: the Challenge times of every boss.")] public TMPro.TMP_Text bossRecordsText;
        /// <summary>Set when a boss test ends: the menu opens on the boss list instead of the main panel.</summary>
        public static bool OpenBossListOnLoad;
        [Header("Save slots")]
        public GameObject slotPanel;
        [Tooltip("One button per save slot (Start Run asks which save to play).")] public Button[] slotButtons;
        public TMPro.TMP_Text[] slotTexts;
        [Tooltip("Erase button beside each slot: the first press asks, the second erases.")] public Button[] slotErase;
        public TMPro.TMP_Text[] slotEraseLabels;
        public Button slotBack;
        int eraseArmed = -1;

        void Start()
        {
            Time.timeScale = 1f;
            RunSession.Clear();         // (first: back from a Challenge fight, the menu shows the save again - SaveSystem.Apart)
            SaveSystem.EnsureLoaded();
            UIUtil.Wire(startButton, OpenSlots);
            UIUtil.Wire(slotBack, ShowMain);
            if (slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int slot = i;
                    UIUtil.Wire(slotButtons[i], () => { SaveSystem.SelectSlot(slot); if (SaveSystem.SavedRun != null) ContinueRun(); else StartRun(); });
                    if (slotErase != null && i < slotErase.Length) UIUtil.Wire(slotErase[i], () => EraseSlot(slot));
                }
            UIUtil.Wire(tutorialButton, StartTutorial);
            UIUtil.Wire(settingsButton, () => Open(settingsPanel.gameObject, settingsPanel.First));
            UIUtil.Wire(recordsButton, () => { RefreshRecords(); Open(recordsPanel, recordsBack); });
            UIUtil.Wire(quitButton, GameDirector.QuitGame);
            UIUtil.Wire(recordsBack, ShowMain);
            UIUtil.Wire(bossButton, OpenBossList);
            UIUtil.Wire(bossBack, ShowMain);
            if (bossButtons != null)
                for (int i = 0; i < bossButtons.Length; i++)
                {
                    int stage = bossStages != null && i < bossStages.Length ? bossStages[i] : -1;
                    // the stage list may have been edited since the menu was built: only real boss stages are offered
                    bool ok = stage >= 0 && stage < config.stages.Length && (config.stages[stage].kind == LevelKind.Boss || config.stages[stage].kind == LevelKind.Final);
                    bossButtons[i].gameObject.SetActive(ok);
                    if (ok) UIUtil.Wire(bossButtons[i], () => StartBossPractice(stage));
                }
            settingsPanel.onBack = ShowMain;
            settingsPanel.onKeys = OpenKeys;
            if (keysPanel != null) keysPanel.onBack = () => Open(settingsPanel.gameObject, settingsPanel.keys != null ? settingsPanel.keys : settingsPanel.First);
            RefreshVersion();
            ShowMain();
            if (AudioManager.Instance != null) { AudioManager.Instance.SetMusicPitch(1f); AudioManager.Instance.PlayMusic("menu"); }
            if (OpenBossListOnLoad) { OpenBossListOnLoad = false; OpenBossList(); }
            // "-bossTest N" on the command line starts boss number N (1 = the first in the list) right away
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-bossTest" && !bossTestArgUsed && int.TryParse(args[i + 1], out int n) && bossStages != null && n >= 1 && n <= bossStages.Length)
                {
                    bossTestArgUsed = true;
                    StartBossPractice(bossStages[n - 1]);
                    return;
                }

            // "-endingTest": the scene that follows a run that was won, by itself (for looking at it)
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-endingTest" && !endingArgUsed) { endingArgUsed = true; EndingScene.BeginFromMenu(config); return; }
            // "-wakeTest": the scene of a save's first death - waking in bed - by itself (for looking at it)
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-wakeTest" && !endingArgUsed)
                {
                    endingArgUsed = true;
                    EndingScene.ForceFirstDeath = true; EndingScene.Last = EndingScene.Night.Died;
                    RunSession.NewRun(12345, false, config.player).wakeInBed = true;
                    SceneManager.LoadScene(GameConst.SceneGame);
                    return;
                }
            // "-tutorialTest": straight into the tutorial, as a new game begins (for looking at it)
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-tutorialTest" && !tutorialArgUsed) { tutorialArgUsed = true; StartTutorial(); return; }
            // "-villageTest H": straight into the village street as it is at hour H (0-23), for looking at its light
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-villageTest" && VillageClock.testHour < 0 && int.TryParse(args[i + 1], out int vh) && vh >= 0)
                {
                    VillageClock.testHour = vh % 24;
                    for (int k = 0; k + 1 < args.Length; k++) if (args[k] == "-villageX" && float.TryParse(args[k + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float vx)) VillageClock.testX = vx;
                    RunSession.NewRun(12345, false, config.player);
                    SceneManager.LoadScene(GameConst.SceneGame);
                    return;
                }
            if (ScenarioRunner.Active) { StartCoroutine(ScenarioStart()); return; }
            if (LoadingScreen.ShotDir != null && !loadingShotUsed) { loadingShotUsed = true; Invoke(SaveSystem.SavedRun != null ? nameof(ContinueRun) : nameof(StartRun), 0.8f); return; }
            if (AutoTest.Active)
            {
                RunSession.NewRun(AutoTest.CurrentSeed, false, config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
            }
        }

        System.Collections.IEnumerator ScenarioStart()
        {
            // the tests look at the screens in a normal wide window, whatever shape the desktop left the last one in
            if (Screen.fullScreen || Screen.width < Screen.height * 1.5f || Screen.width > Screen.height * 2f)
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.6f);
            }
            // the snow over the title picture must be falling: where the flakes are now, and again a moment later
            var snow = FindFirstObjectByType<MenuSnow>();
            Vector2[] before = null;
            if (snow != null) { yield return null; before = new Vector2[snow.Count]; for (int i = 0; i < before.Length; i++) before[i] = snow.PositionOf(i); }
            yield return new WaitForSecondsRealtime(0.8f);
            if (snow != null)
            {
                int moved = 0, down = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    var now = snow.PositionOf(i);
                    if ((now - before[i]).sqrMagnitude > 1f) moved++;
                    if (now.y < before[i].y) down++;
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_snow.txt"), $"flakes={before.Length} moved={moved} movedDown={down}");
            }
            else System.IO.File.WriteAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_snow.txt"), "no MenuSnow in the scene");
            // the scarf animation must be running too: it goes through several frames in half a second
            {
                var scarf = FindFirstObjectByType<MenuSpriteAnim>();
                var seen = new System.Collections.Generic.HashSet<int>();
                for (float t0 = Time.unscaledTime; scarf != null && Time.unscaledTime - t0 < 0.6f;) { seen.Add(scarf.Frame); yield return null; }
                System.IO.File.AppendAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_snow.txt"),
                    scarf != null ? $" | scarf frames={scarf.frames.Length} seenIn0.6s={seen.Count}" : " | no scarf animation in the scene");
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_main.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            // every menu screen is checked for texts over one another or out of their frames
            var layout = new System.Text.StringBuilder();
            void Layout(string screen)
            {
                var faults = UIOverlap.Find();
                layout.AppendLine(screen + ": " + (faults.Count == 0 ? "clean" : faults.Count + " fault(s)"));
                foreach (var f in faults) layout.AppendLine("    " + f);
                System.IO.File.WriteAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_layout.txt"), layout.ToString());
            }
            Layout("main");
            // a (virtual) gamepad button here must switch the menu hints to gamepad buttons
            if (!ScenarioRunner.Filtered)       // a quick run of a few scenarios skips the menu pictures
            {
                var prevBg = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>("MenuPad");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,
                    new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftShoulder));
                yield return new WaitForSecondsRealtime(0.3f);
                Debug.Log("[Scenario] menu: gamepad button -> UsingGamepad = " + InputDeviceTracker.UsingGamepad);
                System.IO.File.WriteAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_pad.txt"), "UsingGamepad=" + InputDeviceTracker.UsingGamepad);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_pad.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
                yield return null;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
                InputDeviceTracker.ResetToKeyboard();
                // the same with a PlayStation pad: the hints must show cross / circle / square / triangle
                string psResult = "not created";
                UnityEngine.InputSystem.InputDevice ps = null;
                try { ps = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShockGamepad>("MenuPadPS"); }
                catch (System.Exception ex) { psResult = "could not create a virtual DualShock: " + ex.Message; }
                if (ps != null)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(ps,
                        new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftShoulder));
                    yield return new WaitForSecondsRealtime(0.3f);
                    psResult = "UsingGamepad=" + InputDeviceTracker.UsingGamepad + " PlayStation=" + InputDeviceTracker.PlayStation;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_ps.png"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(ps, new UnityEngine.InputSystem.LowLevel.GamepadState());
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(ps);
                    InputDeviceTracker.ResetToKeyboard();
                }
                System.IO.File.AppendAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_pad.txt"), "\nPS: " + psResult);
                // and with a Nintendo pad (recognised by its description): B / A / Y / X as printed on it
                string swResult = "not created";
                UnityEngine.InputSystem.InputDevice sw = null;
                try
                {
                    UnityEngine.InputSystem.InputSystem.RegisterLayoutMatcher<UnityEngine.InputSystem.Gamepad>(
                        new UnityEngine.InputSystem.Layouts.InputDeviceMatcher().WithManufacturer("Nintendo").WithProduct("Scenario Pro Controller"));
                    sw = UnityEngine.InputSystem.InputSystem.AddDevice(new UnityEngine.InputSystem.Layouts.InputDeviceDescription
                        { manufacturer = "Nintendo", product = "Scenario Pro Controller" });
                }
                catch (System.Exception ex) { swResult = "could not create a virtual Switch pad: " + ex.Message; }
                if (sw != null)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(sw,
                        new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftShoulder));
                    yield return new WaitForSecondsRealtime(0.3f);
                    swResult = "UsingGamepad=" + InputDeviceTracker.UsingGamepad + " Nintendo=" + InputDeviceTracker.Nintendo + " Kind=" + InputDeviceTracker.Kind;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_switch.png"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(sw, new UnityEngine.InputSystem.LowLevel.GamepadState());
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(sw);
                    InputDeviceTracker.ResetToKeyboard();
                }
                System.IO.File.AppendAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_pad.txt"), "\nSwitch: " + swResult);
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = prevBg;
            }
            OpenSlots();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_slots.png"));
            Layout("save slots");
            yield return new WaitForSecondsRealtime(0.3f);
            Open(settingsPanel.gameObject, settingsPanel.First);
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_settings.png"));
            Layout("settings");
            {
                // the mouse must reach every control of the settings screen: what lies under the pointer at the
                // middle of each one has to be that control (sliders and tick boxes included)
                var es = UnityEngine.EventSystems.EventSystem.current;
                var controls = new Selectable[] { settingsPanel.master, settingsPanel.music, settingsPanel.sfx, settingsPanel.fullscreen, settingsPanel.shake, settingsPanel.keys, settingsPanel.back };
                var corners = new Vector3[4];
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                string missed = "";
                foreach (var sel in controls)
                {
                    if (sel == null) continue;
                    ((RectTransform)sel.transform).GetWorldCorners(corners);
                    var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = (corners[0] + corners[2]) * 0.5f };
                    hits.Clear();
                    es.RaycastAll(ped, hits);
                    if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Selectable>() != sel) missed += " " + sel.name;
                }
                layout.AppendLine("settings, mouse: " + (missed.Length == 0 ? "every control can be clicked" : "CANNOT CLICK:" + missed));
                System.IO.File.WriteAllText(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_layout.txt"), layout.ToString());
            }
            yield return new WaitForSecondsRealtime(0.3f);
            OpenKeys();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_keys.png"));
            Layout("keys");
            yield return new WaitForSecondsRealtime(0.3f);
            OpenBossList();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_boss.png"));
            Layout("challenge");
            yield return new WaitForSecondsRealtime(0.3f);
            RefreshRecords(); Open(recordsPanel, recordsBack);
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_records.png"));
            Layout("records");
            yield return new WaitForSecondsRealtime(0.3f);
            RunSession.NewRun(1, true, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void Update()
        {
            // Esc, or a gamepad's back button (the right-hand face button), leaves the panel that is open
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || InputDeviceTracker.PadBackPressed;
            if (!back || mainPanel.activeSelf || KeyBindings.Busy) return;   // Busy: the press is cancelling a key change
            if (keysPanel != null && keysPanel.gameObject.activeSelf) keysPanel.onBack?.Invoke();                      // back to Settings
            else ShowMain();
        }

        /// <summary>The controls / change-keys screen (from the Settings panel).</summary>
        public void OpenKeys()
        {
            if (keysPanel != null) Open(keysPanel.gameObject, keysPanel.First);
        }

        void RefreshVersion()
        {
            if (versionText != null) versionText.text = $"v{GameConst.Version}   ·   save {SaveSystem.Slot + 1}";
        }

        /// <summary>The save slots: what each one holds. Picking one plays it; Erase wipes it (asks once).</summary>
        public void OpenSlots()
        {
            if (slotPanel == null) { StartRun(); return; }
            eraseArmed = -1;
            RefreshSlots();
            Selectable first = slotBack;
            if (slotButtons != null && slotButtons.Length > 0) first = slotButtons[Mathf.Clamp(SaveSystem.Slot, 0, slotButtons.Length - 1)];
            Open(slotPanel, first);
        }

        void RefreshSlots()
        {
            for (int i = 0; slotTexts != null && i < slotTexts.Length; i++)
            {
                var r = SaveSystem.PeekSlot(i);
                bool used = SaveSystem.SlotUsed(i) && r != null;
                string head = $"SAVE {i + 1}" + (i == SaveSystem.Slot && used ? "   (last played)" : "");
                if (!used) slotTexts[i].text = head + "\n<color=#FFFFFF99>empty  -  start a new game</color>";
                else
                {
                    int freed = r.rescued != null ? r.rescued.Count : 0;
                    string going = r.run != null && r.run.active ? $"   ·   <color=#FFD662>Continue: {r.run.stageName}</color>" : "";
                    // (a night in progress is named beside the slot's name: picking the slot goes on with it)
                    slotTexts[i].text = head + going + $"\n<color=#FFFFFFCC>{freed}/{Villagers.All.Length} villagers home   ·   {r.embers} embers</color>";
                }
                if (slotErase != null && i < slotErase.Length) slotErase[i].gameObject.SetActive(used);
                if (slotEraseLabels != null && i < slotEraseLabels.Length) slotEraseLabels[i].text = eraseArmed == i ? "SURE?" : "ERASE";
            }
        }

        void EraseSlot(int i)
        {
            if (eraseArmed != i) { eraseArmed = i; RefreshSlots(); return; }       // first press: ask
            eraseArmed = -1;
            SaveSystem.EraseSlot(i);
            RefreshSlots();
            RefreshVersion();
            if (slotButtons != null && i < slotButtons.Length) UIUtil.Select(slotButtons[i]);
        }

        /// <summary>Goes on with the run saved in the slot being played: the level it had reached, from its start.</summary>
        void ContinueRun()
        {
            if (LoadingScreen.Busy) return;
            var saved = SaveSystem.SavedRun;
            if (saved == null) { StartRun(); return; }
            var run = RunSession.Restore(saved, config);
            RunPlan.Ensure(run, config);
            int at = Mathf.Clamp(run.stageIndex, 0, run.stageOrder.Length - 1);
            LevelFactory.Pregenerate(config, config.stages[Mathf.Clamp(run.stageOrder[at], 0, config.stages.Length - 1)].Named(run.stageNames[at]), run.seed, run.stageIndex);
            LoadingScreen.Load(GameConst.SceneGame);
        }

        void StartRun()
        {
            if (LoadingScreen.Busy) return;
            // a new game begins with the tutorial (it can be skipped from its pause screen); after that, the village
            if (!SaveSystem.TutorialDone) { StartTutorial(); return; }
            Codex.Give();       // (a save older than the Field Guide gets the book it would have found)
            RunSession.NewRun(RunSession.RandomSeed(), false, config.player);
            // the first level is built behind the loading screen
            var run = RunSession.Current;
            RunPlan.Ensure(run, config);
            if (run.stageOrder != null && run.stageOrder.Length > 0)
                LevelFactory.Pregenerate(config, config.stages[Mathf.Clamp(run.stageOrder[0], 0, config.stages.Length - 1)].Named(run.stageNames[0]), run.seed, 0);
            LoadingScreen.Load(GameConst.SceneGame);
        }

        static bool bossTestArgUsed, loadingShotUsed, tutorialArgUsed, endingArgUsed;

        /// <summary>"01:00.2  [flawless]" for a boss that was beaten in Challenge, "" otherwise.</summary>
        public static string BossBest(string boss, string never)
        {
            var rec = SaveSystem.BossRecordOf(boss);
            if (rec == null) return never;
            return (rec.noDamage ? "[flawless]  " : "") + UIUtil.FormatTime(rec.bestTime);      // (the badge first: at the end of a right-aligned line it hung out past the row)
        }

        public void OpenBossList()
        {
            if (bossPanel == null) return;
            if (bossBest != null && bossNames != null)
                for (int i = 0; i < bossBest.Length && i < bossNames.Length; i++)
                    if (bossBest[i] != null) bossBest[i].text = BossBest(bossNames[i], "");
            Selectable first = bossBack;
            if (bossButtons != null) foreach (var b in bossButtons) if (b != null && b.gameObject.activeSelf) { first = b; break; }
            Open(bossPanel, first);
        }

        /// <summary>Boss test: straight into this boss's arena with a fresh explorer.</summary>
        public void StartBossPractice(int stageIndex)
        {
            if (LoadingScreen.Busy) return;
            RunSession.NewBossPractice(stageIndex, config.player);
            LoadingScreen.Load(GameConst.SceneGame);
        }

        void ShowTitle(bool on)
        {
            if (titleObjects == null) return;
            foreach (var o in titleObjects) if (o != null) o.SetActive(on);
        }

        void StartTutorial()
        {
            if (LoadingScreen.Busy) return;
            RunSession.NewRun(1, true, config.player);
            LoadingScreen.Load(GameConst.SceneGame);
        }

        void Open(GameObject panel, Selectable first)
        {
            mainPanel.SetActive(false);
            ShowTitle(false);
            recordsPanel.SetActive(panel == recordsPanel);
            if (slotPanel != null) slotPanel.SetActive(panel == slotPanel);
            if (bossPanel != null) bossPanel.SetActive(panel == bossPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            if (keysPanel != null) keysPanel.gameObject.SetActive(panel == keysPanel.gameObject);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            ShowTitle(true);
            recordsPanel.SetActive(false);
            if (slotPanel != null) slotPanel.SetActive(false);
            if (bossPanel != null) bossPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            if (keysPanel != null) keysPanel.gameObject.SetActive(false);
            UIUtil.Select(startButton);
        }

        void RefreshRecords()
        {
            var r = SaveSystem.Totals;      // (all saves together, since the game was first played)
            recordsText.text =
                $"Runs played        {r.runs}\n" +
                $"Victories          {r.wins}\n" +
                $"Deaths             {r.deaths}\n" +
                $"Fastest victory    {(r.fastestWin < 0 ? "--" : UIUtil.FormatTime(r.fastestWin))}\n" +
                $"Creatures killed   {r.kills}\n" +
                $"Bosses defeated    {r.bossKills}";
            if (bossRecordsText != null && bossNames != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var n in bossNames) sb.Append(n).Append("     ").Append(BossBest(n, "--")).Append((char)10);
                bossRecordsText.text = sb.ToString();
            }
        }
    }
}
