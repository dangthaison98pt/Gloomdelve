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
        public SfxLibrary sfx;
        [Tooltip("Every equipment type in the game (selectable in the gear wheel when owned).")]
        public EquipmentDef[] equipment;

        [Header("Run structure")]
        public StageDef[] stages;
        public StageDef tutorial;
        public TextAsset roomTemplates;
        [Tooltip("Extra room-template symbols: place any PrefabRegistry id in rooms without code (see Docs/EXTENDING.md).")]
        public List<MarkerDef> customMarkers = new List<MarkerDef>();

        [Header("Time pressure (Gloom Wraith)")]
        public bool pressureEnabled = true;
        [Tooltip("Seconds in a level before the warning.")] public float pressureWarnTime = 150f;
        [Tooltip("Seconds in a level before the wraith appears.")] public float pressureSpawnTime = 180f;
        [Tooltip("Another wraith every N seconds after the first.")] public float pressureRepeat = 60f;

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
    }
}
