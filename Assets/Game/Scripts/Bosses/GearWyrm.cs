using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks boss: the Cogwyrm, a centipede of gears. A head with a burning core, a train of gear segments
    /// behind it and a tail. Only the head can be hurt (struck or stomped); every segment is a saw: touching one
    /// hurts. It does not walk the floor as the other bosses do - it runs on a track round the hall: up the walls
    /// and along the ceiling, out of reach, and comes down for its moves (random order, never the same twice
    /// running, none shared with the other bosses):
    ///  - Floor run: down one wall, flat out along the floor, up the other. Jump it against its run, or dash
    ///    through. A belt under it speeds it up or slows it down.
    ///  - Dive: from the ceiling over the player, head first into the floor - and stuck there for a moment, its
    ///    body hanging above it. The opening of the fight.
    ///  - Bolts: it lowers its head from the ceiling and spits two fans of bolts. The head is within reach of a
    ///    jump for that long.
    ///  - Wheel: it curls up into one great gear and bounces across the hall and back. Nothing hurts it then; run
    ///    under it while it is high.
    /// At every quarter of its health its last segment drops off and rolls away as a loose saw; shorter, it is
    /// faster. At half health it dives twice and runs the floor there and back.
    /// It never touches the tiles: it moves along a path worked out from the hall it wakes up in.
    /// </summary>
    public class GearWyrm : Boss
    {
        public int segments = 6;
        [Tooltip("Distance between two segments along its body.")] public float segmentSpacing = 0.8f;
        public float segmentRadius = 0.42f;
        public int segmentDamage = 1;
        public float patrolSpeed = 5.5f;
        [Tooltip("How fast it goes to where a move starts.")] public float seekSpeed = 11f;
        public float actionGap = 1.5f;
        [Tooltip("How high above the floor it stays while it only patrols (out of reach).")] public float safeHeight = 3.6f;
        [Tooltip("Each segment lost makes it this much faster.")] public float speedPerShed = 0.08f;
        [Header("Floor run")]
        public float runTell = 0.45f;
        public float runSpeed = 10f;
        [Header("Dive")]
        public float diveTell = 0.5f;
        public float diveSpeed = 22f;
        [Tooltip("Seconds its head stays stuck in the floor (the opening).")] public float stuckTime = 1.2f;
        [Tooltip("... after the second dive of an enraged pair.")] public float stuckTimeSecond = 0.7f;
        public float riseSpeed = 8f;
        [Header("Bolts")]
        public string boltId = "bolt";
        [Tooltip("How high above the floor the head hangs while it spits.")] public float boltHeight = 3.3f;
        public float boltSpeed = 9f;
        public float boltSpread = 16f;
        public float boltHold = 1.5f;
        [Header("Wheel")]
        public float wheelRadius = 1.1f;
        public float wheelSpeed = 8f;
        public float wheelBounce = 2.5f;
        [Tooltip("Tiles between two bounces.")] public float wheelBounceLength = 5f;
        [Header("Shed segments")]
        public string sawId = "sawblade";
        public int maxShed = 3;
        public Sprite segmentSprite, tailSprite;
        public SpriteAnim animIdle, animOpen;

        static readonly Color Brass = new Color(0.95f, 0.75f, 0.35f);
        enum State { Rise, Patrol, RunSeek, RunTell, Run, DiveSeek, DiveTell, DiveDown, DiveStuck, DiveUp, BoltSeek, BoltDown, BoltHold, BoltUp, WheelSeek, WheelCurl, WheelRoll, WheelUncurl }
        State state;

        // the hall, and the track round it
        float floorY, L, R, T, B, H, W, per, uA, uB;
        Vector2 hc, headDir = Vector2.right;      // the head's centre, and where it is going
        float s, timer, runLeft, speedMul = 1f, curl, spin, travelled, hurtCd, targetS, boltT;
        int dirS = 1, runDir, runPasses, divesLeft, wheelDir, wheelPasses, segCount, shed, forced = -1, volleys;
        Vector2 moveTo;
        readonly List<Vector2> trail = new List<Vector2>();
        Vector2[] segPos, segPrev;
        SpriteRenderer[] segSr;
        const float TrailStep = 0.1f;

        public override bool Attacking => !Dead && (state == State.Run || state == State.DiveDown || state == State.WheelRoll);
        public bool Enraged => HealthFraction <= 0.5f;
        public bool Patrolling => state == State.Patrol;
        public bool Stuck => state == State.DiveStuck;
        public bool Running => state == State.Run;
        public bool Rolling => state == State.WheelCurl || state == State.WheelRoll;
        public bool Spitting => state == State.BoltHold;
        public int SegmentCount => segCount;
        public int Shed => shed;
        public Vector2 HeadCentre => hc;
        public float FloorLevel => floorY;
        public Vector2 SegmentAt(int i) => segPos != null && i >= 0 && i < segPos.Length ? segPos[i] : hc;
        /// <summary>Makes its next move this one (0 run, 1 dive, 2 bolts, 3 wheel) as soon as it patrols (tests).</summary>
        public void ForceMove(int kind) { forced = kind; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (Dead) return;
            var g = World.Grid;
            int cx = Mathf.FloorToInt(pos.x), fy = Mathf.FloorToInt(pos.y + 0.5f);
            while (fy > 0 && !g.IsSolid(cx, fy - 1)) fy--;
            int cy = fy + 1;
            while (cy < g.Height - 1 && !g.IsSolid(cx, cy)) cy++;
            int row = Mathf.Min(cy - 1, fy + 5);      // (above the ledges of the hall)
            int lx = cx, rx = cx;
            while (lx > 0 && !g.IsSolid(lx - 1, row)) lx--;
            while (rx < g.Width - 1 && !g.IsSolid(rx + 1, row)) rx++;
            floorY = fy;
            L = lx + 1f; R = rx; T = cy - 0.9f; B = fy + size.y * 0.5f + 0.05f;
            H = Mathf.Max(1f, T - B); W = Mathf.Max(2f, R - L);
            per = 2f * H + 2f * W;
            float safe = Mathf.Min(H - 0.5f, safeHeight - (B - floorY));
            uA = safe; uB = 2f * H + W - safe;

            segCount = segments;
            hc = new Vector2(Mathf.Clamp(pos.x, L, R), pos.y + size.y * 0.5f);
            ResetTrail();
            segPos = new Vector2[segments]; segPrev = new Vector2[segments];
            segSr = new SpriteRenderer[segments];
            for (int i = 0; i < segments; i++)
            {
                segPos[i] = segPrev[i] = hc;
                var go = new GameObject("Segment" + i);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = i == segments - 1 && tailSprite != null ? tailSprite : segmentSprite;
                if (sr != null) { r.sharedMaterial = sr.sharedMaterial; r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder - 1 - i; }
                segSr[i] = r;
            }
            if (sr != null) sr.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            // it wakes on the floor and goes straight up to its track
            s = H + Mathf.Clamp(hc.x - L, 0.5f, W - 0.5f);
            moveTo = P(s);
            state = State.Rise; timer = 1.2f;
            anim?.Play(animIdle);
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (segSr != null) foreach (var r in segSr) if (r != null) Destroy(r.gameObject);
        }

        /// <summary>The point at distance 'at' along the track: up the left wall, along the ceiling, down the right wall, back along the floor.</summary>
        Vector2 P(float at)
        {
            at = Mathf.Repeat(at, per);
            if (at < H) return new Vector2(L, B + at);
            if (at < H + W) return new Vector2(L + (at - H), T);
            if (at < 2f * H + W) return new Vector2(R, T - (at - H - W));
            return new Vector2(R - (at - 2f * H - W), B);
        }

        void ResetTrail()
        {
            trail.Clear();
            for (int i = 0; i < 4; i++) trail.Add(hc);
        }

        bool MoveHead(Vector2 to, float speed, float dt)
        {
            hc = Vector2.MoveTowards(hc, to, speed * dt);
            return (hc - to).sqrMagnitude < 0.0004f;
        }

        bool SeekS(float to, float speed, float dt)
        {
            s = Mathf.MoveTowards(s, to, speed * dt);
            hc = P(s);
            return Mathf.Abs(s - to) < 0.001f;
        }

        float NearEnd() => Mathf.Abs(s - uA) <= Mathf.Abs(s - uB) ? uA : uB;
        float Over(float x) => H + Mathf.Clamp(x - L, 0.6f, W - 0.6f);

        void Go(State to, float t = 0f) { state = to; timer = t; }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Dead || segPos == null) return;
            Vector2 before = hc;
            var p = Target;
            hurtCd -= dt;
            if (!HoldCounterPose(dt, false)) Act(p, dt);

            // the body: every segment follows the head's own trail
            Vector2 d = hc - before;
            if (d.sqrMagnitude > 0.000001f) headDir = d.normalized;
            int guard = 0;
            while ((hc - trail[0]).sqrMagnitude >= TrailStep * TrailStep && guard++ < 60)
                trail.Insert(0, Vector2.MoveTowards(trail[0], hc, TrailStep));
            if (trail.Count > 400) trail.RemoveRange(400, trail.Count - 400);
            spin += dt * 260f;
            int per1 = Mathf.Max(1, Mathf.RoundToInt(segmentSpacing / TrailStep));
            for (int i = 0; i < segPos.Length; i++)
            {
                segPrev[i] = segPos[i];
                Vector2 on = trail[Mathf.Min(trail.Count - 1, (i + 1) * per1)];
                if (curl > 0f && segCount > 0)
                {
                    // curled up: the segments stand in a ring round the head and turn with it
                    float a = (spin * 1.4f + i * 360f / segCount) * Mathf.Deg2Rad;
                    Vector2 ring = hc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (wheelRadius - segmentRadius);
                    on = Vector2.Lerp(on, ring, curl);
                }
                segPos[i] = on;
            }
            pos = new Vector2(hc.x, hc.y - size.y * 0.5f);
            vel = Vector2.zero;

            Shedding(p);
            // every segment is a saw; curled up, so is the whole wheel
            if (p != null && hurtCd <= 0f)
            {
                Vector2 pc = p.Center;
                bool hit = state == State.WheelRoll && (pc - hc).sqrMagnitude < (wheelRadius + 0.3f) * (wheelRadius + 0.3f);
                float reach = segmentRadius + 0.3f;
                for (int i = 0; i < segCount && !hit; i++) hit = (pc - segPos[i]).sqrMagnitude < reach * reach;
                if (hit)
                {
                    hurtCd = 0.6f;
                    int away = pc.x >= hc.x ? 1 : -1;
                    p.TakeDamage(new DamageInfo(segmentDamage, DamageType.Contact, this, new Vector2(away * 6f, 6f), displayName));
                }
            }
        }

        void Act(PlayerController p, float dt)
        {
            float fast = speedMul;
            switch (state)
            {
                case State.Rise:
                    timer -= dt;
                    if (timer <= 0f && MoveHead(moveTo, riseSpeed, dt)) Go(State.Patrol, Rest(actionGap));
                    break;

                case State.Patrol:
                    anim?.Play(animIdle);
                    s += dirS * patrolSpeed * fast * dt;
                    if (s >= uB) { s = uB; dirS = -1; }
                    else if (s <= uA) { s = uA; dirS = 1; }
                    hc = P(s);
                    timer -= dt;
                    if (p != null && (timer <= 0f || forced >= 0))
                    {
                        int kind = forced >= 0 ? forced : NextMove(0, 1, 2, 3);
                        forced = -1;
                        if (kind == 0) { targetS = NearEnd(); Go(State.RunSeek); }
                        else if (kind == 1) { divesLeft = Enraged ? 2 : 1; targetS = Over(p.pos.x); Go(State.DiveSeek); }
                        else if (kind == 2) { targetS = Over(p.pos.x + (p.pos.x < (L + R) * 0.5f ? 3f : -3f)); Go(State.BoltSeek); }
                        else { targetS = NearEnd(); Go(State.WheelSeek); }
                    }
                    break;

                // ---- floor run
                case State.RunSeek:
                    if (SeekS(targetS, seekSpeed * fast, dt))
                    {
                        Go(State.RunTell, runTell); Tell(); anim?.Play(animOpen);
                        Sfx.Play("brute", pos, 1f, 1.3f);
                        runDir = Mathf.Approximately(targetS, uA) ? -1 : 1;      // off the end of the track: down the wall, along the floor
                        runPasses = Enraged ? 2 : 1;
                        runLeft = 2f * uA + W;
                    }
                    break;
                case State.RunTell:
                    timer -= dt;
                    if (timer <= 0f) Go(State.Run);
                    break;
                case State.Run:
                {
                    anim?.Play(animOpen);
                    float step = runSpeed * fast * BeltFactor() * dt;
                    s = Mathf.Repeat(s + runDir * step, per);
                    runLeft -= step;
                    hc = P(s);
                    if (hc.y <= B + 0.05f && Random.value < 0.4f) Fx.Dust(new Vector2(hc.x, floorY), 1);
                    if (runLeft <= 0f)
                    {
                        s = runDir < 0 ? uB : uA; hc = P(s);
                        if (--runPasses > 0) { runDir = -runDir; runLeft = 2f * uA + W; }      // enraged: straight back the way it came
                        else { dirS = runDir < 0 ? -1 : 1; Go(State.Patrol, Rest(actionGap)); }
                    }
                    break;
                }

                // ---- dive
                case State.DiveSeek:
                    if (SeekS(targetS, seekSpeed * fast, dt)) { Go(State.DiveTell, diveTell); Tell(); anim?.Play(animOpen); Sfx.Play("brute", pos, 1f, 0.8f); }
                    break;
                case State.DiveTell:
                    timer -= dt;
                    hc = P(s) + new Vector2(Mathf.Sin(timer * 60f) * 0.06f, 0f);      // it trembles before it strikes
                    headDir = Vector2.down;
                    if (timer <= 0f) { hc = P(s); Go(State.DiveDown); Sfx.Play("throw", pos, 1f, 0.6f); }
                    break;
                case State.DiveDown:
                    hc.y -= diveSpeed * dt;
                    if (hc.y <= B)
                    {
                        hc.y = B;
                        Go(State.DiveStuck, divesLeft <= 1 && Enraged ? stuckTimeSecond : stuckTime);
                        anim?.Play(animIdle);
                        Fx.Shake(0.45f); Fx.Dust(new Vector2(hc.x, floorY), 8); Fx.Sparks(new Vector2(hc.x, floorY + 0.2f), Brass, 8, 5f);
                        Sfx.Play("thud", pos, 1f, 0.8f);
                    }
                    break;
                case State.DiveStuck:
                    timer -= dt;
                    if (timer <= 0f) { moveTo = P(s); Go(State.DiveUp); }
                    break;
                case State.DiveUp:
                    if (MoveHead(moveTo, riseSpeed * fast, dt))
                    {
                        if (--divesLeft > 0 && p != null) { targetS = Over(p.pos.x); Go(State.DiveSeek); }
                        else Go(State.Patrol, Rest(actionGap));
                    }
                    break;

                // ---- bolts
                case State.BoltSeek:
                    if (SeekS(targetS, seekSpeed * fast, dt)) { moveTo = new Vector2(hc.x, floorY + boltHeight); Go(State.BoltDown); Tell(); }
                    break;
                case State.BoltDown:
                    if (MoveHead(moveTo, riseSpeed, dt)) { Go(State.BoltHold, boltHold); boltT = 0.2f; volleys = 2; anim?.Play(animOpen); }
                    break;
                case State.BoltHold:
                    timer -= dt; boltT -= dt;
                    if (p != null) headDir = (p.Center - hc).normalized;
                    if (volleys > 0 && boltT <= 0f && p != null)
                    {
                        volleys--; boltT = 0.6f;
                        FanAt(hc, p.Center, 3, boltSpread, boltSpeed, boltId);
                        Sfx.Play("throw", pos, 1f, 1.2f);
                    }
                    if (timer <= 0f) { moveTo = P(s); anim?.Play(animIdle); Go(State.BoltUp); }
                    break;
                case State.BoltUp:
                    if (MoveHead(moveTo, riseSpeed, dt)) Go(State.Patrol, Rest(actionGap));
                    break;

                // ---- wheel
                case State.WheelSeek:
                    if (SeekS(targetS, seekSpeed * fast, dt))
                    {
                        bool left = Mathf.Approximately(targetS, uA);
                        moveTo = new Vector2(left ? L + 0.3f : R - 0.3f, floorY + wheelRadius);
                        wheelDir = left ? 1 : -1; wheelPasses = 2; travelled = 0f;
                        Go(State.WheelCurl, 0.6f); Tell();
                        Sfx.Play("brute", pos, 1f, 0.6f);
                    }
                    break;
                case State.WheelCurl:
                    timer -= dt;
                    curl = Mathf.MoveTowards(curl, 1f, dt / 0.45f);
                    bool there = MoveHead(moveTo, seekSpeed, dt);
                    if (timer <= 0f && there && curl >= 1f) Go(State.WheelRoll);
                    break;
                case State.WheelRoll:
                {
                    float step = wheelSpeed * fast * dt;
                    travelled += step;
                    float x = hc.x + wheelDir * step;
                    float lo = L + 0.3f, hi = R - 0.3f;
                    if (x <= lo || x >= hi)
                    {
                        x = Mathf.Clamp(x, lo, hi);
                        wheelDir = -wheelDir; wheelPasses--;
                        Fx.Shake(0.3f); Sfx.Play("thud", pos, 1f, 1.1f);
                    }
                    float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * travelled / Mathf.Max(1f, wheelBounceLength)));
                    hc = new Vector2(x, floorY + wheelRadius + wheelBounce * hop);
                    if (hop < 0.08f && Random.value < 0.5f) Fx.Dust(new Vector2(x, floorY), 1);
                    if (wheelPasses <= 0)
                    {
                        s = hc.x < (L + R) * 0.5f ? uA : uB;
                        moveTo = P(s);
                        Go(State.WheelUncurl);
                    }
                    break;
                }
                case State.WheelUncurl:
                    curl = Mathf.MoveTowards(curl, 0f, dt / 0.35f);
                    if (MoveHead(moveTo, riseSpeed, dt) && curl <= 0f) { dirS = Mathf.Approximately(s, uA) ? 1 : -1; Go(State.Patrol, Rest(actionGap)); }
                    break;
            }
        }

        /// <summary>A belt under its head, running its way, hurries it along the floor; one against it holds it back.</summary>
        float BeltFactor()
        {
            if (hc.y > B + 0.05f) return 1f;
            int way = runDir > 0 ? -1 : 1;      // (along the floor the track runs from right to left)
            foreach (var e in World.Entities)
                if (!e.Dead && e is Conveyor c && Mathf.Abs(c.pos.x - hc.x) < 0.6f && Mathf.Abs(c.pos.y - floorY) < 0.4f)
                    return c.dir == way ? 1.25f : 0.8f;
            return 1f;
        }

        /// <summary>At every quarter of its health the last segment drops off and rolls away as a loose saw.</summary>
        void Shedding(PlayerController p)
        {
            while (shed < maxShed && segCount > 1 && HealthFraction <= 1f - 0.25f * (shed + 1))
            {
                Vector2 at = segPos[segCount - 1];
                shed++; segCount--;
                speedMul = 1f + speedPerShed * shed;
                // (the tail picture moves up to the new last segment)
                if (segSr[segCount] != null) segSr[segCount].enabled = false;
                if (segSr[segCount - 1] != null && tailSprite != null) segSr[segCount - 1].sprite = tailSprite;
                Fx.Debris(at, Brass, 10);
                float x = Mathf.Clamp(at.x, L + 0.5f, R - 0.5f);
                var saw = World.SpawnById(sawId, new Vector2(x, floorY)) as SawBlade;
                if (saw != null) saw.Launch(p != null && p.pos.x < x ? -1 : 1);
                Sfx.Play("crack", pos, 1f, 0.8f);
            }
        }

        /// <summary>Curled up into a wheel nothing gets through to its head.</summary>
        public override bool TakeDamage(in DamageInfo d)
        {
            if (!Dead && (state == State.WheelRoll || state == State.WheelCurl))
            {
                Fx.Sparks(hc, Brass, 8, 5f);
                Sfx.Play("crack", pos, 0.8f, 1.5f);
                return false;
            }
            return base.TakeDamage(d);
        }

        /// <summary>Stomped again: its tail whips up and throws the player off.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 7f, 11f), 0.2f);
            Fx.Sparks(new Vector2(hc.x, hc.y + size.y * 0.5f), Brass, 10, 6f);
            Fx.Shake(0.3f);
            Sfx.Play("crack", pos, 1f, 0.7f);
        }

        protected override void Die(in DamageInfo d)
        {
            if (segPos != null) for (int i = 0; i < segCount; i++) Fx.Debris(segPos[i], Brass, 8);
            if (segSr != null) foreach (var r in segSr) if (r != null) r.enabled = false;
            base.Die(d);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (segSr == null) return;
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            for (int i = 0; i < segSr.Length; i++)
            {
                var r = segSr[i];
                if (r == null) continue;
                if (i >= segCount || Dead) { r.enabled = false; continue; }
                r.transform.position = Vector2.Lerp(segPrev[i], segPos[i], alpha);
                r.transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * spin);
            }
            if (sr == null || Countering) return;
            // the head looks where it is going (the picture faces right)
            bool wheel = curl > 0.5f;
            float ang = wheel ? spin * 1.4f : Mathf.Atan2(headDir.y, headDir.x) * Mathf.Rad2Deg;
            sr.flipY = !wheel && headDir.x < -0.01f;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
    }
}
