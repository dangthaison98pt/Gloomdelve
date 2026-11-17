using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks: a wind-up guard behind a tall shield. Anything that comes at it from the front - the blade, a
    /// thrown rock, an arrow - rings off the shield and does nothing. It is slow to turn: get behind it (jump over
    /// it) and hit its back, stomp it, or blow it up. Up close it jabs: a short wind-up, then a lunge - only the
    /// lunge hurts. A magnet charm (StatModifiers.shieldPierce) lets the player's blade through the shield.
    /// </summary>
    public class Sentry : Enemy
    {
        public float speed = 1.1f;
        [Header("Jab")]
        public float jabRange = 1.3f;
        public float jabWindup = 0.42f;
        public float jabTime = 0.18f;
        public float jabLunge = 4.5f;
        public float jabRecover = 1.0f;
        [Tooltip("Seconds it needs to turn around to a player behind it: the opening.")] public float turnDelay = 0.75f;
        public SpriteAnim animWalk, animJab;

        float turnT;

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
                bool alive = p != null && !p.IsDead;
                float dx = alive ? p.pos.x - pos.x : 0f;
                bool level = alive && Mathf.Abs(p.pos.y - pos.y) < 1.2f;
                bool behind = alive && level && Mathf.Abs(dx) < 4f && Mathf.Abs(dx) > 0.15f && (dx > 0 ? 1 : -1) != facing;
                if (behind && melee == MeleePhase.None)
                {
                    turnT += dt;
                    if (turnT >= turnDelay) { facing = -facing; turnT = 0f; }
                }
                else turnT = 0f;
                bool near = alive && level && !behind && Mathf.Abs(dx) < jabRange && World.LineClear(Center, p.Center);
                if (Melee(dt, near, jabWindup, jabTime, jabRecover))
                {
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * jabLunge) : 0f;
                    anim?.Play(animJab);
                }
                else
                {
                    vel.x = behind ? 0f : PatrolStep() * speed;       // stands still while it grinds round
                    anim?.Play(animWalk);
                }
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || (grounded && Mathf.Abs(vel.x) < 0.01f && melee == MeleePhase.None) ? 0f : 1f;
        }

        /// <summary>Does the shield stop this hit? Only hits that arrive from the side it faces.</summary>
        public bool Blocks(in DamageInfo d)
        {
            if (d.type == DamageType.Explosion || d.type == DamageType.Stomp || d.type == DamageType.Spikes ||
                d.type == DamageType.Crush || d.type == DamageType.Fire) return false;
            if (d.source == null) return false;
            if ((d.source.Center.x - pos.x) * facing <= 0.05f) return false;          // from behind or right above
            var run = RunSession.Current;
            if (d.type == DamageType.Whip && d.source is PlayerController && run != null && run.stats.shieldPierce > 0f) return false;
            return true;
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!Dead && Blocks(d))
            {
                Fx.Sparks(new Vector2(pos.x + facing * size.x * 0.5f, pos.y + size.y * 0.55f), new Color(1f, 0.9f, 0.6f), 6, 4f);
                Sfx.Play("crack", pos, 0.7f, 1.8f);
                return false;
            }
            return base.TakeDamage(d);
        }
    }
}
