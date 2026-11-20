using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The people of Brindlemoor out in the street: grown-ups who walk from one end of it to the other, stop, look
    /// about, stand a while with whoever is near; and children who play tag - one runs, the other chases, a touch
    /// swaps them over. They are scenery: nothing can be done to them or with them, and they never stand in the
    /// player's way. Populate puts them out when the village is built (GameDirector); nothing in the level file.
    /// </summary>
    public class Townsfolk : Prop
    {
        [Tooltip("Grown-ups: one standing picture per look ...")] public Sprite[] idle;
        [Tooltip("... and four walking pictures per look, one look after the other.")] public Sprite[] walk;
        [Tooltip("Children: one standing picture per look, and two running pictures per look.")] public Sprite[] kidIdle;
        public Sprite[] kidRun;
        public float walkSpeed = 1.5f;
        public float kidSpeed = 4.2f;

        const int KidBase = 100;
        int look;
        bool kid, chaser;
        float minX, maxX, targetX, wait, speed, animT, hopT, tagCool;
        Townsfolk friend;

        public bool IsKid => kid;
        public bool Chasing => kid && chaser;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            kid = sp.param >= KidBase;
            look = kid ? sp.param - KidBase : sp.param;
            flipWithFacing = true;
        }

        /// <summary>
        /// Puts the street's people out: along the open ground under the sky, from wall to wall. Returns how many.
        /// </summary>
        public static int Populate(World w, int grownUps = 6, int children = 4)
        {
            var prefab = w.config != null && w.config.prefabs != null ? w.config.prefabs.Get("townsfolk") : null;
            var g = w.Grid;
            if (prefab == null || g == null) return 0;
            // the street: the highest floor of the level (the houses' rooms lie in the rock under it)
            int col = Mathf.Min(g.Width - 2, 5), y = g.Height - 1;
            while (y > 0 && g.IsSolid(col, y)) y--;       // (a roof over it, where there is one)
            while (y > 0 && !g.IsSolid(col, y)) y--;
            int floor = y + 1;
            if (floor >= g.Height - 1) return 0;
            int x0 = 1, x1 = g.Width - 2;
            while (x0 < x1 && g.IsSolid(x0, floor)) x0++;
            while (x1 > x0 && g.IsSolid(x1, floor)) x1--;
            if (x1 - x0 < 12) return 0;
            var rng = new System.Random(RunSession.Current != null ? RunSession.Current.seed * 31 + 7 : 7);
            var made = new List<Townsfolk>();
            int n = 0;
            for (int i = 0; i < grownUps; i++)
            {
                int x = x0 + 2 + (x1 - x0 - 4) * (i * 2 + 1) / (grownUps * 2) + rng.Next(-2, 3);
                var t = w.Spawn(prefab, new Vector2(x + 0.5f, floor), new SpawnPoint("townsfolk", new Vector2Int(x, floor), rng.Next(2) == 0 ? -1 : 1, i)) as Townsfolk;
                if (t != null) { t.SetRange(x0 + 1.5f, x1 - 0.5f, rng); n++; }
            }
            for (int i = 0; i < children; i++)
            {
                // in pairs, each pair with a stretch of street to itself
                int pair = i / 2, pairs = Mathf.Max(1, (children + 1) / 2);
                float a = x0 + 2 + (x1 - x0 - 4) * (pair + 0.15f) / pairs, b = x0 + 2 + (x1 - x0 - 4) * (pair + 0.85f) / pairs;
                float x = Mathf.Lerp(a, b, i % 2 == 0 ? 0.3f : 0.7f);
                var t = w.Spawn(prefab, new Vector2(x, floor), new SpawnPoint("townsfolk", new Vector2Int(Mathf.FloorToInt(x), floor), 1, KidBase + i)) as Townsfolk;
                if (t == null) continue;
                t.SetRange(a, b, rng);
                made.Add(t); n++;
                if (i % 2 == 1) { var other = made[made.Count - 2]; t.friend = other; other.friend = t; other.chaser = true; }
            }
            return n;
        }

        void SetRange(float a, float b, System.Random rng)
        {
            minX = a; maxX = b;
            speed = (kid ? kidSpeed : walkSpeed) * (0.85f + (float)rng.NextDouble() * 0.3f);
            targetX = Mathf.Lerp(a, b, (float)rng.NextDouble());
            wait = (float)rng.NextDouble() * 2f;
            animT = (float)rng.NextDouble();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (maxX <= minX) { minX = pos.x - 4f; maxX = pos.x + 4f; targetX = pos.x; speed = kid ? kidSpeed : walkSpeed; }
            Show(false);
        }

        public override void Tick(float dt)
        {
            animT += dt;
            tagCool -= dt;
            bool moving = false;
            if (kid && friend != null && !friend.Dead)
            {
                // tag: the chaser runs at the other, who runs from end to end of their stretch; a touch swaps them
                if (chaser)
                {
                    if (wait > 0f) wait -= dt;      // (just tagged: counts a moment before giving chase)
                    else
                    {
                        targetX = friend.pos.x;
                        if (Mathf.Abs(friend.pos.x - pos.x) < 0.55f && tagCool <= 0f)
                        {
                            chaser = false; friend.chaser = true; friend.wait = 0.7f; friend.tagCool = tagCool = 1f;
                            friend.hopT = 0.35f;
                            targetX = pos.x < (minX + maxX) * 0.5f ? maxX : minX;
                        }
                    }
                }
                else
                {
                    bool reached = Mathf.Abs(targetX - pos.x) < 0.2f;
                    // the chaser stands between this one and where it was going: turn and run the other way
                    bool cutOff = friend.wait <= 0f && Mathf.Abs(friend.pos.x - pos.x) < 2f && (friend.pos.x - pos.x) * (targetX - pos.x) > 0f;
                    if (cutOff) targetX = friend.pos.x > pos.x ? minX : maxX;
                    else if (reached) targetX = Mathf.Abs(pos.x - minX) < Mathf.Abs(pos.x - maxX) ? maxX : minX;
                }
                moving = wait <= 0f || !chaser;
            }
            else if (wait > 0f)
            {
                wait -= dt;
                if (wait <= 0f) targetX = Mathf.Lerp(minX, maxX, Hash(animT));
            }
            else moving = true;

            if (moving)
            {
                float dx = targetX - pos.x;
                float runSpeed = kid && chaser ? speed * 1.08f : speed;       // the chaser is a little faster: tag ends
                if (Mathf.Abs(dx) > 0.05f) { facing = dx > 0 ? 1 : -1; pos.x = Mathf.Clamp(pos.x + facing * Mathf.Min(Mathf.Abs(dx), runSpeed * dt), minX, maxX); }
                if (Mathf.Abs(targetX - pos.x) <= 0.05f)
                {
                    if (!kid) { wait = 1.5f + Hash(animT * 3f) * 4f; if (Hash(animT * 7f) > 0.5f) facing = -facing; }
                    moving = false;
                }
            }
            if (hopT > 0f) hopT -= dt;
            Show(moving);
        }

        static float Hash(float v) { float f = Mathf.Sin(v * 127.1f) * 43758.5453f; return f - Mathf.Floor(f); }

        void Show(bool moving)
        {
            if (sr == null) return;
            Sprite s = null;
            if (kid)
            {
                int n = kidIdle != null ? kidIdle.Length : 0;
                if (n == 0) return;
                int k = look % n;
                s = moving && kidRun != null && kidRun.Length >= n * 2 ? kidRun[k * 2 + (int)(animT * 9f) % 2] : kidIdle[k];
            }
            else
            {
                int n = idle != null ? idle.Length : 0;
                if (n == 0) return;
                int k = look % n;
                s = moving && walk != null && walk.Length >= n * 4 ? walk[k * 4 + (int)(animT * 7f) % 4] : idle[k];
            }
            if (s != null) sr.sprite = s;
            // a child that has just been tagged jumps
            sr.transform.localPosition = new Vector3(0f, hopT > 0f ? Mathf.Sin((0.35f - hopT) / 0.35f * Mathf.PI) * 0.35f : 0f, 0f);
        }
    }
}
