using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Thrown by the Yeti: flies in an arc, bursts on terrain, and shoves the player without hurting.</summary>
    public class Snowball : Entity
    {
        public float gravity = 28f;
        [Tooltip("Horizontal / vertical speed given to the player it hits.")] public Vector2 shove = new Vector2(7.5f, 4.5f);
        public float stunTime = 0.35f;
        [Tooltip("How long the player is frozen (no fur coat).")] public float freezeTime = 1.1f;
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
                if (run != null && run.stats.coldResist > 0f) p.Shove(new Vector2(dir * shove.x * 0.4f, 2f), 0f);   // warm: a nudge
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
