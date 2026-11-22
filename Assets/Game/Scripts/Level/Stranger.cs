using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The stranger on the mountain (Story): he looks like a trader and sells nothing. Spoken to and heard out, he
    /// hands over the haft of the hammer - and is gone. He is only there once the old signs have been read.
    /// </summary>
    public class Stranger : Entity, IInteractable
    {
        [Tooltip("Picture of the hammer's haft (its item card).")] public Sprite handleIcon;

        public const string Name = "A Stranger";
        public const string Lines =
            "No wares. I sell nothing, and you carry nothing I want. " +
            "But you have walked this way before, have you not. Many times. So have I. " +
            "Take this. It was never mine to keep - only to hold until somebody read the stones.";

        bool given;
        float leave;

        public bool Given => given;

        public string GetPrompt(PlayerController p) => given ? null : "[enter]  Talk";

        public void Interact(PlayerController p)
        {
            if (given) return;
            var dir = GameDirector.Instance;
            var hud = dir != null ? dir.hud : null;
            Sfx.PlayUI("menu_select", 0.8f);
            if (hud != null) hud.ShowTalk(Name, Lines, 0f, Give);
            else Give();
        }

        void Give()
        {
            if (given) return;
            given = true;
            leave = 2.4f;
            Story.GotPart(RunSession.Current, World != null ? World.config : null, "hammerhandle");
        }

        public override void Tick(float dt)
        {
            if (!given) return;
            leave -= dt;
            if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01(leave / 1.2f); sr.color = c; }
            if (leave <= 0f) Despawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
