using System;
using UnityEngine;

namespace Gloomdelve
{
    [Serializable]
    public class WeightedId
    {
        public string id;
        public float weight = 1f;
        public WeightedId() { }
        public WeightedId(string id, float weight) { this.id = id; this.weight = weight; }

        public static string Pick(WeightedId[] list, Rng rng)
        {
            if (list == null || list.Length == 0) return null;
            float total = 0f;
            foreach (var w in list) total += Mathf.Max(0f, w.weight);
            if (total <= 0f) return null;
            float r = rng.Value() * total;
            foreach (var w in list)
            {
                r -= Mathf.Max(0f, w.weight);
                if (r <= 0f) return w.id;
            }
            return list[list.Length - 1].id;
        }
    }

    [Serializable]
    public class StageDef
    {
        public string displayName = "1-1";
        public LevelKind kind = LevelKind.Area;
        public AreaConfig area;
        [Tooltip("1-based level number inside the area (used for difficulty scaling).")] public int levelNumber = 1;
        [Tooltip("Hand-made layout (Final / Tutorial only).")] public TextAsset handcrafted;

        /// <summary>The same stage under the name it has in one particular run ("3-2": third area of that run).</summary>
        public StageDef Named(string name) => new StageDef { displayName = name, kind = kind, area = area, levelNumber = levelNumber, handcrafted = handcrafted };
    }

    /// <summary>Resource ids understood by RunState.AddResource. Any other id is stored as a custom counter.</summary>
    public static class Res
    {
        public const string Gold = "gold", Health = "health", Bombs = "bombs", Ropes = "ropes";
        /// <summary>Raises the health limit by the amount and refills health completely.</summary>
        public const string MaxHealth = "maxhealth";
        /// <summary>Shot for the slingshot. Only so many can be carried (RunState.PebbleCap).</summary>
        public const string Pebbles = "pebbles";
    }

    /// <summary>Upgrade ids that game code asks about by name (RunState.HasUpgrade). Pure stat upgrades need no entry here.</summary>
    public static class UpgradeIds
    {
        public const string IronWhip = "ironwhip", Compass = "compass";
    }

    /// <summary>
    /// Passive stat bonuses of an upgrade. Everything is additive and 0 means "no change", so a new stat-only
    /// upgrade is just a new ShopConfig offer with numbers in here - no code.
    /// </summary>
    [Serializable]
    public class StatModifiers
    {
        [Tooltip("Added to the jump multiplier (0.17 = jump 17% stronger).")] public float jumpBonus;
        [Tooltip("Added to whip damage.")] public int whipDamageBonus;
        [Tooltip("Added to whip reach (tiles).")] public float whipRangeBonus;
        [Tooltip("Added to bomb blast radius (tiles).")] public float blastRadiusBonus;
        [Tooltip("Footing on ice: 1 = never slides (the ground grip of the level is raised to at least this).")] public float iceGrip;
        [Tooltip("Above 0: snowballs and other cold attacks cannot freeze the wearer.")] public float coldResist;
        [Tooltip("Above 0: spore clouds cannot make the wearer dizzy.")] public float sporeResist;
        [Tooltip("Added to the reach of the light the player carries (0.6 = 60% further). Helps in dark areas.")] public float lightBonus;
        [Tooltip("Above 0: conveyor belts do not carry the wearer.")] public float beltGrip;
        [Tooltip("Above 0: the wearer's blade goes through a sentry's shield.")] public float shieldPierce;
        [Tooltip("Above 0: quicksand neither slows nor swallows the wearer.")] public float sandGrip;
        [Tooltip("Above 0: a mummy's grab cannot take the wearer's gold.")] public float goldWard;
        [Tooltip("Above 0: the shards of a bursting crystal pass through the wearer.")] public float shardWard;
        [Tooltip("Above 0: a mimic lying still is shown with a red cast.")] public float mimicSight;
        [Tooltip("More pebbles can be carried: added to PlayerConfig.pebbleCapacity.")] public int pebbleBonus;
        [Tooltip("Added to the damage a stomp does (1 = a stomp hits for 2).")] public int stompBonus;
        [Tooltip("Above 0: something falling on the wearer's head (a stalactite, a dropped rock or pot) does 1 less damage.")] public float headGuard;
        [Tooltip("Added to the height of a fall before it hurts (tiles; 3 = a fall must be 12 tiles instead of 9).")] public float fallBonus;
        [Tooltip("Added to how far the view moves when looking up or down (1 = twice as far).")] public float lookBonus;

