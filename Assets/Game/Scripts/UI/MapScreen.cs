using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The map Oswin the Mapmaker sells: a key item - bought once, kept with the save for good, like the Ember
    /// Pouch. With it in the bag the level draws itself as it is walked: whatever has come within a few steps of the player is put down,
    /// the rest of the sheet stays dark. Holding the map button (M / L1) unfolds it over the game - which goes on
    /// underneath - and letting go folds it away again.
    /// Lives on the GameDirector's object, made when a level starts (Attach): every level begins with a dark sheet.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        /// <summary>The shop offer, and the key item the save keeps once it is bought.</summary>
        public const string ItemId = "map";
        /// <summary>How far around the player the level is put down on the sheet, in tiles.</summary>
        public const float Reach = 6.5f;

        /// <summary>Tests: the button counts as held.</summary>
        public bool forceHeld;

        GameDirector dir;
        bool[] seen;
        int w, h;
        Texture2D tex;
        Color32[] px;
        GameObject root;
        RawImage sheet;
        float nextDraw;

        public bool IsOpen => root != null && root.activeSelf;
        public static bool Carried => Villagers.HasKey(ItemId);
        public bool Seen(int x, int y) => seen != null && x >= 0 && y >= 0 && x < w && y < h && seen[y * w + x];

        public static MapScreen Attach(GameDirector d)
        {
            var m = d.gameObject.GetComponent<MapScreen>();
            if (m == null) m = d.gameObject.AddComponent<MapScreen>();
            m.dir = d;
            return m;
        }

        void Update()
        {
            if (dir == null || dir.Level == null || dir.world == null || dir.world.Grid == null) return;
            var run = RunSession.Current;
            var p = dir.Player;
            bool carried = Carried && run != null && !run.inVillage;
            if (seen == null || w != dir.Level.Width || h != dir.Level.Height)
            {
                w = dir.Level.Width; h = dir.Level.Height;
                seen = new bool[w * h];
            }
            // what the player has come near is put down on the sheet (only with the map in the bag: nobody draws without paper)
            if (carried && p != null && !p.IsDead) Mark(p.Center);

            bool held = forceHeld || (dir.input != null && dir.input.MapHeld);
            bool want = carried && held && p != null && !dir.Paused && !dir.Ended && !EndingScene.Playing
                        && (dir.inventory == null || !dir.inventory.IsOpen) && (dir.codex == null || !dir.codex.IsOpen)
                        && (dir.gearWheel == null || !dir.gearWheel.IsOpen) && (dir.hud == null || !dir.hud.CardModal);
            if (want != IsOpen)
            {
                if (want && root == null) Build();
                if (root != null) root.SetActive(want);
                if (want) { nextDraw = 0f; Sfx.PlayUI("menu_select", 0.5f); }
            }
            if (IsOpen && Time.unscaledTime >= nextDraw) { nextDraw = Time.unscaledTime + 0.08f; Draw(); }
        }

        void Mark(Vector2 at)
        {
            int cx = Mathf.FloorToInt(at.x), cy = Mathf.FloorToInt(at.y), r = Mathf.CeilToInt(Reach);
            float r2 = Reach * Reach;
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(h - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(w - 1, cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2) seen[y * w + x] = true;
        }

        static readonly Color32 Dark = new Color32(9, 9, 16, 255), Air = new Color32(44, 46, 68, 255), Rock = new Color32(150, 128, 104, 255),
            Hard = new Color32(92, 88, 104, 255), Wood = new Color32(196, 150, 86, 255), Sharp = new Color32(214, 72, 72, 255),
            Gold = new Color32(240, 200, 80, 255), Way = new Color32(110, 220, 240, 255), Start = new Color32(120, 210, 120, 255),
            Me = new Color32(255, 255, 255, 255), MeDim = new Color32(255, 214, 98, 255);

        /// <summary>The sheet as it is now: one dot a tile. Dark where nobody has been.</summary>
        void Draw()
        {
            var g = dir.world.Grid;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = Dark;
                    if (seen[y * w + x])
                    {
                        switch (g.Get(x, y))
                        {
                            case TileType.Empty: c = Air; break;
                            case TileType.Bedrock: c = Hard; break;
                            case TileType.Platform: case TileType.Ladder: c = Wood; break;
                            case TileType.Spikes: c = Sharp; break;
                            case TileType.GoldVein: case TileType.GemVein: c = Gold; break;
                            default: c = Rock; break;
                        }
                    }
                    px[y * w + x] = c;
                }
            void Dot(Vector2Int cell, Color32 c, bool tall)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= w || cell.y >= h) return;
                px[cell.y * w + cell.x] = c;
                if (tall && cell.y + 1 < h) px[(cell.y + 1) * w + cell.x] = c;
            }
            // the way in is always known; the way out only once it has been seen
            Dot(dir.Level.entrance, Start, true);
            if (Seen(dir.Level.exit.x, dir.Level.exit.y)) Dot(dir.Level.exit, Way, true);
            var p = dir.Player;
            if (p != null)
            {
                bool on = Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 0;
                Dot(new Vector2Int(Mathf.FloorToInt(p.pos.x), Mathf.FloorToInt(p.pos.y + 0.1f)), on ? Me : MeDim, true);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>The sheet and its frame, in the middle of the screen: made the first time the map is unfolded.</summary>
        void Build()
        {
            root = new GameObject("MapScreen");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[w * h];
            // every tile a whole number of screen dots, as large as leaves room for the frame and its name
            float dot = Mathf.Clamp(Mathf.Floor(Mathf.Min(1560f / w, 820f / h)), 2f, 16f);       // (a small level is not blown up to fill the screen)
            var size = new Vector2(w * dot, h * dot);

            Image Box(string name, Vector2 sz, Color col)
            {
                var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(root.transform, false);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(0f, -24f);
                img.rectTransform.sizeDelta = sz;
                img.color = col;
                img.raycastTarget = false;
                return img;
            }
            Box("Edge", size + new Vector2(44f, 44f), new Color(0.78f, 0.64f, 0.34f, 0.95f));
            Box("Paper", size + new Vector2(36f, 36f), new Color(0.04f, 0.04f, 0.08f, 0.94f));
            sheet = new GameObject("Sheet", typeof(RectTransform)).AddComponent<RawImage>();
            sheet.transform.SetParent(root.transform, false);
            sheet.rectTransform.anchorMin = sheet.rectTransform.anchorMax = sheet.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            sheet.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            sheet.rectTransform.sizeDelta = size;
            sheet.texture = tex;
            sheet.raycastTarget = false;

            var title = new GameObject("Name", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            title.transform.SetParent(root.transform, false);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.5f + 28f);
            title.rectTransform.sizeDelta = new Vector2(900f, 48f);
            if (dir.hud != null && dir.hud.healthText != null) title.font = dir.hud.healthText.font;
            title.fontSize = 34f;
            title.color = new Color(1f, 0.85f, 0.4f);
            title.alignment = TMPro.TextAlignmentOptions.Center;
            title.raycastTarget = false;
            title.text = dir.Stage != null && !string.IsNullOrEmpty(dir.Stage.displayName) ? "Map  -  " + dir.Stage.displayName : "Map";
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
            if (root != null) Destroy(root);
        }
    }
}
