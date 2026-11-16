using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A treasure chest. Opening it lays out its contents - by default three pieces of equipment the run does
    /// not own yet - which are then free to pick up.
    /// </summary>
    public class Chest : Entity, IInteractable
    {
        public Sprite openSprite;
        public int itemCount = 3;

        public bool Opened { get; private set; }
        public bool UpTriggers => true;

        public string GetPrompt(PlayerController p) => Opened ? null : "[enter] / [up]  open the chest";

        public void Interact(PlayerController p)
        {
            if (Opened) return;
            Opened = true;
            if (sr != null && openSprite != null) sr.sprite = openSprite;
            Sfx.Play("idol", pos);
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 16, 5f);

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

            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            for (int i = 0; i < picks.Count; i++)
            {
                // one item on the chest's own cell, the others two cells apart on either side
                var cell = new Vector2Int(cx + Mathf.RoundToInt((i - (picks.Count - 1) * 0.5f) * 2f), cy);
                World.Spawn(prefab, new Vector2(cell.x + 0.5f, cy), new SpawnPoint("shopitem", cell, 0, i, picks[i]));
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
