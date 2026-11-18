using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Final boss: the Hollow Warden, guardian of the Heartstone. Drifts after the player and works through five
    /// moves: a fan of gloom orbs, a rain of orbs from above the player, a lunge, a blink to the other side of the
    /// player, and (below half health, when it also speeds up) a ring of orbs. The Heartstone cannot be lifted
    /// while it lives.
    /// </summary>
    public class Warden : Boss
    {
        public float driftSpeed = 1.6f;
        public float attackGap = 2.2f;
        /// <summary>It is not flesh: spikes, fire and falling rock pass through it.</summary>
        protected override bool TrapProof => true;
        public float shotSpeed = 6f;
        public float dashSpeed = 10f;
        public string shotId = "gloomorb";
        [Header("Orb rain")]
        public int rainOrbs = 5;
        public float rainSpacing = 1.7f;
        public float rainHeight = 6f;
        [Header("Blink")]
        public float blinkDistance = 5f;
        public SpriteAnim animFloat;

        enum State { Drift, Windup, Dash }
        State state;
        float timer, bob;
        int attacks;
        Vector2 dashDir;

        bool Enraged => HealthFraction <= 0.5f;

        /// <summary>Up close only its lunge hurts (the orbs are its real attack).</summary>
        public override bool Attacking => state == State.Dash;

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = 2f;
            anim?.Play(animFloat);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Target;
            bob += dt;
            timer -= dt;
            float speed = driftSpeed * (Enraged ? 1.5f : 1f);
            switch (state)
            {
                case State.Drift:
                    if (p != null)
                    {
                        Vector2 to = (p.Center + Vector2.up * 0.8f) - Center;
                        vel = Vector2.MoveTowards(vel, to.normalized * speed + new Vector2(0f, Mathf.Sin(bob * 2.4f) * 0.8f), 8f * dt);
                        facing = to.x >= 0 ? 1 : -1;
                        if (timer <= 0f)
                        {
                            // its moves, in random order: fan, rain, lunge, blink, ring (a fan until it is enraged)
                            float gap = RestFlat(attackGap) * (Enraged ? 0.7f : 1f);
                            switch (NextMove(0, 1, 2, 3, 4))
                            {
                                case 1: Rain(p); timer = gap; break;
                                case 2: state = State.Windup; timer = 0.55f; Tell(); Sfx.Play("wraith", pos, 0.6f, 1.4f); break;
                                case 3: Blink(p); timer = 0.6f; break;                 // the next move follows quickly
                                case 4: if (Enraged) Ring(8); else Fan(p, 3); timer = gap; break;
                                default: Fan(p, Enraged ? 5 : 3); timer = gap; break;
                            }
                        }
                    }
                    else vel = Vector2.MoveTowards(vel, Vector2.zero, 6f * dt);
                    break;
                case State.Windup:
                    vel = Vector2.MoveTowards(vel, Vector2.zero, 20f * dt);
                    if (timer <= 0f)
                    {
                        dashDir = p != null ? (p.Center - Center).normalized : Vector2.right * facing;
                        state = State.Dash; timer = 0.6f;
                    }
                    break;
                case State.Dash:
                    vel = dashDir * dashSpeed;
                    if (timer <= 0f) { state = State.Drift; timer = RestFlat(attackGap) * 0.6f; }
                    break;
            }
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
            if (state == State.Dash && (b.hitLeft || b.hitRight || b.hitCeiling || b.landed)) { state = State.Drift; timer = RestFlat(attackGap) * 0.6f; Fx.Shake(0.3f); }
        }

        /// <summary>A row of orbs appears high above the player and falls: stand in a gap or step out from under it.</summary>
        void Rain(PlayerController p)
        {
            Tell();
            var g = World.Grid;
            for (int i = 0; i < rainOrbs; i++)
            {
                float x = p.pos.x + (i - (rainOrbs - 1) * 0.5f) * rainSpacing;
                // start as high as there is open air above that spot (up to rainHeight)
                float y = p.Center.y;
                int cx = Mathf.FloorToInt(x);
                while (y < p.Center.y + rainHeight && g.IsEmpty(cx, Mathf.FloorToInt(y + 1f))) y += 1f;
                if (y < p.Center.y + 2.5f) continue;            // no room above: that orb is skipped
                Shoot(Vector2.down * 0.85f, new Vector2(x, y - 0.4f));
            }
            Sfx.Play("wraith", pos, 0.5f, 1.8f);
        }

        /// <summary>Vanishes and reappears on the other side of the player (when there is room there).</summary>
        void Blink(PlayerController p)
        {
            int side = pos.x >= p.pos.x ? -1 : 1;
            var to = new Vector2(p.pos.x + side * blinkDistance, p.pos.y + 1.2f);
            var g = World.Grid;
            int x0 = Mathf.FloorToInt(to.x - size.x * 0.5f), x1 = Mathf.FloorToInt(to.x + size.x * 0.5f);
            int y0 = Mathf.FloorToInt(to.y), y1 = Mathf.FloorToInt(to.y + size.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (!g.IsEmpty(x, y)) return;                // blocked: it stays where it is
            Fx.Sparks(Center, new Color(0.7f, 0.5f, 1f), 14, 4f);
            pos = to; prevPos = to; vel = Vector2.zero;
            facing = -side;
            Fx.Sparks(Center, new Color(0.7f, 0.5f, 1f), 14, 4f);
            Sfx.Play("wraith", pos, 0.7f, 1.2f);
        }

        void Shoot(Vector2 dir) => Shoot(dir, Center);

        void Shoot(Vector2 dir, Vector2 from)
        {
            var e = World.SpawnById(shotId, from);
            if (e == null) return;
            e.vel = dir * shotSpeed * (Enraged ? 1.2f : 1f);       // dir is a unit vector; a shorter one makes a slower shot
            if (e is BossShot s) s.owner = this;
        }

        void Fan(PlayerController p, int count)
        {
            Vector2 d = (p.Center - Center).normalized;
            float baseAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + (i - (count - 1) * 0.5f) * 16f) * Mathf.Deg2Rad;
                Shoot(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            Sfx.Play("throw", pos, 1f, 0.7f);
        }

        void Ring(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Shoot(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            Sfx.Play("throw", pos, 1f, 0.5f);
        }
    }
}
