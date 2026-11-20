using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The kennel by the player's house in Brindlemoor. It keeps a dog (it puts the dog out itself when the
    /// village is built): see VillageDog.
    /// </summary>
    public class Kennel : Prop
    {
        public override void OnSpawn()
        {
            base.OnSpawn();
            var cfg = World.config;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("villagedog") : null;
            if (prefab == null) return;
            var cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.RoundToInt(pos.y));
            var dog = World.Spawn(prefab, new Vector2(pos.x - 1.1f, pos.y), new SpawnPoint("villagedog", cell)) as VillageDog;
            if (dog != null) dog.SetHome(new Vector2(pos.x - 1.1f, pos.y));
        }
    }
}
