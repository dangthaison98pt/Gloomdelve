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
            if (prefab == null) { Debug.LogError("[GameServices] Resources/GameServices prefab missing. Run Gloomdelve > Build Everything."); return; }
            var inst = Instantiate(prefab);
            inst.name = "GameServices";
        }

        // The window shows the full title "Brann: Gloomdelve". The product name itself stays "Gloomdelve": it names the
        // save folder and the .exe, where a colon is not allowed, and changing it would orphan existing saves.
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
            if (TestRun) StayOutOfTheWay();
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

        public static void ApplyDisplaySettings()
        {
            if (Application.isEditor) return;
            if (TestRun)
            {
                // automated runs (scenarios, bot runs, picture runs) stay in a small window: the person at the
                // desk is working on something else and a full-screen game would cover it
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                return;
            }
            var s = SaveSystem.Settings;
            if (s.fullscreen)
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
            else if (Screen.fullScreen)
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }

        void Update()
        {
            if (TestRun) StayOutOfTheWay();
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
