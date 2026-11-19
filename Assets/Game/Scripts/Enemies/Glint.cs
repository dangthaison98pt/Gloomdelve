using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Shardlight Geode: a glint - a bat of living prism. It sleeps on the ceiling until it sees the player, then
    /// keeps its distance in the air and shoots light: it holds still and a thin line reaches from it to the
    /// player (the warning; the line follows them at first, then locks) - and a moment later a beam burns along
    /// that line. Step off the line and it misses. (Light does not ring a resonant crystal.)
    /// It is fragile: one stroke breaks it, if it can be reached.
    /// </summary>
    public class Glint : Enemy
    {
        public float speed = 3.2f;
        public float accel = 8f;
        public float wakeRange = 7f;
        [Tooltip("It tries to stay about this far from the player.")] public float keepDistance = 4f;
        [Header("Beam")]
        [Tooltip("Seconds between two beams.")] public float beamCooldown = 2.4f;
        [Tooltip("Seconds the thin line shows before the beam.")] public float aimTime = 0.75f;
        [Tooltip("For the last part of the aiming the line no longer follows the player.")] public float lockTime = 0.3f;
        public float fireTime = 0.22f;
        public float beamRange = 9f;
        public float beamWidth = 0.32f;
        public int beamDamage = 1;
        public Material beamMaterial;
        public SpriteAnim animHang;
        public SpriteAnim animFly;

        enum State { Asleep, Fly, Aim, Fire }
        State state;
        float timer, bobT, cool;
        Vector2 aimDir, beamEnd;
        bool hitPlayer;
        BeamVisual beam, spot;

        public bool Aiming => state == State.Aim;
        public bool Firing => state == State.Fire;
        /// <summary>Beams fired so far (tests).</summary>
        public int Beams { get; private set; }
        /// <summary>Its body never hurts: the beam does.</summary>
        public override int ContactDamage => 0;

        public override void Configure(SpawnPoint sp) { HangFromCeiling(sp); facing = 1; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Asleep;
            anim?.Play(animHang);
            bobT = Random.value * 6f;
            cool = 0.8f;
            beam = BeamVisual.Create(this, beamMaterial);
            spot = BeamVisual.Create(this, beamMaterial);
        }

        Vector2 EndOf(Vector2 from, Vector2 dir)
        {
            var g = World.Grid;
            Vector2 at = from;
            for (float d = 0f; d < beamRange; d += 0.2f)
            {
                at = from + dir * d;
                if (g.IsSolid(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y))) break;
            }
            return at;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            timer -= dt;
            if (state == State.Asleep)
            {
                if (SeesPlayer(wakeRange) && p.Center.y <= Center.y + 1f) { state = State.Fly; anim?.Play(animFly); Sfx.Play("bat", pos, 0.7f, 1.6f); }
                return;
            }
            if (stunTimer > 0f) { state = State.Fly; }
            bobT += dt * 6f;
            Vector2 desired = Vector2.zero;
            bool alive = p != null && !p.IsDead;
            switch (state)
            {
                case State.Fly:
                    cool -= dt;
                    if (alive)
                    {
                        Vector2 to = p.Center - Center;
                        facing = to.x >= 0 ? 1 : -1;
                        float dist = to.magnitude;
                        Vector2 dir = dist > 0.01f ? to / dist : Vector2.down;
                        // holds its distance, a little above the player
                        Vector2 want = dist > keepDistance + 0.6f ? dir * speed : dist < keepDistance - 0.6f ? -dir * speed : Vector2.zero;
                        desired = want + new Vector2(0f, (p.Center.y + 1.6f - Center.y) * 1.2f + Mathf.Sin(bobT) * 0.8f);
                        if (cool <= 0f && dist < beamRange - 1f && World.LineClear(Center, p.Center))
                        {
                            state = State.Aim; timer = aimTime;
                            aimDir = dir;
                            Sfx.Play("gem", pos, 0.6f, 1.8f);
                        }
                    }
                    else desired = new Vector2(0f, Mathf.Sin(bobT));
                    break;
                case State.Aim:
                    if (alive && timer > lockTime)
                    {
                        Vector2 to = p.Center - Center;
                        if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                        facing = to.x >= 0 ? 1 : -1;
                    }
                    beamEnd = EndOf(Center, aimDir);
                    if (timer <= 0f)
                    {
                        state = State.Fire; timer = fireTime;
                        hitPlayer = false;
                        Beams++;
                        Sfx.Play("flame", pos, 0.8f, 1.8f);
                    }
                    break;
                case State.Fire:
                    beamEnd = EndOf(Center, aimDir);
                    if (alive && !hitPlayer && BeamVisual.DistanceTo(p.Center, Center, beamEnd) < beamWidth * 0.5f + 0.3f)
                    {
                        hitPlayer = true;
                        p.TakeDamage(new DamageInfo(beamDamage, DamageType.Fire, this, new Vector2(Mathf.Sign(aimDir.x) * 5f, 5f), "a glint's beam"));
                    }
                    if (timer <= 0f) { state = State.Fly; cool = beamCooldown; }
                    break;
            }
            vel = Vector2.MoveTowards(vel, desired, accel * dt);
            var b = new Body { pos = pos, vel = vel, hw = size.x * 0.5f, h = size.y };
            BodyPhysics.Move(World.Grid, ref b, dt, true);
            pos = b.pos; vel = b.vel;
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.5f;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (beam == null) return;
            if (Dead || (state != State.Aim && state != State.Fire)) { beam.Hide(); if (spot != null) spot.Hide(); return; }
            if (state == State.Aim) beam.Show(Center, beamEnd, 0.05f, new Color(1f, 0.7f, 1f, timer > lockTime ? 0.4f : 0.8f));
            else beam.Show(Center, beamEnd, beamWidth, new Color(1f, 0.8f, 1f, 0.95f));
            // a round spot where the beam strikes: no square end
            if (spot != null) spot.ShowDot(beamEnd, state == State.Aim ? 0.14f : beamWidth * 1.9f, new Color(1f, 0.8f, 1f, state == State.Aim ? 0.6f : 0.95f));
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            if (beam != null) Destroy(beam.gameObject);
            if (spot != null) Destroy(spot.gameObject);
        }

        protected override void OnHurt(in DamageInfo d) { base.OnHurt(d); state = State.Fly; cool = Mathf.Max(cool, 1f); }
    }
}
