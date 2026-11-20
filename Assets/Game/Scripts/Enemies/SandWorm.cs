using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: a sand worm, waiting under the floor. All that shows is a small mound - and nothing but a
    /// blast can hurt it down there. It is the tomb's sentry: when the player comes within sight of its spot the
    /// mound shakes (the warning), the worm rises out of the ground, turns to the player and spits a clod of sand
    /// at them; then it sinks back, waits a moment and does it again, for as long as they are in reach. Whoever
    /// is standing right at its mound when it comes up is bitten instead: that lunge is its attack up close.
    /// Out of the ground - while it aims, and for a moment after the shot - it can be cut or stomped.
    /// It never leaves its spot.
    /// </summary>
    public class SandWorm : Enemy
    {
        [Tooltip("How far (tiles) it notices the player and comes up to spit.")] public float sightRange = 7f;
        [Tooltip("Closer than this (tiles, sideways) it lunges instead of spitting.")] public float biteRange = 1.5f;
        public float tellTime = 0.55f;
        [Tooltip("Seconds of the lunge: only now its body hurts.")] public float strikeTime = 0.35f;
        [Tooltip("Seconds it aims, out of the ground, before it spits.")] public float aimTime = 0.45f;
        [Tooltip("Seconds it stays out after the shot or the lunge: the opening.")] public float outTime = 0.9f;
        [Tooltip("Seconds under the sand before it comes up again.")] public float restTime = 1.5f;
        [Header("Spit")]
        public string shotId = "sandshot";
        public float shotSpeed = 7.5f;
        public SpriteAnim animMound;
        public SpriteAnim animOut;

        enum State { Buried, Tell, Strike, Aim, Out }
        State state;
        float timer;
        bool biting;

        public bool Buried => state == State.Buried || state == State.Tell;
        public bool Aiming => state == State.Aim;
        public override bool Attacking => state == State.Strike;
        public override bool IsStompable => stompable && !Buried;
        public override bool CanBeHitByItems => !Dead && !Buried;
        public override bool TriggersTraps => false;
        /// <summary>Shots fired so far (tests).</summary>
        public int Shots { get; private set; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Buried; timer = 0.6f;
            anim?.Play(animMound);
        }

        bool PlayerInReach(PlayerController p)
        {
            if (p == null || p.IsDead) return false;
            Vector2 d = p.Center - new Vector2(pos.x, pos.y + 1f);
            if (d.sqrMagnitude > sightRange * sightRange || Mathf.Abs(d.y) > 4f) return false;
            return World.LineClear(new Vector2(pos.x, pos.y + 1f), p.Center);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            timer -= dt;
            var p = Player;
            vel.x = 0f;
            if (vel.y > 0f) vel.y = 0f;       // (nothing lifts it out of the ground: not a blow, not a blast)
            switch (state)
            {
                case State.Buried:
                    anim?.Play(animMound);
                    if (timer <= 0f && PlayerInReach(p))
                    {
                        // right at the mound: the lunge. Further off: up to spit
                        biting = Mathf.Abs(p.pos.x - pos.x) < biteRange && Mathf.Abs(p.pos.y - pos.y) < 2.2f;
                        state = State.Tell; timer = tellTime;
                        Fx.Text(new Vector2(pos.x, pos.y + 0.9f), "!", new Color(1f, 0.85f, 0.3f), 0.5f);
                        Sfx.Play("toad", pos, 0.8f, 0.6f);
                    }
                    break;
                case State.Tell:
                    if (World.Frame % 5 == 0) Fx.Dust(new Vector2(pos.x + Random.Range(-0.3f, 0.3f), pos.y + 0.1f), 1);
                    if (timer <= 0f)
                    {
                        if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.2f) facing = p.pos.x > pos.x ? 1 : -1;
                        anim?.Play(animOut);
                        Fx.Dust(pos, 8);
                        Sfx.Play("throw", pos, 1f, 0.6f);
                        if (biting) { state = State.Strike; timer = strikeTime; }
                        else { state = State.Aim; timer = aimTime; }
                    }
                    break;
                case State.Strike:
                    if (timer <= 0f) { state = State.Out; timer = outTime; }
                    break;
                case State.Aim:
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.2f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f)
                    {
                        Spit(p);
                        state = State.Out; timer = outTime;
                    }
                    break;
                case State.Out:
                    if (timer <= 0f) { state = State.Buried; timer = restTime; anim?.Play(animMound); Fx.Dust(pos, 5); }
                    break;
            }
            StepPhysics(dt, gravity);
        }

        void Spit(PlayerController p)
        {
            if (p == null || p.IsDead) return;
            Vector2 from = new Vector2(pos.x + facing * 0.3f, pos.y + size.y - 0.2f);
            Vector2 to = p.Center - from;
            var s = World.SpawnById(shotId, from) as BossShot;
            if (s == null) return;
            s.vel = (to.sqrMagnitude > 0.001f ? to.normalized : new Vector2(facing, 0f)) * shotSpeed;
            s.owner = this;
            Shots++;
            Sfx.Play("throw", pos, 0.9f, 0.8f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (Buried && d.type != DamageType.Explosion) return false;      // under the sand only a blast reaches it
            // It is rooted in the ground: a blow hurts it but cannot throw it. (A blow's knockback, taken as it
            // comes, sent the whole worm sailing up out of its hole.)
            return base.TakeDamage(new DamageInfo(d.amount, d.type, d.source, Vector2.zero, d.cause));
        }

        protected override void OnHurt(in DamageInfo d) { }     // it does not flinch: it finishes its turn above ground
    }
}
