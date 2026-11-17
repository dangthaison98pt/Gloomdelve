using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Laid by the Broodmother: an egg sac on the floor. It does nothing but throb, faster and faster - and after
    /// 'hatchTime' a spiderling comes out of it. One blade stroke or a stomp bursts it first.
    /// </summary>
    public class EggSac : Enemy
    {
        public float hatchTime = 4.5f;
        public string hatchId = "spider";
        public SpriteAnim animPulse;

        float timer;

        public override bool Attacking => false;
        public float TimeLeft => timer;

        public override void OnSpawn()
        {
            base.OnSpawn();
            timer = hatchTime;
            anim?.Play(animPulse);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            timer -= dt;
            if (anim != null) anim.speed = Mathf.Lerp(3f, 1f, Mathf.Clamp01(timer / hatchTime));
            StepPhysics(dt, gravity);
            if (timer > 0f) return;
            var e = World.SpawnById(hatchId, pos);
            if (e is Enemy en) en.summoned = true;        // no gold for the brood of a boss
            Fx.Debris(Center, new Color(0.85f, 0.9f, 0.75f), 8);
            Sfx.Play("toad", pos, 0.7f, 1.6f);
            Despawn();
        }
    }
}
