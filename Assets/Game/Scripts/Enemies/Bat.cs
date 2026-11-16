using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Flying chaser: sleeps on the ceiling until it sees you, flies up to you, hangs back for a moment and
    /// then swoops - only the swoop hurts. After a swoop it backs off before trying again.</summary>
    public class Bat : Enemy
    {
        public float speed = 3.6f;
        public float accel = 9f;
        public float wakeRange = 6f;
        [Header("Swoop")]
        public float swoopRange = 2.0f;
        public float swoopWindup = 0.35f;
        public float swoopTime = 0.38f;
        public float swoopSpeed = 7.5f;
        public float swoopRecover = 1.1f;
        public SpriteAnim animHang;
        public SpriteAnim animFly;

        bool awake;
        Vector2 swoopDir;
        float bobT;

        public override void Configure(SpawnPoint sp) { HangFromCeiling(sp); facing = 1; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            anim?.Play(animHang);
            bobT = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            if (!awake)
            {
                if (SeesPlayer(wakeRange) && p.Center.y <= Center.y + 1f) Wake();
                return;
            }
            bobT += dt * 7f;
            Vector2 desired;
            if (stunTimer > 0f) desired = Vector2.zero;
            else if (p != null && !p.IsDead)
            {
                Vector2 to = p.Center - Center;
                Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.down;
                if (melee != MeleePhase.Strike) facing = to.x >= 0 ? 1 : -1;
                bool inReach = to.sqrMagnitude < swoopRange * swoopRange && World.LineClear(Center, p.Center);
                if (Melee(dt, inReach, swoopWindup, swoopTime, swoopRecover))
                {
                    if (melee == MeleePhase.Strike) { vel = swoopDir * swoopSpeed; desired = vel; }     // committed: straight at where the player was
                    else desired = -dir * 1.2f + new Vector2(0f, 1.2f);                                // hangs back and rises: the tell
                }
                else if (melee == MeleePhase.Recover) desired = -dir * speed * 0.7f + new Vector2(0f, 1.5f);   // backs off
                else desired = dir * speed + new Vector2(0f, Mathf.Sin(bobT) * 1.5f);
            }
            else desired = new Vector2(facing * speed * 0.5f, Mathf.Sin(bobT));
            vel = Vector2.MoveTowards(vel, desired, accel * dt);
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.5f;
        }

        protected override void OnMeleeStrike()
        {
            var p = Player;
            Vector2 to = p != null ? p.Center - Center : Vector2.down;
            swoopDir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.down;
            facing = swoopDir.x >= 0 ? 1 : -1;
            Sfx.Play("bat", pos, 0.7f, 1.3f);
        }

        void Wake()
        {
            awake = true;
            anim?.Play(animFly);
            Sfx.Play("bat", pos);
            vel = new Vector2(0f, -1.5f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!awake) Wake();
            return base.TakeDamage(d);
        }
    }
}
