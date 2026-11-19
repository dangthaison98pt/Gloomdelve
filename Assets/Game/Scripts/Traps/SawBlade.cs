using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks trap: a spinning saw. On a rail it runs back and forth along a stretch of floor for ever: jump
    /// over it (the generator only lays rails where there is room to jump). It cuts whatever it touches - the
    /// player, and creatures too, so a sentry can be led into it. It cannot be broken.
    /// A segment the Cogwyrm sheds is a loose saw: no rail. It falls from where it came off, lies where it lands
    /// for a moment, then rolls along the floor at the player and shatters at the first wall or after a few
    /// seconds. It wears the picture of the segment it was.
    /// Spawn point: the cell is the left end of the rail, param is the rail length in cells.
    /// </summary>
    public class SawBlade : Entity
    {
        public float speed = 2.6f;
        public int damage = 1;
        [Tooltip("Degrees per second the blade turns (visual).")] public float spin = 720f;
        [Tooltip("The rail drawn under a railed saw (tiled sprite). Hidden for loose saws.")] public SpriteRenderer rail;
        [Header("Loose saw (a segment the Cogwyrm has shed)")]
        public float looseSpeed = 4.5f;
        public float looseLife = 7f;
        public float gravity = 40f;

        float x0, x1, life, hitCd, angle, wait;
        int dir = 1;
        bool loose, rolling, atPlayer;

        public override bool IsHazard => true;
        public bool Loose => loose;
        /// <summary>A loose saw that has landed, waited and set off along the floor.</summary>
        public bool Rolling => loose && rolling;
        public Sprite Picture => sr != null ? sr.sprite : null;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            int len = Mathf.Max(1, sp.param);
            x0 = sp.cell.x + 0.5f;
            x1 = sp.cell.x + len - 0.5f;
            pos.x = x0;
            dir = 1;
            facing = 1;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (rail != null)
            {
                rail.enabled = !loose;
                rail.size = new Vector2(Mathf.Max(1f, x1 - x0 + 1f), rail.size.y);
            }
        }

        /// <summary>Turns a freshly spawned saw into a loose one rolling this way (+1 right).</summary>
        public void Launch(int direction)
        {
            loose = true; rolling = true; atPlayer = false; wait = 0f;
            dir = direction >= 0 ? 1 : -1;
            life = looseLife;
            if (rail != null) rail.enabled = false;
        }

        /// <summary>
        /// Turns a freshly spawned saw into a piece that has come off something: it wears that piece's picture, falls
        /// from where it is, lies 'rest' seconds where it lands and only then rolls off - towards the player.
        /// </summary>
        public void Drop(Sprite picture, float rest)
        {
            Launch(1);
            rolling = false; atPlayer = true; wait = Mathf.Max(0f, rest);
            if (picture != null && sr != null) sr.sprite = picture;
        }

        public override void Tick(float dt)
        {
            hitCd -= dt;
            if (loose)
            {
                if (!rolling)
                {
                    // falling, then lying where it landed: time to see it and get ready
                    vel.x = 0f;
                    bool was = grounded;
                    StepPhysics(dt, gravity);
                    if (grounded && !was) { Fx.Dust(pos, 4); Sfx.Play("thud", pos, 0.7f, 1.4f); }
                    if (grounded) wait -= dt;
                    if (grounded && wait <= 0f)
                    {
                        rolling = true;
                        var pl = World.Player;
                        if (atPlayer && pl != null) dir = pl.pos.x < pos.x ? -1 : 1;
                    }
                }
                else
                {
                    life -= dt;
                    vel.x = dir * looseSpeed;
                    var b = StepPhysics(dt, gravity);
                    if (b.hitLeft || b.hitRight || life <= 0f) { Shatter(); return; }
                }
            }
            else
            {
                pos.x += dir * speed * dt;
                if (pos.x >= x1) { pos.x = x1; dir = -1; }
                else if (pos.x <= x0) { pos.x = x0; dir = 1; }
            }
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
            {
                int away = p.pos.x >= pos.x ? 1 : -1;
                p.TakeDamage(new DamageInfo(damage, DamageType.Contact, this, new Vector2(away * 5f, 7f), "a saw blade"));
            }
            if (hitCd <= 0f)
                foreach (var e in World.Entities)
                {
                    if (e == null || e.Dead || !(e is Enemy en) || e is Boss || !Overlaps(e)) continue;
                    en.TakeDamage(new DamageInfo(1, DamageType.Crush, this, new Vector2((e.pos.x >= pos.x ? 1 : -1) * 3f, 4f), "a saw blade"));
                    hitCd = 0.5f;
                }
        }

        void Shatter()
        {
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.6f), 8, 5f);
            Sfx.Play("crack", pos, 0.6f, 1.6f);
            Despawn();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null)
            {
                angle -= dir * spin * (loose && !rolling ? 0.35f : 1f) * Time.deltaTime;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            if (rail != null && rail.enabled)       // the rail stays where it lies while the saw runs along it
                rail.transform.position = new Vector3((x0 + x1) * 0.5f, transform.position.y + 0.05f, 0f);
        }

        public override void OnWhipped(PlayerController p, int damage, int dir)
        {
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.6f), 4, 4f);
            Sfx.Play("crack", pos, 0.5f, 1.8f);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { if (loose) Shatter(); }
    }
}
