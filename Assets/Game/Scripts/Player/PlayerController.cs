using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    public interface IInteractable
    {
        /// <summary>What the screen shows while the player stands at it (see HUD.SplitPrompt); null for none.</summary>
        string GetPrompt(PlayerController p);
        void Interact(PlayerController p);
    }

    /// <summary>
    /// Somebody (or something) that offers more than one thing to do: a villager who can be talked to or whose
    /// shop can be opened. Nothing opens to choose from: the prompt at the edge of the screen lists what there
    /// is, one row each; the row with the button on it is what the interact button does, and up / down moves
    /// that row (PlayerController.Interactions). While the list is up, up and down do nothing else.
    /// </summary>
    public interface IInteractChoices : IInteractable
    {
        /// <summary>What can be done, by name, the usual one first. Null (or one) when there is nothing to choose: Interact is used.</summary>
        string[] Choices(PlayerController p);
        void Choose(PlayerController p, int index);
        /// <summary>Who (or what) this is: written over the list, so the player knows whom the rows belong to.</summary>
        string ChoiceTitle { get; }
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

        [Header("Blade visuals")]
        public SpriteRenderer whipRenderer;
        [Tooltip("Glider canopy shown while gliding.")] public SpriteRenderer gliderRenderer;
        [Tooltip("The Hearth Wisp flying beside the player while it is worn (a child with its own light).")] public SpriteRenderer wispRenderer;
        [Tooltip("Its flicker, in order; the last is a blink.")] public Sprite[] wispFrames;
        GlowLight wispGlow;
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
        /// <summary>The prompt is the writing on a sign (the screen shows it as a notice, not as something to do).</summary>
        public bool PromptIsSign { get; private set; }
        public float InvulnTime => invuln;
        public bool IsStunned => stunTimer > 0f;
        /// <summary>Encased in ice (snowball without a fur coat): cannot act until it wears off.</summary>
        public bool Frozen => frozen > 0f;
        float frozen;
        /// <summary>Chilled: moving slower for a moment (a snowball on someone with cold resistance).</summary>
        public bool Slowed => slowTimer > 0f;
        /// <summary>Breathing spores: left and right are swapped (Sporegloom).</summary>
        public bool Confused => confused > 0f;
        float confused;
        /// <summary>Jump was held in the last simulation step (bounce mushrooms throw higher then).</summary>
        public bool JumpHeld { get; private set; }
        float slowTimer, slowAmount;
        int tintState;      // 0 normal, 1 frozen (blue), 2 dizzy (green)

        float whipTimer = -1f, whipCooldown, invuln, stunTimer, lookTimer, throwAnimTimer;

        // The dash: a short burst straight along the way the player faces (or steers), on the ground or once in
        // the air. While it lasts - and for a moment after - nothing hurts: it goes through creatures, bosses
        // and whatever they throw. Walls stop it. Each dash is paid from an energy bar (PlayerConfig.dashCost of
        // dashEnergyMax); the bar fills again by itself once the player has not dashed for a moment.
        float dashTimer, dashGrace, ghostT, dashEnergy = -1f, sinceDash = 99f;
        int dashDir = 1;
        bool airDashed;
        static readonly Color DashColor = new Color(0.6f, 0.95f, 1f);
        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        readonly List<float> ghostLife = new List<float>();
        const float GhostTime = 0.22f;
        public bool Dashing => dashTimer > 0f;
        /// <summary>Nothing can hurt: dashing, or just out of a dash.</summary>
        public bool DashSafe => dashTimer > 0f || dashGrace > 0f;
        /// <summary>What is left in the energy bar, and how much it holds.</summary>
        public float DashEnergy => dashEnergy < 0f ? cfg.dashEnergyMax : dashEnergy;
        public float DashEnergyMax => cfg.dashEnergyMax;
        /// <summary>Enough energy for another dash.</summary>
        public bool CanAffordDash => DashEnergy >= cfg.dashCost;
        /// <summary>Goes up each time the dash button was pressed without the energy for it (the bar on the screen flashes).</summary>
        public int DashRefusals { get; private set; }
        /// <summary>Dashes made so far (tests).</summary>
        public int Dashes { get; private set; }
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
        /// <summary>Down on the ground: crouched or crawling.</summary>
        public bool Crouching => motor.crouching;
        public void PlayThrowAnim(float seconds) => throwAnimTimer = seconds;

        public Vector2 HoldPos => new Vector2(pos.x + facing * 0.35f, pos.y + 0.22f);

        public override void Tick(float dt)
        {
            if (IsDead) { TickCorpse(dt); return; }

            FrameInput fi = bot != null ? bot.Next() : (input != null ? input.Consume() : default);
            invuln -= dt; stunTimer -= dt; whipCooldown -= dt; throwAnimTimer -= dt; frozen -= dt; slowTimer -= dt; confused -= dt;
            if (confused > 0f) fi.x = -fi.x;        // dizzy from spores: left is right
            JumpHeld = fi.jumpHeld;
            bool stunned = stunTimer > 0f;

            dashGrace -= dt;
            if (dashEnergy < 0f) dashEnergy = cfg.dashEnergyMax;        // the bar starts full
            if (dashTimer <= 0f)
            {
                // left alone for a moment, the energy comes back
                sinceDash += dt;
                if (sinceDash >= cfg.dashRegenDelay) dashEnergy = Mathf.Min(cfg.dashEnergyMax, dashEnergy + cfg.dashRegenRate * dt);
            }
            if (motor.grounded && dashTimer <= 0f) airDashed = false;
            if (fi.dashPressed && !(fi.interactPressed && interactable) && !stunned && dashTimer <= 0f && motor.mode == MotorMode.Normal && !(airDashed && !motor.grounded))
            {
                if (dashEnergy >= cfg.dashCost) StartDash(fi.x != 0 ? Math.Sign(fi.x) : facing);
                else { DashRefusals++; Sfx.Play("error", pos, 0.6f); }
            }

            var mi = new MotorInput
            {
                x = fi.x, up = fi.up, down = fi.down, jumpPressed = fi.jumpPressed, jumpHeld = fi.jumpHeld, walk = fi.walk,
                stunned = stunned,
                dash = dashTimer > 0f ? dashDir : 0, dashSpeed = cfg.dashSpeed,
                speedMult = (held != null && held.heavy ? cfg.heavyCarrySpeed : 1f) * (slowTimer > 0f ? 1f - slowAmount : 1f),
                jumpMult = Run != null ? 1f + Run.stats.jumpBonus : 1f,
                grip = Mathf.Max(World.Level != null ? World.Level.grip : 1f, Run != null ? Run.stats.iceGrip : 0f),
            };
            EquippedGear?.ModifyMotor(this, ref mi);
            if (choiceOf != null && motor.mode == MotorMode.Normal && motor.grounded) { mi.up = false; mi.down = false; }      // (they move the chosen row: Interactions)
            var prevMode = motor.mode;
            float prevVy = motor.vel.y;
            PlayerMotor.Step(ref motor, mi, World.Grid, cfg.motor, dt);
            SyncFromMotor();
            MotorEvents(prevMode);
            if (IsDead) return;

            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                ghostT -= dt;
                if (ghostT <= 0f) { ghostT = 0.035f; SpawnGhost(); }
                if (motor.hitWall || motor.mode != MotorMode.Normal || stunned || dashTimer <= 0f) EndDash();
            }

            if (!stunned && !Dashing) HandleActions(fi);
            TickWhip(dt);
            UpdateHeld();
            if (!godMode && !DashSafe) Contacts(fi, prevVy);
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
                // Landing on a creature's head breaks the fall: the stomp (Contacts, right after this) takes the hit instead.
                if (motor.fallDistance >= cfg.fallDamageHeight + (Run != null ? Run.stats.fallBonus : 0f) && !godMode && !StompTargetBelow())      // (Soft-Sole Boots: fallBonus)
                {
                    TakeDamage(new DamageInfo(cfg.fallDamage, DamageType.Fall, null, Vector2.zero, "a long fall"));
                    stunTimer = Mathf.Max(stunTimer, cfg.fallStunTime);
                    Fx.Shake(0.4f);
                }
            }
            if (motor.grabbedThisStep) Sfx.Play("grab", pos);
            if (motor.mode == MotorMode.PullUp && prevMode != MotorMode.PullUp) Sfx.Play("climb", pos);
            if (motor.hitSpikes && !godMode)
            {
                // Spikes wound instead of killing outright: lose spikeDamage health and get thrown back up and
                // away, with the usual moment of invulnerability to get clear.
                // All spikes, everywhere: the thorn chasms under a glider's crossing used to kill outright, with
                // nothing to tell them from the others. Now they are spikes like any - whoever would rather pay
                // in hearts and walk may.
                int away = motor.vel.x > 0.5f ? -1 : motor.vel.x < -0.5f ? 1 : -facing;
                TakeDamage(new DamageInfo(Mathf.Max(1, cfg.spikeDamage), DamageType.Spikes, null,
                    new Vector2(away * 3f, cfg.spikeBounce), "spikes"));
            }
        }

        // ------------------------------------------------------------------ dash

        void StartDash(int dir)
        {
            dashDir = dir != 0 ? dir : 1;
            dashTimer = cfg.dashTime;
            dashEnergy = Mathf.Max(0f, dashEnergy - cfg.dashCost);
            sinceDash = 0f;
            if (!motor.grounded) airDashed = true;
            whipTimer = -1f;
            ghostT = 0f;
            Dashes++;
            squash = new Vector2(1.35f, 0.72f);
            Fx.Dust(pos, 4);
            Sfx.Play("throw", pos, 0.8f, 1.7f);
        }

        void EndDash()
        {
            dashTimer = 0f;
            dashGrace = cfg.dashGrace;
            sinceDash = 0f;
            // it does not coast on at dash speed
            float run = cfg.motor.runSpeed;
            motor.vel.x = Mathf.Clamp(motor.vel.x, -run, run);
            vel = motor.vel;
        }

        /// <summary>A fading picture of the player left behind in the dash.</summary>
        void SpawnGhost()
        {
            if (sr == null || sr.sprite == null) return;
            int slot = -1;
            for (int i = 0; i < ghosts.Count; i++) if (ghosts[i] != null && ghostLife[i] <= 0f) { slot = i; break; }
            if (slot < 0)
            {
                if (ghosts.Count >= 8) return;
                var go = new GameObject("DashGhost");
                var r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = sr.sharedMaterial;
                r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder - 1;
                ghosts.Add(r); ghostLife.Add(0f);
                slot = ghosts.Count - 1;
            }
            var g = ghosts[slot];
            g.sprite = sr.sprite; g.flipX = sr.flipX;
            g.transform.position = sr.transform.position;
            g.transform.localScale = sr.transform.lossyScale;
            g.color = new Color(DashColor.r, DashColor.g, DashColor.b, 0.6f);
            g.enabled = true;
            ghostLife[slot] = GhostTime;
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
            ghosts.Clear(); ghostLife.Clear();
        }

        // ------------------------------------------------------------------ actions

        void HandleActions(FrameInput fi)
        {
            // at home in the village nobody draws a blade or lights a fuse
            var session = RunSession.Current;
            if (session != null && session.inVillage) return;
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
            EquippedGear?.Hold(this, fi);
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

        /// <summary>A stompable enemy right under the feet (so a long fall ends on it, not on the ground).</summary>
        bool StompTargetBelow()
        {
            var r = Bounds;
            r.xMin += 0.06f; r.xMax -= 0.06f; r.yMin -= 0.25f;
            World.Query(r, tmp, e => e.IsEnemy && e.IsStompable && !e.Dead);
            foreach (var e in tmp)
                if (prevPos.y >= e.pos.y + e.size.y * 0.45f) return true;
            return false;
        }

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
                    if (en.CounterStomp(this)) continue;      // it punished the stomp: no damage to it, no bounce
                    en.TakeDamage(new DamageInfo(1 + (Run != null ? Run.stats.stompBonus : 0), DamageType.Stomp, this, new Vector2(0f, -2f), "a stomp"));      // (Hobnail Boots: stompBonus)
                    motor.vel.y = fi.jumpHeld ? cfg.stompBounceHeld : cfg.stompBounce;
                    motor.jumping = fi.jumpHeld;
                    motor.grounded = false;
                    motor.apexY = motor.pos.y;      // the fall ended here: the bounce starts a new one
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

        // Something within reach answers the interact button. On a pad that button is also the dash, so while
        // this is set the press goes to the thing and not to a dash.
        bool interactable;
        // which row of what somebody offers is chosen (IInteractChoices), and who that is
        IInteractable choiceOf;
        int choiceIndex;
        bool choiceUpWas, choiceDownWas;
        /// <summary>What the interact button will do with whoever is in reach, by its row in their list (0 when there is no list).</summary>
        public int ChoiceIndex => choiceIndex;
        /// <summary>Where the words of every row begin: the up / down mark and the button's picture sit before the chosen row's.</summary>
        const int ChoiceTextAt = 112;

        void Interactions(FrameInput fi)
        {
            Prompt = null;
            PromptIsSign = false;
            interactable = false;
            var r = Bounds;
            World.Query(r, tmp, e => e is IInteractable);
            IInteractable best = null; float bd = float.MaxValue;
            foreach (var e in tmp)
            {
                if (((IInteractable)e).GetPrompt(this) == null) continue;   // nothing to do with it (opened chest, entrance door)
                float d = Mathf.Abs(e.pos.x - pos.x);
                if (d < bd) { bd = d; best = (IInteractable)e; }
            }
            if (best != null)
            {
                interactable = true;
                Prompt = best.GetPrompt(this);
                PromptIsSign = best is Sign;
                // more than one thing to do here: the list, the one in front first; the change button turns it
                string[] offers = best is IInteractChoices ic0 ? ic0.Choices(this) : null;
                if (offers != null && offers.Length > 1)
                {
                    if (!ReferenceEquals(choiceOf, best)) { choiceOf = best; choiceIndex = 0; choiceUpWas = fi.up; choiceDownWas = fi.down; }
                    choiceIndex = Mathf.Clamp(choiceIndex, 0, offers.Length - 1);
                    // up / down (a key or the stick) moves the chosen row; the rows themselves stay where they are
                    int step = !IsStunned ? (fi.down && !choiceDownWas ? 1 : 0) - (fi.up && !choiceUpWas ? 1 : 0) : 0;
                    choiceUpWas = fi.up; choiceDownWas = fi.down;
                    if (step != 0) { choiceIndex = (choiceIndex + step + offers.Length) % offers.Length; Sfx.PlayUI("menu_select", 0.5f); }
                    var rows = new System.Text.StringBuilder();
                    // who it is, over the rows: smaller, in gold, beginning where the words of the rows begin
                    string title = ((IInteractChoices)best).ChoiceTitle;
                    if (!string.IsNullOrEmpty(title)) rows.Append("<pos=").Append(ChoiceTextAt).Append("><size=78%><color=#FFD98C>").Append(title).Append("</color></size>").Append((char)10);
                    for (int k = 0; k < offers.Length; k++)
                    {
                        if (k > 0) rows.Append('\n');
                        if (k == choiceIndex) rows.Append("[updown] [enter]<pos=").Append(ChoiceTextAt).Append('>').Append(offers[k]);
                        else rows.Append("<pos=").Append(ChoiceTextAt).Append("><color=#FFFFFF8C>").Append(offers[k]).Append("</color>");
                    }
                    Prompt = rows.ToString();        // (no words about how to choose: the mark before the button says it)
                }
                else { offers = null; choiceOf = null; choiceIndex = 0; }
                if (fi.interactPressed && !IsStunned)
                {
                    // something is being read or heard: the same button puts it down (it does not start it over)
                    var hud = GameDirector.Instance != null ? GameDirector.Instance.hud : null;
                    if (hud != null && hud.TalkUp) hud.AdvanceTalk();            // the rest of the sentence, the next one, the end
                    else if (hud != null && hud.NoteUp) hud.DismissHeld();
                    else if (best is Sign && hud != null) hud.ToggleSign(Prompt);    // a sign's card can be put away, and taken out again
                    else if (offers != null) ((IInteractChoices)best).Choose(this, choiceIndex);
                    else best.Interact(this);
                }
                return;
            }
            if (held == null && grounded)
            {
                World.Query(r, tmp, e => e is Item it && it.CanPickUp);
                if (tmp.Count > 0) Prompt = "[down] + [whip]  Pick up";
            }
            else if (held != null)
            {
                Prompt = held.heavy ? "[whip]  Throw  -  [down] + [whip]  Put down" : null;
            }
        }

        void Look(FrameInput fi, float dt)
        {
            bool still = grounded && Mathf.Abs(vel.x) < 0.1f && motor.mode == MotorMode.Normal && Prompt == null;
            int dir = fi.up ? 1 : (fi.down ? -1 : 0);
            if (still && dir != 0) lookTimer += dt; else lookTimer = 0f;
            float far = 1f + (Run != null ? Run.stats.lookBonus : 0f);         // (a Spyglass: lookBonus 1 = twice as far, in the same time)
            float target = lookTimer > cfg.lookDelay ? dir * cfg.lookDistance * far : 0f;
            LookOffset = Mathf.MoveTowards(LookOffset, target, dt * 9f * far);
        }

        // ------------------------------------------------------------------ damage

        public override bool TakeDamage(in DamageInfo d)
        {
            if (IsDead || godMode || Run == null) return false;
            // in a dash nothing hurts - except what nothing can stop (the Reaper, and whatever kills outright)
            if (DashSafe && d.type != DamageType.Wraith && d.amount < 99) return false;
            bool bypass = d.type == DamageType.Wraith || d.type == DamageType.Explosion || d.amount >= 99;
            if (invuln > 0f && !bypass) return false;
            int amount = d.amount;
            // an Iron Cap: something falling on the head does one less (a blow it takes all of glances off with a clank)
            if (d.type == DamageType.Falling && Run.stats.headGuard > 0f && amount < 99)
            {
                amount--;
                if (amount <= 0)
                {
                    Sfx.Play("hit", pos, 0.8f, 1.6f);       // (high: metal)
                    Fx.Sparks(Center + Vector2.up * 0.5f, new Color(0.85f, 0.85f, 0.9f), 6, 3f);
                    invuln = Mathf.Max(invuln, 0.4f);
                    return false;
                }
            }
            Run.health -= amount;
            Run.damageTaken += amount;
            Flash(0.25f);
            Fx.Blood(Center, BloodRed, 8);
            Fx.Shake(0.35f);
            Sfx.Play("hurt", pos);
            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerHurt(d, Describe(d, Run.health <= 0));
            // a Hearth Wisp worn: it gives its light, and the death is turned away once (the Reaper takes whom it takes)
            if (Run.health <= 0 && d.type != DamageType.Wraith && Run.stats.revive > 0 && Revive()) return true;
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

        /// <summary>Pushed without being hurt (snowballs): a short stagger and a knockback, no damage, no invulnerability.</summary>
        public void Shove(Vector2 knockback, float stun)
        {
            if (IsDead || godMode || DashSafe) return;
            stunTimer = Mathf.Max(stunTimer, stun);
            motor.mode = MotorMode.Normal;
            motor.vel = knockback;
            motor.grounded = false;
            motor.jumping = false;
            vel = motor.vel;
            Fx.Shake(0.15f);
        }

        /// <summary>Thrown straight up by something springy (bounce mushroom): the fall so far is forgotten, so no fall damage.</summary>
        public void Bounce(float upSpeed)
        {
            if (IsDead || godMode) return;
            motor.mode = MotorMode.Normal;
            motor.vel.y = upSpeed;
            motor.grounded = false;
            motor.jumping = false;
            motor.apexY = motor.pos.y;
            vel = motor.vel;
            squash = new Vector2(0.78f, 1.25f);
        }

        /// <summary>
        /// Carried sideways by a conveyor belt under the feet (Gearworks). Stops at walls. Heavy boots
        /// (stats.beltGrip) make the player ignore belts; the test bot (god mode) is not carried either.
        /// </summary>
        public void BeltCarry(float dx)
        {
            if (Run != null && Run.stats.beltGrip > 0f) return;
            Push(dx);
        }

        /// <summary>Moved sideways by something that is not a hit (a belt, a gale): no stun, no damage. Stops at walls.</summary>
        public void Push(float dx)
        {
            if (IsDead || godMode || dx == 0f) return;
            float edge = motor.pos.x + Mathf.Sign(dx) * HalfW + dx;
            int cx = Mathf.FloorToInt(edge);
            var g = World.Grid;
            if (g.IsSolid(cx, Mathf.FloorToInt(motor.pos.y + 0.1f)) || g.IsSolid(cx, Mathf.FloorToInt(motor.pos.y + cfg.motor.height - 0.1f))) return;
            motor.pos.x += dx;
            pos = motor.pos;
        }

        /// <summary>Dizzy for a while: left and right are swapped. No damage. A spore mask (stats.sporeResist) prevents it.</summary>
        public void Confuse(float seconds)
        {
            if (IsDead || godMode) return;
            if (Run != null && Run.stats.sporeResist > 0f) return;
            if (confused <= 0f)
            {
                Fx.Text(Center + Vector2.up * 0.9f, "DIZZY", new Color(0.7f, 1f, 0.5f), 0.9f);
                Sfx.Play("shatter", pos, 0.35f, 0.6f);
            }
            confused = Mathf.Max(confused, seconds);
        }

        /// <summary>Moves slower by the given fraction (0.3 = 30%) for a while. No damage, full control.</summary>
        public void Slow(float amount, float seconds)
        {
            if (IsDead || godMode) return;
            bool already = slowTimer > 0f;       // kept slow by something (a web underfoot): say it once, not every step
            slowAmount = Mathf.Clamp01(amount);
            slowTimer = Mathf.Max(slowTimer, seconds);
            if (already) return;
            Fx.Text(Center + Vector2.up * 0.9f, "SLOWED", new Color(0.7f, 0.9f, 1f), 0.8f);
            Sfx.Play("shatter", pos, 0.4f, 1.7f);
        }

        /// <summary>Frozen solid for a moment: no damage, but no control either.</summary>
        public void Freeze(float seconds)
        {
            if (IsDead || godMode || DashSafe) return;
            frozen = Mathf.Max(frozen, seconds);
            stunTimer = Mathf.Max(stunTimer, seconds);
            Fx.Text(Center + Vector2.up * 0.9f, "FROZEN", new Color(0.7f, 0.9f, 1f), 1f);
            Sfx.Play("shatter", pos, 0.6f, 1.4f);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            TakeDamage(new DamageInfo(damage, DamageType.Explosion, source, d * 9f + Vector2.up * 5f, "an explosion"));
            if (IsDead) vel = d * 9f + Vector2.up * 6f;
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }

        /// <summary>"a snake", "an ember bat", "the Broodmother" (names written with a capital are one of a kind).</summary>
        static string WithArticle(string name)
        {
            if (string.IsNullOrEmpty(name)) return "something";
            // already has one ("a snowball", "the Reaper", "the Yeti's snowball")
            string low = name.ToLowerInvariant();
            if (low.StartsWith("a ") || low.StartsWith("an ") || low.StartsWith("the ")) return name;
            if (char.IsUpper(name[0])) return "the " + name;
            return ("aeiou".IndexOf(char.ToLowerInvariant(name[0])) >= 0 ? "an " : "a ") + name;
        }

        /// <summary>
        /// What happened to the player, in words. fatal = this hit killed them (results screen, death text);
        /// otherwise it is the short line shown next to the damage number, which must not say "killed".
        /// </summary>
        string Describe(in DamageInfo d, bool fatal)
        {
            string src = d.source != null ? d.source.displayName : d.cause;
            string who = WithArticle(src);
            switch (d.type)
            {
                case DamageType.Spikes: return fatal ? "Impaled on spikes" : "Spiked";
                case DamageType.Explosion:
                    return d.source == this ? (fatal ? "Blown up by your own bomb" : "Caught in your own blast") : "Caught in an explosion";
                case DamageType.Arrow: return fatal ? "Shot by an arrow trap" : "Hit by an arrow";
                case DamageType.Fall: return fatal ? "Fell too far" : "Hard landing";
                case DamageType.Contact: return (fatal ? "Killed by " : "Hit by ") + who;
                case DamageType.Thrown: return d.source is Item it && it.thrower == this ? $"Hit by your own {src}" : $"Hit by a flying {src}";
                case DamageType.Crush: return $"Crushed by {who}";
                case DamageType.Falling: return $"Hit by a falling {src}";
                case DamageType.Fire: return "Burned by a flame vent";
                case DamageType.Wraith: return "Taken by the Reaper";
                default: return (fatal ? "Killed by " : "Hit by ") + who;
            }
        }

        /// <summary>The Hearth Wisp spends itself: back up with its health, a moment of grace, and the piece is gone.</summary>
        bool Revive()
        {
            string used = null;
            foreach (var id in Run.worn) { var o = Run.Owned(id); if (o != null && o.modifiers != null && o.modifiers.revive > 0) { used = id; break; } }
            if (used == null) return false;
            int hp = Mathf.Clamp(Run.Owned(used).modifiers.revive, 1, Mathf.Max(1, Run.maxHealth));
            Run.RemoveUpgrade(used);
            Run.health = hp;
            invuln = 2.5f;
            stunTimer = 0f;
            Vector2 at = wispRenderer != null && wispRenderer.enabled ? (Vector2)wispRenderer.transform.position : Center;
            Fx.Sparks(at, new Color(1f, 0.86f, 0.55f), 24, 6f);
            Fx.Sparks(Center, new Color(1f, 0.95f, 0.8f), 18, 4f);
            LevelLighting.Flash(Center, new Color(1f, 0.85f, 0.55f), 7f, 2f, 0.7f);
            Sfx.Play("idol", pos);
            var gd = GameDirector.Instance;
            if (gd != null) gd.ShowMessage("Your Hearth Wisp gave its light for you. You rise again.");
            Revived++;
            return true;
        }

        /// <summary>The Hearth Wisp, while one is worn: a little light bobbing at the player's shoulder, behind them.</summary>
        void UpdateWisp()
        {
            if (wispRenderer == null) return;
            bool on = Run != null && Run.stats.revive > 0 && !IsDead;
            if (wispRenderer.enabled != on)
            {
                wispRenderer.enabled = on;
                if (wispGlow == null) wispGlow = wispRenderer.GetComponent<GlowLight>();
                if (wispGlow != null) wispGlow.SetOn(on);
            }
            if (!on) return;
            float t = Time.time;
            wispRenderer.transform.localPosition = new Vector3(-facing * 0.6f + Mathf.Sin(t * 1.7f) * 0.12f, 1.3f + Mathf.Sin(t * 3.1f) * 0.12f, 0f);
            // the flame sways and pulses (8 frames a second); every few seconds it blinks
            if (wispFrames != null && wispFrames.Length >= 2)
            {
                int flick = wispFrames.Length - 1;
                bool blink = (t % 3.3f) < 0.14f;
                var f = blink ? wispFrames[flick] : wispFrames[Mathf.FloorToInt(t * 8f) % flick];
                if (f != null && wispRenderer.sprite != f) wispRenderer.sprite = f;
            }
            wispRenderer.flipX = facing < 0;
        }

        /// <summary>How many times a Hearth Wisp has turned death away from this player (tests).</summary>
        public int Revived { get; private set; }

        void Die(in DamageInfo d)
        {
            IsDead = true;
            DeathCause = Describe(d, true);
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
            UpdateWisp();
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i] == null || ghostLife[i] <= 0f) continue;
                ghostLife[i] -= Time.deltaTime;
                if (ghostLife[i] <= 0f) { ghosts[i].enabled = false; continue; }
                var gc = ghosts[i].color; gc.a = 0.6f * ghostLife[i] / GhostTime; ghosts[i].color = gc;
            }
            int tint = Frozen ? 1 : Confused ? 2 : 0;
            if (sr != null && tintState != tint)
            {
                tintState = tint;
                sr.color = tint == 1 ? new Color(0.55f, 0.82f, 1f) : tint == 2 ? new Color(0.72f, 1f, 0.6f) : Color.white;
            }
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
                    float ext = Run != null && Run.HasActive(UpgradeIds.IronWhip) ? 1.15f : 1f;
                    whipRenderer.transform.localScale = new Vector3(back ? 1f : ext, 1f, 1f);
                    whipRenderer.transform.localPosition = back
                        ? new Vector3(-facing * 0.45f, 0.75f, 0f)
                        : new Vector3(facing * (0.3f + 0.65f * ext), 0.42f, 0f);
                    whipRenderer.color = Run != null && Run.HasActive(UpgradeIds.IronWhip) ? new Color(0.8f, 0.85f, 1f) : Color.white;
                }
            }
        }
    }
}
