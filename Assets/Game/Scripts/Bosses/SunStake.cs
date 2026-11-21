using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A stake of light the Eclipse King's core drives into the floor when it calls the column of light: four of them
    /// across the hall. One blow of anything breaks a stake (the blade, a thrown thing, a blast), and breaking it
    /// wounds the King. A stake still standing when the column reaches it bursts (EclipseKing.BurstStake): light, not
    /// fire - it breaks no stone, and burns whoever stands in it.
    /// </summary>
    public class SunStake : Entity
    {
        [System.NonSerialized] public EclipseKing owner;

        public override bool CanBeHitByItems => true;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (sr != null && LevelLighting.Instance != null && LevelLighting.Instance.glowMaterial != null) sr.sharedMaterial = LevelLighting.Instance.glowMaterial;
        }

        public override void Tick(float dt)
        {
            // it does not outlast the one who planted it
            if (owner == null || owner.Dead) { Fx.Sparks(Center, new Color(1f, 0.82f, 0.35f), 6, 3f); Despawn(); }
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (Dead || d.source == owner || d.source is BossShot) return false;      // (its own master's light does not break it)
            Fx.Debris(Center, new Color(1f, 0.82f, 0.35f), 10);
            Fx.Sparks(Center, new Color(1f, 0.95f, 0.7f), 12, 6f);
            Sfx.Play("crack", pos);
            var o = owner;
            Despawn();
            if (o != null && !o.Dead) o.StakeBroken(this);
            return true;
        }
    }
}
