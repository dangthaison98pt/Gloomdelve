using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Outdoor flyer: circles around its perch in the sky, dives at the player below, then climbs back.</summary>
    public class Hawk : Enemy
    {
        public float hoverRadius = 2.5f;
        public float diveSpeed = 9.5f;
        public float climbSpeed = 3.6f;
        public float senseRange = 8f;
        public float diveCooldown = 1.4f;
        public SpriteAnim animFly;
        public SpriteAnim animDive;

        enum State { Hover, Dive, Climb }
        State state;
        Vector2 home, diveDir;
        float t, cooldown, diveDist;

        public override void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y);
            home = pos;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            home = pos;
            t = Random.value * 10f;
            anim?.Play(animFly);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (stunTimer > 0f)
            {
                var sb = StepPhysics(dt, gravity);
                if (sb.landed) vel.x = 0f;
                CheckSpikes(sb);
                return;
            }
            t += dt;
            cooldown -= dt;
            var p = Player;
            switch (state)
            {
                case State.Hover:
                {
                    var target = home + new Vector2(Mathf.Sin(t * 0.8f) * hoverRadius, Mathf.Sin(t * 1.7f) * 0.5f);
                    vel = Vector2.ClampMagnitude((target - pos) * 2.5f, 4f);
                    if (Mathf.Abs(vel.x) > 0.2f) facing = vel.x > 0 ? 1 : -1;
                    if (cooldown <= 0f && p != null && !p.IsDead && p.Center.y < Center.y - 1f &&
                        Mathf.Abs(p.pos.x - pos.x) < senseRange && (p.Center - Center).sqrMagnitude < senseRange * senseRange * 1.6f &&
                        World.LineClear(Center, p.Center))
                    {
                        state = State.Dive;
                        diveDir = (p.Center - Center).normalized;
                        diveDist = 0f;
                        facing = diveDir.x >= 0 ? 1 : -1;
                        anim?.Play(animDive);
                        Sfx.Play("bat", pos, 0.8f, 0.6f);
                    }
                    break;
                }
                case State.Dive:
                    vel = diveDir * diveSpeed;
                    diveDist += diveSpeed * dt;
                    break;
                case State.Climb:
                    vel = (home - pos).normalized * climbSpeed;
                    if ((home - pos).sqrMagnitude < 0.3f) { state = State.Hover; cooldown = diveCooldown; }
                    break;
            }
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos;
            if (state == State.Dive && (b.hitLeft || b.hitRight || b.hitCeiling || b.landed || b.grounded || diveDist > 13f ||
                                        (p != null && pos.y < p.pos.y - 1.5f)))
            {
                state = State.Climb;
                anim?.Play(animFly);
            }
            if (state == State.Climb && (b.hitLeft || b.hitRight || b.hitCeiling))
            {
                // blocked on the way home: make this spot the new perch
                home = pos;
            }
        }

        protected override void OnHurt(in DamageInfo d)
        {
            base.OnHurt(d);
            state = State.Climb;
            anim?.Play(animFly);
        }
    }
}
