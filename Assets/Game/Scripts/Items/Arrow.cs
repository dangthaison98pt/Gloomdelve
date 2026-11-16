using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Fired by arrow traps. Flies flat while fast, then becomes a normal throwable item.</summary>
    public class Arrow : Item
    {
        public int fastDamage = 2;
        public float fastSpeed = 9f;

        protected override float CurrentGravity => Mathf.Abs(vel.x) > fastSpeed ? 3f : gravity;

        public void Fire(Entity trap, int dir, float speed)
        {
            thrower = trap;
            throwerIgnore = 0.15f;
            vel = new Vector2(dir * speed, 0.4f);
            facing = dir;
            dangerous = true;
            grounded = false;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Mathf.Abs(vel.x) > 0.5f) facing = vel.x > 0 ? 1 : -1;
        }

        protected override int DamageFor(Entity target) => Mathf.Abs(vel.x) > fastSpeed ? fastDamage : throwDamage;

        protected override void HitEntities()
        {
            // Arrows fired by traps count as arrow damage for the death message.
            if (thrower is ArrowTrap)
            {
                var r = Bounds;
                var list = new System.Collections.Generic.List<Entity>();
                World.Query(r, list, e => e != this && e.CanBeHitByItems);
                foreach (var e in list)
                {
                    if (e == thrower && throwerIgnore > 0f) continue;
                    if (e.TakeDamage(new DamageInfo(DamageFor(e), DamageType.Arrow, this, new Vector2(Mathf.Sign(vel.x) * 4f, 3f), "arrow")))
                    {
                        vel.x *= -0.2f; vel.y = 2f; dangerous = false; thrower = null;
                        Sfx.Play("thud", pos);
                        return;
                    }
                }
                return;
            }
            base.HitEntities();
        }

        public override void Throw(Entity by, Vector2 v)
        {
            base.Throw(by, v * 1.2f);
        }
    }
}
