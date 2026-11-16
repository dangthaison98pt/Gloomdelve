using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Collected on touch: gold, gems, bomb bags, rope piles.</summary>
    public class Pickup : Entity
    {
        public enum Kind { Gold, Bombs, Ropes, Health }

        [Header("Pickup")]
        public Kind kind = Kind.Gold;
        public int value = 25;
        public string sfx = "coin";
        public Color textColor = new Color(1f, 0.85f, 0.3f);
        public float gravity = 30f;

        float age;

        public override void Tick(float dt)
        {
            age += dt;
            var b = StepPhysics(dt, gravity);
            if (b.landed && -b.impactVelY > 3f) { vel.y = -b.impactVelY * 0.3f; grounded = false; }
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.4f;
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 12f * dt);
            if (pos.y < -4f) Despawn();
        }

        public void Collect(PlayerController p)
        {
            if (Dead || age < 0.15f) return;
            var run = RunSession.Current;
            if (run == null) return;
            switch (kind)
            {
                case Kind.Gold: run.gold += value; Fx.Text(Center + Vector2.up * 0.4f, "+" + value, textColor, 0.8f); break;
                case Kind.Bombs: run.bombs += value; Fx.Text(Center + Vector2.up * 0.4f, "+" + value + " BOMBS", textColor); break;
                case Kind.Ropes: run.ropes += value; Fx.Text(Center + Vector2.up * 0.4f, "+" + value + " ROPES", textColor); break;
                case Kind.Health: run.health += value; Fx.Text(Center + Vector2.up * 0.4f, "+" + value + " HP", textColor); break;
            }
            Fx.Sparks(Center, textColor, 4, 2.5f);
            Sfx.Play(sfx, pos);
            Despawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            vel = d * 7f + Vector2.up * 3f;
            grounded = false;
        }
    }
}
