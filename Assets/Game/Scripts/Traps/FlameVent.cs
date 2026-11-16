using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Cyclic trap: idle, sputters as a warning, then blasts a 2-tile flame column. Burns everything, lights bombs.</summary>
    public class FlameVent : Entity, ITileBound
    {
        public float idleTime = 2.2f;
        public float warnTime = 0.75f;
        public float burnTime = 1.1f;
        public float flameHeight = 2f;
        public int damage = 1;
        public SpriteRenderer flame;
        public SpriteAnimator flameAnim;
        public SpriteAnim flameLoop;

        Vector2Int cell;
        float t;
        float sparkTimer;
        GlowLight glow;
        readonly List<Entity> tmp = new List<Entity>();

        float Period => idleTime + warnTime + burnTime;
        public bool Burning => t >= idleTime + warnTime;

        public override void Configure(SpawnPoint sp)
        {
            cell = sp.cell;
            pos = new Vector2(cell.x + 0.5f, cell.y + 1f);
            t = (sp.param % 7) * 0.53f;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            World.RegisterTileEntity(cell.x, cell.y, this);
            if (flameAnim != null) flameAnim.Play(flameLoop);
            if (flame != null) flame.enabled = false;
            glow = GetComponent<GlowLight>();
        }

        public override void Tick(float dt)
        {
            if (World.Grid.Get(cell.x, cell.y) != TileType.VentBlock) { Despawn(); return; }
            bool wasBurning = Burning;
            t += dt;
            if (t >= Period) t -= Period;
            bool warning = t >= idleTime && !Burning;
            if (warning)
            {
                sparkTimer -= dt;
                if (sparkTimer <= 0f) { sparkTimer = 0.12f; Fx.Sparks(pos + new Vector2(0f, 0.1f), new Color(1f, 0.6f, 0.2f), 2, 2.5f); }
            }
            if (Burning && !wasBurning) Sfx.Play("flame", pos);
            if (flame != null) flame.enabled = Burning;
            if (glow != null) glow.SetOn(Burning);
            if (!Burning) return;

            var r = new Rect(cell.x + 0.15f, cell.y + 1f, 0.7f, flameHeight);
            World.Query(r, tmp, e => e != this);
            foreach (var e in tmp)
            {
                if (e is Bomb b) { b.Ignite(); continue; }
                if (e.CanBeHitByItems)
                    e.TakeDamage(new DamageInfo(damage, DamageType.Fire, this, new Vector2(Mathf.Sign(e.pos.x - pos.x + 0.01f) * 3f, 7f), displayName));
                else if (e is Breakable br) br.Break();
            }
        }

        public void OnTileDestroyed() => Despawn();
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
