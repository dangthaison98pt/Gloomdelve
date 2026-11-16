using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    [Serializable]
    public struct SpawnPoint
    {
        public string id;
        public Vector2Int cell;
        public int dir;
        public int param;
        public string text;

        public SpawnPoint(string id, Vector2Int cell, int dir = 0, int param = 0, string text = null)
        {
            this.id = id; this.cell = cell; this.dir = dir; this.param = param; this.text = text;
        }
    }

    /// <summary>Output of the generator / handcrafted parser. Pure data, no Unity objects.</summary>
    public class LevelData
    {
        public string name;
        public LevelKind kind;
        public int seed;
        public int attempts;
        public TileGrid grid;
        public Vector2Int entrance;
        public Vector2Int exit;
        public Vector2Int? goal;                 // final level: Heartstone altar cell
        public readonly List<SpawnPoint> spawns = new List<SpawnPoint>();
        public readonly List<RectInt> shopRects = new List<RectInt>();
        public readonly List<string> signs = new List<string>();
        public int[,] roomTypes;                 // procedural levels only
        public string[,] roomTemplates;
        public bool hasShop;
        public bool hasIdol;

        // Terrain puzzles
        /// <summary>Equipment id the main path requires ('rope' / 'bomb'), or null.</summary>
        public string gateGear;
        public int gateCount;
        public int gatesWanted;
        /// <summary>Cells where a player stands to throw a rope up a cliff (rope gates).</summary>
        public readonly List<Vector2Int> ropeSpots = new List<Vector2Int>();
        public ValidationResult validation;
        public List<string> notes = new List<string>();

        public int Width => grid.Width;
        public int Height => grid.Height;
    }
}
