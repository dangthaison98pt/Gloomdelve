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
        [Tooltip("The coin beside the gold count; in the village it becomes an ember and the count is the Ember Pouch.")] public Image goldIcon;
        public Sprite goldSprite, emberSprite;
        public TMPro.TMP_Text stageText;
        public TMPro.TMP_Text timeText;
        public TMPro.TMP_Text totalTimeText;
        public TMPro.TMP_Text upgradesText;
        [Header("What the thing in front of the player offers, says or reads")]
        [Tooltip("A small tag at the right edge of the screen: the button and what it does.")] public RectTransform tagRoot;
        public TMPro.TMP_Text tagText, tagSub;
        [Tooltip("Somebody speaking: their name on a plate, their words under it.")] public RectTransform talkRoot;
        public RectTransform talkPlate;
        public TMPro.TMP_Text talkName, talkText;
        [Tooltip("The button shown in the corner of the box once a sentence is all there: press it for the next.")] public GameObject talkMore;
        [Tooltip("Letters a second while somebody's words are written out.")] public float talkSpeed = 42f;
        [Tooltip("A sign being read: a pale card, dark writing.")] public RectTransform noteRoot;
        public TMPro.TMP_Text noteText;
        [Header("A thing picked up for the first time: a card with its picture and what it is")]
        public RectTransform cardRoot;
        public Image cardIcon;
        public TMPro.TMP_Text cardName, cardCount, cardText;
        [Tooltip("\"press to go on\" at the foot of the card (shown while the game waits for it).")] public GameObject cardHint;
        bool cardWaits;
        float cardShownAt, cardSavedScale = 1f;

        /// <summary>A card is up and the game stands still until it is put away (GameDirector reads the button).</summary>
        public bool CardModal => card != null && cardWaits;

        /// <summary>Puts the card away and lets the game go on (not in its first half second: a button already held must not skip it).</summary>
        public void DismissCard()
        {
            if (!CardModal || Time.unscaledTime - cardShownAt < 0.5f) return;
            card = null; cardWaits = false;
            Time.timeScale = cardSavedScale;
            Sfx.PlayUI("menu_select", 0.7f);
        }
        [Header("Things picked up again: a list at the left edge of the screen")]
        public RectTransform[] feedRows;
        public Image[] feedIcons;
        public TMPro.TMP_Text[] feedNames, feedAmounts;
        public float feedTime = 3.2f;

        class Gained { public string id, name, text; public Sprite icon; public int amount; public float t, age, y = float.NaN, bump; }
        readonly System.Collections.Generic.List<Gained> feed = new System.Collections.Generic.List<Gained>();
        readonly System.Collections.Generic.Queue<Gained> cards = new System.Collections.Generic.Queue<Gained>();
        Gained card;
        float cardA;

        /// <summary>A card is up (or waiting) / lines in the list at the left (tests).</summary>
        public bool CardUp => card != null || cards.Count > 0;
        public string CardName => card != null ? card.name : null;
        public int FeedCount => feed.Count;
        /// <summary>All that the lines for this thing add up to / how many lines it has (tests).</summary>
        public int FeedAmount(string id) { int n = 0; foreach (var f in feed) if (f.id == id) n += f.amount; return n; }
        public int FeedLines(string id) { int n = 0; foreach (var f in feed) if (f.id == id) n++; return n; }

        /// <summary>
        /// The player has got something. The first time in this save (ItemLog) - and whenever 'always' says so - it
        /// is shown on a card: its picture, its name, how many, what it is. After that it is a line in the list at
        /// the left edge - one line for every thing picked up, two of a kind included.
        /// </summary>
        public void Gain(string id, string name, Sprite icon, int amount, string description, bool always = false)
        {
            if (string.IsNullOrEmpty(name)) return;
            bool first = !string.IsNullOrEmpty(description) && (ItemLog.FirstTime(id) || always);
            QuestLog.Notify(QuestTrigger.Obtain, id, amount);
            if (first) { cards.Enqueue(new Gained { id = id, name = name, text = description, icon = icon, amount = amount }); return; }
            // every thing picked up has a line of its own, even two of a kind: each shows what that one was worth
            feed.Add(new Gained { id = id, name = name, icon = icon, amount = amount, t = feedTime });
            int max = feedRows != null ? feedRows.Length : 0;
            while (max > 0 && feed.Count > max) feed.RemoveAt(0);
        }

        [Tooltip("Where on its sheet each picture is drawn (made by the project builder).")] public IconFrames iconFrames;
        readonly System.Collections.Generic.Dictionary<Image, Vector2> iconHome = new System.Collections.Generic.Dictionary<Image, Vector2>();

        /// <summary>
        /// Shows a thing's picture in a square of 'box' pixels: at its own pixels times 'scale', what is drawn of it
        /// (not the empty margin of its sheet) set in the middle of the square.
        /// </summary>
        public void FitIcon(Image img, Sprite s, float box, float scale)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            if (!iconHome.TryGetValue(img, out var home)) { home = rt.anchoredPosition; iconHome[img] = home; }
            img.sprite = s;
            img.preserveAspect = false;
            if (s == null) return;
            float ppu = s.pixelsPerUnit;
            Vector2 sheet = s.rect.size / ppu, min = -s.pivot / ppu, max = min + sheet;       // the whole sheet, about the pivot
            if (iconFrames != null && iconFrames.Find(s, out var fr))
            {
                Vector2 corner = -s.pivot / ppu;
                min = corner + new Vector2(fr.xMin * sheet.x, fr.yMin * sheet.y);
                max = corner + new Vector2(fr.xMax * sheet.x, fr.yMax * sheet.y);
            }
            Vector2 drawn = max - min, mid = (min + max) * 0.5f;
            // Every picture at the same scale - 'scale' screen pixels to one of its own - so a small thing stays small
            // and a big one big, as they are in the world; only one too big for the square is brought down to fit.
            float k = Mathf.Min(scale * ppu, box / Mathf.Max(0.0001f, Mathf.Max(drawn.x, drawn.y)));               // screen pixels to a unit
            Vector2 sheetMid = -s.pivot / ppu + sheet * 0.5f;
            rt.sizeDelta = sheet * k;
            rt.anchoredPosition = home - (mid - sheetMid) * k;
        }

        void GainedUpdate(float dt, bool covered)
        {
            // the card: one at a time, long enough to read
            // (it waits for the place's name and for a banner to leave the screen: they are written where it goes)
            if (card == null && cards.Count > 0 && !covered && introT < 0f && bannerT <= 0f)
            {
                card = cards.Dequeue();
                card.t = ReadTime(card.text) + 0.5f;
                // A new thing is worth reading, and there is no reading in the middle of a fight: the game stands
                // still until the card is put away with a button. (The test bots press nothing: for them it passes.)
                cardWaits = !ScenarioRunner.Active && !AutoTest.Active;
                if (cardWaits)
                {
                    cardShownAt = Time.unscaledTime;
                    cardSavedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                    Time.timeScale = 0f;
                }
                if (cardHint != null) cardHint.SetActive(cardWaits);
                if (cardRoot != null)
                {
                    cardName.text = card.name;
                    cardText.text = card.text;
                    cardCount.text = "\u00D7" + Mathf.Max(1, card.amount);
                    cardIcon.enabled = card.icon != null;
                    FitIcon(cardIcon, card.icon, 120f, 7f);
                    // name, count and words as one block, in the middle of the card's height (level with the picture)
                    float words = Mathf.Min(cardText.GetPreferredValues(card.text, cardText.rectTransform.rect.width, 0f).y, 260f), top = (52f + 44f + words) * 0.5f;      // (asked for at its own width: before it is laid out it would answer for one line)
                    // the card is as tall as what is written on it needs (three lines used to run over its lower rule)
                    cardText.rectTransform.sizeDelta = new Vector2(cardText.rectTransform.sizeDelta.x, words + 8f);
                    cardRoot.sizeDelta = new Vector2(cardRoot.sizeDelta.x, Mathf.Max(210f, 52f + 44f + words + 88f));
                    cardName.rectTransform.anchoredPosition = new Vector2(cardName.rectTransform.anchoredPosition.x, top);
                    cardCount.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(cardName.rectTransform.anchoredPosition.x + 2f, top - 54f);
                    cardText.rectTransform.anchoredPosition = new Vector2(cardName.rectTransform.anchoredPosition.x, top - 96f);
                }
            }
            if (card != null && !cardWaits) { card.t -= dt; if (card.t <= 0f) card = null; }
            if (cardRoot != null) Fade(cardRoot, card != null && !covered, ref cardA, Mathf.Max(dt, Time.deltaTime));
            // The list: the newest line lowest, each for a few seconds. Nothing in it jumps: a line slides in from
            // the edge as it fades in, slides back out as it fades, the lines under one that has gone glide up into
            // its place, and a number that has just grown swells for a moment.
            const float step = 52f, outTime = 0.35f, inTime = 0.22f;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var f = feed[i];
                f.t -= dt; f.age += dt; f.bump = Mathf.MoveTowards(f.bump, 0f, dt / 0.28f);
                if (f.t <= -outTime) feed.RemoveAt(i);
            }
            if (feedRows == null) return;
            for (int i = 0; i < feedRows.Length; i++)
            {
                bool on = i < feed.Count && !covered;
                if (feedRows[i].gameObject.activeSelf != on) feedRows[i].gameObject.SetActive(on);
                if (!on) continue;
                var f = feed[i];
                float slot = 90f - i * step;
                f.y = float.IsNaN(f.y) ? slot : Mathf.Lerp(f.y, slot, 1f - Mathf.Exp(-14f * dt));
                float into = Mathf.Clamp01(f.age / inTime), outOf = f.t < 0f ? Mathf.Clamp01(-f.t / outTime) : 0f;
                float ease = 1f - (1f - into) * (1f - into);                         // out of the edge, slowing as it arrives
                feedRows[i].anchoredPosition = new Vector2(18f - 70f * (1f - ease) - 40f * outOf * outOf, f.y);
                var cg = feedRows[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = ease * (1f - outOf);
                if (feedNames[i].text != f.name) feedNames[i].text = f.name;
                string amt = "+" + f.amount;
                if (feedAmounts[i].text != amt) feedAmounts[i].text = amt;
                feedAmounts[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(f.bump * Mathf.PI));
                feedIcons[i].enabled = f.icon != null;
                if (feedIcons[i].sprite != f.icon) FitIcon(feedIcons[i], f.icon, 40f, 3f);
            }
        }

        [Header("The quest in hand: its name and what to do next (top right)")]
        public RectTransform questRoot;
        public TMPro.TMP_Text questTitle, questStep;
        float questA;

        /// <summary>The quest the screen is showing, and what it says to do (tests).</summary>
        public string QuestShown => questRoot != null && questRoot.gameObject.activeSelf && questTitle != null ? questTitle.text : null;
        public string QuestStepShown => questRoot != null && questRoot.gameObject.activeSelf && questStep != null ? questStep.text : null;

        void QuestUpdate(bool covered)
        {
            if (questRoot == null) return;
            var q = QuestLog.Tracked;
            bool on = q != null && !covered;
            if (on)
            {
                string step = QuestLog.StepText(q);
                if (questTitle.text != q.title) questTitle.text = q.title;
                if (questStep.text != step)
                {
                    questStep.text = step;
                    // a step that takes two lines (or three) to say: the frame grows down to hold them
                    var stepAt = questStep.rectTransform;
                    float lines = questStep.GetPreferredValues(step, stepAt.sizeDelta.x, 0f).y;
                    stepAt.sizeDelta = new Vector2(stepAt.sizeDelta.x, Mathf.Max(48f, lines + 4f));
                    questRoot.sizeDelta = new Vector2(questRoot.sizeDelta.x, Mathf.Max(104f, -stepAt.anchoredPosition.y + lines + 22f));
                }
                // in the village the clock's corner is put away: the quest moves up into its place
                var run = RunSession.Current;
                questRoot.anchoredPosition = new Vector2(questRoot.anchoredPosition.x, run != null && run.inVillage ? -16f : -178f);
            }
            Fade(questRoot, on, ref questA, Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime));
        }

        string shownPrompt;
        float promptPop, tagA, talkA, noteA;

        /// <summary>
        /// Brings one of the prompt's places in or takes it out, softly: it fades in quickly and out a little more
        /// slowly, still showing what it showed - so walking past a sign, in and out of its reach, does not make
        /// a card flash on and off.
        /// </summary>
        static void Fade(RectTransform root, bool on, ref float a, float dt)
        {
            a = Mathf.MoveTowards(a, on ? 1f : 0f, dt / (on ? 0.14f : 0.3f));
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) { cg = root.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false; }
            cg.alpha = a;
            bool show = a > 0.001f;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        }
        string heldNote, heldSpeaker, heldSpeech;
        float heldNoteT, heldTalkT;
        Vector2 heldAt;

        /// <summary>Something read at the press of a button (a letter, a shelf): the same card a sign shows, for a while
        /// or until the player walks away.</summary>
        public void ShowNote(string text, float seconds)
        {
            heldNote = text; heldNoteT = seconds; heldTalkT = 0f;
            if (dir != null && dir.Player != null) heldAt = dir.Player.pos;
            promptPop = 0f;
        }

        /// <summary>
        /// Somebody answers when spoken to: their name, and their words written out a sentence at a time. The
        /// button (AdvanceTalk) finishes the sentence being written, then brings the next, then closes the box;
        /// walking away closes it too. ('seconds' is not used any more: words wait to be read.)
        /// </summary>
        public void ShowTalk(string speaker, string text, float seconds = 0f, System.Action heardOut = null)
        {
            heldSpeaker = speaker; heldTalkT = 1f; heldNoteT = 0f;
            talkHeardOut = heardOut;       // (called when the last sentence is put away - not when the player walks off)
            talkPages.Clear();
            talkPages.AddRange(Sentences(text));
            if (talkPages.Count == 0) talkPages.Add(text ?? "");
            talkPage = 0; talkShown = 0f;
            heldSpeech = talkPages[0];
            if (dir != null && dir.Player != null) heldAt = dir.Player.pos;
            promptPop = 0f;
        }

        readonly System.Collections.Generic.List<string> talkPages = new System.Collections.Generic.List<string>();
        int talkPage;
        float talkShown;
        System.Action talkHeardOut;
        string hiddenSign;

        /// <summary>The sentence on show is still being written out.</summary>
        public bool TalkTyping => heldTalkT > 0f && heldSpeech != null && talkShown < heldSpeech.Length;
        public int TalkPage => talkPage;
        public int TalkPages => talkPages.Count;

        /// <summary>The button while somebody speaks: all of this sentence at once; or the next one; or, after the last, the end of it.</summary>
        public void AdvanceTalk()
        {
            if (heldTalkT <= 0f) return;
            if (TalkTyping) { talkShown = heldSpeech.Length; return; }
            if (talkPage + 1 < talkPages.Count) { talkPage++; heldSpeech = talkPages[talkPage]; talkShown = 0f; Sfx.PlayUI("menu_select", 0.5f); return; }
            heldTalkT = 0f;
            var done = talkHeardOut; talkHeardOut = null;
            done?.Invoke();
        }

        /// <summary>Cuts what somebody says into sentences (the box shows one at a time).</summary>
        public static System.Collections.Generic.List<string> Sentences(string text)
        {
            var list = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            int from = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                bool end = (ch == '.' || ch == '!' || ch == '?') && (i + 1 >= text.Length || text[i + 1] == ' ') && !(i + 1 < text.Length - 1 && text[i + 1] == '.');
                if (!end) continue;
                string part = text.Substring(from, i + 1 - from).Trim();
                if (part.Length > 0) list.Add(part);
                from = i + 1;
            }
            string rest = text.Substring(from).Trim();
            if (rest.Length > 0) list.Add(rest);
            return list;
        }

        /// <summary>A sign's card is put away (the button, at the sign) until the player comes back to it - or taken out again.</summary>
        public void ToggleSign(string prompt) { hiddenSign = hiddenSign == prompt ? null : prompt; promptPop = 0f; }
        /// <summary>A sign's card is on the screen.</summary>
        public bool SignUp { get; private set; }
        public void HideSign() { if (SignUp) hiddenSign = shownPrompt; }

        /// <summary>How long words stay up: long enough to read them.</summary>
        public static float ReadTime(string text) => Mathf.Clamp(2.5f + (text != null ? text.Length : 0) * 0.05f, 3.5f, 11f);

        /// <summary>A note or somebody's words are up (tests).</summary>
        /// <summary>Puts down what is being read or heard (the same button again, or the pause button).</summary>
        public void DismissHeld() { heldNoteT = 0f; heldTalkT = 0f; }
        public bool HeldUp => heldNoteT > 0f || heldTalkT > 0f;
        public bool NoteUp => heldNoteT > 0f;
        public bool TalkUp => heldTalkT > 0f;

        /// <summary>
        /// Takes a prompt apart. The things of the world still hand over one line - "[enter]  open the chest",
        /// "Elder Aldous:  \"...\"", "[enter]  free Tam      Tam:  \"...\"", "[enter]  buy rope for $20  -  what it does" -
        /// and the screen shows each part where it belongs: what to press as a tag at the right edge ('action', with
        /// a smaller second line 'sub'), spoken words in a box with the speaker's name, the writing on a sign as a card.
        /// </summary>
        public static void SplitPrompt(string prompt, bool sign, out string action, out string sub, out string speaker, out string speech, out string note)
        {
            action = sub = speaker = speech = note = null;
            if (string.IsNullOrEmpty(prompt)) return;
            if (sign) { note = prompt.Trim(); return; }
            int q0 = prompt.IndexOf('"'), q1 = prompt.LastIndexOf('"');
            if (q0 >= 0 && q1 > q0)
            {
                speech = prompt.Substring(q0 + 1, q1 - q0 - 1).Trim();
                string pre = prompt.Substring(0, q0).Trim();
                if (pre.EndsWith(":"))
                {
                    pre = pre.Substring(0, pre.Length - 1);
                    int gap = pre.LastIndexOf("    ");
                    if (gap >= 0) { speaker = pre.Substring(gap).Trim(); pre = pre.Substring(0, gap).Trim(); }
                    else { speaker = pre.Trim(); pre = ""; }
                }
                if (pre.Length > 0) action = pre;
                return;
            }
            if (prompt.IndexOf('[') >= 0)
            {
                action = prompt.Trim();
                int dash = action.IndexOf("  -  ");
                if (dash >= 0) { sub = action.Substring(dash + 5).Trim(); action = action.Substring(0, dash).Trim(); }
                return;
            }
            speech = prompt.Trim();      // told, not spoken by anyone: a locked door, a dark house
        }
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
        private string lastBossName = string.Empty;
        [Header("Dash energy")]
        [Tooltip("The bar under the health and the gold: what is left for dashing.")] public Image energyFill;
        public float energyFillWidth = 440f;
        public Color energyColor = new Color(0.45f, 0.9f, 1f);
        [Tooltip("The bar when there is not enough left for a dash.")] public Color energyLowColor = new Color(0.95f, 0.45f, 0.4f);
        int shownRefusals;
        float energyFlash;
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
            if (energyFill != null)
            {
                var pl = dir.Player;
                float frac = pl != null && pl.DashEnergyMax > 0f ? Mathf.Clamp01(pl.DashEnergy / pl.DashEnergyMax) : 1f;
                var rt = energyFill.rectTransform;
                rt.sizeDelta = new Vector2(energyFillWidth * frac, rt.sizeDelta.y);
                // asked for a dash it could not pay for: the bar flashes white
                if (pl != null && pl.DashRefusals != shownRefusals) { shownRefusals = pl.DashRefusals; energyFlash = 0.3f; }
                energyFlash -= dt;
                Color c = pl == null || pl.CanAffordDash ? energyColor : energyLowColor;
                energyFill.color = energyFlash > 0f ? Color.Lerp(c, Color.white, energyFlash / 0.3f) : c;
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
            if (run.inVillage)
            {
                bool pouch = Villagers.HasKey(Villagers.KeyPouch);
                goldText.text = pouch ? Villagers.Embers + " / " + Villagers.PouchCapacity : "-";
                if (goldIcon != null && emberSprite != null) goldIcon.sprite = emberSprite;
            }
            else
            {
                goldText.text = "$" + Mathf.RoundToInt(shownGold);
                if (goldIcon != null && goldSprite != null) goldIcon.sprite = goldSprite;
            }

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
                || (dir.codex != null && dir.codex.IsOpen)
                || (dir.villageShop != null && dir.villageShop.IsOpen)
                || (dir.transition != null && dir.transition.root != null && dir.transition.root.activeSelf);
            // in the village only health and gold are shown: no gear to use there and no clock running
            if (corners != null)
                for (int ci = 0; ci < corners.Length; ci++)
                {
                    bool show = !results && !(run.inVillage && ci > 0);
                    if (corners[ci] != null && corners[ci].activeSelf != show) corners[ci].SetActive(show);
                }

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
            GainedUpdate(dt, covered);
            SplitPrompt(prompt, p != null && p.PromptIsSign, out string act, out string sub, out string who, out string said, out string note);
            // what was read or said at the press of a button stays up for a while - unless the player walks off
            heldNoteT -= dt; heldTalkT -= dt;
            if (covered || p == null || (p.pos - heldAt).sqrMagnitude > 9f) { heldNoteT = 0f; heldTalkT = 0f; talkHeardOut = null; }
            QuestUpdate(covered);
            heldTalkT = heldTalkT > 0f ? 1f : 0f;      // (somebody's words wait to be read: only the button or walking off ends them)
            if (hiddenSign != null && hiddenSign != prompt) hiddenSign = null;       // left the sign: next time it shows again
            SignUp = note != null && hiddenSign == null;
            if (note != null && hiddenSign != null) note = null;
            bool written = false;
            if (heldNoteT > 0f) { note = heldNote; said = null; who = null; }
            else if (heldTalkT > 0f) { said = heldSpeech; who = heldSpeaker; note = null; written = true; talkShown += dt * talkSpeed; }
            if (prompt != shownPrompt) { shownPrompt = prompt; promptPop = 0f; }
            promptPop += dt;
            if (tagRoot != null)
            {
                bool on = !string.IsNullOrEmpty(act);
                Fade(tagRoot, on, ref tagA, Mathf.Max(dt, Time.deltaTime));
                if (on)
                {
                    bool two = !string.IsNullOrEmpty(sub);
                    if (tagText.text != act) tagText.text = act;
                    if (tagSub.gameObject.activeSelf != two) tagSub.gameObject.SetActive(two);
                    if (two && tagSub.text != sub) tagSub.text = sub;
                    float w = Mathf.Max(tagText.preferredWidth, two ? tagSub.preferredWidth : 0f);
                    // One row as a rule. Somebody who offers several things has a row for each (the button sits on the
                    // chosen one): the tag grows downward by the rows, and the small line goes under the last of them.
                    float rowsH = Mathf.Max(58f, tagText.preferredHeight + 22f);
                    float subH = two ? Mathf.Max(30f, tagSub.preferredHeight) : 0f;
                    tagText.rectTransform.sizeDelta = new Vector2(tagText.rectTransform.sizeDelta.x, rowsH);
                    if (two)
                    {
                        tagSub.rectTransform.anchoredPosition = new Vector2(tagSub.rectTransform.anchoredPosition.x, -(rowsH - 2f));
                        tagSub.rectTransform.sizeDelta = new Vector2(tagSub.rectTransform.sizeDelta.x, subH);
                    }
                    tagRoot.sizeDelta = new Vector2(Mathf.Clamp(w + 110f, 260f, 1100f), two ? rowsH + 4f + subH : rowsH);
                }
            }
            if (talkRoot != null)
            {
                bool on = !string.IsNullOrEmpty(said);
                Fade(talkRoot, on, ref talkA, Mathf.Max(dt, Time.deltaTime));
                if (on)
                {
                    bool named = !string.IsNullOrEmpty(who);
                    if (talkPlate.gameObject.activeSelf != named) talkPlate.gameObject.SetActive(named);
                    if (named)
                    {
                        if (talkName.text != who) talkName.text = who;
                        talkPlate.sizeDelta = new Vector2(talkName.preferredWidth + 44f, talkPlate.sizeDelta.y);
                    }
                    if (talkText.text != said) talkText.text = said;
                    talkText.maxVisibleCharacters = written ? Mathf.FloorToInt(talkShown) : 99999;
                    if (talkMore != null) { bool more = written && !TalkTyping; if (talkMore.activeSelf != more) talkMore.SetActive(more); }
                    talkRoot.sizeDelta = new Vector2(talkRoot.sizeDelta.x, Mathf.Clamp(talkText.preferredHeight + (named ? 78f : 56f), 110f, 360f));
                    talkText.rectTransform.anchoredPosition = new Vector2(44f, named ? -46f : -28f);
                }
            }
            if (noteRoot != null)
            {
                bool on = !string.IsNullOrEmpty(note);
                Fade(noteRoot, on, ref noteA, Mathf.Max(dt, Time.deltaTime));
                if (on)
                {
                    if (noteText.text != note) noteText.text = note;
                    noteRoot.sizeDelta = new Vector2(noteRoot.sizeDelta.x, Mathf.Clamp(noteText.preferredHeight + 56f, 96f, 360f));
                }
            }

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
                    if (boss.displayName != lastBossName)
                    {
                        lastBossName = boss.displayName;
                        bossName.text = string.IsNullOrEmpty(lastBossName) ? string.Empty
                            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lastBossName.ToLowerInvariant());
                    }
                    bossFill.rectTransform.sizeDelta = new Vector2(bossFillWidth * boss.HealthFraction, bossFill.rectTransform.sizeDelta.y);
                }
            }

            UpdateWaypoint(run, p, dt);
        }

        // Screen-space objective marker: floats above the exit when it is on screen, otherwise rides the
        // screen edge with a chevron pointing towards it. Distance in tiles ("m").
        void UpdateWaypoint(RunState run, PlayerController p, float dt)
        {
            // the quest in hand comes first: its step may point at something in this level (QuestLog.Waypoint)
            Vector2 questAt = default;
            bool quest = p != null && !p.IsDead && dir.cam != null && wpMarker != null && QuestLog.Waypoint(World.Instance, p.Center, out questAt);
            bool show = quest || (run.HasActive(UpgradeIds.Compass) && p != null && !p.IsDead && dir.Level != null && dir.cam != null && wpMarker != null);
            WaypointVisible = show;
            compassRoot.SetActive(show);
            if (!show) { wpSnapped = false; return; }

            bool goal = dir.Level.goal.HasValue && !dir.Collapsing;
            Vector2Int cell = goal ? dir.Level.goal.Value : dir.Level.exit;
            Vector2 world = new Vector2(cell.x + 0.5f, cell.y + 1.35f);   // top of the door / altar
            float dist = Vector2.Distance(new Vector2(cell.x + 0.5f, cell.y + 0.5f), p.Center);
            if (quest) { world = questAt; dist = Vector2.Distance(questAt, p.Center); goal = true; }
            // in the village the way anywhere is through a door: out of the room one is in, or into the house the
            // thing is in - never a straight line to a room that lies somewhere else in the level
            if (RunSession.Current != null && RunSession.Current.inVillage)
            {
                Vector2 way = VillageInterior.WayTo(World.Instance, p.Center, world);
                if (way != world) { world = way; dist = Vector2.Distance(way, p.Center); }
            }

            var root = (RectTransform)compassRoot.transform;
            Vector2 screen = dir.cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            WaypointTarget = local;

            Vector2 half = root.rect.size * 0.5f;
            const float side = 80f, top = 190f, bottom = 120f, lift = 72f;       // (lift: how far over the thing it hangs)
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
