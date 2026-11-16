using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Peaceful shopkeeper. The first hits by the player (whip, thrown object, bomb, blast on his wares) are only
    /// warnings: no damage, a shout and a message. The hit after the last warning makes him angry: from then on he
    /// takes damage, chases and hurts you, and his wares become free.
    /// </summary>
    public class Shopkeeper : Enemy
    {
        public float chaseSpeed = 4.6f;
        public float jumpVelocity = 12f;
        public SpriteAnim animIdle;
        public SpriteAnim animAngry;

        [System.NonSerialized] public bool angry;
        [System.NonSerialized] public RectInt shopRect;
        readonly List<ShopItem> wares = new List<ShopItem>();
        float jumpCd;
        int strikes;
        float strikeCooldown;

        /// <summary>Number of warnings given so far (0..warningsBeforeAngry).</summary>
        public int Strikes => strikes;

        public override int ContactDamage => angry && stunTimer <= 0f ? contactDamage : 0;
        public override bool IsStompable => angry;
        public override bool IsHazard => false;   // peaceful until provoked: allowed inside shops / near the entrance
        public override bool TriggersTraps => angry && base.TriggersTraps;

        public void AddWare(ShopItem s) { wares.Add(s); s.keeper = this; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (World.config.shop != null) { maxHealth = World.config.shop.shopkeeperHealth; health = maxHealth; }
            anim?.Play(animIdle);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            jumpCd -= dt;
            strikeCooldown -= dt;
            if (!angry)
            {
                if (p != null) facing = p.pos.x >= pos.x ? 1 : -1;
                vel.x = 0f;
            }
            else if (stunTimer <= 0f && grounded && p != null && !p.IsDead)
            {
                int dir = p.pos.x >= pos.x ? 1 : -1;
                facing = dir;
                vel.x = dir * chaseSpeed;
                if (jumpCd <= 0f && (WallAhead(dir) || p.pos.y > pos.y + 1.2f))
                {
                    vel.y = jumpVelocity; grounded = false; jumpCd = 0.8f;
                }
            }
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
        }

        public void MakeAngry()
        {
            if (angry || Dead) return;
            angry = true;
            anim?.Play(animAngry);
            Sfx.Play("shopkeeper_angry", pos);
            Fx.Text(Center + Vector2.up, "THIEF!", new Color(1f, 0.3f, 0.3f), 1.5f);
            if (GameDirector.Instance != null) GameDirector.Instance.ShowMessage("The shopkeeper is furious! His wares are free for the taking...");
        }

        int WarningsAllowed => World.config.shop != null ? World.config.shop.warningsBeforeAngry : 2;

        /// <summary>
        /// The player hit him (or his wares). Returns true when the hit was only a warning (no damage).
        /// One swing / one blast counts once thanks to a short cooldown.
        /// </summary>
        public bool Strike()
        {
            if (angry || Dead) return false;
            if (strikeCooldown > 0f) return true;
            strikeCooldown = 0.6f;
            strikes++;
            if (strikes <= WarningsAllowed)
            {
                Warn(strikes);
                return true;
            }
            MakeAngry();
            return false;
        }

        void Warn(int n)
        {
            bool last = n >= WarningsAllowed;
            var p = Player;
            if (p != null) facing = p.pos.x >= pos.x ? 1 : -1;
            stunTimer = 0.25f;
            Flash(0.1f);
            Sfx.Play("shopkeeper_angry", pos, 0.6f, last ? 1.15f : 1.35f);
            Fx.Text(Center + Vector2.up, last ? "LAST WARNING!" : "HEY! WATCH IT!", last ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.85f, 0.3f), 1.5f);
            if (GameDirector.Instance != null)
                GameDirector.Instance.ShowMessage(last
                    ? "The shopkeeper glares at you: \"One more hit and you're dead meat!\""
                    : "The shopkeeper warns you: \"Careful with that, explorer!\"");
        }

        static bool CausedByPlayer(in DamageInfo d) =>
            d.source is PlayerController || d.source is Bomb || (d.source is Item it && it.thrower is PlayerController) ||
            d.type == DamageType.Stomp || (d.type == DamageType.Explosion && d.source == null);

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!angry && CausedByPlayer(d) && Strike()) return true;   // warning: no damage
            return base.TakeDamage(d);
        }
    }
}
