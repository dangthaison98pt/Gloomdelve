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
        [Tooltip("The fire (a child object with its flames and light): put in the hearth of the rooms that have one.")] public Transform fire;

        /// <summary>Where the fire burns in each room, in floor-space pixels of the picture (-1 = no fire): the forge's
        /// furnace, the hearths of the hall and of the player's house. Same numbers as ArtGenerator.Village, Room.</summary>
        static readonly int[] FireX = { 193, -1, -1, -1, -1, -1, 196, 198 };
        static readonly int[] FireY = { 8, 0, 0, 0, 0, 0, 4, 4 };

        public int Index { get; private set; }
        bool cameraHere;

        public bool UpTriggers => true;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            Index = sp.param;
        }

        /// <summary>Left edge of the room's floor (the door is the second cell).</summary>
        float RoomLeft => Mathf.Floor(pos.x) - 1f;
        Rect RoomRect => new Rect(RoomLeft, pos.y, RoomW, RoomH);
        /// <summary>What the camera is kept to while the player is inside: the middle of the room. The room is smaller
        /// than the view, so the camera stands still on it instead of sliding after the player.</summary>
        Rect Frame => new Rect(RoomLeft + RoomW * 0.5f - 0.05f, pos.y + RoomH * 0.5f - 0.05f, 0.1f, 0.1f);

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (sr != null && rooms != null && Index >= 0 && Index < rooms.Length && rooms[Index] != null) sr.sprite = rooms[Index];
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
            else if (Index == VillageBuilding.Hall)
            {
                var prefab = cfg.prefabs.Get("elder");
                var cell = new Vector2Int(cx + 6, cy);
                if (prefab != null) World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("elder", cell, -1));
            }
        }

        public string GetPrompt(PlayerController p) => "[up] / [enter]  go outside";

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
