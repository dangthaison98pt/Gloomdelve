using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Mossroot boss: a giant spider. Four moves in a fixed round: leaps at the player, spits a fan of venom,
    /// jumps high and slams down (a shockwave runs along the floor both ways: jump it), and now and then calls a
    /// bat down from the dark. Strike her while she rests on the ground.
    /// </summary>
    public class Broodmother : Boss
    {
        public float leapSpeedX = 6.5f;
        public float leapVelocity = 13f;
        public float restTime = 1.1f;
        public string minionId = "bat";
        [Tooltip("Most minions alive at once.")] public int maxMinions = 2;
        [Tooltip("Minions called each time.")] public int minionsPerCall = 1;
        [Tooltip("Seconds between two calls at the least (the first call comes after half of this).")] public float broodCooldown = 12f;
        [Header("Venom spit")]
        public float spitSpeed = 7f;
        public float tellTime = 0.5f;
        [Header("Slam")]
        public float slamVelocity = 17f;
        public float shockSpeed = 7.5f;
        public SpriteAnim animIdle;
        public SpriteAnim animJump;

        float timer, broodTimer, tell;
        int step;
        bool slamming;
        enum Move { None, Spit, Slam }
        Move pending;
        static readonly Color Venom = new Color(0.6f, 1f, 0.45f);

        /// <summary>Her attack is the leap: on the ground she can be touched safely.</summary>
        public override bool Attacking => !grounded;

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = 1.6f;
            broodTimer = broodCooldown * 0.5f;
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            broodTimer -= dt;
            var p = Target;
            if (grounded)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                anim?.Play(animIdle);
                if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                if (tell > 0f)
                {
                    tell -= dt;
                    if (tell <= 0f) Unleash(p);
                }
                else
                {
                    timer -= dt;
                    if (timer <= 0f && p != null) NextMove(p);
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.landed)
            {
                timer = restTime;
                Fx.Shake(slamming ? 0.5f : 0.25f);
                Fx.Dust(pos, 6);
                Sfx.Play("thud", pos, 1f, slamming ? 0.7f : 1f);
                if (slamming) { slamming = false; Shockwave(shockSpeed, Venom); timer = restTime * 1.6f; }   // a longer opening after the slam
            }
        }

        /// <summary>The round: leap, leap, spit, leap, slam, call a bat (or leap when the call is not ready).</summary>
        void NextMove(PlayerController p)
        {
            switch (step++ % 6)
            {
                case 2: pending = Move.Spit; tell = tellTime; Tell(); Sfx.Play("bat", pos, 0.8f, 0.5f); break;
                case 4: pending = Move.Slam; tell = tellTime; Tell(); Sfx.Play("brute", pos, 0.8f, 0.6f); break;
                case 5:
                    if (broodTimer <= 0f && CountAlive<Bat>() < maxMinions) { Brood(); broodTimer = broodCooldown; timer = restTime; }
                    else Leap(p);
                    break;
                default: Leap(p); break;
            }
        }

        void Leap(PlayerController p)
        {
            vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 1.3f, -leapSpeedX, leapSpeedX), leapVelocity);
            grounded = false;
            anim?.Play(animJump);
            Sfx.Play("toad", pos, 1f, 0.6f);
        }

        void Unleash(PlayerController p)
        {
            var move = pending;
            pending = Move.None;
            timer = restTime;
            if (p == null) return;
            if (move == Move.Spit)
            {
                FanAt(new Vector2(pos.x + facing * 0.6f, pos.y + size.y * 0.6f), p.Center, 3, 18f, spitSpeed, "venomshot");
                Sfx.Play("throw", pos, 1f, 0.6f);
            }
            else if (move == Move.Slam)
            {
                // straight up and down onto where the player is now
                vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 0.9f, -4.5f, 4.5f), slamVelocity);
                grounded = false;
                slamming = true;
                anim?.Play(animJump);
                Sfx.Play("toad", pos, 1f, 0.45f);
            }
        }

        /// <summary>Stomped again: she rears up and bites straight upward - it hurts and throws the player high.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            anim?.Play(animJump);
            p.TakeDamage(new DamageInfo(contactDamage, DamageType.Contact, this, new Vector2(away * 4f, 12f), displayName + "'s fangs"));
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), Venom, 8);
            Fx.Shake(0.3f);
            Sfx.Play("bat", pos, 1f, 0.5f);
        }

        void Brood()
        {
            int room = maxMinions - CountAlive<Bat>();
            var g = World.Grid;
            int calls = Mathf.Min(minionsPerCall, room);
            // on the side away from the player first, so the newcomer does not land on top of them
            var pl = Target;
            float away = pl != null && pl.pos.x > pos.x ? -2f : 2f;
            for (int i = 0; i < 2 && calls > 0; i++)
            {
                var at = new Vector2(pos.x + (i == 0 ? away : -away), pos.y + 3.2f);
                if (!g.IsEmpty(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y))) continue;
                var m = World.SpawnById(minionId, at);
                if (m == null) continue;
                if (m is Enemy en) en.summoned = true;
                calls--;
            }
            Sfx.Play("bat", pos, 1f, 0.7f);
            Fx.Debris(Center, new Color(0.5f, 0.4f, 0.6f), 8);
        }
    }
}
