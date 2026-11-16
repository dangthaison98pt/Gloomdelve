using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Pot or crate: breaks when whipped, hit hard, burned or blasted, releasing its contents.</summary>
    public class Breakable : Item
    {
        [Header("Breakable")]
        public bool breakOnWhip = true;
        public Color debrisColor = new Color(0.65f, 0.4f, 0.25f);
        public string breakSfx = "pot_break";
        [Tooltip("Possible contents (ids). Use 'enemy' for a random ground enemy of the area, empty string for nothing.")]
        public WeightedId[] contents =
        {
            new WeightedId("gold_small", 4f), new WeightedId("gem", 0.8f), new WeightedId("enemy", 1.2f), new WeightedId("heart", 0.5f), new WeightedId("", 3f)
        };

        bool broken;

        public override void Break()
        {
            if (broken || Dead) return;
            broken = true;
            if (holder != null) holder.DropHeld();
            Fx.Debris(Center, debrisColor, 8);
            Sfx.Play(breakSfx, pos);
            var rng = new Rng(Rng.Hash(Mathf.FloorToInt(pos.x * 16), Mathf.FloorToInt(pos.y * 16), World.Frame));
            string id = WeightedId.Pick(contents, rng);
            if (id == "enemy" && World.Area != null) id = WeightedId.Pick(World.Area.groundEnemies, rng);
            if (!string.IsNullOrEmpty(id))
            {
                var e = World.SpawnById(id, new Vector2(pos.x, pos.y + 0.05f));
                if (e != null) e.vel = new Vector2(0f, 4f);
                if (e is Enemy en) en.stunTimer = 0.4f;
            }
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir)
        {
            if (breakOnWhip) Break(); else base.OnWhipped(p, damage, dir);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (d.type == DamageType.Fire || d.type == DamageType.Explosion || d.type == DamageType.Crush || d.amount >= 2) { Break(); return true; }
            return base.TakeDamage(d);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) => Break();
        public override bool CanBeHitByItems => !broken;
    }
}
