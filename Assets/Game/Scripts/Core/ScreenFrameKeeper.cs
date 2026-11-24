using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gloomdelve
{
    /// <summary>
    /// Keeps every camera drawing into the frame (and one camera clearing the bars to dark), and puts every
    /// screen-space canvas into it (FramedCanvas). Made before the first scene and kept for the whole game.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ScreenFrameKeeper : MonoBehaviour
    {
        static ScreenFrameKeeper instance;
        Camera bars;
        Camera[] cams = new Camera[8];
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("ScreenFrame");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFrameKeeper>();
            SceneManager.sceneLoaded += (s, m) => { if (instance != null) instance.Scan(); };
        }

        void Awake()
        {
            bars = new GameObject("FrameBars").AddComponent<Camera>();
            bars.transform.SetParent(transform, false);
            bars.cullingMask = 0;
            bars.clearFlags = CameraClearFlags.SolidColor;
            bars.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            bars.depth = -100f;
            bars.orthographic = true;
            bars.allowHDR = false;
            bars.allowMSAA = false;
            bars.useOcclusionCulling = false;
            bars.enabled = false;
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan) Scan();
            FitCameras();
        }

        /// <summary>Puts every screen-space canvas not yet framed into the frame.</summary>
        void Scan()
        {
            nextScan = Time.unscaledTime + 0.5f;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Frame(c);
        }

        public static void Frame(Canvas c)
        {
            if (c == null || c.renderMode != RenderMode.ScreenSpaceOverlay) return;
            if (c.transform.parent != null && c.transform.parent.GetComponentInParent<Canvas>(true) != null) return;   // nested: goes with its root
            if (c.GetComponent<FramedCanvas>() != null) return;
            if (c.GetComponent<ScreenFade>() != null) return;     // the fade to dark covers the whole screen, bars and all
            c.gameObject.AddComponent<FramedCanvas>();
        }

        void FitCameras()
        {
            bool box = ScreenFrame.Letterboxed;
            if (bars.enabled != box) bars.enabled = box;
            var vp = box ? ScreenFrame.Viewport : new Rect(0f, 0f, 1f, 1f);
            if (cams.Length < Camera.allCamerasCount) cams = new Camera[Camera.allCamerasCount + 4];
            int n = Camera.GetAllCameras(cams);
            for (int i = 0; i < n; i++)
            {
                var cam = cams[i];
                if (cam == null || cam == bars || cam.targetTexture != null || cam.cameraType != CameraType.Game) continue;
                if (cam.rect != vp) cam.rect = vp;
            }
        }
    }
}
