using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The length of wrapping a mummy casts at the player. It flies straight and does no harm by itself: whoever
    /// it catches is dragged towards the mummy and tangled for a moment (slowed) - into the reach of its grab.
    /// The blade cuts it out of the air; it falls apart on a wall.
    /// </summary>
    public class MummyWrap : Entity
    {
        public float lifetime = 0.7f;
        [Tooltip("The fastest whoever is caught is dragged towards the mummy (less from close by: the drag ends in front of it, not past it).")] public float pull = 11f;
        [Tooltip("How far in front of the mummy the drag ends (tiles).")] public float stopShort = 0.8f;
        public float slow = 0.6f;
        public float slowTime = 1.1f;
        public Color burstColor = new Color(0.9f, 0.86f, 0.7f);

        [System.NonSerialized] public Entity owner;
        float life;

        public override void OnSpawn()
        {
            base.OnSpawn();
            life = lifetime;
        }

        public override void Tick(float dt)
        {
            life -= dt;
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos;
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
            {
                if (!p.godMode)
                {
                    float toward = owner != null && !owner.Dead ? Mathf.Sign(owner.pos.x - p.pos.x) : -Mathf.Sign(vel.x);
                    // (a shove at speed v carries the player about 0.28 v tiles: just enough to end up in front of the mummy)
                    float dist = owner != null && !owner.Dead ? Mathf.Abs(owner.pos.x - p.pos.x) : 2f;
                    float speed = Mathf.Clamp((dist - stopShort) / 0.28f, 0f, pull);
                    p.Shove(new Vector2(toward * speed, 5f), 0.3f);
                    p.Slow(slow, slowTime);
                    Fx.Text(p.Center + Vector2.up * 0.9f, "CAUGHT", new Color(0.95f, 0.9f, 0.7f), 0.7f);
                    Sfx.Play("grab", pos, 1f, 0.7f);
                }
                Burst();
                return;
            }
            if (b.hitLeft || b.hitRight || b.hitCeiling || life <= 0f) Burst();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null) sr.flipX = vel.x < 0f;
        }

        void Burst()
        {
            Fx.Debris(Center, burstColor, 5);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { Sfx.Play("hit", pos); Burst(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }
    }
}
