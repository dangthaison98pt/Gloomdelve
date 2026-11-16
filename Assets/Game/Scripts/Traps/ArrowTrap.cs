using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Stone block that fires one arrow when anything moves in front of it (player, enemy or thrown object).</summary>
    public class ArrowTrap : Entity, ITileBound
    {
        public int range = 7;
        public float arrowSpeed = 16f;
        public Sprite armedSprite;
        public Sprite emptySprite;

        Vector2Int cell;
        bool armed = true;
        int scanTimer;

        public override void Configure(SpawnPoint sp)
        {
            cell = sp.cell;
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y);
            facing = sp.dir == 0 ? 1 : sp.dir;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            World.RegisterTileEntity(cell.x, cell.y, this);
            if (sr != null) sr.sprite = armedSprite;
        }

        public override void Tick(float dt)
        {
            if (!armed) return;
            if (World.Grid.Get(cell.x, cell.y) != TileType.ArrowTrap) { Despawn(); return; }
            if (++scanTimer < 3) return;
            scanTimer = 0;
            int len = 0;
            for (int i = 1; i <= range; i++)
            {
                if (World.Grid.IsSolid(cell.x + facing * i, cell.y)) break;
                len = i;
            }
            if (len == 0) return;
            float x0 = facing > 0 ? cell.x + 1 : cell.x - len;
            var ray = new Rect(x0, cell.y + 0.05f, len, 0.9f);
            var list = World.Entities;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.Dead || e == this || !e.TriggersTraps) continue;
                if (e.Bounds.Overlaps(ray)) { Fire(); return; }
            }
        }

        void Fire()
        {
            armed = false;
            if (sr != null) sr.sprite = emptySprite;
            var a = World.SpawnById("arrow", new Vector2(cell.x + 0.5f + facing * 0.75f, cell.y + 0.35f)) as Arrow;
            if (a != null) a.Fire(this, facing, arrowSpeed);
            Sfx.Play("arrow", pos);
            Fx.Dust(new Vector2(cell.x + 0.5f + facing * 0.6f, cell.y + 0.4f), 2);
        }

        public void OnTileDestroyed() => Despawn();
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
