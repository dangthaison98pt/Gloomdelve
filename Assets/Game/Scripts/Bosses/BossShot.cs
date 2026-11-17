using UnityEngine;

namespace Gloomdelve
{
    /// <summary>A boss projectile: flies straight, hurts the player it touches, bursts on terrain. A whip crack destroys it.</summary>
    public class BossShot : Entity
    {
        public int damage = 1;
        public float lifetime = 4f;
        public Color burstColor = new Color(0.7f, 0.5f, 1f);
        [Tooltip("Turn the picture along the flight direction (art must point right): feathers, venom.")] public bool faceVelocity;
        [Tooltip("Mirror the picture when flying left (art must run right): shockwaves.")] public bool flipByVelocity;
        [Tooltip("The blade and blasts do nothing to it (the Rot Heart's tendril): it has to be avoided.")] public bool unbreakable;

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

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            if (faceVelocity && vel.sqrMagnitude > 0.01f)
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            if (flipByVelocity) sr.flipX = vel.x < 0f;
        }

        /// <summary>Recolours the shot (each boss has its own colour) and its burst.</summary>
        public void Tint(Color c)
        {
            burstColor = c;
            if (sr != null) sr.color = c;
        }

        void Burst()
        {
            Fx.Debris(Center, burstColor, 6);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { if (unbreakable) return; Sfx.Play("hit", pos); Burst(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { if (!unbreakable) Burst(); }
    }
}
