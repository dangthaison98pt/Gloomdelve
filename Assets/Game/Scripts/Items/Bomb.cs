using System;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Lit bomb: explodes after the fuse, can be picked up and re-thrown, sets off other bombs (bounded chain).</summary>
    public class Bomb : Item
    {
        [Header("Bomb")]
        public float fuse = 2.5f;
        public float radius = 2.3f;
        public SpriteAnim animLit;

        [NonSerialized] public int generation;
        [NonSerialized] public Entity owner;
        float timer;
        bool lit;
        /// <summary>The fuse is burning (scrap beetles go for lit bombs).</summary>
        public bool IsLit => lit;
        float tickTimer;
        GlowLight glow;

        public void Light(Entity by, float radiusBonus)
        {
            lit = true;
            timer = fuse;
            owner = by;
            radius += radiusBonus;
            if (anim != null && animLit != null) anim.Play(animLit, true);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Dead || !lit) return;
            if (glow == null) { glow = GetComponent<GlowLight>(); if (glow != null) glow.SetOn(true); }
            timer -= dt;
            if (anim != null) anim.speed = timer < 0.8f ? 3f : 1f;
            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer = timer < 0.8f ? 0.15f : 0.45f;
                Sfx.Play("fuse", pos, 0.5f);
                Fx.Sparks(pos + new Vector2(0f, size.y), new Color(1f, 0.85f, 0.3f), 1, 2f);
            }
            if (timer <= 0f) Detonate();
        }

        void Detonate()
        {
            if (Dead) return;
            if (holder != null) holder.DropHeld();
            World.Explode(Center, radius, owner, generation);
            Despawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int gen, int damage)
        {
            base.OnExplosion(center, radius, source, gen, damage);
            if (gen + 1 > World.config.maxChainGeneration) { Fx.Smoke(pos, 2); Despawn(); return; }   // chain cap: fizzle
            if (!lit) { lit = true; timer = fuse; owner = source; if (anim != null && animLit != null) anim.Play(animLit, true); }
            generation = gen + 1;
            timer = Mathf.Min(timer, UnityEngine.Random.Range(World.config.chainFuseMin, World.config.chainFuseMax));
        }

        /// <summary>Flame vents light bombs.</summary>
        public void Ignite()
        {
            if (!lit) { lit = true; timer = 1.2f; if (anim != null && animLit != null) anim.Play(animLit, true); }
        }
    }
}
