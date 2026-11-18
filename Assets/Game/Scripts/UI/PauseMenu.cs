using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class PauseMenu : MonoBehaviour
    {
        public GameObject root;
        public GameObject mainPanel;
        public SettingsPanel settingsPanel;
        [Tooltip("Settings > Keys: the controls list, where keyboard keys can be changed.")] public KeyBindPanel keysPanel;
        public Button resumeButton, settingsButton, menuButton;
        public TMPro.TMP_Text infoText;

        void Awake()
        {
            var d = GetComponentInParent<GameDirector>();
            UIUtil.Wire(resumeButton, () => GameDirector.Instance.SetPaused(false));
            UIUtil.Wire(settingsButton, () => { Open(settingsPanel.gameObject, settingsPanel.First); });
            UIUtil.Wire(menuButton, () => GameDirector.Instance.QuitToMenu());
            settingsPanel.onBack = ShowMain;
            settingsPanel.onKeys = OpenKeys;
            if (keysPanel != null) keysPanel.onBack = () => Open(settingsPanel.gameObject, settingsPanel.keys != null ? settingsPanel.keys : settingsPanel.First);
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

        /// <summary>The controls / change-keys screen (from the Settings panel).</summary>
        public void OpenKeys()
        {
            if (keysPanel != null) Open(keysPanel.gameObject, keysPanel.First);
        }

        void Open(GameObject panel, Selectable first)
        {
            mainPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            if (keysPanel != null) keysPanel.gameObject.SetActive(panel == keysPanel.gameObject);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            settingsPanel.gameObject.SetActive(false);
            if (keysPanel != null) keysPanel.gameObject.SetActive(false);
            UIUtil.Select(resumeButton);
        }

        /// <summary>Esc inside a sub panel goes back; returns false when already on the main panel.</summary>
        public bool HandleBack()
        {
            if (keysPanel != null && keysPanel.gameObject.activeSelf) { keysPanel.onBack?.Invoke(); return true; }   // back to Settings
            if (!mainPanel.activeSelf) { ShowMain(); return true; }
            return false;
        }
    }
}
