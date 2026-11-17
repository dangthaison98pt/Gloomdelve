using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A flame standing on the floor (the Forge Brute's fire). With a warning time it first only glows and spits
    /// embers - the spot to get off - and then bursts into flame. Burns the player who touches it; creatures are
    /// not hurt. Goes out by itself after 'lifetime', or at once with Extinguish().
    /// Spawn with World.SpawnById and call Arm(warning, burnTime) to change the defaults.
    /// </summary>
    public class FirePatch : Entity
    {
        public float lifetime = 4.5f;
        [Tooltip("Seconds it only glows before it burns (0 = burns at once).")] public float warn = 0f;
        public int damage = 1;

        float life, warnT;

        public override bool IsHazard => true;
        public bool Burning => warnT <= 0f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            life = lifetime; warnT = warn;
        }

        public void Arm(float warning, float burnTime)
        {
            warnT = warning; life = burnTime;
        }

        public void Extinguish()
        {
            if (Dead) return;
            Fx.Smoke(Center, 2);
            Despawn();
        }

        public override void Tick(float dt)
        {
            if (warnT > 0f)
            {
                warnT -= dt;
                if (warnT % 0.12f < dt) Fx.Embers(new Vector2(pos.x + Random.Range(-0.3f, 0.3f), pos.y + 0.1f), 1);
                if (warnT <= 0f) Sfx.Play("fuse", pos, 0.5f, 0.7f);
                return;
            }
            life -= dt;
            if (life <= 0f) { Despawn(); return; }
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
                p.TakeDamage(new DamageInfo(damage, DamageType.Fire, this, new Vector2((p.pos.x >= pos.x ? 1 : -1) * 4f, 7f), "fire"));
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            sr.enabled = warnT <= 0f;
            float k = Mathf.Clamp01(life / 0.5f);
            sr.transform.localScale = new Vector3(1f, 0.6f + 0.4f * k + Mathf.Sin(Time.time * 18f + pos.x) * 0.06f, 1f);
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
