using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Mossroot boss: a giant spider. Leaps at the player twice (the landing shakes the cave), then calls bats
    /// down from the dark. Whip it between leaps or stomp it right after it lands.
    /// </summary>
    public class Broodmother : Boss
    {
        public float leapSpeedX = 6.5f;
        public float leapVelocity = 13f;
        public float restTime = 1.1f;
        public string minionId = "bat";
        public int maxMinions = 3;
        public SpriteAnim animIdle;
        public SpriteAnim animJump;

        float timer;
        int leaps;

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
            if (grounded)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                anim?.Play(animIdle);
                timer -= dt;
                if (p != null && Mathf.Abs(p.pos.x - pos.x) > 0.5f) facing = p.pos.x > pos.x ? 1 : -1;
                if (timer <= 0f && p != null)
                {
                    if (leaps >= 2) { Brood(); leaps = 0; timer = restTime; }
                    else
                    {
                        vel = new Vector2(Mathf.Clamp((p.pos.x - pos.x) * 1.3f, -leapSpeedX, leapSpeedX), leapVelocity);
                        grounded = false;
                        leaps++;
                        anim?.Play(animJump);
                        Sfx.Play("toad", pos, 1f, 0.6f);
                    }
                }
            }
            var b = StepPhysics(dt, gravity);
            if (b.landed)
            {
                timer = restTime;
                Fx.Shake(0.25f);
                Fx.Dust(pos, 6);
                Sfx.Play("thud", pos);
            }
        }

        void Brood()
        {
            int room = maxMinions - CountAlive<Bat>();
            var g = World.Grid;
            for (int i = 0; i < 2 && room > 0; i++)
            {
                var at = new Vector2(pos.x + (i == 0 ? -2f : 2f), pos.y + 3.2f);
                if (!g.IsEmpty(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y))) continue;
                var m = World.SpawnById(minionId, at);
                if (m == null) continue;
                if (m is Enemy en) en.summoned = true;
                room--;
            }
            Sfx.Play("bat", pos, 1f, 0.7f);
            Fx.Debris(Center, new Color(0.5f, 0.4f, 0.6f), 8);
        }
    }
}
