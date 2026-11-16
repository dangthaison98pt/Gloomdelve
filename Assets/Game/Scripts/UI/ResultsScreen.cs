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
        public Text body;
        public Text records;
        public Text news;
        public Button newRunButton;
        public Button retrySeedButton;
        public Button menuButton;
        public Text newRunLabel;

        bool tutorialMode;
        float shownAt;

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
            string how = victory ? "You carried the Heartstone back to the surface." : run.deathCause;
            body.text =
                $"{how}\n\n" +
                $"Reached:  {run.reachedStageName}\n" +
                $"Gold:  ${run.gold}\n" +
                $"Time:  {UIUtil.FormatTime(run.totalTime)}\n" +
                $"Kills:  {run.kills}      Levels cleared:  {run.levelsCompleted}\n" +
                $"Seed:  {run.seed}   (SAME SEED replays this cave)";
            var r = SaveSystem.Records;
            records.text =
                $"BEST   deepest {r.bestStageName}   ·   gold ${r.bestGold}   ·   fastest win {(r.fastestWin < 0 ? "--" : UIUtil.FormatTime(r.fastestWin))}\n" +
                $"runs {r.runs}   ·   wins {r.wins}   ·   deaths {r.deaths}";
            news.text = newsText;
            retrySeedButton.gameObject.SetActive(true);
            if (newRunLabel) newRunLabel.text = "NEW RUN  (R)";
            UIUtil.Select(newRunButton);
        }

        public void ShowTutorialComplete(RunState run)
        {
            tutorialMode = true;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            title.text = "TUTORIAL COMPLETE";
            title.color = new Color(0.6f, 1f, 0.6f);
            body.text = "You know the basics: run, jump, hang, climb, whip,\nthrow, bomb and rope.\n\nTwo cave areas and the Sunken Vault await.\nFind the Heartstone and carry it out!";
            records.text = "";
            news.text = $"Time {UIUtil.FormatTime(run.totalTime)}";
            retrySeedButton.gameObject.SetActive(false);
            if (newRunLabel) newRunLabel.text = "START A RUN  (R)";
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
