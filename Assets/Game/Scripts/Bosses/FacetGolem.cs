using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode boss: the Facet - a colossus of stone and crystal too big for its own hall. Its head and
    /// shoulders fill the back wall from floor to ceiling (the rest of it is somewhere below); it cannot be
    /// reached and it cannot be hurt there. What comes down into the hall are its two fists (FacetFist), and
    /// they are the fight. Their shell turns every stroke ("CLANG"). What breaks it is crystal: a shard from one
    /// of the resonant crystals in the floor cracks a fist open, it drops and lies there with its core showing for
    /// a few seconds, and every blow on it is a blow on the Facet - until the shell closes again.
    /// Its moves (in random order, never the same twice running):
    ///  - Slam: a fist follows the player overhead, stops, trembles and comes down. It rings every crystal it
    ///    lands on - so the way to crack a fist is to make it land on a crystal (stand there, then get out), or
    ///    to ring a crystal with the blade while the fist lies beside it. Half dead, both fists slam, one after
    ///    the other.
    ///  - Sweep: a fist goes down to one wall and sweeps the whole floor: jump it. It smashes the crystals in its way: they burst at once and the fist stops the
    ///    shards on its own side, so they only fly on ahead of it - the way out is over the fist, to behind it.
    ///  - Light: three thin lines reach from the gem in its forehead, a fan about the player; they follow for a moment, lock,
    ///    and then three beams burn along them (twice when half dead). A beam that falls on a crystal is thrown
    ///    back as from a mirror - off the face it meets, up and away across the hall - so near a crystal
    ///    there is a second beam to stay out of. (The light does not ring the crystal: it only turns it.)
    ///  - Rain: it roars and spikes of rock fall where the player stands, one after another.
    /// </summary>
    public class FacetGolem : Boss
    {
        public float actionGap = 1.5f;
        public float tellTime = 0.55f;
        [Header("Body")]
        [Tooltip("How high above the floor its forehead is (the body picture hangs from there).")] public float headHeight = 7f;
        public string fistId = "facetfist";
        [Tooltip("How far to each side of its middle a fist hovers.")] public float fistSpread = 6.5f;
        [Tooltip("How high above the floor a fist hovers.")] public float hoverHeight = 4.6f;
        [Header("Shell")]
        [Tooltip("Seconds a fist lies open after a shard has cracked it: the opening.")] public float crackedTime = 3f;
        [Tooltip("Seconds after the shell has closed before a fist can be cracked again.")] public float crackGuard = 1.2f;
        [Header("Slam")]
        [Tooltip("Seconds a fist follows the player before it stops over them.")] public float trackTime = 0.8f;
        public float trackSpeed = 11f;
        [Tooltip("Seconds it hangs trembling before it falls: the warning.")] public float hangTime = 0.32f;
        public float slamSpeed = 32f;
        [Tooltip("Seconds a fist stays on the floor after a slam.")] public float groundTime = 0.9f;
        [Header("Sweep")]
        public float sweepSpeed = 12f;
        [Tooltip("Seconds a fist waits at the wall before it sweeps: the warning.")] public float sweepTell = 0.7f;
        [Tooltip("How much faster than the sweeping fist the shards fly that it knocks out of a crystal (they must stay ahead of it).")] public float sweepShardLead = 5f;
        [Header("Light")]
        public int beams = 3;
        [Tooltip("Degrees between two beams of the fan.")] public float beamSpread = 16f;
        [Tooltip("Seconds the thin lines show before the beams: the warning.")] public float beamAim = 0.95f;
        [Tooltip("For the last part of the aiming the lines no longer follow the player.")] public float beamLock = 0.35f;
        [Tooltip("Seconds the beams burn.")] public float beamBurn = 0.4f;
        public float beamWidth = 0.3f;
        public int beamDamage = 1;
        [Tooltip("How often one beam can be thrown back by crystals.")] public int beamBounces = 3;
        [Tooltip("How far above its forehead mark the gem sits that the light comes from.")] public float gemHeight = 0.27f;
        public Material beamMaterial;
        [Header("Rain")]
        public int rainSpikes = 3;
        public float rainInterval = 0.4f;
        public SpriteAnim animIdle;
        public SpriteAnim animStrike;
        public SpriteAnim animCracked;

        static readonly Color Crystal = new Color(0.6f, 0.95f, 1f);
        enum State { Idle, Tell, Fists, Rain, Cracked, Aim, Burn }
        State state;
        float timer, guard, clangT, rainT, minX, maxX;
        int pending, rainLeft, volleysLeft, chain;
        bool relay;
        Vector2 home;
        FacetFist left, right, lead, openFist;
        Vector2 aimDir = Vector2.down;
        bool burnt;
        List<Vector2>[] paths;
        readonly List<ResonantCrystal> mirrors = new List<ResonantCrystal>();
        readonly List<BeamVisual> lines = new List<BeamVisual>();
        static readonly Color Light = new Color(0.55f, 0.95f, 1f);

        /// <summary>A fist lies broken open: now the Facet can be hurt.</summary>
        public bool Cracked => state == State.Cracked;
        /// <summary>How often a fist has been cracked, has slammed, has swept (tests).</summary>
        public int Cracks { get; private set; }
        public int Slams { get; private set; }
        public int Sweeps { get; private set; }
        /// <summary>How often it has fired its light, how often a beam was thrown back by a crystal, how often one burnt the player (tests).</summary>
        public int Volleys { get; private set; }
        public int Bounces { get; private set; }
        public int BeamHits { get; private set; }
        public bool Aiming => state == State.Aim;
        public bool Burning => state == State.Burn;
        /// <summary>Where its light comes from: the gem in its forehead.</summary>
        Vector2 Gem => new Vector2(pos.x, pos.y + gemHeight);
        public FacetFist Left => left;
        public FacetFist Right => right;
        public FacetFist OpenFist => openFist;
        public float FloorY => home.y;
        /// <summary>How far left and right a fist can go (its middle).</summary>
        public float MinX => minX;
        public float MaxX => maxX;
        public float ClampX(float x) => Mathf.Clamp(x, minX, maxX);
        public Vector2 RestPoint(int side) => new Vector2(ClampX(home.x + side * fistSpread), home.y + hoverHeight);

        // the body is a picture on the back wall: it does not strike, and nobody lands on it
        public override bool Attacking => false;
        public override bool IsStompable => false;
        public override bool TriggersTraps => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (Dead) return;      // (the test bot walks an empty hall)
            home = pos;
            var g = World.Grid;
            int fy = Mathf.FloorToInt(home.y + 0.5f), cx = Mathf.FloorToInt(home.x);
            int lx = cx, rx = cx;
            while (lx > 1 && !g.IsSolid(lx - 1, fy)) lx--;
            while (rx < g.Width - 2 && !g.IsSolid(rx + 1, fy)) rx++;
            minX = lx + 1.05f; maxX = rx + 1f - 1.05f;
            home.x = (lx + rx + 1) * 0.5f;      // it sits in the middle of its hall, wherever the arena file put its mark
            pos = home + Vector2.up * headHeight;
            prevPos = pos; transform.position = pos;
            left = SpawnFist(-1); right = SpawnFist(1);
            if (left != null) { float hw = left.size.x * 0.5f + 0.1f; minX = lx + hw; maxX = rx + 1f - hw; }      // a fist stops short of the walls
            state = State.Idle; timer = 1.6f;
            anim?.Play(animIdle);
        }

        FacetFist SpawnFist(int side)
        {
            var f = World.SpawnById(fistId, RestPoint(side)) as FacetFist;
            if (f == null) return null;
            f.boss = this; f.side = side; f.summoned = true;
            return f;
        }

        FacetFist Other(FacetFist f) => f == left ? right : left;

        public override void Tick(float dt)
        {
            base.Tick(dt);
            guard -= dt; clangT -= dt; timer -= dt;
            if (left == null || right == null) return;
            var p = Target;
            switch (state)
            {
                case State.Idle:
                    anim?.Play(animIdle);
                    if (timer <= 0f && p != null && !left.Busy && !right.Busy) Choose(p);
                    break;
                case State.Tell:
                    if (timer > 0f) break;
                    if (pending == 2)
                    {
                        state = State.Rain; rainLeft = rainSpikes; rainT = 0f; timer = rainSpikes * rainInterval + 0.3f;
                        Fx.Shake(0.4f);
                        Sfx.Play("rumble", pos, 0.8f, 1.3f);
                    }
                    else { state = State.Aim; timer = beamAim; Sfx.Play("gem", Gem, 0.9f, 0.7f); }
                    break;
                case State.Aim:
                    // the lines follow the player, then lock: that is where the beams will burn
                    if (p != null && timer > beamLock)
                    {
                        Vector2 to = p.Center - Gem;
                        if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                    }
                    TraceAll();
                    if (timer <= 0f)
                    {
                        state = State.Burn; timer = beamBurn;
                        burnt = false;
                        Volleys++;
                        Bounces += TraceAll();
                        LevelLighting.Flash(Gem, Light, 7f, 1.6f, beamBurn);
                        Fx.Shake(0.2f);
                        Sfx.Play("flame", Gem, 1f, 1.5f);
                    }
                    break;
                case State.Burn:
                    TraceAll();
                    foreach (var path in paths)
                        for (int i = 0; i + 1 < path.Count; i++)
                        {
                            if (World.Frame % 3 == 0) Fx.Sparks(path[i + 1], Light, 1, 3f);
                            if (burnt || p == null || BeamVisual.DistanceTo(p.Center, path[i], path[i + 1]) > beamWidth * 0.5f + 0.3f) continue;
                            burnt = true;
                            if (p.TakeDamage(new DamageInfo(beamDamage, DamageType.Fire, this, new Vector2(Mathf.Sign(path[i + 1].x - path[i].x) * 5f, 5f), "the Facet's light"))) BeamHits++;
                        }
                    if (timer > 0f) break;
                    if (--volleysLeft > 0) { state = State.Aim; timer = beamAim * 0.75f; Sfx.Play("gem", Gem, 0.9f, 0.7f); }
                    else { state = State.Idle; timer = Rest(actionGap); }
                    break;
                case State.Fists:
                    // half dead: as soon as the first fist is down the other one sets out
                    if (chain > 0 && lead != null && (lead.mode == FacetFist.Mode.Down || lead.mode == FacetFist.Mode.Rest))
                    {
                        chain--;
                        lead = Other(lead);
                        lead.Slam(); Slams++;
                    }
                    else if (chain <= 0 && !left.Busy && !right.Busy) { state = State.Idle; timer = Rest(actionGap); }
                    break;
                case State.Rain:
                    rainT -= dt;
                    if (rainLeft > 0 && rainT <= 0f && p != null)
                    {
                        DropFromCeiling(Mathf.FloorToInt(p.pos.x), Mathf.FloorToInt(home.y) + 1);
                        rainLeft--; rainT = rainInterval;
                    }
                    if (timer <= 0f) { state = State.Idle; timer = Rest(actionGap); }
                    break;
                case State.Cracked:
                    anim?.Play(animCracked != null ? animCracked : animIdle);
                    if (openFist != null && World.Frame % 6 == 0) Fx.Sparks(new Vector2(openFist.pos.x + Random.Range(-0.6f, 0.6f), openFist.Center.y), new Color(1f, 0.8f, 0.5f), 1, 2f);
                    if (timer <= 0f)
                    {
                        if (openFist != null) openFist.Release();
                        openFist = null;
                        state = State.Idle; timer = Rest(actionGap * 0.6f); guard = crackGuard;
                        anim?.Play(animIdle);
                    }
                    break;
            }
        }

        void Choose(PlayerController p)
        {
            // its moves, in random order: slam (twice as often), sweep, light, rain
            int k = NextMove(0, 0, 1, 2, 3);
            int kind = k <= 1 ? 0 : k - 1;
            anim?.Play(animStrike);
            switch (kind)
            {
                case 0:
                    lead = Mathf.Abs(left.pos.x - p.pos.x) <= Mathf.Abs(right.pos.x - p.pos.x) ? left : right;
                    lead.Slam(); Slams++;
                    chain = HealthFraction < 0.5f ? 1 : 0;
                    state = State.Fists;
                    break;
                case 1:
                    lead = Random.value < 0.5f ? left : right;
                    lead.SweepFrom(lead == left ? 1 : -1); Sweeps++;
                    chain = 0;
                    state = State.Fists;
                    break;
                default:
                    pending = kind == 3 ? 2 : 0;
                    volleysLeft = HealthFraction < 0.5f ? 2 : 1;
                    state = State.Tell; timer = tellTime;
                    Tell();
                    Sfx.Play("brute", pos, 1f, pending == 2 ? 0.7f : 1.2f);
                    break;
            }
        }

        /// <summary>Works out where every beam of the fan goes; returns how often they are thrown back by a crystal.</summary>
        int TraceAll()
        {
            if (paths == null || paths.Length != beams)
            {
                paths = new List<Vector2>[beams];
                for (int i = 0; i < beams; i++) paths[i] = new List<Vector2>();
            }
            mirrors.Clear();
            foreach (var e in World.Entities)
                if (e is ResonantCrystal c && !c.Dead && !c.Spent) mirrors.Add(c);
            int n = 0;
            for (int i = 0; i < beams; i++)
            {
                float a = (i - (beams - 1) * 0.5f) * beamSpread;
                n += Trace(Gem, Quaternion.Euler(0f, 0f, a) * aimDir, paths[i]);
            }
            return n;
        }

        /// <summary>
        /// One beam: straight until it meets rock. A crystal in its way is a mirror: the beam is thrown back off the
        /// face it meets (the face looks out from the crystal's middle), so it goes up and away again.
        /// </summary>
        int Trace(Vector2 from, Vector2 dir, List<Vector2> path)
        {
            const float step = 0.1f;
            var g = World.Grid;
            path.Clear(); path.Add(from);
            Vector2 at = from;
            ResonantCrystal last = null;
            int n = 0;
            for (int s = 0; s < 700; s++)
            {
                Vector2 next = at + dir * step;
                if (g.IsSolid(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y))) { at = next; break; }
                foreach (var c in mirrors)
                {
                    if (c == last || Mathf.Abs(next.x - c.pos.x) > c.size.x * 0.5f || next.y < c.pos.y || next.y > c.pos.y + c.size.y) continue;
                    Vector2 normal = next - c.Center;
                    normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
                    if (Vector2.Dot(dir, normal) < 0f) dir = Vector2.Reflect(dir, normal).normalized;
                    path.Add(next);
                    last = c; n++;
                    break;
                }
                at = next;
                if (n > beamBounces) break;
            }
            path.Add(at);
            return n;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            int used = 0;
            if (!Dead && paths != null && (state == State.Aim || state == State.Burn))
            {
                bool burn = state == State.Burn;
                foreach (var path in paths)
                    for (int i = 0; i + 1 < path.Count; i++)
                    {
                        if (burn)
                        {
                            Line(used++).Show(path[i], path[i + 1], beamWidth, new Color(Light.r, Light.g, Light.b, 0.75f));
                            Line(used++).Show(path[i], path[i + 1], beamWidth * 0.35f, Color.white);
                        }
                        else Line(used++).Show(path[i], path[i + 1], 0.06f, new Color(Light.r, Light.g, Light.b, timer > beamLock ? 0.4f : 0.9f));
                    }
                // round spots of light where a beam strikes and where it turns, and one at the mouth: no square ends
                var glow = new Color(Light.r, Light.g, Light.b, burn ? 0.75f : timer > beamLock ? 0.4f : 0.9f);
                float flicker = burn ? 1f + Mathf.Sin(Time.time * 45f) * 0.12f : 1f;
                foreach (var path in paths)
                    for (int i = 1; i < path.Count; i++)
                    {
                        Line(used++).ShowDot(path[i], (burn ? beamWidth * 2.1f : 0.16f) * flicker, glow);
                        if (burn) Line(used++).ShowDot(path[i], beamWidth * 1.1f * flicker, Color.white);
                    }
                if (paths.Length > 0 && paths[0].Count > 0)
                {
                    Line(used++).ShowDot(paths[0][0], (burn ? beamWidth * 3.2f : 0.3f) * flicker, glow);
                    if (burn) Line(used++).ShowDot(paths[0][0], beamWidth * 1.8f * flicker, Color.white);
                }
            }
            for (int i = used; i < lines.Count; i++) lines[i].Hide();
        }

        BeamVisual Line(int i)
        {
            while (lines.Count <= i)
            {
                var b = BeamVisual.Create(this, beamMaterial);
                b.SetOrder(30);
                lines.Add(b);
            }
            return lines[i];
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            foreach (var b in lines) if (b != null) Destroy(b.gameObject);
            lines.Clear();
        }

        /// <summary>A fist has come down on the floor.</summary>
        public void FistLanded(FacetFist f)
        {
            Fx.Shake(0.5f);
            Fx.Dust(f.pos, 10);
            Sfx.Play("thud", f.pos, 1f, 0.7f);
        }

        /// <summary>A shard has reached a fist: its shell cracks (unless one lies open already, or has only just closed).</summary>
        public void CrackFist(FacetFist f)
        {
            if (Dead || f == null || state == State.Cracked || guard > 0f) return;
            state = State.Cracked; timer = crackedTime;
            Cracks++;
            chain = 0;
            openFist = f;
            f.BreakOpen();
            var o = Other(f);
            if (o != null) o.Release();
            anim?.Play(animCracked != null ? animCracked : animIdle);
            Fx.Debris(f.Center, Crystal, 18);
            Fx.Shake(0.5f);
            Fx.Text(new Vector2(f.pos.x, f.pos.y + f.size.y + 0.5f), "CRACKED", new Color(1f, 0.9f, 0.5f), crackedTime * 0.8f);
            Sfx.Play("shatter", f.pos, 1f, 0.8f);
        }

        /// <summary>
        /// Something has hit a fist. The shell turns the player's own blows; only on the fist that lies open do they
        /// count. (A bomb or a trap is worth its half point as against any boss - the shell is no help against a blast.)
        /// </summary>
        public bool FistStruck(FacetFist f, in DamageInfo d)
        {
            bool playersBlow = d.type == DamageType.Whip || d.type == DamageType.Stomp || d.type == DamageType.Thrown;
            if (playersBlow && !f.IsOpen)
            {
                if (clangT <= 0f)
                {
                    clangT = 0.35f;
                    Fx.Text(new Vector2(f.pos.x, f.pos.y + f.size.y + 0.3f), "CLANG", new Color(0.8f, 0.9f, 1f), 0.5f);
                    Fx.Sparks(f.Center, Crystal, 6, 4f);
                    Sfx.Play("hit", f.pos, 0.8f, 1.8f);
                }
                return false;
            }
            relay = true;
            bool hit = TakeDamage(d);
            relay = false;
            if (hit) { f.Flash(0.15f); Fx.Debris(f.Center, new Color(1f, 0.8f, 0.5f), 4); }
            return hit;
        }

        /// <summary>The head on the wall cannot be hurt: only what a fist passes on counts.</summary>
        public override bool TakeDamage(in DamageInfo d) => relay && base.TakeDamage(d);

        protected override void Die(in DamageInfo d)
        {
            // the whole wall of it comes apart
            foreach (var f in new[] { left, right })
            {
                if (f == null || f.Dead) continue;
                Fx.Debris(f.Center, Crystal, 14);
                f.Despawn();
            }
            for (int i = 0; i < 10; i++)
                Fx.Debris(new Vector2(pos.x + Random.Range(-7f, 7f), pos.y + Random.Range(-6f, 1f)), i % 2 == 0 ? Crystal : new Color(0.3f, 0.3f, 0.45f), 10);
            base.Die(d);
        }
    }
}
