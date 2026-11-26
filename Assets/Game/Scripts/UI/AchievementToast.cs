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

        public static AchievementToast Instance { get; private set; }

        readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
        AchievementDef showing;
        float t;

        /// <summary>One is on the screen (or coming): for the tests.</summary>
        public bool Showing => showing != null;
        public string ShowingTitle => showing != null ? showing.title : null;

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
                if (queue.Count == 0) return;
                showing = queue.Dequeue();
                t = 0f;
                int i = System.Array.IndexOf(Achievements.All, showing);
                if (icon != null) icon.sprite = icons != null && i >= 0 && i < icons.Length ? icons[i] : null;
                if (title != null) title.text = showing.title;
                if (line != null) line.text = showing.description;
                root.gameObject.SetActive(true);
                Sfx.PlayUI("achievement", 0.8f);
            }
            t += Time.unscaledDeltaTime;
            float total = slideTime * 2f + holdTime;
            float k = t < slideTime ? Ease(t / slideTime) : t < slideTime + holdTime ? 1f : 1f - Ease((t - slideTime - holdTime) / slideTime);
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, Mathf.Lerp(HiddenY, shownY, k));
            if (t >= total) { showing = null; root.gameObject.SetActive(false); }
        }

        static float Ease(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    }
}
