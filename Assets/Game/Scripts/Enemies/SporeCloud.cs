using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom: a cloud of spores left by a puffshroom. It does no damage: while the player stands in it (and
    /// for a moment after) left and right are swapped. A spore mask (StatModifiers.sporeResist) makes it harmless.
    /// It drifts up slowly and thins out.
    /// </summary>
    public class SporeCloud : Entity
    {
        public float lifetime = 3.2f;
        [Tooltip("How long left / right stay swapped after the last breath of spores.")] public float dizzyTime = 2f;
        public float riseSpeed = 0.25f;
        public Color color = new Color(0.62f, 1f, 0.5f);
        [Tooltip("Size of the picture relative to its pixels (the hitbox is the entity size).")] public float spriteScale = 1.4f;

        float life;

        public override bool IsHazard => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            life = lifetime;
            // centred on the spot it was spawned at
            pos = new Vector2(pos.x, pos.y - size.y * 0.5f);
            prevPos = pos;
        }

        public override void Tick(float dt)
        {
            life -= dt;
            pos.y += riseSpeed * dt;
            if (life <= 0f) { Despawn(); return; }
            var p = World.Player;
            if (p != null && !p.IsDead && life > 0.3f && Overlaps(p)) p.Confuse(dizzyTime);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            float k = Mathf.Clamp01(life / lifetime);
            float grow = spriteScale * (1f + (1f - k) * 0.25f);
            sr.transform.localScale = new Vector3(grow, grow, 1f);
            sr.color = new Color(color.r, color.g, color.b, Mathf.Min(0.75f, k * 1.6f));
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Despawn(); }   // a blast blows it away
    }
}
