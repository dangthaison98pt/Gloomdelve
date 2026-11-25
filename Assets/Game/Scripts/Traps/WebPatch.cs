using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Laid by the Broodmother: a sheet of web on the floor. Walking through it is slow (no damage); jumping over
    /// it or through the air above it is not affected. It thins out and is gone after 'lifetime'. A blast clears it.
    /// </summary>
    public class WebPatch : Entity
    {
        public float lifetime = 6.5f;
        [Tooltip("Movement speed lost while standing in it (0.5 = half speed).")] [Range(0, 0.9f)] public float slow = 0.5f;

        float life;

        public override bool IsHazard => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            life = lifetime;
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Despawn(); return; }
            var p = World.Player;
            var run = RunSession.Current;
            bool immune = run != null && run.stats.venomGuard > 0f;          // (the Antidote Vial: webs do not hold)
            if (p != null && !p.IsDead && p.grounded && !immune && Overlaps(p)) p.Slow(slow, 0.25f);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null) sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(life / 1.2f) * 0.9f);
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Despawn(); }
    }
}
