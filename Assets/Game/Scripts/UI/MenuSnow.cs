using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Falling snow over the title picture. Put it on a full-screen RectTransform between the picture and the UI:
    /// it makes its own flakes (plain squares, the size of the picture's pixels) and moves them every frame - small
    /// dim ones drifting slowly far away, larger bright ones falling faster up close, all leaning with the wind
    /// and swaying a little. A flake that leaves the bottom or a side comes back in on the other side.
    /// Purely decorative; runs on unscaled time.
    /// </summary>
    public class MenuSnow : MonoBehaviour
    {
        [Tooltip("Flakes far away: many, small, slow.")] public int farFlakes = 110;
        [Tooltip("Flakes close by: fewer, larger, faster.")] public int nearFlakes = 45;
        [Tooltip("Size of one picture pixel on the canvas (the title picture is 480 wide on a 1920 canvas: 4).")] public float pixel = 4f;
        [Tooltip("Fall speed of the far (x) and the near (y) flakes, canvas units per second.")] public Vector2 fallSpeed = new Vector2(46f, 120f);
        [Tooltip("Sideways drift (wind), as a fraction of the fall speed. Negative blows left.")] public float wind = -0.35f;
        public Color farColor = new Color(0.84f, 0.88f, 0.97f, 0.55f);
        public Color nearColor = new Color(0.96f, 0.98f, 1f, 0.95f);

        class Flake
        {
            public RectTransform rt;
            public Vector2 pos;          // the true position; what is drawn is this, snapped to the pixel grid
            public float speed, sway, phase;
        }
        Flake[] flakes;
        RectTransform area;

        public int Count => flakes != null ? flakes.Length : 0;
        /// <summary>Where flake i really is (tests check that the snow moves).</summary>
        public Vector2 PositionOf(int i) => flakes[i].pos;

        void Awake()
        {
            area = (RectTransform)transform;
            int n = farFlakes + nearFlakes;
            flakes = new Flake[n];
            var rnd = new System.Random(12345);
            float R() => (float)rnd.NextDouble();
            for (int i = 0; i < n; i++)
            {
                bool near = i >= farFlakes;
                var go = new GameObject("Flake", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = near ? nearColor : farColor;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;      // measured from the bottom-left corner of the area
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = near ? pixel * (R() < 0.3f ? 2f : 1f) : pixel;
                rt.sizeDelta = new Vector2(size, size);
                flakes[i] = new Flake
                {
                    rt = rt,
                    pos = new Vector2(R() * 1920f, R() * 1080f),      // already spread over the picture on the first frame
                    speed = (near ? fallSpeed.y : fallSpeed.x) * (0.75f + R() * 0.5f),
                    sway = (near ? 14f : 8f) * (0.5f + R()),
                    phase = R() * 6.28f,
                };
            }
        }

        void Update()
        {
            if (flakes == null) return;
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            float w = Mathf.Max(100f, area.rect.width), h = Mathf.Max(100f, area.rect.height);
            for (int i = 0; i < flakes.Length; i++)
            {
                var f = flakes[i];
                f.pos.y -= f.speed * dt;
                f.pos.x += (f.speed * wind + Mathf.Cos(t * 1.3f + f.phase) * f.sway) * dt;
                if (f.pos.y < 0f) f.pos.y += h;             // out at the bottom: back in at the top
                else if (f.pos.y > h) f.pos.y -= h;
                if (f.pos.x < 0f) f.pos.x += w;             // blown out at a side: back in at the other
                else if (f.pos.x > w) f.pos.x -= w;
                f.rt.anchoredPosition = new Vector2(Mathf.Round(f.pos.x / pixel) * pixel, Mathf.Round(f.pos.y / pixel) * pixel);
            }
        }
    }
}
