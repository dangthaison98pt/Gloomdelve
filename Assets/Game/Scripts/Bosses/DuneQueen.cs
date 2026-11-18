using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs boss: the Dune Queen, a great sand worm, and a fight about where to stand. The whole floor of
    /// her chamber is quicksand (she sees to that herself when the fight begins) - nobody stands still there for
    /// long, and a bomb dropped on it is swallowed. Three blocks of stone come up out of the sand (SandPillar):
    /// the only firm ground, solid enough to keep her scarabs off - until one of her attacks breaks it. A broken
    /// block comes up again a few seconds later. Her moves come in random order (never the same twice
    /// running), none shared with the other bosses:
    ///  - Breach: she sinks into the sand, her mound runs across the floor to where the player is, the ground
    ///    shakes there (the warning) - and she bursts up out of it. That lunge is what hurts, and it breaks any
    ///    block above her - but she comes up head first, and stone is harder than her head: breaking a block
    ///    that way costs her a point of health. That is the heart of the fight: stand on a block, let the
    ///    ground shake under it, and jump clear at the last moment. Out of the sand she is spent for a moment - the
    ///    opening - and the slam of her body sends a wave along the sand to each side: it breaks the first
    ///    block it runs into.
    ///  - Spit: she rears and spits a fan of sand at the player - a clod that lands on a block breaks it.
    ///  - Brood: she shakes scarabs out of the sand near the player (a skipped call is a spit instead).
    /// Under the sand nothing reaches her. Stomped too often she throws the player off. She goes into the sand
    /// and comes out of it bodily: the part of her below the floor line is not drawn (a sprite mask that ends at
    /// the floor), so she sinks and rises instead of vanishing.
    /// </summary>
    public class DuneQueen : Boss
    {
        public float actionGap = 1.6f;
        [Header("Breach")]
        [Tooltip("Seconds she takes to sink out of sight.")] public float diveTime = 0.7f;
        [Tooltip("Seconds she takes to come up out of the sand when she breaches.")] public float riseTime = 0.16f;
        public float travelSpeed = 9f;
        [Tooltip("The longest her mound runs after the player before she comes up anyway.")] public float travelMax = 2.4f;
        [Tooltip("Seconds the ground shakes where she will come up: the warning.")] public float rumbleTime = 0.75f;
        [Tooltip("Seconds of the lunge: only now she hurts.")] public float breachTime = 0.45f;
        [Tooltip("Seconds she lies spent afterwards: the opening.")] public float exposedTime = 1.7f;
        [Tooltip("Health she loses when she comes up head first into a block of stone.")] public int headbuttDamage = 1;
        public float shockSpeed = 8f;
        [Header("Spit")]
        public float spitTell = 0.55f;
        public int spitShots = 5;
        public float spitSpread = 13f;
        public float spitSpeed = 8.5f;
        public string spitId = "sandshot";
        [Header("Brood")]
        public string minionId = "scarab";
        public int minionsPerCall = 2;
        public int maxMinions = 4;
        public float summonCooldown = 12f;
        public SpriteAnim animIdle;
        public SpriteAnim animRear;
        public SpriteAnim animMound;

        static readonly Color Sand = new Color(0.95f, 0.8f, 0.5f);
        enum State { Idle, Dive, Travel, Rumble, Breach, Exposed, Spit }
        State state;
        float timer, floorY, summonTimer;

        /// <summary>Under the sand: only her mound shows, and nothing reaches her.</summary>
        public bool Hidden => state == State.Travel || state == State.Rumble || (state == State.Dive && timer < diveTime * 0.3f);
        public bool Diving => state == State.Dive;
        public bool Exposed => state == State.Exposed;
        public bool Breaching => state == State.Breach;
        public override bool Attacking => state == State.Breach;
        public override bool IsStompable => stompable && !Hidden;
        public override bool CanBeHitByItems => !Dead && !Hidden;

        // Everything of her below the floor line is hidden: a mask that covers the air above the floor, and her
        // picture drawn only inside it while she is going down or coming up.
        SpriteMask mask;
        static Sprite maskSprite;

        void SetMasked(bool on)
        {
            if (sr == null) return;
            if (on && mask == null)
            {
                if (maskSprite == null)
                    maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                                               new Vector2(0.5f, 0f), Texture2D.whiteTexture.width);
                var go = new GameObject("DuneQueenFloorMask");
                mask = go.AddComponent<SpriteMask>();
                mask.sprite = maskSprite;
                go.transform.localScale = new Vector3(8f, 8f, 1f);
            }
            if (mask != null)
            {
                mask.enabled = on;
                mask.transform.position = new Vector3(pos.x, floorY, 0f);
            }
            sr.maskInteraction = on ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (mask != null) Destroy(mask.gameObject);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            floorY = pos.y;
            state = State.Idle; timer = 1.6f;
            summonTimer = summonCooldown * 0.5f;
            anim?.Play(animIdle);
            if (!Dead) FloodWithSand();
        }

        bool Fits(float x)
        {
            float hw = size.x * 0.5f;
            return x - hw > 1.05f && x + hw < World.Grid.Width - 1.05f;
        }

        /// <summary>Every plain cell of the chamber's floor becomes quicksand (those that already are stay as they are).</summary>
        void FloodWithSand()
        {
            var g = World.Grid;
            int fy = Mathf.FloorToInt(floorY + 0.1f);
            var have = new System.Collections.Generic.HashSet<int>();
            foreach (var e in World.Entities) if (e is Quicksand && !e.Dead && Mathf.Abs(e.pos.y - floorY) < 0.5f) have.Add(Mathf.FloorToInt(e.pos.x));
            for (int x = 1; x < g.Width - 1; x++)
            {
                if (have.Contains(x) || g.IsSolid(x, fy) || !g.IsSolid(x, fy - 1)) continue;
                World.SpawnById("quicksand", new Vector2(x + 0.5f, fy));
            }
        }

        /// <summary>How often she has come up head first into stone.</summary>
        public int Headbutts { get; private set; }

        /// <summary>The blocks standing above her are broken by her coming up - and a block that was really there costs
        /// her: she has rammed it with her head.</summary>
        void BreakSlabs()
        {
            float hw = size.x * 0.5f;
            var hit = new System.Collections.Generic.List<SandPillar>();
            foreach (var e in World.Entities)
                if (e is SandPillar sp && !sp.Dead && sp.Right > pos.x - hw && sp.Left < pos.x + hw) hit.Add(sp);
            bool rammed = false;
            foreach (var sp in hit) if (sp.Shatter()) rammed = true;
            if (!rammed || headbuttDamage <= 0) return;
            Headbutts++;
            Fx.Text(new Vector2(pos.x, floorY + size.y + 0.6f), "CRACK!", new Color(1f, 0.9f, 0.5f), 0.9f);
            Fx.Shake(0.6f);
            // (a blow of its own kind: not a trap's half point, and it lands even though she has only just surfaced)
            hurtTimer = 0f;
            base.TakeDamage(new DamageInfo(headbuttDamage, DamageType.Thrown, null, Vector2.zero, "a block of stone"));
        }

        /// <summary>The nearest spot to x where her whole body can come up out of the sand.</summary>
        float Surface(float x)
        {
            if (Fits(x)) return x;
            for (float d = 0.25f; d < World.Grid.Width; d += 0.25f)
            {
                if (Fits(x + d)) return x + d;
                if (Fits(x - d)) return x - d;
            }
            return pos.x;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            summonTimer -= dt;
            if (HoldCounterPose(dt, false)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            vel = Vector2.zero;
            switch (state)
            {
                case State.Idle:
                    pos.y = floorY;
                    anim?.Play(animIdle);
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f && p != null)
                    {
                        // her moves, in random order: breach (twice as often), spit, brood
                        int k = NextMove(0, 1, 0, 2);
                        if (k == 3 && (summonTimer > 0f || CountAlive<Scarab>() >= maxMinions)) k = 1;
                        if (k == 1) { state = State.Spit; timer = spitTell; Tell(); anim?.Play(animRear); Sfx.Play("brute", pos, 1f, 1.3f); }
                        else if (k == 3) { Brood(p); summonTimer = summonCooldown; timer = Rest(actionGap); }
                        else { state = State.Dive; timer = diveTime; Tell(); Sfx.Play("toad", pos, 1f, 0.5f); Fx.Dust(pos, 8); }
                    }
                    break;
                case State.Dive:
                    // sinks straight down into the sand (slowly at first), sand flying where she goes under
                    SetMasked(true);
                    float sunk = 1f - Mathf.Clamp01(timer / diveTime);
                    pos.y = floorY - size.y * sunk * sunk;
                    if (World.Frame % 3 == 0) Fx.Dust(new Vector2(pos.x + Random.Range(-0.8f, 0.8f), floorY + 0.1f), 1);
                    if (timer <= 0f)
                    {
                        SetMasked(false);
                        Fx.Debris(new Vector2(pos.x, floorY + 0.2f), Sand, 10);
                        state = State.Travel; timer = travelMax; pos.y = floorY; anim?.Play(animMound);
                    }
                    break;
                case State.Travel:
                {
                    // her mound runs along the floor to where the player is
                    anim?.Play(animMound);
                    float want = p != null ? Surface(p.pos.x) : pos.x;
                    pos.x = Mathf.MoveTowards(pos.x, want, travelSpeed * dt);
                    if (World.Frame % 4 == 0) Fx.Dust(new Vector2(pos.x, floorY + 0.1f), 1);
                    if (Mathf.Abs(pos.x - want) < 0.05f || timer <= 0f)
                    {
                        pos.x = Surface(pos.x);
                        state = State.Rumble; timer = rumbleTime;
                        Tell();
                        Sfx.Play("rumble", pos, 0.8f, 1.2f);
                    }
                    break;
                }
                case State.Rumble:
                    anim?.Play(animMound);
                    if (World.Frame % 3 == 0) Fx.Dust(new Vector2(pos.x + Random.Range(-0.8f, 0.8f), floorY + 0.1f), 1);
                    if (timer <= 0f)
                    {
                        state = State.Breach; timer = breachTime;
                        anim?.Play(animRear);
                        SetMasked(true);
                        pos.y = floorY - size.y;
                        BreakSlabs();
                        Fx.Shake(0.5f);
                        Fx.Debris(new Vector2(pos.x, floorY + 0.3f), Sand, 14);
                        Sfx.Play("brute", pos, 1f, 0.7f);
                    }
                    break;
                case State.Breach:
                    // up out of the sand, fast
                    pos.y = floorY - size.y * (1f - Mathf.Clamp01((breachTime - timer) / riseTime));
                    if (timer <= 0f)
                    {
                        pos.y = floorY;
                        SetMasked(false);
                        state = State.Exposed; timer = exposedTime;
                        anim?.Play(animIdle);
                        Shockwave(shockSpeed, Sand);            // her body comes down on the sand: a wave to each side
                        Fx.Shake(0.4f);
                        Sfx.Play("thud", pos, 1f, 0.7f);
                        Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.5f), "SPENT", new Color(1f, 0.9f, 0.5f), exposedTime * 0.8f);
                    }
                    break;
                case State.Exposed:
                    if (timer <= 0f) { state = State.Idle; timer = Rest(actionGap * 0.5f); }
                    break;
                case State.Spit:
                    if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                    if (timer <= 0f)
                    {
                        if (p != null) FanAt(new Vector2(pos.x + facing * 0.5f, pos.y + size.y * 0.8f), p.Center, spitShots, spitSpread, spitSpeed, spitId);
                        Sfx.Play("throw", pos, 1f, 0.5f);
                        state = State.Idle; timer = Rest(actionGap);
                    }
                    break;
            }
        }

        /// <summary>Starts her dive right now, whatever she was about to do (tests).</summary>
        public void DiveNow() { state = State.Dive; timer = diveTime; Tell(); }

        /// <summary>Calls her brood right now (tests).</summary>
        public void BroodNow(PlayerController p) => Brood(p);

        void Brood(PlayerController p)
        {
            for (int i = 0; i < minionsPerCall && CountAlive<Scarab>() < maxMinions; i++)
            {
                if (!MinionSpot(p, 3, 6, out var spot)) return;
                Fx.Debris(new Vector2(spot.x + 0.5f, spot.y + 0.3f), Sand, 8);
                Scarab.Single = true;
                Entity e;
                try { e = World.SpawnById(minionId, new Vector2(spot.x + 0.5f, spot.y)); }
                finally { Scarab.Single = false; }
                if (e != null) e.facing = p.pos.x > e.pos.x ? 1 : -1;
                if (e is Enemy en) en.summoned = true;
            }
            Sfx.Play("toad", pos, 1f, 1.5f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (Hidden) return false;
            return base.TakeDamage(d);
        }

        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 9f, 10f), 0.35f);
            Fx.Shake(0.4f);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), Sand, 10);
            Sfx.Play("brute", pos, 1f, 0.9f);
        }
    }
}
