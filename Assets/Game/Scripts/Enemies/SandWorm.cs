using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: a sand worm, waiting under the floor. All that shows is a small mound - and nothing can
    /// hurt it down there. When the player comes close the mound shakes (the warning), then the worm shoots up
    /// out of the ground: that lunge is its attack. It stays out for a moment, swaying, and that is when it can
    /// be cut or stomped; then it sinks back and waits again. It never leaves its spot.
    /// </summary>
    public class SandWorm : Enemy
    {
        [Tooltip("How close (tiles, sideways) the player must come before it stirs.")] public float wakeRange = 1.7f;
        public float tellTime = 0.6f;
        [Tooltip("Seconds of the lunge: only now it hurts.")] public float strikeTime = 0.35f;
        [Tooltip("Seconds it stays out afterwards: the opening.")] public float outTime = 1.3f;
        [Tooltip("Seconds under the sand before it can strike again.")] public float restTime = 1.4f;
        public SpriteAnim animMound;
        public SpriteAnim animOut;

        enum State { Buried, Tell, Strike, Out }
        State state;
        float timer;

        public bool Buried => state == State.Buried || state == State.Tell;
        public override bool Attacking => state == State.Strike;
        public override bool IsStompable => stompable && !Buried;
        public override bool CanBeHitByItems => !Dead && !Buried;
        public override bool TriggersTraps => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Buried; timer = 0.5f;
            anim?.Play(animMound);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            timer -= dt;
            var p = Player;
            vel.x = 0f;
            switch (state)
            {
                case State.Buried:
                    anim?.Play(animMound);
                    if (timer <= 0f && p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) < wakeRange && p.pos.y > pos.y - 0.5f && p.pos.y - pos.y < 2.2f)
                    {
                        state = State.Tell; timer = tellTime;
                        Fx.Text(new Vector2(pos.x, pos.y + 0.9f), "!", new Color(1f, 0.85f, 0.3f), 0.5f);
                        Sfx.Play("toad", pos, 0.8f, 0.6f);
                    }
                    break;
                case State.Tell:
                    if (World.Frame % 5 == 0) Fx.Dust(new Vector2(pos.x + Random.Range(-0.3f, 0.3f), pos.y + 0.1f), 1);
                    if (timer <= 0f)
                    {
                        state = State.Strike; timer = strikeTime;
                        anim?.Play(animOut);
                        Fx.Dust(pos, 8);
                        Sfx.Play("throw", pos, 1f, 0.6f);
                    }
                    break;
                case State.Strike:
                    if (timer <= 0f) { state = State.Out; timer = outTime; }
                    break;
                case State.Out:
                    if (timer <= 0f) { state = State.Buried; timer = restTime; anim?.Play(animMound); Fx.Dust(pos, 5); }
                    break;
            }
            StepPhysics(dt, gravity);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (Buried && d.type != DamageType.Explosion) return false;      // under the sand only a blast reaches it
            return base.TakeDamage(d);
        }

        protected override void OnHurt(in DamageInfo d) { }     // it does not flinch: it finishes its turn above ground
    }
}
