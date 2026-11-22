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

        /// <summary>Where the fire burns in each room, in floor-space pixels of the picture (-1 = no fire): the forge's
        /// furnace, the hearths of the hall and of the player's house. Same numbers as ArtGenerator.Village, Room.</summary>
        /// <summary>
        /// Things worth a closer look, room by room: cell across (0-13, the door is 1), storey (0 = street level,
        /// 1 = upstairs), what it is called, what the player notices. Each says something about who lives here - and,
        /// read together, about this winter.
        /// </summary>
        static readonly (int cell, int storey, string label, string text)[][] Curios =
        {
            new[]   // the forge - Hilde
            {
                (12, 0, "the furnace", "The coals are banked the way Hilde always leaves them at dusk. They have not burned down at all."),
                (4, 0, "the rack of blades", "Six blades, each stamped with the day it was finished. All six carry the same day."),
                (10, 1, "her bed", "Under the pillow: a ring too small for Hilde's hand, and a letter that begins 'When the sun comes back...'"),
                (6, 1, "the crates", "Horseshoes, hinges, nails - a whole season's orders, packed and labelled. Nobody has come to collect them."),
            },
            new[]   // the ropewalk - Tam
            {
                (11, 0, "the ledger", "Rope sold. The last entry: 'To the Elder, forty fathoms, for the mountain.' It is written on every line of the page."),
                (6, 1, "the old loom", "A climbing harness hangs on it, cut through. Not frayed. Cut."),
                (8, 1, "the window", "From here you can see the mountain path. Someone has scratched a tally into the sill: more marks than a winter has days."),
                (11, 1, "his bed", "Two pillows. Only one of them has been slept on for a long time."),
            },
            new[]   // the apothecary - Maren
            {
                (12, 0, "the cauldron", "Maren's tonic simmers. The recipe pinned above it is in two hands: hers, and an older one that looks just like hers."),
                (6, 1, "the cabinet", "Jars labelled by night, not by herb: 'the first night', 'the cold night', 'the night he did not come back'."),
                (8, 1, "the window", "A candle stub on the sill, burned down to the same height as the stub beside it, and the one beside that."),
                (11, 1, "her bed", "A man's coat is folded at the foot of the bed, brushed clean. It has not been worn this winter."),
            },
            new[]   // the map room - Oswin
            {
                (12, 0, "the globe", "A hole is burned clean through Oswin's globe, exactly where Brindlemoor should be."),
                (7, 1, "the chart table", "A map of the caverns. Then another. Then another. No two agree, and all of them are in Oswin's hand."),
                (11, 1, "the telescope", "It is fixed on the mountain's peak. Through it: a light, where no one lives."),
                (5, 1, "his bed", "The blankets are drawn tight, unslept in. A note on them: 'If I am asleep I will not remember. Do not let me sleep.'"),
            },
            new[]   // Pip's Oddments
            {
                (11, 0, "the crates", "Things Pip swears he has never sold: three compasses, none of them pointing north. All of them point at the mountain."),
                (5, 0, "the rug on the wall", "Woven into the border, very small: a village in the snow, and a sun that has been unpicked, thread by thread."),
                (6, 1, "the crates upstairs", "A peddler's licence from a town called Harrowmere. You have never heard of it. Neither, it seems, has any map in Oswin's room."),
                (11, 1, "his bed", "A packed bag, by the bed. It is always packed. Pip never leaves."),
            },
            new[]   // the lamp house - Bera
            {
                (12, 0, "the oil barrels", "Lamp oil, enough for one long night. The barrels are full. Bera says they are always full."),
                (8, 1, "the great lamp", "Scratched into the brass, in Bera's hand: 'While this burns, it cannot see us.'"),
                (5, 1, "the barrel", "A list is nailed to the lid: every lamp in Brindlemoor, each with a tick beside it. And a last line: 'the one on the mountain - not mine.'"),
                (11, 1, "her bed", "Bera sleeps in her boots. There is soot on the blanket in the shape of a hand that is not hers."),
            },
            new[]   // the village hall - the Elder
            {
                (12, 0, "the hearth", "Carved above it: the names of everyone who went up the mountain for the Sunstone. Yours is among them. The cut is old and weathered."),
                (9, 0, "the long table", "The Elder's cup is still warm. So is the one set across from it, though no one sits there."),
                (6, 1, "the bookcase", "A book of village winters. Every page after tonight is blank. Every page before it reads the same."),
                (8, 1, "the writing table", "A letter the Elder never sent: 'Forgive me. I did not know what the stone would ask of them.'"),
                (11, 1, "his bed", "The Elder does not seem to use his bed. The pillow is dusty. The candle beside it is new."),
            },
            new[]   // home
            {
                (7, 0, "the painting", "Your mother, at this window. She is not looking at the painter. She is looking at the mountain."),
                (12, 0, "the hearth", "Your boots by the fire, dry and warm. You do not remember taking them off."),
                (9, 0, "the armchair", "Your father's chair. The lantern he carried up the mountain, the winter you were small, is the one in your hand."),
                (5, 1, "the chest", "Your own handwriting, on a scrap at the bottom: 'The gate. Then down. Do not trust the first way down.' You do not remember writing it."),
                (12, 1, "the wardrobe", "Coats for a winter that should have ended by now. One sleeve is singed, as if by something very bright."),
                (8, 1, "your bed", "The blankets are thrown back the way you always leave them. Exactly the way. Every time."),
            },
        };

        static readonly int[] FireX = { 193, -1, -1, -1, -1, -1, 196, 198 };
        static readonly int[] FireY = { 8, 0, 0, 0, 0, 0, 4, 4 };

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
            if (fire != null)
            {
                bool lit = Index >= 0 && Index < FireX.Length && FireX[Index] >= 0;
                fire.gameObject.SetActive(lit);
                // floor-space pixel 24 is the middle of the door cell, where this entity stands
                if (lit) fire.localPosition = new Vector3((FireX[Index] - 24f) / 16f, FireY[Index] / 16f, 0f);
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
            // the things worth a closer look
            var curio = cfg.prefabs.Get("curio");
            if (curio != null && Index >= 0 && Index < Curios.Length)
                foreach (var c in Curios[Index])
                {
                    var cell = new Vector2Int(cx - 1 + c.cell, cy + c.storey * 7);
                    World.Spawn(curio, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("curio", cell, 0, 0, c.label + "|" + c.text));
                }
            if (Index == VillageBuilding.Hall)
            {
                var prefab = cfg.prefabs.Get("elder");
                var cell = new Vector2Int(cx + 6, cy);
                if (prefab != null) World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("elder", cell, -1));
            }
        }

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
