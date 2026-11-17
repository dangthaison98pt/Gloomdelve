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
        public Text recordsText;
        public Text versionText;
        [Header("Boss test")]
        public Button bossButton, bossBack;
        public GameObject bossPanel;
        [Tooltip("One button per boss ...")] public Button[] bossButtons;
        [Tooltip("... and the stage of the run (GameConfig.stages index) each one starts.")] public int[] bossStages;
        /// <summary>Set when a boss test ends: the menu opens on the boss list instead of the main panel.</summary>
        public static bool OpenBossListOnLoad;

        void Start()
        {
            Time.timeScale = 1f;
            SaveSystem.EnsureLoaded();
            UIUtil.Wire(startButton, StartRun);
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
            if (versionText != null) versionText.text = $"v{GameConst.Version}   ·   last seed {SaveSystem.Records.lastSeed}";
            ShowMain();
            if (AudioManager.Instance != null) { AudioManager.Instance.SetMusicPitch(1f); AudioManager.Instance.PlayMusic("menu"); }
            RunSession.Clear();
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

            if (ScenarioRunner.Active) { StartCoroutine(ScenarioStart()); return; }
            if (AutoTest.Active)
            {
                RunSession.NewRun(AutoTest.CurrentSeed, false, config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
            }
        }

        System.Collections.IEnumerator ScenarioStart()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_main.png"));
            yield return new WaitForSecondsRealtime(0.3f);
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
            Open(settingsPanel.gameObject, settingsPanel.First);
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_settings.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            OpenKeys();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_keys.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            OpenBossList();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_boss.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            RunSession.NewRun(1, true, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || mainPanel.activeSelf || KeyBindings.Busy) return;   // Busy: Esc is cancelling a key change
            if (keysPanel != null && keysPanel.gameObject.activeSelf) keysPanel.onBack?.Invoke();                      // back to Settings
            else ShowMain();
        }

        /// <summary>The controls / change-keys screen (from the Settings panel).</summary>
        public void OpenKeys()
        {
            if (keysPanel != null) Open(keysPanel.gameObject, keysPanel.First);
        }

        void StartRun()
        {
            RunSession.NewRun(RunSession.RandomSeed(), false, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        static bool bossTestArgUsed;

        public void OpenBossList()
        {
            if (bossPanel == null) return;
            Selectable first = bossBack;
            if (bossButtons != null) foreach (var b in bossButtons) if (b != null && b.gameObject.activeSelf) { first = b; break; }
            Open(bossPanel, first);
        }

        /// <summary>Boss test: straight into this boss's arena with a fresh explorer.</summary>
        public void StartBossPractice(int stageIndex)
        {
            RunSession.NewBossPractice(stageIndex, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void StartTutorial()
        {
            RunSession.NewRun(1, true, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void Open(GameObject panel, Selectable first)
        {
            mainPanel.SetActive(false);
            recordsPanel.SetActive(panel == recordsPanel);
            if (bossPanel != null) bossPanel.SetActive(panel == bossPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            if (keysPanel != null) keysPanel.gameObject.SetActive(panel == keysPanel.gameObject);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            recordsPanel.SetActive(false);
            if (bossPanel != null) bossPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            if (keysPanel != null) keysPanel.gameObject.SetActive(false);
            UIUtil.Select(startButton);
        }

        void RefreshRecords()
        {
            var r = SaveSystem.Records;
            recordsText.text =
                $"Runs played        {r.runs}\n" +
                $"Victories          {r.wins}\n" +
                $"Deaths             {r.deaths}\n" +
                $"Deepest reached    {r.bestStageName}\n" +
                $"Most gold          ${r.bestGold}\n" +
                $"Fastest victory    {(r.fastestWin < 0 ? "--" : UIUtil.FormatTime(r.fastestWin))}\n" +
                $"Total kills        {r.totalKills}\n" +
                $"Last seed          {r.lastSeed}";
        }
    }
}
