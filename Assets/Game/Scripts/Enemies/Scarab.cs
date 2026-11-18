using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: a scarab. Small, quick, and never alone - where one is placed a little pack of them hatches.
    /// They scuttle about until they see the player; then they run at them, and from a few steps away they
    /// pounce: a crouch (the warning), then a leap through the air that hurts where it lands on someone. Up close
    /// they nip. One stroke kills one - and the death of one sends the rest of the pack into a frenzy for a few
    /// seconds: faster, and pouncing again almost at once. So a pack is best not picked off slowly.
    /// </summary>
    public class Scarab : Enemy
    {
        public float speed = 2.2f;
        public float chaseSpeed = 4.4f;
        public float sightRange = 6f;
        [Tooltip("How many hatch where one is placed (itself included).")] public int packSize = 3;
        [Header("Nip")]
        public float biteRange = 0.75f;
        public float biteWindup = 0.22f;
        public float biteTime = 0.14f;
        public float biteLunge = 6f;
        public float biteRecover = 0.7f;
        [Header("Pounce")]
        [Tooltip("It pounces when the player is between these distances (tiles, sideways).")] public float pounceMin = 1.3f;
        public float pounceMax = 3.6f;
        [Tooltip("Seconds it crouches first: the warning.")] public float pounceTell = 0.3f;
        public float pounceSpeedX = 6.8f;
        public float pounceSpeedY = 9.5f;
        [Tooltip("Seconds before it can pounce again.")] public float pounceCooldown = 1.8f;
        [Header("Frenzy (when one of the pack dies)")]
        public float frenzyTime = 4f;
        public float frenzySpeed = 1.4f;
        public float frenzyRange = 7f;
        public SpriteAnim animWalk;

        /// <summary>Set while one scarab is being put down on its own (a boss's helper, a pack member): no pack hatches.</summary>
        public static bool Single;

        float pounceT, crouchT, frenzy;
        bool crouching, pouncing;

        public bool Pouncing => pouncing;
        public bool Frenzied => frenzy > 0f;
        /// <summary>The nip and the pounce through the air are its attacks.</summary>
        public override bool Attacking => melee == MeleePhase.Strike || (pouncing && !grounded);

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animWalk);
            pounceT = Random.Range(0.2f, 1f);          // the pack does not leap as one
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

        /// <summary>One of the pack has died nearby: faster and leaping again at once, for a while.</summary>
        public void Enrage()
        {
            frenzy = frenzyTime;
            pounceT = Mathf.Min(pounceT, 0.25f);
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "!", new Color(1f, 0.4f, 0.3f), 0.5f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            pounceT -= dt;
            frenzy -= dt;
            float haste = frenzy > 0f ? frenzySpeed : 1f;
            if (pouncing && grounded && vel.y <= 0.01f) pouncing = false;              // landed
            if (stunTimer > 0f) { crouching = false; }
            if (stunTimer <= 0f && grounded && !pouncing)
            {
                var p = Player;
                bool sees = SeesPlayer(sightRange) && p != null && Mathf.Abs(p.pos.y - pos.y) < 2.2f;
                float dx = sees ? Mathf.Abs(p.pos.x - pos.x) : 99f;
                bool near = sees && dx < biteRange && Mathf.Abs(p.pos.y - pos.y) < 0.8f;
                if (melee == MeleePhase.None && sees && dx > 0.1f) facing = p.pos.x > pos.x ? 1 : -1;
                if (crouching)
                {
                    // the crouch before the leap
                    vel.x = 0f;
                    crouchT -= dt;
                    if (crouchT <= 0f)
                    {
                        crouching = false; pouncing = true;
                        vel = new Vector2(facing * pounceSpeedX * haste, pounceSpeedY);
                        grounded = false;
                        pounceT = pounceCooldown / haste;
                        Sfx.Play("jump", pos, 0.6f, 1.6f);
                    }
                }
                else if (Melee(dt, near, biteWindup, biteTime, biteRecover))
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * biteLunge) : 0f;
                else if (sees && pounceT <= 0f && dx >= pounceMin && dx <= pounceMax)
                {
                    crouching = true; crouchT = pounceTell / haste;
                    vel.x = 0f;
                    Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.35f), "!", new Color(1f, 0.85f, 0.3f), 0.35f);
                }
                else if (sees) vel.x = BlockedAhead(facing) ? 0f : facing * chaseSpeed * haste;
                else vel.x = PatrolStep() * speed;
            }
            else if (grounded && !pouncing) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || crouching || (grounded && Mathf.Abs(vel.x) < 0.01f) ? 0f : (Mathf.Abs(vel.x) > speed + 0.5f ? 2f : 1f);
        }

        protected override void Die(in DamageInfo d)
        {
            // the rest of the pack takes it badly
            var w = World;
            if (w != null)
            {
                var pack = new System.Collections.Generic.List<Scarab>();
                foreach (var e in w.Entities)
                    if (e is Scarab s && s != this && !s.Dead && (s.pos - pos).sqrMagnitude < frenzyRange * frenzyRange) pack.Add(s);
                foreach (var s in pack) s.Enrage();
            }
            base.Die(d);
        }
    }
}
