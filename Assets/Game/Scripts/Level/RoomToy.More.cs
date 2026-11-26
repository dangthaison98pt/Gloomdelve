using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The rest of the things in the houses that answer when touched: Hilde's bellows and anvil, Oswin's globe and
    /// telescope, Pip's owl, clock and birdcage, Bera's row of lamps and the great lamp, the Elder's candles, cup and
    /// banner, the bed at home (a short nap, and a dream) and a coil of rope on Tam's floor.
    /// Places are floor-space pixels of the room picture (ArtGenerator.Village, Room), like the rest of the room.
    /// </summary>
    public partial class RoomToy
    {
        [Tooltip("More pictures of the things in the houses (artNames gives their names; artOutline their outlines, or null).")] public Sprite[] art;
        public Sprite[] artOutline;
        public string[] artNames;
        [Tooltip("By Kind: the outline of a thing painted into the room picture (a sheet the size of the picture; null = it has a picture of its own).")] public Sprite[] kindOutline;

        Sprite Art(string n)
        {
            if (artNames != null && art != null)
                for (int i = 0; i < artNames.Length && i < art.Length; i++) if (artNames[i] == n) return art[i];
            return null;
        }

        Sprite OutlineOf(Sprite s)
        {
            if (s == null || art == null || artOutline == null) return null;
            for (int i = 0; i < art.Length && i < artOutline.Length; i++) if (art[i] == s) return artOutline[i];
            return null;
        }

        /// <summary>The thing is painted into the room picture: its outline is a sheet the size of the picture.</summary>
        bool Baked => kindOutline != null && (int)kind < kindOutline.Length && kindOutline[(int)kind] != null;

        /// <summary>A picture of its own, put at a spot of the room picture.</summary>
        SpriteRenderer Child(string name, Sprite s, float x, float y, int order = 6)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            if (sr != null) { r.sharedMaterial = sr.sharedMaterial; r.sortingLayerID = sr.sortingLayerID; }
            r.sortingOrder = order;
            Put(go.transform, x, y);
            return r;
        }

        void Put(Transform tr, float x, float y) { var w = RoomAt(x, y); tr.position = new Vector3(w.x, w.y, tr.position.z); }

        static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        /// <summary>Once the room is found: the pictures this thing needs, where they go.</summary>
        void Build()
        {
            if (sr == null) return;
            switch (kind)
            {
                case Kind.Globe: sr.sprite = Art("globe_0"); Put(sr.transform, 199.5f, 29.5f); break;
                case Kind.Owl: sr.sprite = Art("owl_0"); Put(sr.transform, 183.5f, 36f); break;
                case Kind.Clock:
                    sr.sprite = Art("clock"); Put(sr.transform, 160.5f, 44.5f);
                    minuteHand = Child("Minute", Art("hand_min"), 160.5f, 44.5f, 7);
                    hourHand = Child("Hour", Art("hand_hour"), 160.5f, 44.5f, 7);
                    break;
                case Kind.Birdcage: bird = Child("Bird", Art("bird_0"), 140.5f, 72f, 7); break;
                case Kind.LampRow:
                    lampsOff = new SpriteRenderer[7];
                    for (int i = 0; i < 7; i++) { lampsOff[i] = Child("Off" + i, Art("lamp_off"), 74 + i * 22 + 0.5f, 74 + (i % 2) * 8 + 3.5f, 7); lampsOff[i].enabled = false; }
                    break;
                case Kind.GreatLamp:
                    flash = Child("Flash", Art("white"), 0f, 0f, 45);
                    flash.transform.position = room.transform.position + new Vector3(5.5f, 6.5f, -0.5f);
                    flash.transform.localScale = new Vector3(16f / 0.25f, 15f / 0.25f, 1f);
                    flash.enabled = false;
                    break;
                case Kind.Candles:
                    flames = new SpriteRenderer[3];
                    for (int i = 0; i < 3; i++) flames[i] = Child("Flame" + i, Art("flame_0"), 208f + i * 5f, VillageInterior.UpperYPx + 34f, 7);
                    break;
                case Kind.Banner: sr.sprite = Art("banner_0"); Put(sr.transform, 108f, 90f); break;
                case Kind.Coil: sr.sprite = Art("coil"); Put(sr.transform, 160.5f, 0f); coilFrom = coilTo = pos; break;
            }
        }

        bool MoreBusy =>
            (kind == Kind.Globe && globeT >= 0f) || (kind == Kind.Telescope && scopeT > 0f) || (kind == Kind.Owl && owlT > 0f) ||
            (kind == Kind.Clock && clockT >= 0f) || (kind == Kind.Birdcage && birdT >= 0f) || (kind == Kind.LampRow && rowT >= 0f) ||
            (kind == Kind.GreatLamp && flashT > 0f) || (kind == Kind.Candles && relightT > 0f) || (kind == Kind.Banner && bannerT > 0f) ||
            (kind == Kind.Coil && coilT >= 0f) || (kind == Kind.Anvil && answerT > 0f) || (kind == Kind.Bed && napT > 0f);

        string MorePrompt()
        {
            switch (kind)
            {
                case Kind.Bellows: return "[enter]  Work the bellows";
                case Kind.Anvil: return "[enter]  Strike the anvil";
                case Kind.Globe: return "[enter]  Spin the globe";
                case Kind.Telescope: return "[enter]  Look through it";
                case Kind.Owl: return "[enter]  Poke the owl";
                case Kind.Clock: return "[enter]  Wind the clock";
                case Kind.Birdcage: return "[enter]  Open the cage";
                case Kind.LampRow: return "[enter]  Snuff the lamps";
                case Kind.GreatLamp: return "[enter]  Touch the great lamp";
                case Kind.Candles: return "[enter]  Blow out a candle";
                case Kind.Cup: return "[enter]  Drink";
                case Kind.Banner: return "[enter]  Touch the banner";
                case Kind.Bed: return "[enter]  Sleep";
                case Kind.Coil: return "[enter]  Kick the coil";
            }
            return null;
        }

        void MoreInteract(PlayerController p)
        {
            switch (kind)
            {
                case Kind.Bellows: Bellows(); break;
                case Kind.Anvil: Strike(); break;
                case Kind.Globe: SpinGlobe(); break;
                case Kind.Telescope: LookThrough(p); break;
                case Kind.Owl: PokeOwl(); break;
                case Kind.Clock: WindClock(); break;
                case Kind.Birdcage: OpenCage(); break;
                case Kind.LampRow: rowT = 0f; break;
                case Kind.GreatLamp: TouchGreatLamp(); break;
                case Kind.Candles: BlowCandle(); break;
                case Kind.Cup: Drink(p); break;
                case Kind.Banner: bannerT = 2f; Sfx.Play("throw", pos, 0.5f, 0.6f); KeeperSays(BannerLines, 0.6f); break;
                case Kind.Bed: Sleep(p); break;
                case Kind.Coil: KickCoil(p); break;
            }
        }

        void MoreTick(float dt)
        {
            switch (kind)
            {
                case Kind.Anvil: TickAnvil(dt); break;
                case Kind.Globe: TickGlobe(dt); break;
                case Kind.Telescope: TickScope(dt); break;
                case Kind.Owl: TickOwl(dt); break;
                case Kind.Clock: TickClock(dt); break;
                case Kind.Birdcage: TickBird(dt); break;
                case Kind.LampRow: TickRow(dt); break;
                case Kind.GreatLamp: if (flashT > 0f) flashT -= dt; break;
                case Kind.Candles: TickCandles(dt); break;
                case Kind.Banner: if (bannerT > 0f) bannerT -= dt; break;
                case Kind.Coil: TickCoil(dt); break;
                case Kind.Bed: TickNap(dt); break;
            }
        }

        /// <summary>What moves on screen each frame (pictures, hands, the flash, the telescope's view).</summary>
        void MoreLate()
        {
            switch (kind)
            {
                case Kind.Clock:
                    {
                        var now = System.DateTime.Now;
                        float min = now.Minute + now.Second / 60f, hr = (VillageClock.Hour % 12) + min / 60f;
                        // whole turns backwards - three for the minute hand, one for the hour hand - so both stop just where
                        // they were (a part of a turn would leave a hand off and make it jump back at the end)
                        float back = clockT >= 0f ? EaseOut(clockT / ClockSpin) : 0f;
                        if (minuteHand != null) minuteHand.transform.localRotation = Quaternion.Euler(0f, 0f, -min * 6f + 1080f * back);
                        if (hourHand != null) hourHand.transform.localRotation = Quaternion.Euler(0f, 0f, -hr * 30f + 360f * back);
                        break;
                    }
                case Kind.GreatLamp:
                    if (flash != null) { flash.enabled = flashT > 0f; flash.color = new Color(1f, 0.97f, 0.86f, 0.9f * Mathf.Clamp01(flashT / FlashTime)); }
                    break;
                case Kind.Candles:
                    if (flames != null)
                        for (int i = 0; i < 3; i++)
                        {
                            flames[i].enabled = lit[i];
                            flames[i].sprite = Art(((int)(Time.time * 6f + i * 1.7f)) % 2 == 0 ? "flame_0" : "flame_1");
                        }
                    break;
                case Kind.Banner:
                    if (sr != null) sr.sprite = Art("banner_" + (bannerT > 0f ? new[] { 0, 1, 2, 1 }[((int)(bannerT * 8f)) % 4] : 0));
                    break;
                case Kind.Coil:
                    if (sr != null) { float sq = coilT >= 0f ? Mathf.Sin(Mathf.Clamp01(coilT / 0.5f) * Mathf.PI) * 0.15f : 0f; sr.transform.localScale = new Vector3(1f + sq, 1f - sq, 1f); }
                    break;
                case Kind.Telescope: LateScope(); break;
            }
        }

        void MoreDespawn()
        {
            if (scopeHolder != null && scopeHolder.restingIn == this) scopeHolder.restingIn = null;
            if (napper != null && napper.restingIn == this) napper.restingIn = null;
            if (scopeRoot != null) Destroy(scopeRoot);
        }

        // ------------------------------------------------------------------ Hilde's forge

        static readonly string[] BellowsLines = { "Easy, you'll melt the roof!", "Ha! You've got the arm for it.", "Not so hard, not so hard!" };

        void Bellows()
        {
            if (room != null) room.Flare();
            Sfx.Play("bellows", pos, 0.8f, Random.Range(0.9f, 1.1f));
            Fx.Embers(RoomAt(193f, 20f), 10);
            Fx.Smoke(RoomAt(193f, 44f), 2);
            KeeperSays(BellowsLines);
        }

        readonly System.Collections.Generic.List<float> strikes = new System.Collections.Generic.List<float>();
        float answerT;
        /// <summary>Hilde has answered a rhythm struck on her anvil (three strikes, quick).</summary>
        public bool Answered { get; private set; }

        void Strike()
        {
            Sfx.Play("clang", pos, 0.7f, Random.Range(0.96f, 1.04f));
            Fx.Motes(RoomAt(149f, 17f), new Color(1f, 0.75f, 0.3f), 6, 0.3f, 0.1f);
            strikes.Add(t);
            strikes.RemoveAll(s => t - s > 1.6f);
            if (strikes.Count >= 3) { strikes.Clear(); answerT = 0.7f; }
        }

        void TickAnvil(float dt)
        {
            if (answerT <= 0f) return;
            float was = answerT;
            answerT -= dt;
            // from behind the counter, Hilde strikes it back: two blows with her own hammer
            if (was > 0.35f && answerT <= 0.35f) { Sfx.Play("clang", pos, 0.6f, 0.8f); KeeperLine("clang!"); }
            if (answerT <= 0f) { Sfx.Play("clang", pos, 0.6f, 0.75f); Answered = true; }
        }

        /// <summary>The keeper says this, now (whatever was said a moment ago).</summary>
        void KeeperLine(string line) { sayCool = 0f; KeeperSays(new[] { line }, 1f); }

        // ------------------------------------------------------------------ Oswin's map room

        float globeT = -1f, globePhase, globeTarget;
        const float GlobeSpin = 2.6f;
        public int GlobeFrame { get; private set; }
        public int GlobeTurns { get; private set; }

        void SpinGlobe()
        {
            // a few turns, and it always comes to rest with the burned hole facing out
            globeT = 0f;
            globePhase %= 360f;
            globeTarget = (Mathf.Floor(globePhase / 360f) + 3f) * 360f;
            Sfx.Play("creak", pos, 0.4f, 1.6f);
        }

        void TickGlobe(float dt)
        {
            if (globeT < 0f) return;
            globeT += dt;
            float k = EaseOut(globeT / GlobeSpin);
            float ph = Mathf.Lerp(globePhase, globeTarget, k);
            GlobeFrame = ((int)(ph / 45f)) % 8;
            if (sr != null) sr.sprite = Art("globe_" + GlobeFrame);
            if (globeT >= GlobeSpin) { globeT = -1f; globePhase = 0f; GlobeFrame = 0; GlobeTurns++; if (sr != null) sr.sprite = Art("globe_0"); }
        }

        float scopeT;
        int scopeLooks;
        PlayerController scopeHolder;
        GameObject scopeRoot;
        SpriteRenderer scopeBack, scopeView, scopeLight, scopeClimber;
        const float ScopeTime = 4.2f, ScopeFade = 0.25f;
        public bool Viewing => scopeT > 0f;
        bool showLight, showClimber;
        /// <summary>Tests: what this look shows ("light", "climber", "both").</summary>
        public string Sight => showLight && showClimber ? "both" : showClimber ? "climber" : "light";
        /// <summary>Tests: the sight the next look shows (0 the light on the peak, 1 the climber, 2 both); -1 = by chance.</summary>
        [System.NonSerialized] public int nextSight = -1;

        /// <summary>The player asks to stop (interact or jump): the telescope's view fades out at once.</summary>
        public void LetGo(PlayerController p)
        {
            if (kind == Kind.Telescope && scopeT > ScopeFade && scopeHolder == p) scopeT = ScopeFade;
        }

        void LookThrough(PlayerController p)
        {
            scopeHolder = p;
            p.restingIn = this; p.restAngle = 0f;           // still, at the eyepiece
            scopeT = ScopeTime;
            scopeLooks++;
            Sfx.Play("creak", pos, 0.4f, 1.3f);
            if (scopeRoot == null)
            {
                scopeRoot = new GameObject("TelescopeView");
                SpriteRenderer Layer(string n, Sprite s, int order)
                {
                    var go = new GameObject(n); go.transform.SetParent(scopeRoot.transform, false);
                    var r = go.AddComponent<SpriteRenderer>(); r.sprite = s; r.sortingOrder = order;
                    if (sr != null) { r.sharedMaterial = sr.sharedMaterial; r.sortingLayerID = sr.sortingLayerID; }
                    return r;
                }
                scopeBack = Layer("Dark", Art("white"), 300); scopeBack.color = Color.black;
                scopeView = Layer("View", Art("scope_view"), 301);
                scopeLight = Layer("Light", Art("scope_light"), 302);
                scopeClimber = Layer("Climber", Art("scope_climber"), 302);
            }
            scopeRoot.SetActive(true);
            // what is out there this time, by chance: the light on the peak, someone climbing with a lantern, or both
            int sight = nextSight >= 0 ? nextSight : Random.Range(0, 5) switch { 0 or 1 => 0, 2 or 3 => 1, _ => 2 };
            nextSight = -1;
            showLight = sight != 1; showClimber = sight != 0;
            scopeLight.enabled = showLight;
            scopeClimber.enabled = showClimber;
        }

        void TickScope(float dt)
        {
            if (scopeT <= 0f) return;
            scopeT -= dt;
            if (scopeT > 0f && scopeHolder != null && !scopeHolder.IsDead) return;
            scopeT = 0f;
            if (scopeHolder != null && scopeHolder.restingIn == this) scopeHolder.restingIn = null;
            scopeHolder = null;
            if (scopeRoot != null) scopeRoot.SetActive(false);
        }

        void LateScope()
        {
            if (scopeRoot == null || !scopeRoot.activeSelf) return;
            var cam = Camera.main;
            if (cam == null) return;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            scopeRoot.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            float s = h * 0.86f / 8f;                         // the view is 128 px, 8 units: most of the screen's height
            scopeView.transform.localScale = new Vector3(s, s, 1f);
            scopeBack.transform.localScale = new Vector3(w * 1.2f / 0.25f, h * 1.2f / 0.25f, 1f);
            float a = Mathf.Clamp01(Mathf.Min(ScopeTime - scopeT, scopeT) / ScopeFade);
            scopeBack.color = new Color(0f, 0f, 0f, a);
            scopeView.color = new Color(1f, 1f, 1f, a);
            // the light on the peak, breathing; the climber a little higher every moment
            scopeLight.transform.localPosition = new Vector3(0f, 2f * s, 0f);
            scopeLight.transform.localScale = new Vector3(s, s, 1f);
            scopeLight.color = new Color(1f, 1f, 1f, a * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.4f))));
            float up = (ScopeTime - scopeT) / ScopeTime;
            scopeClimber.transform.localPosition = new Vector3((-0.75f + up * 0.35f) * s, (0.4f + up * 0.6f) * s, 0f);
            scopeClimber.transform.localScale = new Vector3(s, s, 1f);
            scopeClimber.color = new Color(1f, 1f, 1f, a);
        }

        // ------------------------------------------------------------------ Pip's Oddments

        float owlT;
        int owlPokes;
        public bool Hooed { get; private set; }
        static readonly string[] OwlLines = { "Don't mind him.", "He's been like that since I bought him." };

        void PokeOwl()
        {
            owlPokes++;
            owlT = 0.9f;
            if (sr != null) sr.sprite = Art("owl_1");          // the head goes right round
            Sfx.Play("creak", pos, 0.3f, 2f);
            if (owlPokes >= 5)
            {
                owlPokes = 0; Hooed = true;
                Sfx.Play("hoo", pos, 0.8f);
                Fx.Text(RoomAt(183.5f, 52f), "Hoo.", new Color(0.88f, 0.9f, 1f), 1.8f);
                KeeperLine("It's stuffed! I swear it's stuffed.");
            }
            else KeeperSays(OwlLines, 0.4f);
        }

        void TickOwl(float dt)
        {
            if (owlT <= 0f) return;
            owlT -= dt;
            if (sr == null) return;
            sr.sprite = Art(owlT > 0.3f ? "owl_1" : owlT > 0.15f ? "owl_2" : "owl_0");     // round, then back with a blink
            if (owlT <= 0f) sr.sprite = Art("owl_0");
        }

        float clockT = -1f, tickT;
        const float ClockSpin = 1.8f;
        SpriteRenderer minuteHand, hourHand;

        /// <summary>Tests: where the clock's hands point (the minute hand's angle, the hour hand's).</summary>
        public Vector2 HandAngles => new Vector2(minuteHand != null ? minuteHand.transform.localEulerAngles.z : 0f, hourHand != null ? hourHand.transform.localEulerAngles.z : 0f);

        void WindClock() { clockT = 0f; KeeperSays(new[] { "It does that. Don't ask." }, 0.9f); }

        void TickClock(float dt)
        {
            if (clockT < 0f) return;
            clockT += dt; tickT -= dt;
            if (tickT <= 0f) { tickT = Mathf.Lerp(0.05f, 0.2f, clockT / ClockSpin); Sfx.Play("tick", pos, 0.5f); }
            if (clockT >= ClockSpin) clockT = -1f;
        }

        float birdT = -1f, flapT;
        SpriteRenderer bird;
        static readonly string[] BirdLines = { "Back in, you.", "She always comes back." };
        /// <summary>How far the bird is from its cage now (in cells).</summary>
        public float BirdOut => bird != null ? Vector2.Distance(bird.transform.position, RoomAt(140.5f, 72f)) : 0f;

        void OpenCage() { birdT = 0f; Sfx.Play("chirp", pos, 0.7f); KeeperSays(BirdLines, 0.6f); }

        void TickBird(float dt)
        {
            if (birdT < 0f || bird == null) return;
            birdT += dt;
            Vector2 cage = RoomAt(140.5f, 72f), c = RoomAt(130f, 58f);
            float rx = 62f / 16f, ry = 24f / 16f, a0 = 60f * Mathf.Deg2Rad;
            Vector2 P(float a) => c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            Vector2 at;
            if (birdT < 0.4f) at = Vector2.Lerp(cage, P(a0), birdT / 0.4f);
            else if (birdT < 3f) at = P(a0 + (birdT - 0.4f) / 2.6f * Mathf.PI * 2f);
            else if (birdT < 3.5f) at = Vector2.Lerp(P(a0), cage, (birdT - 3f) / 0.5f);
            else { at = cage; birdT = -1f; Sfx.Play("chirp", pos, 0.5f, 1.2f); }
            Vector3 was = bird.transform.position;
            bird.transform.position = new Vector3(at.x, at.y, was.z);
            if (Mathf.Abs(at.x - was.x) > 0.001f) bird.flipX = at.x < was.x;
            flapT += dt;
            bird.sprite = Art(birdT < 0f ? "bird_0" : ((int)(flapT * 10f)) % 2 == 0 ? "bird_0" : "bird_1");
        }

        // ------------------------------------------------------------------ Bera's lamp house

        float rowT = -1f;
        SpriteRenderer[] lampsOff;
        const float RowStep = 0.18f;
        static readonly string[] RowLines = { "Every one, every night.", "Mind the wicks!" };
        /// <summary>How many of the row of lamps are out now.</summary>
        public int LampsDark { get { int n = 0; if (lampsOff != null) foreach (var l in lampsOff) if (l.enabled) n++; return n; } }

        void TickRow(float dt)
        {
            if (rowT < 0f || lampsOff == null) return;
            float was = rowT;
            rowT += dt;
            if (was == 0f) KeeperSays(RowLines, 0.6f);
            // out one by one, left to right; a breath; lit again one by one
            float relight = RowStep * 7f + 0.6f;
            for (int i = 0; i < 7; i++)
            {
                float off = i * RowStep, on = relight + i * RowStep;
                if (was < off && rowT >= off) { lampsOff[i].enabled = true; Sfx.Play("switch", pos, 0.4f, 0.7f); Fx.Smoke((Vector2)lampsOff[i].transform.position + new Vector2(0f, 0.3f), 1); }
                if (was < on && rowT >= on) { lampsOff[i].enabled = false; Sfx.Play("switch", pos, 0.4f, 1.2f); }
            }
            if (rowT > relight + 7f * RowStep + 0.2f) rowT = -1f;
        }

        float flashT;
        const float FlashTime = 0.8f;
        SpriteRenderer flash;
        public bool Flashing => flashT > 0f;

        void TouchGreatLamp()
        {
            flashT = FlashTime;
            Sfx.Play("idol", pos, 0.7f, 0.6f);
            Fx.Shake(0.2f);
            KeeperLine("Not that one! Never that one.");
        }

        // ------------------------------------------------------------------ the village hall

        SpriteRenderer[] flames;
        readonly bool[] lit = { true, true, true };
        float relightT, restT;
        public bool CandleLit(int i) => lit[i];

        void BlowCandle()
        {
            int i = System.Array.IndexOf(lit, true);
            if (i < 0) return;
            lit[i] = false;
            Sfx.Play("throw", pos, 0.4f, 1.6f);
            if (flames != null) Fx.Smoke((Vector2)flames[i].transform.position + new Vector2(0f, 0.2f), 1);
            if (i == 2) relightT = 1f;          // the last one will not stay out
        }

        void TickCandles(float dt)
        {
            if (relightT > 0f)
            {
                relightT -= dt;
                if (relightT <= 0f)
                {
                    lit[2] = true; restT = 4f;
                    Sfx.Play("flame", pos, 0.3f, 1.4f);
                    if (flames != null) Fx.Embers(flames[2].transform.position, 3);
                    KeeperLine("Some lights will not go out.");
                }
            }
            else if (restT > 0f) { restT -= dt; if (restT <= 0f) { lit[0] = lit[1] = true; } }
        }

        public int Drunk { get; private set; }
        static readonly string[] CupLines = { "That one was not for you.", "...it was poured for someone else." };

        void Drink(PlayerController p)
        {
            Drunk++;
            Sfx.Play("gulp", pos, 0.8f);
            Fx.Say(p, "Warm. Still warm.", new Color(1f, 0.94f, 0.82f), 2f);
            KeeperSays(CupLines, 0.85f);
        }

        float bannerT;
        static readonly string[] BannerLines = { "Mm. There is no wind in here." };

        // ------------------------------------------------------------------ home: the bed

        /// <summary>
        /// A short nap: the screen goes dark a moment, and on waking the player remembers a dream. (The hour stays the
        /// player's own: the village keeps the time of their clock.)
        /// </summary>
        static readonly string[] Dreams =
        {
            "You dream of the mountain. Someone is climbing it with your lantern.",
            "You dream of the sun. You cannot remember what colour it was.",
            "You dream of your father at the gate. He says the same thing every time. You never hear it.",
            "You dream of a stone, warm as a hand. It is counting.",
            "You dream of the village in summer. Nobody in it has a face you know.",
        };
        float napT;
        PlayerController napper;
        public bool Napping => napT > 0f;
        public string LastDream { get; private set; }

        void Sleep(PlayerController p)
        {
            napper = p;
            p.restingIn = this; p.restAngle = 0f;
            napT = 2.2f;
            Fx.Say(p, "Zzz...", new Color(0.85f, 0.9f, 1f), 1.4f);
            ScreenFade.Through(null, 0.6f, 1f, 0.6f);
        }

        void TickNap(float dt)
        {
            if (napT <= 0f) return;
            napT -= dt;
            if (napT > 0f && napper != null && !napper.IsDead) return;
            napT = 0f;
            if (napper != null && napper.restingIn == this) napper.restingIn = null;
            napper = null;
            LastDream = Dreams[Random.Range(0, Dreams.Length)];
            var dir = GameDirector.Instance;
            if (dir != null && dir.hud != null) dir.hud.ShowNote(LastDream, HUD.ReadTime(LastDream));
        }

        // ------------------------------------------------------------------ Tam's floor: a coil of rope

        float coilT = -1f;
        Vector2 coilFrom, coilTo;
        static readonly string[] CoilLines = { "Leave my coils be!", "That's forty fathoms you're kicking." };

        void KickCoil(PlayerController p)
        {
            int dir = p.pos.x <= pos.x ? 1 : -1;
            float lo = RoomAt(128f, 0f).x, hi = RoomAt(172f, 0f).x;
            float to = pos.x + dir * 1.3f;
            if (to > hi || to < lo) to = pos.x - dir * 1.3f;
            coilFrom = pos; coilTo = new Vector2(Mathf.Clamp(to, lo, hi), pos.y);
            coilT = 0f;
            Sfx.Play("thud", pos, 0.6f, 1.3f);
            Fx.Dust(pos, 4);
            KeeperSays(CoilLines, 0.5f);
        }

        void TickCoil(float dt)
        {
            if (coilT < 0f) return;
            coilT += dt;
            float k = EaseOut(coilT / 0.5f);
            prevPos = pos;
            pos = Vector2.Lerp(coilFrom, coilTo, k) + new Vector2(0f, Mathf.Sin(Mathf.Clamp01(coilT / 0.5f) * Mathf.PI) * 0.12f);
            if (coilT >= 0.5f) { pos = coilTo; coilT = -1f; Fx.Dust(pos, 2); }
        }
    }
}
