using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class ResultsScreen : MonoBehaviour
    {
        public GameObject root;
        public Text title;
        [Tooltip("One line under the title: how the run ended.")] public Text cause;
        [Tooltip("How far the run got.")] public Text reached;
        [Tooltip("The four stat tiles (hidden for the tutorial).")] public GameObject statsRoot;
        public Text statGold, statTime, statKills, statLevels;
        public Text seedText;
        [Tooltip("Free text block (tutorial only).")] public Text body;
        public Text records;
        public Text news;
        public Button newRunButton;
        public Button retrySeedButton;
        public Button menuButton;
        public Text newRunLabel;

        bool tutorialMode;
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
                RunSession.NewRun(RunSession.RandomSeed(), false, GameDirector.Instance.config.player);
                SceneManager.LoadScene(GameConst.SceneGame);
            }
            else GameDirector.Instance.RestartRun(false);
        }

        public void ShowRunEnd(RunState run, bool victory, string newsText, int stageReached)
        {
            tutorialMode = false;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            title.text = victory ? "VICTORY!" : "YOU DIED";
            title.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.35f);
            cause.text = victory ? "You carried the Heartstone back to the surface." : run.deathCause;
            reached.text = victory ? "" : "REACHED  " + run.reachedStageName;
            body.text = "";
            statsRoot.SetActive(true);
            statGold.text = "$" + run.gold;
            statTime.text = UIUtil.FormatTime(run.totalTime);
            statKills.text = run.kills.ToString();
            statLevels.text = run.levelsCompleted.ToString();
            seedText.text = "Seed " + run.seed;
            var r = SaveSystem.Records;
            records.text =
                $"BEST     deepest {r.bestStageName}     gold ${r.bestGold}     fastest win {(r.fastestWin < 0 ? "--" : UIUtil.FormatTime(r.fastestWin))}\n" +
                $"{r.runs} runs     {r.wins} wins     {r.deaths} deaths";
            news.text = newsText;
            retrySeedButton.gameObject.SetActive(true);
            if (newRunLabel) newRunLabel.text = "NEW RUN";
            UIUtil.Select(newRunButton);
        }

        /// <summary>
        /// Challenge (boss fight from the main menu) won: what the fight cost. Stays up until the player picks "fight again" or "boss list".
        /// </summary>
        public void ShowBossTest(RunState run, string bossName, float fightTime)
        {
            tutorialMode = false;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            title.text = "BOSS DEFEATED";
            title.color = new Color(1f, 0.85f, 0.3f);
            cause.text = string.IsNullOrEmpty(bossName) ? "" : bossName.ToUpperInvariant();
            reached.text = "";
            seedText.text = "";
            body.text = "";
            records.text = "";
            // the four big tiles, relabelled for a fight (this screen is not reused: the scene reloads afterwards)
            statsRoot.SetActive(true);
            // only two numbers matter for a fight: how long it took and how much health it cost (the two middle tiles)
            Tile(statTime, "TIME", UIUtil.FormatTime(fightTime), true);
            Tile(statKills, "HEALTH LOST", run.damageTaken.ToString(), false);
            if (statGold != null) statGold.transform.parent.gameObject.SetActive(false);
            if (statLevels != null) statLevels.transform.parent.gameObject.SetActive(false);
            news.text = "Challenge - nothing is recorded.";
            retrySeedButton.gameObject.SetActive(false);
            if (newRunLabel) newRunLabel.text = "FIGHT AGAIN";
            var menuLabel = menuButton != null ? menuButton.GetComponentInChildren<Text>() : null;
            if (menuLabel != null) menuLabel.text = "BOSS LIST";
            UIUtil.Wire(menuButton, () => GameDirector.Instance.BackToBossList());
            UIUtil.Select(newRunButton);
        }

        /// <summary>Sets one stat tile: its caption, its value, and whether its small icon shows.</summary>
        static void Tile(Text value, string caption, string text, bool icon)
        {
            if (value == null) return;
            value.text = text;
            var tile = value.transform.parent;
            var label = tile.Find("Label");
            if (label != null)
            {
                var lt = label.GetComponent<Text>();
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
            title.text = "TUTORIAL COMPLETE";
            title.color = new Color(0.6f, 1f, 0.6f);
            cause.text = "";
            reached.text = "";
            seedText.text = "";
            statsRoot.SetActive(false);
            body.text = "You know the basics: run, jump, hang, climb, slash, throw, bomb and rope.\n\nFour areas, each with a boss, lie between you and the Heartstone.";
            records.text = "";
            news.text = $"Time {UIUtil.FormatTime(run.totalTime)}";
            retrySeedButton.gameObject.SetActive(false);
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
