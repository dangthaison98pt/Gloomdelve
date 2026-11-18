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
#else
        void ApplyWindowTitle() { }
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
        }

        public static void ApplyDisplaySettings()
        {
            if (Application.isEditor) return;
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
