using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The Field Guide, open (Codex has the book itself). The game stands still while it is read. On the left the
    /// list of pages - each place in gold, its creatures under it; on the right the page chosen: its picture, its
    /// name, what the book says. Up / down turns the pages (held: it keeps turning), left / right jumps from place
    /// to place. Pause, the bag's button or a gamepad's back button shuts it (GameDirector).
    /// </summary>
    public class CodexScreen : MonoBehaviour
    {
        public GameObject root;
        [Tooltip("The lines of the list, top to bottom.")] public TMPro.TMP_Text[] rows;
        [Tooltip("The bar behind the line that is chosen.")] public RectTransform marker;
        public TMPro.TMP_Text sectionText, nameText, bodyText, countText;
        [Tooltip("\"Defeated 12\" under a creature's name.")] public TMPro.TMP_Text killsText;
        public Color unknownColor = new Color(1f, 1f, 1f, 0.38f);
        public Image picture;
        [Tooltip("Pictures by name, for the pages that have no prefab to show (Codex.Page.art): the names ...")] public string[] artKeys;
        [Tooltip("... and the pictures.")] public Sprite[] artSprites;
        [Tooltip("A little view of a place, made of its tiles: these cells, row by row from the top.")] public Image[] sceneCells;
        public int sceneColumns = 4;
        [Tooltip("The sky behind every creature and trap.")] public Image sky;
        [Tooltip("A second picture laid over the first (the face of an arrow trap on its block).")] public Image overlay;
        [Tooltip("\"Close\", for the mouse.")] public TMPro.TMP_Text closeLabel;

        Sprite Art(string key)
        {
            if (artKeys != null && artSprites != null)
                for (int i = 0; i < artKeys.Length && i < artSprites.Length; i++) if (artKeys[i] == key) return artSprites[i];
            return null;
        }

        /// <summary>The chosen page shows something: a picture, or the view of a place (tests).</summary>
        public bool PictureShown
        {
            get
            {
                if (picture != null && picture.enabled && picture.sprite != null) return true;
                if (sceneCells != null) foreach (var c in sceneCells) if (c != null && c.enabled && c.sprite != null) return true;
                return false;
            }
        }
        public PlayerInputReader input;
        public GameDirector director;
        public Color placeColor = new Color(1f, 0.84f, 0.38f), pageColor = new Color(1f, 1f, 1f, 0.82f), chosenColor = Color.white;
        public float pictureBox = 190f;

        bool open;
        float savedScale = 1f, repeatAt;
        int sel, top, lastX, lastY;

        public bool IsOpen => open;
        public int Selected => sel;
        public string SelectedName => sel >= 0 && sel < Codex.Pages.Count ? Codex.Pages[sel].name : null;
        /// <summary>The chosen page can be read (a creature's is blank until one has been killed).</summary>
        public bool SelectedKnown => sel >= 0 && sel < Codex.Pages.Count && Known(Codex.Pages[sel]);
        public string ShownName => nameText != null ? nameText.text : null;

        Entity PrefabOf(Codex.Page page)
        {
            var cfg = director != null ? director.config : null;
            return cfg != null && cfg.prefabs != null && !string.IsNullOrEmpty(page.prefab) ? cfg.prefabs.Get(page.prefab) : null;
        }

        /// <summary>The name its kills are counted under: what the creature itself is called.</summary>
        string KillKey(Codex.Page page) { var pf = PrefabOf(page); return pf != null ? pf.displayName : page.name; }

        bool Known(Codex.Page page) => page.place || page.info || Codex.AllKnown || Codex.Kills(KillKey(page)) > 0;

        void Awake()
        {
            // the mouse: a click on a line of the list opens that page, the wheel turns the pages, "close" shuts the book
            if (rows != null)
                for (int i = 0; i < rows.Length; i++)
                {
                    int line = i;
                    if (rows[i] == null) continue;
                    rows[i].raycastTarget = true;
                    PointerRelay.On(rows[i].gameObject, null, () => Click(line));
                }
            if (closeLabel != null) { closeLabel.raycastTarget = true; PointerRelay.On(closeLabel.gameObject, null, Close); }
            if (root != null) root.SetActive(false);
        }

        /// <summary>A click on the line 'line' of the list (0 = the top one shown).</summary>
        public void Click(int line)
        {
            if (!open) return;
            int t = top + line;
            if (t < 0 || t >= Codex.Pages.Count || t == sel) return;
            sel = t;
            Refresh();
            Sfx.PlayUI("menu_select", 0.6f);
        }

        float wheel;

        public void Open()
        {
            if (open) return;
            open = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
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
            if (!open || input == null) return;
            int dx = input.MoveX, dy = input.MoveY;
            float now = Time.unscaledTime;
            if (dy != 0 && (dy != lastY || now >= repeatAt)) { Move(-dy); repeatAt = now + (dy != lastY ? 0.38f : 0.09f); }
            if (dx != 0 && dx != lastX) Jump(dx);
            lastX = dx; lastY = dy;
            // the mouse wheel turns the pages, a notch a page
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                float sc = mouse.scroll.ReadValue().y;
                if (sc != 0f && Time.unscaledTime >= wheel) { Move(sc > 0f ? -1 : 1); wheel = Time.unscaledTime + 0.05f; }
            }
        }

        /// <summary>One page on (+1) or back (-1).</summary>
        public void Move(int d)
        {
            int n = Codex.Pages.Count, t = Mathf.Clamp(sel + d, 0, n - 1);
            if (t == sel) return;
            sel = t;
            Refresh();
            Sfx.PlayUI("menu_select", 0.6f);
        }

        /// <summary>To the next place (+1) or the one before (-1).</summary>
        public void Jump(int d)
        {
            var p = Codex.Pages;
            int t = sel;
            for (int i = sel + d; i >= 0 && i < p.Count; i += d) if (p[i].place) { t = i; break; }
            if (t == sel) return;
            sel = t;
            Refresh();
            Sfx.PlayUI("menu_select", 0.6f);
        }

        void Refresh()
        {
            var p = Codex.Pages;
            int shown = rows != null ? rows.Length : 0;
            if (p.Count == 0 || shown == 0) return;
            sel = Mathf.Clamp(sel, 0, p.Count - 1);
            // the chosen line stays in sight, a couple of lines from either end of the list where it can
            if (sel < top + 2) top = sel - 2;
            if (sel > top + shown - 3) top = sel - shown + 3;
            top = Mathf.Clamp(top, 0, Mathf.Max(0, p.Count - shown));
            for (int i = 0; i < shown; i++)
            {
                int k = top + i;
                bool on = k < p.Count;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var e = p[k];
                bool known = Known(e);
                rows[i].text = e.place ? e.name.ToUpperInvariant() : "    " + (known ? e.name : "? ? ?");
                rows[i].color = k == sel ? chosenColor : e.place ? placeColor : known ? pageColor : unknownColor;
                rows[i].fontStyle = e.place || k == sel ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
                if (k == sel && marker != null) marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, rows[i].rectTransform.anchoredPosition.y);
            }
            var page = p[sel];
            bool read = Known(page);
            bool creature = !page.place && !page.info;
            if (sectionText != null) sectionText.text = page.place ? "A PLACE ON THE MOUNTAIN" : page.section.ToUpperInvariant();
            if (nameText != null) nameText.text = read ? page.name : "? ? ?";
            // a creature nobody has killed yet: a dark shape and nothing written
            if (bodyText != null) bodyText.text = read ? page.text : "Nothing is written here yet.\n\nKill one, and what is known of it is yours to read.";
            if (killsText != null)
            {
                killsText.gameObject.SetActive(creature && !Codex.AllKnown);       // (Challenge keeps no count)
                if (creature) killsText.text = "Defeated  " + Codex.Kills(KillKey(page));
            }
            if (countText != null)
            {
                int all = 0, got = 0;
                foreach (var e in p) if (!e.place && !e.info) { all++; if (Known(e)) got++; }
                countText.text = "Creatures known  " + got + " / " + all;
            }
            if (picture != null)
            {
                Sprite s = null;
                var prefab = PrefabOf(page);
                picture.color = read ? Color.white : Color.black;
                bool scene = page.art != null && page.art.StartsWith("scene:");
                if (sceneCells != null)
                {
                    // Every page stands in front of the wall of the place it belongs to (a creature not yet killed is
                    // a black shape: on the panel's own dark it could not be seen at all). A place's own page has
                    // its ground along the bottom as well.
                    string set = scene ? page.art.Substring(6) : null;
                    int cols = Mathf.Max(1, sceneColumns), rowsN = Mathf.Max(1, sceneCells.Length / cols);
                    for (int i = 0; i < sceneCells.Length; i++)
                    {
                        if (sceneCells[i] == null) continue;
                        Sprite cs = set == null ? null : scene && i / cols == rowsN - 1 ? Art("Tiles/" + set + "/dirt_01_0") : Art("Tiles/" + set + "/bg_" + (i * 3 + i / cols) % 4);
                        sceneCells[i].sprite = cs;
                        sceneCells[i].enabled = cs != null;
                    }
                }
                // every creature and trap is shown against the sky: light enough behind it that the black shape of
                // one not yet killed can be made out
                if (sky != null) sky.enabled = !scene;
                Sprite own = !scene && !string.IsNullOrEmpty(page.art) ? Art(page.art) : null;
                // (two pictures: the second lies over the first)
                Sprite over = null;
                int plus = !scene && page.art != null ? page.art.IndexOf('+') : -1;
                if (plus > 0) { own = Art(page.art.Substring(0, plus)); over = Art(page.art.Substring(plus + 1)); }
                if (own != null) { s = own; prefab = null; }       // (a picture of its own comes before its prefab's)
                if (overlay != null)
                {
                    overlay.sprite = over; overlay.enabled = over != null && own != null;
                    overlay.color = picture.color;
                    if (overlay.enabled) overlay.rectTransform.sizeDelta = over.rect.size * Mathf.Min(4f, pictureBox / Mathf.Max(1f, Mathf.Max(own.rect.width, own.rect.height)));
                }
                if (prefab != null) { var r = prefab.GetComponentInChildren<SpriteRenderer>(true); if (r != null) s = r.sprite; }
                picture.sprite = s;
                picture.enabled = s != null;
                if (s != null)
                {
                    // its own pixels, four to one - brought down only if that is more than the frame holds
                    float k = Mathf.Min(4f, pictureBox / Mathf.Max(1f, Mathf.Max(s.rect.width, s.rect.height)));
                    picture.rectTransform.sizeDelta = s.rect.size * k;
                }
            }
        }
    }
}
