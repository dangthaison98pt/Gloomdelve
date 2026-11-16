using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Static decoration entity (altar, shop sign...). Ignores everything.</summary>
    public class Prop : Entity
    {
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
