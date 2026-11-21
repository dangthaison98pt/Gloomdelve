using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A pebble out of the slingshot. It flies flat for a moment and then drops; the first thing it meets ends it:
    /// a creature takes one point (as from a blow of the blade), a pot or a crate is broken, an arrow trap is
    /// spent on it. It is gone after that - a shot is not picked up again.
    /// </summary>
    public class PebbleShot : Item
    {
        [Tooltip("Seconds it flies flat before it starts to drop (speed times this is its level reach).")] public float flatTime = 0.5f;

        float age;

        protected override float CurrentGravity => age < flatTime ? 0f : gravity;
        public override bool CanPickUp => false;

        /// <summary>Sent off by whoever shot it, this way (a unit direction) at this speed.</summary>
        public void Fire(Entity by, Vector2 dir, float speed)
        {
            thrower = by;
            throwerIgnore = 0.3f;
            vel = dir * speed;
            if (Mathf.Abs(dir.x) > 0.01f) facing = dir.x > 0f ? 1 : -1;
            dangerous = true;
            grounded = false;
            age = 0f;
        }

        public override void Tick(float dt)
        {
            age += dt;
            base.Tick(dt);
            if (!Dead && (grounded || age > 4f)) Shatter();      // (spent: it never lies about as something to pick up)
        }

        protected override void HitEntities()
        {
            // a pot or a crate in its way is broken open (a thrown thing only knocks them about)
            var r = Bounds;
            r.xMin -= 0.05f; r.xMax += 0.05f;
            var list = new System.Collections.Generic.List<Entity>();
            World.Query(r, list, e => e != this && e is Breakable && !e.Dead);
            foreach (var e in list) { ((Breakable)e).Break(); Shatter(); return; }
            base.HitEntities();
        }

        protected override void OnImpact(float speed) => Shatter();

        void Shatter()
        {
            if (Dead) return;
            Fx.Debris(Center, new Color(0.62f, 0.6f, 0.58f), 3);
            Sfx.Play(impactSfx, pos, 0.5f, 1.6f);
            Despawn();
        }
    }
}
