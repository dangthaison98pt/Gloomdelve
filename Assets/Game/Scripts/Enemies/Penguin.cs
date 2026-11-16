using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Ice enemy: stands still until it sees you on its level, hops, then slides on its belly in a straight line -
    /// off ledges too - until it hits a wall (dazed for a moment) or runs out of steam. Jump over it or stomp it.
    /// </summary>
    public class Penguin : Enemy
    {
        public float slideSpeed = 8.5f;
        public float sightRange = 8f;
        public float windupTime = 0.35f;
        public float slideTime = 2.6f;
        public float dazeTime = 1.1f;
        public float restTime = 0.9f;
        public SpriteAnim animIdle;
        public SpriteAnim animWindup;
        public SpriteAnim animSlide;

        enum State { Idle, Windup, Slide }
        State state;
        float timer, rest;

        public bool Sliding => state == State.Slide;
        /// <summary>Only the belly slide hurts.</summary>
        public override bool Attacking => state == State.Slide;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            rest -= dt;
            var p = Player;
            if (stunTimer > 0f)
            {
                if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 12f * dt);
                state = State.Idle;
                anim?.Play(animIdle);
            }
            else switch (state)
            {
                case State.Idle:
                    if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 10f * dt);
                    anim?.Play(animIdle);
                    if (p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) > 0.4f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (grounded && rest <= 0f && p != null && !p.IsDead && Mathf.Abs(p.pos.y - pos.y) < 1.2f &&
                        Mathf.Abs(p.pos.x - pos.x) < sightRange && World.LineClear(Center, p.Center))
                    {
                        state = State.Windup; timer = windupTime;
                        vel = new Vector2(0f, 5f); grounded = false;       // a little hop: the tell
                        Sfx.Play("toad", pos, 1f, 1.5f);
                    }
                    break;
                case State.Windup:
                    anim?.Play(animWindup);
                    timer -= dt;
                    if (timer <= 0f && grounded) { state = State.Slide; timer = slideTime; }
                    break;
                case State.Slide:
                    anim?.Play(animSlide);
                    vel.x = facing * slideSpeed;
                    timer -= dt;
                    if (grounded && timer % 0.12f < dt) Fx.Dust(pos, 1);
                    if (timer <= 0f) { state = State.Idle; rest = restTime; }
                    break;
            }
            var b = StepPhysics(dt, gravity);
            if ((b.hitLeft || b.hitRight) && state == State.Slide)
            {
                state = State.Idle;
                stunTimer = dazeTime;
                rest = restTime;
                vel.x = -facing * 2f;
                Fx.Shake(0.15f);
                Fx.Debris(new Vector2(pos.x + facing * 0.4f, pos.y + 0.3f), new Color(0.8f, 0.92f, 1f), 5);
                Sfx.Play("thud", pos);
                facing = -facing;
            }
            CheckSpikes(b);
        }

        protected override void OnHurt(in DamageInfo d)
        {
            stunTimer = Mathf.Max(stunTimer, 0.5f);
        }
    }
}
