using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gloomdelve
{
    /// <summary>Draws the TileGrid with Unity Tilemaps (auto-tiling by exposed sides) and keeps it in sync when tiles are destroyed.</summary>
    public class LevelRenderer : MonoBehaviour
    {
        public Tilemap background;
        [Tooltip("Soft shadows where the back wall meets solid terrain.")] public Tilemap shade;
        public Tilemap decor;
        public Tilemap terrain;
        public Tilemap overlay;

        TileGrid grid;
        TileSet ts;
        bool[,] shopCells;

        int[] surface;
        bool[,] wall;   // outdoor only: cells that have the back wall behind them

        int SurfaceAt(int x) => surface == null || surface.Length == 0 ? int.MaxValue : surface[Mathf.Clamp(x, 0, surface.Length - 1)];

        public void Build(LevelData level, TileSet tiles)
        {
            if (grid != null) grid.TileChanged -= OnTileChanged;
            grid = level.grid;
            ts = tiles;
            background.ClearAllTiles(); if (shade != null) shade.ClearAllTiles(); decor.ClearAllTiles(); terrain.ClearAllTiles(); overlay.ClearAllTiles();
            if (ts == null) { Debug.LogError("[LevelRenderer] Missing TileSet"); return; }

            shopCells = new bool[grid.Width, grid.Height];
            foreach (var r in level.shopRects)
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                        if (grid.InBounds(x, y)) shopCells[x, y] = true;

            int w = grid.Width, h = grid.Height;
            wall = null;
            if (ts.outdoor) BuildOutdoorWall(w, h);
            // Background extends a little beyond the level so the camera never shows void at the borders.
            for (int y = -6; y < h + 6; y++)
                for (int x = -10; x < w + 10; x++)
                    if (HasBackWall(x, y))
                        background.SetTile(new Vector3Int(x, y, 0), BackgroundTile(x, y));

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    RefreshCell(x, y);
                    PlaceDecor(x, y);
                }
            // Bedrock frame drawn outside the grid too (purely visual).
            for (int y = -6; y < h + 6; y++)
                for (int x = -10; x < w + 10; x++)
                    if (!grid.InBounds(x, y) && HasBackWall(x, y)) terrain.SetTile(new Vector3Int(x, y, 0), ts.bedrock[0]);

            grid.TileChanged += OnTileChanged;
        }

        /// <summary>Solid masses up to this many tiles that touch nothing else count as floating islands outdoors.</summary>
        const int IslandMaxTiles = 40;

        /// <summary>
        /// True where the back wall is drawn behind a cell. Indoors: everywhere. Outdoors: only below the terrain
        /// surface - above it, and behind floating islands, the sky shows.
        /// </summary>
        public bool HasBackWall(int x, int y)
        {
            if (ts == null || !ts.outdoor || wall == null) return true;
            if (grid.InBounds(x, y)) return wall[x, y];
            return y <= SurfaceAt(x);
        }

        void BuildOutdoorWall(int w, int h)
        {
            // Split the solid tiles into connected masses: big ones are the ground, small ones are floating islands.
            var comp = new int[w, h];
            var sizes = new System.Collections.Generic.List<int> { 0 };
            var stack = new System.Collections.Generic.Stack<Vector2Int>();
            for (int y0 = 0; y0 < h; y0++)
                for (int x0 = 0; x0 < w; x0++)
                {
                    if (comp[x0, y0] != 0 || !grid.IsSolid(x0, y0)) continue;
                    int id = sizes.Count, n = 0;
                    comp[x0, y0] = id; stack.Push(new Vector2Int(x0, y0));
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop(); n++;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || comp[nx, ny] != 0 || !grid.IsSolid(nx, ny)) continue;
                            comp[nx, ny] = id; stack.Push(new Vector2Int(nx, ny));
                        }
                    }
                    sizes.Add(n);
                }
            bool Ground(int x, int y) => comp[x, y] != 0 && sizes[comp[x, y]] > IslandMaxTiles;

            // The terrain surface of each column: its highest ground tile. Above it the sky backdrop shows.
            var top = new int[w];
            for (int x = 0; x < w; x++)
            {
                top[x] = -1;
                for (int y = h - 1; y >= 0; y--) if (Ground(x, y)) { top[x] = y; break; }
            }
            // A deep hole with ground on both sides (shaft, drop hole) stays "underground" up to the lower rim.
            // Shallow dips such as thorn pits get no wall: the sky shows right behind them.
            surface = new int[w];
            for (int x = 0; x < w; x++)
            {
                int l = -1, r = -1;
                for (int k = 1; k <= 4; k++)
                {
                    l = Mathf.Max(l, top[Mathf.Clamp(x - k, 0, w - 1)]);
                    r = Mathf.Max(r, top[Mathf.Clamp(x + k, 0, w - 1)]);
                }
                int rim = Mathf.Min(l, r);
                surface[x] = rim - top[x] >= 3 ? rim : top[x];
            }
            wall = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h && y <= surface[x]; y++) wall[x, y] = true;
        }

        void OnDestroy()
        {
            if (grid != null) grid.TileChanged -= OnTileChanged;
        }

        TileBase BackgroundTile(int x, int y)
        {
            if (grid.InBounds(x, y) && shopCells != null && shopCells[x, y] && ts.shopBackground != null) return ts.shopBackground;
            if (ts.background == null || ts.background.Length == 0) return null;
            int hsh = Rng.Hash(x * 73 + 11, y * 37 + 5);
            // Mostly variant 0/1, occasionally the detailed ones.
            int v = (hsh % 10) < 7 ? (hsh % 2) : 2 + (hsh % (ts.background.Length - 2 > 0 ? ts.background.Length - 2 : 1));
            return ts.background[Mathf.Clamp(v, 0, ts.background.Length - 1)];
        }

        void OnTileChanged(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (grid.InBounds(x + dx, y + dy)) RefreshCell(x + dx, y + dy);
            // decor that lost its support disappears
            ValidateDecor(x, y);
            ValidateDecor(x, y + 1);
            ValidateDecor(x, y - 1);
        }

        int Mask(int x, int y)
        {
            int m = 0;
            if (!grid.IsSolid(x, y + 1)) m |= 1;
            if (!grid.IsSolid(x + 1, y)) m |= 2;
            if (!grid.IsSolid(x, y - 1)) m |= 4;
            if (!grid.IsSolid(x - 1, y)) m |= 8;
            return m;
        }

        public void RefreshCell(int x, int y)
        {
            var p = new Vector3Int(x, y, 0);
            var t = grid.Get(x, y);
            TileBase tile = null, over = null;
            switch (t)
            {
                case TileType.Dirt:
                case TileType.GoldVein:
                case TileType.GemVein:
                {
                    int v = Rng.Hash(x, y) % 2;
                    tile = ts.dirt[Mask(x, y) + 16 * v];
                    if (t == TileType.GoldVein) over = ts.goldOverlay;
                    else if (t == TileType.GemVein) over = ts.gemOverlay;
                    break;
                }
                case TileType.Bedrock: tile = ts.bedrock[Mask(x, y)]; break;
                case TileType.Brick: tile = ts.brick != null && ts.brick.Length == 16 && ts.brick[0] != null ? ts.brick[Mask(x, y)] : ts.bedrock[Mask(x, y)]; break;
                case TileType.Platform: tile = ts.platform; break;
                case TileType.Ladder: tile = ts.ladder; break;
                case TileType.Spikes: tile = ts.spikes; break;
                case TileType.ArrowTrap: tile = ts.trapBlock; break;
                case TileType.VentBlock: tile = ts.ventBlock; break;
                case TileType.Rubble: tile = ts.rubble != null ? ts.rubble : ts.trapBlock; break;
            }
            terrain.SetTile(p, tile);
            overlay.SetTile(p, over);
            if (shade != null && ts.shade != null && ts.shade.Length == 16)
            {
                TileBase sh = null;
                if (!grid.IsSolid(x, y) && HasBackWall(x, y))
                {
                    int solidSides = ~Mask(x, y) & 15;
                    if (solidSides != 0) sh = ts.shade[solidSides];
                }
                shade.SetTile(p, sh);
            }
        }

        void PlaceDecor(int x, int y)
        {
            if (!grid.IsEmpty(x, y)) return;
            int hsh = Rng.Hash(x * 7 + 3, y * 13 + 1);
            if ((hsh % 1000) / 1000f > ts.decorChance) return;
            if (grid.IsSolid(x, y - 1) && ts.decorFloor != null && ts.decorFloor.Length > 0)
                decor.SetTile(new Vector3Int(x, y, 0), ts.decorFloor[(hsh / 1000) % ts.decorFloor.Length]);
            else if (grid.IsSolid(x, y + 1) && ts.decorCeiling != null && ts.decorCeiling.Length > 0)
                decor.SetTile(new Vector3Int(x, y, 0), ts.decorCeiling[(hsh / 1000) % ts.decorCeiling.Length]);
        }

        void ValidateDecor(int x, int y)
        {
            var p = new Vector3Int(x, y, 0);
            if (decor.GetTile(p) == null) return;
            if (!grid.IsEmpty(x, y) || (!grid.IsSolid(x, y - 1) && !grid.IsSolid(x, y + 1))) decor.SetTile(p, null);
        }

        public Color DebrisColor(TileType t)
        {
            switch (t)
            {
                case TileType.Ladder:
                case TileType.Platform: return new Color(0.55f, 0.36f, 0.2f);
                case TileType.Spikes: return new Color(0.7f, 0.7f, 0.75f);
                case TileType.GoldVein: return new Color(0.95f, 0.8f, 0.3f);
                case TileType.Brick: return new Color(0.45f, 0.45f, 0.55f);
                case TileType.Rubble: return new Color(0.5f, 0.42f, 0.4f);
            }
            var a = World.Instance != null ? World.Instance.Area : null;
            if (a != null && a.areaNumber == 2) return new Color(0.5f, 0.27f, 0.22f);
            if (a != null && a.groundGrip < 1f) return new Color(0.62f, 0.78f, 0.9f);
            return new Color(0.45f, 0.3f, 0.2f);
        }
    }
}
