using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The Dune Queen's chamber: a block of stone, two cells wide and one high, that comes up out of the quicksand
    /// and stays - the only firm ground there is. It is solid: the player climbs onto it, and scarabs cannot walk
    /// through it. Anything the Queen does breaks it (Shatter): her breach from below, a clod of her spit, the
    /// wave of her landing. A broken block comes up again a few seconds later, so the firm ground is lost again
    /// and again unless her attacks are drawn away from it.
    /// It rises bodily: a row of pictures, each showing a little more of the block from its top down ('riseFrames',
    /// the last one the whole block). (Not a sprite mask: two masked sprites at once - this and the Queen going
    /// under - made the renderer draw the one with the picture of the other.)
    /// While up it owns two Bedrock tiles of the grid. (Arena files: marker 'i' in the left one of its two cells.)
    /// </summary>
    public class SandPillar : Entity
    {
        [Tooltip("Seconds it takes to come up: sand boils there first.")] public float riseTime = 0.8f;
        [Tooltip("Seconds until a broken block comes up again.")] public float brokenTime = 5f;
        public Color debrisColor = new Color(0.75f, 0.62f, 0.42f);

        enum State { Down, Rising, Up }
        State state;
        float timer;
        Vector2Int cell;
        static int spawned;
        [Tooltip("The block coming up: each picture shows more of it, the last one all of it.")] public Sprite[] riseFrames;

        /// <summary>Standing: its two tiles are in the grid.</summary>
        public bool IsUp => state == State.Up;
        public bool Rising => state == State.Rising;
        public float Left => cell.x;
        public float Right => cell.x + 2f;

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }

        public override void Configure(SpawnPoint sp)
        {
            cell = sp.cell;
            pos = new Vector2(cell.x + 1f, cell.y);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            cell = new Vector2Int(Mathf.RoundToInt(pos.x - 1f), Mathf.RoundToInt(pos.y));
            // one after another, not all at once
            state = State.Down;
            timer = 0.5f + (spawned++ % 3) * 0.6f;
            Show(0f);
        }

        /// <summary>k = how much of the block is above the floor (0 none, 1 all of it).</summary>
        void Show(float k)
        {
            if (sr == null) return;
            k = Mathf.Clamp01(k);
            sr.enabled = k > 0.001f;
            if (riseFrames == null || riseFrames.Length == 0 || k <= 0.001f) return;
            int i = Mathf.Clamp(Mathf.CeilToInt(k * riseFrames.Length) - 1, 0, riseFrames.Length - 1);
            if (riseFrames[i] != null) sr.sprite = riseFrames[i];
        }

        void SetTiles(bool on)
        {
            var g = World.Grid;
            for (int i = 0; i < 2; i++)
            {
                var t = g.Get(cell.x + i, cell.y);
                if (on && t == TileType.Empty) g.Set(cell.x + i, cell.y, TileType.Bedrock);
                else if (!on && t == TileType.Bedrock) g.Set(cell.x + i, cell.y, TileType.Empty);
            }
        }

        /// <summary>A boss is standing where the block would come up: it waits.</summary>
        bool Blocked()
        {
            foreach (var e in World.Entities)
                if (e is Boss b && !b.Dead && Mathf.Abs(b.pos.x - pos.x) < b.size.x * 0.5f + 1.2f) return true;
            return false;
        }

        /// <summary>Whoever stands where the block has come up is carried to its top (nobody ends up inside the stone).</summary>
        void LiftWhoeverIsHere()
        {
            float top = cell.y + 1f;
            var p = World.Player;
            if (p != null && !p.IsDead && p.pos.y < top - 0.02f && p.pos.y > cell.y - 0.5f &&
                p.pos.x + p.size.x * 0.5f > cell.x && p.pos.x - p.size.x * 0.5f < cell.x + 2f)
            {
                p.motor = MotorState.At(new Vector2(p.pos.x, top));
                p.motor.grounded = false;
                p.motor.apexY = top;
                p.pos = p.prevPos = p.motor.pos;
            }
            foreach (var e in World.Entities)
                if (e is Enemy en && !(en is Boss) && !en.Dead && en.pos.y < top - 0.02f && en.pos.y > cell.y - 0.5f &&
                    en.pos.x + en.size.x * 0.5f > cell.x && en.pos.x - en.size.x * 0.5f < cell.x + 2f)
                    en.pos = new Vector2(en.pos.x, top);
        }

        public override void Tick(float dt)
        {
            if (AutoTest.Active) return;            // the test bot walks a chamber of plain floor
            timer -= dt;
            switch (state)
            {
                case State.Down:
                    if (timer <= 0f && !Blocked()) { state = State.Rising; timer = riseTime; Sfx.Play("rumble", pos, 0.4f, 1.5f); }
                    break;
                case State.Rising:
                    Show(1f - Mathf.Clamp01(timer / riseTime));
                    if (World.Frame % 3 == 0) Fx.Dust(new Vector2(pos.x + Random.Range(-1f, 1f), pos.y + 0.1f), 1);
                    if (timer <= 0f)
                    {
                        Show(1f);
                        SetTiles(true);
                        LiftWhoeverIsHere();
                        state = State.Up;
                    }
                    break;
                case State.Up:
                {
                    // anything the Queen sends at it breaks it: a clod of spit, the wave along the sand
                    if (World.Grid.Get(cell.x, cell.y) != TileType.Bedrock && World.Grid.Get(cell.x + 1, cell.y) != TileType.Bedrock) { Shatter(); break; }
                    var area = new Rect(cell.x - 0.4f, cell.y - 0.1f, 2.8f, 1.5f);
                    // (the shot ends here: a wave along the sand breaks the first block it meets and goes no further)
                    BossShot struck = null;
                    foreach (var e in World.Entities)
                        if (e is BossShot s && !s.Dead && area.Overlaps(s.Bounds)) { struck = s; break; }
                    if (struck != null)
                    {
                        Fx.Debris(struck.Center, struck.burstColor, 6);
                        struck.Despawn();
                        Shatter();
                    }
                    break;
                }
            }
        }

        /// <summary>Broken by the Queen: gone, to come up again after 'brokenTime'. True if there was stone there to break.</summary>
        public bool Shatter()
        {
            if (state == State.Down) { timer = Mathf.Max(timer, brokenTime * 0.5f); return false; }
            bool stood = state == State.Up;
            SetTiles(false);
            Show(0f);
            Fx.Debris(new Vector2(pos.x, pos.y + 0.6f), debrisColor, 16);
            Fx.Shake(0.25f);
            Sfx.Play("shatter", pos, 1f, 0.7f);
            state = State.Down; timer = brokenTime;
            return stood;
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (World != null && World.Grid != null) SetTiles(false);
        }
    }
}
