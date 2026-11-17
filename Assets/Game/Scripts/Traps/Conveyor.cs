using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks: one cell of conveyor belt lying on the floor. Whatever stands on it - the player, creatures,
    /// loose items - is carried along at 'speed'. It does no harm by itself: it changes where a jump starts and
    /// where a fight drifts. Heavy boots (StatModifiers.beltGrip) make the player ignore it.
    /// Placed by the level generator (AreaConfig.conveyorChance) only on long plain floors, well away from pits and
    /// ledges, and the route check does not count on it (the test bot, in god mode, is not carried), so a level
    /// never needs a belt and a belt never makes a planned jump impossible.
    /// In hand-made levels: '&lt;' runs left, '&gt;' runs right (GameConfig.customMarkers).
    /// </summary>
    public class Conveyor : Prop
    {
        [Tooltip("Tiles per second. The player runs at about 6.4.")] public float speed = 3f;
        [Tooltip("0 = take the direction from the spawn point; -1 / 1 = always run that way (the '>' marker).")] public int forceDir = 0;
        public Sprite[] frames;

        [System.NonSerialized] public int dir = 1;

        public override bool IsHazard => false;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            dir = forceDir != 0 ? forceDir : (sp.dir != 0 ? sp.dir : 1);
        }

        /// <summary>Runs the other way from now on (the Foreman's lever).</summary>
        public void Reverse() { dir = -dir; }

        static bool OnBelt(Entity e, Vector2 belt) => e.grounded && Mathf.Abs(e.pos.y - belt.y) < 0.08f && e.pos.x >= belt.x - 0.5f && e.pos.x < belt.x + 0.5f;

        public override void Tick(float dt)
        {
            float dx = dir * speed * dt;
            var w = World;
            var p = w.Player;
            if (p != null && !p.IsDead && OnBelt(p, pos)) p.BeltCarry(dx);
            var g = w.Grid;
            foreach (var e in w.Entities)
            {
                if (e == null || e.Dead || e is Prop || e is PlayerController || !OnBelt(e, pos)) continue;
                // not into a wall
                float edge = e.pos.x + dir * (e.size.x * 0.5f) + dx;
                if (g.IsSolid(Mathf.FloorToInt(edge), Mathf.FloorToInt(e.pos.y + 0.2f))) continue;
                e.pos.x += dx;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null || frames == null || frames.Length == 0) return;
            int f = (int)(Time.time * speed * 4f) % frames.Length;
            sr.sprite = frames[dir > 0 ? f : frames.Length - 1 - f];      // the ridges walk the way the belt runs
        }
    }
}
