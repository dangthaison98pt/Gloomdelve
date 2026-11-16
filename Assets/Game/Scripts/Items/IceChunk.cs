using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// What a frost bat leaves behind: pick it up and throw it like a rock. It shatters on the first hard hit
    /// after being thrown. Merely falling (when the bat dies) neither breaks it nor hurts anyone.
    /// </summary>
    public class IceChunk : Item
    {
        public float shatterSpeed = 6f;

        protected override void HitEntities()
        {
            if (thrower == null) return;
            base.HitEntities();
        }

        protected override void OnImpact(float speed)
        {
            base.OnImpact(speed);
            if (!Dead && thrower != null && speed >= shatterSpeed) Break();
        }

        public override void Place(Vector2 v)
        {
            base.Place(v);   // put down gently: thrower cleared, so it will not shatter
        }

        public override void Break()
        {
            if (Dead) return;
            Fx.Debris(Center, new Color(0.7f, 0.88f, 1f), 8);
            Sfx.Play("shatter", pos);
            Despawn();
        }
    }
}
