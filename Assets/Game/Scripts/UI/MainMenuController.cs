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
        public GameObject controlsPanel;
        public GameObject recordsPanel;
        public SettingsPanel settingsPanel;
        public Button startButton, tutorialButton, controlsButton, settingsButton, recordsButton, quitButton;
        public Button controlsBack, recordsBack, resetRecordsButton;
        public Text recordsText;
        public Text versionText;
        public Text controlsBody;

        void Start()
        {
            Time.timeScale = 1f;
            SaveSystem.EnsureLoaded();
            UIUtil.Wire(startButton, StartRun);
            UIUtil.Wire(tutorialButton, StartTutorial);
            UIUtil.Wire(controlsButton, () => Open(controlsPanel, controlsBack));
            UIUtil.Wire(settingsButton, () => Open(settingsPanel.gameObject, settingsPanel.First));
            UIUtil.Wire(recordsButton, () => { RefreshRecords(); Open(recordsPanel, recordsBack); });
            UIUtil.Wire(quitButton, GameDirector.QuitGame);
            UIUtil.Wire(controlsBack, ShowMain);
            UIUtil.Wire(recordsBack, ShowMain);
            UIUtil.Wire(resetRecordsButton, () => { SaveSystem.ResetRecords(); RefreshRecords(); });
            settingsPanel.onBack = ShowMain;
            if (controlsBody != null) controlsBody.text = ControlsText.Body;
            if (versionText != null) versionText.text = $"v{GameConst.Version}   ·   last seed {SaveSystem.Records.lastSeed}";
            ShowMain();
            if (AudioManager.Instance != null) { AudioManager.Instance.SetMusicPitch(1f); AudioManager.Instance.PlayMusic("menu"); }
            RunSession.Clear();

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
            Open(controlsPanel, controlsBack);
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScenarioRunner.OutDir, "menu_controls.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            RunSession.NewRun(1, true, config.player);
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !mainPanel.activeSelf) ShowMain();
        }

        void StartRun()
        {
            RunSession.NewRun(RunSession.RandomSeed(), false, config.player);
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
            controlsPanel.SetActive(panel == controlsPanel);
            recordsPanel.SetActive(panel == recordsPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            controlsPanel.SetActive(false);
            recordsPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
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
