using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Anything you can pick up and throw: rock, pot, crate, arrow, bomb, idol...
    /// Moving fast makes it dangerous: it damages whatever it hits (enemies, the player, the shopkeeper).
    /// Falling fast also counts, so objects knocked off ledges are hazards too.
    /// </summary>
    public class Item : Entity
    {
        [Header("Item")]
        public float gravity = 40f;
        [Range(0, 1)] public float bounce = 0.35f;
        public float groundFriction = 16f;
        public int throwDamage = 1;
        [Tooltip("Minimum speed (tiles/s) at which the item hurts what it hits.")] public float damageSpeed = 6f;
        public bool heavy;
        public bool canPickUp = true;
        [Tooltip(">0: breaks when it hits something faster than this.")] public float breakSpeed = -1f;
        public string impactSfx = "thud";

        [NonSerialized] public PlayerController holder;
        [NonSerialized] public Entity thrower;
        [NonSerialized] public bool dangerous;
        protected float throwerIgnore;
        readonly List<Entity> hitList = new List<Entity>();

        public override bool IsItem => true;
        public virtual bool CanPickUp => canPickUp && holder == null && !Dead;
        public override bool CanBeHitByItems => false;
        public override bool TriggersTraps => holder == null && vel.sqrMagnitude > 1f;

        int baseOrder = -1;

        void SetHeldOrder(bool heldNow)
        {
            if (sr == null) return;
            if (baseOrder < 0) baseOrder = sr.sortingOrder;
            sr.sortingOrder = heldNow ? 22 : baseOrder;
        }

        public virtual void OnPickedUp(PlayerController p)
        {
            SetHeldOrder(true);
            holder = p;
            dangerous = false;
            vel = Vector2.zero;
        }

        public virtual void Throw(Entity by, Vector2 v)
        {
            SetHeldOrder(false);
            holder = null;
            thrower = by;
            throwerIgnore = 0.3f;
            vel = v;
            grounded = false;
            dangerous = true;
        }

        public virtual void Place(Vector2 v)
        {
            SetHeldOrder(false);
            holder = null;
            thrower = null;
            vel = v;
            dangerous = false;
        }

        protected virtual float CurrentGravity => gravity;

        public override void Tick(float dt)
        {
            if (holder != null)
            {
                if (holder.IsDead || holder.held != this) holder = null;
                else return;
            }
            throwerIgnore -= dt;
            float speedBefore = vel.magnitude;
            var b = StepPhysics(dt, CurrentGravity);

            if (b.hitLeft || b.hitRight)
            {
                float iv = Mathf.Abs(b.impactVelX);
                if (iv > 1.5f)
                {
                    vel.x = -b.impactVelX * bounce;
                    OnImpact(iv);
                    if (Dead) return;
                }
            }
            if (b.hitCeiling && b.impactVelY > 2f) { vel.y = -b.impactVelY * bounce * 0.5f; OnImpact(b.impactVelY); if (Dead) return; }
            if (b.landed)
            {
                float iv = -b.impactVelY;
                if (iv > 3.5f)
                {
                    vel.y = iv * bounce;
                    grounded = false;
                    OnImpact(iv);
                    if (Dead) return;
                }
            }
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, groundFriction * dt);

            if (!dangerous && vel.y < -damageSpeed * 1.3f) dangerous = true;   // falling objects hurt
            if (dangerous && grounded && Mathf.Abs(vel.x) < damageSpeed * 0.5f) dangerous = false;
            if (dangerous && speedBefore >= damageSpeed) HitEntities();
            if (pos.y < -4f) Despawn();
        }

        protected virtual void HitEntities()
        {
            var r = Bounds;
            r.xMin -= 0.05f; r.xMax += 0.05f;
            World.Query(r, hitList, e => e != this && e.CanBeHitByItems);
            foreach (var e in hitList)
            {
                if (e == holder) continue;
                if (e == thrower && throwerIgnore > 0f) continue;
                if (e is PlayerController p && p.held == this) continue;
                var type = vel.y < -damageSpeed && Mathf.Abs(vel.x) < damageSpeed ? DamageType.Falling : DamageType.Thrown;
                if (e.TakeDamage(new DamageInfo(DamageFor(e), type, this, new Vector2(Mathf.Sign(vel.x) * 3f, 3f), displayName)))
                {
                    vel.x *= -0.3f;
                    vel.y = Mathf.Max(vel.y * -0.3f, 2.5f);
                    dangerous = false;
                    OnImpact(damageSpeed + 1f);
                    return;
                }
            }
        }

        protected virtual int DamageFor(Entity target) => throwDamage;

        /// <summary>Hit a wall / floor / entity at the given speed.</summary>
        protected virtual void OnImpact(float speed)
        {
            if (speed > 4f) Sfx.Play(impactSfx, pos, Mathf.Clamp01(speed / 12f));
            if (breakSpeed > 0f && speed >= breakSpeed) Break();
        }

        public virtual void Break() { }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (d.knockback != Vector2.zero && holder == null) ApplyImpulse(d.knockback * 0.6f);
            return false;
        }

        public override void OnWhipped(PlayerController p, int damage, int dir)
        {
            if (holder != null) return;
            vel = new Vector2(dir * 6f, 4f);
            grounded = false;
            thrower = p;
            throwerIgnore = 0.3f;
            dangerous = true;
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            if (holder != null) holder.DropHeld();
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            vel = d * 10f + Vector2.up * 5f;
            grounded = false;
            thrower = source;
            throwerIgnore = 0.2f;
            dangerous = false;
        }
    }
}
