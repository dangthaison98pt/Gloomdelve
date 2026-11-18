using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class HUD : MonoBehaviour
    {
        public TMPro.TMP_Text healthText;
        public TMPro.TMP_Text bombText;
        public TMPro.TMP_Text ropeText;
        public TMPro.TMP_Text goldText;
        public TMPro.TMP_Text stageText;
        public TMPro.TMP_Text timeText;
        public TMPro.TMP_Text totalTimeText;
        public TMPro.TMP_Text upgradesText;
        public GameObject promptPanel;
        public TMPro.TMP_Text promptText;
        public CanvasGroup bannerGroup;
        public TMPro.TMP_Text bannerTitle;
        public TMPro.TMP_Text bannerSub;
        [Header("Area introduction (start of a level)")]
        public CanvasGroup introGroup;
        public TMPro.TMP_Text introName;
        public RectTransform introRule;
        public RectTransform introGem;
        public float introRuleWidth = 560f;
        public CanvasGroup messageGroup;
        public TMPro.TMP_Text messageText;
        public CanvasGroup hurtGroup;
        public TMPro.TMP_Text hurtText;
        public Image damageVignette;
        [Header("Waypoint (Compass upgrade)")]
        public GameObject compassRoot;          // full-screen layer holding the marker
        public TMPro.TMP_Text compassText;                // distance label
        public RectTransform wpMarker;
        public RectTransform wpArrowPivot;
        public Image wpRing, wpPing, wpChevron, wpTail, wpIcon;
        public Sprite wpExitIcon, wpGoalIcon;
        public CanvasGroup wpGroup;
        public Color wpExitColor = new Color(0.36f, 1f, 0.82f);
        public Color wpGoalColor = new Color(1f, 0.45f, 0.62f);

        /// <summary>Waypoint state (read by the scenario tests).</summary>
        public bool WaypointVisible { get; private set; }
        public bool WaypointOnScreen { get; private set; }
        public Vector2 WaypointPos => wpMarker != null ? wpMarker.anchoredPosition : Vector2.zero;
        public Vector2 WaypointTarget { get; private set; }
        bool wpSnapped;
        float wpArrowA, wpTailA;
        public Image heartIcon;
        [Header("Equipped gear")]
        public Image equipIcon;
        public TMPro.TMP_Text equipName;
        public TMPro.TMP_Text equipAmmo;
        [Tooltip("Small icons of the equipment being worn.")] public Image[] wornIcons;
        [Header("Boss health bar")]
        public GameObject bossRoot;
        public TMPro.TMP_Text bossName;
        public Image bossFill;
        public float bossFillWidth = 752f;

        [Tooltip("The corner panels (health, gear, stage and clock): hidden while the result screen is up, which needs the room.")]
        public GameObject[] corners;

        GameDirector dir;
        float bannerT, msgT, hurtT, vignette;
        float introT = -1f, introLength = 3.6f;
        float shownGold;
        int lastHealth = -1, shownHealth = -1, shownMax = -1;
        float heartPulse;

        public void Bind(GameDirector d) { dir = d; shownGold = RunSession.Current != null ? RunSession.Current.gold : 0; }

        public void ShowBanner(string title, string sub, float seconds = 3.5f)
        {
            // an event banner takes the place of the area title card: the two share the same spot
            introT = -1f;
            if (introGroup != null) introGroup.alpha = 0f;
            bannerTitle.text = title;
            bannerSub.text = sub;
            bannerT = seconds;
        }

        /// <summary>
        /// The title card of a new place: its name, with a rule that draws itself underneath.
        /// Fades in, holds, fades out.
        /// </summary>
        public void ShowAreaIntro(string place, float seconds = 3.6f)
        {
            if (introGroup == null) { ShowBanner(place, "", seconds); return; }
            introName.text = (place ?? "").ToUpperInvariant();
            introT = 0f;
            introLength = Mathf.Max(1.5f, seconds);
            bannerT = 0f;
        }

        public void ShowMessage(string m, float seconds = 3.5f)
        {
            messageText.text = m;
            msgT = seconds;
        }

        public void ShowHurt(string cause, int amount)
        {
            hurtText.text = amount >= 50 ? cause : $"-{amount}  {cause}";
            hurtT = 2.2f;
            vignette = 0.55f;
        }

        void Update()
        {
            var run = RunSession.Current;
            if (run == null || dir == null) return;
            float dt = Time.unscaledDeltaTime;

            if (run.health != shownHealth || run.maxHealth != shownMax)
            {
                shownHealth = run.health; shownMax = run.maxHealth;
                healthText.text = run.maxHealth > 0 ? run.health + "/" + run.maxHealth : run.health.ToString();
            }
            if (lastHealth >= 0 && run.health != lastHealth) heartPulse = 0.4f;
            lastHealth = run.health;
            heartPulse -= dt;
            if (heartIcon != null) heartIcon.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, heartPulse) * 0.8f);
            // red when about a third of the health is left (at least at the last point)
            int lowHealth = Mathf.Max(1, Mathf.CeilToInt(run.maxHealth * 0.3f));
            healthText.color = run.health <= lowHealth ? new Color(1f, 0.35f, 0.35f) : Color.white;

            if (bombText != null) bombText.text = run.bombs.ToString();
            if (ropeText != null) ropeText.text = run.ropes.ToString();
            var gear = dir.config != null ? dir.config.FindGear(run.EquippedId) : null;
            if (equipIcon != null)
            {
                equipIcon.enabled = gear != null && gear.icon != null;
                if (gear != null) equipIcon.sprite = gear.icon;
                if (equipName != null) equipName.text = gear != null ? gear.displayName.ToUpperInvariant() : "-";
                int ammo = gear != null ? run.Ammo(gear.id) : 0;
                equipAmmo.text = gear != null && ammo >= 0 ? ammo.ToString() : "";
                equipAmmo.color = ammo > 0 ? Color.white : new Color(1f, 0.4f, 0.4f);
            }
            shownGold = Mathf.MoveTowards(shownGold, run.gold, Mathf.Max(60f, Mathf.Abs(run.gold - shownGold) * 4f) * dt);
            goldText.text = "$" + Mathf.RoundToInt(shownGold);

            if (dir.Stage != null)
                stageText.text = run.bossPractice ? "CHALLENGE"           // one word: the area name next to it did not fit the frame
                    : dir.Stage.kind == LevelKind.Area || dir.Stage.kind == LevelKind.Boss ? $"{dir.Stage.displayName}  {dir.Stage.area.displayName}" : dir.Stage.displayName;
            timeText.text = UIUtil.FormatTime(run.levelTime);
            if (dir.WraithActive) timeText.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.4f);
            else if (dir.PressureWarned) timeText.color = new Color(1f, 0.6f, 0.2f);
            else timeText.color = Color.white;
            totalTimeText.text = "TOTAL " + UIUtil.FormatTime(run.totalTime);

            // A screen over the game (pause, equipment, gear wheel, level summary, results): the passing texts of the
            // HUD - title card, banner, messages, the prompt - step aside so nothing shows through or under it.
            bool results = dir.results != null && dir.results.Showing;
            bool covered = dir.Paused || dir.Ended || results
                || (dir.gearWheel != null && dir.gearWheel.IsOpen)
                || (dir.inventory != null && dir.inventory.IsOpen)
                || (dir.transition != null && dir.transition.root != null && dir.transition.root.activeSelf);
            if (corners != null)
                foreach (var c in corners)
                    if (c != null && c.activeSelf == results) c.SetActive(!results);

            if (upgradesText != null) upgradesText.text = "";
            if (wornIcons != null)
            {
                int k = 0;
                foreach (var id in run.worn)
                {
                    var offer = run.Owned(id);
                    if (results || offer == null || offer.icon == null || k >= wornIcons.Length) continue;
                    wornIcons[k].sprite = offer.icon;
                    wornIcons[k].enabled = true;
                    k++;
                }
                for (; k < wornIcons.Length; k++) wornIcons[k].enabled = false;
            }

            var p = dir.Player;
            string prompt = p != null && !p.IsDead && !dir.Ended ? p.Prompt : null;
            if (covered) prompt = null;
            promptPanel.SetActive(!string.IsNullOrEmpty(prompt));
            if (!string.IsNullOrEmpty(prompt)) promptText.text = prompt;

            if (introGroup != null)
            {
                bool hideIntro = covered;
                if (introT >= 0f)
                {
                    introT += dt;
                    float fadeIn = Mathf.Clamp01(introT / 0.45f), fadeOut = Mathf.Clamp01((introLength - introT) / 0.7f);
                    introGroup.alpha = hideIntro ? 0f : Mathf.Min(fadeIn, fadeOut);
                    float draw = 1f - Mathf.Pow(1f - Mathf.Clamp01((introT - 0.15f) / 0.7f), 3f);       // ease out
                    introRule.sizeDelta = new Vector2(introRuleWidth * draw, introRule.sizeDelta.y);
                    introGem.localScale = Vector3.one * Mathf.Clamp01((introT - 0.5f) / 0.25f);
                    introName.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, fadeIn);
                    if (introT >= introLength) introT = -1f;
                }
                else introGroup.alpha = 0f;
            }
            bannerT -= dt; msgT -= dt; hurtT -= dt;
            bannerGroup.alpha = covered ? 0f : Mathf.Clamp01(bannerT);
            messageGroup.alpha = covered ? 0f : Mathf.Clamp01(msgT);
            hurtGroup.alpha = covered ? 0f : Mathf.Clamp01(hurtT);
            vignette = Mathf.Max(0f, vignette - dt * 0.8f);
            if (p != null && p.IsDead) vignette = Mathf.Max(vignette, 0.35f);
            damageVignette.color = new Color(0.8f, 0f, 0f, vignette);

            if (bossRoot != null)
            {
                var boss = dir.Boss;
                bool showBoss = boss != null && !boss.Dead && !dir.Ended && !results;
                if (bossRoot.activeSelf != showBoss) bossRoot.SetActive(showBoss);
                if (showBoss)
                {
                    bossName.text = boss.displayName.ToUpperInvariant();
                    bossFill.rectTransform.sizeDelta = new Vector2(bossFillWidth * boss.HealthFraction, bossFill.rectTransform.sizeDelta.y);
                }
            }

            UpdateWaypoint(run, p, dt);
        }

        // Screen-space objective marker: floats above the exit when it is on screen, otherwise rides the
        // screen edge with a chevron pointing towards it. Distance in tiles ("m").
        void UpdateWaypoint(RunState run, PlayerController p, float dt)
        {
            bool show = run.HasActive(UpgradeIds.Compass) && p != null && !p.IsDead && dir.Level != null && dir.cam != null && wpMarker != null;
            WaypointVisible = show;
            compassRoot.SetActive(show);
            if (!show) { wpSnapped = false; return; }

            bool goal = dir.Level.goal.HasValue && !dir.Collapsing;
            Vector2Int cell = goal ? dir.Level.goal.Value : dir.Level.exit;
            Vector2 world = new Vector2(cell.x + 0.5f, cell.y + 1.35f);   // top of the door / altar
            float dist = Vector2.Distance(new Vector2(cell.x + 0.5f, cell.y + 0.5f), p.Center);

            var root = (RectTransform)compassRoot.transform;
            Vector2 screen = dir.cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            WaypointTarget = local;

            Vector2 half = root.rect.size * 0.5f;
            const float side = 80f, top = 190f, bottom = 120f, lift = 125f;
            float minX = -half.x + side, maxX = half.x - side, minY = -half.y + bottom, maxY = half.y - top;

            bool onScreen = local.x > minX && local.x < maxX && local.y > minY && local.y < maxY - lift * 0.5f;
            Vector2 want;
            float t = Time.unscaledTime;
            if (onScreen)
            {
                want = local + new Vector2(0f, lift + Mathf.Sin(t * 2.4f) * 6f);
                want.y = Mathf.Min(want.y, maxY);
            }
            else
            {
                // clamp along the ray from the screen centre to the target
                Vector2 c = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                Vector2 d = local - c;
                float sx = d.x > 0f ? (maxX - c.x) / d.x : d.x < 0f ? (minX - c.x) / d.x : float.MaxValue;
                float sy = d.y > 0f ? (maxY - c.y) / d.y : d.y < 0f ? (minY - c.y) / d.y : float.MaxValue;
                want = c + d * Mathf.Min(1f, Mathf.Min(sx, sy));
            }
            WaypointOnScreen = onScreen;

            Vector2 pos = wpSnapped ? Vector2.Lerp(wpMarker.anchoredPosition, want, 1f - Mathf.Exp(-16f * dt)) : want;
            wpSnapped = true;
            wpMarker.anchoredPosition = pos;

            Vector2 toT = local - pos;
            if (!onScreen && toT.sqrMagnitude > 1f)
                wpArrowPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toT.y, toT.x) * Mathf.Rad2Deg);
            wpArrowA = Mathf.MoveTowards(wpArrowA, onScreen ? 0f : 1f, dt * 6f);
            wpTailA = Mathf.MoveTowards(wpTailA, onScreen ? 1f : 0f, dt * 6f);

            Color accent = goal ? wpGoalColor : wpExitColor;
            wpRing.color = accent;
            wpChevron.color = new Color(accent.r, accent.g, accent.b, wpArrowA);
            wpTail.color = new Color(accent.r, accent.g, accent.b, wpTailA);
            float ph = (t % 1.8f) / 1.8f;
            wpPing.rectTransform.localScale = Vector3.one * (1f + ph * 0.9f);
            wpPing.color = new Color(accent.r, accent.g, accent.b, (1f - ph) * 0.6f);
            wpIcon.sprite = goal ? wpGoalIcon : wpExitIcon;

            compassText.text = $"{Mathf.RoundToInt(dist)} m";
            bool hidden = dir.Paused || dir.Ended || (dir.gearWheel != null && dir.gearWheel.IsOpen);
            float a = hidden ? 0f : dist < 2.2f ? 0.35f : 1f;
            wpGroup.alpha = Mathf.MoveTowards(wpGroup.alpha, a, dt * 5f);
        }
    }
}
