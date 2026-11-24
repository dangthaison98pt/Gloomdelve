using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Gloomdelve
{
    /// <summary>
    /// Persistent services (audio, settings). Auto-created from Resources/GameServices before the first scene loads,
    /// so every scene (including Game.unity opened directly in the editor) works on its own.
    /// </summary>
    public class GameServices : MonoBehaviour
    {
        public static GameServices Instance { get; private set; }
        public GameConfig config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var prefab = Resources.Load<GameServices>("GameServices");
            if (prefab == null) { Debug.LogError("[GameServices] Resources/GameServices prefab missing. Run Brindlemoor > Build Everything."); return; }
            var inst = Instantiate(prefab);
            inst.name = "GameServices";
        }

        // The window shows the full title (GameConst.GameName). The product name is just "Brindlemoor": it names the
        // save folder and the .exe, where a colon is not allowed. (Saves from when it was "Gloomdelve" are carried
        // over by SaveSystem.CarryOverOldSaves.)
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")] static extern System.IntPtr GetActiveWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowText(System.IntPtr hWnd, string text);
        static bool titled;
        void ApplyWindowTitle()
        {
            if (titled) return;
            var w = GetActiveWindow();
            if (w == System.IntPtr.Zero) return;        // not in front yet: tried again shortly
            titled = SetWindowText(w, GameConst.GameName);
        }

        // Test runs must not get in the way of whoever is working at this desk: the window is parked off the
        // screen, behind everything, and if Windows put it in front the window that was in front gets its place back.
        [StructLayout(LayoutKind.Sequential)] struct WinRect { public int left, top, right, bottom; }
        [DllImport("user32.dll")] static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern System.IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(System.IntPtr hWnd);
        [DllImport("user32.dll")] static extern System.IntPtr GetWindow(System.IntPtr hWnd, uint cmd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(System.IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsIconic(System.IntPtr hWnd);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(System.IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(System.IntPtr hWnd, out WinRect r);
        static System.IntPtr ownWindow;
        const int ParkedAt = -20000;

        static void StayOutOfTheWay()
        {
            if (ownWindow == System.IntPtr.Zero)
            {
                try { ownWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; } catch { }
                if (ownWindow == System.IntPtr.Zero) ownWindow = GetActiveWindow();
                if (ownWindow == System.IntPtr.Zero) return;
            }
            if (GetWindowRect(ownWindow, out var r) && r.left > ParkedAt + 1000)
                SetWindowPos(ownWindow, (System.IntPtr)1 /* bottom */, ParkedAt, ParkedAt, 0, 0, 0x0001 /* keep size */ | 0x0010 /* do not activate */);
            if (GetForegroundWindow() != ownWindow) return;
            for (var w = GetWindow(ownWindow, 2 /* next below */); w != System.IntPtr.Zero; w = GetWindow(w, 2))
                if (IsWindowVisible(w) && !IsIconic(w) && GetWindowTextLength(w) > 0) { SetForegroundWindow(w); break; }
        }
#else
        void ApplyWindowTitle() { }
        static void StayOutOfTheWay() { }
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ApplyWindowTitle();
            InvokeRepeating(nameof(ApplyWindowTitle), 0.5f, 1f);
            DontDestroyOnLoad(gameObject);
            Time.fixedDeltaTime = GameConst.Dt;
            Time.maximumDeltaTime = 0.1f;
            SaveSystem.EnsureLoaded();
            ApplyDisplaySettings();
            if (TestRun && !ToggleShot) StayOutOfTheWay();
        }

        /// <summary>True when the game was started by a test command (-scenarios, -autotest, -loadingShot).</summary>
        public static bool TestRun
        {
            get
            {
                if (testRun < 0)
                {
                    testRun = 0;
                    foreach (var a in System.Environment.GetCommandLineArgs())
                        if (a == "-scenarios" || a == "-autotest" || a == "-loadingShot" || a == "-shotEvery") testRun = 1;
                }
                return testRun == 1;
            }
        }
        static int testRun = -1;

        /// <summary>The command line names the screen (-screen-width / -screen-height / -screen-fullscreen): test runs keep it.</summary>
        public static bool CommandLineOwnsScreen
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs())
                    if (a == "-screen-width" || a == "-screen-height" || a == "-screen-fullscreen") return true;
                return false;
            }
        }

        public static void ApplyDisplaySettings()
        {
            if (Application.isEditor) return;
            if (TestRun)
            {
                // automated runs (scenarios, bot runs, picture runs) stay in a small window: the person at the
                // desk is working on something else and a full-screen game would cover it - unless the command
                // line names the screen itself (-screen-width / -screen-height / -screen-fullscreen), to look at
                // the game at another size or on a screen of another shape
                if (!CommandLineOwnsScreen) Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                return;
            }
            ApplySize();
        }

        static void ApplySize()
        {
            var s = SaveSystem.Settings;
            var want = s.fullscreen ? NativeSize : WindowSizes[WindowSizeIndex];
            var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.width != want.x || Screen.height != want.y || Screen.fullScreenMode != mode)
                Screen.SetResolution(want.x, want.y, mode);
        }

        /// <summary>
        /// "-toggleShot" with "-loadingShot dir": once the street picture is taken, the game goes window, full screen,
        /// window again on this screen, then moves to another screen (a portrait one, say) and does the same,
        /// with a picture after every change - the frame and the screens must fit each size without a stray dark band.
        /// The window stays on the screen meanwhile (it is not parked out of the way), and nothing is saved.
        /// </summary>
        public static bool ToggleShot => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-toggleShot") >= 0;

        public static System.Collections.IEnumerator ToggleShots(string dir)
        {
            bool was = SaveSystem.Settings.fullscreen;
            int step = 0;
            System.Collections.IEnumerator Shot(bool full)
            {
                SaveSystem.Settings.fullscreen = full;
                ApplySize();
                yield return new WaitForSecondsRealtime(1.5f);
                var d = Screen.mainWindowDisplayInfo;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,
                    $"toggle_{++step}_{(full ? "full" : "window")}_{Screen.width}x{Screen.height}_on_{d.width}x{d.height}.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return Shot(false);
            yield return Shot(true);
            yield return Shot(false);
            var here = Screen.mainWindowDisplayInfo;
            var screens = new System.Collections.Generic.List<DisplayInfo>();
            Screen.GetDisplayLayout(screens);
            foreach (var other in screens)
            {
                if (other.width == here.width && other.height == here.height && other.name == here.name) continue;
                var move = Screen.MoveMainWindowTo(other, Vector2Int.zero);
                while (move != null && !move.isDone) yield return null;
                yield return Shot(false);
                yield return Shot(true);
                yield return Shot(false);
                break;
            }
            SaveSystem.Settings.fullscreen = was;
        }

        // ---- the size of the window: fixed 16:9 sizes, picked in Settings; the window cannot be dragged to another size ----
        public static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };

        /// <summary>
        /// The real size of the screen the game window is on (with more than one screen it need not be the main one).
        /// Full screen always runs at this size: at a smaller one Windows stretches the picture and the words go soft.
        /// </summary>
        public static Vector2Int NativeSize
        {
            get
            {
                var d = Screen.mainWindowDisplayInfo;
                if (d.width > 0 && d.height > 0) return new Vector2Int(d.width, d.height);
                var r = Screen.currentResolution;
                return new Vector2Int(r.width, r.height);
            }
        }

        /// <summary>The largest window size that fits this screen, leaving room for the title bar and the taskbar.</summary>
        public static int MaxWindowSizeIndex
        {
            get
            {
                var n = NativeSize;
                int best = 0;
                for (int i = 0; i < WindowSizes.Length; i++)
                    if (WindowSizes[i].x <= n.x && WindowSizes[i].y <= n.y - 100) best = i;
                return best;
            }
        }

        /// <summary>The window size in use (Settings > Window size); the first time, the largest that fits.</summary>
        public static int WindowSizeIndex
        {
            get
            {
                int i = SaveSystem.Settings.windowSize;
                return i < 0 ? MaxWindowSizeIndex : Mathf.Clamp(i, 0, MaxWindowSizeIndex);
            }
        }

        /// <summary>Goes to the next window size (round to the smallest after the largest that fits). Not in full screen.</summary>
        public static void NextWindowSize()
        {
            if (SaveSystem.Settings.fullscreen) return;
            int i = WindowSizeIndex;
            SaveSystem.Settings.windowSize = i >= MaxWindowSizeIndex ? 0 : i + 1;
            SaveSystem.SaveSettings();
            ApplyDisplaySettings();
        }

        void Update()
        {
            if (TestRun && !ToggleShot) StayOutOfTheWay();
            var kb = Keyboard.current;
            if (kb != null && (kb.f11Key.wasPressedThisFrame || kb.f4Key.wasPressedThisFrame))
            {
                SaveSystem.Settings.fullscreen = !SaveSystem.Settings.fullscreen;
                SaveSystem.SaveSettings();
                ApplyDisplaySettings();
            }
        }
    }
}
