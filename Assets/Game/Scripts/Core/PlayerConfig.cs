using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        public MotorStats motor = new MotorStats();

        [Header("Run start")]
        public int startHealth = 10;
        public int startBombs = 4;
        public int startRopes = 4;
        public int startGold = 0;
        [Tooltip("Equipment ids the player starts every run with (see GameConfig.equipment).")]
        public string[] startingGear = { "bomb", "rope" };

        [Header("Blade (fields keep the old whip names)")]
        public float whipDuration = 0.3f;
        public float whipCooldown = 0.1f;
        [Tooltip("Back-swing hitbox active until this time.")] public float whipBackEnd = 0.08f;
        [Tooltip("Front hitbox active from whipBackEnd until this time.")] public float whipFrontEnd = 0.2f;
        public Vector2 whipFrontSize = new Vector2(1.25f, 0.6f);
        public int whipDamage = 1;

        [Header("Throwing")]
        public Vector2 throwVelocity = new Vector2(11f, 4f);
        public Vector2 throwUpVelocity = new Vector2(2f, 14f);
        public Vector2 bombThrowVelocity = new Vector2(9f, 6.5f);
        public Vector2 bombThrowUpVelocity = new Vector2(1.5f, 13f);
        [Tooltip("Speed multiplier while carrying a heavy object (idol / Heartstone).")] public float heavyCarrySpeed = 0.8f;

        [Header("Stomp")]
        public float stompBounce = 9f;
        public float stompBounceHeld = 13.5f;

        [Header("Dash")]
        [Tooltip("Speed of the dash, tiles a second (it must stay under one tile a simulation step).")] public float dashSpeed = 17f;
        [Tooltip("Seconds a dash lasts (speed times this is how far it goes: 3.4 tiles).")] public float dashTime = 0.2f;
        [Tooltip("The energy bar a dash is paid from.")] public float dashEnergyMax = 100f;
        [Tooltip("Energy one dash costs.")] public float dashCost = 30f;
        [Tooltip("Seconds without dashing before the energy starts to come back.")] public float dashRegenDelay = 0.5f;
        [Tooltip("Energy that comes back each second once it has started to.")] public float dashRegenRate = 60f;
        [Tooltip("Seconds after a dash has ended in which nothing can hurt yet: time to come out of whatever was dashed through.")] public float dashGrace = 0.12f;

        [Header("Damage")]
        public float invulnTime = 1.2f;
        [Tooltip("Health lost when landing on spikes (99 = instant death, the old rule). The player is thrown clear and is briefly invulnerable.")]
        public int spikeDamage = 2;
        [Tooltip("Upward speed of the bounce off spikes.")] public float spikeBounce = 10f;
        public float hurtStunTime = 0.45f;
        public Vector2 hurtKnockback = new Vector2(5f, 7f);
        [Tooltip("Falls of at least this many tiles hurt and stun. The level validator never requires such a fall.")]
        public float fallDamageHeight = 9f;
        public int fallDamage = 2;
        public float fallStunTime = 1.0f;

        // Upgrade bonuses (spring boots, iron whip, blast powder...) live in ShopConfig offers -> modifiers.

        [Header("Rope")]
        public int ropeThrowHeight = 7;
        public int ropeLength = 8;
        [Tooltip("Seconds a hooked rope lasts before vanishing (it blinks for the last 3 s, never vanishes while climbed).")]
        public float ropeLifetime = 20f;

        [Header("Camera look")]
        public float lookDelay = 0.45f;
        public float lookDistance = 4.5f;
    }
}
