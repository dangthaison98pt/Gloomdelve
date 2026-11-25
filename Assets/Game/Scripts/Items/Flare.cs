using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A flare from Bera the Lamplighter (FlareGear): thrown like a bomb, it lands and burns for half a minute with
    /// a strong red light, then gutters out over its last few seconds. It hurts nobody. It can be picked up and
    /// carried like a torch, and thrown again while it burns. The light is its own GlowLight (made by the project
    /// builder); this only counts the time down and dims it at the end.
    /// </summary>
    public class Flare : Item
    {
        [Tooltip("Seconds it burns.")] public float burnTime = 30f;
        [Tooltip("Seconds at the end over which the light dies away.")] public float fadeTime = 4f;

        float left = -1f, baseIntensity = -1f, sparkT;
        GlowLight glow;

        public float Left => left;
        public bool Burning => left > 0f;
        /// <summary>Tests: how long it has left to burn.</summary>
        public void BurnFor(float seconds) => left = seconds;

        public override void OnSpawn()
        {
            base.OnSpawn();
            left = burnTime;
            glow = GetComponent<GlowLight>();
            if (glow != null) { if (baseIntensity < 0f) baseIntensity = glow.intensity; glow.SetOn(true); }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Dead) return;
            left -= dt;
            if (glow != null && baseIntensity >= 0f)
                glow.intensity = baseIntensity * Mathf.Clamp01(left / Mathf.Max(0.01f, fadeTime));
            sparkT -= dt;
            if (sparkT <= 0f && left > 0.5f)
            {
                sparkT = 0.12f;
                Fx.Sparks(Center + Vector2.up * 0.15f, new Color(1f, 0.55f, 0.35f), 1, 2.2f);
            }
            if (left <= 0f)
            {
                Fx.Dust(Center, 2);
                if (holder != null && holder.held == this) holder.DropHeld();
                Despawn();
            }
        }

        /// <summary>It lights the way; it does not hurt what it hits.</summary>
        protected override void HitEntities() { }
    }
}
