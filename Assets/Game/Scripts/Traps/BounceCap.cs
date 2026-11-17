using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sporegloom: a wide glowing mushroom cap that works as a trampoline. Fall onto it and it throws the player
    /// high into the air (higher with jump held) and the fall does no damage. It is also a light in the dark.
    /// Harmless and unbreakable. Placed by the level generator (AreaConfig.bounceCapChance).
    /// The route check never counts on it (levels stay finishable without bouncing), so it is always a shortcut or
    /// a way to a side ledge, never the only way. The test bot (god mode) passes through it.
    /// </summary>
    public class BounceCap : Prop
    {
        [Tooltip("Upward speed given to the player. Height = v^2 / (2 * gravity): 22 -> about 4.6 tiles.")] public float bounce = 22f;
        [Tooltip("Upward speed when jump is held: 25.5 -> about 6.2 tiles.")] public float bounceHeld = 25.5f;
        [Tooltip("The player must be falling at least this fast (walking into it does nothing).")] public float minFallSpeed = 2f;
        public Sprite restSprite, squashSprite;
        public Color sporeColor = new Color(0.45f, 1f, 0.85f);

        float squashT;

        public override bool IsHazard => false;

        public override void Tick(float dt)
        {
            if (squashT > 0f)
            {
                squashT -= dt;
                if (squashT <= 0f && sr != null && restSprite != null) sr.sprite = restSprite;
            }
            var p = World.Player;
            if (p == null || p.IsDead || p.godMode) return;
            if (p.vel.y > -minFallSpeed || !Overlaps(p)) return;
            if (p.pos.y < pos.y + size.y * 0.25f) return;         // came in from the side, below the cap
            p.Bounce(p.JumpHeld ? bounceHeld : bounce);
            squashT = 0.18f;
            if (sr != null && squashSprite != null) sr.sprite = squashSprite;
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), sporeColor, 8);
            LevelLighting.Flash(Center, sporeColor, 4.5f, 1.2f, 0.3f);
            Sfx.Play("toad", pos, 0.8f, 0.7f);
        }
    }
}
