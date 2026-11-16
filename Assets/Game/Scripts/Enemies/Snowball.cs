using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Thrown by the Yeti: flies in an arc and bursts on terrain. It does no damage: it shoves the player and
    /// freezes them for a moment; with cold resistance (fur coat) it only slows them down for a while.</summary>
    public class Snowball : Entity
    {
        public float gravity = 28f;
        [Tooltip("0 = harmless (the default): the snowball only freezes or slows.")] public int damage = 0;
        [Tooltip("Horizontal / vertical speed given to the player it hits.")] public Vector2 shove = new Vector2(7.5f, 4.5f);
        public float stunTime = 0.35f;
        [Tooltip("How long the player is frozen (no cold resistance).")] public float freezeTime = 0.5f;
        [Tooltip("With cold resistance: movement speed lost (0.3 = 30% slower) ...")] [Range(0, 1)] public float slowAmount = 0.3f;
        [Tooltip("... for this many seconds.")] public float slowTime = 1f;
        public float lifetime = 4f;

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
            int dir = vel.x >= 0f ? 1 : -1;
            var b = StepPhysics(dt, gravity, 30f);
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
            {
                var run = RunSession.Current;
                bool warm = run != null && run.stats.coldResist > 0f;
                if (damage > 0)
                    p.TakeDamage(new DamageInfo(damage, DamageType.Contact, owner, warm ? new Vector2(dir * shove.x * 0.4f, 2f) : new Vector2(dir * shove.x, shove.y),
                        owner != null ? owner.displayName + "'s snowball" : "a snowball"));
                if (p.IsDead) { Burst(); return; }
                if (warm) p.Slow(slowAmount, slowTime);                                   // resists the freeze: only slowed
                else { p.Shove(new Vector2(dir * shove.x, shove.y), stunTime); p.Freeze(freezeTime); }
                Burst();
                return;
            }
            if (b.hitLeft || b.hitRight || b.hitCeiling || b.landed || grounded || life <= 0f || pos.y < -4f) Burst();
        }

        void Burst()
        {
            Fx.Debris(Center, new Color(0.92f, 0.97f, 1f), 7);
            Sfx.Play("thud", pos, 0.5f, 1.6f);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { Burst(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }
    }
}
