using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sunfall boss: a giant hawk circling overhead out of reach. Four moves in a fixed round: a dive (a miss
    /// leaves it on the ground for a moment: strike then), a fan of feathers shot at the player, a string of
    /// dropped rocks (they can be thrown back), and a low sweep right across the arena at ground level: jump it.
    /// </summary>
    public class StormHawk : Boss
    {
        public float hoverHeight = 6.5f;
        public float hoverSpeed = 5f;
        public float diveSpeed = 14f;
        public float hoverTime = 2.4f;
        public float windupTime = 0.55f;
        public float groundedTime = 1.7f;
        public float climbSpeed = 5f;
        public string rockId = "rock";
        [Header("Feather fan")]
        public int feathers = 5;
        public float featherSpeed = 8f;
        public float featherTell = 0.45f;
        [Header("Sweep")]
        public float sweepSpeed = 11f;
        public float sweepHeight = 0.35f;
        public SpriteAnim animFly;
        public SpriteAnim animDive;

        enum State { Hover, Windup, Dive, Grounded, Climb, SweepSetup, Sweep }
        State state;
        float timer, floorY, rockTimer;
        int dives, rocksLeft, step, sweepDir;
        float fanTimer;
        bool sweepNext;
        Vector2 diveDir;

        /// <summary>Only the dive hurts.</summary>
        public override bool Attacking => state == State.Dive || state == State.Sweep;
        public override int ContactDamage => Attacking ? contactDamage : 0;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            floorY = pos.y;
            pos.y += hoverHeight;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Hover; timer = hoverTime;
            anim?.Play(animFly);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (HoldCounterPose(dt, false)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            float top = floorY + hoverHeight;
            switch (state)
            {
                case State.Hover:
                {
                    anim?.Play(animFly);
                    float tx = p != null ? p.pos.x : pos.x;
                    var want = new Vector2(Mathf.Clamp((tx - pos.x) * 2f, -hoverSpeed, hoverSpeed), (top - pos.y) * 3f + Mathf.Sin(Time.time * 3f) * 0.6f);
                    vel = Vector2.MoveTowards(vel, want, 18f * dt);
                    if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0 ? 1 : -1;
                    if (rocksLeft > 0)
                    {
                        rockTimer -= dt;
                        if (rockTimer <= 0f) { DropRock(); rocksLeft--; rockTimer = 0.45f; }
                    }
                    else if (fanTimer > 0f)
                    {
                        fanTimer -= dt;
                        if (fanTimer <= 0f && p != null)
                        {
                            FanAt(Center, p.Center, feathers, 13f, featherSpeed, "feathershot");
                            Sfx.Play("throw", pos, 1f, 1.3f);
                        }
                    }
                    else if (timer <= 0f && p != null)
                    {
                        // the round: dive, feathers, dive, rocks, sweep
                        switch (step++ % 5)
                        {
                            case 1: fanTimer = featherTell; timer = hoverTime * 0.8f; Tell(); Sfx.Play("bat", pos, 1f, 0.7f); break;
                            case 3: rocksLeft = 3; rockTimer = 0.2f; timer = hoverTime; Sfx.Play("bat", pos, 1f, 0.5f); break;
                            case 4: sweepNext = true; state = State.Windup; timer = windupTime; Tell(); Sfx.Play("bat", pos, 1f, 0.35f); break;
                            default: state = State.Windup; timer = windupTime; Sfx.Play("bat", pos, 1f, 0.45f); break;
                        }
                    }
                    break;
                }
                case State.Windup:
                    vel = new Vector2(0f, 2f);
                    if (timer <= 0f && sweepNext)
                    {
                        // first off to the side away from the player and down to the ground, then back across
                        sweepNext = false;
                        float px = p != null ? p.pos.x : pos.x;
                        sweepDir = pos.x >= px ? -1 : 1;          // the sweep itself runs toward the player
                        state = State.SweepSetup; timer = 1.3f;
                    }
                    else if (timer <= 0f)
                    {
                        Vector2 to = p != null ? p.Center - Center : Vector2.down;
                        if (to.y > -1f) to = Vector2.down;
                        diveDir = to.normalized;
                        state = State.Dive; timer = 2f; dives++;
                        anim?.Play(animDive);
                    }
                    break;
                case State.Dive:
                    vel = diveDir * diveSpeed;
                    break;
                case State.SweepSetup:
                {
                    anim?.Play(animFly);
                    float low = floorY + sweepHeight;
                    vel = new Vector2(-sweepDir * hoverSpeed * 1.4f, Mathf.Clamp((low - pos.y) * 5f, -11f, 4f));
                    facing = sweepDir;
                    float px = p != null ? p.pos.x : pos.x;
                    bool far = Mathf.Abs(pos.x - px) > 8f || pos.x < 2.5f || pos.x > World.Grid.Width - 2.5f;
                    if ((far && pos.y < low + 0.6f) || timer <= 0f) { state = State.Sweep; timer = 2.6f; anim?.Play(animDive); Sfx.Play("bat", pos, 1f, 0.6f); }
                    break;
                }
                case State.Sweep:
                    vel = new Vector2(sweepDir * sweepSpeed, Mathf.Clamp((floorY + sweepHeight - pos.y) * 6f, -11f, 4f));
                    facing = sweepDir;
                    if (timer % 0.08f < dt) Fx.Dust(new Vector2(pos.x, floorY), 1);
                    break;
                case State.Grounded:
                    vel = Vector2.zero;
                    anim?.Play(animFly);
                    if (timer <= 0f) state = State.Climb;
                    break;
                case State.Climb:
                    anim?.Play(animFly);
                    vel = new Vector2(0f, climbSpeed);
                    if (pos.y >= top - 0.3f) { state = State.Hover; timer = hoverTime; }
                    break;
            }
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos;
            if (state == State.SweepSetup && (b.hitLeft || b.hitRight)) { state = State.Sweep; timer = 2.6f; anim?.Play(animDive); }
            else if (state == State.Sweep && (b.hitLeft || b.hitRight || timer <= 0f ||
                                             (sweepDir > 0 ? pos.x > World.Grid.Width - 2.5f : pos.x < 2.5f)))
            {
                state = State.Climb;
                anim?.Play(animFly);
            }
            if (state == State.Dive && (b.landed || b.grounded || b.hitLeft || b.hitRight || timer <= 0f))
            {
                state = State.Grounded; timer = groundedTime;
                Fx.Shake(0.4f);
                Fx.Dust(pos, 8);
                Sfx.Play("thud", pos);
            }
        }

        /// <summary>Stomped again: a beat of its wings blows the player high into the air, feathers fly up after them,
        /// and the hawk takes off - the opening on the ground is over.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 5f, 15f), 0.1f);
            // the feathers leave from the wing on the far side of the player and fan upward, so they are something
            // to dodge on the way back down, not an instant hit
            Vector2 from = new Vector2(pos.x - away * (size.x * 0.5f + 0.5f), pos.y + size.y + 0.2f);
            FanAt(from, from + new Vector2(away * 2.5f, 6f), 3, 22f, 6.5f, "feathershot");
            if (state == State.Grounded) state = State.Climb;
            Fx.Dust(pos, 10);
            Sfx.Play("bat", pos, 1f, 0.45f);
        }

        void DropRock()
        {
            var e = World.SpawnById(rockId, new Vector2(pos.x, pos.y - 0.3f));
            if (e is Item it) it.Throw(this, new Vector2(Random.Range(-1.5f, 1.5f), -3f));
        }
    }
}
