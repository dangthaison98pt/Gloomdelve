using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// One of the Facet's two fists (FacetGolem): the only part of the colossus that comes down into the hall.
    /// The Facet gives the orders (Slam, SweepFrom, BreakOpen, Release); the fist carries them out by itself.
    /// It only hurts while it strikes - falling, or sweeping along the floor. Its shell turns the blade; broken
    /// open by a crystal shard it lies on the floor with its core showing, and every blow on it is a blow on the
    /// Facet. Whatever hits a fist is passed on to the Facet (FacetGolem.FistStruck).
    /// </summary>
    public class FacetFist : Enemy
    {
        [Tooltip("Pictures: whole, broken open.")] public Sprite[] frames;

        public enum Mode { Rest, Track, Hang, Fall, Down, SweepReady, Sweep, Open }
        [System.NonSerialized] public FacetGolem boss;
        /// <summary>-1 the left fist, +1 the right one.</summary>
        [System.NonSerialized] public int side = 1;
        public Mode mode { get; private set; }
        float t;
        int sweepDir = 1, shown = -1;
        bool told;

        public bool Busy => mode != Mode.Rest;
        /// <summary>Falling or sweeping: the only time it hurts.</summary>
        public bool Striking => mode == Mode.Fall || mode == Mode.Sweep;
        public bool IsOpen => mode == Mode.Open;
        public override bool Attacking => Striking;
        // its back can be landed on, also while it sweeps (the player bounces off it: that is one way over);
        // only a fist that is coming down cannot
        public override bool IsStompable => mode != Mode.Fall;
        public override bool TriggersTraps => false;

        /// <summary>Follow the player overhead, hang for a moment, then come down.</summary>
        public void Slam() { mode = Mode.Track; t = boss.trackTime; }
        /// <summary>Go down to the wall, then sweep the floor in this direction (+1 to the right).</summary>
        public void SweepFrom(int dir) { mode = Mode.SweepReady; sweepDir = dir; told = false; t = 99f; }
        /// <summary>The shell is broken: drop to the floor and lie open.</summary>
        public void BreakOpen() { mode = Mode.Open; }
        /// <summary>Back to where it hovers.</summary>
        public void Release() { mode = Mode.Rest; }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (boss == null || boss.Dead) { Despawn(); return; }
            t -= dt;
            float floor = boss.FloorY;
            var p = Player;
            switch (mode)
            {
                case Mode.Rest:
                {
                    Vector2 r = boss.RestPoint(side);
                    r.y += Mathf.Sin(World.Frame * 0.035f + side) * 0.18f;
                    pos = Vector2.MoveTowards(pos, r, 9f * dt);
                    break;
                }
                case Mode.Track:
                {
                    float tx = boss.ClampX(p != null && !p.IsDead ? p.pos.x : pos.x);
                    var to = new Vector2(tx, floor + boss.hoverHeight);
                    pos = Vector2.MoveTowards(pos, to, boss.trackSpeed * dt);
                    if (t <= 0f && Mathf.Abs(pos.y - to.y) < 0.3f)
                    {
                        // it stops where the player is now: the warning
                        mode = Mode.Hang; t = boss.hangTime;
                        Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "!", new Color(1f, 0.85f, 0.3f), 0.45f);
                        Sfx.Play("brute", pos, 0.9f, 1.3f);
                    }
                    break;
                }
                case Mode.Hang:
                    if (t <= 0f) mode = Mode.Fall;
                    break;
                case Mode.Fall:
                    pos.y -= boss.slamSpeed * dt;
                    if (pos.y <= floor)
                    {
                        pos.y = floor;
                        mode = Mode.Down; t = boss.groundTime;
                        RingCrystals();
                        boss.FistLanded(this);
                    }
                    break;
                case Mode.Down:
                    if (t <= 0f) mode = Mode.Rest;
                    break;
                case Mode.SweepReady:
                {
                    var to = new Vector2(sweepDir > 0 ? boss.MinX : boss.MaxX, floor);
                    if (!told)
                    {
                        pos = Vector2.MoveTowards(pos, to, 16f * dt);
                        if ((pos - to).sqrMagnitude < 0.0004f)
                        {
                            told = true; t = boss.sweepTell;
                            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "!", new Color(1f, 0.85f, 0.3f), 0.55f);
                            Fx.Shake(0.2f);
                            Sfx.Play("brute", pos, 0.9f, 0.8f);
                        }
                    }
                    else if (t <= 0f) { mode = Mode.Sweep; Sfx.Play("rumble", pos, 0.8f, 1.2f); }
                    break;
                }
                case Mode.Sweep:
                    pos.x += sweepDir * boss.sweepSpeed * dt;
                    // it smashes every crystal in its way: they burst at once and the fist stops the shards on its own
                    // side, so they only fly on ahead of it - the safe place is behind the fist: jump it
                    foreach (var e in World.Entities)
                        if (e is ResonantCrystal c && !c.Dead && !c.Spent && Overlaps(c)) c.BurstAhead(sweepDir, boss.sweepSpeed + boss.sweepShardLead);
                    if (World.Frame % 3 == 0) Fx.Dust(new Vector2(pos.x - sweepDir * size.x * 0.5f, pos.y), 2);
                    if (sweepDir > 0 ? pos.x >= boss.MaxX : pos.x <= boss.MinX)
                    {
                        pos.x = sweepDir > 0 ? boss.MaxX : boss.MinX;
                        mode = Mode.Down; t = 0.3f;
                        Fx.Shake(0.3f);
                        Sfx.Play("thud", pos, 0.9f, 0.8f);
                    }
                    break;
                case Mode.Open:
                    if (pos.y > floor) pos.y = Mathf.Max(floor, pos.y - 22f * dt);
                    break;
            }
        }

        /// <summary>Every crystal under the fist starts to ring.</summary>
        void RingCrystals()
        {
            foreach (var e in World.Entities)
                if (e is ResonantCrystal c && !c.Dead && Overlaps(c)) c.Ring();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            sr.flipX = side > 0;
            // it trembles while it hangs over its mark
            sr.transform.localPosition = mode == Mode.Hang ? new Vector3(Mathf.Sin(Time.time * 70f) * 0.05f, 0f, 0f) : Vector3.zero;
            int f = IsOpen ? 1 : 0;
            if (frames == null || f == shown || f >= frames.Length) return;
            shown = f;
            sr.sprite = frames[f];
        }

        public override bool TakeDamage(in DamageInfo d) => boss != null && !boss.Dead && boss.FistStruck(this, d);
    }
}
