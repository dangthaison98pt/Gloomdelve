using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode: a cluster of resonant crystal growing from the floor. Left alone it does nothing. Struck -
    /// by the blade, a blast, a shot, a flying shard - it rings (it brightens and hums: the
    /// warning) and a moment later it bursts: shards fly out to both sides and up at a slant, and they cut whoever
    /// they meet, the player and the creatures of the geode alike. A shard that reaches another crystal sets that
    /// one ringing too. The stump grows back after a while. Light does nothing to it: no beam makes it ring.
    /// So a crystal is a trap and a weapon: ring it and step back, and whatever stands near it pays.
    /// A Dampener (StatModifiers.shardWard) lets the wearer walk through the shards.
    /// Placed by the level generator (AreaConfig.crystalChance); arena files: marker 'j'.
    /// </summary>
    public class ResonantCrystal : Entity
    {
        [Tooltip("Seconds it rings before it bursts: the warning.")] public float ringTime = 0.6f;
        [Tooltip("Seconds until the stump has grown back.")] public float regrowTime = 5f;
        public string shardId = "crystalshard";
        public float shardSpeed = 9f;
        [Tooltip("Pictures: whole, ringing, burst (the stump).")] public Sprite[] frames;

        enum State { Whole, Ringing, Spent }
        State state;
        float timer;
        int shown = -1;

        public bool Whole => state == State.Whole;
        public bool Ringing => state == State.Ringing;
        public bool Spent => state == State.Spent;
        /// <summary>How often it has burst (tests).</summary>
        public int Bursts { get; private set; }
        public override bool IsHazard => true;

        /// <summary>Sets it ringing (nothing happens if it already rings or is a stump).</summary>
        public void Ring()
        {
            if (state != State.Whole || Dead) return;
            state = State.Ringing; timer = ringTime;
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.35f), "!", new Color(0.6f, 1f, 1f), 0.45f);
            Sfx.Play("gem", pos, 0.9f, 1.5f);
        }

        public override void Tick(float dt)
        {
            timer -= dt;
            switch (state)
            {
                case State.Whole:
                {
                    // a shot or a shard that touches it sets it off (its own shards are long gone by the time it is whole again)
                    bool struck = false;
                    foreach (var e in World.Entities)
                    {
                        if (e.Dead || !Overlaps(e)) continue;
                        if (e is BossShot || (e is CrystalShard cs && cs.owner != this && !cs.swept)) { struck = true; break; }
                    }
                    if (struck) Ring();
                    break;
                }
                case State.Ringing:
                    if (World.Frame % 4 == 0) Fx.Sparks(new Vector2(pos.x + Random.Range(-0.3f, 0.3f), pos.y + size.y * 0.6f), new Color(0.6f, 1f, 1f), 1, 2f);
                    if (timer <= 0f) Burst();
                    break;
                case State.Spent:
                    if (timer <= 0f) { state = State.Whole; Fx.Sparks(new Vector2(pos.x, pos.y + 0.5f), new Color(0.6f, 1f, 1f), 5, 2f); }
                    break;
            }
        }

        /// <summary>
        /// Smashed by something that ploughs through it (the Facet's sweeping fist): it bursts at once, with no
        /// ringing, and whatever smashed it stops the shards on its own side - only those flying on ahead of it
        /// (dir: +1 to the right) get away, along the floor and up at a slant. They fly at 'speed'.
        /// </summary>
        public void BurstAhead(int dir, float speed)
        {
            if (state == State.Spent || Dead) return;
            Burst(dir, speed);
        }

        void Burst(int only = 0, float speed = 0f)
        {
            state = State.Spent; timer = regrowTime;
            Bursts++;
            // (low: the shards that run along the floor must reach the smallest creature standing on it)
            Vector2 from = new Vector2(pos.x, pos.y + 0.12f);
            // out to both sides along the floor, and up at a slant to both sides
            Vector2[] dirs = { Vector2.left, Vector2.right, new Vector2(-0.7f, 0.7f), new Vector2(0.7f, 0.7f) };
            foreach (var d in dirs)
            {
                if (only != 0 && d.x * only < 0f) continue;
                var s = World.SpawnById(shardId, from + d * 0.5f) as CrystalShard;
                if (s == null) continue;
                s.vel = d.normalized * (only != 0 ? speed : shardSpeed);
                s.owner = this;
                s.swept = only != 0;
            }
            Fx.Debris(from, new Color(0.6f, 0.95f, 1f), 12);
            Fx.Shake(0.15f);
            Sfx.Play("shatter", pos, 0.9f, 1.4f);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            int f = (int)state;
            if (sr == null || frames == null || f == shown || f >= frames.Length) return;
            shown = f;
            sr.sprite = frames[f];
        }

        public override bool TakeDamage(in DamageInfo d) { Ring(); return state == State.Ringing; }
        public override void OnWhipped(PlayerController p, int damage, int dir) { if (state == State.Whole) Sfx.Play("hit", pos, 0.7f, 1.6f); Ring(); }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { Ring(); }
    }
}
