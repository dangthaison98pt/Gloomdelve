using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Ice enemy: holds its ground and lobs snowballs in an arc at the player. A snowball does no damage but
    /// shoves you back - dangerous next to a drop or spikes, doubly so on ice.
    /// </summary>
    public class Yeti : Enemy
    {
        public float sightRange = 9f;
        public float throwCooldown = 2.2f;
        public float windupTime = 0.45f;
        [Tooltip("Gravity of the snowball; the throw is aimed with it.")] public float ballGravity = 28f;
        public string projectileId = "snowball";
        public SpriteAnim animIdle;
        public SpriteAnim animThrow;

        float cooldown, windup;
        Vector2 aim;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            cooldown = 1f;
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            cooldown -= dt;
            var p = Player;
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
            if (stunTimer > 0f) { windup = 0f; anim?.Play(animIdle); }
            else if (windup > 0f)
            {
                windup -= dt;
                if (windup <= 0f) Throw();
            }
            else
            {
                anim?.Play(animIdle);
                if (p != null && !p.IsDead)
                {
                    if (Mathf.Abs(p.pos.x - pos.x) > 0.4f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (cooldown <= 0f && grounded && SeesPlayer(sightRange) && Mathf.Abs(p.pos.x - pos.x) > 1.2f)
                    {
                        windup = windupTime;
                        aim = p.Center;
                        anim?.Play(animThrow);
                    }
                }
            }
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
        }

        void Throw()
        {
            cooldown = throwCooldown;
            Vector2 from = new Vector2(pos.x + facing * 0.35f, pos.y + size.y + 0.1f);
            Vector2 d = aim - from;
            // a lob that lands on the spot the player stood on when the yeti wound up
            float t = Mathf.Clamp(Mathf.Abs(d.x) / 8f, 0.55f, 1.1f);
            var v = new Vector2(d.x / t, d.y / t + 0.5f * ballGravity * t);
            var e = World.SpawnById(projectileId, from);
            if (e != null)
            {
                e.vel = v;
                if (e is Snowball s) { s.gravity = ballGravity; s.owner = this; }
            }
            Sfx.Play("throw", pos);
        }

        protected override void OnHurt(in DamageInfo d)
        {
            stunTimer = Mathf.Max(stunTimer, 0.5f);
        }
    }
}
