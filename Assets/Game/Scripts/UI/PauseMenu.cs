using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class PauseMenu : MonoBehaviour
    {
        public GameObject root;
        public GameObject mainPanel;
        public GameObject controlsPanel;
        public SettingsPanel settingsPanel;
        public Button resumeButton, controlsButton, settingsButton, restartButton, retrySeedButton, menuButton, quitButton, controlsBack;
        public Text infoText;
        public Text controlsBody;

        void Awake()
        {
            var d = GetComponentInParent<GameDirector>();
            UIUtil.Wire(resumeButton, () => GameDirector.Instance.SetPaused(false));
            UIUtil.Wire(controlsButton, () => Open(controlsPanel, controlsBack));
            UIUtil.Wire(settingsButton, () => { Open(settingsPanel.gameObject, settingsPanel.First); });
            UIUtil.Wire(restartButton, () => GameDirector.Instance.RestartRun(false));
            UIUtil.Wire(retrySeedButton, () => GameDirector.Instance.RestartRun(true));
            UIUtil.Wire(menuButton, () => GameDirector.Instance.QuitToMenu());
            UIUtil.Wire(quitButton, GameDirector.QuitGame);
            UIUtil.Wire(controlsBack, ShowMain);
            settingsPanel.onBack = ShowMain;
            if (controlsBody != null) controlsBody.text = ControlsText.Body;
            root.SetActive(false);
        }

        public void Show(RunState run)
        {
            root.SetActive(true);
            var dir = GameDirector.Instance;
            if (infoText != null && run != null)
                infoText.text = $"Run seed {run.seed}   ·   {dir.Stage.displayName}   ·   level seed {dir.Level.seed}\n" +
                                $"Time {UIUtil.FormatTime(run.levelTime)}  (total {UIUtil.FormatTime(run.totalTime)})   ·   Gold ${run.gold}   ·   Kills {run.kills}";
            ShowMain();
        }

        public void Hide() { root.SetActive(false); }

        void Open(GameObject panel, Selectable first)
        {
            mainPanel.SetActive(false);
            controlsPanel.SetActive(panel == controlsPanel);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            controlsPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            UIUtil.Select(resumeButton);
        }

        /// <summary>Esc inside a sub panel goes back; returns false when already on the main panel.</summary>
        public bool HandleBack()
        {
            if (!mainPanel.activeSelf) { ShowMain(); return true; }
            return false;
        }
    }
}
