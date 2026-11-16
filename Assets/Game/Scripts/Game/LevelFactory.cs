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
                return LevelGenerator.Generate(req);
            }
            var d = HandcraftedLevel.Parse(stage.handcrafted.text, stage.displayName, stage.kind, seed);
            if (stage.kind == LevelKind.Final)
                d.validation = LevelValidator.Validate(d, LevelValidator.DefaultOptions(cfg.player));
            return d;
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
