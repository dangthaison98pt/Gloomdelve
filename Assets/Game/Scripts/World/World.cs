using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Owns the tile grid and all entities; runs the fixed-step simulation and resolves explosions.</summary>
    [DefaultExecutionOrder(-50)]
    public class World : MonoBehaviour
    {
        public static World Instance { get; private set; }

        public GameConfig config;
        public LevelRenderer levelRenderer;
        public Transform entityRoot;

        [NonSerialized] public TileGrid Grid;
        [NonSerialized] public LevelData Level;
        [NonSerialized] public AreaConfig Area;
        [NonSerialized] public PlayerController Player;
        [NonSerialized] public int Frame;
        [NonSerialized] public System.Random fxRandom = new System.Random(1);

        readonly List<Entity> entities = new List<Entity>();
        readonly List<Entity> toAdd = new List<Entity>();
        readonly Dictionary<int, Entity> tileEntities = new Dictionary<int, Entity>();
        readonly Queue<PendingExplosion> explosions = new Queue<PendingExplosion>();
        readonly List<Entity> scratch = new List<Entity>();

        struct PendingExplosion
        {
            public Vector2 center; public float radius; public Entity source; public int generation; public int damage;
        }

        public IReadOnlyList<Entity> Entities => entities;
        public int EntityCount => entities.Count + toAdd.Count;
        public int ExplosionsThisLevel { get; private set; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Setup(LevelData level, AreaConfig area)
        {
            Level = level;
            Grid = level.grid;
            Area = area;
            fxRandom = new System.Random(level.seed);
            if (levelRenderer != null) levelRenderer.Build(level, area != null ? area.tiles : null);
        }

        public T Spawn<T>(T prefab, Vector2 position, SpawnPoint? sp = null) where T : Entity
        {
            if (prefab == null) return null;
            if (EntityCount >= config.maxEntities && !(prefab is PlayerController)) return null;
            var e = Instantiate(prefab, position, Quaternion.identity, entityRoot);
            e.pos = position;
            if (sp.HasValue) e.Configure(sp.Value);
            e.OnSpawn();
            toAdd.Add(e);
            return e;
        }

        public Entity SpawnById(string id, Vector2 position)
        {
            var p = config.prefabs.Get(id);
            if (p == null) { Debug.LogWarning($"[World] Unknown prefab id '{id}'"); return null; }
            return Spawn(p, position);
        }

        void FlushAdds()
        {
            if (toAdd.Count == 0) return;
            entities.AddRange(toAdd);
            toAdd.Clear();
        }

        void FixedUpdate()
        {
            if (Grid == null) return;
            float dt = GameConst.Dt;
            FlushAdds();
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null || e.Dead) continue;
                e.prevPos = e.pos;
                e.Tick(dt);
            }
            ProcessExplosions();
            FlushAdds();
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var e = entities[i];
                if (e == null) { entities.RemoveAt(i); continue; }
                if (e.Dead) { entities.RemoveAt(i); Destroy(e.gameObject); }
            }
            Frame++;
        }

        // ------------------------------------------------------------------ queries

        public void Query(Rect r, List<Entity> results, Func<Entity, bool> filter = null)
        {
            results.Clear();
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null || e.Dead) continue;
                if (!e.Bounds.Overlaps(r)) continue;
                if (filter != null && !filter(e)) continue;
                results.Add(e);
            }
        }

        public bool LineClear(Vector2 a, Vector2 b)
        {
            float d = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(d / 0.25f);
            for (int i = 1; i < steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                if (Grid.IsSolid(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y))) return false;
            }
            return true;
        }

        public bool PlayerAlive => Player != null && !Player.IsDead;

        // ------------------------------------------------------------------ tiles

        public void RegisterTileEntity(int x, int y, Entity e) => tileEntities[y * Grid.Width + x] = e;

        public bool DestroyTile(int x, int y, bool fx = true)
        {
            if (!Grid.IsDestructible(x, y)) return false;
            var t = Grid.Get(x, y);
            var center = new Vector2(x + 0.5f, y + 0.5f);
            if (t == TileType.GoldVein)
            {
                int n = 1 + fxRandom.Next(2);
                for (int i = 0; i < n; i++) SpawnLoot("gold_small", center);
                if (fxRandom.Next(3) == 0) SpawnLoot("gold_big", center);       // a vein is rare now: it pays a little better than a lone nugget
            }
            else if (t == TileType.GemVein) SpawnLoot("gem", center);

            int key = y * Grid.Width + x;
            if (tileEntities.TryGetValue(key, out var te))
            {
                tileEntities.Remove(key);
                if (te != null && !te.Dead && te is ITileBound tb) tb.OnTileDestroyed();
            }
            if (t == TileType.Ladder || t == TileType.Platform) { /* wood */ }
            Grid.Set(x, y, TileType.Empty);
            if (fx)
            {
                Color c = levelRenderer != null ? levelRenderer.DebrisColor(t) : new Color(0.45f, 0.3f, 0.2f);
                Fx.Debris(center, c, 5);
            }
            return true;
        }

        void SpawnLoot(string id, Vector2 at)
        {
            var e = SpawnById(id, at - new Vector2(0f, 0.25f));
            if (e != null) e.vel = new Vector2((float)(fxRandom.NextDouble() * 4 - 2), 3f + (float)fxRandom.NextDouble() * 3f);
        }

        // ------------------------------------------------------------------ explosions

        public void Explode(Vector2 center, float radius, Entity source, int generation = 0)
        {
            explosions.Enqueue(new PendingExplosion
            {
                center = center, radius = radius, source = source,
                generation = Mathf.Min(generation, config.maxChainGeneration), damage = config.explosionDamage
            });
        }

        void ProcessExplosions()
        {
            int n = 0;
            // Bounded per frame; the rest waits for the next frame -> chain reactions can never stall the game.
            while (explosions.Count > 0 && n < config.maxExplosionsPerFrame)
            {
                DoExplosion(explosions.Dequeue());
                n++;
            }
        }

        void DoExplosion(PendingExplosion p)
        {
            ExplosionsThisLevel++;
            int r = Mathf.CeilToInt(p.radius);
            int cx = Mathf.FloorToInt(p.center.x), cy = Mathf.FloorToInt(p.center.y);
            float r2 = p.radius * p.radius;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    float dx = x + 0.5f - p.center.x, dy = y + 0.5f - p.center.y;
                    if (dx * dx + dy * dy <= r2) DestroyTile(x, y, fx: (x + y) % 2 == 0);
                }

            scratch.Clear();
            scratch.AddRange(entities);
            scratch.AddRange(toAdd);
            foreach (var e in scratch)
            {
                if (e == null || e.Dead) continue;
                float reach = p.radius + Mathf.Max(e.size.x, e.size.y) * 0.5f;
                if ((e.Center - p.center).sqrMagnitude <= reach * reach)
                    e.OnExplosion(p.center, p.radius, p.source, p.generation, p.damage);
            }
            Fx.Explosion(p.center, p.radius);
            Sfx.Play("explosion", p.center);
            if (Player != null)
            {
                float d = Vector2.Distance(Player.Center, p.center);
                Fx.Shake(Mathf.Clamp01(1.2f - d / 14f) * 0.6f);
            }
        }
    }

    /// <summary>Entities attached to a tile (arrow trap, flame vent) are told when their tile is destroyed.</summary>
    public interface ITileBound
    {
        void OnTileDestroyed();
    }
}
