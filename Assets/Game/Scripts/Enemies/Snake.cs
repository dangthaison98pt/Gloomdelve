using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Ground patrol: walks back and forth, turning at walls and ledges.</summary>
    public class Snake : Enemy
    {
        public float speed = 1.4f;
        public SpriteAnim animWalk;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (stunTimer <= 0f && grounded)
            {
                if (WallAhead(facing) || EdgeAhead(facing) || SpikesAhead(facing)) facing = -facing;
                vel.x = facing * speed;
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f ? 0f : 1f;
        }
    }
}
