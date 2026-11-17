using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom: a walking mushroom. It never bites. When the player comes close it stops, swells up (the tell)
    /// and bursts a cloud of spores around itself (SporeCloud: left and right are swapped), then stands deflated
    /// for a while. It also puffs when it dies - so stomping or slashing it lands the player in the cloud. Bait the puff, step
    /// back out of it, then kill it while it is deflated (no spores left) - or throw something from afar.
    /// </summary>
    public class Puffshroom : Enemy
    {
        public float speed = 1.3f;
        [Tooltip("It starts to swell when the player is this close.")] public float puffRange = 1.6f;
        public float swellTime = 0.6f;
        [Tooltip("Deflated and harmless for this long after a puff.")] public float deflatedTime = 2.6f;
        public string cloudId = "sporecloud";
        public SpriteAnim animWalk, animSwell, animDeflated;

        enum State { Walk, Swell, Deflated }
        State state;
        float timer;

        /// <summary>It has no attack that hurts.</summary>
        public override bool Attacking => false;
        public bool Swelling => state == State.Swell;
        public bool Deflated => state == State.Deflated;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Walk;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            if (grounded && stunTimer <= 0f)
            {
                switch (state)
                {
                    case State.Walk:
                        int dir = PatrolStep();
                        vel.x = dir * speed;
                        anim?.Play(animWalk);
                        if (p != null && !p.IsDead && (p.Center - Center).sqrMagnitude < puffRange * puffRange && World.LineClear(Center, p.Center))
                        {
                            state = State.Swell; timer = swellTime;
                            vel.x = 0f;
                            anim?.Play(animSwell);
                            Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.35f), "!", new Color(0.7f, 1f, 0.5f), 0.45f);
                            Sfx.Play("toad", pos, 0.6f, 1.5f);
                        }
                        break;
                    case State.Swell:
                        vel.x = 0f;
                        timer -= dt;
                        if (timer <= 0f)
                        {
                            Puff();
                            state = State.Deflated; timer = deflatedTime;
                            anim?.Play(animDeflated);
                        }
                        break;
                    case State.Deflated:
                        vel.x = 0f;
                        timer -= dt;
                        if (timer <= 0f) state = State.Walk;
                        break;
                }
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
            var b = StepPhysics(dt, gravity);
            if (b.hitLeft || b.hitRight) facing = -facing;
            CheckSpikes(b);
        }

        void Puff()
        {
            var w = World;
            if (w == null) return;
            w.SpawnById(cloudId, Center);
            Fx.Debris(Center, new Color(0.62f, 1f, 0.5f), 10);
            Sfx.Play("thud", pos, 0.6f, 1.7f);
        }

        protected override void Die(in DamageInfo d)
        {
            if (state != State.Deflated) Puff();        // nothing left to puff right after a burst
            base.Die(d);
        }
    }
}
