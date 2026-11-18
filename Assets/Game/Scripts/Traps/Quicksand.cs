using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: one cell of quicksand in the floor. Walking through it is slow, and whoever stays in it
    /// sinks: after 'sinkTime' seconds in the sand (the cells of one pool count together) the player is pulled
    /// under, hurt and thrown back out. A jump gets out of it - so a pool is crossed in hops, not on foot.
    /// A bomb that comes to rest on it is swallowed before it can go off: bombs are for firm ground.
    /// Sand Striders (StatModifiers.sandGrip) make the wearer walk on it as on stone.
    /// Placed by the level generator (AreaConfig.quicksandChance) on long plain floors, clear of pits and doors.
    /// </summary>
    public class Quicksand : Entity
    {
        [Tooltip("Movement speed lost while standing in it (0.5 = half speed).")] [Range(0, 0.9f)] public float slow = 0.55f;
        [Tooltip("Seconds in the sand before it pulls the player under.")] public float sinkTime = 2f;
        public int damage = 1;
        public SpriteAnim animFlow;

        // the cells of a pool share one clock: stepping from one cell to the next does not start it again
        static float sunk;
        static int lastFrame = -100;
        static bool warned;

        public override bool IsHazard => true;

        /// <summary>How long the player has been in the sand (seconds; 0 when out of it).</summary>
        public static float SunkFor(World w) => w != null && Mathf.Abs(w.Frame - lastFrame) <= 3 ? sunk : 0f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            anim?.Play(animFlow);
        }

        /// <summary>A bomb lying on the sand sinks into it and is gone (no blast).</summary>
        void SwallowBombs()
        {
            Bomb sunkBomb = null;
            foreach (var e in World.Entities)
                if (e is Bomb b && !b.Dead && b.grounded && Overlaps(b)) { sunkBomb = b; break; }
            if (sunkBomb == null) return;
            Fx.Dust(sunkBomb.pos, 8);
            Fx.Text(sunkBomb.Center + Vector2.up * 0.6f, "SWALLOWED", new Color(1f, 0.85f, 0.5f), 0.8f);
            Sfx.Play("land", pos, 0.8f, 0.6f);
            sunkBomb.Despawn();
        }

        public override void Tick(float dt)
        {
            SwallowBombs();
            var p = World.Player;
            if (p == null || p.IsDead || !p.grounded || !Overlaps(p)) return;
            var run = RunSession.Current;
            if (run != null && run.stats.sandGrip > 0f) return;
            p.Slow(slow, 0.25f);
            int frame = World.Frame;
            if (frame == lastFrame) return;                     // another cell of the pool already counted this step
            if (Mathf.Abs(frame - lastFrame) > 3) { sunk = 0f; warned = false; }
            lastFrame = frame;
            sunk += dt;
            if (frame % 9 == 0) Fx.Dust(new Vector2(p.pos.x, pos.y + 0.1f), 1);
            if (!warned && sunk > sinkTime * 0.45f)
            {
                warned = true;
                Fx.Text(p.Center + Vector2.up * 0.9f, "SINKING", new Color(1f, 0.85f, 0.5f), 0.9f);
            }
            if (sunk < sinkTime) return;
            sunk = 0f; warned = false;
            // pulled under: it costs health, and the sand spits the player back out
            p.TakeDamage(new DamageInfo(damage, DamageType.Generic, this, new Vector2(0f, 11f), "quicksand"));
            Fx.Dust(p.pos, 10);
            Sfx.Play("thud", pos, 0.8f, 0.7f);
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
