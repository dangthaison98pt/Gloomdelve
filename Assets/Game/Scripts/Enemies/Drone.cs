using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks: a hovering turret. It hangs in the air a little below the ceiling and never chases. When it can
    /// see the player its eye turns red (the tell), then it fires one slow bolt at where the player is - walk on or
    /// cut the bolt with the blade. Touching it does nothing. One hit, a stomp or a thrown rock brings it down.
    /// </summary>
    public class Drone : Enemy
    {
        public float sightRange = 8f;
        [Tooltip("Seconds between shots.")] public float cooldown = 2.6f;
        [Tooltip("Red-eye warning before the shot.")] public float aimTime = 0.55f;
        public float boltSpeed = 5.5f;
        public string boltId = "bolt";
        [Tooltip("How far below the ceiling it hovers.")] public float hang = 0.8f;
        public SpriteAnim animHover, animAim;

        Vector2 home;
        float timer, aim, bobT;

        public override bool Attacking => false;
        public bool Aiming => aim > 0f;

        public override void Configure(SpawnPoint sp)
        {
            HangFromCeiling(sp);
            pos.y -= hang;
            facing = 1;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            home = pos;
            timer = cooldown * 0.6f;
            bobT = Random.value * 6f;
            anim?.Play(animHover);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            bobT += dt * 2.4f;
            // knocked about by a hit, it drifts back to its post
            Vector2 want = home + new Vector2(0f, Mathf.Sin(bobT) * 0.12f);
            vel = Vector2.MoveTowards(vel, (want - pos) * 4f, 20f * dt);
            pos += vel * dt;
            var p = Player;
            bool sees = stunTimer <= 0f && SeesPlayer(sightRange);
            if (sees) facing = p.Center.x >= Center.x ? 1 : -1;
            if (aim > 0f)
            {
                aim -= dt;
                if (aim <= 0f)
                {
                    anim?.Play(animHover);
                    if (sees) Fire(p);
                    timer = cooldown;
                }
                return;
            }
            timer -= dt;
            if (timer <= 0f && sees)
            {
                aim = aimTime;
                anim?.Play(animAim);
                Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.3f), "!", new Color(1f, 0.4f, 0.3f), 0.45f);
                Sfx.Play("warning", pos, 0.4f, 1.6f);
            }
        }

        void Fire(PlayerController p)
        {
            var shot = World.SpawnById(boltId, Center) as BossShot;
            if (shot == null) return;
            Vector2 to = p.Center - Center;
            shot.vel = (to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.down) * boltSpeed;
            shot.owner = this;
            Sfx.Play("throw", pos, 0.7f, 1.7f);
        }
    }
}
