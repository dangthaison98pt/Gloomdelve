using UnityEngine;

namespace Gloomdelve
{
    /// <summary>What the "Use gear" button does. Add a new kind here + its behaviour in PlayerController.UseGear.</summary>
    public enum GearKind { Bomb, Rope, Glider }

    /// <summary>
    /// One piece of equipment the player can carry and select in the gear wheel (hold R1 / D).
    /// Ammo for consumable gear is stored in RunState (bombs, ropes).
    /// </summary>
    [CreateAssetMenu(menuName = "Gloomdelve/Equipment", fileName = "Gear")]
    public class EquipmentDef : ScriptableObject
    {
        public string id = "bomb";
        public string displayName = "Bombs";
        [TextArea] public string description;
        public Sprite icon;
        public GearKind kind = GearKind.Bomb;
        [Tooltip("Terrain this gear is meant for (shown in the wheel).")] public string terrainHint;
    }
}
