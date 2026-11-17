using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The Yeti King's snow boulder: it rolls along the floor one way and grows as it goes. Small, it can be jumped;
    /// grown, it is taller than a jump and has to be cut down - every blade stroke knocks snow off it and a few
    /// break it (a blast breaks it at once). It hurts, and the bigger it has grown the more: 1 while small, 2 at
    /// middle size, 3 fully grown - and it throws the player back. It shatters on a wall.
    /// Spawn with World.SpawnById and call Roll(direction).
    /// </summary>
    public class RollingBall : Entity
    {
        public float speed = 4.2f;
        [Tooltip("Size at the start and when fully grown (1 = one tile across).")] public float startScale = 0.7f;
        public float maxScale = 2.3f;
        [Tooltip("Scale gained per second while it rolls.")] public float growth = 0.55f;
        [Tooltip("Blade strokes needed to break it.")] public int hits = 3;
        [Tooltip("Scale lost with each stroke.")] public float chipPerHit = 0.4f;
        [Tooltip("Damage while smaller than 'midScale', from there up to 'bigScale', and above it.")] public int damageSmall = 1;
        public int damageMid = 2;
        public int damageBig = 3;
        public float midScale = 1.2f;
        public float bigScale = 1.8f;
        public float gravity = 40f;

        float scale, angle;
        int dir = 1, hitsLeft;

        public override bool IsHazard => true;
        public float Scale => scale;
        /// <summary>What a hit would cost right now: grows with the boulder.</summary>
        public int DamageNow => scale < midScale ? damageSmall : scale < bigScale ? damageMid : damageBig;

        public override void OnSpawn()
        {
            base.OnSpawn();
            scale = startScale;
            hitsLeft = hits;
            size = new Vector2(scale * 0.85f, scale * 0.85f);
        }

        public void Roll(int direction) { dir = direction >= 0 ? 1 : -1; }

        public override void Tick(float dt)
        {
            scale = Mathf.Min(maxScale, scale + growth * dt);
            size = new Vector2(scale * 0.85f, scale * 0.85f);
            vel.x = dir * speed;
            var b = StepPhysics(dt, gravity);
            if (b.hitLeft || b.hitRight) { Burst(); return; }
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
            {
                p.TakeDamage(new DamageInfo(DamageNow, DamageType.Contact, this, new Vector2(dir * 7f, 5f), "a snow boulder"));
                Burst();
            }
        }

        void Burst()
        {
            Fx.Debris(Center, new Color(0.92f, 0.97f, 1f), 14);
            Sfx.Play("thud", pos, 0.7f, 1.4f);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir)
        {
            hitsLeft--;
            scale = Mathf.Max(startScale * 0.8f, scale - chipPerHit);
            Fx.Debris(Center, new Color(0.92f, 0.97f, 1f), 6);
            Sfx.Play("hit", pos, 0.7f, 1.5f);
            if (hitsLeft <= 0) Burst();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Burst(); }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            angle -= dir * speed / Mathf.Max(0.3f, scale) * 60f * Time.deltaTime;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.transform.localPosition = new Vector3(0f, scale * 0.42f, 0f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
