using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gloomdelve
{
    /// <summary>
    /// The loading screen between the menu and the game: the screen fades to a snowy night with the game's name,
    /// one tip and a line that fills, stays for a moment (the level is loaded behind it and only starts once the
    /// screen is about to lift), then fades away. Lives in the menu scene and survives scene changes.
    /// Use LoadingScreen.Load(scene) instead of SceneManager.LoadScene. Test runs skip it.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance;

        public GameObject cover;
        public CanvasGroup group;
        public TMP_Text tip, loadingText;
        public RectTransform fill;
        public float fillWidth = 600f;
        [Tooltip("The screen stays at least this long, however fast the level loads.")] public float minTime = 1.6f;
        [TextArea] public string[] tips;

        bool busy;
        public static bool Busy => Instance != null && Instance.busy;

        /// <summary>"-loadingShot dir" on the command line: start a run from the menu, save pictures of the loading screen and of the game after it, and quit.</summary>
        public static string ShotDir
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-loadingShot") return args[i + 1];
                return null;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            cover.SetActive(false);
        }

        public static void Load(string scene)
        {
            if (Instance == null || ScenarioRunner.Active || AutoTest.Active) { SceneManager.LoadScene(scene); return; }
            if (Instance.busy) return;
            Instance.StartCoroutine(Instance.Run(scene));
        }

        IEnumerator Run(string scene)
        {
            busy = true;
            cover.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            if (tip != null && tips != null && tips.Length > 0) tip.text = tips[Random.Range(0, tips.Length)];
            SetFill(0f);
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.25f; yield return null; }
            group.alpha = 1f;

            string shots = ShotDir;
            var op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;            // the level waits behind the screen: nothing can hurt the player yet
            bool shot = false;
            for (float t = 0f; t < minTime || op.progress < 0.9f; t += Time.unscaledDeltaTime)
            {
                SetFill(Mathf.Min(t / minTime, op.progress / 0.9f));
                if (loadingText != null) loadingText.text = "Loading" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);
                if (shots != null && !shot && t > minTime * 0.6f) { shot = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, "loading.png")); }
                yield return null;
            }
            SetFill(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.3f; yield return null; }
            cover.SetActive(false);
            busy = false;
            if (shots != null)
            {
                yield return new WaitForSecondsRealtime(1.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, "loading_after.png"));
                yield return new WaitForSecondsRealtime(0.4f);
                // the run begins indoors: step out of the house for a picture of the street as well
                var dir = GameDirector.Instance;
                if (dir != null && dir.Player != null)
                    foreach (var room in FindObjectsByType<VillageInterior>(FindObjectsSortMode.None))
                        if (room.Index == VillageBuilding.HomeOfPlayer) { room.Interact(dir.Player); break; }
                yield return new WaitForSecondsRealtime(1.0f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, "village_street.png"));
                yield return new WaitForSecondsRealtime(0.4f);
                Application.Quit();
            }
        }

        void SetFill(float f)
        {
            if (fill != null) fill.sizeDelta = new Vector2(fillWidth * Mathf.Clamp01(f), fill.sizeDelta.y);
        }
    }
}
