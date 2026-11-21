using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Peaceful shopkeeper. The first hits by the player (whip, thrown object, bomb, blast on his wares) are only
    /// warnings: no damage, a shout and a message. The hit after the last warning makes him angry: from then on he
    /// takes damage, chases and hurts you, and his wares become free.
    /// He holds a grudge for the rest of the run (RunState.shopkeeperWanted / shopkeeperDead):
    ///  - angered but alive: every later shop is closed, and when you walk in he attacks with two brothers;
    ///  - killed: every later shop is simply gone.
    /// </summary>
    public class Shopkeeper : Enemy, IInteractable
    {
        // He buys as well as sells: a golden idol carried up to him is worth its gold on the spot (it need not be
        // lugged to the way out). Only while he is at peace, and never the Sunstone.
        static Treasure Idol(PlayerController p) => p != null && p.held is Treasure t && !t.isHeartstone ? t : null;

        public string GetPrompt(PlayerController p)
        {
            var idol = Idol(p);
            if (angry || Dead || idol == null) return null;
            return $"[enter]  Sell  -  {ItemLog.Title(idol.displayName)}, ${idol.value}";
        }

        public void Interact(PlayerController p)
        {
            var idol = Idol(p);
            var run = RunSession.Current;
            if (angry || Dead || idol == null || run == null) return;
            int paid = idol.value;
            var picture = idol.GetComponentInChildren<SpriteRenderer>();
            Sprite icon = picture != null ? picture.sprite : null;
            string what = ItemLog.Title(idol.displayName);
            p.held = null;
            idol.Despawn();
            run.AddResource(Res.Gold, paid);
            Sfx.PlayUI("buy");
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 14, 4f);
            var dir = GameDirector.Instance;
            if (dir != null && dir.hud != null) dir.hud.Gain("sold:" + what, what + " sold", icon, paid, null);
        }

        public float chaseSpeed = 4.6f;
        public float jumpVelocity = 12f;
        public SpriteAnim animIdle;
        public SpriteAnim animAngry;

        [System.NonSerialized] public bool angry;
        /// <summary>One of the two brothers called in for an ambush (their deaths do not close the shops).</summary>
        [System.NonSerialized] public bool brother;
        [Tooltip("An ambush starts when the player comes this close (or steps into the shop).")] public float ambushRange = 5.5f;
        bool ambush;
        static bool spawningBrother;
        [System.NonSerialized] public RectInt shopRect;
        readonly List<ShopItem> wares = new List<ShopItem>();
        float jumpCd;
        int strikes;
        float strikeCooldown;

        /// <summary>Number of warnings given so far (0..warningsBeforeAngry).</summary>
        public int Strikes => strikes;

        public override int ContactDamage => angry ? base.ContactDamage : 0;
        public override bool IsStompable => angry;
        public override bool IsHazard => false;   // peaceful until provoked: allowed inside shops / near the entrance
        public override bool TriggersTraps => angry && base.TriggersTraps;

        public void AddWare(ShopItem s) { wares.Add(s); s.keeper = this; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (World.config.shop != null) { maxHealth = World.config.shop.shopkeeperHealth; health = maxHealth; }
            anim?.Play(animIdle);
            var run = RunSession.Current;
            if (spawningBrother)
            {
                brother = true;
                angry = true;
                anim?.Play(animAngry);
            }
            else if (run != null && run.shopkeeperDead) Despawn();      // he is dead: the shop stays empty
            else if (run != null && run.shopkeeperWanted) ambush = true; // he remembers
        }

        bool PlayerInShop(PlayerController p)
        {
            if (shopRect.width > 0 && shopRect.Contains(new Vector2Int(Mathf.FloorToInt(p.pos.x), Mathf.FloorToInt(p.pos.y)))) return true;
            return Mathf.Abs(p.pos.x - pos.x) < ambushRange && Mathf.Abs(p.pos.y - pos.y) < 2.5f;
        }

        void Ambush()
        {
            ambush = false;
            angry = true;
            anim?.Play(animAngry);
            Sfx.Play("shopkeeper_angry", pos);
            Fx.Text(Center + Vector2.up, "YOU AGAIN!", new Color(1f, 0.3f, 0.3f), 1.5f);
            if (GameDirector.Instance != null)
                GameDirector.Instance.ShowMessage("The shopkeeper remembers you. His brothers are with him - and nothing is for sale.");
            var g = World.Grid;
            spawningBrother = true;
            foreach (float dx in new[] { -1.2f, 1.2f })
            {
                var at = new Vector2(pos.x + dx, pos.y);
                if (!g.IsEmpty(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y + 0.2f))) at.x = pos.x;
                World.SpawnById("shopkeeper", at);
            }
            spawningBrother = false;
        }

        protected override void Die(in DamageInfo d)
        {
            var run = RunSession.Current;
            if (!brother && run != null)
            {
                run.shopkeeperDead = true;
                run.shopkeeperWanted = false;
                if (GameDirector.Instance != null) GameDirector.Instance.ShowMessage("The shopkeeper is dead. No shop will open for you again.");
            }
            base.Die(d);
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
                if (ambush && p != null && !p.IsDead && PlayerInShop(p)) Ambush();
            }
            else if (stunTimer <= 0f && grounded && p != null && !p.IsDead)
            {
                int dir = melee == MeleePhase.Strike ? facing : (p.pos.x >= pos.x ? 1 : -1);
                facing = dir;
                // he has to land a blow: stops short, winds up, then lunges - only the lunge hurts
                bool near = Mathf.Abs(p.pos.x - pos.x) < 1.25f && Mathf.Abs(p.pos.y - pos.y) < 1.1f;
                if (Melee(dt, near, 0.28f, 0.2f, 0.55f)) vel.x = melee == MeleePhase.Strike ? dir * chaseSpeed * 1.5f : 0f;
                else vel.x = dir * chaseSpeed;
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
            if (!brother && RunSession.Current != null) RunSession.Current.shopkeeperWanted = true;   // he will not forget
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
                    : "The shopkeeper warns you: \"Careful with that, Brann!\"");
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
