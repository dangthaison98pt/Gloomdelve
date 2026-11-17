using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom boss: the Rot Heart. A beating fungus heart hanging from the ceiling on a stalk - it never moves
    /// and cannot be stomped. It hangs too high for a jump: bounce on the bounce mushrooms under it (or jump off
    /// the side platforms) and slash it in the air, or throw things at it.
    /// A fixed round of moves: a fan of spore darts at the player, a rain of spore clouds on the floor (dizzy, no
    /// damage), darts again, a pulse that sends a shockwave along the floor both ways, and a lurecap woken among
    /// the glowing plants. The lower its health, the faster it beats - and at two thirds and one third of its
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
        [Header("Spore rain")]
        public int clouds = 3;
        public float cloudSpacing = 3.5f;
        public string cloudId = "sporecloud";
        [Header("Pulse")]
        public float shockSpeed = 6.5f;
        [Header("Lurecaps")]
        public int maxLurecaps = 2;
        public float summonCooldown = 11f;
        public string minionId = "lurecap";
        public SpriteAnim animBeat, animSwell;

        static readonly Color Spore = new Color(0.62f, 1f, 0.5f);
        enum Move { None, Darts, Rain, Pulse }
        Move pending;
        float timer, tell, summonTimer, floorY;
        int step, lightsOut;
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
            timer = Mathf.Lerp(actionGapLow, actionGap, HealthFraction);
            // the round: darts, rain, darts, pulse, a lurecap (or rain when the call is not ready)
            switch (step++ % 5)
            {
                case 0: case 2: Begin(Move.Darts); break;
                case 1: Begin(Move.Rain, p); break;
                case 3: Begin(Move.Pulse); break;
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
                case Move.Rain:
                    if (rainX != null)
                        foreach (float x in rainX) World.SpawnById(cloudId, new Vector2(x, floorY + 1.3f));
                    Sfx.Play("thud", pos, 0.7f, 1.6f);
                    break;
                case Move.Pulse:
                    FireShot(new Vector2(pos.x - 0.7f, floorY + 0.2f), new Vector2(-shockSpeed, 0f), "shockwave", Spore);
                    FireShot(new Vector2(pos.x + 0.7f, floorY + 0.2f), new Vector2(shockSpeed, 0f), "shockwave", Spore);
                    Fx.Dust(new Vector2(pos.x, floorY), 10);
                    Fx.Shake(0.4f);
                    LevelLighting.Flash(Center, Spore, 7f, 1.6f, 0.35f);
                    Sfx.Play("thud", pos, 1f, 0.6f);
                    break;
            }
            pending = Move.None;
        }

        /// <summary>One of the lights in the room turns out to be alive: a lurecap appears among the plants, away from the player.</summary>
        void Summon(PlayerController p)
        {
            var g = World.Grid;
            int y = Mathf.FloorToInt(floorY);
            int x = p.pos.x > g.Width * 0.5f ? Random.Range(3, 8) : g.Width - 1 - Random.Range(3, 8);
            if (!g.IsEmpty(x, y) || !g.IsSolid(x, y - 1)) return;
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
