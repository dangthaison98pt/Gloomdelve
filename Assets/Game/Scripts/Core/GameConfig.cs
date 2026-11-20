using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public PlayerConfig player;
        public ShopConfig shop;
        public PrefabRegistry prefabs;
        [Tooltip("Every quest of the game (the assets in Data/Quests).")] public QuestCatalog quests;
        public SfxLibrary sfx;
        [Tooltip("Every equipment type in the game (selectable in the gear wheel when owned).")]
        public EquipmentDef[] equipment;

        [Header("Run structure")]
        [Tooltip("The catalogue: one block of stages per area (its levels, then its boss arena), followed by the stages every run ends with (camp, final vault). Each run draws 'Areas Per Run' of the area blocks by its seed. See RunPlan.")]
        public StageDef[] stages;
        [Tooltip("How many areas one run goes through, drawn at random (by the run seed) from the catalogue. 0 = all of them in the written order.")]
        public int areasPerRun = 4;
        [Tooltip("On: every run starts in the first area of the catalogue and only the others are drawn at random. Off: every area is drawn at random, the first one too.")]
        public bool firstAreaFixed = false;
        public StageDef tutorial;
        [Tooltip("Brindlemoor: the village the player walks through before every run (hand-made level).")] public StageDef village;
        public TextAsset roomTemplates;
        [Tooltip("Extra room-template symbols: place any PrefabRegistry id in rooms without code (see Docs/EXTENDING.md).")]
        public List<MarkerDef> customMarkers = new List<MarkerDef>();

        [Header("Time pressure (the Reaper)")]
        public bool pressureEnabled = true;
        [Tooltip("Seconds in a level before the warning.")] public float pressureWarnTime = 270f;
        [Tooltip("Seconds in a level before the Reaper appears.")] public float pressureSpawnTime = 300f;
        [Tooltip("Another Reaper every N seconds after the first.")] public float pressureRepeat = 60f;

        [Header("Explosions / chain reactions")]
        public int explosionDamage = 10;
        public int maxExplosionsPerFrame = 3;
        public int maxChainGeneration = 12;
        public float chainFuseMin = 0.12f;
        public float chainFuseMax = 0.3f;
        [Tooltip("Hard cap on live entities to keep chain reactions / loot fountains bounded.")]
        public int maxEntities = 600;

        [Header("Generation")]
        public int maxGenerationAttempts = 60;
        [Tooltip("No traps or enemies this close (tiles) to the entrance.")] public int entranceSafeRadius = 4;
        public int enemySafeRadius = 6;

        [Header("Camera")]
        public float cameraOrthoSize = 6.75f;

        public EquipmentDef FindGear(string id)
        {
            if (equipment != null) foreach (var e in equipment) if (e != null && e.id == id) return e;
            return null;
        }

        [Header("Final level")]
        public float collapseInterval = 0.9f;
        public int heartstoneBonusGold = 5000;
        [HideInInspector] public int economyVersion = 0;
    }
}
