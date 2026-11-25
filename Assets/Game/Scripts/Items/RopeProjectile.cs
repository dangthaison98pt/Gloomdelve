using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Thrown rope. It flies up to maxRise tiles. It only hooks on when it has something to hold:
    /// a ceiling it bumps into, or a wall / ledge right beside its top when it stops rising.
    /// With nothing to hold it falls back down and can be picked up again (not wasted).
    /// A hooked rope lasts `lifetime` seconds, blinks during the last `blinkTime` seconds, then vanishes
    /// (never while the player is still climbing it).
    /// </summary>
    public class RopeProjectile : Entity
    {
        [Header("Rope")]
        public int maxRise = 7;
        public int length = 8;
        public float riseSpeed = 16f;
        [Tooltip("Seconds a hooked rope stays before vanishing.")] public float lifetime = 20f;
        [Tooltip("The rope blinks during these last seconds as a warning.")] public float blinkTime = 3f;
        public Sprite segmentSprite;
        public Sprite topSprite;
        public Sprite bottomSprite;
        [Tooltip("Top of a rope hooked over a ledge lip beside it (drawn for a wall on the right, flipped for the left).")]
        public Sprite lipSprite;
        [Tooltip("Piton driven into a wall face beside the rope top.")]
        public Sprite pitonSprite;
        public Material material;

        /// <summary>What the hooked rope is holding on to (decides how the hook is drawn).</summary>
        public enum HoldKind { Ceiling, Lip, LipBelow, Face, Corner, None }
        public HoldKind Hold { get; private set; } = HoldKind.None;
        public int HoldSide { get; private set; } = 1;
        public int Column => column;

        enum State { Rising, Falling, Hooked }
        State state = State.Rising;
        float startY;
        int column;
        float life;
        readonly List<Vector2Int> cells = new List<Vector2Int>();
        readonly List<SpriteRenderer> segs = new List<SpriteRenderer>();

        public const float DefaultRiseSpeed = 16f;
        public enum Rise { Rising, Hooked, NoHold }

        /// <summary>
        /// One frame of a rising rope. Shared by the rope itself and by LevelValidator, so the validator
        /// predicts exactly where a thrown rope hooks.
        /// </summary>
        public static Rise RiseStep(TileGrid g, int column, ref float y, float startY, int maxRise, float speed, float dt, out int anchor)
        {
            anchor = -1;
            float ny = y + speed * dt;
            int top = Mathf.FloorToInt(ny + 0.35f);
            if (g.IsSolid(column, top))
            {
                anchor = Mathf.FloorToInt(y + 0.35f);
                while (anchor > 0 && g.IsSolid(column, anchor)) anchor--;
                return Rise.Hooked;
            }
            y = ny;
            if (ny - startY >= maxRise)
            {
                int a = Mathf.FloorToInt(y + 0.35f);
                if (HasHold(g, column, a)) { anchor = a; return Rise.Hooked; }
                return Rise.NoHold;
            }
            return Rise.Rising;
        }

        /// <summary>Classifies the hold of a rope whose top cell is (c, a); side = direction of the wall.</summary>
        public static HoldKind ClassifyHold(TileGrid g, int c, int a, out int side)
        {
            side = 1;
            if (g.IsSolid(c, a + 1)) return HoldKind.Ceiling;
            for (int s = 1; s >= -1; s -= 2) if (g.IsSolid(c + s, a) && !g.IsSolid(c + s, a + 1)) { side = s; return HoldKind.Lip; }
            for (int s = 1; s >= -1; s -= 2) if (g.IsSolid(c + s, a)) { side = s; return HoldKind.Face; }
            for (int s = 1; s >= -1; s -= 2) if (g.IsSolid(c + s, a + 1)) { side = s; return HoldKind.Corner; }
            for (int s = 1; s >= -1; s -= 2) if (g.IsSolid(c + s, a - 1) && !g.IsSolid(c + s, a)) { side = s; return HoldKind.LipBelow; }
            return HoldKind.None;
        }

        public static bool HasHold(TileGrid g, int column, int y) =>
            g.IsSolid(column, y + 1) || g.IsSolid(column - 1, y) || g.IsSolid(column + 1, y) ||
            g.IsSolid(column - 1, y + 1) || g.IsSolid(column + 1, y + 1);

        /// <summary>Anchor row of a rope thrown up from a player standing with feet at feetY in this column, or -1.</summary>
        public static int SimulateThrow(TileGrid g, int column, float feetY, int maxRise, float speed = DefaultRiseSpeed)
        {
            float start = feetY + 0.4f, y = start;
            if (g.IsSolid(column, Mathf.FloorToInt(feetY + 0.5f))) return -1;
            for (int f = 0; f < 600; f++)
            {
                var r = RiseStep(g, column, ref y, start, maxRise, speed, GameConst.Dt, out int anchor);
                if (r == Rise.Hooked) return anchor;
                if (r == Rise.NoHold) return -1;
            }
            return -1;
        }

        /// <summary>Climbable cells of a rope hooked at (column, anchor), unrolling down to the floor or its length.</summary>
        public static List<Vector2Int> ComputeCells(TileGrid g, int column, int anchorY, int length)
        {
            var list = new List<Vector2Int>();
            for (int i = 0; i < length; i++)
            {
                int y = anchorY - i;
                if (g.IsSolid(column, y)) break;
                list.Add(new Vector2Int(column, y));
                if (i > 0 && g.IsSolid(column, y - 1)) break;
            }
            return list;
        }

        public bool Hooked => state == State.Hooked;
        public float LifeLeft => life;

        public override void OnSpawn()
        {
            base.OnSpawn();
            var cfg = World.config.player;
            if (cfg != null) { maxRise = cfg.ropeThrowHeight; length = cfg.ropeLength; lifetime = cfg.ropeLifetime; }
            var run = RunSession.Current;
            if (run != null && run.stats.ropeBonus > 0) { maxRise += run.stats.ropeBonus; length += run.stats.ropeBonus; }      // (the Grappling Hook)
        }

        public void LaunchUp()
        {
            startY = pos.y;
            column = Mathf.FloorToInt(pos.x);
            pos.x = column + 0.5f;
            state = State.Rising;
        }

        /// <summary>DOWN + rope at a ledge: hooks onto the ledge edge and unrolls down the drop.</summary>
        public void DeployDown(int x, int y)
        {
            column = x;
            Deploy(y);
            Sfx.Play("rope_deploy", pos);
        }

        public void SetLife(float seconds) => life = seconds;

        public override void Tick(float dt)
        {
            switch (state)
            {
                case State.Rising: TickRising(dt); break;
                case State.Falling: TickFalling(dt); break;
                case State.Hooked: TickHooked(dt); break;
            }
        }

        void TickRising(float dt)
        {
            float y = pos.y;
            var r = RiseStep(World.Grid, column, ref y, startY, maxRise, riseSpeed, dt, out int anchor);
            pos.y = y;
            if (r == Rise.Hooked)
            {
                Deploy(anchor);
                Sfx.Play("rope_deploy", pos);
            }
            else if (r == Rise.NoHold)
            {
                // Nothing to hook onto: the rope drops back down.
                state = State.Falling;
                vel = Vector2.zero;
                Sfx.Play("error", pos, 0.5f);
            }
        }

        void TickFalling(float dt)
        {
            StepPhysics(dt, 40f);
            if (grounded || pos.y < 0.5f)
            {
                var p = World.SpawnById("ropepile", new Vector2(pos.x, pos.y)) as Pickup;
                if (p != null) { p.value = 1; p.vel = new Vector2(0f, 2f); }
                Despawn();
            }
        }

        void TickHooked(float dt)
        {
            bool climbing = false;
            var pl = World.Player;
            if (pl != null && !pl.IsDead && pl.motor.mode == MotorMode.Climb && pl.motor.climbX == column)
            {
                int py = Mathf.FloorToInt(pl.motor.pos.y + 0.3f);
                foreach (var c in cells) if (c.y == py) { climbing = true; break; }
            }
            life -= dt;
            if (climbing && life < blinkTime) life = blinkTime;   // never vanish under the player's hands
            if (life <= 0f)
            {
                Fx.Debris(Center, new Color(0.7f, 0.55f, 0.3f), 6);
                Despawn();
            }
        }

        void Deploy(int anchorY)
        {
            state = State.Hooked;
            life = lifetime;
            var g = World.Grid;
            foreach (var c in ComputeCells(g, column, anchorY, length))
            {
                cells.Add(c);
                g.AddRope(c.x, c.y, 1);
            }
            if (cells.Count == 0) { Despawn(); return; }
            int bottom = cells[cells.Count - 1].y;
            pos = new Vector2(column + 0.5f, bottom);
            prevPos = pos;
            size = new Vector2(0.4f, cells.Count);
            if (sr != null) sr.enabled = false;
            Hold = ClassifyHold(g, column, anchorY, out int side);
            HoldSide = side;
            for (int i = 0; i < cells.Count; i++)
            {
                Sprite sp = i == cells.Count - 1 && i > 0 ? bottomSprite : segmentSprite;
                float dy = 0f;
                bool flip = false;
                if (i == 0)
                {
                    switch (Hold)
                    {
                        case HoldKind.Lip when lipSprite != null: sp = lipSprite; flip = side < 0; break;
                        case HoldKind.LipBelow when lipSprite != null: sp = lipSprite; flip = side < 0; dy = -1f; break;
                        case HoldKind.Face when pitonSprite != null:
                        case HoldKind.Corner when pitonSprite != null: sp = segmentSprite; break;
                        default: sp = topSprite; break;
                    }
                }
                AddPiece(sp, cells.Count - 1 - i + 0.5f + dy, flip);
            }
            // the hook visibly holds on to the wall beside the rope top
            if ((Hold == HoldKind.Face || Hold == HoldKind.Corner) && pitonSprite != null)
                AddPiece(pitonSprite, cells.Count - 0.5f + (Hold == HoldKind.Corner ? 0.35f : 0f), side < 0);
            transform.position = pos;
        }

        void AddPiece(Sprite sp, float y, bool flip)
        {
            var go = new GameObject("seg");
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<SpriteRenderer>();
            if (material != null) s.sharedMaterial = material;
            s.sprite = sp;
            s.flipX = flip;
            s.sortingOrder = 5;
            go.transform.localPosition = new Vector3(0f, y, 0f);
            segs.Add(s);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (state != State.Hooked) return;
            bool visible = life > blinkTime || ((int)(Time.time * (life < 1f ? 16f : 8f)) % 2 == 0);
            foreach (var s in segs) if (s != null) s.enabled = visible;
        }

        void Remove()
        {
            if (state != State.Hooked) return;
            var g = World.Grid;
            foreach (var c in cells) g.AddRope(c.x, c.y, -1);
            cells.Clear();
            state = State.Falling;
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Fx.Debris(Center, new Color(0.7f, 0.55f, 0.3f), 6);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }

        protected override void OnDespawn() => Remove();

        void OnDestroy()
        {
            if (state == State.Hooked && World.Instance != null && World.Instance.Grid != null) Remove();
        }
    }
}
