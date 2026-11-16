using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Final boss: the Hollow Warden, guardian of the Heartstone. Drifts after the player firing fans of gloom
    /// orbs and now and then lunges. Below half health it speeds up and adds rings of orbs. The Heartstone
    /// cannot be lifted while it lives.
    /// </summary>
    public class Warden : Boss
    {
        public float driftSpeed = 1.6f;
        public float attackGap = 2.2f;
        public float shotSpeed = 6f;
        public float dashSpeed = 10f;
        public string shotId = "gloomorb";
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
                            attacks++;
                            if (attacks % 3 == 0) { state = State.Windup; timer = 0.55f; Sfx.Play("wraith", pos, 0.6f, 1.4f); }
                            else
                            {
                                if (Enraged && attacks % 2 == 0) Ring(8); else Fan(p, Enraged ? 5 : 3);
                                timer = attackGap * (Enraged ? 0.7f : 1f);
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
                    if (timer <= 0f) { state = State.Drift; timer = attackGap * 0.6f; }
                    break;
            }
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
            if (state == State.Dash && (b.hitLeft || b.hitRight || b.hitCeiling || b.landed)) { state = State.Drift; timer = attackGap * 0.6f; Fx.Shake(0.3f); }
        }

        void Shoot(Vector2 dir)
        {
            var e = World.SpawnById(shotId, Center);
            if (e == null) return;
            e.vel = dir.normalized * shotSpeed * (Enraged ? 1.2f : 1f);
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
