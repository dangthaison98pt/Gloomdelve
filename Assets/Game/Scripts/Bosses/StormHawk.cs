using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sunfall boss: a giant hawk. Circles overhead out of reach, drops rocks, then dives. A dive that misses
    /// leaves it on the ground for a moment: that is the time to strike. The rocks it drops can be thrown back.
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
        public SpriteAnim animFly;
        public SpriteAnim animDive;

        enum State { Hover, Windup, Dive, Grounded, Climb }
        State state;
        float timer, floorY, rockTimer;
        int dives, rocksLeft;
        Vector2 diveDir;

        /// <summary>Only the dive hurts.</summary>
        public override bool Attacking => state == State.Dive;
        public override int ContactDamage => state == State.Dive ? contactDamage : 0;

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
                    else if (timer <= 0f && p != null)
                    {
                        if (dives >= 2) { dives = 0; rocksLeft = 3; rockTimer = 0.2f; timer = hoverTime; Sfx.Play("bat", pos, 1f, 0.5f); }
                        else { state = State.Windup; timer = windupTime; Sfx.Play("bat", pos, 1f, 0.45f); }
                    }
                    break;
                }
                case State.Windup:
                    vel = new Vector2(0f, 2f);
                    if (timer <= 0f)
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
            if (state == State.Dive && (b.landed || b.grounded || b.hitLeft || b.hitRight || timer <= 0f))
            {
                state = State.Grounded; timer = groundedTime;
                Fx.Shake(0.4f);
                Fx.Dust(pos, 8);
                Sfx.Play("thud", pos);
            }
        }

        void DropRock()
        {
            var e = World.SpawnById(rockId, new Vector2(pos.x, pos.y - 0.3f));
            if (e is Item it) it.Throw(this, new Vector2(Random.Range(-1.5f, 1.5f), -3f));
        }
    }
}
