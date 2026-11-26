using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The inside of a house of Brindlemoor: one furnished room (the picture) with its door on the left. The entity
    /// stands at that door; Up / Enter there goes back out to the street. Whoever keeps the place stands inside
    /// with their wares (villagers 0-5 once freed), the elder waits in the village hall (6), and the player's own
    /// house (7) is where every run begins, beside the bed.
    /// The rooms lie apart from the street in the same level; while the player is in one the camera stays on it.
    /// </summary>
    public class VillageInterior : Entity, IInteractable
    {
        /// <summary>Room size in cells (floor space), and the frame the camera keeps to around it.</summary>
        public const int RoomW = 14, RoomH = 13;        // two storeys of six cells with the floor between them

        [Tooltip("Rooms by building number.")] public Sprite[] rooms;
        [Tooltip("The same rooms at the other hours of the village clock: all at dawn, then all by day, then all at dusk ('rooms' is the night).")] public Sprite[] roomsByHour;
        [Tooltip("The fire (a child object with its flames and light): put in the hearth of the rooms that have one.")] public Transform fire;
        [Tooltip("What hangs close to the eye in a room, by building number (null = nothing): drawn in front of the people in it.")] public Sprite[] foregrounds;
        [Tooltip("The renderer of that foreground (a child, in front of everyone).")] public SpriteRenderer foreground;
        [Tooltip("Home with its lamps put out, at night, dawn, day and dusk: lit by the windows and the fire alone.")] public Sprite[] homeLampsOut;

        /// <summary>Where the fire burns in each room, in floor-space pixels of the picture (-1 = no fire): the forge's
        /// furnace, the hearths of the hall and of the player's house. Same numbers as ArtGenerator.Village, Room.</summary>
        /// <summary>
        /// Things to read, room by room (Curio): where it lies (floor-space pixels across), storey (0 = street level,
        /// 1 = upstairs), how high it lies (pixels above that floor: a table is 15, a mantel 46; a pinned note: its
        /// foot), what it is, what it is called, what is written. Each says something about who lives here - and,
        /// read together, about this winter. Same places as the furniture in ArtGenerator.Village, Room.
        /// </summary>
        static readonly (float x, int storey, int lift, Curio.Form form, string label, string text)[][] Curios =
        {
            new[]   // the forge - Hilde
            {
                (141f, 0, 56, Curio.Form.Pinned, "a note by the furnace", "In Hilde's hand: 'Bank the coals at dusk.' Under it, newer, pressed hard into the paper: 'They have not burned down. Not once.'"),
                (84f, 0, 16, Curio.Form.Book, "the order book", "Horseshoes, hinges, nails: a whole season of orders, every one marked 'done', none collected. The last six lines are blades, and all six are dated the same day."),
                (165f, 1, 16, Curio.Form.Letter, "a letter under the pillow", "Wrapped round a ring too small for Hilde's hand: 'When the sun comes back, ask me again.' The fold is worn soft. It has been opened a great many times."),
            },
            new[]   // the ropewalk - Tam
            {
                (184f, 0, 23, Curio.Form.Book, "the ledger", "Rope sold, fathom by fathom. The last entry: 'To the Elder, forty fathoms, for the mountain.' It is written on every line of the page, in the same hand, in the same ink."),
                (137f, 1, 0, Curio.Form.Chest, "Tam's chest", "Under the tools, a climbing harness, cut through - not frayed, cut. Tucked in with it, a board scratched with a tally: more marks than a winter has days."),
                (162f, 1, 16, Curio.Form.Note, "a note on the pillow", "On the second pillow, in a woman's hand: 'Back before the snow.' This is the third winter of snow since."),
            },
            new[]   // the apothecary - Maren
            {
                (185f, 0, 37, Curio.Form.Pinned, "the recipe", "Maren's tonic. The recipe is in two hands: hers, and an older one that looks just like hers. In the margin, in the older hand: 'Do not let her go up the mountain.'"),
                (176f, 1, 15, Curio.Form.Book, "Maren's night-book", "Her remedies, listed by night instead of by herb: 'the first night', 'the cold night', 'the night he did not come back'. Then the same night, over and over, to the foot of the page."),
                (201f, 1, 17, Curio.Form.Letter, "a note on the coat", "Pinned to a man's coat, folded at the foot of the bed: 'Brushed. Mended. Waiting.' The coat has not been worn this winter."),
            },
            new[]   // the map room - Oswin
            {
                (183f, 0, 17, Curio.Form.Note, "a note by the globe", "A hole is burned clean through Oswin's globe, exactly where Brindlemoor should be. His note beside it: 'Not a fault in the globe.'"),
                (126f, 1, 17, Curio.Form.Book, "the chart book", "Map after map of the caverns, all in Oswin's hand, and no two agree. The last page is a sketch through the telescope: a light on the peak, where no one lives."),
                (106f, 1, 15, Curio.Form.Note, "a note on the bed", "Pinned to blankets drawn tight and never slept in: 'If I am asleep I will not remember. Do not let me sleep.'"),
            },
            new[]   // Pip's Oddments
            {
                (131f, 1, 14, Curio.Form.Note, "a peddler's licence", "Issued in a town called Harrowmere. You have never heard of it, and neither has any map in Oswin's room. Folded inside: three compasses Pip swears he never sold. All three point at the mountain."),
                (217f, 1, 11, Curio.Form.Letter, "a note on the bag", "Pip's bag, packed, as it always is. On top, in his hand: 'Leave tomorrow.' The paper has gone yellow."),
            },
            new[]   // the lamp house - Bera
            {
                (71f, 1, 25, Curio.Form.Book, "the lamp log", "Every lamp in Brindlemoor, every night, each with its tick. The last line: 'the one on the mountain - not mine.' Scratched in the margin: 'While the great lamp burns, it cannot see us.'"),
                (196f, 0, 17, Curio.Form.Note, "a note on the barrel", "'Oil delivered: none. Oil burned: every night. Oil left: all of it.' Bera has underlined the last line twice."),
            },
            new[]   // the village hall - the Elder
            {
                (152f, 0, 15, Curio.Form.Book, "the book of winters", "A book of village winters, a page for each. Every page after tonight is blank. Every page before it reads the same."),
                (186f, 0, 46, Curio.Form.Note, "a list on the mantel", "Names to be carved over the hearth: everyone who went up for the Sunstone. The last is yours. It has been crossed out, and written in again, and crossed out."),
                (128f, 1, 17, Curio.Form.Letter, "an unsent letter", "In the Elder's hand: 'Forgive me. I did not know what the stone would ask of them.' It is addressed to no one. There are many copies in the drawer."),
            },
            new[]   // home
            {
                (95f, 1, 0, Curio.Form.Chest, "your chest", "At the bottom, a scrap of paper in your own handwriting: 'The gate. Then down. Do not trust the first way down.' You do not remember writing it."),
                (190f, 0, 46, Curio.Form.Book, "father's journal", "Your father's journal, open at its last page: the winter he carried the lantern up the mountain. The lantern in your hand is the same one. So is the date."),
            },
        };

        /// <summary>
        /// Things in a room that answer when touched (RoomToy): room, kind, where it stands (floor-space pixels across),
        /// storey. The cat by the fire and the lamps at home, Tam's rope wheel and hammock, Maren's cauldron.
        /// </summary>
        static readonly (int room, RoomToy.Kind kind, float x, int storey)[] Toys =
        {
            (7, RoomToy.Kind.Cat, 171f, 0), (7, RoomToy.Kind.Lamps, 136f, 0),
            (1, RoomToy.Kind.Wheel, 200f, 0), (1, RoomToy.Kind.Hammock, 71f, 1),
            (2, RoomToy.Kind.Cauldron, 200f, 0),
        };

        /// <summary>The player's own house (its lamps can be put out).</summary>
        public const int Home = 7;
        static bool homeLampsAreOut;
        /// <summary>The lamps of this room are out (only at home; it stays so until they are lit again).</summary>
        public bool LampsOut => Index == Home && homeLampsAreOut;

        public void SetLampsOut(bool value)
        {
            if (Index != Home) return;
            homeLampsAreOut = value;
            ShowLamps();
        }

        void ShowLamps()
        {
            if (sr == null) return;
            var part = VillageClock.PartOf(VillageClock.Hour);
            Sprite pic = RoomPicture(Index, part);
            if (LampsOut && homeLampsOut != null && homeLampsOut.Length == 4 && homeLampsOut[(int)part] != null) pic = homeLampsOut[(int)part];
            if (pic != null) sr.sprite = pic;
            // the lamplight on whoever is in the room goes out with them
            foreach (var n in new[] { "RoomLight", "UpstairsLight" }) { var l = transform.Find(n); if (l != null) l.gameObject.SetActive(!LampsOut); }
        }

        static readonly int[] FireX = { 193, -1, 200, -1, -1, -1, 196, 198 };
        static readonly int[] FireY = { 8, 0, 5, 0, 0, 0, 4, 4 };
        /// <summary>How big each fire burns (the apothecary's is a little flame under the cauldron).</summary>
        static readonly float[] FireScale = { 1f, 1f, 0.5f, 1f, 1f, 1f, 1f, 1f };

        public int Index { get; private set; }
        bool cameraHere;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            Index = sp.param;
        }

        /// <summary>Left edge of the room's floor (the door is the second cell).</summary>
        float RoomLeft => Mathf.Floor(pos.x) - 1f;
        Rect RoomRect => new Rect(RoomLeft, pos.y, RoomW, RoomH);

        /// <summary>The room this spot is in (null = out on the street).</summary>
        public static VillageInterior RoomAt(World w, Vector2 at)
        {
            if (w == null) return null;
            foreach (var e in w.Entities)
                if (e is VillageInterior room && !room.Dead && room.RoomRect.Contains(at)) return room;
            return null;
        }

        /// <summary>
        /// The way to a place in the village is through doors: the rooms lie somewhere else in the level than the
        /// fronts they belong to, so a straight line from one to the other points at nothing. From inside a room
        /// the way to anything outside it is that room's own door; from the street the way to something inside a
        /// room is the door of its building. Otherwise the place itself. (Returns the spot to point at.)
        /// </summary>
        public static Vector2 WayTo(World w, Vector2 from, Vector2 target)
        {
            var here = RoomAt(w, new Vector2(from.x, from.y));
            var there = RoomAt(w, new Vector2(target.x, target.y - 0.6f));
            if (here == there) return target;
            if (here != null) return new Vector2(here.pos.x, here.pos.y + 1.7f);       // out through this room's door first
            foreach (var e in w.Entities)
                if (e is VillageBuilding b && !b.Dead && b.Index == there.Index) return new Vector2(b.pos.x, b.pos.y + 1.9f);
            return target;
        }
        /// <summary>What the camera is kept to while the player is inside: the middle of the room. The room is smaller
        /// than the view, so the camera stands still on it instead of sliding after the player.</summary>
        Rect Frame => new Rect(RoomLeft + RoomW * 0.5f - 0.05f, pos.y + RoomH * 0.5f - 0.05f, 0.1f, 0.1f);

        /// <summary>
        /// The picture of a room at an hour of the village clock: its windows show the sky of the street outside, and
        /// the room is lit by it (the night picture where there is none for that hour).
        /// </summary>
        public Sprite Picture => sr != null ? sr.sprite : null;

        public Sprite RoomPicture(int index, VillageClock.Part part)
        {
            if (rooms == null || index < 0 || index >= rooms.Length) return null;
            int at = part == VillageClock.Part.Night ? -1 : ((int)part - 1) * rooms.Length + index;
            if (at >= 0 && roomsByHour != null && at < roomsByHour.Length && roomsByHour[at] != null) return roomsByHour[at];
            return rooms[index];
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (sr != null) { var room = RoomPicture(Index, VillageClock.PartOf(VillageClock.Hour)); if (room != null) sr.sprite = room; }
            if (Index == Home) ShowLamps();
            if (fire != null)
            {
                bool lit = Index >= 0 && Index < FireX.Length && FireX[Index] >= 0;
                fire.gameObject.SetActive(lit);
                // floor-space pixel 24 is the middle of the door cell, where this entity stands
                if (lit) fire.localPosition = new Vector3((FireX[Index] - 24f) / 16f, FireY[Index] / 16f, 0f);
                if (lit) fire.localScale = Vector3.one * FireScale[Index];
            }
            if (foreground != null)
            {
                var fg = foregrounds != null && Index >= 0 && Index < foregrounds.Length ? foregrounds[Index] : null;
                foreground.sprite = fg;
                foreground.enabled = fg != null;
            }
            var cfg = World.config;
            if (cfg == null || cfg.prefabs == null) return;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            if (Index < Villagers.All.Length)
            {
                // the keeper behind the counter, wares set out to the right
                var prefab = cfg.prefabs.Get("villagerhome");
                var cell = new Vector2Int(cx + 5, cy);
                if (prefab != null) World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("villagerhome", cell, 0, Index));
            }
            // the things to read
            var curio = cfg.prefabs.Get("curio");
            if (curio != null && Index >= 0 && Index < Curios.Length)
                foreach (var c in Curios[Index])
                {
                    var at = At(c.x, c.storey * UpperYPx);
                    World.Spawn(curio, at, new SpawnPoint("curio", new Vector2Int(Mathf.FloorToInt(at.x), Mathf.RoundToInt(at.y)), c.lift, (int)c.form, c.label + "|" + c.text));
                }
            // the things that answer when touched
            var toy = cfg.prefabs.Get("roomtoy");
            if (toy != null)
                foreach (var t in Toys)
                {
                    if (t.room != Index) continue;
                    var at = At(t.x, t.storey * UpperYPx);
                    World.Spawn(toy, at, new SpawnPoint("roomtoy", new Vector2Int(Mathf.FloorToInt(at.x), Mathf.RoundToInt(at.y)), 0, (int)t.kind));
                }
            if (Index == VillageBuilding.Hall)
            {
                var prefab = cfg.prefabs.Get("elder");
                var cell = new Vector2Int(cx + 6, cy);
                if (prefab != null) World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("elder", cell, -1));
            }
        }

        // ------------------------------------------------------------------ a room that lives

        float steamT, moteT, bubbleT;

        /// <summary>A spot of the room picture (floor-space pixels: x across, y up from the ground floor) in the world.</summary>
        public Vector2 At(float x, float y) => new Vector2(pos.x + (x - 24f) / 16f, pos.y + y / 16f);

        /// <summary>
        /// What moves in a room while the player is in it (purely to be looked at): in the apothecary the brew
        /// steams and bubbles, and dust hangs in the moonlight from the upstairs window.
        /// </summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!cameraHere || Index < 0 || Index >= WinW.Length) return;
            float dt = Time.deltaTime;
            steamT -= dt; bubbleT -= dt; moteT -= dt; emberT -= dt;
            if (Index == 2)
            {
                // the apothecary's brew steams and bubbles
                if (steamT <= 0f) { steamT = Random.Range(0.25f, 0.45f); Fx.Smoke(At(200f + Random.Range(-8f, 8f), 33f), 1); }
                if (bubbleT <= 0f) { bubbleT = Random.Range(0.3f, 0.7f); Fx.Motes(At(200f + Random.Range(-9f, 9f), 30f), new Color(0.6f, 1f, 0.7f, 0.9f), 1, 0.05f, 0.02f); }
            }
            else if (FireX[Index] >= 0 && emberT <= 0f)
            {
                // a furnace or a hearth: now and then an ember goes up the chimney
                emberT = Random.Range(0.35f, 0.8f);
                Fx.Embers(At(FireX[Index] + Random.Range(-6f, 6f), FireY[Index] + 10f), 1);
            }
            if (WinW[Index] > 0 && moteT <= 0f)
            {
                // dust hanging in the light that falls in through the upstairs window
                moteT = Random.Range(0.12f, 0.25f);
                float down = Random.Range(3f, WinY[Index] - 2f);
                float x = WinX[Index] + down * 0.55f + Random.Range(1f, WinW[Index] - 1f), y = UpperYPx + WinY[Index] - down;
                Fx.Drift(At(x, y), new Color(0.82f, 0.88f, 1f, 0.7f));
            }
        }

        float emberT;
        /// <summary>The upstairs window of each room, in floor-space pixels of its storey (x, foot, width; 0 wide = none): where the light falls in.
        /// Same numbers as ArtGenerator.Village, Room (NightWindow).</summary>
        static readonly int[] WinX = { 100, 118, 120, 160, 128, 0, 124, 128 }, WinY = { 46, 46, 46, 40, 50, 0, 50, 46 }, WinW = { 18, 18, 18, 28, 16, 0, 16, 22 };

        /// <summary>Height of the upstairs floor in the room picture (pixels; ArtGenerator.Village, UpperY).</summary>
        public const float UpperYPx = 112f;

        public string GetPrompt(PlayerController p) => "[enter]  Leave";

        public void Interact(PlayerController p)
        {
            if (ScreenFade.Busy) return;
            foreach (var e in World.Entities)
                if (e is VillageBuilding b && !b.Dead && b.Index == Index)
                {
                    Sfx.Play("land", pos);
                    ScreenFade.Through(() => { if (b != null && !b.Dead && p != null) { cameraHere = false; b.StepOut(p); } });
                    return;
                }
        }

        /// <summary>The player comes in through the door.</summary>
        public void Enter(PlayerController p)
        {
            Place(p, new Vector2(pos.x + 1f, pos.y));
            Sfx.Play("land", pos);
            HoldCamera();
        }

        void HoldCamera()
        {
            cameraHere = true;
            var dir = GameDirector.Instance;
            if (dir != null && dir.cameraFollow != null) dir.cameraFollow.SetBounds(Frame, 1.16f);       // a step back: the whole house in view, cellar boards to roof beams
            if (Ambience.Instance != null) Ambience.Instance.gameObject.SetActive(false);        // no snow indoors
        }

        public override void Tick(float dt)
        {
            // a run begins inside the player's own house: the camera finds the room by itself
            if (cameraHere) return;
            var dir = GameDirector.Instance;
            var p = dir != null ? dir.Player : null;
            if (p != null && RoomRect.Contains(new Vector2(p.pos.x, p.pos.y + 0.5f))) HoldCamera();
        }

        /// <summary>Puts the player on their feet at a spot, standing still.</summary>
        public static void Place(PlayerController p, Vector2 at)
        {
            p.motor = MotorState.At(at);
            p.motor.grounded = false;
            p.motor.apexY = at.y;
            p.pos = p.prevPos = p.motor.pos;
            p.vel = Vector2.zero;
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
