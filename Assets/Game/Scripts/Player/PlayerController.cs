using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    public interface IInteractable
    {
        /// <summary>Text shown at the bottom of the screen while overlapping; null for none.</summary>
        string GetPrompt(PlayerController p);
        /// <summary>Whether pressing Up (not only the Interact key) triggers it.</summary>
        bool UpTriggers { get; }
        void Interact(PlayerController p);
    }

    /// <summary>The explorer. Movement is delegated to PlayerMotor; this class handles actions, damage and feedback.</summary>
    public class PlayerController : Entity
    {
        public PlayerConfig cfg;
        public PlayerInputReader input;

        [Header("Animations")]
        public SpriteAnim animIdle;
        public SpriteAnim animRun;
        public SpriteAnim animJump;
        public SpriteAnim animFall;
        public SpriteAnim animCrouch;
        public SpriteAnim animCrawl;
        public SpriteAnim animClimb;
        public SpriteAnim animHang;
        public SpriteAnim animWhip;
        public SpriteAnim animThrow;
        public SpriteAnim animStunned;
        public SpriteAnim animDead;
        public SpriteAnim animLookUp;

        [Header("Whip visuals")]
        public SpriteRenderer whipRenderer;
        [Tooltip("Glider canopy shown while gliding.")] public SpriteRenderer gliderRenderer;
        public Sprite whipBackSprite;
        public Sprite whipFrontSprite;

        [NonSerialized] public MotorState motor;
        [NonSerialized] public Item held;
        [NonSerialized] public bool godMode;
        [NonSerialized] public IFrameInputSource bot;

        public bool IsDead { get; private set; }
        public string DeathCause { get; private set; }
        public float LookOffset { get; private set; }
        public string Prompt { get; private set; }
        public float InvulnTime => invuln;
        public bool IsStunned => stunTimer > 0f;

        float whipTimer = -1f, whipCooldown, invuln, stunTimer, lookTimer, throwAnimTimer;
        readonly HashSet<Entity> whipHits = new HashSet<Entity>();
        readonly List<Entity> tmp = new List<Entity>();
        static readonly Color BloodRed = new Color(0.75f, 0.1f, 0.12f);

        RunState Run => RunSession.Current;
        float HalfW => cfg.motor.width * 0.5f;

        public override bool CanBeHitByItems => !IsDead;
        public override bool TriggersTraps => !IsDead && (Mathf.Abs(vel.x) > 0.3f || Mathf.Abs(vel.y) > 0.3f);

        public override void OnSpawn()
        {
            size = new Vector2(cfg.motor.width, cfg.motor.height);
            base.OnSpawn();
            motor = MotorState.At(pos);
            facing = 1;
            if (whipRenderer != null) whipRenderer.enabled = false;
        }

        /// <summary>Behaviour of the equipped gear (null when nothing usable is equipped).</summary>
        GearBehaviour EquippedGear => Run == null || World == null || World.config.FindGear(Run.EquippedId) == null
            ? null : GearRegistry.Get(Run.EquippedId);

        public float HalfWidth => HalfW;
        public void PlayThrowAnim(float seconds) => throwAnimTimer = seconds;

        public Vector2 HoldPos => new Vector2(pos.x + facing * 0.35f, pos.y + 0.22f);

        public override void Tick(float dt)
        {
            if (IsDead) { TickCorpse(dt); return; }

            FrameInput fi = bot != null ? bot.Next() : (input != null ? input.Consume() : default);
            invuln -= dt; stunTimer -= dt; whipCooldown -= dt; throwAnimTimer -= dt;
            bool stunned = stunTimer > 0f;

            var mi = new MotorInput
            {
                x = fi.x, up = fi.up, down = fi.down, jumpPressed = fi.jumpPressed, jumpHeld = fi.jumpHeld, walk = fi.walk,
                stunned = stunned,
                speedMult = held != null && held.heavy ? cfg.heavyCarrySpeed : 1f,
                jumpMult = Run != null ? 1f + Run.stats.jumpBonus : 1f,
            };
            EquippedGear?.ModifyMotor(this, ref mi);
            var prevMode = motor.mode;
            float prevVy = motor.vel.y;
            PlayerMotor.Step(ref motor, mi, World.Grid, cfg.motor, dt);
            SyncFromMotor();
            MotorEvents(prevMode);
            if (IsDead) return;

            if (!stunned) HandleActions(fi);
            TickWhip(dt);
            UpdateHeld();
            if (!godMode) Contacts(fi, prevVy);
            Pickups();
            Interactions(fi);
            Look(fi, dt);
        }

        void SyncFromMotor()
        {
            pos = motor.pos; vel = motor.vel; grounded = motor.grounded; facing = motor.facing;
        }

        void MotorEvents(MotorMode prevMode)
        {
            if (motor.jumpedThisStep) { Sfx.Play("jump", pos); Fx.Dust(pos, 3); squash = new Vector2(0.82f, 1.2f); }
            if (grounded && Mathf.Abs(vel.x) > 4f && !motor.crouching)
            {
                runDustTimer -= GameConst.Dt;
                if (runDustTimer <= 0f) { runDustTimer = 0.2f; Fx.Dust(pos - new Vector2(facing * 0.2f, 0f), 1); }
            }
            if (motor.landed)
            {
                float hard = Mathf.Clamp01(-motor.landImpact / 16f);
                squash = new Vector2(1f + 0.3f * hard, 1f - 0.28f * hard);
                Fx.Dust(pos, motor.landImpact < -10f ? 6 : 2);
                if (motor.landImpact < -7f) Sfx.Play("land", pos, Mathf.Clamp01(-motor.landImpact / 18f));
                if (motor.fallDistance >= cfg.fallDamageHeight && !godMode)
                {
                    TakeDamage(new DamageInfo(cfg.fallDamage, DamageType.Fall, null, Vector2.zero, "a long fall"));
                    stunTimer = Mathf.Max(stunTimer, cfg.fallStunTime);
                    Fx.Shake(0.4f);
                }
            }
            if (motor.grabbedThisStep) Sfx.Play("grab", pos);
            if (motor.mode == MotorMode.PullUp && prevMode != MotorMode.PullUp) Sfx.Play("climb", pos);
            if (motor.hitSpikes && !godMode)
                TakeDamage(new DamageInfo(99, DamageType.Spikes, null, Vector2.zero, "spikes"));
        }

        // ------------------------------------------------------------------ actions

        void HandleActions(FrameInput fi)
        {
            if (fi.attackPressed)
            {
                if (held != null)
                {
                    if (fi.down && grounded) PlaceHeld(); else ThrowHeld(fi.up);
                }
                else if (fi.down && grounded && TryPickup()) { }
                else if (whipCooldown <= 0f && motor.mode == MotorMode.Normal) StartWhip();
            }
            if (fi.usePressed) UseGear(fi);
        }

        bool TryPickup()
        {
            var r = Bounds; r.xMin -= 0.3f; r.xMax += 0.3f; r.yMax += 0.1f;
            World.Query(r, tmp, e => e is Item it && it.CanPickUp);
            Item best = null; float bd = float.MaxValue;
            foreach (var e in tmp)
            {
                float d = Mathf.Abs(e.pos.x - HoldPos.x);
                if (d < bd) { bd = d; best = (Item)e; }
            }
            if (best == null) return false;
            held = best;
            best.OnPickedUp(this);
            Sfx.Play("pickup", pos);
            return true;
        }

        void ThrowHeld(bool up)
        {
            var it = held; held = null;
            it.pos = HoldPos;
            Vector2 v = up ? new Vector2(facing * cfg.throwUpVelocity.x, cfg.throwUpVelocity.y)
                           : new Vector2(facing * cfg.throwVelocity.x, cfg.throwVelocity.y);
            if (it.heavy) v *= 0.6f;
            v.x += vel.x * 0.4f;
            it.Throw(this, v);
            throwAnimTimer = 0.2f;
            Sfx.Play("throw", pos);
        }

        void PlaceHeld()
        {
            var it = held; held = null;
            it.pos = new Vector2(pos.x + facing * 0.3f, pos.y + 0.05f);
            it.Place(new Vector2(facing * 0.5f, 0f));
        }

        public void DropHeld()
        {
            if (held == null) return;
            var it = held; held = null;
            it.pos = HoldPos;
            it.Place(new Vector2(facing * 1.5f, 2f));
        }

        void StartWhip()
        {
            whipTimer = 0f;
            whipHits.Clear();
            whipCooldown = cfg.whipDuration + cfg.whipCooldown;
            Sfx.Play("whip", pos);
        }

        void TickWhip(float dt)
        {
            if (whipTimer < 0f) return;
            whipTimer += dt;
            if (whipTimer > cfg.whipDuration) { whipTimer = -1f; return; }
            Rect hit;
            if (whipTimer < cfg.whipBackEnd)
                hit = new Rect(pos.x - facing * 0.55f - 0.4f, pos.y + 0.5f, 0.8f, 0.8f);
            else if (whipTimer < cfg.whipFrontEnd)
            {
                float range = cfg.whipFrontSize.x + (Run != null ? Run.stats.whipRangeBonus : 0f);
                float x0 = facing > 0 ? pos.x + HalfW - 0.1f : pos.x - HalfW + 0.1f - range;
                hit = new Rect(x0, pos.y + 0.45f - cfg.whipFrontSize.y * 0.5f, range, cfg.whipFrontSize.y);
            }
            else return;

            World.Query(hit, tmp);
            int dmg = cfg.whipDamage + (Run != null ? Run.stats.whipDamageBonus : 0);
            foreach (var e in tmp)
            {
                if (e == this || e == held || whipHits.Contains(e)) continue;
                if (e is Pickup || e is Door || e is ShopItem || e is Sign) continue;
                whipHits.Add(e);
                e.OnWhipped(this, dmg, facing);
                Fx.Sparks(e.Center, new Color(1f, 0.9f, 0.6f), 3, 3f);
            }
        }

        /// <summary>The "Use gear" button: uses whatever is equipped in the gear wheel (see GearBehaviour).</summary>
        void UseGear(FrameInput fi)
        {
            if (Run == null) return;
            var gear = EquippedGear;
            if (gear == null) { Sfx.Play("error", pos); return; }
            gear.Use(this, fi);
        }

        void UpdateHeld()
        {
            if (held == null) return;
            if (held.Dead || held.holder != this) { held = null; return; }
            held.pos = HoldPos;
            held.vel = vel;
            held.facing = facing;
        }

        // ------------------------------------------------------------------ contacts

        void Contacts(FrameInput fi, float prevVy)
        {
            var me = Bounds;
            me.xMin += 0.06f; me.xMax -= 0.06f;
            World.Query(me, tmp, e => e.IsEnemy);
            foreach (var e in tmp)
            {
                if (e.Dead || IsDead) continue;
                var en = (Enemy)e;
                bool falling = vel.y < 0f || prevVy < 0f;
                bool above = prevPos.y >= e.pos.y + e.size.y * 0.45f;
                if (en.IsStompable && falling && above && motor.mode == MotorMode.Normal)
                {
                    en.TakeDamage(new DamageInfo(1, DamageType.Stomp, this, new Vector2(0f, -2f), "a stomp"));
                    motor.vel.y = fi.jumpHeld ? cfg.stompBounceHeld : cfg.stompBounce;
                    motor.jumping = fi.jumpHeld;
                    motor.grounded = false;
                    vel = motor.vel;
                    Sfx.Play("stomp", pos);
                    Fx.Dust(pos, 3);
                    continue;
                }
                if (en.ContactDamage > 0)
                {
                    int dir = pos.x < e.pos.x ? -1 : 1;
                    TakeDamage(new DamageInfo(en.ContactDamage, en.ContactType, e,
                        new Vector2(dir * cfg.hurtKnockback.x * en.knockbackScale, cfg.hurtKnockback.y), e.displayName));
                }
            }
        }

        void Pickups()
        {
            World.Query(Bounds, tmp, e => e is Pickup);
            foreach (var e in tmp) ((Pickup)e).Collect(this);
        }

        void Interactions(FrameInput fi)
        {
            Prompt = null;
            var r = Bounds;
            World.Query(r, tmp, e => e is IInteractable);
            IInteractable best = null; float bd = float.MaxValue;
            foreach (var e in tmp)
            {
                float d = Mathf.Abs(e.pos.x - pos.x);
                if (d < bd) { bd = d; best = (IInteractable)e; }
            }
            if (best != null)
            {
                Prompt = best.GetPrompt(this);
                bool upOk = best.UpTriggers && fi.upPressed && motor.mode == MotorMode.Normal && grounded;
                if ((fi.interactPressed || upOk) && !IsStunned) best.Interact(this);
                return;
            }
            if (held == null && grounded)
            {
                World.Query(r, tmp, e => e is Item it && it.CanPickUp);
                if (tmp.Count > 0) Prompt = $"DOWN + X : pick up {tmp[0].displayName}";
            }
            else if (held != null)
            {
                Prompt = held.heavy ? $"Carrying {held.displayName}  (X throw, DOWN+X put down)" : null;
            }
        }

        void Look(FrameInput fi, float dt)
        {
            bool still = grounded && Mathf.Abs(vel.x) < 0.1f && motor.mode == MotorMode.Normal && Prompt == null;
            int dir = fi.up ? 1 : (fi.down ? -1 : 0);
            if (still && dir != 0) lookTimer += dt; else lookTimer = 0f;
            float target = lookTimer > cfg.lookDelay ? dir * cfg.lookDistance : 0f;
            LookOffset = Mathf.MoveTowards(LookOffset, target, dt * 9f);
        }

        // ------------------------------------------------------------------ damage

        public override bool TakeDamage(in DamageInfo d)
        {
            if (IsDead || godMode || Run == null) return false;
            bool bypass = d.type == DamageType.Spikes || d.type == DamageType.Wraith || d.type == DamageType.Explosion;
            if (invuln > 0f && !bypass) return false;
            Run.health -= d.amount;
            Flash(0.25f);
            Fx.Blood(Center, BloodRed, 8);
            Fx.Shake(0.35f);
            Sfx.Play("hurt", pos);
            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerHurt(d, Describe(d));
            if (Run.health <= 0)
            {
                Run.health = 0;
                Die(d);
                return true;
            }
            invuln = cfg.invulnTime;
            stunTimer = Mathf.Max(stunTimer, cfg.hurtStunTime);
            if (d.knockback != Vector2.zero)
            {
                motor.mode = MotorMode.Normal;
                motor.vel = d.knockback;
                motor.grounded = false;
                motor.jumping = false;
                vel = motor.vel;
            }
            return true;
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            TakeDamage(new DamageInfo(damage, DamageType.Explosion, source, d * 9f + Vector2.up * 5f, "an explosion"));
            if (IsDead) vel = d * 9f + Vector2.up * 6f;
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }

        string Describe(in DamageInfo d)
        {
            string src = d.source != null ? d.source.displayName : d.cause;
            switch (d.type)
            {
                case DamageType.Spikes: return "Impaled on spikes";
                case DamageType.Explosion:
                    return d.source == this ? "Blown up by your own bomb" : "Caught in an explosion";
                case DamageType.Arrow: return "Shot by an arrow trap";
                case DamageType.Fall: return "Fell too far";
                case DamageType.Contact: return $"Killed by a {src}";
                case DamageType.Thrown: return d.source is Item it && it.thrower == this ? $"Hit by your own {src}" : $"Hit by a flying {src}";
                case DamageType.Crush: return $"Crushed by a {src}";
                case DamageType.Falling: return $"Hit by a falling {src}";
                case DamageType.Fire: return "Burned by a flame vent";
                case DamageType.Wraith: return "Taken by the Gloom Wraith";
                default: return $"Killed by {src}";
            }
        }

        void Die(in DamageInfo d)
        {
            IsDead = true;
            DeathCause = Describe(d);
            DropHeld();
            whipTimer = -1f;
            vel = d.knockback != Vector2.zero ? d.knockback : new Vector2(0f, 6f);
            grounded = false;
            Fx.Blood(Center, BloodRed, 18);
            Sfx.Play("death", pos);
            Fx.Shake(0.6f);
            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerDied(DeathCause);
        }

        void TickCorpse(float dt)
        {
            var b = StepPhysics(dt, cfg.motor.gravity);
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.3f;
        }

        // ------------------------------------------------------------------ visuals

        // squash & stretch of the sprite on jump / landing (visual only)
        Vector2 squash = Vector2.one;
        float runDustTimer;

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null)
            {
                squash = Vector2.Lerp(squash, Vector2.one, 1f - Mathf.Exp(-14f * Time.deltaTime));
                sr.transform.localScale = new Vector3(squash.x, squash.y, 1f);
            }
            if (anim == null) return;
            SpriteAnim a;
            anim.speed = 1f;
            if (IsDead) a = animDead;
            else if (stunTimer > 0f) a = animStunned;
            else if (motor.mode == MotorMode.Hang) a = animHang;
            else if (motor.mode == MotorMode.PullUp) a = animClimb;
            else if (motor.mode == MotorMode.Climb) { a = animClimb; anim.speed = Mathf.Abs(vel.y) > 0.01f ? 1f : 0f; }
            else if (whipTimer >= 0f) a = animWhip;
            else if (throwAnimTimer > 0f) a = animThrow;
            else if (!grounded) a = vel.y > 0f ? animJump : animFall;
            else if (motor.crouching) { a = Mathf.Abs(vel.x) > 0.1f ? animCrawl : animCrouch; }
            else if (Mathf.Abs(vel.x) > 0.1f) { a = animRun; anim.speed = Mathf.Clamp(Mathf.Abs(vel.x) / cfg.motor.runSpeed, 0.4f, 1.2f); }
            else if (LookOffset > 0.5f) a = animLookUp;
            else a = animIdle;

            if (a == animWhip && animWhip != null && animWhip.frames.Length >= 2)
                anim.SetFrame(animWhip, whipTimer < cfg.whipBackEnd ? 0 : 1);
            else anim.Play(a);

            if (sr != null) sr.enabled = IsDead || invuln <= 0f || ((int)(Time.time * 18f) % 2 == 0);

            if (gliderRenderer != null)
            {
                bool g = !IsDead && motor.gliding;
                gliderRenderer.enabled = g;
                if (g) gliderRenderer.transform.localPosition = new Vector3(0f, 1.25f + Mathf.Sin(Time.time * 6f) * 0.03f, 0f);
            }
            if (whipRenderer != null)
            {
                bool show = !IsDead && whipTimer >= 0f && whipTimer < cfg.whipFrontEnd;
                whipRenderer.enabled = show;
                if (show)
                {
                    bool back = whipTimer < cfg.whipBackEnd;
                    whipRenderer.sprite = back ? whipBackSprite : whipFrontSprite;
                    whipRenderer.flipX = facing < 0;
                    float ext = Run != null && Run.HasUpgrade(UpgradeIds.IronWhip) ? 1.15f : 1f;
                    whipRenderer.transform.localScale = new Vector3(back ? 1f : ext, 1f, 1f);
                    whipRenderer.transform.localPosition = back
                        ? new Vector3(-facing * 0.45f, 0.75f, 0f)
                        : new Vector3(facing * (0.3f + 0.65f * ext), 0.42f, 0f);
                    whipRenderer.color = Run != null && Run.HasUpgrade(UpgradeIds.IronWhip) ? new Color(0.8f, 0.85f, 1f) : Color.white;
                }
            }
        }
    }
}
