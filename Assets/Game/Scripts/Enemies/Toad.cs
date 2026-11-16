using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Jumper: waits, then leaps in an arc toward the player.</summary>
    public class Toad : Enemy
    {
        public float jumpX = 4.2f;
        public float jumpY = 10.5f;
        public float aggroRange = 9f;
        public Vector2 waitRange = new Vector2(1.0f, 2.0f);
        public SpriteAnim animIdle;
        public SpriteAnim animJump;

        float waitTimer;

        /// <summary>Its attack is the leap: sitting on the ground it is harmless.</summary>
        public override bool Attacking => !grounded;

        public override void OnSpawn()
        {
            base.OnSpawn();
            waitTimer = Random.Range(waitRange.x, waitRange.y);
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (grounded)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                if (stunTimer <= 0f)
                {
                    waitTimer -= dt;
                    var p = Player;
                    if (waitTimer <= 0f)
                    {
                        bool chase = p != null && !p.IsDead && (p.Center - Center).sqrMagnitude < aggroRange * aggroRange;
                        int dir = chase ? (p.pos.x >= pos.x ? 1 : -1) : (Random.value < 0.5f ? -1 : 1);
                        if (SpikesAhead(dir) || (WallAhead(dir) && !chase)) dir = -dir;
                        facing = dir;
                        float s = chase ? 1f : 0.45f;
                        vel = new Vector2(dir * jumpX * s, jumpY * (chase ? 1f : 0.6f));
                        grounded = false;
                        waitTimer = Random.Range(waitRange.x, waitRange.y);
                        Sfx.Play("toad", pos, 0.6f);
                    }
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.hitLeft || b.hitRight) { vel.x = -b.impactVelX * 0.3f; }
            CheckSpikes(b);
            anim?.Play(grounded ? animIdle : animJump);
        }
    }
}
