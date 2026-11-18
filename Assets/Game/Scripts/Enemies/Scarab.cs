using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: a scarab. Small, quick, and never alone - where one is placed a little pack of them hatches.
    /// They scuttle about until they see the player, then they all run at them and nip. One stroke kills one;
    /// the trouble is that there are three, and they do not arrive together.
    /// </summary>
    public class Scarab : Enemy
    {
        public float speed = 2.2f;
        public float chaseSpeed = 4.4f;
        public float sightRange = 5.5f;
        [Tooltip("How many hatch where one is placed (itself included).")] public int packSize = 3;
        [Header("Nip")]
        public float biteRange = 0.75f;
        public float biteWindup = 0.22f;
        public float biteTime = 0.14f;
        public float biteLunge = 6f;
        public float biteRecover = 0.7f;
        public SpriteAnim animWalk;

        /// <summary>Set while one scarab is being put down on its own (a boss's helper, a pack member): no pack hatches.</summary>
        public static bool Single;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animWalk);
            if (Single) return;
            // the rest of the pack, a step to either side where there is floor
            Single = true;
            try
            {
                for (int i = 1; i < packSize; i++)
                {
                    float x = pos.x + (i % 2 == 1 ? 1 : -1) * 0.6f * ((i + 1) / 2);
                    var g = World.Grid;
                    int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(pos.y + 0.1f);
                    if (!g.IsEmpty(cx, cy) || !g.IsSolid(cx, cy - 1)) x = pos.x;
                    var e = World.SpawnById("scarab", new Vector2(x, pos.y));
                    if (e != null) e.facing = i % 2 == 1 ? 1 : -1;
                    if (e is Enemy en) en.summoned = summoned;
                }
            }
            finally { Single = false; }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (stunTimer <= 0f && grounded)
            {
                var p = Player;
                bool sees = SeesPlayer(sightRange) && p != null && Mathf.Abs(p.pos.y - pos.y) < 1.6f;
                bool near = sees && Mathf.Abs(p.pos.x - pos.x) < biteRange && Mathf.Abs(p.pos.y - pos.y) < 0.8f;
                if (melee == MeleePhase.None && sees && Mathf.Abs(p.pos.x - pos.x) > 0.1f) facing = p.pos.x > pos.x ? 1 : -1;
                if (Melee(dt, near, biteWindup, biteTime, biteRecover))
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * biteLunge) : 0f;
                else if (sees) vel.x = BlockedAhead(facing) ? 0f : facing * chaseSpeed;
                else vel.x = PatrolStep() * speed;
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || (grounded && Mathf.Abs(vel.x) < 0.01f) ? 0f : (Mathf.Abs(vel.x) > speed + 0.5f ? 2f : 1f);
        }
    }
}
