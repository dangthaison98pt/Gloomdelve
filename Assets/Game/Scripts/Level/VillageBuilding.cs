using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A house in the street of Brindlemoor: its whole front is the picture, the entity itself stands at the door.
    /// Up / Enter at the door goes inside (see VillageInterior). Buildings 0-5 belong to the villagers in the
    /// order of Villagers.All and stay dark and locked until their keeper has been freed; 6 is the village hall,
    /// 7 the player's own house.
    /// </summary>
    public class VillageBuilding : Entity, IInteractable, IQuestTarget
    {
        public const int Hall = 6, HomeOfPlayer = 7;
        /// <summary>What quests know each house by (QuestStep.target / waypoint), by building number.</summary>
        public static readonly string[] PlaceIds = { "forge", "ropewalk", "apothecary", "maproom", "oddments", "lamphouse", "hall", "home" };
        public string QuestId => Index >= 0 && Index < PlaceIds.Length ? PlaceIds[Index] : "house";
        public static readonly string[] PlaceNames = { "the Forge", "the Ropewalk", "the Apothecary", "the Map Room", "Pip's Oddments", "the Lamp House", "the Village Hall", "your house" };

        [Tooltip("Fronts by building number, dark and shut.")] public Sprite[] darkFronts;
        [Tooltip("The same fronts with the windows lit.")] public Sprite[] litFronts;
        [Tooltip("The lantern by the door and the glow of the windows (on while the building is open).")] public GlowLight[] lights;
        [Tooltip("Smoke from the chimney (rising while the building is open: the fire is lit).")] public ChimneySmoke smoke;

        // Where each front's chimney ends, in pixels of the picture (112 wide, door in the middle): the same numbers
        // the art generator draws them with (ArtGenerator.Village, Front).
        static readonly int[] BodyHeights = { 84, 90, 82, 94, 86, 96, 90, 80 };
        static Vector2 ChimneyTop(int kind)
        {
            int top = BodyHeights[kind] + 2;
            int x = kind == 0 ? 82 : kind % 2 == 0 ? 20 : 84, w = kind == 0 ? 14 : 9;
            int y = Mathf.Min(128 - 3, top + (kind == 0 ? 36 : 30)) + 2;
            return new Vector2((x + w * 0.5f - 56f) / 16f, y / 16f);
        }

        public int Index { get; private set; }
        public bool Open { get; private set; }

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            Index = sp.param;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            Open = Index >= Villagers.All.Length || Villagers.IsRescued(Villagers.All[Index].id);
            var pics = Open ? litFronts : darkFronts;
            if (sr != null && pics != null && Index >= 0 && Index < pics.Length && pics[Index] != null) sr.sprite = pics[Index];
            if (lights != null) foreach (var l in lights) if (l != null) l.SetOn(Open);
            if (smoke != null)
            {
                if (Index >= 0 && Index < BodyHeights.Length) smoke.transform.localPosition = ChimneyTop(Index);
                smoke.SetOn(Open);
            }
        }

        string Place => Index >= 0 && Index < PlaceNames.Length ? PlaceNames[Index] : "the house";

        public string GetPrompt(PlayerController p)
        {
            if (!Open) return "[enter]  Look";
            return "[enter]  Enter";
        }

        public void Interact(PlayerController p)
        {
            if (!Open)
            {
                // dark and shut: it says so when it is looked at
                string words = $"{char.ToUpper(Place[0]) + Place.Substring(1)} is dark and locked. Its keeper is lost on the mountain.";
                var d = GameDirector.Instance;
                if (d != null && d.hud != null) d.hud.ShowNote(words, HUD.ReadTime(words));
                Sfx.Play("error", pos);
                return;
            }
            if (ScreenFade.Busy) return;
            foreach (var e in World.Entities)
                if (e is VillageInterior room && !room.Dead && room.Index == Index)
                {
                    Sfx.Play("land", pos);
                    ScreenFade.Through(() => { if (room != null && !room.Dead && p != null) { room.Enter(p); QuestLog.Notify(QuestTrigger.Enter, QuestId); } });
                    return;
                }
        }

        /// <summary>The player steps out of this building's door: back on the street.</summary>
        public void StepOut(PlayerController p)
        {
            VillageInterior.Place(p, new Vector2(pos.x, pos.y));
            var dir = GameDirector.Instance;
            if (dir != null && dir.cameraFollow != null) dir.cameraFollow.SetBounds(StreetBounds());
            if (Ambience.Instance != null)
            {
                // the snow is falling again - all over the sky at once, not creeping in from the top
                Ambience.Instance.gameObject.SetActive(true);
                if (dir != null && dir.Stage != null) Ambience.Instance.Setup(dir.Stage.area);
            }
            Sfx.Play("land", pos);
            if (!nameShown && dir != null && dir.hud != null) { nameShown = true; dir.hud.ShowAreaIntro("Brindlemoor"); }
        }

        /// <summary>The village's name is shown once per visit, the first time the player steps out into the street.</summary>
        static bool nameShown;
        public static void NewVisit() { nameShown = false; }

        /// <summary>The part of the level the camera shows on the street: from just under the pavement up to the sky.</summary>
        public Rect StreetBounds()
        {
            var g = World.Grid;
            float y0 = pos.y - 2.2f;
            return new Rect(0f, y0, g.Width, g.Height - y0);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
