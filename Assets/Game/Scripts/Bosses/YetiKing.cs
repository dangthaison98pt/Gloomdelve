using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Frostdeep boss: the yeti king. Six moves in random order (never the same twice running): fans of snowballs (no damage: they shove and freeze -
    /// a fur coat stops the freeze), a belly slide across the whole arena (jump it: he is dazed when he hits the
    /// wall), a snow boulder rolled at the player that grows as it rolls and hurts more the bigger it gets (jump it
    /// early, or cut it down - three strokes or a bomb - once it is too tall), a penguin called in now and then, a leap at the player, and
    /// icicles shaken loose above the player.
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
        public float slideSpeed = 13.5f;
        public float slideDaze = 1.5f;
        [Header("Icicles")]
        public float icicleTellTime = 0.6f;
        public int icicles = 5;
        public string minionId = "penguin";
        [Header("Punch (when the player is frozen)")]
        [Tooltip("He runs at a frozen player this fast ...")] public float punchSpeed = 14f;
        [Tooltip("... for at most this long, and hits when he gets there.")] public float punchTime = 1.3f;
        public float punchReach = 1.5f;
        public int punchDamage = 2;
        [Header("Snow boulder")]
        public string boulderId = "snowboulder";
        public float boulderTell = 0.55f;
        public SpriteAnim animIdle;
        public SpriteAnim animThrow;

        float timer, windup, summonTimer, slideTell, slideT, daze, icicleTell, boulderT, punchT;
        bool punchArmed;

        /// <summary>Running in to punch a frozen player.</summary>
        public bool Punching => punchT > 0f;
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
            if (HoldCounterPose(dt)) return;      // busy answering a stomp
            summonTimer -= dt;
            var p = Target;
            if (grounded && !sliding && punchT <= 0f) vel.x = Mathf.MoveTowards(vel.x, 0f, 14f * dt);
            // A frozen player is an invitation: whatever he was about to do, he drops it, runs in and punches.
            // Once per freeze; a fur coat (no freeze) never invites it.
            if (p == null || !p.Frozen) { if (punchT <= 0f) punchArmed = false; }
            else if (!punchArmed && punchT <= 0f && grounded && !sliding && daze <= 0f && slideTell <= 0f)
            {
                punchArmed = true;
                punchT = punchTime;
                windup = 0f; icicleTell = 0f; boulderT = 0f;
                Tell();
                Sfx.Play("brute", pos, 1f, 1.3f);
            }
            if (daze > 0f) { daze -= dt; anim?.Play(animIdle); }                 // hit the wall: open to attack
            else if (punchT > 0f)
            {
                punchT -= dt;
                anim?.Play(animThrow);
                if (p != null)
                {
                    float dx = p.pos.x - pos.x;
                    if (Mathf.Abs(dx) > 0.2f) facing = dx > 0 ? 1 : -1;
                    if (Mathf.Abs(dx) <= punchReach && Mathf.Abs(p.pos.y - pos.y) < 1.8f)
                    {
                        p.TakeDamage(new DamageInfo(punchDamage, DamageType.Contact, this, new Vector2(facing * 9f, 6f), displayName + "'s fist"));
                        Fx.Shake(0.45f);
                        Sfx.Play("thud", pos, 1f, 0.8f);
                        punchT = 0f; vel.x = 0f; timer = actionGap;
                    }
                    else vel.x = WallAhead(facing) ? 0f : facing * punchSpeed;
                }
                if (punchT <= 0f) { vel.x = 0f; timer = Mathf.Max(timer, actionGap * 0.6f); }
            }
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
            else if (boulderT > 0f)
            {
                boulderT -= dt;
                anim?.Play(animThrow);
                if (boulderT <= 0f)
                {
                    var ball = World.SpawnById(boulderId, new Vector2(pos.x + facing * (size.x * 0.5f + 0.5f), pos.y)) as RollingBall;
                    if (ball != null) ball.Roll(facing);
                    Sfx.Play("throw", pos);
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
                    // his moves, in random order: snowballs, belly slide, snow boulder, penguin (or snowballs), leap, icicles
                    int kind = NextMove(0, 1, 2, 3, 4, 5);
                    if (kind == 1) { slideTell = slideTellTime; Tell(); Sfx.Play("toad", pos, 1f, 0.5f); }
                    else if (kind == 2) { boulderT = boulderTell; Tell(); Sfx.Play("toad", pos, 1f, 0.7f); timer = actionGap * 1.3f; }
                    else if (kind == 5) { icicleTell = icicleTellTime; icicleX = Mathf.FloorToInt(p.pos.x); Tell(); Sfx.Play("brute", pos, 1f, 0.5f); }
                    else if (kind == 3 && summonTimer <= 0f && CountAlive<Penguin>() < maxPenguins) { Summon(p); summonTimer = summonCooldown; }
                    else if (kind == 4)
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

        /// <summary>Calls a helper right now (tests).</summary>
        public void SummonNow(PlayerController p) => Summon(p);

        void Summon(PlayerController p)
        {
            // it bursts out of the snow a few steps from the player, usually behind them: no corner of the arena is safe
            if (!MinionSpot(p, 4, 7, out var spot)) return;
            int x = spot.x, y = spot.y;
            Fx.Debris(new Vector2(x + 0.5f, y + 0.3f), new Color(0.9f, 0.95f, 1f), 10);
            var e = World.SpawnById(minionId, new Vector2(x + 0.5f, y));
            if (e != null) e.facing = p.pos.x > e.pos.x ? 1 : -1;
            if (e is Enemy en) en.summoned = true;
            Sfx.Play("toad", pos, 1f, 1.5f);
        }
    }
}
