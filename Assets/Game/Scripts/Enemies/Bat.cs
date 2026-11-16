using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Flying chaser: sleeps on the ceiling until it sees you, then swoops toward you.</summary>
    public class Bat : Enemy
    {
        public float speed = 3.6f;
        public float accel = 9f;
        public float wakeRange = 6f;
        public SpriteAnim animHang;
        public SpriteAnim animFly;

        bool awake;
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
                desired = to.normalized * speed + new Vector2(0f, Mathf.Sin(bobT) * 1.5f);
                facing = to.x >= 0 ? 1 : -1;
            }
            else desired = new Vector2(facing * speed * 0.5f, Mathf.Sin(bobT));
            vel = Vector2.MoveTowards(vel, desired, accel * dt);
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.5f;
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
