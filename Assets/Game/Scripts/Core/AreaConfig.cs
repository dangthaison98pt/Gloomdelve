using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Area Config", fileName = "Area")]
    public class AreaConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Mossroot Caverns";
        public int areaNumber = 1;
        public TileSet tiles;
        public string musicId = "area1";
        public Color cameraBackground = new Color(0.07f, 0.05f, 0.05f);

        [Header("Layout")]
        public int roomsX = 4;
        public int roomsY = 4;
        [Tooltip("Outdoor area: open sky above the top rooms, sky backdrop, trees, no ceiling border.")]
        public bool outdoor;
        [Tooltip("Empty sky rows added above the rooms (outdoor only).")]
        public int skyRows = 7;
        [Range(0, 1)] public float treeChance = 0.25f;

        [Header("Footing")]
        [Tooltip("1 = normal ground. Lower = ice: the player slides when stopping and is slower to turn (0.25 = very slippery).")]
        [Range(0.1f, 1f)] public float groundGrip = 1f;

        [Header("Enemies (ids from PrefabRegistry)")]
        public WeightedId[] groundEnemies = { new WeightedId("snake", 1f) };
        public WeightedId[] ceilingEnemies = { new WeightedId("bat", 1f) };
        [Range(0, 1)] public float groundEnemyChance = 0.45f;
        [Range(0, 1)] public float ceilingEnemyChance = 0.4f;
        [Tooltip("Extra random ground enemies per room (path rooms).")] public float extraEnemiesPerRoom = 0.25f;
        [Tooltip("Flying enemies placed in open air (e.g. hawks).")]
        public WeightedId[] airEnemies = new WeightedId[0];
        public float airEnemiesPerLevel = 0f;
        [Tooltip("Multiplier for enemy/trap chances in side rooms (off the main path) - more risk, more reward.")]
        public float sideRoomRiskMultiplier = 1.4f;

        [Header("Items & treasure")]
        public WeightedId[] items = { new WeightedId("rock", 2f), new WeightedId("pot", 3f), new WeightedId("crate", 1f) };
        [Range(0, 1)] public float itemChance = 0.5f;
        [Range(0, 1)] public float treasureChancePath = 0.35f;
        [Range(0, 1)] public float treasureChanceSide = 0.7f;
        [Range(0, 1)] public float goldVeinChance = 0.035f;
        [Range(0, 1)] public float gemVeinChance = 0.008f;
        [Range(0, 1)] public float idolRoomChance = 0.35f;
        [Range(0, 1)] public float vaultRoomChance = 0.3f;

        [Header("Traps")]
        public bool spikes = true;
        public bool arrowTraps = true;
        public bool stalactites = true;
        public bool crushers = false;
        public bool flameVents = false;
        [Range(0, 1)] public float spikeDensity = 0.04f;
        [Range(0, 1)] public float arrowTrapDensity = 0.025f;
        [Range(0, 1)] public float ceilingHazardChance = 0.6f;
        [Range(0, 1)] public float markedTrapChance = 0.75f;

        [Header("Difficulty scaling per level (added per level beyond the first)")]
        public float perLevelDensityBonus = 0.15f;

        [Header("Terrain puzzles (required gear)")]
        [Tooltip("Equipment id required to get through this area's obstacles: 'rope' (sheer cliffs) or 'bomb' (rubble walls). Empty = none.")]
        public string requiredGear = "";
        [Tooltip("Number of obstacle rooms on the main path for level 1, 2, 3.")]
        public int[] gatesByLevel = { 1, 1, 2 };
        [Tooltip("Equipment given to the player when the area starts (if not owned yet), e.g. 'glider'.")]
        public string grantGear = "";

        [Header("Lighting & atmosphere (visual only)")]
        [Tooltip("Colour of the ambient light. Caves: cool and dim; outdoors: bright and warm.")]
        public Color ambientColor = new Color(0.62f, 0.7f, 0.85f);
        [Tooltip("Brightness away from any light source. Below 0 = not set yet (Build Everything fills in the built-in areas).")]
        public float ambientIntensity = -1f;
        [Tooltip("Light carried by the player (0 = none).")] public float playerLightIntensity = 0.85f;
        public float playerLightRadius = 7.5f;
        public Color torchColor = new Color(1f, 0.72f, 0.4f);
        [Tooltip("Chance of a wall torch on each suitable spot (0 = no torches).")] [Range(0, 0.3f)] public float torchChance = 0.06f;
        public AmbientParticles ambientParticles = AmbientParticles.Dust;
        public int ambientParticleCount = 40;
        [Tooltip("Tint of the parallax rock layers behind the terrain (indoor areas).")]
        public Color backdropTint = new Color(0.42f, 0.36f, 0.34f);

        [Header("Shop")]
        [Tooltip("Chance of a shop on level 1,2,3 of this area.")]
        public float[] shopChanceByLevel = { 0f, 1f, 0.4f };
    }
}
