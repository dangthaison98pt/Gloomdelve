using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Data of one piece of equipment the player can carry and select in the gear wheel (hold R1 / D).
    /// What the gear DOES lives in a GearBehaviour registered under the same id (see Gear/GearBehaviour.cs).
    /// </summary>
    [CreateAssetMenu(menuName = "Brindlemoor/Equipment", fileName = "Gear")]
    public class EquipmentDef : ScriptableObject
    {
        public string id = "bomb";
        public string displayName = "Bombs";
        [TextArea] public string description;
        public Sprite icon;
        [Tooltip("Terrain this gear is meant for (shown in the wheel).")] public string terrainHint;

        [Header("Terrain puzzles (areas with requiredGear = this id)")]
        [Tooltip("Level banner when the level has gates for this gear. {0} = number of gates.")]
        [TextArea] public string gateBanner;
        [Tooltip("Text of the hint sign ('!' marker) in a gate room.")]
        [TextArea] public string gateSign;
        [Tooltip("PrefabRegistry id of the pickup dropped next to the entrance when the player lacks ammo for the gates. Empty = none.")]
        public string supplyPickupId;
        [Tooltip("How the ammo is called in messages, e.g. 'bomb(s)'.")]
        public string ammoLabel;
    }
}
