using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Emberdeep boss: a huge mole brute. A fixed round: it charges the full width of the arena (jump it and let
    /// it hit the wall: it is dazed, hit it now, while rocks shake loose), it pounds the floor and fire comes up in
    /// two spots - under the player and one step on - which glow first and burn only briefly, it charges again,
    /// and it leaps at the player and slams the ground three times running: three shockwaves along the floor, one
    /// after another, each to be jumped (the ground slam is his).
    /// Nothing made of dirt stops its charge: trapped in a pit blasted into the floor, it tears through the
    /// earth in front of it. Only the bedrock walls of the arena daze it.
    /// </summary>
    public class ForgeBrute : Boss
    {
        [Tooltip("Speed of the charge across the arena (the player runs at about 6.4).")] public float chargeSpeed = 12.5f;
        public float windupTime = 0.7f;
        public float dazeTime = 2.2f;
        public float idleTime = 0.9f;
        public int rocksPerSlam = 3;
        [Header("Stomp: fire comes up")]
        public float stompTell = 0.65f;
        public string fireId = "firepatch";
        [Tooltip("How many spots catch fire (the first is under the player).")] public int firePillars = 2;
        public float pillarSpacing = 2.6f;
        [Tooltip("Seconds a spot glows before it burns.")] public float pillarWarn = 0.8f;
        [Tooltip("Seconds it burns: short, the floor is never covered in fire.")] public float pillarBurnTime = 1.1f;
        [Header("Leap")]
        public float leapVelocity = 14f;
        public float leapSpeedX = 7f;
        public float shockSpeed = 8f;
        [Tooltip("Shockwaves per slam: it lands, then stomps again and again. Each one has to be jumped.")] public int slamWaves = 3;
        [Tooltip("Seconds between two of them.")] public float slamInterval = 0.55f;
        public SpriteAnim animWalk;
        public SpriteAnim animCharge;
        public SpriteAnim animDazed;

        enum State { Idle, Windup, Charge, Dazed, Stomp, Leap, Slam }
        State state;
        float timer;
        int step, slamsLeft;
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

        /// <summary>Breaks the breakable tiles right in front of the charging brute (its whole height). True if any broke.</summary>
        bool TearThrough()
        {
            var w = World;
            int x = Mathf.FloorToInt(pos.x + facing * (size.x * 0.5f + 0.3f));
            int y0 = Mathf.FloorToInt(pos.y + 0.1f), y1 = Mathf.FloorToInt(pos.y + size.y - 0.1f);
            bool any = false;
            for (int y = y0; y <= y1; y++) if (w.DestroyTile(x, y)) any = true;
            return any;
        }

        void SlamWave()
        {
            Fx.Shake(0.5f);
            Sfx.Play("thud", pos, 1f, 0.7f);
            Shockwave(shockSpeed, Ember);
        }

        void Flame(float x)
        {
            var g = World.Grid;
            if (x < 1.5f || x > g.Width - 1.5f) return;
            int fy = Mathf.FloorToInt(pos.y + 0.5f);
            while (fy > 0 && !g.IsSolid(Mathf.FloorToInt(x), fy - 1)) fy--;
            if (!g.IsEmpty(Mathf.FloorToInt(x), fy)) return;
            var f = World.SpawnById(fireId, new Vector2(x, fy)) as FirePatch;
            if (f != null) f.Arm(pillarWarn, pillarBurnTime);
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
                        // fire comes up under the player and one step further on (away from the brute): a glow first
                        Fx.Shake(0.5f);
                        Sfx.Play("thud", pos, 1f, 0.6f);
                        float px = p != null ? p.pos.x : pos.x;
                        int away = px >= pos.x ? 1 : -1;
                        for (int i = 0; i < firePillars; i++) Flame(Mathf.Floor(px + away * i * pillarSpacing) + 0.5f);
                        state = State.Idle; timer = idleTime;
                    }
                    break;
                case State.Leap:
                    anim?.Play(animCharge);
                    break;
                case State.Slam:
                    // where it landed it stomps again: one more shockwave each time
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer <= 0f)
                    {
                        SlamWave();
                        slamsLeft--;
                        timer = slamInterval;
                        if (slamsLeft <= 0) { state = State.Idle; timer = idleTime * 1.4f; }
                    }
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
                vel.x = 0f;
                SlamWave();
                slamsLeft = slamWaves - 1;
                if (slamsLeft > 0) { state = State.Slam; timer = slamInterval; }
                else { state = State.Idle; timer = idleTime * 1.4f; }
            }
            if ((b.hitLeft || b.hitRight) && state == State.Charge && TearThrough())
            {
                // dirt in its way (the wall of a pit it was lured into): it ploughs on
                Fx.Shake(0.3f);
                Sfx.Play("thud", pos, 0.8f, 1.2f);
            }
            else if ((b.hitLeft || b.hitRight) && state == State.Charge)
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
