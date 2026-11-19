using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A shard thrown out by a bursting crystal (ResonantCrystal). It flies straight for a short way and cuts
    /// whatever it meets first: the player (unless they wear a Dampener), a creature of the geode, another crystal
    /// (which then rings in its turn). It cracks the shell of the Facet's fists. It breaks on rock.
    /// </summary>
    public class CrystalShard : Entity
    {
        public int damage = 1;
        [Tooltip("Seconds it flies (its reach is this times its speed).")] public float lifetime = 0.5f;
        public Color burstColor = new Color(0.6f, 0.95f, 1f);

        [System.NonSerialized] public Entity owner;
        /// <summary>Knocked out of a crystal by the Facet's sweeping fist: it does nothing to the fists and rings no crystal.</summary>
        [System.NonSerialized] public bool swept;
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
                var run = RunSession.Current;
                if (run == null || run.stats.shardWard <= 0f)
                {
                    p.TakeDamage(new DamageInfo(damage, DamageType.Contact, this, new Vector2(Mathf.Sign(v.x) * 5f, 5f), "a crystal shard"));
                    Burst();
                    return;
                }
            }
            Enemy hit = null;
            foreach (var e in World.Entities)
                if (e is Enemy en && !en.Dead && !(swept && en is FacetFist) && Overlaps(en)) { hit = en; break; }
            if (hit != null)
            {
                if (hit is FacetFist fist) { if (fist.boss != null) fist.boss.CrackFist(fist); }
                else hit.TakeDamage(new DamageInfo(damage, DamageType.Crush, this, new Vector2(Mathf.Sign(v.x) * 3f, 4f), "a crystal shard"));
                Burst();
                return;
            }
            if (b.hitLeft || b.hitRight || b.hitCeiling || b.landed || life <= 0f) Burst();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null && vel.sqrMagnitude > 0.01f)
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
        }

        void Burst()
        {
            Fx.Debris(Center, burstColor, 4);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { Sfx.Play("hit", pos); Burst(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }
    }
}
