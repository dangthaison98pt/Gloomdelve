using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Mossroot boss: a giant spider, and a fight about the ceiling. Her moves come in random order (never the same twice running), none shared with the
    /// other bosses:
    ///  - Drop: she runs up to the ceiling, crawls along it until she is above the player (grit trickles down
    ///    under her: watch it), stops for a blink and lets herself fall. Step aside: she lands stuck on her back
    ///    legs for a moment - the opening. Cocoons hang across her den: falling through one tears it open, and
    ///    the spiderling inside drops out - so where the player stands when she lets go matters.
    ///  - Web: she lays a sheet of web on the floor where the player stands. Walking through it is slow (jumping
    ///    is not), and it stays for a while, so the room gets smaller.
    ///  - Eggs: she leaves egg sacs on the floor. Cut them before they hatch, or there are spiderlings to deal with.
    ///  - Venom: she goes up to the ceiling again, but this time she stays there and spits single globs of venom
    ///    down at the player, one after another (keep moving), then lowers herself to the floor - no drop attack.
    /// She only hurts while she is falling. Stomped too often she bites upward.
    /// </summary>
    public class Broodmother : Boss
    {
        public float restTime = 1.5f;
        public float tellTime = 0.5f;
        [Header("Drop from the ceiling")]
        public float climbSpeed = 11f;
        public float crawlSpeed = 7.5f;
        [Tooltip("The longest she crawls along the ceiling after the player: she lets go sooner, as soon as she is above them.")] public float ceilingTime = 3f;
        [Tooltip("She stops this long before letting go: the last warning.")] public float aimTime = 0.4f;
        public float dropSpeed = 30f;
        [Tooltip("Speed of the two shockwaves that run along the floor from where she lands. Jump them.")] public float shockSpeed = 8.5f;
        [Tooltip("Stuck on the floor after a drop: the opening.")] public float stuckTime = 1.4f;
        [Header("Web")]
        public string webId = "webpatch";
        [Header("Eggs")]
        public string eggId = "eggsac";
        public int eggs = 2;
        [Tooltip("With this many spiderlings and eggs around she lays web instead.")] public int maxBrood = 3;
        [Header("Venom from the ceiling")]
        public int spits = 3;
        public float spitInterval = 0.6f;
        public float spitSpeed = 8f;
        public string spitId = "venomshot";
        [Tooltip("Speed at which she lowers herself back to the floor afterwards (harmless).")] public float lowerSpeed = 7f;
        public SpriteAnim animIdle;
        public SpriteAnim animJump;
        [Tooltip("Falling picture: legs trailing above her.")] public SpriteAnim animFall;

        static readonly Color Venom = new Color(0.6f, 1f, 0.45f);
        enum State { Ground, Tell, Climb, Ceiling, Drop, Stuck, Lower }
        enum Move { None, Drop, Web, Eggs, Spit }
        State state;
        Move pending;
        float timer, dripT, spitT;
        int step, spitsLeft;
        bool flipped, spitting, aiming;

        /// <summary>Only the fall from the ceiling hurts.</summary>
        public override bool Attacking => state == State.Drop;
        public bool OnCeiling => state == State.Ceiling;
        public bool Stuck => state == State.Stuck;
        /// <summary>Up on the ceiling for the venom move (no drop will follow).</summary>
        public bool Spitting => state == State.Ceiling && spitting;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Ground; timer = 1.6f;
            anim?.Play(animIdle);
        }

        float FloorY(float x)
        {
            var g = World.Grid;
            int y = Mathf.FloorToInt(pos.y + 0.5f);
            while (y > 0 && !g.IsSolid(Mathf.FloorToInt(x), y - 1)) y--;
            return y;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (HoldCounterPose(dt, state != State.Ceiling && state != State.Climb)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            switch (state)
            {
                case State.Ground:
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                    anim?.Play(animIdle);
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f && p != null && grounded)
                    {
                        // her moves, in random order: drop (twice as often), web, venom from the ceiling, eggs
                        int k = NextMove(0, 1, 2, 0, 3);
                        pending = k == 1 ? Move.Web : k == 2 ? Move.Spit : k == 4 ? Move.Eggs : Move.Drop;
                        if (pending == Move.Eggs && CountAlive<Spider>() + CountAlive<EggSac>() >= maxBrood) pending = Move.Web;
                        state = State.Tell; timer = tellTime;
                        Tell();
                        Sfx.Play("bat", pos, 0.8f, 0.5f);
                    }
                    StepPhysics(dt, gravity);
                    break;
                case State.Tell:
                    vel.x = 0f;
                    StepPhysics(dt, gravity);
                    if (timer <= 0f)
                    {
                        if (pending == Move.Drop || pending == Move.Spit)
                        {
                            spitting = pending == Move.Spit;
                            state = State.Climb; grounded = false; anim?.Play(animJump); Sfx.Play("toad", pos, 1f, 0.6f);
                        }
                        else
                        {
                            if (pending == Move.Web) LayWeb(p); else LayEggs(p);
                            state = State.Ground; timer = Rest(restTime);
                        }
                        pending = Move.None;
                    }
                    break;
                case State.Climb:
                {
                    // straight up to the ceiling, no gravity
                    vel = new Vector2(0f, climbSpeed);
                    var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
                    BodyPhysics.Move(World.Grid, ref b, dt, true);
                    pos = b.pos;
                    if (b.hitCeiling || pos.y > World.Grid.Height)
                    {
                        state = State.Ceiling; vel = Vector2.zero;
                        anim?.Play(animIdle);              // upside down, legs on the rock
                        if (spitting) { spitsLeft = spits; spitT = spitInterval; timer = spits * spitInterval + 0.5f; }
                        else { timer = ceilingTime; aiming = false; }
                    }
                    break;
                }
                case State.Ceiling:
                {
                    if (spitting)
                    {
                        // stays up and spits single globs at the player; a "!" before each
                        vel = Vector2.zero;
                        if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.3f) facing = p.pos.x > pos.x ? 1 : -1;
                        spitT -= dt;
                        if (spitsLeft > 0 && spitT <= 0.25f && spitT + dt > 0.25f) Tell();
                        if (spitsLeft > 0 && spitT <= 0f && p != null)
                        {
                            Vector2 from = new Vector2(pos.x, pos.y - 0.1f);
                            Vector2 to = p.Center - from;
                            FireShot(from, (to.sqrMagnitude > 0.001f ? to.normalized : Vector2.down) * spitSpeed, spitId);
                            Sfx.Play("throw", pos, 1f, 0.6f);
                            spitsLeft--; spitT = spitInterval;
                        }
                        if (timer <= 0f) { spitting = false; state = State.Lower; anim?.Play(animFall); }
                        break;
                    }
                    // crawls after the player, then holds still for the last moment before the fall
                    // (she crawls until she is right above them - or gives up after ceilingTime - and only then takes aim)
                    if (!aiming && (timer <= 0f || p == null || Mathf.Abs(p.pos.x - pos.x) < 0.45f)) { aiming = true; timer = aimTime; }
                    float want = !aiming && p != null ? Mathf.Clamp((p.pos.x - pos.x) * 4f, -crawlSpeed, crawlSpeed) : 0f;
                    vel = new Vector2(want, 0f);
                    var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
                    BodyPhysics.Move(World.Grid, ref b, dt, true);
                    pos = b.pos;
                    if (Mathf.Abs(want) > 0.3f) facing = want > 0 ? 1 : -1;
                    dripT -= dt;
                    if (dripT <= 0f) { dripT = 0.12f; Fx.Dust(new Vector2(pos.x, pos.y - 0.1f), 1); }      // grit falls where she is
                    if (aiming && timer <= 0f) { state = State.Drop; vel = new Vector2(0f, -dropSpeed); anim?.Play(animFall); Sfx.Play("toad", pos, 1f, 0.45f); }
                    break;
                }
                case State.Drop:
                {
                    vel = new Vector2(0f, -dropSpeed);
                    var b = StepPhysics(dt, 0f, dropSpeed);
                    TearCocoons();
                    if (b.landed || grounded) Landed();
                    break;
                }
                case State.Lower:
                {
                    // down on her thread, slowly: touching her now does nothing
                    vel = new Vector2(0f, -lowerSpeed);
                    var b = StepPhysics(dt, 0f, lowerSpeed);
                    if (b.landed || grounded) { state = State.Ground; timer = Rest(restTime); anim?.Play(animIdle); Fx.Dust(pos, 4); }
                    break;
                }
                case State.Stuck:
                    vel.x = 0f;
                    anim?.Play(animIdle);
                    StepPhysics(dt, gravity);
                    if (timer <= 0f) { state = State.Ground; timer = Rest(restTime * 0.5f); }
                    break;
            }
        }

        /// <summary>Falling through a cocoon tears it open: its spiderling drops out.</summary>
        void TearCocoons()
        {
            Cocoon hit = null;
            foreach (var e in World.Entities)
                if (e is Cocoon c && !c.Dead && c.Touches(pos, size.x * 0.5f, size.y)) { hit = c; break; }
            if (hit != null) hit.Burst();
        }

        void Landed()
        {
            state = State.Stuck; timer = stuckTime;
            Fx.Shake(0.5f);
            Fx.Dust(pos, 10);
            Shockwave(shockSpeed, new Color(0.75f, 0.95f, 0.5f));      // the landing sends a wave along the floor to both sides
            Sfx.Play("thud", pos, 1f, 0.7f);
            Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.5f), "STUCK", new Color(1f, 0.9f, 0.5f), stuckTime * 0.8f);
        }

        /// <summary>A sheet of web on the floor under the player.</summary>
        void LayWeb(PlayerController p)
        {
            if (p == null) return;
            float x = Mathf.Clamp(p.pos.x, 2.5f, World.Grid.Width - 2.5f);
            World.SpawnById(webId, new Vector2(x, FloorY(x)));
            Fx.Debris(new Vector2(pos.x + facing * 0.6f, pos.y + size.y * 0.6f), Color.white, 8);
            Sfx.Play("throw", pos, 1f, 0.6f);
        }

        /// <summary>Egg sacs left on the floor to both sides of her.</summary>
        void LayEggs(PlayerController p)
        {
            var g = World.Grid;
            int room = maxBrood - CountAlive<Spider>() - CountAlive<EggSac>();
            for (int i = 0; i < eggs && room > 0; i++)
            {
                float x = pos.x + (i % 2 == 0 ? -1 : 1) * (2.2f + i * 0.4f);
                float fy = FloorY(x);
                if (x < 1.5f || x > g.Width - 1.5f || !g.IsEmpty(Mathf.FloorToInt(x), Mathf.FloorToInt(fy + 0.2f))) continue;
                var e = World.SpawnById(eggId, new Vector2(x, fy));
                if (e is Enemy en) en.summoned = true;
                room--;
            }
            Fx.Debris(Center, new Color(0.85f, 0.9f, 0.75f), 8);
            Sfx.Play("toad", pos, 1f, 1.4f);
        }

        // On the ceiling she hangs upside down: the picture is turned over and hung from the top of her body.
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            bool up = state == State.Ceiling;
            if (up != flipped)
            {
                flipped = up;
                sr.flipY = up;
                sr.transform.localPosition = up ? new Vector3(0f, size.y, 0f) : Vector3.zero;
            }
        }

        /// <summary>Stomped again: she rears up and bites straight upward - it hurts and throws the player high.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            anim?.Play(animJump);
            var throwOff = new Vector2(away * 4f, 12f);
            // the bite throws the player by its knockback; when they cannot be hurt right now (just hit) they are
            // thrown off all the same, or they would sink into her and be countered again every step
            if (!p.TakeDamage(new DamageInfo(contactDamage, DamageType.Contact, this, throwOff, displayName + "'s fangs")))
                p.Shove(throwOff, 0.2f);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), Venom, 8);
            Fx.Shake(0.3f);
            Sfx.Play("bat", pos, 1f, 0.5f);
        }
    }
}
