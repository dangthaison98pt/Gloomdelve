using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The Achievements screen of the title menu: one row for each (Achievements.All) - its picture, its name and
    /// what earned it; one not yet earned is dimmed behind a lock, a secret one shows only "???" and a hint.
    /// </summary>
    public class AchievementsPanel : MonoBehaviour
    {
        public TMPro.TMP_Text count;
        public Image[] icons;
        public TMPro.TMP_Text[] titles, lines;
        public Image[] locks;
        [Tooltip("The pictures of the achievements, in the order of Achievements.All.")] public Sprite[] pictures;
        [Tooltip("What a secret one not yet earned shows instead of its picture.")] public Sprite secretPicture;
        public Button back;
        [Tooltip("The two tabs at the top: the achievements, the records (runs and Challenge times).")] public Button tabAchievements, tabRecords;
        public GameObject listGroup, recordsGroup;
        public TMPro.TMP_Text tabAchievementsLabel, tabRecordsLabel;
        /// <summary>Called when the Records tab is shown (the menu fills its texts).</summary>
        public System.Action onRecords;

        public bool ShowingRecords { get; private set; }

        void Awake()
        {
            if (tabAchievements != null) tabAchievements.onClick.AddListener(() => ShowTab(false));
            if (tabRecords != null) tabRecords.onClick.AddListener(() => ShowTab(true));
        }

        /// <summary>The achievements (false) or the records (true).</summary>
        public void ShowTab(bool records)
        {
            ShowingRecords = records;
            if (records) onRecords?.Invoke(); else Refresh();
            if (listGroup != null) listGroup.SetActive(!records);
            if (recordsGroup != null) recordsGroup.SetActive(records);
            var on = new Color(1f, 0.84f, 0.38f); var off = new Color(1f, 1f, 1f, 0.5f);
            if (tabAchievementsLabel != null) tabAchievementsLabel.color = records ? off : on;
            if (tabRecordsLabel != null) tabRecordsLabel.color = records ? on : off;
        }

        public void Refresh()
        {
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
                    icons[i].color = got || secret ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                }
                if (locks != null && i < locks.Length && locks[i] != null) locks[i].enabled = !got;
                if (titles != null && i < titles.Length && titles[i] != null)
                {
                    titles[i].text = secret ? "???" : a.title;
                    titles[i].color = got ? new Color(1f, 0.84f, 0.38f) : new Color(1f, 1f, 1f, 0.55f);
                }
                if (lines != null && i < lines.Length && lines[i] != null)
                {
                    string on = got ? Achievements.EarnedOn(a.id) : null;
                    lines[i].text = secret ? a.hint : a.description + (on != null ? $"  <color=#FFFFFF66>({on})</color>" : "");
                    lines[i].color = got ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }
    }
}
