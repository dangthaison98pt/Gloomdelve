using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Emberdeep boss: a huge mole brute. Three moves in a fixed round: it charges the full width of the arena
    /// (jump it and let it hit the wall: it is dazed, hit it now, while rocks shake loose), it stomps the ground so
    /// that rocks fall around the player, and it leaps at the player and lands with a shockwave along the floor.
    /// </summary>
    public class ForgeBrute : Boss
    {
        public float chargeSpeed = 9.5f;
        public float windupTime = 0.7f;
        public float dazeTime = 2.2f;
        public float idleTime = 0.9f;
        public int rocksPerSlam = 3;
        [Header("Stomp")]
        public float stompTell = 0.65f;
        public int stompRocks = 4;
        [Header("Leap")]
        public float leapVelocity = 14f;
        public float leapSpeedX = 7f;
        public float shockSpeed = 8f;
        public SpriteAnim animWalk;
        public SpriteAnim animCharge;
        public SpriteAnim animDazed;

        enum State { Idle, Windup, Charge, Dazed, Stomp, Leap }
        State state;
        float timer;
        int step;
        bool leapNext;
        static readonly Color Ember = new Color(1f, 0.6f, 0.25f);

        /// <summary>The charge and the leap hurt; standing, stomping or dazed it can be touched.</summary>
        public override bool Attacking => state == State.Charge || (state == State.Leap && !grounded);
        [Tooltip("Damage of the charge (the leap uses contactDamage).")] public int chargeDamage = 3;
        public override int ContactDamage => !Attacking ? 0 : (state == State.Charge ? chargeDamage : contactDamage);

        /// <summary>Stomped again: it bucks and hurls the player across the arena - no damage, but a long flight and a
        /// moment on the floor, usually right in the path of its next charge.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 12f, 8.5f), 0.45f);
            Fx.Shake(0.45f);
            Fx.Dust(pos, 8);
            Sfx.Play("brute", pos, 1f, 0.9f);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Idle; timer = 1.5f;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (HoldCounterPose(dt)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            switch (state)
            {
                case State.Idle:
                    vel.x = 0f;
                    anim?.Play(animWalk);
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f && p != null && grounded)
                    {
                        // the round: charge, stomp, charge, leap
                        int k = step++ % 4;
                        if (k == 1) { state = State.Stomp; timer = stompTell; Tell(); Sfx.Play("brute", pos, 1f, 0.5f); }
                        else { leapNext = k == 3; state = State.Windup; timer = leapNext ? windupTime * 0.8f : windupTime; if (leapNext) Tell(); Sfx.Play("brute", pos, 1f, 0.7f); }
                    }
                    break;
                case State.Windup:
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer % 0.12f < dt) Fx.Dust(pos, 2);
                    if (timer <= 0f)
                    {
                        if (leapNext && p != null)
                        {
                            leapNext = false;
                            state = State.Leap;
                            vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 1.2f, -leapSpeedX, leapSpeedX), leapVelocity);
                            grounded = false;
                        }
                        else { leapNext = false; state = State.Charge; timer = 4f; }
                    }
                    break;
                case State.Charge:
                    anim?.Play(animCharge);
                    vel.x = facing * chargeSpeed;
                    if (timer % 0.1f < dt) Fx.Dust(pos, 1);
                    if (timer <= 0f) { state = State.Idle; timer = idleTime; }
                    break;
                case State.Stomp:
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer % 0.1f < dt) Fx.Dust(pos, 2);
                    if (timer <= 0f)
                    {
                        // rocks come down in a row around the player, with gaps to stand in
                        Fx.Shake(0.5f);
                        Sfx.Play("thud", pos, 1f, 0.6f);
                        int cx = Mathf.FloorToInt(p != null ? p.pos.x : pos.x);
                        for (int i = 0; i < stompRocks; i++) DropFromCeiling(cx + (i * 2 - (stompRocks - 1)));
                        state = State.Idle; timer = idleTime;
                    }
                    break;
                case State.Leap:
                    anim?.Play(animCharge);
                    break;
                case State.Dazed:
                    if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
                    anim?.Play(animDazed);
                    if (timer <= 0f) { state = State.Idle; timer = idleTime; }
                    break;
            }
            var b = StepPhysics(dt, gravity);
            if (state == State.Leap && (b.landed || (grounded && vel.y <= 0f)))
            {
                state = State.Idle; timer = idleTime * 1.4f;
                vel.x = 0f;
                Fx.Shake(0.5f);
                Sfx.Play("thud", pos, 1f, 0.7f);
                Shockwave(shockSpeed, Ember);
            }
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
