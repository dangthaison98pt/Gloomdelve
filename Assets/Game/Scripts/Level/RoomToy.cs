using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Something in a house that answers when it is touched - there for the fun of it, not for the run: the cat asleep
    /// by the fire at home (pet it; pester it ten times and it moves to the armchair), the lamps at home (out, and the
    /// room is lit by the moon and the fire alone), Tam's great rope wheel (it spins, and slows), Tam's hammock (lie in
    /// it; it never ends well) and Maren's cauldron (stir it). Whoever keeps the house has something to say about it.
    /// VillageInterior puts them out (param = Kind).
    /// </summary>
    public partial class RoomToy : Entity, IInteractable
    {
        public enum Kind { Cat, Wheel, Cauldron, Hammock, Lamps, Bellows, Anvil, Globe, Telescope, Owl, Clock, Birdcage, LampRow, GreatLamp, Candles, Cup, Banner, Bed, Coil }

        [Tooltip("The cat (facing left): asleep, stretching, sitting up, two steps of a walk.")] public Sprite[] cat;
        public Sprite wheel, hammockRolled, hammockHung;
        [Tooltip("A second picture: the hammock hung up, in front of whoever lies in it.")] public SpriteRenderer extra;
        [Tooltip("The white outlines shown while the player is in reach: the cat's (by frame), the wheel's, the rolled hammock's.")] public Sprite[] catOutline;
        public Sprite wheelOutline, hammockOutline;
        [Tooltip("The outline's renderer (a child, just in front).")] public SpriteRenderer glow;

        public Kind kind;
        VillageInterior room;
        float sayCool, t;

        public override void Configure(SpawnPoint sp)
        {
            var at = pos;
            base.Configure(sp);
            pos = at;                       // just where the room put it (not the middle of a cell)
            kind = (Kind)sp.param;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            FindRoom();
            if (sr == null) return;
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
            sr.sprite = null;
            switch (kind)
            {
                case Kind.Cat: SetCat(0); break;
                case Kind.Wheel: sr.sprite = wheel; sr.transform.localPosition = new Vector3(0.5f / 16f, 70.5f / 16f, 0f); break;      // its hub, high on the wall over its stand
                case Kind.Hammock:
                    sr.sprite = hammockRolled; sr.transform.localPosition = new Vector3(0f, 42.5f / 16f, 0f);                         // rolled up between its two hooks
                    if (extra != null) { extra.sprite = hammockHung; extra.enabled = false; extra.transform.localPosition = new Vector3(0.5f / 16f, 43.5f / 16f, 0f); }
                    break;
            }
        }

        /// <summary>The room this stands in. (Put out while the room itself is being set up, so it may not be found at
        /// once: looked for again until it is.)</summary>
        void FindRoom()
        {
            if (room == null || room.Dead) room = VillageInterior.RoomAt(World, new Vector2(pos.x, pos.y + 0.3f));
            if (!built && room != null) { built = true; Build(); }      // (what it needs placed in the room picture: RoomToy.More)
        }
        bool built;

        Sprite Frame(int i) => cat != null && i < cat.Length ? cat[i] : null;

        /// <summary>Still busy with the last touch (the cat is up and about, someone is in the hammock): nothing to do with it now.</summary>
        public bool Busy => (kind == Kind.Cat && catState != 0) || (kind == Kind.Hammock && (sleeper != null || hamRest > 0f)) || MoreBusy;

        public string GetPrompt(PlayerController p)
        {
            FindRoom();
            if (Busy) return null;
            switch (kind)
            {
                case Kind.Cat: return "[enter]  Pet the cat";
                case Kind.Wheel: return "[enter]  Spin the wheel";
                case Kind.Cauldron: return "[enter]  Stir the brew";
                case Kind.Hammock: return "[enter]  Lie in the hammock";
                case Kind.Lamps: return room != null && room.LampsOut ? "[enter]  Light the lamps" : "[enter]  Put out the lamps";
                default: return MorePrompt();
            }
        }

        public void Interact(PlayerController p)
        {
            FindRoom();
            if (p == null || Busy || p.motor.mode != MotorMode.Normal || !p.motor.grounded) return;
            switch (kind)
            {
                case Kind.Cat: PetCat(); break;
                case Kind.Wheel: SpinWheel(); break;
                case Kind.Cauldron: Stir(); break;
                case Kind.Hammock: LieDown(p); break;
                case Kind.Lamps: SwitchLamps(); break;
                default: MoreInteract(p); break;
            }
        }

        public override void Tick(float dt)
        {
            t += dt;
            sayCool -= dt;
            FindRoom();
            switch (kind)
            {
                case Kind.Cat: TickCat(dt); break;
                case Kind.Wheel: TickWheel(dt); break;
                case Kind.Cauldron: TickCauldron(dt); break;
                case Kind.Hammock: TickHammock(dt); break;
                default: MoreTick(dt); break;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            if (kind == Kind.Cat)
            {
                // asleep, it breathes; in the dark it is a darker shape
                float b = catState == 0 ? Mathf.Sin(Time.time * 2.2f) : 0f;
                sr.transform.localScale = new Vector3(1f, 1f + b * 0.05f, 1f);
                sr.color = room != null && room.LampsOut ? new Color(0.58f, 0.6f, 0.78f) : Color.white;
            }
            else if (kind == Kind.Wheel) sr.transform.localRotation = Quaternion.Euler(0f, 0f, -Angle);
            else if (kind == Kind.Hammock && extra != null) extra.transform.localRotation = Quaternion.Euler(0f, 0f, swing);
            MoreLate();
            // in reach: a white outline round it (round the thing in the picture, for the cauldron and the lamp)
            Sprite outline = null;
            if (!Busy)
                switch (kind)
                {
                    case Kind.Cat: outline = catOutline != null && catFrame < catOutline.Length ? catOutline[catFrame] : null; break;
                    case Kind.Wheel: outline = wheelOutline; break;
                    case Kind.Hammock: outline = hammockOutline; break;
                    default: outline = Baked ? kindOutline[(int)kind] : OutlineOf(sr.sprite); break;
                }
            if (glow != null)
            {
                if (Baked)
                {
                    if (room != null) glow.transform.position = room.transform.position + new Vector3(5.5f, 6.5f, -0.1f);       // where the room picture's middle is
                    glow.transform.localScale = Vector3.one; glow.transform.localRotation = Quaternion.identity; glow.flipX = false;
                }
                else
                {
                    glow.transform.localPosition = sr.transform.localPosition;
                    glow.transform.localRotation = sr.transform.localRotation;
                    glow.transform.localScale = sr.transform.localScale;
                    glow.flipX = sr.flipX;
                }
            }
            InReach.Show(glow, outline, this);
        }

        /// <summary>The keeper of the house says something about it now and then (a line over their head).</summary>
        void KeeperSays(string[] lines, float chance = 0.7f)
        {
            if (sayCool > 0f || lines == null || lines.Length == 0 || Random.value > chance) return;
            foreach (var e in World.Entities)
                if ((e is VillagerHome || e is Elder) && !e.Dead && VillageInterior.RoomAt(World, new Vector2(e.pos.x, e.pos.y + 0.3f)) == room)
                {
                    sayCool = 4f;
                    Fx.Say(e, lines[Random.Range(0, lines.Length)], new Color(1f, 0.9f, 0.68f), 2.4f);
                    return;
                }
        }

        Vector2 RoomAt(float x, float y) => room != null ? room.At(x, y) : pos;

        // ------------------------------------------------------------------ the cat

        static readonly string[] Purrs = { "Mrrp.", "Mew?", "Prrrr...", "Meow.", "Mrrow?", "...prrp." };
        static readonly string[] Grumbles = { "Mrr!", "Hrrm.", "Mew!!" };
        /// <summary>Where the cat sleeps (floor-space pixels of the ground floor): by the fire, on the armchair.</summary>
        static readonly Vector2[] CatSpots = { new Vector2(171f, 0f), new Vector2(151f, 16f) };

        /// <summary>0 asleep, 1 stretching, 2 sitting up, 3 on its way somewhere else.</summary>
        int catState;
        float catT;
        [System.NonSerialized] public int pokes;
        public int CatSpot { get; private set; }
        bool leaving;
        readonly System.Collections.Generic.List<(Vector2 to, bool hop)> path = new System.Collections.Generic.List<(Vector2, bool)>();
        Vector2 hopFrom; float hopT;

        void PetCat()
        {
            pokes++;
            if (pokes >= 10)
            {
                // enough is enough: it gets up and goes to sleep somewhere else
                leaving = true; catState = 2; catT = 0.7f; SetCat(2);
                Meow("Mrrow!", 0.8f);
            }
            else { catState = 1; catT = 0.45f; SetCat(1); Sfx.Play("land", pos, 0.3f, 1.6f); }
        }

        void Meow(string line, float pitch = 1f)
        {
            Sfx.Play("meow", pos, 0.8f, pitch * Random.Range(0.92f, 1.12f));
            Fx.Text(new Vector2(pos.x, pos.y + 1.15f), line, new Color(0.88f, 0.9f, 1f), 1.8f);
        }

        int catFrame;
        void SetCat(int frame) { catFrame = frame; if (sr != null) sr.sprite = Frame(frame); }

        void TickCat(float dt)
        {
            prevPos = pos;
            switch (catState)
            {
                case 1:     // a long stretch
                    catT -= dt;
                    if (catT <= 0f)
                    {
                        catState = 2; catT = 1.4f; SetCat(2);
                        Meow(pokes >= 7 ? Grumbles[Random.Range(0, Grumbles.Length)] : Purrs[Random.Range(0, Purrs.Length)]);
                    }
                    break;
                case 2:     // sitting up, looking at whoever woke it
                    catT -= dt;
                    var pl = World.Player;
                    if (sr != null && pl != null) sr.flipX = pl.pos.x > pos.x;
                    if (catT > 0f) break;
                    if (!leaving) { catState = 0; SetCat(0); if (sr != null) sr.flipX = false; break; }
                    path.Clear();
                    if (CatSpot == 0) { path.Add((RoomAt(162f, 0f), false)); path.Add((RoomAt(CatSpots[1].x, CatSpots[1].y), true)); }
                    else { path.Add((RoomAt(160f, 0f), true)); path.Add((RoomAt(CatSpots[0].x, CatSpots[0].y), false)); }
                    catState = 3; hopT = -1f;
                    break;
                case 3:     // off to the other spot: a walk, a hop
                    if (path.Count == 0)
                    {
                        CatSpot ^= 1; pokes = 0; leaving = false; catState = 0; SetCat(0);
                        if (sr != null) sr.flipX = false;
                        break;
                    }
                    var (to, hop) = path[0];
                    if (sr != null) sr.flipX = to.x > pos.x;
                    if (hop)
                    {
                        if (hopT < 0f) { hopT = 0f; hopFrom = pos; Sfx.Play("jump", pos, 0.25f, 1.8f); }
                        hopT += dt / 0.4f;
                        float k = Mathf.Clamp01(hopT);
                        pos = Vector2.Lerp(hopFrom, to, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 0.55f);
                        SetCat(3);
                        if (k >= 1f) { pos = to; path.RemoveAt(0); hopT = -1f; Sfx.Play("land", pos, 0.3f, 1.7f); }
                    }
                    else
                    {
                        pos = new Vector2(Mathf.MoveTowards(pos.x, to.x, 1.6f * dt), to.y);
                        SetCat(3 + ((int)(t * 8f)) % 2);
                        if (Mathf.Abs(pos.x - to.x) < 0.001f) path.RemoveAt(0);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ the rope wheel

        public float Angle { get; private set; }
        float spin, creakAt;
        static readonly string[] WheelLines = { "Easy! That's a day's twist.", "Mind your fingers.", "She spins true, eh?" };

        void SpinWheel()
        {
            spin = Mathf.Min(900f, Mathf.Max(spin, 0f) + 620f);
            Sfx.Play("creak", pos, 0.7f);
            KeeperSays(WheelLines);
        }

        void TickWheel(float dt)
        {
            if (spin <= 0f) return;
            Angle += spin * dt;
            spin = Mathf.Max(0f, spin - (90f + spin * 0.35f) * dt);
            // a creak with every half turn while it still goes round with a will
            if (Angle >= creakAt) { creakAt = Angle + 180f; if (spin > 160f) Sfx.Play("creak", pos, 0.4f, 0.9f + spin / 1800f); }
        }

        // ------------------------------------------------------------------ the cauldron

        float stirred, bubbleT;
        public bool Stirring => stirred > 0f;
        static readonly string[] BrewLines = { "Hands off my brew!", "Clockwise! Always clockwise.", "...was it meant to go green?" };
        static readonly Color[] Brews = { new Color(0.55f, 1f, 0.6f), new Color(1f, 0.55f, 0.85f), new Color(0.6f, 0.75f, 1f), new Color(1f, 0.85f, 0.4f) };

        void Stir()
        {
            stirred = 2.5f;
            Sfx.Play("bubble", pos, 0.8f, Random.Range(0.9f, 1.1f));
            Fx.Motes(RoomAt(200f, 31f), new Color(0.6f, 1f, 0.7f, 0.9f), 12, 0.45f, 0.12f);
            Fx.Smoke(RoomAt(200f, 34f), 3);
            var c = Brews[Random.Range(0, Brews.Length)];
            Fx.Motes(RoomAt(200f, 38f), c, 10, 0.35f, 0.35f);
            if (Random.value < 0.2f) Fx.Text(RoomAt(200f, 46f), "blub!", c, 1.2f);
            KeeperSays(BrewLines);
        }

        void TickCauldron(float dt)
        {
            if (stirred <= 0f) return;
            stirred -= dt; bubbleT -= dt;
            if (bubbleT <= 0f) { bubbleT = 0.1f; Fx.Motes(RoomAt(200f + Random.Range(-8f, 8f), 31f), new Color(0.6f, 1f, 0.7f, 0.9f), 2, 0.1f, 0.05f); }
        }

        // ------------------------------------------------------------------ the hammock

        PlayerController sleeper;
        float hamT, hamRest, swing;
        bool snored1, snored2;
        static readonly string[] HammockLines = { "Ha! Every time.", "It's for napping, not falling.", "Told you it bites." };
        /// <summary>The hammock hangs from the middle of its two hooks (floor-space pixels upstairs).</summary>
        const float HookX = 71.5f, HookY = 43.5f;
        Vector2 Hooks => RoomAt(HookX, VillageInterior.UpperYPx + HookY);

        void LieDown(PlayerController p)
        {
            sleeper = p;
            p.restingIn = this;
            p.facing = 1;
            hamT = 0f; snored1 = snored2 = false;
            if (sr != null) sr.enabled = false;
            if (extra != null) extra.enabled = true;
            Sfx.Play("creak", pos, 0.6f, 0.85f);
            PlaceSleeper(true);
        }

        void PlaceSleeper(bool snap)
        {
            float a = swing * Mathf.Deg2Rad;
            var f = new Vector2(8f, -8.5f) / 16f;          // where the sleeper's feet lie, from the hooks
            var at = Hooks + new Vector2(f.x * Mathf.Cos(a) - f.y * Mathf.Sin(a), f.x * Mathf.Sin(a) + f.y * Mathf.Cos(a));
            sleeper.prevPos = snap ? at : sleeper.pos;
            sleeper.pos = at;
            sleeper.vel = Vector2.zero;
            sleeper.restAngle = 90f + swing;
        }

        void TickHammock(float dt)
        {
            if (sleeper == null)
            {
                if (hamRest > 0f) { hamRest -= dt; if (hamRest <= 0f) { if (extra != null) extra.enabled = false; if (sr != null) sr.enabled = true; } }
                swing = Mathf.MoveTowards(swing, 0f, 60f * dt);
                return;
            }
            if (sleeper.IsDead || sleeper.restingIn != this) { sleeper = null; hamRest = 0.4f; return; }
            hamT += dt;
            // a gentle sway, a snore or two; then it swings wider and wider, and over it goes
            float amp = hamT < 2.3f ? 6f : Mathf.Lerp(6f, 34f, (hamT - 2.3f) / 0.9f);
            swing = amp * Mathf.Sin(hamT * 2.8f);
            if (!snored1 && hamT > 0.9f) { snored1 = true; Fx.Text(Hooks + new Vector2(-0.3f, 0.2f), "z", new Color(0.85f, 0.9f, 1f), 1.2f); }
            if (!snored2 && hamT > 1.7f) { snored2 = true; Fx.Text(Hooks + new Vector2(-0.1f, 0.4f), "Zz", new Color(0.85f, 0.9f, 1f), 1.2f); }
            if (hamT < 3.2f) { PlaceSleeper(false); return; }
            // tipped out
            var p = sleeper;
            sleeper = null;
            p.restingIn = null;
            p.restAngle = 0f;
            VillageInterior.Place(p, Hooks + new Vector2(0.35f, -0.75f));
            p.motor.vel = new Vector2(1.2f, 1.5f);
            Sfx.Play("thud", p.pos, 0.8f);
            Fx.Say(p, "Oof!", new Color(1f, 0.92f, 0.8f), 1.4f);
            hamRest = 0.6f;
            KeeperSays(HammockLines, 0.85f);
        }

        // ------------------------------------------------------------------ the lamps

        void SwitchLamps()
        {
            if (room == null) return;
            bool outNow = !room.LampsOut;
            room.SetLampsOut(outNow);
            Sfx.Play("switch", pos, 0.8f);
            if (outNow) { Fx.Smoke(RoomAt(128f, 82f), 2); Fx.Smoke(RoomAt(96f, VillageInterior.UpperYPx + 82f), 2); }
        }

        protected override void OnDespawn()
        {
            if (sleeper != null && sleeper.restingIn == this) { sleeper.restingIn = null; sleeper.restAngle = 0f; }
            sleeper = null;
            MoreDespawn();
            base.OnDespawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