        public void Add(StatModifiers o)
        {
            if (o == null) return;
            jumpBonus += o.jumpBonus; whipDamageBonus += o.whipDamageBonus;
            whipRangeBonus += o.whipRangeBonus; blastRadiusBonus += o.blastRadiusBonus;
            iceGrip = Mathf.Max(iceGrip, o.iceGrip); coldResist += o.coldResist; sporeResist += o.sporeResist; lightBonus += o.lightBonus; beltGrip += o.beltGrip; shieldPierce += o.shieldPierce; sandGrip += o.sandGrip; goldWard += o.goldWard; shardWard += o.shardWard; mimicSight += o.mimicSight; pebbleBonus += o.pebbleBonus;
            stompBonus += o.stompBonus; headGuard += o.headGuard; fallBonus += o.fallBonus; lookBonus += o.lookBonus;
        }

        public void Clear()
        {
            jumpBonus = 0f; whipDamageBonus = 0; whipRangeBonus = 0f; blastRadiusBonus = 0f; iceGrip = 0f; coldResist = 0f; sporeResist = 0f; lightBonus = 0f; beltGrip = 0f; shieldPierce = 0f; sandGrip = 0f; goldWard = 0f; shardWard = 0f; mimicSight = 0f; pebbleBonus = 0; stompBonus = 0; headGuard = 0f; fallBonus = 0f; lookBonus = 0f;
        }

        public bool IsZero => jumpBonus == 0f && whipDamageBonus == 0 && whipRangeBonus == 0f && blastRadiusBonus == 0f && iceGrip == 0f && coldResist == 0f && sporeResist == 0f && lightBonus == 0f && beltGrip == 0f && shieldPierce == 0f && sandGrip == 0f && goldWard == 0f && shardWard == 0f && mimicSight == 0f && pebbleBonus == 0 && stompBonus == 0 && headGuard == 0f && fallBonus == 0f && lookBonus == 0f;
    }

    /// <summary>
    /// Something a shop sells. Consumable: gives 'amount' of 'resource'. Upgrade (isUpgrade): owned once per run,
    /// applies 'modifiers', and code can ask RunState.HasUpgrade(id) for bespoke behaviour.
    /// </summary>
    [Serializable]
    public class ShopOffer
    {
        public string id;
        public string displayName;
        public int price;
        [Tooltip("Resource granted: gold / health / bombs / ropes, or any custom counter id. Empty = none.")]
        public string resource;
        public int amount = 1;
        public bool isUpgrade;
        [Tooltip("Where an upgrade is worn: feet / body / hand / charm (two charms fit). Only worn items have an effect; the player swaps them on the equipment screen. Empty = always on.")]
        public string slot;
        [Tooltip("Passive bonuses while this upgrade is worn (upgrades only).")]
        public StatModifiers modifiers = new StatModifiers();
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("What its blueprint costs at a villager's stall, in embers (Blueprints). 0 = worked out from the gold price (about one ember for 250 gold).")]
        public int blueprintPrice;
    }

    public enum MarkerPlacement { Anywhere, OnFloor, UnderCeiling }

    /// <summary>
    /// A custom room-template symbol (GameConfig.customMarkers): lets designers place a prefab in room templates
    /// and hand-made levels without touching the generator. Built-in symbols are listed in RoomLegend.
    /// </summary>
    [Serializable]
    public class MarkerDef
    {
        [Tooltip("One character used in RoomTemplates.txt. Must not clash with a built-in symbol.")] public string symbol = "";
        public string label = "";
        [Tooltip("PrefabRegistry id to spawn in this cell.")] public string spawnId = "";
        [Range(0, 1)] public float chance = 1f;
        public MarkerPlacement placement = MarkerPlacement.OnFloor;
        [Tooltip("Tile written into the cell (usually Empty).")] public TileType tile = TileType.Empty;
        [Tooltip("Colour in the Room Template Editor.")] public Color color = new Color(0.9f, 0.5f, 0.9f);

        public char Char => string.IsNullOrEmpty(symbol) ? ' ' : symbol[0];
    }
}
