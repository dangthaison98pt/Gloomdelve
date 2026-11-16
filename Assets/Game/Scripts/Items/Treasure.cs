using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Heavy carried treasure. Golden Idol: worth gold if carried out through the exit.
    /// Heartstone: the final goal; lifting it starts the vault collapse, carrying it out wins the run.
    /// </summary>
    public class Treasure : Item
    {
        [Header("Treasure")]
        public bool isHeartstone;
        public int value = 1200;
        public Color glow = new Color(1f, 0.85f, 0.3f);

        bool lifted;
        float sparkle;

        public override void OnPickedUp(PlayerController p)
        {
            base.OnPickedUp(p);
            if (!lifted)
            {
                lifted = true;
                if (isHeartstone && GameDirector.Instance != null) GameDirector.Instance.OnHeartstoneLifted();
                else Sfx.Play("idol", pos);
            }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            sparkle -= dt;
            if (sparkle <= 0f)
            {
                sparkle = 0.35f;
                Fx.Sparks(Center + Random.insideUnitCircle * 0.3f, glow, 1, 1f);
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            base.OnExplosion(center, radius, source, generation, damage);
            vel *= 0.5f;   // heavy
        }
    }
}
