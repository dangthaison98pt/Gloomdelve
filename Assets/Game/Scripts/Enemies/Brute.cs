using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Tough patroller that charges when it spots you on its level; slams into walls and gets dazed.</summary>
    public class Brute : Enemy
    {
        public float patrolSpeed = 1.2f;
        public float chargeSpeed = 7.5f;
        public float sightRange = 7f;
        public float windupTime = 0.4f;
        public float dazeTime = 1.6f;
        public SpriteAnim animWalk;
        public SpriteAnim animCharge;
        public SpriteAnim animDazed;

        enum State { Patrol, Windup, Charge }
        State state;
        float timer;

        /// <summary>Only the charge hurts.</summary>
        public override bool Attacking => state == State.Charge;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (stunTimer > 0f)
            {
                if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 25f * dt);
                state = State.Patrol;
                anim?.Play(animDazed);
            }
            else if (grounded)
            {
                var p = Player;
                switch (state)
                {
                    case State.Patrol:
                        vel.x = PatrolStep() * patrolSpeed;
                        anim?.Play(animWalk);
                        if (p != null && !p.IsDead && Mathf.Abs(p.pos.y - pos.y) < 1.2f && Mathf.Abs(p.pos.x - pos.x) < sightRange &&
                            World.LineClear(Center, p.Center))
                        {
                            facing = p.pos.x >= pos.x ? 1 : -1;
                            state = State.Windup; timer = windupTime; vel.x = 0f;
                            Sfx.Play("brute", pos);
                        }
                        break;
                    case State.Windup:
                        vel.x = 0f;
                        timer -= dt;
                        anim?.Play(animCharge);
                        if (timer <= 0f) { state = State.Charge; timer = 2.2f; }
                        break;
                    case State.Charge:
                        vel.x = facing * chargeSpeed;
                        timer -= dt;
                        if (timer % 0.15f < dt) Fx.Dust(pos, 1);
                        if (EdgeAhead(facing) || SpikesAhead(facing) || timer <= 0f) { state = State.Patrol; vel.x = 0f; }
                        break;
                }
            }
            var b = StepPhysics(dt, gravity);
            if ((b.hitLeft || b.hitRight) && state == State.Charge)
            {
                state = State.Patrol;
                stunTimer = dazeTime;
                Fx.Shake(0.25f);
                Fx.Debris(new Vector2(pos.x + facing * 0.5f, pos.y + 0.5f), new Color(0.6f, 0.5f, 0.45f), 5);
                Sfx.Play("thud", pos);
                facing = -facing;
            }
            CheckSpikes(b);
        }

        protected override void OnHurt(in DamageInfo d)
        {
            stunTimer = Mathf.Max(stunTimer, d.type == DamageType.Stomp ? 1.0f : 0.4f);
        }
    }
}
