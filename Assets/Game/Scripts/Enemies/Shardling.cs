using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode: a crab grown over with crystal. It patrols, and when it sees the player along its floor
    /// it tucks in (the warning) and comes at them in a long, fast rush - only the rush hurts. Its back is all
    /// points: it cannot be stomped. And it does not simply die: broken, it comes apart into smaller ones
    /// ('splitInto' of 'splitId'), quicker than their parent, which rush in their turn. The small ones do not split.
    /// </summary>
    public class Shardling : Enemy
    {
        public float speed = 1.5f;
        [Header("Rush")]
        [Tooltip("How far along the floor it starts its rush from.")] public float rushRange = 4.5f;
        public float rushWindup = 0.4f;
        public float rushTime = 0.55f;
        public float rushSpeed = 8f;
        public float rushRecover = 1f;
        [Header("Breaking apart")]
        [Tooltip("Prefab id of what it comes apart into (empty: it simply dies).")] public string splitId = "shardlet";
        public int splitInto = 2;
        public SpriteAnim animWalk;
        public SpriteAnim animRush;

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
                bool near = p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) < rushRange && Mathf.Abs(p.pos.y - pos.y) < 0.9f &&
                            World.LineClear(Center, p.Center);
                if (melee == MeleePhase.None && near && Mathf.Abs(p.pos.x - pos.x) > 0.1f) facing = p.pos.x > pos.x ? 1 : -1;
                if (Melee(dt, near, rushWindup, rushTime, rushRecover))
                {
                    anim?.Play(animRush != null ? animRush : animWalk);
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * rushSpeed) : 0f;
                    if (melee == MeleePhase.Strike && World.Frame % 3 == 0) Fx.Dust(pos, 1);
                }
                else { anim?.Play(animWalk); vel.x = PatrolStep() * speed; }
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || (grounded && Mathf.Abs(vel.x) < 0.01f && melee == MeleePhase.None) ? 0f : 1f;
        }

        protected override void Die(in DamageInfo d)
        {
            var w = World;
            if (w != null && !string.IsNullOrEmpty(splitId) && d.type != DamageType.Spikes)
                for (int i = 0; i < splitInto; i++)
                {
                    var e = w.SpawnById(splitId, new Vector2(pos.x + (i % 2 == 0 ? -0.25f : 0.25f), pos.y));
                    if (e == null) continue;
                    e.vel = new Vector2((i % 2 == 0 ? -1 : 1) * 3f, 6f);
                    e.grounded = false;
                    e.facing = i % 2 == 0 ? -1 : 1;
                    if (e is Enemy en) { en.summoned = true; en.stunTimer = 0.35f; }      // (the parent pays whatever there is to pay)
                }
            base.Die(d);
        }
    }
}
