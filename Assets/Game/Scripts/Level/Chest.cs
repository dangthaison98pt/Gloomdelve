using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A treasure chest. Opening it lays out its contents - by default three pieces of equipment the run does
    /// not own yet - which are then free to pick up.
    /// A chest with 'gearId' set is another thing: the chest by the gate of an area that asks for a piece of gear
    /// (AreaConfig.grantGear - the glider under the open sky). It holds that one piece and hands it over when it
    /// is opened, so that the player sees what they have been given and where it came from.
    /// </summary>
    public class Chest : Entity, IInteractable
    {
        public Sprite openSprite;
        [Tooltip("Picture of the Field Guide (the chest that holds it shows it on a card).")] public Sprite bookIcon;
        public int itemCount = 3;

        [Tooltip("Set (by its spawn point's text): the chest holds this piece of gear and nothing else.")] public string gearId = "";

        public bool Opened { get; private set; }

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            if (!string.IsNullOrEmpty(sp.text)) gearId = sp.text;
        }

        /// <summary>Puts a chest holding this piece of gear on the floor beside 'near' (the level's entrance): the nearest free cell.</summary>
        public static Chest SpawnGearChest(World w, string gear, Vector2 near)
        {
            var prefab = w.config != null && w.config.prefabs != null ? w.config.prefabs.Get("gearchest") : null;
            var g = w.Grid;
            if (prefab == null || g == null) return null;
            int cx = Mathf.FloorToInt(near.x), cy = Mathf.RoundToInt(near.y);
            foreach (int dx in new[] { 2, -2, 3, -3, 1, -1, 4, -4 })
            {
                int x = cx + dx;
                if (!g.IsEmpty(x, cy) || !g.IsEmpty(x, cy + 1) || !(g.IsSolid(x, cy - 1) || g.IsPlatformTop(x, cy - 1))) continue;
                return w.Spawn(prefab, new Vector2(x + 0.5f, cy), new SpawnPoint("gearchest", new Vector2Int(x, cy), 0, 0, gear)) as Chest;
            }
            return null;
        }
        public string GetPrompt(PlayerController p) => Opened ? null : "[enter]  Open";

        public void Interact(PlayerController p)
        {
            if (Opened) return;
            Opened = true;
            if (sr != null && openSprite != null) sr.sprite = openSprite;
            Sfx.Play("idol", pos);
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 16, 5f);
            if (gearId == Codex.Key)
            {
                // the chest at the end of the tutorial: the Field Guide
                // ... and the Recall Charm, which every climb after this one starts with
                Codex.Give();
                GameDirector.tutorialChestOpened = true;
                var d0 = GameDirector.Instance;
                if (d0 != null && d0.hud != null) d0.hud.Gain(Codex.Key, Codex.Title, bookIcon, 1, Codex.About + "\n\nOpen your bag ([inventory]) and read it now: the gate waits for that.", true);
                var s0 = RunSession.Current;
                var charm = World.config.FindGear("recall");
                if (s0 != null && charm != null)
                {
                    if (!s0.gear.Contains("recall")) s0.gear.Add("recall");
                    if (d0 != null && d0.hud != null) d0.hud.Gain("gear:recall", charm.displayName, charm.icon, 1, $"Hold [gear] to take it in hand.  {charm.description}", true);
                }
                return;
            }
            if (!string.IsNullOrEmpty(gearId))
            {
                // the one piece of gear this place asks for
                var session = RunSession.Current;
                var gear = World.config.FindGear(gearId);
                if (session != null && gear != null && !session.gear.Contains(gearId)) session.gear.Add(gearId);
                var dir = GameDirector.Instance;
                if (gear != null)
                {
                    // always on a card, however often it has been found before: the area cannot be crossed without it
                    if (dir != null && dir.hud != null) dir.hud.Gain("gear:" + gearId, gear.displayName, gear.icon, 1, $"Hold [gear] to equip it.  {gear.description}", true);
                }
                return;
            }

            var shop = World.config.shop;
            var run = RunSession.Current;
            var prefab = World.config.prefabs.Get("shopitem");
            if (shop == null || prefab == null) return;
            // equipment not owned yet first, in a seeded order; topped up with supplies
            var pool = new List<string>();
            foreach (var o in shop.offers) if (o.isUpgrade && (run == null || !run.HasUpgrade(o.id))) pool.Add(o.id);
            var rng = new Rng(World.Level != null ? World.Level.seed + 77 : 77);
            var picks = new List<string>();
            while (picks.Count < itemCount && pool.Count > 0)
            {
                int k = rng.Range(0, pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            string[] fill = { "heart", "bombs", "ropes" };
            for (int i = 0; picks.Count < itemCount; i++) picks.Add(fill[i % fill.Length]);

            // Laid out in a row beside the chest, two cells apart - never on the chest itself, where the item
            // and the chest would overlap. To the right when there is floor there, else to the left.
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            var g = World.Grid;
            bool Fits(int dir)
            {
                for (int i = 1; i <= picks.Count; i++)
                {
                    int x = cx + dir * i * 2;
                    if (!g.IsEmpty(x, cy) || !(g.IsSolid(x, cy - 1) || g.IsPlatformTop(x, cy - 1))) return false;
                }
                return true;
            }
            int side = Fits(1) || !Fits(-1) ? 1 : -1;
            for (int i = 0; i < picks.Count; i++)
            {
                var cell = new Vector2Int(cx + side * (i + 1) * 2, cy);
                World.Spawn(prefab, new Vector2(cell.x + 0.5f, cy), new SpawnPoint("shopitem", cell, 0, i, picks[i]));
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
