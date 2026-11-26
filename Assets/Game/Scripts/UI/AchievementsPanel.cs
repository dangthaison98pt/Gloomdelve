using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The Achievements screen of the title menu, on one page: the overall statistics at the top (the menu fills
    /// them, onStats), then a grid of tiles, one for each achievement (Achievements.All). An earned one shows its
    /// picture in a gold frame; one not yet earned is dimmed in a grey frame behind a lock (a secret one shows a
    /// question mark). A tile pressed shows its name and what earns it in a box beside it (pressed again: hidden).
    /// </summary>
    public class AchievementsPanel : MonoBehaviour
    {
        public TMPro.TMP_Text count;
        public Image[] icons, frames, locks;
        public Button[] tiles;
        [Tooltip("The pictures of the achievements, in the order of Achievements.All.")] public Sprite[] pictures;
        [Tooltip("What a secret one not yet earned shows instead of its picture.")] public Sprite secretPicture;
        [Tooltip("The frame round an earned one, and round one not yet earned.")] public Sprite frameEarned, frameLocked;
        public Button back;
        [Tooltip("The box beside the tile in focus: its name and what earns it.")] public RectTransform tip;
        public TMPro.TMP_Text tipTitle, tipText;
        /// <summary>Called when the screen is shown (the menu fills the statistics).</summary>
        public System.Action onStats;
        [Tooltip("The window the grid scrolls in (more than two rows); the box is hidden when it moves.")] public ScrollRect scroll;

        int tipOf = -1;

        /// <summary>The first tile (where the focus starts), or Back when there is none.</summary>
        public Selectable First => tiles != null && tiles.Length > 0 && tiles[0] != null ? (Selectable)tiles[0] : back;

        void Awake() { if (scroll != null) scroll.onValueChanged.AddListener(_ => HideTip(tipOf)); }

        void OnEnable()
        {
            HideTip(tipOf);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;      // (from the top each time)
        }

        public void Refresh()
        {
            onStats?.Invoke();
            var all = Achievements.All;
            if (count != null) count.text = $"{Achievements.EarnedCount} / {all.Length}";
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                bool got = Achievements.IsEarned(a.id);
                bool secret = a.hidden && !got;
                if (icons != null && i < icons.Length && icons[i] != null)
                {
                    icons[i].sprite = secret && secretPicture != null ? secretPicture : pictures != null && i < pictures.Length ? pictures[i] : null;
                    icons[i].color = got ? Color.white : new Color(0.34f, 0.36f, 0.42f, 1f);
                }
                if (frames != null && i < frames.Length && frames[i] != null) frames[i].sprite = got ? frameEarned : frameLocked;
                if (locks != null && i < locks.Length && locks[i] != null) locks[i].enabled = !got;
            }
            if (tipOf >= 0) ShowTip(tipOf);
        }

        /// <summary>The box with the name and what earns it, beside tile i.</summary>
        public void ShowTip(int i)
        {
            var all = Achievements.All;
            if (tip == null || i < 0 || i >= all.Length || tiles == null || i >= tiles.Length || tiles[i] == null) return;
            var a = all[i];
            bool got = Achievements.IsEarned(a.id);
            bool secret = a.hidden && !got;
            tipOf = i;
            tipTitle.text = secret ? "???" : a.title;
            string on = got ? Achievements.EarnedOn(a.id) : null;
            tipText.text = (secret ? a.hint : a.description) + (on != null ? $"\n<size=80%><color=#FFFFFF80>Earned {on}</color></size>" : got ? "" : "\n<size=80%><color=#FFFFFF80>Not yet earned</color></size>");
            // its height follows the words; it sits beside the tile with a gap, to its right (or its left, near the right edge)
            float w = tip.sizeDelta.x;
            float h = 26f + tipTitle.GetPreferredValues(tipTitle.text, w - 36f, 0f).y + 8f + tipText.GetPreferredValues(tipText.text, w - 36f, 0f).y + 24f;
            tip.sizeDelta = new Vector2(w, h);
            var t = (RectTransform)tiles[i].transform;
            var parent = (RectTransform)tip.parent;
            Vector2 local = (Vector2)parent.InverseTransformPoint(t.position) - parent.rect.center;     // (from the middle of the panel: the box is anchored there, its pivot at its top left)
            bool left = local.x + t.rect.width * 0.5f + w > parent.rect.width * 0.5f - 20f;
            const float gap = 18f;
            tip.anchoredPosition = new Vector2(left ? local.x - t.rect.width * 0.5f - gap - w : local.x + t.rect.width * 0.5f + gap, local.y + t.rect.height * 0.5f);
            tip.gameObject.SetActive(true);
            tip.SetAsLastSibling();
        }

        /// <summary>Tile i pressed: its box shown, or hidden when it was showing.</summary>
        public void ToggleTip(int i)
        {
            if (tipOf == i) HideTip(i); else ShowTip(i);
        }

        public void HideTip(int i)
        {
            if (i != tipOf && i >= 0) return;
            tipOf = -1;
            if (tip != null) tip.gameObject.SetActive(false);
        }
    }
}
