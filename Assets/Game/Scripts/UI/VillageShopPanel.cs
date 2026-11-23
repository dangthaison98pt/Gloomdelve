using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The shop of a villager of Brindlemoor, as a screen: who it is, a line, and their wares in a list - picture,
    /// name, what it is for, and the price in embers. Nothing is laid out on the floor in the village (that is how
    /// the shops on the mountain look). The elder's work on the bags (Bags) is offered the same way.
    /// It is only the list: whether to talk or to open the shop is chosen in the world, at the prompt beside
    /// whoever it is (IInteractChoices) - no screen opens for that.
    /// The game stands still while it is open. Confirm buys the row in focus; Close, the pause button or a
    /// gamepad's back button shuts it (GameDirector).
    /// Pictures are all drawn at the same size of pixel (HUD.FitIcon), so a small thing is small beside a big one.
    /// </summary>
    public class VillageShopPanel : MonoBehaviour
    {
        /// <summary>One row: a ware of the shop list, or - with 'bag' set - the elder's enlarging of that bag.</summary>
        public class Entry
        {
            public ShopOffer offer;
            public int price;
            public string bag;
            /// <summary>Paid in embers (what lasts: the elder's work, the map); otherwise in sparks.</summary>
            public bool lasting;
        }

        public GameObject root;
        public TMPro.TMP_Text title, line, embers, note;
        [Tooltip("One button per ware (the rows that are not needed are hidden).")] public Button[] rows;
        public Image[] icons;
        public TMPro.TMP_Text[] names, descs, prices;
        public Button closeButton;
        [Header("Layout")]
        [Tooltip("Pictures: screen units to one pixel of the picture - the same for every ware, so none looks coarser than the next - and the square they are kept inside.")]
        public float iconScale = 4f;
        public float iconBox = 64f;
        [Tooltip("Height of the first row's top edge under the top of the panel, and from one row to the next.")] public float firstRowY = -222f;
        public float rowStep = 96f;
        [Tooltip("Height of the panel without any row; each row adds one step.")] public float baseHeight = 368f;
        public RectTransform panel;

        readonly List<Entry> entries = new List<Entry>();
        bool open;
        float savedScale = 1f;

        public bool IsOpen => open;
        public int Count => entries.Count;
        public Entry At(int i) => i >= 0 && i < entries.Count ? entries[i] : null;
        public string Note => note != null ? note.text : "";

        void Awake()
        {
            if (rows != null)
                for (int i = 0; i < rows.Length; i++) { int k = i; UIUtil.Wire(rows[i], () => Buy(k)); }
            UIUtil.Wire(closeButton, Close);
            if (root != null) root.SetActive(false);
        }

        /// <summary>The wares a villager has out for this run, as rows.</summary>
        public static List<Entry> WaresOf(VillagerDef who, GameConfig cfg, RunState run)
        {
            var list = new List<Entry>();
            if (who == null || cfg == null || cfg.shop == null) return list;
            foreach (var o in Villagers.WaresOf(who, cfg.shop, run != null ? run.seed : 0))
                list.Add(new Entry { offer = o, price = Villagers.EmberPrice(o, who), lasting = Villagers.PaidInEmbers(o) });
            return list;
        }

        /// <summary>The elder's work: one row for each of the four bags.</summary>
        public static List<Entry> BagWork(GameConfig cfg)
        {
            var list = new List<Entry>();
            foreach (var kind in Bags.All)
            {
                Sprite pic = null;
                if (cfg != null && cfg.shop != null && kind != Bags.Embers)
                {
                    var like = cfg.shop.Find(kind == Bags.Bombs ? "bombs" : kind == Bags.Ropes ? "ropes" : "pebblepouch");
                    if (like != null) pic = like.icon;
                }
                if (kind == Bags.Embers && cfg != null && cfg.emberPouchIcon != null) pic = cfg.emberPouchIcon;       // the pouch itself, not what goes in it
                else if (kind == Bags.Embers && cfg != null && cfg.prefabs != null)
                {
                    var ember = cfg.prefabs.Get("ember");
                    var esr = ember != null ? ember.GetComponentInChildren<SpriteRenderer>() : null;
                    if (esr != null) pic = esr.sprite;
                }
                list.Add(new Entry { bag = kind, lasting = true, offer = new ShopOffer { id = "bag:" + kind, displayName = "Larger " + Bags.Name(kind), icon = pic } });
            }
            return list;
        }

        public void Open(string who, string say, List<Entry> list)
        {
            if (root == null || list == null) return;
            entries.Clear();
            entries.AddRange(list);
            if (rows != null && entries.Count > rows.Length) entries.RemoveRange(rows.Length, entries.Count - rows.Length);
            if (title != null) title.text = who ?? "";
            if (line != null) line.text = say ?? "";
            if (note != null) note.text = "";
            if (!open) { savedScale = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f; }
            open = true;
            root.SetActive(true);
            Refresh();
            Selectable first = closeButton;
            if (rows != null) for (int i = 0; i < entries.Count && i < rows.Length; i++) if (rows[i].interactable) { first = rows[i]; break; }
            UIUtil.Select(first);
        }

        /// <summary>The pause button or a gamepad's back button: the same as Close.</summary>
        public void Back() => Close();

        public void Close()
        {
            if (!open) return;
            open = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = savedScale;
            var dir = GameDirector.Instance;
            if (dir != null && dir.input != null) dir.input.ClearLatches();      // (the press that shut it is not also a jump)
        }

        /// <summary>
        /// Why this row cannot be bought now (null: it can; the price is not looked at here): a word or two for the
        /// row itself, and the sentence said under the list when it is tried all the same.
        /// </summary>
        string Blocked(Entry e, RunState run, out string sentence)
        {
            sentence = null;
            if (e.bag != null)
            {
                if (Bags.CanEnlarge(e.bag)) return null;
                sentence = e.bag == Bags.Embers && !Villagers.HasKey(Villagers.KeyPouch) ? "There is no pouch to let out yet." : "It is as large as it gets.";
                return "MAX";
            }
            if (run == null || e.offer == null) { sentence = "Not now."; return ""; }
            if (e.offer.id == MapScreen.ItemId && MapScreen.Carried) { sentence = "You have it already, and it is yours for good."; return "OWNED"; }
            if (e.offer.isUpgrade && run.HasUpgrade(e.offer.id)) { sentence = "That is yours already."; return "OWNED"; }
            if (e.offer.resource == Res.Health && run.HealthFull) { sentence = "Your health is full."; return "FULL"; }
            if (!string.IsNullOrEmpty(e.offer.resource) && run.CapOf(e.offer.resource) != int.MaxValue && run.RoomFor(e.offer.resource) <= 0)
            {
                sentence = $"Your bag is full: it holds {run.CapOf(e.offer.resource)}.";
                return "BAG FULL";
            }
            return null;
        }

        void Refresh()
        {
            var run = RunSession.Current;
            if (embers != null) embers.text = Villagers.HasKey(Villagers.KeyPouch)
                ? $"[spark] {Villagers.Sparks}          [ember] {Villagers.Embers} / {Villagers.PouchCapacity}" : "No pouch for [spark] and [ember] yet";       // (a number and its picture, never the word)
            int n = entries.Count;
            var hud = GameDirector.Instance != null ? GameDirector.Instance.hud : null;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                bool on = i < n;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var e = entries[i];
                var rt = (RectTransform)rows[i].transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, firstRowY - rowStep * i);
                string blocked = Blocked(e, run, out _);
                if (e.bag != null)
                {
                    e.price = Bags.Price(e.bag);
                    int holds = Bags.Holds(e.bag, run);
                    e.offer.description = blocked == null ? $"holds {holds} now, {holds + Bags.Step} after - for good" : $"holds {holds}";
                }
                if (icons != null && i < icons.Length && icons[i] != null)
                {
                    // every picture at the same size of pixel, in the middle of its square
                    icons[i].enabled = e.offer.icon != null;
                    if (hud != null) hud.FitIcon(icons[i], e.offer.icon, iconBox, iconScale);
                    else icons[i].sprite = e.offer.icon;
                }
                if (names != null && i < names.Length && names[i] != null) names[i].text = e.offer.displayName;
                if (descs != null && i < descs.Length && descs[i] != null) descs[i].text = UIUtil.Cap(e.offer.description);
                if (prices != null && i < prices.Length && prices[i] != null)
                {
                    prices[i].text = blocked ?? ((e.lasting ? "[ember] " : "[spark] ") + e.price);       // (the picture first, as in the line of what is in the pouch: the number ends flush with the row)
                    bool enough = (e.lasting ? Villagers.Embers : Villagers.Sparks) >= e.price;
                    prices[i].color = blocked != null ? new Color(1f, 1f, 1f, 0.55f) : !enough ? new Color(1f, 0.45f, 0.4f) : e.lasting ? new Color(1f, 0.72f, 0.36f) : new Color(1f, 0.92f, 0.55f);
                }
            }
            if (panel != null) panel.sizeDelta = new Vector2(panel.sizeDelta.x, baseHeight + rowStep * n);
        }

        void Say(string words, bool good)
        {
            if (note == null) return;
            note.text = words;
            note.color = good ? new Color(0.7f, 1f, 0.75f) : new Color(1f, 0.5f, 0.45f);
        }

        /// <summary>Buys the ware of a row (or has the bag of that row made larger). False when it could not be.</summary>
        public bool Buy(int i)
        {
            var e = At(i);
            var run = RunSession.Current;
            if (!open || e == null || run == null) return false;
            if (Blocked(e, run, out string why) != null) { Sfx.PlayUI("error"); Say(why, false); return false; }
            if (e.bag != null)
            {
                if (!Bags.Enlarge(e.bag)) { Sfx.PlayUI("error"); Say("Not enough [ember].", false); return false; }
                Sfx.PlayUI("buy");
                Say(e.bag == Bags.Embers ? $"{Bags.Name(e.bag)}: it holds {Bags.Holds(e.bag, run)} now, for good."
                                         : $"{Bags.Name(e.bag)}: it holds {Bags.Holds(e.bag, run)} now, for good - and it is full.", true);
                Refresh();
                return true;
            }
            if (e.lasting ? !Villagers.SpendEmbers(e.price) : !Villagers.SpendSparks(e.price))
            {
                Sfx.PlayUI("error");
                Say(e.lasting ? "Not enough [ember]." : "Not enough [spark].", false);
                return false;
            }
            ShopItem.Apply(run, e.offer);
            run.purchases.Add(e.offer.id);
            SaveSystem.CheckpointInVillage(run);       // bought at home: saved at once, like the embers it cost
            ItemLog.FirstTime(e.offer.id);            // (what it is was read here: no card for it later)
            QuestLog.Notify(QuestTrigger.Obtain, e.offer.id, 1);
            Sfx.PlayUI("buy");
            Say(e.offer.isUpgrade && !run.IsWorn(e.offer.id)
                ? $"{e.offer.displayName} is in your bag (its slot is taken): wear it from the bag."
                : e.offer.id == MapScreen.ItemId ? $"{e.offer.displayName}: yours for good. Hold the map button to unfold it."
                : $"{e.offer.displayName}: yours for this climb.", true);
            Refresh();
            return true;
        }
    }
}
