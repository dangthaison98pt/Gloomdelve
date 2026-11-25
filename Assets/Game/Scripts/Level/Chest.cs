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
        [Tooltip("Picture of the hammer's head (the chest that holds it shows it on a card).")] public Sprite hammerHeadIcon;
        public int itemCount = 3;

        [Tooltip("Set (by its spawn point's text): the chest holds this piece of gear and nothing else.")] public string gearId = "";

        [Header("Loot chest (the plain wooden chests of the mountain)")]
        [Tooltip("A loot chest: opened, it throws out a few things from 'lootContents' - and now and then a piece of equipment the run does not own.")]
        public bool loot;
        public WeightedId[] lootContents =
        {
            new WeightedId("gold_big", 4f), new WeightedId("gem", 1.5f), new WeightedId("bombbag", 1.2f), new WeightedId("ropepile", 1.2f),
            new WeightedId("pebbles", 2f), new WeightedId("heart", 1.5f),
        };
        [Tooltip("How many things it throws out.")] public int lootRolls = 3;
        [Tooltip("Chance that a piece of equipment the run does not own yet lies beside it as well (free to take).")]
        [Range(0, 1)] public float lootGearChance = 0.25f;
        [Tooltip("Added to that chance for every loot chest of the run opened in a row without one (RunState.lootMisses); a piece found sets it back.")]
        [Range(0, 1)] public float lootGearStep = 0.1f;

        /// <summary>The chance of a piece of equipment in the next loot chest of this run: the base, and a step more for every chest in a row without one.</summary>
        public float GearChanceFor(RunState run) => Mathf.Clamp01(lootGearChance + lootGearStep * (run != null ? run.lootMisses : 0));

        /// <summary>Tests: what the last loot chest opened gave (ids), and whether a piece of equipment was among it.</summary>
        public static readonly List<string> lastLoot = new List<string>();
        public static string lastLootGear;

        public bool Opened { get; private set; }

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            if (!string.IsNullOrEmpty(sp.text)) gearId = sp.text;
        }

        /// <summary>Tests: loot chests are placed even in a test run.</summary>
        public static bool lootForTests;

        /// <summary>
        /// The plain wooden chests of the mountain: up to GameConfig.lootChestsPerLevel in a generated level, each
        /// there by lootChestChance (seeded by the level), on a floor in a side room off the main path - found by
        /// looking around (Story.Spots: a place the player can reach). Not in the village, the tutorial, an arena,
        /// the camp or the vault, nor in a Challenge fight.
        /// </summary>
        public static List<Chest> PlaceLoot(World w, LevelData level, StageDef stage, RunState run, GameConfig cfg)
        {
            var made = new List<Chest>();
            if (w == null || level == null || stage == null || run == null || cfg == null || cfg.prefabs == null) return made;
            if (stage.kind != LevelKind.Area || run.isTutorial || run.bossPractice || run.inVillage) return made;
            if ((ScenarioRunner.Active || AutoTest.Active) && !lootForTests) return made;
            var prefab = cfg.prefabs.Get("lootchest");
            if (prefab == null || cfg.lootChestsPerLevel <= 0) return made;
            var rng = new Rng(Rng.Hash(level.seed, 6113));
            int n = 0;
            for (int i = 0; i < cfg.lootChestsPerLevel; i++) if (rng.Chance(cfg.lootChestChance)) n++;
            if (n == 0) return made;
            // away from whatever the story has put in the level already (a pillar, a stranger, a chest, a spirit, a cage)
            var avoid = new List<Vector2Int>();
            foreach (var e in w.Entities)
                if (e is RunePillar || e is Stranger || e is Ghost || e is Chest || e is Captive)
                    avoid.Add(new Vector2Int(Mathf.FloorToInt(e.pos.x), Mathf.RoundToInt(e.pos.y)));
            foreach (var cell in Story.Spots(level, cfg.player, n, true, avoid))
                if (w.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("lootchest", cell)) is Chest c) made.Add(c);
            return made;
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
                    if (d0 != null && d0.hud != null) d0.hud.Gain("recall", charm.displayName, charm.icon, 1, $"Hold [gear] to take it in hand.  {charm.description}", true);
                }
                return;
            }
            if (gearId == Story.HeadChest)
            {
                // the chest the old signs speak of: the head of the hammer
                Story.GotPart(RunSession.Current, World.config, "hammerhead");
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
                    // on a card the first time in this save; after that it is known, and is a line at the left like
                    // anything picked up again (every climb finds this chest anew: the card every time was too much)
                    if (dir != null && dir.hud != null) dir.hud.Gain(gearId, gear.displayName, gear.icon, 1, $"Hold [gear] to equip it.  {gear.description}");
                }
                return;
            }

            if (loot) { OpenLoot(); return; }

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

        /// <summary>
        /// A loot chest opens: a few things jump out of it (seeded by the level and the chest's place, so a level
        /// played again gives the same), and - by 'lootGearChance' - a piece of equipment the run does not own yet
        /// lies beside it, free to take.
        /// </summary>
        void OpenLoot()
        {
            lastLoot.Clear(); lastLootGear = null;
            int seed = World.Level != null ? World.Level.seed : 0;
            var rng = new Rng(Rng.Hash(seed, Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y) + 7919));
            for (int i = 0; i < lootRolls; i++)
            {
                string id = WeightedId.Pick(lootContents, rng);
                if (string.IsNullOrEmpty(id)) continue;
                var e = World.SpawnById(id, new Vector2(pos.x, pos.y + 0.4f));
                if (e == null) continue;
                // thrown out in a little fan, so the pieces land apart
                float t = lootRolls > 1 ? i / (float)(lootRolls - 1) - 0.5f : 0f;
                e.vel = new Vector2(t * 5f + rng.Range(-0.6f, 0.6f), rng.Range(6.5f, 8.5f));
                lastLoot.Add(id);
            }
            var run = RunSession.Current;
            var shop = World.config.shop;
            var prefab = World.config.prefabs.Get("shopitem");
            if (shop == null || prefab == null) return;
            var pool = new List<string>();
            foreach (var o in shop.offers) if (o.isUpgrade && (run == null || !run.HasUpgrade(o.id))) pool.Add(o.id);
            if (pool.Count == 0) return;                       // (everything is owned: nothing missed either)
            // no piece this time: the next chest is a step likelier to have one
            if (!rng.Chance(GearChanceFor(run))) { if (run != null) run.lootMisses++; return; }
            string gear = pool[rng.Range(0, pool.Count)];
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            var g = World.Grid;
            foreach (int dx in new[] { 2, -2, 3, -3 })
            {
                int x = cx + dx;
                if (!g.IsEmpty(x, cy) || !(g.IsSolid(x, cy - 1) || g.IsPlatformTop(x, cy - 1))) continue;
                World.Spawn(prefab, new Vector2(x + 0.5f, cy), new SpawnPoint("shopitem", new Vector2Int(x, cy), 0, 0, gear));
                lastLootGear = gear;
                if (run != null) run.lootMisses = 0;          // found: back to the base chance
                Fx.Sparks(new Vector2(x + 0.5f, cy + 0.5f), new Color(1f, 0.9f, 0.4f), 10, 4f);
                return;
            }
            if (run != null) run.lootMisses++;                // (no floor beside it to lay the piece on: it counts as none)
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
