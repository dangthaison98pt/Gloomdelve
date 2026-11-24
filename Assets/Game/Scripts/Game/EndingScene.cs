using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The scenes that say what the game is really about: the Sunstone is no sun. It is what holds Brindlemoor in
    /// one winter day, and every night on the mountain - won or lost - ends with Brann waking in bed on that same
    /// day. Only Brann remembers.
    ///  - A run that is WON (Play): Brann walks into the village at night with the stone and sets it down before
    ///    the elder. No snow melts. The stone begins to turn, and the whole picture is drawn into it in a spiral
    ///    until nothing is left - then the run's numbers, one button under them, and from there the bed.
    ///  - The first DEATH of a save (PlayWaking): the dark, then Brann awake in bed, and three sentences. Seen
    ///    once; it is kept with the save.
    ///  - Every waking after a night that ended (WakingWords): one line on the screen - after a win always the
    ///    same, after a death one of a few, in turn.
    /// The scenes are played in the village itself; the player's buttons only turn their pages or leave them,
    /// as in a menu (NextPressed / SkipPressed). Tests never see them.
    /// </summary>
    public static class EndingScene
    {
        /// <summary>The village that is being loaded is the stage of the scene after a win.</summary>
        public static bool Pending { get; private set; }
        /// <summary>... and that win was the real one: the stone was broken, and the scene is the morning after (PlayTrue).</summary>
        public static bool PendingTrue { get; private set; }
        /// <summary>A scene is running: the game's own buttons (pause, bag) are not listened to.</summary>
        public static bool Playing { get; private set; }
        /// <summary>How the night before this waking ended (told once, by the next waking in bed).</summary>
        public enum Night { None, Won, Died }
        public static Night Last;

        static RunState won;
        static string news = "";

        /// <summary>The words under the picture of the scene after a win, in order.</summary>
        public static readonly string[] Lines =
        {
            "Brann came down off the mountain with the Sunstone.",
            "\"You have brought the sun home, Brann,\" said Elder Aldous. \"Set it down. Let them all see it.\"",
            "No snow melted. The stone began to turn - and the whole night turned with it.",
        };

        /// <summary>What the people in the street cry out as the picture begins to turn (over their heads, one after another).</summary>
        public static readonly string[] Cries = { "What is happening?!", "The stone!", "Brann!", "Hold on!", "What is this?!", "The sky - look at the sky!" };

        /// <summary>The words of the scene after the stone is broken.</summary>
        public static readonly string[] TrueLines =
        {
            "The hammer came down. The Sunstone broke - and the day it had held for so long let go.",
            "Morning came to Brindlemoor. A new one.",
            "The snow ran off the roofs. Nobody remembered a winter that would not end. Brann did.",
            "\"You look as if you had not slept in a hundred years, Brann,\" said Elder Aldous.",
            "Among the shards had lain one sliver, still warm, on a cord. Brann put it on.",
        };

        /// <summary>What the street calls out on the morning after (over people's heads, as the scene goes).</summary>
        public static readonly string[] Cheers = { "The sun!", "It is warm!", "Look - the roofs!", "Brann! You are back!", "No more snow!", "Spring!" };

        /// <summary>The credits, top to bottom ("" = a gap; a line in capitals is a heading).</summary>
        public static readonly string[] CreditLines =
        {
            "BRINDLEMOOR", "The Sunstone Hunt", "", "",
            "A GAME BY", "Brindlemoor Team", "", "",
            "TYPEFACE", "EB Garamond", "SIL Open Font License", "", "",
            "MADE WITH", "Unity", "", "",
            "The stone is broken.", "One sliver of it is not.", "", "",
            "Thank you for playing.",
        };

        /// <summary>The first death of a save.</summary>
        public static readonly string[] FirstDeathLines =
        {
            "Brann died on the mountain.",
            "And woke. The same bed. The same snow on the sill. The same winter day.",
            "Nobody in Brindlemoor remembers. Only Brann does.",
        };

        /// <summary>What the first waking after the stone was brought home says.</summary>
        public const string WonWakingLine = "The same bed. Snow on the sill. Nobody else remembers the stone.";

        /// <summary>What a waking after a death says: one of these, in turn.</summary>
        public static readonly string[] DeathWakingLines =
        {
            "The same bed. The same snow. Only Brann remembers dying.",
            "Awake again. Nobody in Brindlemoor knows there was a yesterday.",
            "The mountain took Brann, and the day began again.",
            "Still winter. Still the same day. Only Brann has been counting.",
        };

        /// <summary>The line for this waking ("" = none), by how the night before ended and how many deaths there have been.</summary>
        public static string WakingWords(Night last, int deaths)
        {
            if (last == Night.Won) return WonWakingLine;
            if (last == Night.Died) return DeathWakingLines[Mathf.Abs(deaths) % DeathWakingLines.Length];
            return "";
        }

        /// <summary>This waking is the one that gets the scene of the first death: after a death, in a save that has not seen it.</summary>
        public static bool FirstDeathDue(Night last) => last == Night.Died && !SaveSystem.Records.loopSeen;

        /// <summary>
        /// The run is won: on to the village, where the scene is played. False when it cannot be (tests, a game
        /// with no village) - then the caller shows the run's numbers at once, as it used to.
        /// </summary>
        public static bool Begin(GameDirector d, RunState run, string newsText)
        {
            if (d == null || run == null || ScenarioRunner.Active || AutoTest.Active || EditorPlaytest.Active) return false;
            if (d.config.village == null || d.config.village.handcrafted == null || !Villagers.Enabled) return false;
            won = run; news = newsText ?? "";
            Time.timeScale = 1f;
            // (the new run is made in the dark: until then the hall and its numbers stay as they were)
            ScreenFade.Through(() => Load(d.config), 1.6f, 0.6f, 1.6f);
            return true;
        }

        /// <summary>
        /// The Sunstone has been broken (GameDirector.BreakSunstone): the hall fills with its light, slowly, and
        /// out of that light comes the village on the morning after. False when the scene cannot be played (tests).
        /// </summary>
        public static bool BeginTrue(GameDirector d, RunState run, string newsText, Entity stone)
        {
            if (d == null || run == null || ScenarioRunner.Active || AutoTest.Active || EditorPlaytest.Active) return false;
            if (d.config.village == null || d.config.village.handcrafted == null || !Villagers.Enabled) return false;
            won = run; news = newsText ?? "";
            Time.timeScale = 1f;
            d.StartCoroutine(Break(d, stone));
            return true;
        }

        static IEnumerator Break(GameDirector d, Entity stone)
        {
            Playing = true;
            HideHud(d);
            var ui = new Overlay(d.hud.healthText != null ? d.hud.healthText.font : null);
            ui.Bars(false);
            Vector2 at = stone != null ? stone.Center : (d.Player != null ? d.Player.Center : Vector2.zero);
            var warm = new Color(1f, 0.9f, 0.6f);
            if (stone != null && !stone.Dead) stone.Despawn();
            Fx.Sparks(at, warm, 40, 7f);
            Sfx.PlayUI("victory");
            // cracks of light first, a little at a time; then the whole hall, slowly
            const float fill = 3.2f;
            float next = 0f;
            for (float t = 0f; t < fill; t += Time.deltaTime)
            {
                float k = t / fill;
                if (t >= next) { next = t + 0.22f; Fx.Sparks(at + Random.insideUnitCircle * (0.4f + k * 2f), warm, 6, 3f + k * 5f); LevelLighting.Flash(at, warm, 4f + k * 14f, 1.2f + k, 0.5f); }
                ui.White(k * k * (3f - 2f * k));
                yield return null;
            }
            ui.White(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            PendingTrue = true;
            Load(d.config);
        }

        static void Load(GameConfig cfg)
        {
            Pending = true;
            var next = RunSession.NewRun(RunSession.RandomSeed(), false, cfg.player);
            next.inVillage = true;
            SceneManager.LoadScene(GameConst.SceneGame);
        }

        /// <summary>Where Brann stands when the scene opens: on the street, a walk away from its middle, on the mountain's side.</summary>
        public static bool Entrance(TileGrid g, out Vector2 at)
        {
            if (!VillageClock.Street(g, out at)) return false;
            at = new Vector2(Mathf.Min(g.Width - 4.5f, g.Width * 0.5f + WalkCells), at.y);
            return true;
        }

        const float WalkCells = 11f;

        /// <summary>Feeds the player the scene's steps instead of the buttons.</summary>
        class Steps : IFrameInputSource
        {
            public int x;
            /// <summary>Set to hop once (a small jump where the player stands).</summary>
            public bool hop;
            public FrameInput Next()
            {
                var f = new FrameInput { x = x, walk = true, jumpPressed = hop };
                hop = false;
                return f;
            }
        }

        /// <summary>What the buttons have done to the scene: left it, or nothing yet.</summary>
        class Buttons { public bool skip; }

        // The scene is turned like a page of a menu: on a pad the button that says "yes" goes on to the next
        // sentence and the one that says "back" leaves the scene (A and B on an Xbox pad, cross and circle on a
        // PlayStation one, A and B as printed on a Nintendo one). On the keyboard: the jump or the enter key
        // goes on, the pause key leaves. (A pad's other buttons do nothing here - its jump is not its "yes".)
        static bool FromKeyboard => UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame;
        static bool NextPressed(GameDirector d) => InputDeviceTracker.PadConfirmPressed || (FromKeyboard && d.input != null && d.input.ConfirmPressed);
        static bool SkipPressed(GameDirector d) => InputDeviceTracker.PadBackPressed || (FromKeyboard && d.input != null && d.input.PausePressed);

        /// <summary>
        /// Lets 'seconds' pass, calling 'each' with 0..1 on the way. The "back" button leaves the scene; with no
        /// 'each' (a sentence standing still to be read) the "yes" button goes on to the next thing early.
        /// </summary>
        static IEnumerator Hold(GameDirector d, Buttons b, float seconds, System.Action<float> each = null)
        {
            for (float t = 0f; t < seconds && !b.skip; t += Time.deltaTime)
            {
                each?.Invoke(Mathf.Clamp01(t / seconds));
                if (SkipPressed(d)) b.skip = true;
                else if (each == null && t > 0.6f && NextPressed(d)) break;
                yield return null;
            }
            each?.Invoke(1f);
        }

        static CanvasGroup HideHud(GameDirector d)
        {
            var group = d.hud.GetComponent<CanvasGroup>();
            if (group == null) group = d.hud.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }

        // ================================================================== a run that was won

        public static IEnumerator Play(GameDirector d)
        {
            Pending = false;
            PendingTrue = false;
            Playing = true;
            var p = d.Player;
            var w = d.world;
            var g = w.Grid;
            // (the picture opens on Brann already on the way down the street, towards the elder)
            var steps = new Steps { x = -1 };
            p.bot = steps;
            p.facing = -1;
            p.godMode = true;
            float ground = p.pos.y;
            float middle = g.Width * 0.5f;
            // (the picture sits higher than on a walk through the village: the lower band must not cover anybody's feet)
            float low = Mathf.Max(0f, ground - 3.6f);
            if (d.cameraFollow != null) d.cameraFollow.SetBounds(new Rect(0f, low, g.Width, g.Height - low));

            // the game's own screen is put away for the length of the scene
            var hudGroup = HideHud(d);
            var ui = new Overlay(d.hud.healthText != null ? d.hud.healthText.font : null);
            ui.Say(Lines[0]);

            // the elder is out in the street, waiting where the stone will be set down
            var elder = w.SpawnById("elder", new Vector2(middle - 2.6f, ground));
            if (elder != null) elder.facing = 1;

            // the stone, carried in the hands as anything is carried on the mountain: its own picture, and a light of its own
            // (the holder is the middle of the stone, so that it turns about its middle; its picture, drawn from
            // the foot like every picture in the game, hangs under that)
            var stone = new GameObject("EndingStone");
            var sr = new GameObject("Picture").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(stone.transform, false);
            var from = d.config.prefabs.Get("heartstone");
            var fromSr = from != null ? from.GetComponentInChildren<SpriteRenderer>() : null;
            if (fromSr != null) { sr.sprite = fromSr.sprite; sr.sharedMaterial = fromSr.sharedMaterial; sr.sortingLayerID = fromSr.sortingLayerID; }
            sr.sortingOrder = 50;
            Vector2 mid = sr.sprite != null ? (Vector2)sr.sprite.bounds.center : new Vector2(0f, 0.4f);
            sr.transform.localPosition = -mid;
            var warm = new Color(1f, 0.82f, 0.45f);
            var light = LevelLighting.MakeLight(stone.transform, "Glow", Light2D.LightType.Point, warm, 1.1f, 0.6f, 5f);
            // (held exactly where a thing picked up on the mountain is held: its foot at the hands)
            Vector2 Carried() => p.HoldPos + mid;
            stone.transform.position = Carried();

            var b = new Buttons();

            // 1. down the street, the stone in hand
            for (float t = 0f; t < 10f && !b.skip && p.pos.x > middle; t += Time.deltaTime)
            {
                stone.transform.position = Carried();
                if (SkipPressed(d)) b.skip = true;
                yield return null;
            }
            steps.x = 0;
            stone.transform.position = Carried();

            // 2. the elder speaks, and the stone is set down between them
            if (!b.skip) { ui.Say(Lines[1]); Sfx.PlayUI("menu_select"); }
            Vector2 from0 = stone.transform.position, to = new Vector2((p.pos.x + (elder != null ? elder.pos.x : p.pos.x - 2.6f)) * 0.5f, ground + mid.y);
            yield return Hold(d, b, 1.2f);
            yield return Hold(d, b, 1.1f, k => stone.transform.position = Vector2.Lerp(from0, to, k * k * (3f - 2f * k)));
            stone.transform.position = to;
            yield return Hold(d, b, 3.4f);

            // 3. nothing melts. A moment of that - then the stone begins to turn, and draws the whole picture into
            // itself: the view turns and closes in on it while the dark winds in from the edges in a spiral
            if (!b.skip) ui.Say(Lines[2]);
            yield return Hold(d, b, 1.8f);
            if (!b.skip) Sfx.PlayUI("idol");
            var cam = Camera.main;
            if (d.cameraFollow != null) d.cameraFollow.enabled = false;
            Vector3 camFrom = cam != null ? cam.transform.position : Vector3.zero;
            float sizeFrom = cam != null ? cam.orthographicSize : 6f;
            float pull = b.skip ? 1.2f : 4.6f;
            // whoever stands nearest sees it first: they cry out, one after another, as the street begins to turn
            var crowd = new System.Collections.Generic.List<Entity>();
            foreach (var en in w.Entities) if (en != null && !en.Dead && (en is Townsfolk || en is Elder)) crowd.Add(en);
            crowd.Sort((x, y) => Mathf.Abs(x.pos.x - to.x).CompareTo(Mathf.Abs(y.pos.x - to.x)));
            int cried = 0;
            for (float t = 0f; t < pull; t += Time.deltaTime)
            {
                float k = t / pull, e = k * k * k, s = k * k * (3f - 2f * k);
                if (!b.skip && cried < Cries.Length && cried < crowd.Count && t > 0.15f + cried * 0.5f)
                {
                    var who = crowd[cried];
                    Fx.Text(new Vector2(who.pos.x, who.pos.y + who.size.y + 0.7f + (cried % 3) * 0.55f), Cries[cried], new Color(1f, 0.95f, 0.8f), 1.7f);
                    cried++;
                }
                if (cam != null)
                {
                    cam.transform.position = Vector3.Lerp(camFrom, new Vector3(to.x, to.y, camFrom.z), s);
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, -260f * e);
                    cam.orthographicSize = Mathf.Lerp(sizeFrom, sizeFrom * 0.4f, e);
                }
                if (light != null) { light.pointLightOuterRadius = Mathf.Lerp(5f, 16f, s); light.intensity = Mathf.Lerp(1.1f, 2.4f, s); }
                stone.transform.rotation = Quaternion.Euler(0f, 0f, 900f * e);
                stone.transform.localScale = Vector3.one * (1f + 0.5f * s);
                Vector2 at = cam != null ? (Vector2)cam.WorldToViewportPoint(to) : new Vector2(0.5f, 0.5f);
                ui.Swirl(k, at, t);
                ui.Fade(1f - Mathf.Clamp01((k - 0.6f) / 0.3f));        // (the words go out with the picture)
                yield return null;
            }
            ui.Dark(1f);
            ui.Swirl(-1f, Vector2.zero, 0f);
            ui.Say("");
            ui.Bars(false);
            yield return new WaitForSecondsRealtime(b.skip ? 0.2f : 0.7f);

            // the run's numbers, with nothing behind them but the dark the picture went into
            Object.Destroy(stone);
            p.bot = null;
            Playing = false;
            Last = Night.Won;
            // (the game's own screen comes back only if the numbers are part of it)
            if (d.results != null && d.results.transform.IsChildOf(d.hud.transform)) hudGroup.alpha = 1f;
            ui.Under(d.results != null ? d.results.GetComponentInParent<Canvas>() : null);
            d.ShowWonRun(won, news);
            won = null;
            // the numbers come up out of the dark, they do not jump onto it
            var panel = d.results != null ? d.results.root : null;
            if (panel != null)
            {
                var pg = panel.GetComponent<CanvasGroup>();
                if (pg == null) pg = panel.AddComponent<CanvasGroup>();
                var scale0 = panel.transform.localScale;
                for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 1.1f, e = k * k * (3f - 2f * k);
                    pg.alpha = e;
                    panel.transform.localScale = scale0 * Mathf.Lerp(0.95f, 1f, e);
                    yield return null;
                }
                pg.alpha = 1f;
                panel.transform.localScale = scale0;
            }
        }

        // ================================================================== the stone is broken

        /// <summary>The numbers of a run come up out of the dark, they do not jump onto it.</summary>
        static IEnumerator FadeIn(GameObject panel)
        {
            if (panel == null) yield break;
            var pg = panel.GetComponent<CanvasGroup>();
            if (pg == null) pg = panel.AddComponent<CanvasGroup>();
            var scale0 = panel.transform.localScale;
            for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.1f, e = k * k * (3f - 2f * k);
                pg.alpha = e;
                panel.transform.localScale = scale0 * Mathf.Lerp(0.95f, 1f, e);
                yield return null;
            }
            pg.alpha = 1f;
            panel.transform.localScale = scale0;
        }

        /// <summary>
        /// The morning after: the village by day, no snow in the air, everybody out. The light the hall ended in
        /// clears slowly onto it. Four sentences, then the numbers of the run - the real ones this time - and
        /// from there the credits.
        /// </summary>
        public static IEnumerator PlayTrue(GameDirector d)
        {
            Pending = false;
            PendingTrue = false;
            Playing = true;
            var p = d.Player;
            var w = d.world;
            var g = w.Grid;
            float ground = p.pos.y;
            float middle = g.Width * 0.5f;
            // Brann comes up the street from the left; the elder is already out, in the middle of it
            VillageInterior.Place(p, new Vector2(middle - 8f, ground));
            var steps = new Steps { x = 1 };
            p.bot = steps;
            p.facing = 1;
            p.godMode = true;
            float low = Mathf.Max(0f, ground - 3.6f);
            if (d.cameraFollow != null) d.cameraFollow.SetBounds(new Rect(0f, low, g.Width, g.Height - low));
            var hudGroup = HideHud(d);
            var ui = new Overlay(d.hud.healthText != null ? d.hud.healthText.font : null);
            ui.White(1f);
            ui.Say(TrueLines[0]);
            var elder = w.SpawnById("elder", new Vector2(middle + 1.8f, ground));
            if (elder != null) elder.facing = -1;
            // the picture begins close on Brann and draws back over the whole street as the morning goes on
            var cam = Camera.main;
            float wide = cam != null ? cam.orthographicSize : 6f;
            if (cam != null) cam.orthographicSize = wide * 0.72f;

            var b = new Buttons();
            var crowd = new System.Collections.Generic.List<Entity>();
            foreach (var en in w.Entities) if (en != null && !en.Dead && en is Townsfolk) crowd.Add(en);
            var snow = new Color(0.95f, 0.97f, 1f);
            float clock = 0f, nextCheer = 0f, nextSnow = 0f, nextHop = 0f;
            int cheered = 0;
            bool roofs = false, cheers = false, hops = false;
            // what goes on in the street while a sentence stands under it (the buttons turn the page as ever)
            IEnumerator Scene(float seconds)
            {
                for (float t = 0f; t < seconds && !b.skip; t += Time.deltaTime)
                {
                    clock += Time.deltaTime;
                    if (cam != null) cam.orthographicSize = Mathf.Lerp(wide * 0.72f, wide, Mathf.SmoothStep(0f, 1f, clock / 16f));
                    if (steps.x != 0 && p.pos.x >= middle - 1.2f) steps.x = 0;       // arrived
                    if (cheers && clock >= nextCheer && crowd.Count > 0)
                    {
                        // whoever is nearest in view calls out next
                        nextCheer = clock + 1.15f;
                        crowd.Sort((x, y) => Mathf.Abs(x.pos.x - p.pos.x).CompareTo(Mathf.Abs(y.pos.x - p.pos.x)));
                        var who = crowd[cheered % Mathf.Min(crowd.Count, 5)];
                        Fx.Text(new Vector2(who.pos.x, who.pos.y + who.size.y + 0.7f + (cheered % 3) * 0.5f), Cheers[cheered % Cheers.Length], new Color(1f, 0.95f, 0.75f), 1.6f);
                        cheered++;
                    }
                    if (roofs && clock >= nextSnow)
                    {
                        // the snow lets go of the roofs: clumps of it come down off the eaves, here and there
                        nextSnow = clock + 0.16f;
                        float cx = cam != null ? cam.transform.position.x : p.pos.x;
                        var at = new Vector2(cx + Random.Range(-11f, 11f), ground + Random.Range(4.6f, 7.4f));
                        Fx.Debris(at, snow, 3);
                        if (Random.value < 0.4f) Fx.Dust(new Vector2(at.x, ground + 0.1f), 3);
                    }
                    if (hops && clock >= nextHop && steps.x == 0) { nextHop = clock + 1.3f; steps.hop = true; }
                    if (SkipPressed(d)) b.skip = true;
                    else if (t > 0.6f && NextPressed(d)) break;
                    yield return null;
                }
            }

            yield return new WaitForSecondsRealtime(0.8f);
            // the light clears, slowly: the first thing seen is a morning, and Brann walking into it
            for (float t = 0f; t < 3.2f && !b.skip; t += Time.deltaTime)
            {
                float k = t / 3.2f;
                ui.White(1f - k * k * (3f - 2f * k));
                clock += Time.deltaTime;
                if (steps.x != 0 && p.pos.x >= middle - 1.2f) steps.x = 0;
                if (SkipPressed(d)) b.skip = true;
                yield return null;
            }
            ui.White(0f);
            yield return Scene(1.6f);
            // morning: the street sees it, and says so
            if (!b.skip) { ui.Say(TrueLines[1]); Sfx.PlayUI("victory"); }
            cheers = true;
            yield return Scene(5f);
            // the roofs
            if (!b.skip) ui.Say(TrueLines[2]);
            roofs = true; hops = true;
            yield return Scene(5.6f);
            // the elder, who remembers nothing
            hops = false; cheers = false;
            if (!b.skip) { ui.Say(TrueLines[3]); Sfx.PlayUI("menu_select"); }
            if (elder != null) Fx.Text(new Vector2(elder.pos.x, elder.pos.y + elder.size.y + 0.6f), "Brann?", new Color(1f, 0.95f, 0.75f), 1.6f);
            yield return Scene(5f);
            // the sliver: it comes down out of the light onto Brann's chest, and stays there
            GameObject sliver = null;
            if (!b.skip)
            {
                ui.Say(TrueLines[4]);
                sliver = new GameObject("EndingSliver");
                var ssr = sliver.AddComponent<SpriteRenderer>();
                ssr.sprite = Story.Icon(d.config, "hammercore");
                var body = p.GetComponentInChildren<SpriteRenderer>();
                if (body != null) { ssr.sortingLayerID = body.sortingLayerID; ssr.sortingOrder = body.sortingOrder + 3; }
                sliver.transform.localScale = Vector3.one * 0.5f;
                var glow = LevelLighting.MakeLight(sliver.transform, "Glow", Light2D.LightType.Point, new Color(0.7f, 0.95f, 1f), 1.2f, 0.3f, 2.6f);
                Sfx.PlayUI("idol");
                Vector2 chest() => new Vector2(p.pos.x, p.pos.y + 0.42f);
                for (float t = 0f; t < 2.2f && !b.skip; t += Time.deltaTime)
                {
                    float k = t / 2.2f, e = k * k * (3f - 2f * k);
                    sliver.transform.position = Vector2.Lerp(chest() + new Vector2(0f, 3.2f), chest(), e) + new Vector2(Mathf.Sin(t * 5f) * 0.25f * (1f - e), 0f);
                    clock += Time.deltaTime;
                    if (cam != null) cam.orthographicSize = Mathf.Lerp(wide * 0.72f, wide, Mathf.SmoothStep(0f, 1f, clock / 16f));
                    if (SkipPressed(d)) b.skip = true;
                    yield return null;
                }
                if (!b.skip) { Fx.Sparks(chest(), new Color(0.7f, 0.95f, 1f), 16, 4f); steps.hop = true; }
                cheers = true;
                yield return Scene(3.6f);
            }
            for (float t = 0f; t < 1.8f; t += Time.deltaTime) { ui.Fade(1f - t / 1.2f); ui.Dark(t / 1.8f); yield return null; }
            ui.Dark(1f);
            ui.Say("");
            ui.Bars(false);
            if (sliver != null) Object.Destroy(sliver);
            if (cam != null) cam.orthographicSize = wide;
            yield return new WaitForSecondsRealtime(b.skip ? 0.2f : 0.8f);
            p.bot = null;
            Playing = false;
            Last = Night.None;
            if (d.results != null && d.results.transform.IsChildOf(d.hud.transform)) hudGroup.alpha = 1f;
            ui.Under(d.results != null ? d.results.GetComponentInParent<Canvas>() : null);
            d.ShowTrueEnd(won, news);
            won = null;
            yield return FadeIn(d.results != null ? d.results.root : null);
        }

        /// <summary>The credits: the names go up over the dark; then the title screen. The "yes" button hurries them, "back" ends them.</summary>
        public static IEnumerator Credits(GameDirector d)
        {
            Playing = true;
            // the numbers go out first; the names only begin once the screen is empty
            var panel = d.results != null ? d.results.root : null;
            if (panel != null)
            {
                var pg = panel.GetComponent<CanvasGroup>();
                if (pg == null) pg = panel.AddComponent<CanvasGroup>();
                for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { pg.alpha = 1f - t / 0.6f; yield return null; }
                panel.SetActive(false);
            }
            var ui = new Overlay(d.hud.healthText != null ? d.hud.healthText.font : null);
            ui.Bars(false);
            ui.Dark(1f);
            // (the column is made out of sight and measured there: it must not be seen in the middle of the
            // screen for the one frame before it is sent down to where it starts from)
            var roll = ui.Roll(CreditLines);
            roll.alpha = 0f;
            roll.rectTransform.anchoredPosition = new Vector2(0f, -5000f);
            roll.ForceMeshUpdate();
            yield return null;
            float height = roll.preferredHeight, y = -620f - height * 0.5f, end = 620f + height * 0.5f;
            roll.rectTransform.anchoredPosition = new Vector2(0f, y);
            roll.alpha = 1f;
            while (y < end)
            {
                bool hurry = InputDeviceTracker.PadConfirmPressed || (d.input != null && d.input.ConfirmPressed);
                var pad = UnityEngine.InputSystem.Gamepad.current;
                bool held = (pad != null && InputDeviceTracker.ConfirmButton(pad).isPressed) || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed);
                if (SkipPressed(d)) break;
                y += Time.unscaledDeltaTime * (held || hurry ? 420f : 70f);
                roll.rectTransform.anchoredPosition = new Vector2(0f, y);
                yield return null;
            }
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { roll.alpha = 1f - t / 0.8f; yield return null; }
            roll.alpha = 0f;
            Playing = false;
            // the title screen comes up out of the same dark the names went into: it does not cut in
            ScreenFade.Through(() => d.QuitToMenu(), 0.5f, 0.35f, 1.6f);
        }

        // ================================================================== the first death of a save

        /// <summary>The first time a save wakes in bed after a death: the dark, then the room, and what it means.</summary>
        public static IEnumerator PlayWaking(GameDirector d)
        {
            Playing = true;
            if (!SaveSystem.Records.loopSeen) { SaveSystem.Records.loopSeen = true; SaveSystem.SaveRecords(); }
            var p = d.Player;
            var steps = new Steps();
            p.bot = steps;
            var hudGroup = HideHud(d);
            var ui = new Overlay(d.hud.healthText != null ? d.hud.healthText.font : null);
            ui.Dark(1f);
            ui.Say(FirstDeathLines[0]);
            var b = new Buttons();
            yield return new WaitForSecondsRealtime(1.2f);       // (the fade between the scenes clears first)
            yield return Hold(d, b, 2.6f);
            if (!b.skip) ui.Say(FirstDeathLines[1]);
            for (float t = 0f; t < 1.5f && !b.skip; t += Time.deltaTime) { ui.Dark(1f - t / 1.5f); yield return null; }
            ui.Dark(0f);
            yield return Hold(d, b, 4.2f);
            if (!b.skip) ui.Say(FirstDeathLines[2]);
            yield return Hold(d, b, 4.2f);
            ui.Remove();
            hudGroup.alpha = 1f;
            p.bot = null;
            if (d.input != null) d.input.ClearLatches();
            Playing = false;
        }

        // ================================================================== the scenes' own layer

        /// <summary>A dark band above and below the picture, the words in the lower one, and a sheet over everything: dark, or the stone's white.</summary>
        class Overlay
        {
            readonly GameObject root;
            readonly Canvas canvas;
            readonly Image top, bottom, cover;
            readonly RawImage swirl;
            readonly TMPro.TextMeshProUGUI words, hint;
            static readonly Color DarkSheet = new Color(0.02f, 0.02f, 0.05f);
            // the spiral is drawn small and shown large, in the same coarse pixels as the game
            const int SwirlW = 192, SwirlH = 108;
            Texture2D swirlTex;
            Color32[] swirlPx;

            public Overlay(TMPro.TMP_FontAsset font)
            {
                root = new GameObject("EndingOverlay");
                canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 350;          // over the game's screens, under the fade between scenes
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                ScreenFrameKeeper.Frame(canvas);      // laid out in the 16:9 frame, like every screen
                cover = Band("Sheet", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), 0f);
                cover.color = new Color(DarkSheet.r, DarkSheet.g, DarkSheet.b, 0f);
                swirl = new GameObject("Swirl", typeof(RectTransform)).AddComponent<RawImage>();
                swirl.transform.SetParent(root.transform, false);
                swirl.rectTransform.anchorMin = Vector2.zero; swirl.rectTransform.anchorMax = Vector2.one;
                swirl.rectTransform.offsetMin = swirl.rectTransform.offsetMax = Vector2.zero;
                swirl.raycastTarget = false;
                swirl.enabled = false;
                top = Band("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 90f);
                bottom = Band("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 200f);
                words = Text("Words", font, 40f, new Color(0.96f, 0.93f, 0.85f), new Vector2(0.12f, 0f), new Vector2(0.88f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), 140f);
                hint = Text("Hint", font, 24f, new Color(1f, 1f, 1f, 0.45f), new Vector2(0.12f, 0f), new Vector2(0.88f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), 34f);
                hint.text = "[next]  Next        [skip]  Skip";
                // (the buttons in it are drawn as everywhere else: by the set any other text on the screen uses)
                var other = Object.FindFirstObjectByType<GlyphText>(FindObjectsInactive.Include);
                if (other != null && other.set != null) hint.gameObject.AddComponent<GlyphText>().set = other.set;
            }

            Image Band(string name, Vector2 min, Vector2 max, Vector2 pivot, float height)
            {
                var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(root.transform, false);
                var rt = img.rectTransform;
                rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                if (height > 0f) rt.sizeDelta = new Vector2(0f, height);
                img.color = new Color(DarkSheet.r, DarkSheet.g, DarkSheet.b, 1f);
                img.raycastTarget = false;
                return img;
            }

            TMPro.TextMeshProUGUI Text(string name, TMPro.TMP_FontAsset font, float size, Color color, Vector2 min, Vector2 max, Vector2 pivot, Vector2 at, float height)
            {
                var t = new GameObject(name, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                t.transform.SetParent(root.transform, false);
                var rt = t.rectTransform;
                rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
                rt.anchoredPosition = at; rt.sizeDelta = new Vector2(0f, height);
                if (font != null) t.font = font;
                t.fontSize = size; t.color = color;
                t.alignment = TMPro.TextAlignmentOptions.Center;
                t.textWrappingMode = TMPro.TextWrappingModes.Normal;
                t.raycastTarget = false;
                t.text = "";
                return t;
            }

            public void Say(string line) { if (words != null) { words.text = line ?? ""; words.alpha = 1f; } }
            /// <summary>The light of the broken stone over the picture: 0 = none, 1 = nothing else to be seen. Soft and warm, not a flash.</summary>
            public void White(float k) { cover.color = new Color(1f, 0.96f, 0.86f, Mathf.Clamp01(k)); }
            /// <summary>A column of names in the middle of the screen (the credits): headings small and dim, names large.</summary>
            public TMPro.TextMeshProUGUI Roll(string[] lines)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i] ?? "";
                    bool heading = l.Length > 0 && l == l.ToUpperInvariant() && i > 0;
                    if (i == 0) sb.Append("<size=96><color=#FFD75E>").Append(l).Append("</color></size>");
                    else if (heading) sb.Append("<size=26><color=#B9B2C8>").Append(l).Append("</color></size>");
                    else sb.Append(l);
                    sb.Append((char)10);
                }
                var t = Text("Roll", words.font, 44f, new Color(0.96f, 0.93f, 0.85f), new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 100f);
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                t.overflowMode = TMPro.TextOverflowModes.Overflow;
                t.lineSpacing = 18f;
                t.text = sb.ToString();
                return t;
            }
            /// <summary>The words and the hint under them, from there (1) to gone (0).</summary>
            public void Fade(float k) { words.alpha = Mathf.Clamp01(k); hint.alpha = 0.45f * Mathf.Clamp01(k); }
            /// <summary>The picture behind the bands goes dark (1) or clears (0).</summary>
            public void Dark(float k) { cover.color = new Color(DarkSheet.r, DarkSheet.g, DarkSheet.b, Mathf.Clamp01(k)); }
            /// <summary>
            /// The picture is drawn into the stone: the dark winds in from the edges as a spiral that turns, its
            /// arms reaching ahead of it, and closes on the point 'at' (0..1 across and up the screen). k: 0 = not
            /// begun, 1 = shut; below 0 puts the spiral away.
            /// </summary>
            public void Swirl(float k, Vector2 at, float time)
            {
                if (k < 0f) { swirl.enabled = false; return; }
                if (swirlTex == null)
                {
                    swirlTex = new Texture2D(SwirlW, SwirlH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    swirlPx = new Color32[SwirlW * SwirlH];
                    swirl.texture = swirlTex;
                }
                swirl.enabled = true;
                k = Mathf.Clamp01(k);
                float open = Mathf.Lerp(1.3f, 0f, k * k * (3f - 2f * k));       // how wide the eye of it still is (1 = the width of the screen)
                float spin = time * (3f + 9f * k);
                float aspect = (float)SwirlH / SwirlW;
                var dark = (Color32)DarkSheet;
                for (int y = 0; y < SwirlH; y++)
                {
                    float dy = ((y + 0.5f) / SwirlH - at.y) * aspect;
                    for (int x = 0; x < SwirlW; x++)
                    {
                        float dx = (x + 0.5f) / SwirlW - at.x;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float arm = Mathf.Sin(3f * Mathf.Atan2(dy, dx) - 16f * r + spin);
                        // the wall of dark, its edge wound into three arms
                        float wall = Mathf.Clamp01((r - open * (0.72f + 0.28f * arm)) / 0.05f);
                        // thin arms of dark run ahead of it, into the eye
                        float thread = Mathf.Clamp01((arm - 0.55f) * 2.2f) * k * 0.8f * Mathf.Clamp01(r * 9f);
                        float a = Mathf.Max(wall, thread);
                        // (the lip of the wall glows with the colour of the stone)
                        float lip = wall > 0f && wall < 1f ? 1f - wall : 0f;
                        swirlPx[y * SwirlW + x] = new Color32(
                            (byte)Mathf.Lerp(dark.r, 255f, lip * 0.9f), (byte)Mathf.Lerp(dark.g, 205f, lip * 0.9f), (byte)Mathf.Lerp(dark.b, 110f, lip * 0.9f),
                            (byte)(a * 255f));
                    }
                }
                swirlTex.SetPixels32(swirlPx);
                swirlTex.Apply(false);
            }
            public void Bars(bool on)
            {
                top.enabled = bottom.enabled = on;
                hint.gameObject.SetActive(on);
            }
            /// <summary>Goes behind this screen of the game (the run's numbers are read over the sheet).</summary>
            public void Under(Canvas screen) { if (screen != null) canvas.sortingOrder = screen.rootCanvas.sortingOrder - 1; }
            public void Remove() { if (swirlTex != null) Object.Destroy(swirlTex); Object.Destroy(root); }
        }
    }
}
