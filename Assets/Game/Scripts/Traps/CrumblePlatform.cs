using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A trap for climbs: a rotten wooden platform. It holds for a moment when stood on, then gives way; a few
    /// seconds later it is back. Keep moving up - do not rest on it.
    /// It owns one Platform tile of the grid (removed while broken). Room templates place it with the custom
    /// marker 'c' (GameConfig.customMarkers), which also writes the Platform tile.
    /// </summary>
    public class CrumblePlatform : Entity
    {
        [Tooltip("Seconds it holds once someone stands on it.")] public float holdTime = 0.5f;
        [Tooltip("Seconds until it is back.")] public float respawnTime = 3.5f;
        public Color debrisColor = new Color(0.55f, 0.36f, 0.2f);

        enum State { Solid, Shaking, Gone }
        State state;
        float timer;
        Vector2Int cell;
        Vector2 home;

        public bool Broken => state == State.Gone;
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }

        public override void Configure(SpawnPoint sp)
        {
            cell = sp.cell;
            pos = new Vector2(cell.x + 0.5f, cell.y);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.RoundToInt(pos.y));
            home = pos;
            if (World.Grid.IsEmpty(cell.x, cell.y)) World.Grid.Set(cell.x, cell.y, TileType.Platform);
        }

        bool PlayerOnTop()
        {
            var p = World.Player;
            return p != null && !p.IsDead && p.grounded && Mathf.Abs(p.pos.y - (cell.y + 1f)) < 0.06f &&
                   Mathf.Abs(p.pos.x - (cell.x + 0.5f)) < 0.5f + p.size.x * 0.5f;
        }

        public override void Tick(float dt)
        {
            if (AutoTest.Active) return;          // the test bot replays a plan made on the intact level
            var g = World.Grid;
            switch (state)
            {
                case State.Solid:
                    if (g.Get(cell.x, cell.y) != TileType.Platform) { state = State.Gone; timer = respawnTime; if (sr != null) sr.enabled = false; break; }
                    if (PlayerOnTop()) { state = State.Shaking; timer = holdTime; Sfx.Play("crack", pos, 0.7f, 1.3f); }
                    break;
                case State.Shaking:
                    timer -= dt;
                    pos = home + new Vector2(Mathf.Sin(timer * 70f) * 0.04f, 0f);
                    if (timer <= 0f)
                    {
                        pos = home;
                        g.Set(cell.x, cell.y, TileType.Empty);
                        Fx.Debris(new Vector2(cell.x + 0.5f, cell.y + 0.8f), debrisColor, 6);
                        Sfx.Play("crate_break", pos, 0.8f);
                        if (sr != null) sr.enabled = false;
                        state = State.Gone; timer = respawnTime;
                    }
                    break;
                case State.Gone:
                    timer -= dt;
                    if (timer <= 0f && g.IsEmpty(cell.x, cell.y))
                    {
                        g.Set(cell.x, cell.y, TileType.Platform);
                        if (sr != null) sr.enabled = true;
                        Fx.Dust(new Vector2(cell.x + 0.5f, cell.y + 0.9f), 3);
                        state = State.Solid;
                    }
                    break;
            }
        }
    }
}
