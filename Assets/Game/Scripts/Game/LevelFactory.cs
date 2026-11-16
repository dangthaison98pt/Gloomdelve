using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Creates LevelData for a stage (procedural or handcrafted) and spawns its entities.</summary>
    public static class LevelFactory
    {
        public static int LevelSeed(int runSeed, int stageIndex) => Rng.Hash(runSeed, stageIndex * 7919 + 13);

        public static LevelData Create(GameConfig cfg, StageDef stage, int runSeed, int stageIndex)
        {
            int seed = LevelSeed(runSeed, stageIndex);
            if (stage.kind == LevelKind.Area)
            {
                var req = new GenRequest
                {
                    seed = seed, area = stage.area, levelNumber = stage.levelNumber, name = stage.displayName, config = cfg,
                    templates = RoomTemplates.Parse(cfg.roomTemplates.text)
                };
                if (EditorPlaytest.Active && !string.IsNullOrEmpty(EditorPlaytest.RoomName))
                    req.forceTemplate = req.templates.Find(t => t.name == EditorPlaytest.RoomName);
                return LevelGenerator.Generate(req);
            }
            var d = HandcraftedLevel.Parse(stage.handcrafted.text, stage.displayName, stage.kind, seed, cfg.customMarkers);
            if (stage.kind == LevelKind.Final)
                d.validation = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
            if (stage.kind == LevelKind.Boss || stage.kind == LevelKind.Rest)
            {
                if (stage.area != null) d.grip = stage.area.groundGrip;   // an ice boss is fought on ice
                StockShop(d, cfg, seed);
            }
            return d;
        }

        /// <summary>Hand-made shop (M markers): supplies first, then random equipment; the shopkeeper guards the row.</summary>
        static void StockShop(LevelData d, GameConfig cfg, int seed)
        {
            var idx = new System.Collections.Generic.List<int>();
            for (int i = 0; i < d.spawns.Count; i++) if (d.spawns[i].id == "shopitem") idx.Add(i);
            if (idx.Count == 0 || cfg.shop == null) return;
            idx.Sort((a, b) => d.spawns[a].cell.x.CompareTo(d.spawns[b].cell.x));
            var offers = new System.Collections.Generic.List<string> { "bombs", "ropes", "heart" };
            var ups = new System.Collections.Generic.List<string>();
            foreach (var o in cfg.shop.offers) if (o.isUpgrade) ups.Add(o.id);
            var rng = new Rng(seed + 5);
            while (offers.Count < idx.Count && ups.Count > 0)
            {
                int k = rng.Range(0, ups.Count);
                offers.Add(ups[k]);
                ups.RemoveAt(k);
            }
            int minX = int.MaxValue, maxX = int.MinValue, y = d.spawns[idx[0]].cell.y;
            for (int s = 0; s < idx.Count; s++)
            {
                var sp = d.spawns[idx[s]];
                d.spawns[idx[s]] = new SpawnPoint("shopitem", sp.cell, 0, s, offers[Mathf.Min(s, offers.Count - 1)]);
                minX = Mathf.Min(minX, sp.cell.x); maxX = Mathf.Max(maxX, sp.cell.x);
            }
            foreach (var sp in d.spawns)
                if (sp.id == "shopkeeper") { minX = Mathf.Min(minX, sp.cell.x); maxX = Mathf.Max(maxX, sp.cell.x); }
            d.shopRects.Add(new RectInt(minX - 1, y, maxX - minX + 3, 3));
            d.hasShop = true;
        }

        public static void SpawnAll(World world, LevelData d, PrefabRegistry reg)
        {
            foreach (var sp in d.spawns)
            {
                var prefab = reg.Get(sp.id);
                if (prefab == null) { Debug.LogWarning($"[Spawn] Missing prefab '{sp.id}'"); continue; }
                world.Spawn(prefab, new Vector2(sp.cell.x + 0.5f, sp.cell.y), sp);
            }
        }

        /// <summary>Connects shop items to their shopkeeper (call after the first simulation step flushed spawns).</summary>
        public static void LinkShops(World world, LevelData d)
        {
            foreach (var r in d.shopRects)
            {
                Shopkeeper keeper = null;
                foreach (var e in world.Entities)
                    if (e is Shopkeeper k && r.Contains(new Vector2Int(Mathf.FloorToInt(e.pos.x), Mathf.FloorToInt(e.pos.y)))) keeper = k;
                if (keeper == null) continue;
                keeper.shopRect = r;
                foreach (var e in world.Entities)
                    if (e is ShopItem s && r.Contains(new Vector2Int(Mathf.FloorToInt(e.pos.x), Mathf.FloorToInt(e.pos.y)))) keeper.AddWare(s);
            }
        }
    }
}
