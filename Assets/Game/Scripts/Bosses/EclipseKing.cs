using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The last boss: the Eclipse King, in a bare hall (no traps, nothing else alive). Two fights in one.
    ///
    /// THE SHELL. A beast of black stone, hunched on its fists, the Sunstone's light leaking through the cracks in
    /// its chest (they open wider below half its health). It walks at the player and has three moves, in random
    /// order: a breath of fire out of its chest, low across half the hall (jump it), a charge as a ball of fire the length of the hall
    /// that ends with the horns stuck in the wall, and a leap that comes down on the player with a wall of light
    /// along the floor to either side (too tall to jump, and the blade does nothing to it: dash through).
    ///
    /// When the shell's health is gone it does not die: it sinks to its knees, the stone bursts off it, the hall
    /// goes dark - and what was inside rises. Its health is full again.
    ///
    /// THE CORE. Pure light: a body without a face, the Sunstone burning in its chest, the covered sun behind its
    /// head, two hands of light that are not joined to it. It hangs in the air out of the blade's reach. Its moves:
    /// a column of light that walks across the hall (it stops at a ledge - stand under one, or dash through) with
    /// embers raining after it; a hand that comes down on the player (step out from under it); both hands swept
    /// along the floor from the walls until they meet (jump them); the stone's burst (the wall of light again, and
    /// embers into the air); and vanishing, to come back over the player with a hand already raised.
    /// Below half its health a second move may follow the first at once.
    ///
    /// The openings: with its horns stuck (the shell), and after each of the core's moves, when it sinks to the
    /// floor with its chest unguarded - a blow of the player's own counts double then.
    /// When the core is put out, the Sunstone drops where it hung (Boss.rewardId).
    /// </summary>
    public class EclipseKing : Boss
    {
        public enum Act
        {
            Idle,
            SweepTell, Sweep, ChargeTell, Charge, Stuck, Leap, Recover, Shatter,              // the shell
            Rise, Cast, SlamTell, Slam, ClapTell, Clap, NovaTell, BlinkOut, Lower, Open       // the core
        }
        /// <summary>The moves (ForceMove, tests).</summary>
        public const int MoveSweep = 0, MoveCharge = 1, MoveLeap = 2, MoveBeam = 3, MoveSlam = 4, MoveClap = 5, MoveNova = 6, MoveBlink = 7;

        [Header("The fight")]
        public float actionGap = 1.3f;
        [Tooltip("The shell walks at the player between moves: there is no far corner to rest in.")] public float walkSpeed = 1.8f;
        [Header("Shell: swipe")]
        public float sweepTell = 0.55f;
        [Tooltip("How far in front of it the arm reaches ...")] public float sweepReach = 5.5f;
        [Tooltip("... and how high above the floor (a jump clears it).")] public float sweepHeight = 1.5f;
        public int sweepDamage = 2;
        [Header("Shell: charge")]
        public float chargeTell = 0.6f;
        public float chargeSpeed = 15f;
        [Tooltip("How tall it is with its head down in the charge.")] public float chargeHeight = 2.6f;
        [Tooltip("Seconds its horns stay in the wall: its chest is open.")] public float stuckTime = 2f;
        [Header("Shell: leap")]
        public float leapVelocity = 16f;
        public float leapReach = 9f;
        public float shockSpeed = 9f;
        [Tooltip("The wave its landing (and the core's burst) throws along the floor: taller than any jump, and the blade does nothing to it - it has to be dashed through.")] public string waveId = "sunwave";
        [Header("The shell breaks")]
        [Tooltip("Seconds it kneels, cracking, before the stone bursts off it (nothing touches it meanwhile).")] public float shatterTime = 1.8f;
        [Tooltip("The core's health (the shell's is maxHealth).")] public int coreHealth = 40;
        public Vector2 coreSize = new Vector2(2.4f, 5f);
        [Tooltip("What is left of the hall's light once the shell is gone.")] [Range(0, 1)] public float eclipseLight = 0.3f;
        [Header("Core")]
        [Tooltip("How high over the floor it hangs (its lower end).")] public float hoverHeight = 3.2f;
        public float riseSpeed = 7f;
        public float driftSpeed = 2.4f;
        [Tooltip("Seconds it stays on the floor, chest unguarded, after a move.")] public float openTime = 1.6f;
        [Tooltip("Below half its health: how often a second move follows the first at once, with no opening between.")] [Range(0, 1)] public float chainChance = 0.5f;
        [Header("Core: the column of light")]
        public float beamTell = 0.9f;
        public float beamSpeed = 8f;
        public float beamWidth = 1.3f;
        public int beamDamage = 2;
        public int embers = 8;
        public float emberGap = 0.3f;
        public float emberSpeed = 9f;
        public string emberId = "sunember";
        [Header("Core: the hands")]
        public Vector2 handSize = new Vector2(1.8f, 1.5f);
        [Tooltip("A hand hangs over the player this long before it comes down ...")] public float slamTell = 0.7f;
        [Tooltip("... and stands still for the last part of it: the time to step out from under.")] public float slamHang = 0.25f;
        public float slamSpeed = 26f;
        public int slamDamage = 2;
        public float clapTell = 0.35f;
        public float clapSpeed = 11f;
        public int clapDamage = 2;
        [Header("Core: the burst, and vanishing")]
        public float novaTell = 0.8f;
        public float blinkTime = 0.7f;
        [Header("Pictures")]
        public Sprite beamSprite;
        public Sprite handOpen, handFist;
        public SpriteAnim animIdle, animWalk, animWindup, animLunge, animOpen, slashFx;
        [Tooltip("The ball of fire it charges as, and catching fire where it stands before that (the tell; played once, as long as chargeTell).")] public SpriteAnim animCharge, animIgnite;
        [Tooltip("The same, with the cracks wide (half the shell's health and under).")] public SpriteAnim animIdleCracked, animWalkCracked, animWindupCracked, animLungeCracked;
        public SpriteAnim animCore, animCoreCast, animCoreOpen;

        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Stone = new Color(0.25f, 0.24f, 0.36f);
        static readonly int[] ShellMoves = { MoveSweep, MoveCharge, MoveLeap };
        static readonly int[] CoreMoves = { MoveBeam, MoveSlam, MoveClap, MoveNova, MoveBlink, MoveSlam };

        Act act;
        int form = 1, forced = -1, embersLeft, slamHand;
        float timer, fullHeight, floorY, ceilY, leftX, rightX, light = 1f, emberT, markT, capT;
        bool swept, chained, embersStarted, eclipse, lightTouched, handHit;

        // the column of light
        int beamPhase, beamDir;         // 0 none, 1 warning, 2 walking
        float beamX, beamT;
        SpriteRenderer beamSr;

        // the two hands of the core (0 left, 1 right): where each is (lower edge, middle) and where it was a step ago
        readonly Vector2[] hand = new Vector2[2], handPrev = new Vector2[2];
        readonly SpriteRenderer[] handSr = new SpriteRenderer[2];
        readonly bool[] handLive = new bool[2];      // it hurts to be under / in front of it

        /// <summary>1 the shell, 2 the core.</summary>
        public int Form => form;
        public Act Doing => act;
        /// <summary>Its chest is unguarded: the player's own blows count double.</summary>
        public bool Exposed => act == Act.Stuck || act == Act.Open;
        /// <summary>The cracks in the shell stand wide (half its health and under).</summary>
        public bool Cracked => form == 1 && HealthFraction <= 0.5f;
        public bool BeamWalking => beamPhase == 2;
        public float BeamX => beamX;
        public bool Darkened => eclipse;
        public Vector2 HandAt(int i) => hand[i];
        /// <summary>Counts of what it has done (tests).</summary>
        public int Stucks { get; private set; }
        public int Leaps { get; private set; }
        public int Sweeps { get; private set; }
        public int Beams { get; private set; }
        public int Slams { get; private set; }
        public int Claps { get; private set; }
        public int Novas { get; private set; }
        public int Blinks { get; private set; }

        /// <summary>Only what it does hurts: the shell's charge and its coming down. Nothing of the core's body does - its light and its hands do.</summary>
        public override bool Attacking => form == 1 && (act == Act.Charge || (act == Act.Leap && vel.y < 0f));

        Vector2 Chest => new Vector2(pos.x, pos.y + size.y * 0.6f);
        float HalfW => size.x * 0.5f;
        float HoverY => Mathf.Max(floorY + 0.5f, Mathf.Min(floorY + hoverHeight, ceilY - size.y - 0.5f));
        SpriteAnim Worn(SpriteAnim whole, SpriteAnim cracked) => Cracked && cracked != null ? cracked : whole;

        public override void OnSpawn()
        {
            base.OnSpawn();
            fullHeight = size.y;
            timer = 1.8f;
            var g = World.Grid;
            floorY = pos.y;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y + 0.5f);
            int y = cy; while (y < g.Height - 1 && !g.IsSolid(cx, y)) y++;
            ceilY = y;
            int x = cx; while (x > 0 && !g.IsSolid(x, cy)) x--;
            leftX = x + 1;
            x = cx; while (x < g.Width - 1 && !g.IsSolid(x, cy)) x++;
            rightX = x;
            anim?.Play(animIdle);
        }

        /// <summary>The next move it picks is this one (tests).</summary>
        public void ForceMove(int move) { forced = move; timer = 0f; }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Target;
            Dim(dt);
            TickBeam(dt, p);
            timer -= dt;
            if (form == 1) TickShell(dt, p);
            else TickCore(dt, p);
        }

        // ================================================================== the shell

        void TickShell(float dt, PlayerController p)
        {
            switch (act)
            {
                case Act.Idle:
                    vel.x = 0f;
                    if (p != null)
                    {
                        float dx = p.pos.x - pos.x;
                        if (Mathf.Abs(dx) > 0.4f) facing = dx > 0f ? 1 : -1;
                        if (grounded && Mathf.Abs(dx) > HalfW + 1.2f) vel.x = facing * walkSpeed * (Cracked ? 1.35f : 1f);
                    }
                    // it steps when it moves (never a slide across the floor)
                    anim?.Play(vel.x != 0f && animWalk != null ? Worn(animWalk, animWalkCracked) : Worn(animIdle, animIdleCracked));
                    if (p != null && timer <= 0f && grounded) ChooseShell(p);
                    break;
                case Act.SweepTell:
                    vel.x = 0f;
                    anim?.Play(Worn(animWindup, animWindupCracked));
                    if (timer <= 0f) Strike();
                    break;
                case Act.Sweep:
                    vel.x = 0f;
                    anim?.Play(Worn(animLunge, animLungeCracked));
                    SweepHit(p);
                    if (timer <= 0f) { act = Act.Recover; timer = 0.35f; }
                    break;
                case Act.ChargeTell:
                    // it catches fire where it stands
                    vel.x = 0f;
                    anim?.Play(animIgnite != null ? animIgnite : animCharge != null ? animCharge : Worn(animLunge, animLungeCracked));
                    Aflame(true);
                    if (timer % 0.1f < dt) Fx.Sparks(Chest + Random.insideUnitCircle * 1.1f, Gold, 2, 3f);
                    if (timer <= 0f)
                    {
                        act = Act.Charge; timer = 4f;
                        size = new Vector2(size.x, chargeHeight);
                        Sfx.Play("brute", pos, 1f, 0.8f);
                    }
                    break;
                case Act.Charge:
                    vel.x = facing * chargeSpeed * (Cracked ? 1.15f : 1f);
                    anim?.Play(animCharge != null ? animCharge : Worn(animLunge, animLungeCracked));
                    if (timer % 0.08f < dt)
                    {
                        // sparks and dust streaming off behind it
                        Fx.Dust(pos, 1);
                        Fx.Sparks(new Vector2(pos.x - facing * HalfW, pos.y + chargeHeight * 0.5f) + Random.insideUnitCircle * 0.6f, Gold, 3, 3f);
                    }
                    if (timer <= 0f) { size = new Vector2(size.x, fullHeight); Aflame(false); act = Act.Recover; timer = 0.4f; }
                    break;
                case Act.Stuck:
                    vel.x = 0f;
                    anim?.Play(animOpen);
                    if (timer <= 0f) EndShellMove();
                    break;
                case Act.Leap:
                    anim?.Play(Worn(animWindup, animWindupCracked));
                    break;
                case Act.Recover:
                    vel.x = 0f;
                    anim?.Play(Worn(animIdle, animIdleCracked));
                    if (timer <= 0f) EndShellMove();
                    break;
                case Act.Shatter:
                    // on its knees, the light pushing out through every crack - then the stone bursts off it
                    vel.x = 0f;
                    anim?.Play(animOpen);
                    markT -= dt;
                    if (markT <= 0f)
                    {
                        markT = Mathf.Lerp(0.05f, 0.22f, Mathf.Clamp01(timer / shatterTime));
                        Fx.Sparks(Chest + Random.insideUnitCircle * 0.9f, Gold, 5, 6f);
                        Fx.Debris(Chest + Random.insideUnitCircle * 1.2f, Stone, 3);
                        LevelLighting.Flash(Chest, Gold, 5f, 1.1f, 0.15f);
                        Fx.Shake(0.12f);
                    }
                    if (timer <= 0f && grounded) { BecomeCore(); return; }
                    break;
            }
            var b = StepPhysics(dt, gravity, 18f, true);        // (it falls through ledges: they are no floor to it)
            if (act == Act.Charge && (b.hitLeft || b.hitRight))
            {
                // the fire goes out against the wall: the horns go into it and stay there
                size = new Vector2(size.x, fullHeight);
                Aflame(false);
                Fx.Sparks(new Vector2(pos.x + facing * HalfW, pos.y + 1.4f), Gold, 20, 8f);
                act = Act.Stuck; timer = stuckTime * (Cracked ? 0.8f : 1f);
                vel.x = 0f;
                Stucks++;
                Fx.Shake(0.55f);
                Fx.Debris(new Vector2(pos.x + facing * (HalfW + 0.3f), pos.y + 1.4f), new Color(0.6f, 0.6f, 0.7f), 14);
                Sfx.Play("thud", pos, 1f, 0.7f);
            }
            if (b.landed && act == Act.Leap)
            {
                Leaps++;
                vel = Vector2.zero;
                Wave(shockSpeed * (Cracked ? 1.2f : 1f));
                Fx.Shake(0.5f);
                Sfx.Play("thud", pos, 1f, 0.6f);
                act = Act.Recover; timer = 0.5f;
            }
        }

        void EndShellMove()
        {
            act = Act.Idle;
            timer = Rest(actionGap);
        }

        void ChooseShell(PlayerController p)
        {
            int move;
            if (forced >= 0 && forced <= MoveLeap) { move = forced; forced = -1; }
            else
            {
                move = ShellMoves[NextMove(ShellMoves)];
                // the arm does not reach across the hall: from far off it comes over instead
                if (move == MoveSweep && Mathf.Abs(p.pos.x - pos.x) > HalfW + sweepReach) move = MoveLeap;
            }
            switch (move)
            {
                case MoveSweep:
                    act = Act.SweepTell; timer = sweepTell * (Cracked ? 0.8f : 1f);
                    Tell(); Sfx.Play("toad", pos, 1f, 0.6f);
                    break;
                case MoveCharge:
                    act = Act.ChargeTell; timer = chargeTell;
                    Tell(); Sfx.Play("toad", pos, 1f, 0.45f);
                    break;
                default:
                {
                    float air = 2f * leapVelocity / Mathf.Max(1f, gravity);
                    vel = new Vector2(Mathf.Clamp(p.pos.x - pos.x, -leapReach, leapReach) / air, leapVelocity);
                    grounded = false;
                    act = Act.Leap;
                    Sfx.Play("brute", pos, 1f, 1.1f);
                    break;
                }
            }
        }

        Rect SweepRect => facing > 0 ? new Rect(pos.x, pos.y, HalfW + sweepReach, sweepHeight)
                                     : new Rect(pos.x - HalfW - sweepReach, pos.y, HalfW + sweepReach, sweepHeight);

        void Strike()
        {
            act = Act.Sweep; timer = 0.2f; swept = false;
            Sweeps++;
            var r = SweepRect;
            // (a jet of fire straight out of the slit in its chest: the picture's left end is the slit, its bottom the floor)
            Fx.PlayAnim(slashFx, new Vector2(r.center.x, pos.y + sweepHeight * 0.5f), 1f, facing < 0);
            Fx.Sparks(Chest, Gold, 12, 6f);
            LevelLighting.Flash(Chest, Gold, 4f, 1.1f, 0.15f);
            Fx.Shake(0.25f);
            Sfx.Play("whip", pos, 1f, 0.5f);
        }

        void SweepHit(PlayerController p)
        {
            if (swept || p == null || !SweepRect.Overlaps(p.Bounds)) return;
            swept = p.TakeDamage(new DamageInfo(sweepDamage, DamageType.Contact, this, new Vector2(facing * 9f, 6f), displayName + "'s fire"));
        }

        /// <summary>A wall of light along the floor to either side of it: out of its chest, one each way.</summary>
        void Wave(float speed)
        {
            // (they start in the middle of it and hurt nobody until they are clear of its body: where they used to start)
            float y = floorY + 0.1f, clear = (HalfW + 0.2f) / Mathf.Max(0.1f, speed);
            for (int side = -1; side <= 1; side += 2)
            {
                var shot = FireShot(new Vector2(pos.x + side * 0.3f, y), new Vector2(side * speed, 0f), waveId);
                if (shot != null) shot.harmless = clear;
            }
            Fx.Dust(new Vector2(pos.x, floorY), 12);
            Fx.Sparks(Chest, Gold, 18, 7f);
            LevelLighting.Flash(Chest, Gold, 5f, 1.2f, 0.2f);
        }

        // ================================================================== the shell breaks

        /// <summary>The shell has no health left: it does not die - what is inside comes out.</summary>
        protected override void Die(in DamageInfo d)
        {
            if (form != 1) { base.Die(d); return; }
            health = 1;
            if (act == Act.Shatter) return;
            size = new Vector2(size.x, fullHeight);
            Aflame(false);
            act = Act.Shatter; timer = shatterTime; markT = 0f;
            vel.x = 0f;
            forced = -1;
            Tell();
            Fx.Shake(0.6f);
            Sfx.Play("crack", pos);
            Sfx.Play("brute", pos, 1f, 0.5f);
        }

        /// <summary>Burning (the charge): its picture is drawn at full brightness whatever the light of the hall, and back.</summary>
        void Aflame(bool on)
        {
            if (sr == null || aflame == on) return;
            aflame = on;
            if (on) shellMaterial = sr.sharedMaterial;
            var glow = LevelLighting.Instance != null ? LevelLighting.Instance.glowMaterial : null;
            if (on ? glow != null : shellMaterial != null) sr.sharedMaterial = on ? glow : shellMaterial;
        }
        bool aflame;
        Material shellMaterial;

        void BecomeCore()
        {
            // the stone bursts off it
            for (int i = 0; i < 6; i++) Fx.Debris(Chest + Random.insideUnitCircle * 1.6f, Stone, 10);
            Fx.Sparks(Chest, Gold, 30, 9f);
            Fx.Shake(0.9f);
            LevelLighting.Flash(Chest, Gold, 14f, 2.2f, 0.8f);
            Sfx.Play("explosion", pos, 0.9f, 0.7f);
            Sfx.Play("wraith", pos, 0.9f, 0.7f);
            Fx.Text(new Vector2(pos.x, pos.y + coreSize.y + 1f), "ECLIPSE", Gold, 1.8f);

            form = 2;
            eclipse = true;
            maxHealth = coreHealth; health = coreHealth;
            size = coreSize;
            vel = Vector2.zero; grounded = false;
            chained = false;
            if (sr != null && LevelLighting.Instance != null && LevelLighting.Instance.glowMaterial != null) sr.sharedMaterial = LevelLighting.Instance.glowMaterial;
            LevelLighting.MakeLight(transform, "CoreLight", UnityEngine.Rendering.Universal.Light2D.LightType.Point, Gold, 1.1f, 1.5f, 8f).transform.localPosition = new Vector3(0f, coreSize.y * 0.6f, 0f);
            anim?.Play(animCore);
            for (int i = 0; i < 2; i++)
            {
                hand[i] = handPrev[i] = HandRest(i);
                if (handOpen == null) continue;
                var go = new GameObject("SunHand" + i);
                var h = go.AddComponent<SpriteRenderer>();
                h.sprite = handOpen;
                h.flipX = i == 0;
                if (sr != null) { h.sortingLayerID = sr.sortingLayerID; h.sortingOrder = sr.sortingOrder + 1; h.sharedMaterial = sr.sharedMaterial; }
                go.transform.position = hand[i];
                handSr[i] = h;
            }
            act = Act.Rise;
        }

        // ================================================================== the core

        Vector2 HandRest(int i) => new Vector2(pos.x + (i == 0 ? -1f : 1f) * (HalfW + 1.5f), pos.y + size.y * 0.42f);
        Rect HandRect(int i) => new Rect(hand[i].x - handSize.x * 0.5f, hand[i].y, handSize.x, handSize.y);

        /// <summary>Moves a hand towards a point; true when it is there.</summary>
        bool MoveHand(int i, Vector2 to, float speed, float dt)
        {
            hand[i] = Vector2.MoveTowards(hand[i], to, speed * dt);
            return (hand[i] - to).sqrMagnitude < 0.0004f;
        }

        void HandsHome(float dt) { for (int i = 0; i < 2; i++) MoveHand(i, HandRest(i), 16f, dt); }

        void HandLook(int i, bool fist) { if (handSr[i] != null) handSr[i].sprite = fist && handFist != null ? handFist : handOpen; }

        /// <summary>A hand that is coming down / along the floor hurts whoever it meets - once a move.</summary>
        void HandHurts(PlayerController p, int damage, Vector2 push)
        {
            if (handHit || p == null) return;
            for (int i = 0; i < 2; i++)
                if (handLive[i] && HandRect(i).Overlaps(p.Bounds))
                {
                    handHit = p.TakeDamage(new DamageInfo(damage, DamageType.Contact, this, push, displayName + "'s hand"));
                    return;
                }
        }

        void TickCore(float dt, PlayerController p)
        {
            handPrev[0] = hand[0]; handPrev[1] = hand[1];
            vel = Vector2.zero;
            if (p != null && act != Act.BlinkOut && Mathf.Abs(p.pos.x - pos.x) > 0.4f) facing = p.pos.x > pos.x ? 1 : -1;
            switch (act)
            {
                case Act.Rise:
                    anim?.Play(animCore);
                    HandsHome(dt);
                    pos.y = Mathf.MoveTowards(pos.y, HoverY, riseSpeed * dt);
                    if (Mathf.Abs(pos.y - HoverY) < 0.01f) { act = Act.Idle; timer = chained ? 0.3f : Rest(actionGap) * 0.7f; }
                    break;
                case Act.Idle:
                    anim?.Play(animCore);
                    HandsHome(dt);
                    pos.y = Mathf.MoveTowards(pos.y, HoverY, riseSpeed * dt);
                    // it drifts after the player: no end of the hall is out of its way
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 3f)
                        pos.x = Mathf.Clamp(Mathf.MoveTowards(pos.x, p.pos.x, driftSpeed * dt), leftX + HalfW + 0.2f, rightX - HalfW - 0.2f);
                    if (p != null && timer <= 0f) ChooseCore(p);
                    break;
                case Act.Cast:
                {
                    anim?.Play(animCoreCast);
                    HandsHome(dt);
                    bool beamBusy = beamPhase != 0;
                    // the embers come after the light has crossed the hall - below half its health, while it is still crossing
                    if (!embersStarted && (HealthFraction <= 0.5f ? beamPhase == 2 : !beamBusy)) { embersStarted = true; embersLeft = embers; emberT = 0.25f; }
                    if (embersLeft > 0)
                    {
                        emberT -= dt;
                        if (emberT <= 0f) { DropEmber(p); embersLeft--; emberT = emberGap; }
                    }
                    if (embersStarted && embersLeft == 0 && !beamBusy) EndCoreMove();
                    break;
                }
                case Act.SlamTell:
                {
                    anim?.Play(animCoreCast);
                    MoveHand(1 - slamHand, HandRest(1 - slamHand), 16f, dt);
                    HandLook(slamHand, true);
                    // the hand hangs over the player, follows them - and stands still for the last moment
                    float top = Mathf.Min(floorY + 5.5f, ceilY - handSize.y - 0.3f);
                    capT -= dt;
                    if (p != null && timer > slamHang)
                    {
                        var over = new Vector2(Mathf.Clamp(p.pos.x, leftX + handSize.x * 0.5f, rightX - handSize.x * 0.5f), top);
                        bool there = MoveHand(slamHand, over, 20f, dt);
                        if (!there && capT > 0f) timer = Mathf.Max(timer, slamHang + dt);        // not over them yet
                    }
                    markT -= dt;
                    if (markT <= 0f) { markT = 0.12f; Fx.Sparks(new Vector2(hand[slamHand].x, floorY + 0.15f), Gold, 3, 2f); }      // where it will land
                    if (timer <= 0f) { act = Act.Slam; handLive[slamHand] = true; handHit = false; Sfx.Play("throw", pos, 1f, 0.5f); }
                    break;
                }
                case Act.Slam:
                {
                    anim?.Play(animCoreCast);
                    MoveHand(1 - slamHand, HandRest(1 - slamHand), 16f, dt);
                    bool down = MoveHand(slamHand, new Vector2(hand[slamHand].x, floorY), slamSpeed, dt);
                    HandHurts(p, slamDamage, new Vector2((p != null && p.pos.x < hand[slamHand].x ? -1f : 1f) * 7f, 6f));
                    if (down)
                    {
                        handLive[slamHand] = false;
                        Slams++;
                        // a ripple off it along the floor, low: this one a jump clears
                        FireShot(new Vector2(hand[slamHand].x - handSize.x * 0.5f - 0.2f, floorY + 0.2f), new Vector2(-shockSpeed, 0f), "shockwave", Gold);
                        FireShot(new Vector2(hand[slamHand].x + handSize.x * 0.5f + 0.2f, floorY + 0.2f), new Vector2(shockSpeed, 0f), "shockwave", Gold);
                        Fx.Dust(hand[slamHand], 10);
                        Fx.Shake(0.55f);
                        Sfx.Play("thud", hand[slamHand], 1f, 0.6f);
                        EndCoreMove();
                    }
                    break;
                }
                case Act.ClapTell:
                {
                    anim?.Play(animCoreCast);
                    HandLook(0, false); HandLook(1, false);
                    // the hands go out to the two walls, down on the floor
                    capT -= dt;
                    bool a = MoveHand(0, new Vector2(leftX + handSize.x * 0.5f, floorY), 24f, dt);
                    bool b = MoveHand(1, new Vector2(rightX - handSize.x * 0.5f, floorY), 24f, dt);
                    if (!(a && b) && capT > 0f) timer = Mathf.Max(timer, clapTell);       // the warning runs once both are in place
                    if (timer <= 0f) { act = Act.Clap; handLive[0] = handLive[1] = true; handHit = false; Sfx.Play("brute", pos, 1f, 1.4f); }
                    break;
                }
                case Act.Clap:
                {
                    anim?.Play(animCoreCast);
                    // ... and come together under it along the floor (jump them)
                    float sp = clapSpeed * (HealthFraction <= 0.5f ? 1.2f : 1f);
                    MoveHand(0, new Vector2(pos.x - handSize.x * 0.5f, floorY), sp, dt);
                    MoveHand(1, new Vector2(pos.x + handSize.x * 0.5f, floorY), sp, dt);
                    if (timer % 0.1f < dt) { Fx.Dust(hand[0], 1); Fx.Dust(hand[1], 1); }
                    HandHurts(p, clapDamage, new Vector2(0f, 9f));
                    if (hand[1].x - hand[0].x <= handSize.x + 0.05f)
                    {
                        handLive[0] = handLive[1] = false;
                        Claps++;
                        var at = new Vector2(pos.x, floorY + handSize.y * 0.5f);
                        FanAt(at, at + Vector2.up, 5, 26f, 7f, emberId);
                        LevelLighting.Flash(at, Gold, 8f, 1.6f, 0.4f);
                        Fx.Sparks(at, Gold, 18, 7f);
                        Fx.Shake(0.5f);
                        Sfx.Play("thud", at, 1f, 1.3f);
                        EndCoreMove();
                    }
                    break;
                }
                case Act.NovaTell:
                    anim?.Play(animCoreCast);
                    HandsHome(dt);
                    // it sinks to the floor, gathering
                    pos.y = Mathf.MoveTowards(pos.y, floorY + 0.3f, 9f * dt);
                    markT -= dt;
                    if (markT <= 0f) { markT = 0.12f; Fx.Sparks(Chest, Gold, 4, 6f); LevelLighting.Flash(Chest, Gold, 5f, 1.1f, 0.15f); }
                    if (timer <= 0f)
                    {
                        Novas++;
                        Wave(shockSpeed * 1.25f);
                        FanAt(Chest, Chest + Vector2.up, 5, 30f, 7f, emberId);
                        LevelLighting.Flash(Chest, Gold, 10f, 1.8f, 0.5f);
                        Fx.Shake(0.6f);
                        Sfx.Play("thud", pos, 1f, 1.4f);
                        act = Act.Open; timer = openTime * 0.75f;        // (it is on the floor already: the opening, whatever its health)
                    }
                    break;
                case Act.BlinkOut:
                    // gone; a flicker of the stone shows where it will be
                    markT -= dt;
                    if (markT <= 0f) { markT = 0.1f; Fx.Sparks(Chest, Gold, 3, 2.5f); LevelLighting.Flash(Chest, Gold, 3.5f, 1.2f, 0.14f); }
                    if (timer <= 0f)
                    {
                        Show(true);
                        hand[0] = handPrev[0] = HandRest(0); hand[1] = handPrev[1] = HandRest(1);
                        Fx.Sparks(Chest, Gold, 16, 5f);
                        Sfx.Play("wraith", pos, 0.8f, 1.5f);
                        BeginSlam(p, slamTell * 0.7f);          // it comes back with a hand already raised
                    }
                    break;
                case Act.Lower:
                    anim?.Play(animCoreOpen);
                    HandsHome(dt);
                    pos.y = Mathf.MoveTowards(pos.y, floorY + 0.3f, 9f * dt);
                    if (pos.y <= floorY + 0.31f) { act = Act.Open; timer = openTime; }
                    break;
                case Act.Open:
                    anim?.Play(animCoreOpen);
                    HandsHome(dt);
                    if (timer <= 0f) act = Act.Rise;
                    break;
            }
        }

        /// <summary>A move is over: it sinks to the floor, chest unguarded - unless (below half) the next one follows at once.</summary>
        void EndCoreMove()
        {
            HandLook(0, false); HandLook(1, false);
            if (HealthFraction <= 0.5f && !chained && forced < 0 && Random.value < chainChance) { chained = true; act = Act.Rise; return; }
            chained = false;
            act = Act.Lower;
        }

        void ChooseCore(PlayerController p)
        {
            int move;
            if (forced >= MoveBeam) { move = forced; forced = -1; }
            else { forced = -1; move = CoreMoves[NextMove(CoreMoves)]; }
            switch (move)
            {
                case MoveBeam:
                    act = Act.Cast;
                    embersStarted = false; embersLeft = 0;
                    Tell();
                    StartBeam(p);
                    break;
                case MoveSlam:
                    Tell();
                    BeginSlam(p, slamTell);
                    break;
                case MoveClap:
                    act = Act.ClapTell; timer = clapTell; capT = 2.2f;
                    Tell(); Sfx.Play("toad", pos, 1f, 0.5f);
                    break;
                case MoveNova:
                    act = Act.NovaTell; timer = novaTell; markT = 0f;
                    Tell(); Sfx.Play("toad", pos, 1f, 0.35f);
                    break;
                default:
                {
                    Blinks++;
                    Fx.Sparks(Chest, Gold, 16, 5f);
                    Sfx.Play("wraith", pos, 0.8f, 1.2f);
                    // it will be over the player, a little to the side they are not looking
                    float x = Mathf.Clamp(p.pos.x - p.facing * 2.5f, leftX + HalfW + 0.2f, rightX - HalfW - 0.2f);
                    pos = prevPos = new Vector2(x, HoverY);
                    Show(false);
                    act = Act.BlinkOut; timer = blinkTime; markT = 0f;
                    break;
                }
            }
        }

        void BeginSlam(PlayerController p, float tell)
        {
            // the hand on the player's side
            slamHand = p != null && p.pos.x < pos.x ? 0 : 1;
            act = Act.SlamTell; timer = tell; capT = 1.4f; markT = 0f;
            Sfx.Play("toad", pos, 1f, 0.7f);
        }

        void Show(bool on)
        {
            if (sr != null) sr.enabled = on;
            for (int i = 0; i < 2; i++) if (handSr[i] != null) handSr[i].enabled = on;
        }

        void DropEmber(PlayerController p)
        {
            float x = Mathf.Clamp((p != null ? p.pos.x : pos.x) + Random.Range(-2.5f, 2.5f), leftX + 0.5f, rightX - 0.5f);
            var s = FireShot(new Vector2(x, ceilY - 0.8f), new Vector2(0f, -emberSpeed * (HealthFraction <= 0.5f ? 1.15f : 1f)), emberId);
            if (s != null) s.burstColor = Gold;
            Fx.Sparks(new Vector2(x, floorY + 0.15f), Gold, 5, 2f);        // where it will land
        }

        // ------------------------------------------------------------------ the column of light

        void StartBeam(PlayerController p)
        {
            // it starts at the wall the player is farther from
            float mid = (leftX + rightX) * 0.5f;
            beamDir = p == null || p.pos.x < mid ? -1 : 1;
            beamX = beamDir < 0 ? rightX - beamWidth * 0.5f : leftX + beamWidth * 0.5f;
            beamPhase = 1; beamT = beamTell;
            Beams++;
            if (beamSr == null && beamSprite != null)
            {
                var go = new GameObject("SunBeam");
                beamSr = go.AddComponent<SpriteRenderer>();
                beamSr.sprite = beamSprite;
                if (sr != null) { beamSr.sortingLayerID = sr.sortingLayerID; beamSr.sortingOrder = sr.sortingOrder - 1; }
                if (LevelLighting.Instance != null && LevelLighting.Instance.glowMaterial != null) beamSr.sharedMaterial = LevelLighting.Instance.glowMaterial;
                LevelLighting.MakeLight(go.transform, "Light", UnityEngine.Rendering.Universal.Light2D.LightType.Point, Gold, 1.1f, 1f, 5.5f);
            }
            Fx.Text(new Vector2(beamX, floorY + 2.2f), "!", Gold, beamTell + 0.2f);
            Sfx.Play("wraith", pos, 0.7f, 1.7f);
        }

        /// <summary>
        /// Where the light stops in this column: on the first ledge or stone under the ceiling - wherever that is.
        /// A hole blown in the floor is no shelter: the light goes down to the bottom of it.
        /// </summary>
        public float BeamBottom(float x)
        {
            var g = World.Grid;
            int cx = Mathf.FloorToInt(x);
            for (int y = Mathf.FloorToInt(ceilY) - 1; y >= 0; y--)
                if (g.IsSolid(cx, y) || g.Get(cx, y) == TileType.Platform) return y + 1;
            return 0f;
        }

        void TickBeam(float dt, PlayerController p)
        {
            if (beamPhase == 0) return;
            float bottom = BeamBottom(beamX), width = beamWidth;
            if (beamPhase == 1)
            {
                beamT -= dt;
                width = beamWidth * 0.3f;
                if (beamT <= 0f) { beamPhase = 2; Sfx.Play("throw", pos, 1f, 0.4f); }
            }
            else
            {
                beamX += beamDir * beamSpeed * (HealthFraction <= 0.5f ? 1.2f : 1f) * dt;
                if (p != null && new Rect(beamX - beamWidth * 0.5f, bottom, beamWidth, ceilY - bottom).Overlaps(p.Bounds))
                    p.TakeDamage(new DamageInfo(beamDamage, DamageType.Contact, this, new Vector2(beamDir * 6f, 5f), "the Sunstone's light"));
                if (beamX < leftX - 0.2f || beamX > rightX + 0.2f) { beamPhase = 0; if (beamSr != null) beamSr.enabled = false; return; }
            }
            if (beamSr == null) return;
            beamSr.enabled = true;
            var bs = beamSr.sprite.bounds.size;
            float h = Mathf.Max(0.1f, ceilY - bottom);
            beamSr.transform.position = new Vector3(beamX, bottom + h * 0.5f, 0f);
            beamSr.transform.localScale = new Vector3(width / Mathf.Max(0.01f, bs.x), h / Mathf.Max(0.01f, bs.y), 1f);
            beamSr.color = beamPhase == 1 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        // ------------------------------------------------------------------ light, pictures, being hit

        /// <summary>The hall's own light goes down once the shell is gone, and comes back when the core is put out.</summary>
        void Dim(float dt)
        {
            float want = eclipse && !Dead ? eclipseLight : 1f;
            if (Mathf.Approximately(light, want)) return;
            light = Mathf.MoveTowards(light, want, dt * 0.6f);
            lightTouched = true;
            LevelLighting.SetAmbientScale(light);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (form != 2) return;
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            for (int i = 0; i < 2; i++)
                if (handSr[i] != null) handSr[i].transform.position = Vector2.Lerp(handPrev[i], hand[i], alpha);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (act == Act.BlinkOut || act == Act.Shatter) return false;        // it is not there / it is coming apart
            bool own = d.type == DamageType.Whip || d.type == DamageType.Stomp || d.type == DamageType.Thrown;
            if (own && Exposed && !Dead)
            {
                Fx.Sparks(Chest, Gold, 10, 5f);
                return base.TakeDamage(new DamageInfo(d.amount * 2, d.type, d.source, d.knockback, d.cause));
            }
            return base.TakeDamage(d);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (beamSr != null) Destroy(beamSr.gameObject);
            for (int i = 0; i < 2; i++) if (handSr[i] != null) Destroy(handSr[i].gameObject);
            if (sr != null) sr.enabled = true;
            if (lightTouched) LevelLighting.SetAmbientScale(1f);
        }
    }
}
