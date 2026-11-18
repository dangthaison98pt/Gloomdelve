using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Gear wheel (in the spirit of a classic item-swap menu):
    ///   hold R1 (gamepad) / D (keyboard) -> the game freezes and the wheel opens,
    ///   tilt left / right to choose, release to equip.
    ///   A quick tap swaps back to the previously equipped item (or the next one if there is none), so after
    ///   choosing something on the wheel one tap always returns to what you had before.
    /// </summary>
    public class EquipmentWheel : MonoBehaviour
    {
        public GameObject root;
        public RectTransform row;
        public GameObject slotTemplate;
        public TMPro.TMP_Text nameText;
        [Tooltip("Optional: the old description block. Leave empty for the compact look.")] public TMPro.TMP_Text descText;
        public TMPro.TMP_Text hintText;
        [Tooltip("Small arrows shown either side of the chosen slot.")] public RectTransform arrowLeft, arrowRight;
        public PlayerInputReader input;
        public GameDirector director;
        [Tooltip("Hold the button this long to open the wheel; shorter = quick swap to the next item.")]
        public float holdToOpen = 0.25f;
        [Tooltip("If the wheel opened but nothing was chosen and the button is released before this, it still counts as a tap.")]
        public float tapGrace = 0.4f;
        public float spacing = 132f;

        class Slot { public RectTransform rt; public Image frame; public Image icon; public TMPro.TMP_Text ammo; }
        readonly List<Slot> slots = new List<Slot>();

        bool open, holding, moved;
        float holdTime, savedScale = 1f;
        int sel, lastDir;

        public bool IsOpen => open;
        public int Selected => sel;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (slotTemplate != null) slotTemplate.SetActive(false);
        }

        bool CanUse()
        {
            var run = RunSession.Current;
            return run != null && director != null && !director.Ended && !director.Paused && director.Player != null &&
                   !director.Player.IsDead && run.gear.Count > 0 && !AutoTest.Active &&
                   (director.inventory == null || !director.inventory.IsOpen);
        }

        void Update()
        {
            if (!CanUse())
            {
                if (open) Close();
                holding = false;
                return;
            }
            bool held = input != null && input.SwapHeld;
            if (held)
            {
                if (!holding) { holding = true; holdTime = 0f; }
                holdTime += Time.unscaledDeltaTime;
                if (!open && holdTime >= holdToOpen) Open();
                if (open)
                {
                    int d = input.MoveX;
                    if (d != 0 && d != lastDir) { Step(d); moved = true; }
                    lastDir = d;
                }
            }
            else if (holding)
            {
                holding = false;
                if (open && moved) Confirm();
                else if (open && holdTime >= tapGrace) Close();   // looked at the wheel, chose nothing
                else
                {
                    // a quick tap, or a slightly long one that just opened the wheel: quick swap
                    if (open) Close();
                    QuickSwap();
                }
            }
        }

        public void Open()
        {
            var run = RunSession.Current;
            if (open || run == null || run.gear.Count == 0) return;
            open = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            sel = Mathf.Clamp(run.equipped, 0, run.gear.Count - 1);
            moved = false;
            lastDir = input != null ? input.MoveX : 0;
            BuildSlots(run);
            root.SetActive(true);
            Refresh();
            Sfx.PlayUI("menu_select");
        }

        public void Step(int dir)
        {
            var run = RunSession.Current;
            if (!open || run == null || run.gear.Count == 0) return;
            int n = run.gear.Count;
            sel = ((sel + dir) % n + n) % n;
            Refresh();
            Sfx.PlayUI("menu_select", 0.7f);
        }

        public void Confirm()
        {
            var run = RunSession.Current;
            if (!open) return;
            bool changed = run != null && run.equipped != sel;
            if (run != null) run.SetEquipped(sel);
            Close();
            if (changed) Announce();
        }

        /// <summary>Quick tap: back to the previously equipped item, else the next one.</summary>
        public void QuickSwap()
        {
            var run = RunSession.Current;
            if (run == null || run.gear.Count <= 1) return;
            int p = run.previousEquipped;
            if (p >= 0 && p < run.gear.Count && p != run.equipped) { run.SetEquipped(p); Announce(); }
            else Cycle(1);
        }

        public void Cycle(int dir)
        {
            var run = RunSession.Current;
            if (run == null || run.gear.Count <= 1) return;
            int n = run.gear.Count;
            run.SetEquipped(((run.equipped + dir) % n + n) % n);
            Announce();
        }

        void Close()
        {
            open = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = director != null && director.Paused ? 0f : savedScale;
            input?.ClearLatches();
        }

        void Announce()
        {
            var run = RunSession.Current;
            var def = director != null ? director.config.FindGear(run.EquippedId) : null;
            if (def == null) return;
            Sfx.PlayUI("pickup", 0.8f);
            if (director.Player != null)
                Fx.Text(director.Player.Center + Vector2.up * 0.9f, def.displayName.ToUpperInvariant(), new Color(0.75f, 1f, 0.8f), 0.9f);
        }

        void BuildSlots(RunState run)
        {
            while (slots.Count < run.gear.Count)
            {
                var go = Instantiate(slotTemplate, row);
                go.SetActive(true);
                var s = new Slot
                {
                    rt = (RectTransform)go.transform,
                    frame = go.GetComponent<Image>(),
                    icon = go.transform.Find("Icon").GetComponent<Image>(),
                    ammo = go.transform.Find("Ammo").GetComponent<TMPro.TMP_Text>(),
                };
                slots.Add(s);
            }
            for (int i = 0; i < slots.Count; i++) slots[i].rt.gameObject.SetActive(i < run.gear.Count);
        }

        // Compact look: a row of slots, the chosen one framed in white with an arrow either side, the item in use
        // on a gold tile, and just the name of the chosen item underneath. No description, no instructions.
        void Refresh()
        {
            var run = RunSession.Current;
            int n = run.gear.Count;
            for (int i = 0; i < n; i++)
            {
                var def = director.config.FindGear(run.gear[i]);
                var s = slots[i];
                bool on = i == sel, inUse = i == run.equipped;
                s.rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * spacing, 0f);
                s.rt.localScale = Vector3.one * (on ? 1.12f : 1f);
                s.frame.color = on ? Color.white : inUse ? new Color(1f, 0.8f, 0.35f) : new Color(0.5f, 0.46f, 0.54f, 0.9f);
                s.icon.sprite = def != null ? def.icon : null;
                s.icon.color = on || inUse ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                int am = def != null ? run.Ammo(def.id) : 0;
                s.ammo.text = def != null && am >= 0 ? am.ToString() : "";
            }
            float selX = (sel - (n - 1) * 0.5f) * spacing;
            float reach = spacing * 0.5f + 6f;
            if (arrowLeft != null) { arrowLeft.gameObject.SetActive(n > 1); arrowLeft.anchoredPosition = new Vector2(selX - reach, 0f); }
            if (arrowRight != null) { arrowRight.gameObject.SetActive(n > 1); arrowRight.anchoredPosition = new Vector2(selX + reach, 0f); }
            var cur = director.config.FindGear(run.gear[sel]);
            nameText.text = cur != null ? cur.displayName : "";
            if (descText != null) descText.text = "";
            if (hintText != null) hintText.text = "Select  [move]";
        }
    }
}
