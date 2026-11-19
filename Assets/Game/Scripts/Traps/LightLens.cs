using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode trap: a lens of crystal set in the ceiling. On a steady beat it gathers light - a thin
    /// line down to the floor shows where (the warning) - and then lets it go: a column of light from the ceiling
    /// to the floor that burns whoever stands in it. It cannot be jumped: wait for the dark and walk through.
    /// Light does nothing to a resonant crystal: it does not ring for it. Neighbouring lenses are out of step.
    /// Placed by the level generator (AreaConfig.lensChance) in ceilings over plain floor.
    /// </summary>
    public class LightLens : Entity
    {
        [Tooltip("Seconds between two beams.")] public float restTime = 2.2f;
        [Tooltip("Seconds the thin line shows: the warning.")] public float warnTime = 0.7f;
        [Tooltip("Seconds the beam burns.")] public float fireTime = 0.6f;
        public int damage = 2;
        public float beamWidth = 0.5f;
        [Tooltip("Material of the beam (unlit, so it glows in the dark).")] public Material beamMaterial;

        float t, floorY;
        BeamVisual beam;
        bool hitPlayer;
        int lastPhase = -1;

        public override bool IsHazard => true;
        public override bool IsCeilingHazard => true;
        /// <summary>0 dark, 1 warning, 2 burning.</summary>
        public int Phase
        {
            get
            {
                float c = t % (restTime + warnTime + fireTime);
                return c < restTime ? 0 : c < restTime + warnTime ? 1 : 2;
            }
        }
        public bool Burning => Phase == 2;

        public override void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y + 1f - size.y);       // hangs from the ceiling of its cell
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            t = (Mathf.FloorToInt(pos.x) * 0.31f) % 1f * (restTime + warnTime + fireTime);
            beam = BeamVisual.Create(this, beamMaterial);
        }

        float FloorBelow()
        {
            var g = World.Grid;
            int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y - 0.05f);
            while (y > 0 && !g.IsSolid(x, y)) y--;
            return y + 1f;
        }

        public override void Tick(float dt)
        {
            if (AutoTest.Active) return;            // the test bot replays a plan made without a clock
            t += dt;
            int ph = Phase;
            if (ph != lastPhase)
            {
                lastPhase = ph;
                floorY = FloorBelow();
                hitPlayer = false;
                if (ph == 2) Sfx.Play("flame", pos, 0.7f, 1.6f);
            }
            if (ph != 2) return;
            var column = new Rect(pos.x - beamWidth * 0.5f, floorY, beamWidth, pos.y - floorY);
            var p = World.Player;
            if (!hitPlayer && p != null && !p.IsDead && column.Overlaps(p.Bounds))
            {
                hitPlayer = true;
                p.TakeDamage(new DamageInfo(damage, DamageType.Fire, this, new Vector2((p.pos.x >= pos.x ? 1 : -1) * 5f, 5f), "a beam of light"));
            }
            Enemy burnt = null;
            foreach (var e in World.Entities)
            {
                if (e.Dead) continue;
                if (burnt == null && e is Enemy en && !(en is Boss) && column.Overlaps(en.Bounds) && World.Frame % 20 == 0) burnt = en;
            }
            if (burnt != null) burnt.TakeDamage(new DamageInfo(1, DamageType.Fire, this, Vector2.zero, "a beam of light"));
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (beam == null) return;
            int ph = Phase;
            if (ph == 0 || AutoTest.Active) { beam.Hide(); return; }
            Vector2 a = new Vector2(pos.x, pos.y), b = new Vector2(pos.x, floorY);
            if (ph == 1) beam.Show(a, b, 0.06f, new Color(0.7f, 1f, 1f, 0.45f));
            else beam.Show(a, b, beamWidth * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f)), new Color(0.75f, 1f, 1f, 0.9f));
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (beam != null) Destroy(beam.gameObject);
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
