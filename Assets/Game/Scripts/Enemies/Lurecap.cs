using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom: looks exactly like one of the harmless glowing mushrooms that light the way - until the player
    /// walks up to it. Then it stands up (a short tell: the glow turns red) and hops after the player like a toad;
    /// only its leap hurts. In the dark the lights are where you go, and some of the lights bite.
    /// Hitting it while it still pretends wakes it too.
    /// </summary>
    public class Lurecap : Toad
    {
        [Tooltip("It wakes when the player is this close.")] public float wakeRange = 1.9f;
        [Tooltip("Time between standing up and the first leap.")] public float wakeTell = 0.5f;
        public SpriteAnim animDormant;
        public Color awakeGlow = new Color(1f, 0.35f, 0.3f);

        bool awake;
        float tell;
        GlowLight glow;

        public bool IsAwake => awake;
        public override bool Attacking => awake && tell <= 0f && base.Attacking;
        /// <summary>Asleep it is part of the scenery: nothing to stomp.</summary>
        public override bool IsStompable => awake && base.IsStompable;

        protected override void Awake()
        {
            base.Awake();
            glow = GetComponent<GlowLight>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            awake = false; tell = 0f;
            anim?.Play(animDormant);
        }

        public override void Tick(float dt)
        {
            if (!awake)
            {
                hurtTimer -= dt;
                var p = Player;
                if (p != null && !p.IsDead && !p.godMode && (p.Center - Center).sqrMagnitude < wakeRange * wakeRange) WakeUp();
                StepPhysics(dt, gravity);
                return;
            }
            if (tell > 0f)
            {
                tell -= dt;
                hurtTimer -= dt; stunTimer -= dt;
                vel.x = 0f;
                StepPhysics(dt, gravity);
                return;
            }
            base.Tick(dt);
        }

        void WakeUp()
        {
            if (awake) return;
            awake = true;
            tell = wakeTell;
            anim?.Play(animIdle);
            if (glow != null) glow.SetColor(awakeGlow);
            Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.35f), "!", new Color(1f, 0.5f, 0.4f), 0.45f);
            Sfx.Play("toad", pos, 0.8f, 1.3f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            WakeUp();
            return base.TakeDamage(d);
        }
    }
}
