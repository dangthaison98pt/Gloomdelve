using System;

namespace Gloomdelve
{
    public enum TileType : byte
    {
        Empty = 0,
        Dirt = 1,        // destructible
        Bedrock = 2,     // indestructible, distinct visuals
        Platform = 3,    // one-way wooden platform
        Ladder = 4,      // climbable; top-most ladder cell acts as a one-way platform
        Spikes = 5,      // non-solid; kills when falling into it
        ArrowTrap = 6,   // solid destructible block with a trap entity
        GoldVein = 7,    // dirt with gold inside (drops gold when destroyed)
        GemVein = 8,     // dirt with a gem inside
        VentBlock = 9,   // solid destructible block with a flame vent entity
        Brick = 10,      // destructible stone brick (final vault)
        Rubble = 11,     // cracked rubble: blocks the way, cleared by bombs (bomb puzzle gates)
    }

    /// <summary>Pure data tile grid. (0,0) is bottom-left; y goes up. Out of bounds counts as bedrock.</summary>
    public sealed class TileGrid
    {
        public readonly int Width, Height;
        readonly TileType[] tiles;
        readonly byte[] ropes;

        public event Action<int, int> TileChanged;

        public TileGrid(int width, int height)
        {
            Width = width; Height = height;
            tiles = new TileType[width * height];
            ropes = new byte[width * height];
        }

        public TileGrid Clone()
        {
            var g = new TileGrid(Width, Height);
            Array.Copy(tiles, g.tiles, tiles.Length);
            Array.Copy(ropes, g.ropes, ropes.Length);
            return g;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TileType Get(int x, int y) => InBounds(x, y) ? tiles[y * Width + x] : TileType.Bedrock;

        public void Set(int x, int y, TileType t)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            if (tiles[i] == t) return;
            tiles[i] = t;
            TileChanged?.Invoke(x, y);
        }

        /// <summary>Set without raising change events (used during generation).</summary>
        public void SetRaw(int x, int y, TileType t)
        {
            if (InBounds(x, y)) tiles[y * Width + x] = t;
        }

        public static bool IsSolidType(TileType t) =>
            t == TileType.Dirt || t == TileType.Bedrock || t == TileType.ArrowTrap || t == TileType.GoldVein ||
            t == TileType.GemVein || t == TileType.VentBlock || t == TileType.Brick || t == TileType.Rubble;

        public bool IsSolid(int x, int y) => IsSolidType(Get(x, y));

        /// <summary>One-way surface you can stand on from above.</summary>
        public bool IsPlatformTop(int x, int y)
        {
            var t = Get(x, y);
            if (t == TileType.Platform) return true;
            if (t == TileType.Ladder && Get(x, y + 1) != TileType.Ladder) return true;
            return false;
        }

        public bool IsLadder(int x, int y) => Get(x, y) == TileType.Ladder;
        public bool HasRope(int x, int y) => InBounds(x, y) && ropes[y * Width + x] > 0;
        public bool IsClimbable(int x, int y) => Get(x, y) == TileType.Ladder || HasRope(x, y);
        public bool IsSpike(int x, int y) => Get(x, y) == TileType.Spikes;
        public bool IsEmpty(int x, int y) => Get(x, y) == TileType.Empty;

        public bool IsDestructible(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var t = Get(x, y);
            return t != TileType.Empty && t != TileType.Bedrock;
        }

        public void AddRope(int x, int y, int delta)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            int v = ropes[i] + delta;
            ropes[i] = (byte)Math.Max(0, Math.Min(255, v));
        }
    }
}
