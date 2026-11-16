using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A trap for climbs: a stone mouth in the ceiling above a shaft. While someone is below it (on the ladder,
    /// usually) it rattles, then drops a rock straight down - again and again, so the climb has to be timed:
    /// wait for a rock, then go, or step off the ladder when it rattles.
    /// Room templates place it with the custom marker 'r' (GameConfig.customMarkers).
    /// </summary>
    public class RockDropper : Entity
    {
        public float interval = 2.6f;
        [Tooltip("Rattle time before the rock falls.")] public float warnTime = 0.65f;
        public float range = 12f;
        [Tooltip("How far to the side of its column it still notices the player.")] public float halfWidth = 1.1f;
        public string rockId = "stalactite";

        float timer, warning;
        Vector2 home;

        public override bool IsHazard => true;
        public override bool IsCeilingHazard => true;
        public bool Warning => warning > 0f;

        public override void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y + 1f - size.y);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            home = pos;
            timer = 0.8f;
        }

        bool SomeoneBelow()
        {
            var p = World.Player;
            return p != null && !p.IsDead && Mathf.Abs(p.pos.x - home.x) < halfWidth && p.pos.y < home.y && home.y - p.pos.y < range;
        }

        public override void Tick(float dt)
        {
            timer -= dt;
            if (warning > 0f)
            {
                warning -= dt;
                pos = home + new Vector2(Mathf.Sin(warning * 80f) * 0.04f, 0f);
                if (warning <= 0f) { pos = home; Drop(); }
            }
            else if (timer <= 0f && SomeoneBelow())
            {
                warning = warnTime;
                timer = interval;
                Sfx.Play("crack", pos, 0.8f);
                Fx.Dust(new Vector2(home.x, home.y - 0.1f), 3);
            }
        }

        void Drop()
        {
            var prefab = World.config.prefabs.Get(rockId);
            if (prefab == null) return;
            var cell = new Vector2Int(Mathf.FloorToInt(home.x), Mathf.FloorToInt(home.y) - 1);
            if (World.Grid.IsSolid(cell.x, cell.y)) return;   // ladders and platforms do not stop a falling rock
            World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint(rockId, cell, 0, 1));   // param 1 = already falling
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
