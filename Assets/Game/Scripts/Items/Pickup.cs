using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Collected on touch: gives 'value' of 'resource' (gold / health / bombs / ropes or a custom counter id,
    /// see RunState.AddResource). A new pickup type is just a prefab with different values.
    /// </summary>
    public class Pickup : Entity
    {
        [Header("Pickup")]
        [Tooltip("Resource id: gold / health / bombs / ropes, or a custom counter id.")]
        public string resource = Res.Gold;
        public int value = 25;
        [Tooltip("Word shown after the amount when collected, e.g. 'BOMBS' -> '+3 BOMBS'. Empty = just the number.")]
        public string label = "";
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
            run.AddResource(string.IsNullOrEmpty(resource) ? Res.Gold : resource, value);
            if (string.IsNullOrEmpty(label)) Fx.Text(Center + Vector2.up * 0.4f, "+" + value, textColor, 0.8f);
            else Fx.Text(Center + Vector2.up * 0.4f, "+" + value + " " + label, textColor);
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
