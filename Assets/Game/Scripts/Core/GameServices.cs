using UnityEngine;
using UnityEngine.InputSystem;

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

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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
