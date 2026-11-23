using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The bag (I / Start). The game freezes while it is open. Three tabs:
    ///  - WEAR: the slots of the body across the top (feet, body, hand, two charms) showing what is worn in each,
    ///    and under them everything wearable the run owns; confirm wears an item or takes it off. Only what is worn
    ///    has an effect, so two pairs of boots are a choice, not a stack.
    ///  - ITEMS: the things used with the "use" button (bombs, ropes, ...) and how many are left; confirm takes one
    ///    in hand.
    ///  - KEY ITEMS: the few things that are never lost (the elder's Ember Pouch).
    /// The arrows / stick only move between the things of the tab; L1 / R1 (Walk / gear button) change tab. The bag's own button, Pause, or the
    /// "back" button of a gamepad closes it. The mouse works too: a click on a tab shows it, the pointer over a
    /// thing chooses it, a click wears it / takes it in hand, and a click on a filled body slot takes that off.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        public GameObject root;
        public RectTransform grid;
        [Tooltip("Where the slots of the body are laid out (WEAR tab).")] public RectTransform slotRow;
        public GameObject cellTemplate;
        public TMPro.TMP_Text nameText;
        public TMPro.TMP_Text descText;
        public TMPro.TMP_Text hintText;
        public TMPro.TMP_Text emptyText;
        [Tooltip("The small heading over the bag on the WEAR tab.")] public TMPro.TMP_Text bagLabel;
        [Tooltip("The three tab headings, in order: wear, items, key items.")] public TMPro.TMP_Text[] tabLabels;
        public Image[] tabFrames;
        [Tooltip("Picture of the Ember Pouch (KEY ITEMS tab).")] public Sprite pouchIcon;
        [Tooltip("Picture of the Field Guide (KEY ITEMS tab): confirm opens it.")] public Sprite bookIcon;
        public PlayerInputReader input;
        public GameDirector director;
        public int columns = 7;
        public float spacing = 124f;
        public float slotSpacing = 150f;
        [Tooltip("Height of the grid's first row below the top of the panel: with the slots above it (WEAR), and without.")]
        public float gridYWear = -412f, gridYPlain = -236f;

        public const int TabWear = 0, TabItems = 1, TabKeys = 2;
        /// <summary>The slots of the body, in the order they are shown; a charm fits twice.</summary>
        public static readonly string[] BodySlots = { "feet", "body", "hand", "charm", "charm" };

        class Cell { public RectTransform rt; public Image frame; public Image icon; public TMPro.TMP_Text slot; public GameObject worn; }
        class Entry { public Sprite icon; public string under, name, desc, act; public bool marked; public Action run; public string slot; }

        readonly List<Cell> cells = new List<Cell>();
        readonly List<Cell> slotCells = new List<Cell>();
        readonly List<Entry> entries = new List<Entry>();

        bool open, onTabs;
        float savedScale = 1f;
        int tab, sel, lastX, lastY;

        public bool IsOpen => open;
        public int Selected => sel;
        public int Tab => tab;
        public int EntryCount => entries.Count;
        public bool OnTabs => onTabs;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (cellTemplate != null) cellTemplate.SetActive(false);
            if (tabFrames != null)
                for (int i = 0; i < tabFrames.Length; i++)
                {
                    if (tabFrames[i] == null) continue;
                    int t = i;
                    PointerRelay.On(tabFrames[i].gameObject, null, () => { if (open) SetTab(t); });
                }
        }

        // what is worn in each body slot as shown (for a click on the slot)
        readonly string[] slotIds = new string[BodySlots.Length];

        void PointAt(int i)
        {
            if (!open || i < 0 || i >= entries.Count || (!onTabs && sel == i)) return;
            onTabs = false; sel = i;
            Refresh();
        }

        void ClickOn(int i)
        {
            if (!open || i < 0 || i >= entries.Count) return;
            onTabs = false; sel = i;
            if (entries[i].run != null) Toggle(); else Refresh();
        }

        void ClickSlot(int i)
        {
            var run = RunSession.Current;
            if (!open || run == null || i < 0 || i >= slotIds.Length || string.IsNullOrEmpty(slotIds[i])) return;
            Sfx.PlayUI(run.TakeOff(slotIds[i]) ? "pickup" : "error", 0.8f);
            Refresh();
        }

        public static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "feet": return "Feet";
                case "body": return "Body";
                case "hand": return "Hand";
                case "charm": return "Charm";
                default: return "Always on";
            }
        }

        public void Open()
        {
            var run = RunSession.Current;
            if (open || run == null) return;
            open = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            lastX = input != null ? input.MoveX : 0;
            lastY = input != null ? input.MoveY : 0;
            root.SetActive(true);
            tab = TabWear;          // always opens on the WEAR tab, not where it was last closed
            sel = 0;
            onTabs = false;
            Refresh();
            Sfx.PlayUI("menu_select");
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = savedScale;
            input?.ClearLatches();
        }

        void Update()
        {
            if (!open) return;
            var run = RunSession.Current;
            if (run == null || input == null) { Close(); return; }
            int dx = input.MoveX, dy = input.MoveY;
            if (dx != 0 && dx != lastX) Move(dx);
            if (dy != 0 && dy != lastY) MoveRow(-dy);
            lastX = dx; lastY = dy;
            if (input.TabLeftPressed) SetTab(tab - 1);
            if (input.TabRightPressed) SetTab(tab + 1);
            if (input.ConfirmPressed) Toggle();
        }

        /// <summary>Shows another tab (0 wear, 1 items, 2 key items).</summary>
        public void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, 2);
            if (t == tab) return;
            tab = t;
            sel = 0;
            Refresh();
            Sfx.PlayUI("menu_select", 0.7f);
        }

        /// <summary>Along the row (and on over its end into the next one).</summary>
        public void Move(int d)
        {
            int t = sel + d;
            if (onTabs || t < 0 || t >= entries.Count) return;
            sel = t;
            Refresh();
            Sfx.PlayUI("menu_select", 0.7f);
        }

        /// <summary>One row down (+1) or up (-1), staying among the things (the tabs change with L1 / R1 only).</summary>
        public void MoveRow(int d)
        {
            Move(d * Mathf.Max(1, columns));
        }

        /// <summary>Confirm on the chosen thing: wear / take off, or take the item in hand.</summary>
        public void Toggle()
        {
            if (onTabs || sel < 0 || sel >= entries.Count) return;
            var e = entries[sel];
            if (e.run == null) return;
            e.run();
            Refresh();
        }

        void BuildEntries(RunState run)
        {
            entries.Clear();
            if (tab == TabWear)
            {
                foreach (var id in run.upgrades)
                {
                    var o = run.Owned(id);
                    if (o == null) continue;
                    bool w = run.IsWorn(id), fixedOn = string.IsNullOrEmpty(o.slot);
                    string uid = id;
                    entries.Add(new Entry
                    {
                        icon = o.icon, under = SlotLabel(o.slot), marked = w, slot = o.slot,
                        name = o.displayName + (w ? "   -   Worn" : "   -   In the bag"),
                        desc = o.description,
                        act = fixedOn ? null : (w ? "Take off" : "Wear"),
                        run = fixedOn ? (Action)null : () => { Sfx.PlayUI(run.ToggleWorn(uid) ? "pickup" : "error", 0.8f); },
                    });
                }
            }
            else if (tab == TabItems)
            {
                for (int i = 0; i < run.gear.Count; i++)
                {
                    var def = director != null && director.config != null ? director.config.FindGear(run.gear[i]) : null;
                    if (def == null) continue;
                    int ammo = run.Ammo(def.id), idx = i;
                    bool inHand = run.equipped == i;
                    entries.Add(new Entry
                    {
                        icon = def.icon, under = ammo >= 0 ? "x" + ammo : "", marked = inHand,
                        name = def.displayName + (inHand ? "   -   In hand" : ""),
                        desc = def.description,
                        act = inHand ? null : "Take in hand",
                        run = inHand ? (Action)null : () => { run.SetEquipped(idx); Sfx.PlayUI("pickup", 0.8f); },
                    });
                }
            }
            else
            {
                if (Codex.Owned)
                    entries.Add(new Entry
                    {
                        icon = bookIcon, under = "Read", marked = false,
                        name = "Field Guide", desc = Codex.About, act = "Read",
                        run = () => { if (director != null) director.OpenCodex(); },
                    });
                if (Villagers.HasKey(Villagers.KeyPouch))
                    entries.Add(new Entry
                    {
                        icon = pouchIcon, under = Villagers.Embers + " / " + Villagers.PouchCapacity, marked = false,
                        name = "Ember Pouch",
                        desc = $"The Elder's gift. It holds {Villagers.Embers} of {Villagers.PouchCapacity} embers, which pay for what lasts, and {Villagers.Sparks} sparks, which the villagers take for their wares. It is never lost.",
                    });
                // the Mapmaker's map: bought once, kept for good; it is unfolded with a button of its own
                if (MapScreen.Carried)
                {
                    var sheet = director != null && director.config != null && director.config.shop != null ? director.config.shop.Find(MapScreen.ItemId) : null;
                    entries.Add(new Entry
                    {
                        icon = sheet != null ? sheet.icon : null, under = "Hold", marked = false,
                        name = sheet != null ? sheet.displayName : "Map",
                        desc = "Hold the map button (M, or L1 on a pad) to unfold it; let go to fold it away. It draws each level as you walk it. It is never lost.",
                    });
                }
                // the story's things (Story): the copy of the old signs, and the parts of the hammer this run has found
                var gcfg = director != null ? director.config : null;
                if (Story.HasRubbing)
                    entries.Add(new Entry
                    {
                        icon = Story.Icon(gcfg, Story.KeyRubbing), under = Story.Decoded ? "Read" : "Unread", marked = false,
                        name = RunePillar.RubbingName,
                        // (what the signs say is not written out here: once the elder has read them, the copy is read
                        // like the Field Guide - its button puts the bag away and shows the reading)
                        desc = Story.Decoded ? "The signs of a pillar on the mountain, rubbed onto paper. Elder Aldous has read them to you."
                                             : "The signs of a pillar on the mountain, rubbed onto paper. Nobody has read them yet. Elder Aldous has read older things than this.",
                        act = Story.Decoded ? "Read" : null,
                        run = Story.Decoded ? () =>
                        {
                            Close();
                            if (director != null && director.hud != null) director.hud.ShowNote(Story.Reading, HUD.ReadTime(Story.Reading));
                        } : (Action)null,
                    });
                if (run != null && !run.gear.Contains(Story.HammerGear))
                {
                    if (run.hammerHandle) entries.Add(new Entry { icon = Story.Icon(gcfg, "hammerhandle"), under = "1 of 3", marked = false, name = "Hammer Haft", desc = "The haft of the hammer the old signs speak of. Lost with the night, like everything carried." });
                    if (run.hammerHead) entries.Add(new Entry { icon = Story.Icon(gcfg, "hammerhead"), under = "1 of 3", marked = false, name = "Hammer Head", desc = "The head of the hammer the old signs speak of. Lost with the night, like everything carried." });
                    if (run.hammerCore) entries.Add(new Entry { icon = Story.Icon(gcfg, "hammercore"), under = "1 of 3", marked = false, name = "Heart of the Hammer", desc = "The heart of the hammer the old signs speak of. Lost with the night, like everything carried." });
                }
            }
        }

        Cell NewCell(Transform parent, bool bodySlot = false)
        {
            var go = Instantiate(cellTemplate, parent);
            go.SetActive(true);
            int index = bodySlot ? slotCells.Count : cells.Count;
            if (bodySlot) PointerRelay.On(go, null, () => ClickSlot(index));
            else PointerRelay.On(go, () => PointAt(index), () => ClickOn(index));
            return new Cell
            {
                rt = (RectTransform)go.transform,
                frame = go.GetComponent<Image>(),
                icon = go.transform.Find("Icon").GetComponent<Image>(),
                slot = go.transform.Find("Slot").GetComponent<TMPro.TMP_Text>(),
                worn = go.transform.Find("Worn").gameObject,
            };
        }

        void Refresh()
        {
            var run = RunSession.Current;
            if (run == null) return;
            BuildEntries(run);
            int n = entries.Count;
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, n - 1));
            onTabs = n == 0;        // an empty tab: nothing to choose
            var cur = !onTabs && n > 0 ? entries[sel] : null;

            // the tabs
            if (tabLabels != null)
                for (int i = 0; i < tabLabels.Length; i++)
                {
                    bool on = i == tab;
                    if (tabLabels[i] != null) tabLabels[i].color = on ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 1f, 1f, 0.55f);
                    if (tabFrames != null && i < tabFrames.Length && tabFrames[i] != null)
                    {
                        tabFrames[i].color = on ? new Color(1f, 0.86f, 0.4f) : new Color(0.45f, 0.42f, 0.5f, 0.55f);
                        tabFrames[i].rectTransform.localScale = Vector3.one * (on ? 1.08f : 1f);
                    }
                }

            // the slots of the body (WEAR): what is worn in each, or nothing
            bool wear = tab == TabWear;
            if (slotRow != null)
            {
                slotRow.gameObject.SetActive(wear);
                if (wear)
                {
                    while (slotCells.Count < BodySlots.Length) slotCells.Add(NewCell(slotRow, true));
                    var used = new HashSet<string>();
                    for (int i = 0; i < BodySlots.Length; i++)
                    {
                        var c = slotCells[i];
                        ShopOffer inIt = null;
                        foreach (var id in run.worn)
                        {
                            var o = run.Owned(id);
                            if (o != null && o.slot == BodySlots[i] && used.Add(id)) { inIt = o; break; }
                        }
                        slotIds[i] = inIt != null ? inIt.id : null;
                        bool fits = cur != null && cur.slot == BodySlots[i];
                        c.rt.anchoredPosition = new Vector2((i - (BodySlots.Length - 1) * 0.5f) * slotSpacing, 0f);
                        c.frame.color = inIt != null ? (fits ? new Color(1f, 0.86f, 0.4f) : new Color(0.8f, 0.62f, 0.26f))
                                                     : (fits ? new Color(0.8f, 0.78f, 0.9f) : new Color(0.45f, 0.42f, 0.5f, 0.7f));
                        c.icon.sprite = inIt != null ? inIt.icon : null;
                        c.icon.enabled = c.icon.sprite != null;
                        c.icon.color = Color.white;
                        c.slot.text = SlotLabel(BodySlots[i]);
                        c.worn.SetActive(false);
                    }
                }
            }
            if (bagLabel != null) bagLabel.gameObject.SetActive(wear && n > 0);
            if (grid != null) grid.anchoredPosition = new Vector2(grid.anchoredPosition.x, wear ? gridYWear : gridYPlain);

            // the things of this tab
            while (cells.Count < n) cells.Add(NewCell(grid));
            int cols = Mathf.Max(1, columns);
            int rowLen = Mathf.Min(cols, Mathf.Max(1, n));
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                c.rt.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var e = entries[i];
                bool on = !onTabs && i == sel;
                c.rt.anchoredPosition = new Vector2((i % cols - (rowLen - 1) * 0.5f) * spacing, -(i / cols) * (spacing + 22f));
                c.rt.localScale = Vector3.one * (on ? 1.12f : 1f);
                c.frame.color = on ? (e.marked ? new Color(1f, 0.86f, 0.4f) : Color.white)
                                   : (e.marked ? new Color(0.8f, 0.62f, 0.26f) : new Color(0.45f, 0.42f, 0.5f, 0.9f));
                c.icon.sprite = e.icon;
                c.icon.enabled = e.icon != null;
                c.icon.color = e.marked || tab != TabWear ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                c.slot.text = e.under ?? "";
                c.worn.SetActive(e.marked);
            }
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(n == 0);
                emptyText.text = tab == TabWear ? "Nothing to wear yet. Shops sell boots, coats and charms."
                               : tab == TabItems ? "Nothing to use."
                               : "No key items yet.";
            }
            nameText.text = cur != null ? cur.name : "";
            // (a description longer than its two lines is set smaller rather than run out of the frame)
            if (!descText.enableAutoSizing) { descText.fontSizeMax = descText.fontSize; descText.fontSizeMin = 15f; descText.enableAutoSizing = true; }
            descText.text = cur != null ? UIUtil.Cap(cur.desc) : "";
            hintText.text = onTabs ? "[walk] [gear]  Tab          [inventory]  Close"
                          : "[move]  Choose          " + (cur != null && cur.act != null ? "[jump]  " + cur.act + "          " : "") + "[walk] [gear]  Tab          [inventory]  Close";
        }
    }
}
