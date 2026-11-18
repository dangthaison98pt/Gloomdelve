using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom boss: the Rot Heart. A beating fungus heart hanging from the ceiling on a stalk - it never moves
    /// and cannot be stomped. It hangs too high for a jump, and so do the two platforms beside it: a bounce
    /// mushroom under each platform carries the player up onto it; jump from there and slash it in the air, or throw things at it.
    /// Its moves come in random order (never the same twice running): a fan of spore darts aimed at the player (so bouncing on the mushrooms and
    /// slashing is never safe for long: the darts knock a bouncing player out of the air), a tendril that lashes
    /// across the whole room low along the floor (jump it), a rain of spore clouds on the floor (dizzy, no
    /// damage), darts again, a tendril at head height (stay on the ground: it passes over a standing player and
    /// catches a jumping one), and a lurecap woken among the glowing plants. The tendrils cannot be cut. The lower its health, the faster it beats - and at two thirds and one third of its
    /// health half of the glowing plants in the room go dark.
    /// </summary>
    public class SporeHeart : Boss
    {
        [Tooltip("Length of the stalk: how far below the ceiling the top of the heart hangs.")] public float hangDepth = 2.6f;
        [Tooltip("Seconds between moves at full health ...")] public float actionGap = 1.8f;
        [Tooltip("... and when it is nearly dead.")] public float actionGapLow = 1.15f;
        public float tellTime = 0.65f;
        [Header("Spore darts")]
        public int darts = 3;
        public float dartSpread = 20f;
        public float dartSpeed = 6.5f;
        public string dartId = "venomshot";
        [Header("Tendril lash")]
        public float lashSpeed = 8.5f;
        public string tendrilId = "tendril";
        [Tooltip("Height of the low lash above the floor (jump over it) ...")] public float lashLowY = 0.15f;
        [Tooltip("... and of the high one (it passes over a standing player).")] public float lashHighY = 1.1f;
        [Header("Spore rain")]
        public int clouds = 3;
        public float cloudSpacing = 3.5f;
        public string cloudId = "sporecloud";
        [Header("Lurecaps")]
        public int maxLurecaps = 2;
        public float summonCooldown = 11f;
        public string minionId = "lurecap";
        public SpriteAnim animBeat, animSwell;

        static readonly Color Spore = new Color(0.62f, 1f, 0.5f);
        enum Move { None, Darts, LashLow, LashHigh, Rain }
        Move pending;
        float timer, tell, summonTimer, floorY;
        int step, lightsOut, lashSide = 1;
        float[] rainX;

        /// <summary>Touching it never hurts: its moves do.</summary>
        public override bool Attacking => false;
        /// <summary>How many times it has put lights out (0..2).</summary>
        public int LightsOut => lightsOut;

        public override void Configure(SpawnPoint sp)
        {
            // hangs from the first ceiling above its cell
            var g = World.Grid;
            int cy = sp.cell.y;
            while (cy < g.Height - 1 && !g.IsSolid(sp.cell.x, cy)) cy++;
            pos = new Vector2(sp.cell.x + 0.5f, cy - hangDepth - size.y);
            facing = 1;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = 1.8f;
            summonTimer = summonCooldown * 0.5f;
            var g = World.Grid;
            int fy = Mathf.FloorToInt(pos.y);
            while (fy > 0 && !g.IsSolid(Mathf.FloorToInt(pos.x), fy)) fy--;
            floorY = fy + 1;
            anim?.Play(animBeat);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            vel = Vector2.zero;                 // it hangs: no gravity, no knockback
            summonTimer -= dt;
            var p = Target;
            if (anim != null) anim.speed = Mathf.Lerp(2.2f, 1f, HealthFraction);     // beats faster as it weakens
            if (tell > 0f)
            {
                tell -= dt;
                anim?.Play(animSwell);
                if (tell <= 0f) { Unleash(p); anim?.Play(animBeat); }
                return;
            }
            timer -= dt;
            if (timer > 0f || p == null) return;
            timer = RestFlat(Mathf.Lerp(actionGapLow, actionGap, HealthFraction));
            // its moves, in random order: darts (twice as often), low lash, rain, high lash, a lurecap (or rain when the call is not ready)
            switch (NextMove(0, 1, 2, 0, 3, 4))
            {
                case 0: case 3: Begin(Move.Darts); break;
                case 1: Begin(Move.LashLow); break;
                case 2: Begin(Move.Rain, p); break;
                case 4: Begin(Move.LashHigh); break;
                default:
                    if (summonTimer <= 0f && CountAlive<Lurecap>() < maxLurecaps) { Summon(p); summonTimer = summonCooldown; }
                    else Begin(Move.Rain, p);
                    break;
            }
        }

        void Begin(Move m, PlayerController p = null)
        {
            pending = m;
            tell = tellTime;
            Tell();
            Sfx.Play("toad", pos, 0.9f, 0.5f);
            if (m == Move.LashLow || m == Move.LashHigh)
            {
                // the tendril comes out of a wall, the other one each time: a mark shows where and how high
                lashSide = -lashSide;
                float wx = lashSide < 0 ? 1.8f : World.Grid.Width - 1.8f;
                float wy = floorY + (m == Move.LashLow ? lashLowY : lashHighY);
                Fx.Text(new Vector2(wx, wy + 0.5f), m == Move.LashLow ? "! JUMP" : "! STAY LOW", Spore, tellTime + 0.4f);
                Fx.Debris(new Vector2(wx, wy + 0.2f), Spore, 8);
            }
            if (m == Move.Rain && p != null)
            {
                // where the clouds will come down: under the player and to both sides; spores trickle down there first
                var g = World.Grid;
                rainX = new float[clouds];
                for (int i = 0; i < clouds; i++)
                {
                    rainX[i] = Mathf.Clamp(p.pos.x + (i - (clouds - 1) * 0.5f) * cloudSpacing, 2.5f, g.Width - 2.5f);
                    Fx.Debris(new Vector2(rainX[i], pos.y), Spore, 6);
                    Fx.Debris(new Vector2(rainX[i], floorY + 0.3f), Spore, 4);
                }
            }
        }

        void Unleash(PlayerController p)
        {
            switch (pending)
            {
                case Move.Darts:
                    if (p != null)
                    {
                        Vector2 from = new Vector2(pos.x, pos.y - 0.15f);
                        FanAt(from, p.Center, darts, dartSpread, dartSpeed, dartId);
                        Sfx.Play("throw", pos);
                    }
                    break;
                case Move.LashLow:
                case Move.LashHigh:
                {
                    float wx = lashSide < 0 ? 1.8f : World.Grid.Width - 1.8f;
                    float wy = floorY + (pending == Move.LashLow ? lashLowY : lashHighY);
                    FireShot(new Vector2(wx, wy), new Vector2(-lashSide * lashSpeed, 0f), tendrilId);
                    Sfx.Play("throw", pos, 1f, 0.5f);
                    break;
                }
                case Move.Rain:
                    if (rainX != null)
                        foreach (float x in rainX) World.SpawnById(cloudId, new Vector2(x, floorY + 1.3f));
                    Sfx.Play("thud", pos, 0.7f, 1.6f);
                    break;
            }
            pending = Move.None;
        }

        /// <summary>One of the lights in the room turns out to be alive: a lurecap appears among the plants, not far from the player.</summary>
        void Summon(PlayerController p)
        {
            // it comes up among the plants a few steps from wherever the player is fighting, on either side
            if (!MinionSpot(p, 4, 8, out var spot)) return;
            int x = spot.x, y = spot.y;
            var e = World.SpawnById(minionId, new Vector2(x + 0.5f, y));
            if (e is Enemy en) en.summoned = true;
            Tell();
            Sfx.Play("toad", pos, 1f, 1.5f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            bool hit = base.TakeDamage(d);
            if (hit && !Dead)
            {
                int want = HealthFraction <= 1f / 3f ? 2 : HealthFraction <= 2f / 3f ? 1 : 0;
                while (lightsOut < want) { lightsOut++; DimRoom(); }
            }
            return hit;
        }

        static List<GlowLight> RoomLights(World w)
        {
            var l = new List<GlowLight>();
            foreach (var e in w.Entities)
                if (!e.Dead && e is Scenery) { var gl = e.GetComponent<GlowLight>(); if (gl != null) l.Add(gl); }
            return l;
        }

        /// <summary>Half of the glowing plants still lit go dark (visual only; the bounce mushrooms stay lit).</summary>
        void DimRoom()
        {
            int k = 0;
            foreach (var gl in RoomLights(World))
                if (gl.IsOn && (k++ % 2 == 0)) gl.SetOn(false);
            Fx.Shake(0.3f);
            Sfx.Play("shatter", pos, 0.7f, 0.5f);
        }

        protected override void Die(in DamageInfo d)
        {
            var w = World;
            if (w != null) foreach (var gl in RoomLights(w)) gl.SetOn(true);       // the room lights up again
            base.Die(d);
        }
    }
}
