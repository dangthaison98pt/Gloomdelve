using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The little board that slides down from the top of the screen when an achievement is earned: its picture, a
    /// gold title, a line under it. It stays a few seconds and slides back up; the game goes on meanwhile. Several
    /// at once come one after the other.
    /// </summary>
    public class AchievementToast : MonoBehaviour
    {
        public RectTransform root;
        public Image icon;
        public TMPro.TMP_Text title, line;
        [Tooltip("The pictures of the achievements, in the order of Achievements.All.")] public Sprite[] icons;
        [Tooltip("How far it hangs below the top of the screen when shown (px).")] public float shownY = -18f;
        public float slideTime = 0.35f, holdTime = 3.2f;
        [Tooltip("The board fits its words: never narrower than this, never wider than that (the line under the name wraps there).")]
        public float minWidth = 440f, maxWidth = 820f;
        [Tooltip("Where the words begin (after the picture), and the margin after them and under them.")]
        public float textLeft = 136f, margin = 30f;
        [Tooltip("When several are earned at once: the pause between one going up out of sight and the next coming down.")] public float gapTime = 0.25f;

        public static AchievementToast Instance { get; private set; }

        readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
        AchievementDef showing;
        float t, gap;

        /// <summary>One is on the screen (or coming): for the tests.</summary>
        public bool Showing => showing != null;
        public string ShowingTitle => showing != null ? showing.title : null;
        /// <summary>How far the board is from fully out of sight (0) to fully down (1): for the tests.</summary>
        public float Shown01 => root == null ? 0f : Mathf.InverseLerp(HiddenY, shownY, root.anchoredPosition.y);
        public int Waiting => queue.Count;

        void Awake()
        {
            Instance = this;
            Achievements.Earned += OnEarned;
            if (root != null) { root.anchoredPosition = new Vector2(root.anchoredPosition.x, HiddenY); root.gameObject.SetActive(false); }
        }

        void OnDestroy()
        {
            Achievements.Earned -= OnEarned;
            if (Instance == this) Instance = null;
        }

        float HiddenY => root != null ? root.rect.height + 30f : 200f;

        void OnEarned(AchievementDef def) { if (def != null) queue.Enqueue(def); }

        void Update()
        {
            if (root == null) return;
            if (showing == null)
            {
                if (gap > 0f) { gap -= Time.unscaledDeltaTime; return; }        // (the last one has just gone up: a breath before the next)
                if (queue.Count == 0) return;
                showing = queue.Dequeue();
                t = 0f;
                int i = System.Array.IndexOf(Achievements.All, showing);
                if (icon != null) icon.sprite = icons != null && i >= 0 && i < icons.Length ? icons[i] : null;
                if (title != null) title.text = showing.title;
                if (line != null) line.text = showing.description;
                Fit();
                root.anchoredPosition = new Vector2(root.anchoredPosition.x, HiddenY);
                root.gameObject.SetActive(true);
                Sfx.PlayUI("achievement", 0.8f);
            }
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);       // (a long frame - a hitch while something loads - does not make it jump)
            float total = slideTime * 2f + holdTime;
            float k = t < slideTime ? Ease(t / slideTime) : t < slideTime + holdTime ? 1f : 1f - Ease((t - slideTime - holdTime) / slideTime);
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, Mathf.Lerp(HiddenY, shownY, k));
            if (t >= total) { showing = null; root.gameObject.SetActive(false); gap = gapTime; }
        }

        /// <summary>The board as wide as its longest line (the picture, the words, a margin), and as tall as the words under the name.</summary>
        void Fit()
        {
            float most = maxWidth - textLeft - margin;
            float w = 0f;
            foreach (var label in new[] { head, title, line })
                if (label != null) w = Mathf.Max(w, label.GetPreferredValues(label.text, 99999f, 0f).x);
            w = Mathf.Min(Mathf.Ceil(w) + 4f, most);
            float boardW = Mathf.Max(minWidth, textLeft + w + margin);
            w = boardW - textLeft - margin;
            foreach (var label in new[] { head, title, line })
                if (label != null) label.rectTransform.sizeDelta = new Vector2(w, label.rectTransform.sizeDelta.y);
            float h = root.sizeDelta.y;
            if (line != null)
            {
                float lineH = line.GetPreferredValues(line.text, w, 0f).y;
                line.rectTransform.sizeDelta = new Vector2(w, lineH);
                float top = -line.rectTransform.anchoredPosition.y;
                h = Mathf.Max(minHeight, top + lineH + bottomMargin);
            }
            root.sizeDelta = new Vector2(boardW, h);
        }

        [Tooltip("Never shorter than this (the picture and its margins), and the room left under the words.")] public float minHeight = 112f, bottomMargin = 20f;
        /// <summary>The label over the name ("ACHIEVEMENT UNLOCKED").</summary>
        public TMPro.TMP_Text head;
        /// <summary>The board's size now (tests).</summary>
        public Vector2 Size => root != null ? root.sizeDelta : Vector2.zero;

        static float Ease(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    }
}
