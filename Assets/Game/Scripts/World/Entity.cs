using System;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Base of every simulated object (player, enemies, items, traps, pickups).
    /// Simulation runs in World.FixedUpdate at GameConst.Dt; rendering is interpolated in LateUpdate.
    /// pos = bottom-centre of the hitbox.
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        [Header("Entity")]
        public string displayName = "thing";
        public Vector2 size = new Vector2(0.6f, 0.6f);
        public int maxHealth = 1;
        public bool flipWithFacing = true;
        [Tooltip("Sprite faces left in the source art.")] public bool artFacesLeft = false;

        [NonSerialized] public int health;
        [NonSerialized] public Vector2 pos, vel, prevPos;
        [NonSerialized] public bool grounded;
        [NonSerialized] public int facing = 1;
        [NonSerialized] public Body lastBody;

        public bool Dead { get; private set; }
        public World World => World.Instance;

        protected SpriteRenderer sr;
        protected SpriteAnimator anim;
        float flashTimer;
        Color baseColor = Color.white;

        public Rect Bounds => new Rect(pos.x - size.x * 0.5f, pos.y, size.x, size.y);
        public Vector2 Center => new Vector2(pos.x, pos.y + size.y * 0.5f);

        public virtual bool IsEnemy => false;
        public virtual bool IsItem => false;
        public virtual bool CanBeHitByItems => false;
        public virtual bool IsStompable => false;
        public virtual int ContactDamage => 0;
        public virtual bool TriggersTraps => false;
        /// <summary>Dangerous to the player: the generator keeps hazards away from the entrance and out of shops.</summary>
        public virtual bool IsHazard => IsEnemy;
        /// <summary>Hazard that drops from above: never placed over the entrance or right above the exit.</summary>
        public virtual bool IsCeilingHazard => false;

        protected virtual void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            anim = GetComponentInChildren<SpriteAnimator>();
            if (sr != null) baseColor = sr.color;
        }

        public virtual void OnSpawn()
        {
            health = maxHealth;
            prevPos = pos;
            transform.position = pos;
        }

        /// <summary>Called by the spawner before OnSpawn: place the entity in its cell.</summary>
        public virtual void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y);
            if (sp.dir != 0) facing = sp.dir;
        }

        public virtual void Tick(float dt) { }

        public void Despawn()
        {
            if (Dead) return;
            Dead = true;
            OnDespawn();
        }

        protected virtual void OnDespawn() { }

        /// <summary>Gravity + tile collision for simple bodies.</summary>
        protected Body StepPhysics(float dt, float gravity, float maxFall = 18f, bool dropThrough = false)
        {
            vel.y = Mathf.Max(vel.y - gravity * dt, -maxFall);
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y, grounded = grounded };
            BodyPhysics.Move(World.Grid, ref b, dt, dropThrough);
            pos = b.pos; vel = b.vel; grounded = b.grounded;
            lastBody = b;
            return b;
        }

        public virtual bool TakeDamage(in DamageInfo d) => false;

        public virtual void OnWhipped(PlayerController p, int damage, int dir)
        {
            TakeDamage(new DamageInfo(damage, DamageType.Whip, p, new Vector2(dir * 3f, 3f), "a whip"));
        }

        public virtual void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            TakeDamage(new DamageInfo(damage, DamageType.Explosion, source, d * 9f + Vector2.up * 4f, "an explosion"));
        }

        public virtual void ApplyImpulse(Vector2 v) { vel += v; grounded = false; }

        public bool Overlaps(Entity o) => Bounds.Overlaps(o.Bounds);

        public void Flash(float t = 0.12f) { flashTimer = t; }

        protected virtual void LateUpdate()
        {
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            transform.position = Vector2.Lerp(prevPos, pos, alpha);
            if (sr != null)
            {
                if (flipWithFacing) sr.flipX = artFacesLeft ? facing > 0 : facing < 0;
                if (flashTimer > 0f)
                {
                    flashTimer -= Time.deltaTime;
                    sr.color = flashTimer > 0f ? new Color(1f, 0.35f, 0.35f, baseColor.a) : baseColor;
                }
            }
        }
    }
}
