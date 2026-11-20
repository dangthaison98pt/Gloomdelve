using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The pause screen. What it offers depends on where the player is:
    ///   on the mountain (a run past the village): Resume, Settings, Give Up (asks first, then back to the village), Main Menu;
    ///   in a Challenge fight: Resume, Fight Again, Boss List (the list opens here, in the game: pick another boss or
    ///   go back to the fight), Settings, Main Menu;
    ///   anywhere else (village, tutorial): Resume, Settings, Main Menu.
    /// The panel grows with the number of buttons.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public GameObject root;
        public GameObject mainPanel;
        public SettingsPanel settingsPanel;
        [Tooltip("Settings > Keys: the controls list, where keyboard keys can be changed.")] public KeyBindPanel keysPanel;
        public Button resumeButton, settingsButton, menuButton;
        [Tooltip("On the mountain only: end the run and go home (asks first).")] public Button giveUpButton;
        [Tooltip("In the tutorial only: enough of it, on to the village.")] public Button skipTutorialButton;
        [Tooltip("In a Challenge fight only: the same boss from the start / the list of bosses (opens inside the pause screen).")] public Button fightAgainButton, bossListButton;
        [Header("Boss list (Challenge)")]
        public GameObject bossPanel;
        [Tooltip("One button per boss ...")] public Button[] bossButtons;
        [Tooltip("... and the stage of the catalogue (GameConfig.stages index) each one starts.")] public int[] bossStages;
        [Tooltip("Display name of each boss (the key of its Challenge record).")] public string[] bossNames;
        [Tooltip("Best time / no-damage badge shown on each boss button.")] public TMPro.TMP_Text[] bossBest;
        public Button bossBack;
        [Tooltip("\"Give up this run?\" - shown in place of the main panel.")] public GameObject confirmPanel;
        public Button confirmYesButton, confirmNoButton;
        public TMPro.TMP_Text infoText;

        [Header("Layout of the main panel")]
        [Tooltip("Height of the first button's middle under the top of the panel.")] public float firstButtonY = -172f;
        [Tooltip("From one button to the next.")] public float buttonStep = 78f;
        [Tooltip("Height of the panel with three buttons; each further button adds one step.")] public float baseHeight = 610f;

        void Awake()
        {
            UIUtil.Wire(resumeButton, () => GameDirector.Instance.SetPaused(false));
            UIUtil.Wire(settingsButton, () => { Open(settingsPanel.gameObject, settingsPanel.First); });
            UIUtil.Wire(menuButton, () => GameDirector.Instance.QuitToMenu());
            UIUtil.Wire(giveUpButton, () => Open(confirmPanel, confirmNoButton));      // "keep going" is in focus: a second press by mistake changes nothing
            UIUtil.Wire(confirmNoButton, ShowMain);
            UIUtil.Wire(skipTutorialButton, () => GameDirector.Instance.SkipTutorial());
            UIUtil.Wire(confirmYesButton, () => GameDirector.Instance.GiveUp());
            UIUtil.Wire(fightAgainButton, () => GameDirector.Instance.RestartRun(false));
            UIUtil.Wire(bossListButton, OpenBossList);
            UIUtil.Wire(bossBack, ShowMain);
            if (bossButtons != null)
                for (int i = 0; i < bossButtons.Length; i++)
                {
                    int stage = bossStages != null && i < bossStages.Length ? bossStages[i] : -1;
                    UIUtil.Wire(bossButtons[i], () => GameDirector.Instance.SwitchBoss(stage));
                }
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
            inTutorial = run != null && run.isTutorial && !ScenarioRunner.Active;
            Layout(run != null && run.bossPractice, dir != null && dir.CanGiveUp);
            ShowMain();
        }

        bool inTutorial;

        /// <summary>Shows the buttons that belong here, one under the other, and fits the panel to them.</summary>
        void Layout(bool challenge, bool mountain)
        {
            var order = new[] { resumeButton, skipTutorialButton, fightAgainButton, bossListButton, settingsButton, giveUpButton, menuButton };
            int n = 0;
            foreach (var b in order)
            {
                if (b == null) continue;
                bool on = b == skipTutorialButton ? inTutorial : b == fightAgainButton || b == bossListButton ? challenge : b != giveUpButton || mountain;
                b.gameObject.SetActive(on);
                if (!on) continue;
                var rt = (RectTransform)b.transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, firstButtonY - buttonStep * n);
                n++;
            }
            var panel = (RectTransform)mainPanel.transform;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, baseHeight + buttonStep * Mathf.Max(0, n - 3));
        }

        public void Hide() { root.SetActive(false); }

        /// <summary>The controls / change-keys screen (from the Settings panel).</summary>
        public void OpenKeys()
        {
            if (keysPanel != null) Open(keysPanel.gameObject, keysPanel.First);
        }

        /// <summary>The list of bosses, without leaving the game. The boss being fought is the one in focus.</summary>
        public void OpenBossList()
        {
            if (bossPanel == null) return;
            var run = RunSession.Current;
            int now = run != null && run.stageOrder != null && run.stageOrder.Length > 0 ? run.stageOrder[0] : -1;
            Selectable first = bossBack;
            if (bossButtons != null)
                for (int i = 0; i < bossButtons.Length; i++)
                {
                    if (bossButtons[i] == null) continue;
                    if (bossBest != null && bossNames != null && i < bossBest.Length && i < bossNames.Length && bossBest[i] != null)
                        bossBest[i].text = MainMenuController.BossBest(bossNames[i], "");
                    bool current = bossStages != null && i < bossStages.Length && bossStages[i] == now;
                    if (first == bossBack || current) first = bossButtons[i];
                }
            Open(bossPanel, first);
        }

        void Open(GameObject panel, Selectable first)
        {
            if (panel == null) return;
            mainPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(panel == settingsPanel.gameObject);
            if (keysPanel != null) keysPanel.gameObject.SetActive(panel == keysPanel.gameObject);
            if (confirmPanel != null) confirmPanel.SetActive(panel == confirmPanel);
            if (bossPanel != null) bossPanel.SetActive(panel == bossPanel);
            UIUtil.Select(first);
        }

        void ShowMain()
        {
            mainPanel.SetActive(true);
            settingsPanel.gameObject.SetActive(false);
            if (keysPanel != null) keysPanel.gameObject.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (bossPanel != null) bossPanel.SetActive(false);
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
