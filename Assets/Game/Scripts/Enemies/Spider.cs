using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Ambusher: hangs on the ceiling, drops when you pass below, then hops after you.</summary>
    public class Spider : Enemy
    {
        public float dropRange = 7f;
        public float hopSpeedX = 3.6f;
        public float hopSpeedY = 9f;
        public SpriteAnim animHang;
        public SpriteAnim animIdle;
        public SpriteAnim animJump;

        bool hanging = true;

        /// <summary>Its attack is the pounce: it only hurts in the air (dropping from the ceiling or mid-hop).</summary>
        public override bool Attacking => !hanging && !grounded;
        float hopTimer;

        public override void Configure(SpawnPoint sp) { HangFromCeiling(sp); }

        public override void OnSpawn()
        {
            base.OnSpawn();
            anim?.Play(animHang);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            if (hanging)
            {
                if (p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) < 1.1f && p.pos.y < pos.y && pos.y - p.pos.y < dropRange &&
                    World.LineClear(Center, p.Center))
                    Drop();
                if (!World.Grid.IsSolid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + size.y + 0.1f))) Drop();
                return;
            }
            if (grounded && stunTimer <= 0f)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                hopTimer -= dt;
                if (hopTimer <= 0f)
                {
                    int dir = p != null && !p.IsDead ? (p.pos.x >= pos.x ? 1 : -1) : (Random.value < 0.5f ? -1 : 1);
                    if (SpikesAhead(dir)) dir = -dir;
                    facing = dir;
                    vel = new Vector2(dir * hopSpeedX, hopSpeedY * Random.Range(0.8f, 1.05f));
                    grounded = false;
                    hopTimer = Random.Range(0.7f, 1.3f);
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.4f;
            CheckSpikes(b);
            anim?.Play(grounded ? animIdle : animJump);
        }

        void Drop()
        {
            hanging = false;
            hopTimer = 0.5f;
            vel = Vector2.zero;
            grounded = false;
            anim?.Play(animJump);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (hanging) Drop();
            return base.TakeDamage(d);
        }
    }
}
