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
    }

    public enum ShopEffect { Bombs, Ropes, Health, SpringBoots, IronWhip, Compass, BlastPowder }

    [Serializable]
    public class ShopOffer
    {
        public string id;
        public string displayName;
        public int price;
        public ShopEffect effect;
        public int amount = 1;
        public bool isUpgrade;
        public Sprite icon;
        [TextArea] public string description;
    }
}
