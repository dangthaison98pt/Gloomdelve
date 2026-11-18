using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Shop Config", fileName = "ShopConfig")]
    public class ShopConfig : ScriptableObject
    {
        public List<ShopOffer> offers = new List<ShopOffer>();
        [Tooltip("Shopkeeper health; the 3rd hit (after 2 warnings) makes him hostile and every item in his shop free to take.")]
        public int shopkeeperHealth = 6;
        [Tooltip("Hits by the player that only earn a warning (no damage). The next hit makes him attack.")]
        public int warningsBeforeAngry = 2;

        public ShopOffer Find(string id) => offers.Find(o => o.id == id);

        /// <summary>
        /// The healing a shelf carries, from a roll of 0-99: mostly the small tonic or the flask, now and then the
        /// elixir that restores everything. (Whatever of the three this shop file does not have falls back to the tonic.)
        /// </summary>
        public string HealOffer(int roll)
        {
            string id = roll < 45 ? "heart" : roll < 85 ? "heart3" : "heartfull";
            return Find(id) != null ? id : "heart";
        }
    }
}
