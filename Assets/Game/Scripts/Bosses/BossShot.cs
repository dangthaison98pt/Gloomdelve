using UnityEngine;

namespace Gloomdelve
{
    /// <summary>A boss projectile: flies straight, hurts the player it touches, bursts on terrain. A whip crack destroys it.</summary>
    public class BossShot : Entity
    {
        public int damage = 1;
        public float lifetime = 4f;
        public Color burstColor = new Color(0.7f, 0.5f, 1f);

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
            Vector2 v = vel;
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos;
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
            {
                p.TakeDamage(new DamageInfo(damage, DamageType.Contact, owner, new Vector2(Mathf.Sign(v.x) * 5f, 4f),
                    owner != null ? owner.displayName : displayName));
                Burst();
                return;
            }
            if (b.hitLeft || b.hitRight || b.hitCeiling || b.landed || b.grounded || life <= 0f) Burst();
        }

        void Burst()
        {
            Fx.Debris(Center, burstColor, 6);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { Sfx.Play("hit", pos); Burst(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }
    }
}
