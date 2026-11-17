using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom: a moth drawn to the player's light. It sleeps on the ceiling, wakes when the light comes near,
    /// flutters around the player's head and drinks the light: while a moth is close, the light the player
    /// carries shrinks (LevelLighting.DimPlayerLight) - in the dark that is the danger. It never hurts.
    /// One hit kills it and the light comes back.
    /// </summary>
    public class GloomMoth : Enemy
    {
        public float speed = 3.4f;
        public float accel = 10f;
        public float wakeRange = 6.5f;
        [Tooltip("Within this distance of the player it drinks the light.")] public float drainRange = 1.7f;
        [Tooltip("Size of the player's light while a moth drinks it (1 = untouched).")] [Range(0.1f, 1f)] public float lightLeft = 0.38f;
        public SpriteAnim animHang, animFly;

        bool awake;
        float orbitT;

        public override bool Attacking => false;
        public bool IsAwake => awake;
        public bool Draining { get; private set; }

        public override void Configure(SpawnPoint sp) { HangFromCeiling(sp); facing = 1; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            anim?.Play(animHang);
            orbitT = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            Draining = false;
            var p = Player;
            if (!awake)
            {
                if (SeesPlayer(wakeRange)) Wake();
                return;
            }
            orbitT += dt * 3.2f;
            Vector2 desired = Vector2.zero;
            if (stunTimer <= 0f && p != null && !p.IsDead)
            {
                // circles a point just above the player's head
                Vector2 target = p.Center + new Vector2(Mathf.Cos(orbitT) * 0.9f, 0.75f + Mathf.Sin(orbitT * 1.7f) * 0.35f);
                Vector2 to = target - Center;
                desired = to.sqrMagnitude > 0.04f ? to.normalized * speed : to * 4f;
                facing = p.Center.x >= Center.x ? 1 : -1;
                if ((p.Center - Center).sqrMagnitude < drainRange * drainRange)
                {
                    Draining = true;
                    LevelLighting.DimPlayerLight(lightLeft);
                }
            }
            vel = Vector2.MoveTowards(vel, desired, accel * dt);
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
        }

        void Wake()
        {
            awake = true;
            anim?.Play(animFly);
            Sfx.Play("bat", pos, 0.5f, 1.6f);
            vel = new Vector2(0f, -1.2f);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!awake) Wake();
            return base.TakeDamage(d);
        }
    }
}
