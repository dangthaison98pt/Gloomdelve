using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks boss: the Foreman, a huge wind-up overseer behind a shield, on a floor of conveyor belts.
    /// Its rule: nothing gets through the shield from the front. It walks at the player and jabs up close, and it
    /// turns slowly - so the fight is about getting behind it (the belts help or hinder), stomping it, or waiting
    /// for the moment after its presses, when it has run down and must wind its own key: then it is wide open.
    /// Its moves come in random order (never the same twice running), none shared with the other bosses:
    ///  - Saws: it sends two loose saw blades rolling along the floor at the player (jump them; a belt under
    ///    them speeds them up or slows them down).
    ///  - Lever: every conveyor in the room runs the other way.
    ///  - Presses: a row of stamping presses comes down from the ceiling one after another, away from it and
    ///    across the player's side of the room - stand between two of them.
    ///  - Rewind: run down, it stops and winds its key. No shield for that long.
    ///  - Mace throw: its mace goes out along the floor on its chain and comes back - jump it twice.
    /// Behind it is no longer safe for long: a player who stays there has the mace swung right round it.
    /// At half health it walks faster and rolls a third saw.
    /// Stomped too often, a blast of steam throws the player off and flips the belts.
    /// </summary>
    public class Foreman : Boss
    {
        public float walkSpeed = 1.3f;
        [Tooltip("Seconds it needs to turn round to a player behind it.")] public float turnDelay = 0.9f;
        public float actionGap = 2.4f;
        public float tellTime = 0.55f;
        [Header("Jab")]
        public float jabRange = 1.9f;
        public float jabWindup = 0.5f;
        public float jabTime = 0.2f;
        public float jabLunge = 6f;
        public float jabRecover = 1.0f;
        [Header("Saws")]
        public string sawId = "sawblade";
        public int saws = 2;
        public float sawGap = 0.6f;
        [Header("Presses")]
        public string pressId = "press";
        public int presses = 5;
        public float pressSpacing = 2.3f;
        public float pressInterval = 0.28f;
        [Tooltip("How long it stands winding its key after the presses: the opening.")] public float rewindTime = 1.6f;
        [Header("Mace spin (a player behind it)")]
        [Tooltip("Seconds a player may stay behind it before it swings.")] public float spinBehindTime = 0.35f;
        public float spinTell = 0.4f;
        public float spinTime = 0.35f;
        public float spinReach = 2.7f;
        public int spinDamage = 2;
        [Tooltip("Seconds after a spin before the next (it turns the slow way meanwhile).")] public float spinCooldown = 3f;
        [Header("Mace throw")]
        public float maceSpeed = 12f;
        public float maceReach = 10f;
        public float maceSize = 0.8f;
        public int maceDamage = 1;
        [Header("Enraged (half health and under)")]
        public float enragedWalk = 1.6f;
        public int enragedSaws = 3;
        public Material chainMaterial;
        public SpriteAnim animWalk, animCharge, animDazed;

        static readonly Color Steam = new Color(0.95f, 0.9f, 0.8f);
        enum State { Walk, SawTell, LeverTell, PressTell, Rewind, SpinTell, Spin, MaceTell, Mace }
        State state;
        float timer, turnT, sawTimer, spinCool, maceX, maceStartX, maceFloor, maceHitCool, spinFlip;
        int maceDir;
        bool maceBack, spinHit, maceShown;
        Vector2 maceAt;
        BeamVisual chain, ball, stud;

        /// <summary>At half health and under.</summary>
        public bool Enraged => HealthFraction <= 0.5f;
        public bool Spinning => state == State.SpinTell || state == State.Spin;
        public bool MaceOut => state == State.Mace;
        /// <summary>Times it swung its mace round / threw it (tests).</summary>
        public int Spins { get; private set; }
        public int MaceThrows { get; private set; }
        int step, sawsLeft, sawDir;

        public override bool Attacking => melee == MeleePhase.Strike;
        /// <summary>Run down and winding its key: the shield is off.</summary>
        public bool Rewinding => state == State.Rewind;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Walk; timer = 1.8f;
            anim?.Play(animWalk);
            chain = BeamVisual.Create(this, chainMaterial);
            ball = BeamVisual.Create(this, chainMaterial);
            stud = BeamVisual.Create(this, chainMaterial);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            foreach (var v in new[] { chain, ball, stud }) if (v != null) Destroy(v.gameObject);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (chain == null) return;
            if (Dead || !maceShown) { chain.Hide(); ball.Hide(); stud.Hide(); return; }
            Vector2 hand = new Vector2(pos.x, pos.y + size.y * 0.5f);
            chain.Show(hand, maceAt, 0.1f, new Color(0.3f, 0.32f, 0.4f));
            ball.ShowDot(maceAt, maceSize, new Color(0.16f, 0.17f, 0.23f));
            stud.ShowDot(maceAt + new Vector2(0.1f, 0.12f), maceSize * 0.35f, new Color(0.62f, 0.66f, 0.76f));
        }

        void HitWithMace(PlayerController p, int damage)
        {
            int away = p.pos.x >= pos.x ? 1 : -1;
            p.TakeDamage(new DamageInfo(damage, DamageType.Contact, this, new Vector2(away * 8f, 7f), displayName + "'s mace"));
        }

        /// <summary>Every conveyor in the room runs the other way.</summary>
        public static int ReverseBelts(World w)
        {
            int n = 0;
            foreach (var e in w.Entities) if (!e.Dead && e is Conveyor c) { c.Reverse(); n++; }
            return n;
        }

        float FloorY()
        {
            var g = World.Grid;
            int y = Mathf.FloorToInt(pos.y + 0.5f);
            while (y > 0 && !g.IsSolid(Mathf.FloorToInt(pos.x), y - 1)) y--;
            return y;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (HoldCounterPose(dt)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            spinCool -= dt;
            maceShown = state == State.Spin || state == State.Mace;
            // loose saws already promised keep coming whatever it does next
            if (sawsLeft > 0)
            {
                sawTimer -= dt;
                if (sawTimer <= 0f) { ThrowSaw(); sawsLeft--; sawTimer = sawGap; }
            }
            switch (state)
            {
                case State.Walk:
                {
                    float dx = p != null ? p.pos.x - pos.x : 0f;
                    bool behind = p != null && Mathf.Abs(dx) > 0.3f && (dx > 0 ? 1 : -1) != facing;
                    if (behind && melee == MeleePhase.None)
                    {
                        turnT += dt;
                        // a player who stays behind it gets the mace swung right round
                        if (spinCool <= 0f && turnT >= spinBehindTime && grounded && Mathf.Abs(dx) < spinReach + 1.5f)
                        {
                            state = State.SpinTell; timer = spinTell; turnT = 0f;
                            Tell(); anim?.Play(animCharge);
                            Sfx.Play("brute", pos, 1f, 1.5f);
                            break;
                        }
                        if (turnT >= turnDelay) { facing = -facing; turnT = 0f; }
                    }
                    else turnT = 0f;
                    bool near = p != null && !behind && Mathf.Abs(dx) < jabRange && Mathf.Abs(p.pos.y - pos.y) < 1.6f;
                    if (Melee(dt, near, jabWindup, jabTime, jabRecover))
                    {
                        vel.x = melee == MeleePhase.Strike ? (WallAhead(facing) ? 0f : facing * jabLunge) : 0f;
                        anim?.Play(animCharge);
                    }
                    else
                    {
                        vel.x = behind || p == null || Mathf.Abs(dx) < jabRange * 0.8f || WallAhead(facing) ? 0f : facing * walkSpeed * (Enraged ? enragedWalk : 1f);
                        anim?.Play(animWalk);
                        if (timer <= 0f && p != null && grounded && melee == MeleePhase.None)
                        {
                            // its moves, in random order: saws, lever, presses (after which it must rewind), the mace thrown
                            switch (NextMove(0, 1, 2, 3))
                            {
                                case 3: state = State.MaceTell; timer = tellTime; Tell(); anim?.Play(animCharge); Sfx.Play("brute", pos, 1f, 0.9f); break;
                                case 0: state = State.SawTell; timer = tellTime; Tell(); anim?.Play(animCharge); Sfx.Play("brute", pos, 1f, 1.3f); break;
                                case 1: state = State.LeverTell; timer = tellTime; Tell(); Sfx.Play("warning", pos, 0.8f, 0.7f); break;
                                default: state = State.PressTell; timer = tellTime; Tell(); Sfx.Play("brute", pos, 1f, 0.6f); break;
                            }
                        }
                    }
                    break;
                }
                case State.SawTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        sawDir = p != null && p.pos.x < pos.x ? -1 : 1;
                        sawsLeft = Enraged ? enragedSaws : saws; sawTimer = 0f;
                        state = State.Walk; timer = Rest(actionGap);
                    }
                    break;
                case State.LeverTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        if (ReverseBelts(World) > 0) Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.9f), "REVERSE!", Steam, 1f);
                        Fx.Shake(0.3f);
                        Sfx.Play("crack", pos, 1f, 0.6f);
                        state = State.Walk; timer = Rest(actionGap * 0.7f);
                    }
                    break;
                case State.PressTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        StampRow(p);
                        state = State.Rewind; timer = rewindTime;
                        anim?.Play(animDazed);
                        Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.9f), "...", Steam, 1.2f);
                    }
                    break;
                case State.Rewind:
                    vel.x = 0f;
                    anim?.Play(animDazed);
                    if (timer <= 0f) { state = State.Walk; timer = Rest(actionGap * 0.6f); }
                    break;
                case State.SpinTell:
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer <= 0f)
                    {
                        state = State.Spin; timer = spinTime; spinHit = false; spinFlip = 0f; Spins++;
                        Sfx.Play("throw", pos, 1f, 0.6f);
                    }
                    break;
                case State.Spin:
                {
                    vel.x = 0f;
                    // the mace goes once round it, low: both sides of it, nothing above its head
                    float a = (1f - Mathf.Clamp01(timer / spinTime)) * Mathf.PI * 2f;
                    maceAt = new Vector2(pos.x + Mathf.Cos(a) * spinReach, pos.y + 0.7f + Mathf.Sin(a) * 0.35f);
                    spinFlip += dt;
                    if (spinFlip >= 0.08f) { spinFlip = 0f; facing = -facing; }
                    if (!spinHit && p != null && Mathf.Abs(p.pos.x - pos.x) <= spinReach + 0.3f && p.pos.y - pos.y < 1.7f && p.pos.y - pos.y > -1f)
                    {
                        spinHit = true;
                        HitWithMace(p, spinDamage);
                        Fx.Shake(0.35f);
                    }
                    if (timer <= 0f)
                    {
                        if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.3f) facing = p.pos.x > pos.x ? 1 : -1;
                        state = State.Walk; spinCool = spinCooldown; timer = Mathf.Max(timer, Rest(actionGap * 0.5f));
                    }
                    break;
                }
                case State.MaceTell:
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    if (timer <= 0f)
                    {
                        maceDir = p != null && p.pos.x < pos.x ? -1 : 1;
                        facing = maceDir;
                        maceFloor = FloorY();
                        maceStartX = maceX = pos.x + maceDir * (size.x * 0.5f + 0.5f);
                        maceBack = false; maceHitCool = 0f; MaceThrows++;
                        maceAt = new Vector2(maceX, maceFloor + maceSize * 0.5f);
                        state = State.Mace;
                        Sfx.Play("throw", pos, 1f, 0.5f);
                    }
                    break;
                case State.Mace:
                {
                    vel.x = 0f;
                    anim?.Play(animCharge);
                    maceX += (maceBack ? -1 : 1) * maceDir * maceSpeed * dt;
                    maceAt = new Vector2(maceX, maceFloor + maceSize * 0.5f);
                    maceHitCool -= dt;
                    if (!maceBack)
                    {
                        bool wall = World.Grid.IsSolid(Mathf.FloorToInt(maceX + maceDir * maceSize * 0.5f), Mathf.FloorToInt(maceFloor + 0.2f));
                        if (wall || Mathf.Abs(maceX - maceStartX) >= maceReach) { maceBack = true; if (wall) Fx.Sparks(maceAt, new Color(1f, 0.9f, 0.6f), 6, 4f); }
                    }
                    else if ((maceX - maceStartX) * maceDir <= 0f)
                    {
                        state = State.Walk; timer = Rest(actionGap * 0.8f);
                        break;
                    }
                    // it runs along the floor: whoever stands in its way is hit, on the way out and on the way back
                    if (maceHitCool <= 0f && p != null && Mathf.Abs(p.pos.x - maceX) < maceSize * 0.5f + 0.3f
                        && p.pos.y < maceFloor + maceSize && p.pos.y > maceFloor - 0.9f)
                    {
                        maceHitCool = 0.6f;
                        HitWithMace(p, maceDamage);
                    }
                    break;
                }
            }
            StepPhysics(dt, gravity);
        }

        void ThrowSaw()
        {
            var w = World;
            float fy = FloorY();
            float x = pos.x + sawDir * (size.x * 0.5f + 0.6f);
            if (w.Grid.IsSolid(Mathf.FloorToInt(x), Mathf.FloorToInt(fy + 0.2f))) return;
            var saw = w.SpawnById(sawId, new Vector2(x, fy)) as SawBlade;
            if (saw == null) return;
            saw.Launch(sawDir);
            Sfx.Play("throw", pos, 1f, 0.7f);
        }

        /// <summary>A row of presses, starting next to it and marching away across the player's side of the room.</summary>
        void StampRow(PlayerController p)
        {
            var w = World;
            var g = w.Grid;
            int side = p != null && p.pos.x < pos.x ? -1 : 1;
            float fy = FloorY();
            for (int i = 0; i < presses; i++)
            {
                float x = Mathf.Floor(pos.x + side * (size.x * 0.5f + 1.2f + i * pressSpacing)) + 0.5f;
                int cx = Mathf.FloorToInt(x);
                if (cx < 1 || cx > g.Width - 2 || g.IsSolid(cx, Mathf.FloorToInt(fy + 0.2f))) break;
                // the ceiling of this column
                int cy = Mathf.FloorToInt(fy) + 1;
                while (cy < g.Height - 1 && !g.IsSolid(cx, cy)) cy++;
                var pr = w.SpawnById(pressId, new Vector2(x, cy - 0.95f)) as Press;
                if (pr != null) pr.Arm(fy, i * pressInterval);
            }
        }

        /// <summary>The shield: hits from the side it faces do nothing, except while it rewinds.</summary>
        public bool Blocks(in DamageInfo d)
        {
            if (state == State.Rewind || Countering) return false;
            if (d.type == DamageType.Explosion || d.type == DamageType.Stomp || d.type == DamageType.Spikes || d.type == DamageType.Crush) return false;
            if (d.source == null) return false;
            return (d.source.Center.x - pos.x) * facing > 0.05f;
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!Dead && Blocks(d))
            {
                Fx.Sparks(new Vector2(pos.x + facing * size.x * 0.5f, pos.y + size.y * 0.55f), new Color(1f, 0.9f, 0.6f), 8, 5f);
                Sfx.Play("crack", pos, 0.8f, 1.5f);
                return false;
            }
            return base.TakeDamage(d);
        }

        /// <summary>Stomped again: a blast of steam off its back throws the player high, and the belts flip.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 6f, 13f), 0.2f);
            ReverseBelts(World);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), Steam, 12);
            Fx.Shake(0.35f);
            Sfx.Play("crack", pos, 1f, 0.7f);
        }
    }
}
