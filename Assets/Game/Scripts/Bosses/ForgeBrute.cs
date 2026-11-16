using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Emberdeep boss: a huge mole brute. Paws the ground, then charges the full width of the arena. Jump it,
    /// let it hit the wall: it is dazed for a moment (hit it now) while rocks shake loose from the ceiling.
    /// </summary>
    public class ForgeBrute : Boss
    {
        public float chargeSpeed = 9.5f;
        public float windupTime = 0.7f;
        public float dazeTime = 2.2f;
        public float idleTime = 0.9f;
        public int rocksPerSlam = 3;
        public SpriteAnim animWalk;
        public SpriteAnim animCharge;
        public SpriteAnim animDazed;

        enum State { Idle, Windup, Charge, Dazed }
        State state;
        float timer;

        /// <summary>Only the charge hurts.</summary>
        public override bool Attacking => state == State.Charge;
        public override int ContactDamage => state == State.Charge ? 2 : 0;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Idle; timer = 1.5f;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Target;
            timer -= dt;
            switch (state)
            {
                case State.Idle:
                    vel.x = 0f;
                    anim?.Play(animWalk);
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f && p != null && grounded) { state = State.Windup; timer = windupTime; Sfx.Play("brute", pos, 1f, 0.7f); }
                    break;
                case State.Windup:
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer % 0.12f < dt) Fx.Dust(pos, 2);
                    if (timer <= 0f) { state = State.Charge; timer = 4f; }
                    break;
                case State.Charge:
                    anim?.Play(animCharge);
                    vel.x = facing * chargeSpeed;
                    if (timer % 0.1f < dt) Fx.Dust(pos, 1);
                    if (timer <= 0f) { state = State.Idle; timer = idleTime; }
                    break;
                case State.Dazed:
                    if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
                    anim?.Play(animDazed);
                    if (timer <= 0f) { state = State.Idle; timer = idleTime; }
                    break;
            }
            var b = StepPhysics(dt, gravity);
            if ((b.hitLeft || b.hitRight) && state == State.Charge)
            {
                state = State.Dazed; timer = dazeTime;
                vel.x = -facing * 2.5f;
                Fx.Shake(0.5f);
                Fx.Debris(new Vector2(pos.x + facing * 0.9f, pos.y + 0.8f), new Color(0.6f, 0.45f, 0.4f), 10);
                Sfx.Play("thud", pos, 1f, 0.7f);
                facing = -facing;
                var tp = Target;
                for (int i = 0; i < rocksPerSlam; i++)
                    DropFromCeiling(Mathf.FloorToInt((tp != null ? tp.pos.x : pos.x) + Random.Range(-4f, 4f)));
            }
        }
    }
}
