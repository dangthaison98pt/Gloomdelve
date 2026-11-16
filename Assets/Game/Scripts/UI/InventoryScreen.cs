using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Equipment screen (Tab / Select): every wearable item the run owns. The game freezes while it is open;
    /// move to choose, confirm to wear it or take it off. Each item belongs to a slot (feet, body, hand, charm)
    /// and only what is worn has an effect, so two pairs of boots are a choice, not a stack.
    /// Items come from shops (ShopConfig offers with a slot).
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        public GameObject root;
        public RectTransform grid;
        public GameObject cellTemplate;
        public Text nameText;
        public Text descText;
        public Text hintText;
        public Text emptyText;
        public PlayerInputReader input;
        public GameDirector director;
        public int columns = 5;
        public float spacing = 156f;

        class Cell { public RectTransform rt; public Image frame; public Image icon; public Text slot; public GameObject worn; }
        readonly List<Cell> cells = new List<Cell>();

        bool open;
        float savedScale = 1f;
        int sel, lastX, lastY;

        public bool IsOpen => open;
        public int Selected => sel;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (cellTemplate != null) cellTemplate.SetActive(false);
        }

        public static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "feet": return "FEET";
                case "body": return "BODY";
                case "hand": return "HAND";
                case "charm": return "CHARM";
                default: return "ALWAYS ON";
            }
        }

        public void Open()
        {
            var run = RunSession.Current;
            if (open || run == null) return;
            open = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, run.upgrades.Count - 1));
            lastX = input != null ? input.MoveX : 0;
            lastY = input != null ? input.MoveY : 0;
            root.SetActive(true);
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
            if (dy != 0 && dy != lastY) Move(-dy * columns);
            lastX = dx; lastY = dy;
            if (input.ConfirmPressed) Toggle();
        }

        public void Move(int d)
        {
            var run = RunSession.Current;
            if (run == null) return;
            int t = sel + d;
            if (t < 0 || t >= run.upgrades.Count) return;
            sel = t;
            Refresh();
            Sfx.PlayUI("menu_select", 0.7f);
        }

        /// <summary>Wear / take off the selected item.</summary>
        public void Toggle()
        {
            var run = RunSession.Current;
            if (run == null || sel < 0 || sel >= run.upgrades.Count) return;
            bool changed = run.ToggleWorn(run.upgrades[sel]);
            Sfx.PlayUI(changed ? "pickup" : "error", 0.8f);
            Refresh();
        }

        void Refresh()
        {
            var run = RunSession.Current;
            if (run == null) return;
            int n = run.upgrades.Count;
            while (cells.Count < n)
            {
                var go = Instantiate(cellTemplate, grid);
                go.SetActive(true);
                cells.Add(new Cell
                {
                    rt = (RectTransform)go.transform,
                    frame = go.GetComponent<Image>(),
                    icon = go.transform.Find("Icon").GetComponent<Image>(),
                    slot = go.transform.Find("Slot").GetComponent<Text>(),
                    worn = go.transform.Find("Worn").gameObject,
                });
            }
            int cols = Mathf.Max(1, columns);
            int rowLen = Mathf.Min(cols, Mathf.Max(1, n));
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                c.rt.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var o = run.Owned(run.upgrades[i]);
                bool worn = run.IsWorn(run.upgrades[i]), on = i == sel;
                c.rt.anchoredPosition = new Vector2((i % cols - (rowLen - 1) * 0.5f) * spacing, -(i / cols) * (spacing + 14f));
                c.rt.localScale = Vector3.one * (on ? 1.15f : 1f);
                c.frame.color = on ? (worn ? new Color(1f, 0.86f, 0.4f) : Color.white)
                                   : (worn ? new Color(0.8f, 0.62f, 0.26f) : new Color(0.45f, 0.42f, 0.5f, 0.9f));
                c.icon.sprite = o != null ? o.icon : null;
                c.icon.enabled = c.icon.sprite != null;
                c.icon.color = worn ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                c.slot.text = o != null ? SlotLabel(o.slot) : "";
                c.worn.SetActive(worn);
            }
            if (emptyText != null) emptyText.gameObject.SetActive(n == 0);
            var cur = n > 0 ? run.Owned(run.upgrades[Mathf.Clamp(sel, 0, n - 1)]) : null;
            if (cur != null)
            {
                bool w = run.IsWorn(cur.id);
                nameText.text = cur.displayName.ToUpperInvariant() + (w ? "   -   WORN" : "   -   in the bag");
                descText.text = cur.description;
                bool fixedOn = string.IsNullOrEmpty(cur.slot);
                hintText.text = "[move]  choose          " + (fixedOn ? "" : "[jump]  " + (w ? "take off" : "wear") + "          ") + "[inventory]  close";
            }
            else
            {
                nameText.text = "";
                descText.text = "";
                hintText.text = "[inventory]  close";
            }
        }
    }
}
