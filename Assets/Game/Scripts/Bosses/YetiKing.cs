using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Frostdeep boss: the yeti king. Throws fans of snowballs (they shove and freeze - wear a fur coat), calls
    /// penguins to slide across the ice, and now and then leaps at the player. All of it on slippery ground.
    /// </summary>
    public class YetiKing : Boss
    {
        public float actionGap = 1.5f;
        public float windupTime = 0.5f;
        public float ballGravity = 28f;
        public int maxPenguins = 2;
        public float leapVelocity = 12f;
        public string projectileId = "snowball";
        public string minionId = "penguin";
        public SpriteAnim animIdle;
        public SpriteAnim animThrow;

        float timer, windup;
        int step;
        Vector2 aim;

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = 1.6f;
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Target;
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 14f * dt);
            if (windup > 0f)
            {
                windup -= dt;
                if (windup <= 0f) Volley();
            }
            else if (grounded)
            {
                anim?.Play(animIdle);
                if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                timer -= dt;
                if (timer <= 0f && p != null)
                {
                    timer = actionGap;
                    int kind = step++ % 4;
                    if (kind == 2 && CountAlive<Penguin>() < maxPenguins) Summon(p);
                    else if (kind == 3)
                    {
                        vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 1.1f, -6f, 6f), leapVelocity);
                        grounded = false;
                        Sfx.Play("brute", pos, 1f, 1.2f);
                    }
                    else { windup = windupTime; aim = p.Center; anim?.Play(animThrow); }
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.landed) { Fx.Shake(0.3f); Fx.Dust(pos, 6); Sfx.Play("thud", pos); }
        }

        void Volley()
        {
            Vector2 from = new Vector2(pos.x + facing * 0.6f, pos.y + size.y + 0.1f);
            float t = Mathf.Clamp(Mathf.Abs(aim.x - from.x) / 8f, 0.6f, 1.1f);
            for (int i = -1; i <= 1; i++)
            {
                var e = World.SpawnById(projectileId, from);
                if (e == null) continue;
                e.vel = Lob(from, aim + new Vector2(i * 2.2f, 0f), ballGravity, t + i * 0.08f);
                if (e is Snowball s) { s.gravity = ballGravity; s.owner = this; }
            }
            Sfx.Play("throw", pos);
        }

        void Summon(PlayerController p)
        {
            var g = World.Grid;
            // at the arena edge farthest from the player
            int x = p.pos.x > g.Width * 0.5f ? 2 : g.Width - 3;
            int y = Mathf.FloorToInt(pos.y);
            if (!g.IsEmpty(x, y)) return;
            var e = World.SpawnById(minionId, new Vector2(x + 0.5f, y));
            if (e != null) e.facing = p.pos.x > e.pos.x ? 1 : -1;
            Sfx.Play("toad", pos, 1f, 1.5f);
        }
    }
}
