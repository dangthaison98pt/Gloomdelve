using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// What a piece of equipment does. One subclass per gear, registered in GearRegistry under the id of its
    /// EquipmentDef asset. To add a new gear:
    ///   1. write a subclass (see BuiltinGear.cs),
    ///   2. add one Register line in GearRegistry,
    ///   3. create an EquipmentDef asset with the same id and add it to GameConfig.equipment.
    /// Nothing in PlayerController, the HUD, the wheel or GameDirector needs to change.
    /// </summary>
    public abstract class GearBehaviour
    {
        /// <summary>Remaining uses; -1 = never used up.</summary>
        public virtual int Ammo(RunState run) => -1;

        /// <summary>The "Use gear" button was pressed with this gear equipped.</summary>
        public abstract void Use(PlayerController p, FrameInput fi);

        /// <summary>Called every simulation step while equipped: change how the player moves (e.g. allow gliding).</summary>
        public virtual void ModifyMotor(PlayerController p, ref MotorInput mi) { }

        /// <summary>
        /// True for gear that changes movement just by being equipped. The autotest bot only keeps such gear
        /// equipped in levels whose gates need it, so its replay matches the validator's plan.
        /// </summary>
        public virtual bool AffectsMovement => false;

        /// <summary>
        /// Level validation: give the path search the abilities this gear provides when a level's gates require it.
        /// (Terrain changes such as cleared rubble and hooked ropes are handled by LevelValidator.GearGrid.)
        /// </summary>
        public virtual void ApplyToValidator(ref LevelValidator.Options o) { }
    }

    public static class GearRegistry
    {
        static readonly Dictionary<string, GearBehaviour> map = new Dictionary<string, GearBehaviour>();

        static GearRegistry()
        {
            Register("bomb", new BombGear());
            Register("rope", new RopeGear());
            Register("glider", new GliderGear());
            Register("recall", new RecallGear());
            // New gear: add a Register line here.
        }

        public static void Register(string id, GearBehaviour behaviour) => map[id] = behaviour;

        public static GearBehaviour Get(string id) => id != null && map.TryGetValue(id, out var b) ? b : null;

        public static IEnumerable<string> Ids => map.Keys;
    }
}
