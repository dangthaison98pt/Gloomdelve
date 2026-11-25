using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The room inside the dog's kennel: one low, warm room (ten by five cells) that lies apart from the street, like
    /// the rooms of the houses (VillageInterior). The player crawls into it from the kennel (Kennel) and out again
    /// by its little door in the back wall on the left (the button there: no need to get down for it). The dog sleeps in its basket here at night; by day it is out in the street and the basket is empty.
    /// The entity stands in the first cell of the room's floor, by the door.
    /// </summary>
    public class KennelRoom : Entity, IInteractable
    {
        public const int RoomW = 10, RoomH = 5;
        [Tooltip("How far the camera stands back in here (1 = as on the street).")] public float zoom = 0.72f;
        [Tooltip("Where the dog's basket is, in floor-space pixels across (ArtGenerator.Kennel).")] public float basketX = 104f;

        bool cameraHere;

        /// <summary>A spot of the room picture (floor-space pixels: x across from the left wall, y up from the floor) in the world.</summary>
        public Vector2 At(float x, float y) => new Vector2(Mathf.Floor(pos.x) + x / 16f, pos.y + y / 16f);
        Rect RoomRect => new Rect(Mathf.Floor(pos.x), pos.y, RoomW, RoomH);
        Rect Frame => new Rect(Mathf.Floor(pos.x) + RoomW * 0.5f - 0.05f, pos.y + RoomH * 0.5f - 0.05f, 0.1f, 0.1f);
        public Vector2 BasketSpot => At(basketX, 10f);      // (on the cushion)

        public bool Contains(Vector2 at) => RoomRect.Contains(at);
        /// <summary>Where the little chest stands (it holds the two little dolls): by the door, floor-space pixels.</summary>
        public Vector2 ChestSpot => At(36f, 0f);

        public override void OnSpawn()
        {
            base.OnSpawn();
            // the little chest by the door, with the two little dolls in it (open and empty once they are found)
            var cfg = World.config;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("dollschest") : null;
            if (prefab != null)
            {
                var at = ChestSpot;
                World.Spawn(prefab, at, new SpawnPoint("dollschest", new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y))));
            }
        }
        public bool PlayerHere => World.Player != null && Contains(new Vector2(World.Player.pos.x, World.Player.pos.y + 0.4f));

        public static KennelRoom Find(World w)
        {
            if (w == null) return null;
            foreach (var e in w.Entities) if (e is KennelRoom r && !r.Dead) return r;
            return null;
        }

        /// <summary>At the little door in the back wall, left (floor-space pixels 5 to 21).</summary>
        bool AtDoor(PlayerController p) => p != null && !p.IsDead && p.grounded && p.pos.x - Mathf.Floor(pos.x) < 1.7f && Mathf.Abs(p.pos.y - pos.y) < 0.5f;

        public string GetPrompt(PlayerController p) => AtDoor(p) && !ScreenFade.Busy ? "[enter]  Crawl out" : null;

        public void Interact(PlayerController p)
        {
            if (ScreenFade.Busy || GetPrompt(p) == null) return;
            var kennel = Kennel.Find(World);
            if (kennel == null) return;
            Sfx.Play("land", pos, 0.35f, 1.2f);
            ScreenFade.Through(() =>
            {
                if (p == null || p.IsDead) return;
                Leave();
                kennel.ComeOut(p);
            });
        }

        /// <summary>The player comes in on all fours through the little door.</summary>
        public void Enter(PlayerController p)
        {
            VillageInterior.Place(p, At(22f, 0f));
            p.motor.crouching = true;
            p.facing = p.motor.facing = 1;
            HoldCamera();
        }

        void HoldCamera()
        {
            cameraHere = true;
            var dir = GameDirector.Instance;
            if (dir != null && dir.cameraFollow != null) dir.cameraFollow.SetBounds(Frame, zoom);
            if (Ambience.Instance != null) Ambience.Instance.gameObject.SetActive(false);        // no snow indoors
        }

        /// <summary>Back out to the street: the camera on the street again, the snow falling; the dog comes out too by day.</summary>
        void Leave()
        {
            cameraHere = false;
            var dir = GameDirector.Instance;
            VillageBuilding street = null;
            foreach (var e in World.Entities) if (e is VillageBuilding b && !b.Dead) { street = b; break; }
            if (dir != null && dir.cameraFollow != null)
                dir.cameraFollow.SetBounds(street != null ? street.StreetBounds() : new Rect(0f, 0f, World.Grid.Width, World.Grid.Height));
            if (Ambience.Instance != null)
            {
                Ambience.Instance.gameObject.SetActive(true);
                if (dir != null && dir.Stage != null) Ambience.Instance.Setup(dir.Stage.area);
            }
            var dog = VillageDog.Find(World);
            if (dog != null && !VillageDog.NightNow) dog.OutOfBed();
        }

        public override void Tick(float dt)
        {
            // the camera finds the room by itself if the player is put in it some other way
            if (cameraHere) return;
            if (PlayerHere) HoldCamera();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
