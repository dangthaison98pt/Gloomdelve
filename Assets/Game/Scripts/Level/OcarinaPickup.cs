using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The Blue Ocarina, shaken out of a fir of the street (ShakeTree). It drops to the snow and glints there; picked up
    /// (Enter), the player holds it up over their head while it plays a few notes of its own, and "Ocarina of GOAT"
    /// is earned (Achievements). It goes into the bag's KEY ITEMS for good (Villagers.KeyOcarina), and can be
    /// played from there.
    /// </summary>
    public class OcarinaPickup : Entity, IInteractable
    {
        [Tooltip("Its outline while the player is in reach.")] public Sprite outline;
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
            p.restingIn = this; p.restAngle = 0f;      // still, holding it up
            heldT = 0f;
            Sfx.Play("ocarina", pos, 0.9f);
            Fx.Text(new Vector2(p.pos.x, p.pos.y + 2.4f), "Blue Ocarina", new Color(0.6f, 0.85f, 1f), 2f);
        }

        public override void Tick(float dt)
        {
            if (holder == null)
            {
                prevPos = pos;
                StepPhysics(dt, 22f);
                if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 8f * dt);
                glintT -= dt;
                if (glintT <= 0f) { glintT = 1.1f; Fx.Sparks(new Vector2(pos.x, pos.y + 0.3f), new Color(0.7f, 0.9f, 1f), 3, 1.5f); }
                return;
            }
            // held up over the head, a few notes, then it is earned
            heldT += dt;
            prevPos = pos = new Vector2(holder.pos.x, holder.pos.y + 1.55f);
            if (Mathf.Repeat(heldT, 0.3f) < dt) Fx.Sparks(pos + new Vector2(0f, 0.2f), new Color(0.7f, 0.9f, 1f), 3, 2f);
            if (heldT < holdTime && !holder.IsDead) return;
            if (holder.restingIn == this) holder.restingIn = null;
            holder = null;
            Villagers.GiveKey(Villagers.KeyOcarina);
            Achievements.Unlock(Achievements.OcarinaOfGoat);
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
