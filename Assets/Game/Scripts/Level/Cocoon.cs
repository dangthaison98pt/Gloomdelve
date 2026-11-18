using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A cocoon hanging on its thread in the Broodmother's den, out of reach of a jump. Whatever tears it - the
    /// Broodmother falling through it from the ceiling, a bomb, something thrown - lets out what is inside: one
    /// spiderling drops to the floor. So it matters where the player stands when she lets go: under a cocoon,
    /// her drop brings company.
    /// (Arena files: marker 'C'. The picture is three tiles tall, thread included; the cocoon is its lower part.)
    /// </summary>
    public class Cocoon : Entity
    {
        public string hatchId = "spider";
        [Tooltip("Height of the cocoon itself (the thread above it is only a picture).")] public float bodyHeight = 1.2f;

        float t;
        bool burst;

        public override void OnSpawn()
        {
            base.OnSpawn();
            t = pos.x * 0.7f;
        }

        public override void Tick(float dt)
        {
            // it sways a little on its thread
            t += dt;
            if (sr != null) sr.transform.localPosition = new Vector3(Mathf.Sin(t * 1.3f) * 0.05f, 0f, 0f);
        }

        /// <summary>Does this body (feet at 'at', given half width and height) touch the cocoon?</summary>
        public bool Touches(Vector2 at, float halfWidth, float height)
            => Mathf.Abs(at.x - pos.x) < halfWidth + size.x * 0.5f && at.y < pos.y + bodyHeight && at.y + height > pos.y;

        /// <summary>Torn open: the spiderling inside drops out.</summary>
        public void Burst()
        {
            if (burst || Dead) return;
            burst = true;
            var e = World.SpawnById(hatchId, pos);
            if (e is Enemy en) en.summoned = true;        // no gold for the brood of a boss
            Fx.Debris(new Vector2(pos.x, pos.y + bodyHeight * 0.5f), new Color(0.9f, 0.92f, 0.82f), 12);
            Sfx.Play("toad", pos, 0.8f, 1.5f);
            Despawn();
        }

        public override bool TakeDamage(in DamageInfo d) { if (burst || Dead) return false; Burst(); return true; }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }
        public override void OnWhipped(PlayerController p, int damage, int dir) { Burst(); }
    }
}
