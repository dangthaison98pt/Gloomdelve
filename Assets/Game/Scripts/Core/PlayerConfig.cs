using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        public MotorStats motor = new MotorStats();

        [Header("Run start")]
        public int startHealth = 4;
        public int startBombs = 4;
        public int startRopes = 4;
        public int startGold = 0;
        [Tooltip("Equipment ids the player starts every run with (see GameConfig.equipment).")]
        public string[] startingGear = { "bomb", "rope" };

        [Header("Whip")]
        public float whipDuration = 0.3f;
        public float whipCooldown = 0.1f;
        [Tooltip("Back-swing hitbox active until this time.")] public float whipBackEnd = 0.08f;
        [Tooltip("Front hitbox active from whipBackEnd until this time.")] public float whipFrontEnd = 0.2f;
        public Vector2 whipFrontSize = new Vector2(1.25f, 0.6f);
        public int whipDamage = 1;
        public int ironWhipDamage = 2;
        public float ironWhipRangeBonus = 0.45f;

        [Header("Throwing")]
        public Vector2 throwVelocity = new Vector2(11f, 4f);
        public Vector2 throwUpVelocity = new Vector2(2f, 14f);
        public Vector2 bombThrowVelocity = new Vector2(9f, 6.5f);
        public Vector2 bombThrowUpVelocity = new Vector2(1.5f, 13f);
        [Tooltip("Speed multiplier while carrying a heavy object (idol / Heartstone).")] public float heavyCarrySpeed = 0.8f;

        [Header("Stomp")]
        public float stompBounce = 9f;
        public float stompBounceHeld = 13.5f;

        [Header("Damage")]
        public float invulnTime = 1.2f;
        public float hurtStunTime = 0.45f;
        public Vector2 hurtKnockback = new Vector2(5f, 7f);
        [Tooltip("Falls of at least this many tiles hurt and stun. The level validator never requires such a fall.")]
        public float fallDamageHeight = 9f;
        public int fallDamage = 1;
        public float fallStunTime = 1.0f;

        [Header("Upgrades")]
        public float springBootsJumpMult = 1.17f;
        public float blastPowderRadiusBonus = 0.9f;

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
