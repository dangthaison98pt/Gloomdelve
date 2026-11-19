using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode: a mimic. It lies on the floor looking exactly like something worth walking up to - a
    /// gem, a pot, a crate (one prefab per disguise: "mimic_gem", "mimic_pot", "mimic_crate", each with the
    /// picture, the size and the glow of the real thing) -
    /// until someone comes for it. Then it shudders (the warning, a short one), springs at them with its mouth
    /// open - that leap is its attack - and after that it will not leave them alone: it comes on in hops, leaping
    /// whenever it has the chance. Killed, it pays well.
    /// A blow wakes it as well as a visitor, so the careful hand strikes a thing before taking it.
    /// A Truth Lens (StatModifiers.mimicSight) shows the wearer which things are not what they seem: a red cast.
    /// </summary>
    public class Mimic : Enemy
    {
        [Tooltip("How close the player must come before it wakes.")] public float wakeRange = 1.5f;
        [Tooltip("Seconds it shudders before it springs.")] public float tellTime = 0.3f;
        public float leapSpeedX = 6.5f;
        public float leapSpeedY = 9f;
        [Tooltip("Seconds between two hops once it is awake.")] public float hopInterval = 0.85f;
        public float hopSpeedX = 4.5f;
        public float hopSpeedY = 7.5f;
        public float sightRange = 7f;
        public SpriteAnim animDormant;
        public SpriteAnim animAwake;

        enum State { Dormant, Tell, Awake }
        State state;
        float timer;
        bool leaping;

        public bool Dormant => state == State.Dormant;
        /// <summary>Lying still in its disguise, but the player wears a Truth Lens: shown with a red cast.</summary>
        public bool Revealed
        {
            get { var run = RunSession.Current; return state == State.Dormant && run != null && run.stats.mimicSight > 0f; }
        }
        /// <summary>Its leaps are its attack.</summary>
        public override bool Attacking => leaping && !grounded;
        public override bool IsStompable => stompable && state == State.Awake;
        public override bool TriggersTraps => state == State.Awake && base.TriggersTraps;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Dormant;
            anim?.Play(animDormant);
        }

        void Wake()
        {
            if (state != State.Dormant) return;
            state = State.Tell; timer = tellTime;
            anim?.Play(animAwake);
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "!", new Color(1f, 0.4f, 0.4f), 0.4f);
            Sfx.Play("toad", pos, 0.9f, 1.7f);
        }

        void Leap(PlayerController p, float sx, float sy)
        {
            int dir = p != null ? (p.pos.x >= pos.x ? 1 : -1) : facing;
            facing = dir;
            vel = new Vector2(dir * sx, sy);
            grounded = false;
            leaping = true;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            timer -= dt;
            var p = Player;
            bool alive = p != null && !p.IsDead;
            if (leaping && grounded && vel.y <= 0.01f) leaping = false;
            switch (state)
            {
                case State.Dormant:
                    vel.x = 0f;
                    if (alive && Mathf.Abs(p.pos.x - pos.x) < wakeRange && Mathf.Abs(p.pos.y - pos.y) < 1.2f) Wake();
                    break;
                case State.Tell:
                    vel.x = 0f;
                    if (timer <= 0f) { state = State.Awake; Leap(p, leapSpeedX, leapSpeedY); timer = hopInterval; Sfx.Play("jump", pos, 0.7f, 1.4f); }
                    break;
                case State.Awake:
                    if (grounded && stunTimer <= 0f)
                    {
                        vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                        if (timer <= 0f)
                        {
                            bool sees = alive && SeesPlayer(sightRange);
                            // towards the player when it sees them, otherwise about where it is
                            if (sees) Leap(p, Mathf.Abs(p.pos.x - pos.x) < 2.6f ? leapSpeedX : hopSpeedX, Mathf.Abs(p.pos.x - pos.x) < 2.6f ? leapSpeedY : hopSpeedY);
                            else { facing = Random.value < 0.5f ? -1 : 1; if (SpikesAhead(facing)) facing = -facing; vel = new Vector2(facing * hopSpeedX * 0.5f, hopSpeedY * 0.7f); grounded = false; }
                            timer = hopInterval;
                        }
                    }
                    break;
            }
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            bool was = state == State.Dormant;
            bool hit = base.TakeDamage(d);
            if (was && !Dead) { state = State.Awake; anim?.Play(animAwake); timer = 0.5f; }      // struck first: it is awake, but it has lost its spring
            return hit;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            // (only when it changes: the hurt flash uses the colour too)
            bool r = Revealed;
            if (sr == null || r == shownRevealed) return;
            shownRevealed = r;
            sr.color = r ? new Color(1f, 0.55f, 0.55f) : Color.white;
        }
        bool shownRevealed;
    }
}
