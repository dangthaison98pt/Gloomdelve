using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Ground patrol: walks back and forth, turning at walls and ledges. Bites when the player comes close:
    /// it rears back for a moment, then lunges - only the lunge hurts.</summary>
    public class Snake : Enemy
    {
        public float speed = 1.4f;
        [Header("Bite")]
        public float biteRange = 1.15f;
        public float biteWindup = 0.35f;
        public float biteTime = 0.16f;
        public float biteLunge = 5.5f;
        public float biteRecover = 0.9f;
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
                var p = Player;
                bool near = p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) < biteRange && Mathf.Abs(p.pos.y - pos.y) < 0.9f &&
                            World.LineClear(Center, p.Center);
                if (melee == MeleePhase.None && near && Mathf.Abs(p.pos.x - pos.x) > 0.1f) facing = p.pos.x > pos.x ? 1 : -1;
                if (Melee(dt, near, biteWindup, biteTime, biteRecover))
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * biteLunge)
                                                       : (BlockedAhead(-facing) ? 0f : -facing * 0.5f);   // rears back
                else vel.x = PatrolStep() * speed;
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || (grounded && Mathf.Abs(vel.x) < 0.01f) ? 0f : 1f;
        }
    }
}
