using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class ResultsScreen : MonoBehaviour
    {
        public GameObject root;
        public TMPro.TMP_Text title;
        [Tooltip("One line under the title: how the run ended.")] public TMPro.TMP_Text cause;
        [Tooltip("How far the run got.")] public TMPro.TMP_Text reached;
        [Tooltip("The four stat tiles (hidden for the tutorial).")] public GameObject statsRoot;
        public TMPro.TMP_Text statGold, statTime, statKills, statLevels;
        public TMPro.TMP_Text seedText;
        [Tooltip("Free text block (tutorial only).")] public TMPro.TMP_Text body;
        public TMPro.TMP_Text records;
        public TMPro.TMP_Text news;
        public Button newRunButton;
        public Button retrySeedButton;
        public Button menuButton;
        public TMPro.TMP_Text newRunLabel;

        bool tutorialMode;
        string runLabel;       // what the first button says when it is not the tutorial's
        float shownAt;
        public bool Showing => root != null && root.activeSelf;

        void Awake()
        {
            root.SetActive(false);
            UIUtil.Wire(newRunButton, NewRun);
            UIUtil.Wire(retrySeedButton, () => GameDirector.Instance.RestartRun(true));
            UIUtil.Wire(menuButton, () => GameDirector.Instance.QuitToMenu());
        }

        void NewRun()
        {
            if (tutorialMode)
            {
                Time.timeScale = 1f;
                if (LoadingScreen.Busy) return;
                RunSession.NewRun(RunSession.RandomSeed(), false, GameDirector.Instance.config.player);
                LoadingScreen.Load(GameConst.SceneGame);
            }
            else GameDirector.Instance.RestartRun(false, false);
        }

        public void ShowRunEnd(RunState run, bool victory, string newsText, int stageReached)
        {
            tutorialMode = false;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            ShowRule(true);
            title.text = victory ? "VICTORY!" : "YOU DIED";
            title.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.35f);
            cause.text = victory ? "You carried the Sunstone home. Brindlemoor is warm again." : run.deathCause;
            reached.text = victory ? "" : "REACHED  " + run.reachedStageName;
            body.text = "";
            statsRoot.SetActive(true);
            statGold.text = "$" + run.gold;
            statTime.text = UIUtil.FormatTime(run.totalTime);
            statKills.text = run.kills.ToString();
            statLevels.text = run.levelsCompleted.ToString();
            seedText.text = "Seed " + run.seed;
            var r = SaveSystem.Totals;
            records.text =
                $"BEST     fastest win {(r.fastestWin < 0 ? "--" : UIUtil.FormatTime(r.fastestWin))}\n" +
                $"{r.runs} runs     {r.wins} wins     {r.deaths} deaths";
            news.text = newsText;
            // two ways on from here: straight down into the mountain again, or home to Brindlemoor (to wake in bed,
            // spend embers, look around). The middle button is gone, so the two sit side by side in the middle.
            retrySeedButton.gameObject.SetActive(false);
            SetX(newRunButton, -172f);
            SetX(menuButton, 172f);
            if (newRunLabel) newRunLabel.text = "NEW RUN";
            var homeLabel = menuButton != null ? menuButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            if (homeLabel != null) homeLabel.text = "BACK TO VILLAGE";
            UIUtil.Wire(menuButton, () => GameDirector.Instance.RestartRun(false, true));
            UIUtil.Select(newRunButton);
        }

        /// <summary>
        /// Challenge (boss fight from the main menu) won: time and health lost, the record for this boss, and the
        /// no-damage badge ("[flawless]") when the fight cost no health. Stays up until the player picks
        /// "fight again" or "boss list".
        /// </summary>
        public void ShowBossTest(RunState run, string bossName, float fightTime, bool newBest, bool firstFlawless, float bestTime, bool everFlawless)
        {
            tutorialMode = false;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            bool flawless = run.damageTaken == 0;
            ShowRule(true);
            title.text = "BOSS DEFEATED";
            title.color = new Color(1f, 0.85f, 0.3f);
            cause.text = (string.IsNullOrEmpty(bossName) ? "" : bossName.ToUpperInvariant()) + (flawless ? "    [flawless]  NO DAMAGE" : "");
            reached.text = "";
            seedText.text = "";
            body.text = "";
            // only two numbers matter for a fight: how long it took and how much health it cost (the two middle tiles)
            statsRoot.SetActive(true);
            Tile(statTime, "TIME", UIUtil.FormatTime(fightTime), true);
            Tile(statKills, "HEALTH LOST", run.damageTaken.ToString(), false);
            if (statGold != null) statGold.transform.parent.gameObject.SetActive(false);
            if (statLevels != null) statLevels.transform.parent.gameObject.SetActive(false);
            news.text = newBest && firstFlawless ? "NEW BEST TIME!     FIRST WIN WITHOUT DAMAGE!"
                      : newBest ? "NEW BEST TIME!"
                      : firstFlawless ? "FIRST WIN WITHOUT DAMAGE!" : "";
            records.text = $"Best time   {UIUtil.FormatTime(bestTime)}" + (everFlawless ? "      [flawless]  beaten without damage" : "");
            retrySeedButton.gameObject.SetActive(false);
            if (newRunLabel) newRunLabel.text = "FIGHT AGAIN";
            var menuLabel = menuButton != null ? menuButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            if (menuLabel != null) menuLabel.text = "BOSS LIST";
            UIUtil.Wire(menuButton, () => GameDirector.Instance.BackToBossList());
            // with the middle button gone the other two sit side by side in the middle
            SetX(newRunButton, -172f);
            SetX(menuButton, 172f);
            UIUtil.Select(newRunButton);
        }

        /// <summary>The thin gold line under the heading.</summary>
        void ShowRule(bool on)
        {
            var rule = title != null ? title.transform.parent.Find("Rule") : null;
            if (rule != null) rule.gameObject.SetActive(on);
        }

        static void SetX(Button b, float x)
        {
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        /// <summary>Sets one stat tile: its caption, its value, and whether its small icon shows.</summary>
        static void Tile(TMPro.TMP_Text value, string caption, string text, bool icon)
        {
            if (value == null) return;
            value.text = text;
            var tile = value.transform.parent;
            var label = tile.Find("Label");
            if (label != null)
            {
                var lt = label.GetComponent<TMPro.TMP_Text>();
                if (lt != null) lt.text = caption;
                if (!icon) ((RectTransform)label).anchoredPosition = new Vector2(0f, ((RectTransform)label).anchoredPosition.y);
            }
            var ic = tile.Find("Icon");
            if (ic != null) ic.gameObject.SetActive(icon);
        }

        public void ShowTutorialComplete(RunState run)
        {
            tutorialMode = true;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            ShowRule(false);        // the text block of this screen sits where the rule is drawn
            title.text = "TUTORIAL COMPLETE";
            title.color = new Color(0.6f, 1f, 0.6f);
            cause.text = "";
            reached.text = "";
            seedText.text = "";
            statsRoot.SetActive(false);
            body.text = "You know the way of it now: run, jump, hang, climb, slash, dash, throw, bomb and rope.\n\nThe Field Guide is in your bag, under Key items.  Brindlemoor is a short walk from here - and the mountain is behind it.";
            var go = newRunButton != null ? newRunButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            if (go != null) { runLabel = runLabel ?? go.text; go.text = "TO THE VILLAGE"; }
            records.text = "";
            news.text = $"Time {UIUtil.FormatTime(run.totalTime)}";
            retrySeedButton.gameObject.SetActive(false);
            // with the middle button gone the other two sit side by side in the middle, not out at the corners
            SetX(newRunButton, -172f);
            SetX(menuButton, 172f);
            if (newRunLabel) newRunLabel.text = "START A RUN";
            UIUtil.Select(newRunButton);
        }

        void Update()
        {
            if (!root.activeSelf) return;
            var kb = Keyboard.current;
            if (kb != null && Time.unscaledTime - shownAt > 0.5f && kb.rKey.wasPressedThisFrame) NewRun();
        }
    }
}
