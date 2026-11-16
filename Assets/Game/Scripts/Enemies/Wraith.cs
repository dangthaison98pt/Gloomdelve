using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Time pressure: drifts through walls toward the player. Touching it is fatal. Cannot be hurt.</summary>
    public class Wraith : Enemy
    {
        public float speed = 1.5f;
        public float speedGrowth = 0.04f;
        public float maxSpeed = 3.2f;
        public SpriteAnim animFloat;

        float t;

        public override DamageType ContactType => DamageType.Wraith;
        public override bool CanBeHitByItems => false;
        public override bool TriggersTraps => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            anim?.Play(animFloat);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.85f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            var p = Player;
            if (p == null || p.IsDead) { vel = Vector2.MoveTowards(vel, Vector2.zero, dt); pos += vel * dt; return; }
            speed = Mathf.Min(maxSpeed, speed + speedGrowth * dt);
            Vector2 to = p.Center - Center;
            facing = to.x >= 0 ? 1 : -1;
            vel = to.normalized * speed + new Vector2(0f, Mathf.Sin(t * 2.5f) * 0.6f);
            pos += vel * dt;
            if (Random.value < 0.15f) Fx.Sparks(Center + Random.insideUnitCircle * 0.5f, new Color(0.6f, 0.9f, 1f, 0.7f), 1, 0.8f);
        }

        public override bool TakeDamage(in DamageInfo d) => false;
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
