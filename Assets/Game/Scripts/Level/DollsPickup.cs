using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Two little dolls, hand in hand - one of clay, one of wood - kept in a little chest in the room inside the
    /// kennel (DollsChest). Taken out of it, the player holds them up over their head for a moment, and "It Takes Two" is earned (Achievements). They go into the bag's KEY ITEMS for
    /// good (Villagers.KeyDolls). A small thank-you to a game about two people who had to hold on to each other.
    /// </summary>
    public class DollsPickup : Entity, IInteractable
    {
        [Tooltip("Their outline while the player is in reach.")] public Sprite outline;
        public SpriteRenderer glow;
        public float holdTime = 1.8f;

        PlayerController holder;
        float heldT, glintT;

        public bool Held => holder != null;

        public string GetPrompt(PlayerController p) => holder != null ? null : "[enter]  Pick up";

        public void Interact(PlayerController p)
        {
            if (p == null || holder != null || p.motor.mode != MotorMode.Normal || !p.motor.grounded) return;
            holder = p;
            p.restingIn = this; p.restAngle = 0f;      // still, holding them up
            heldT = 0f;
            Sfx.Play("pickup", pos, 0.9f);
            Fx.Text(new Vector2(p.pos.x, p.pos.y + 2.4f), "Two Little Dolls", new Color(1f, 0.78f, 0.82f), 2f);
        }

        public override void Tick(float dt)
        {
            if (holder == null)
            {
                glintT -= dt;
                if (glintT <= 0f) { glintT = 1.3f; Fx.Sparks(new Vector2(pos.x, pos.y + 0.3f), new Color(1f, 0.8f, 0.85f), 2, 1.2f); }
                return;
            }
            // held up over the head, a few sparks, then it is earned
            heldT += dt;
            prevPos = pos = new Vector2(holder.pos.x, holder.pos.y + 1.55f);
            if (Mathf.Repeat(heldT, 0.3f) < dt) Fx.Sparks(pos + new Vector2(0f, 0.2f), new Color(1f, 0.6f, 0.7f), 3, 2f);
            if (heldT < holdTime && !holder.IsDead) return;
            if (holder.restingIn == this) holder.restingIn = null;
            holder = null;
            Villagers.GiveKey(Villagers.KeyDolls);
            Achievements.Unlock(Achievements.ItTakesTwo);
            Despawn();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            InReach.Show(glow, outline, this);
        }

        protected override void OnDespawn()
        {
            if (holder != null && holder.restingIn == this) holder.restingIn = null;
            base.OnDespawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
