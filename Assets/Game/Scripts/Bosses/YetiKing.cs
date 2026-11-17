using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Frostdeep boss: the yeti king. Five moves in a fixed round: fans of snowballs (no damage: they shove and freeze -
    /// a fur coat stops the freeze), a belly slide across the whole arena (jump it: he is dazed when he hits the
    /// wall), a penguin called in now and then, a leap at the player, and icicles shaken loose above the player.
    /// </summary>
    public class YetiKing : Boss
    {
        public float actionGap = 1.5f;
        public float windupTime = 0.5f;
        public float ballGravity = 28f;
        [Tooltip("Most penguins alive at once.")] public int maxPenguins = 1;
        [Tooltip("Seconds between two calls at the least (the first call comes after half of this). A skipped call is a snowball volley instead.")] public float summonCooldown = 14f;
        public float leapVelocity = 12f;
        public string projectileId = "snowball";
        [Header("Belly slide")]
        public float slideTellTime = 0.65f;
        public float slideSpeed = 10f;
        public float slideDaze = 1.5f;
        [Header("Icicles")]
        public float icicleTellTime = 0.6f;
        public int icicles = 5;
        public string minionId = "penguin";
        public SpriteAnim animIdle;
        public SpriteAnim animThrow;

        float timer, windup, summonTimer, slideTell, slideT, daze, icicleTell;
        bool sliding;
        int icicleX;

        /// <summary>Up close only his leap hurts (the snowballs are his real attack).</summary>
        public override bool Attacking => (!grounded || sliding) && daze <= 0f;
        int step;
        Vector2 aim;

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = 1.6f;
            summonTimer = summonCooldown * 0.5f;
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            summonTimer -= dt;
            var p = Target;
            if (grounded && !sliding) vel.x = Mathf.MoveTowards(vel.x, 0f, 14f * dt);
            if (daze > 0f) { daze -= dt; anim?.Play(animIdle); }                 // hit the wall: open to attack
            else if (sliding)
            {
                vel.x = facing * slideSpeed;
                slideT -= dt;
                if (grounded && slideT % 0.1f < dt) Fx.Dust(pos, 1);
                if (slideT <= 0f) { sliding = false; timer = actionGap; }
            }
            else if (slideTell > 0f)
            {
                slideTell -= dt;
                anim?.Play(animThrow);
                if (slideTell <= 0f) { sliding = true; slideT = 3.5f; Sfx.Play("brute", pos, 1f, 1.1f); }
            }
            else if (icicleTell > 0f)
            {
                icicleTell -= dt;
                anim?.Play(animThrow);
                if (icicleTell <= 0f)
                {
                    Fx.Shake(0.4f);
                    Sfx.Play("thud", pos, 1f, 0.6f);
                    for (int i = 0; i < icicles; i++) DropFromCeiling(icicleX + (i * 2 - (icicles - 1)));
                }
            }
            else if (windup > 0f)
            {
                windup -= dt;
                if (windup <= 0f) Volley();
            }
            else if (grounded)
            {
                anim?.Play(animIdle);
                if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                timer -= dt;
                if (timer <= 0f && p != null)
                {
                    timer = actionGap;
                    // the round: snowballs, belly slide, penguin (or snowballs), leap, icicles
                    int kind = step++ % 5;
                    if (kind == 1) { slideTell = slideTellTime; Tell(); Sfx.Play("toad", pos, 1f, 0.5f); }
                    else if (kind == 4) { icicleTell = icicleTellTime; icicleX = Mathf.FloorToInt(p.pos.x); Tell(); Sfx.Play("brute", pos, 1f, 0.5f); }
                    else if (kind == 2 && summonTimer <= 0f && CountAlive<Penguin>() < maxPenguins) { Summon(p); summonTimer = summonCooldown; }
                    else if (kind == 3)
                    {
                        vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 1.1f, -6f, 6f), leapVelocity);
                        grounded = false;
                        Sfx.Play("brute", pos, 1f, 1.2f);
                    }
                    else { windup = windupTime; aim = p.Center; anim?.Play(animThrow); }
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.landed) { Fx.Shake(0.3f); Fx.Dust(pos, 6); Sfx.Play("thud", pos); }
            if (sliding && (b.hitLeft || b.hitRight))
            {
                sliding = false;
                daze = slideDaze;
                timer = actionGap * 0.5f;
                vel.x = -facing * 2f;
                Fx.Shake(0.45f);
                Fx.Debris(new Vector2(pos.x + facing * 0.8f, pos.y + 0.8f), new Color(0.85f, 0.95f, 1f), 10);
                Sfx.Play("thud", pos, 1f, 0.7f);
                facing = -facing;
            }
        }

        /// <summary>Stomped again: a burst of cold off his back - the player is thrown up and frozen for a moment
        /// (with cold resistance: only slowed).</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            var run = RunSession.Current;
            p.Shove(new Vector2(away * 4f, 10f), 0.15f);
            if (run != null && run.stats.coldResist > 0f) p.Slow(0.3f, 1.5f);
            else p.Freeze(0.7f);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), new Color(0.85f, 0.95f, 1f), 12);
            Fx.Shake(0.3f);
        }

        void Volley()
        {
            Vector2 from = new Vector2(pos.x + facing * 0.6f, pos.y + size.y + 0.1f);
            float t = Mathf.Clamp(Mathf.Abs(aim.x - from.x) / 8f, 0.6f, 1.1f);
            for (int i = -1; i <= 1; i++)
            {
                var e = World.SpawnById(projectileId, from);
                if (e == null) continue;
                e.vel = Lob(from, aim + new Vector2(i * 2.2f, 0f), ballGravity, t + i * 0.08f);
                if (e is Snowball s) { s.gravity = ballGravity; s.owner = this; }
            }
            Sfx.Play("throw", pos);
        }

        void Summon(PlayerController p)
        {
            var g = World.Grid;
            // at the arena edge farthest from the player
            int x = p.pos.x > g.Width * 0.5f ? 2 : g.Width - 3;
            int y = Mathf.FloorToInt(pos.y);
            if (!g.IsEmpty(x, y)) return;
            var e = World.SpawnById(minionId, new Vector2(x + 0.5f, y));
            if (e != null) e.facing = p.pos.x > e.pos.x ? 1 : -1;
            if (e is Enemy en) en.summoned = true;
            Sfx.Play("toad", pos, 1f, 1.5f);
        }
    }
}
