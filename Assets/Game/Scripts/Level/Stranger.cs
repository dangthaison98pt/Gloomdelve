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
        float t;
        float moteT;
        Highlight halo;
        Color haloColor;

        /// <summary>The haft handed over: from here he is leaving.</summary>
        public bool Given => given;
        /// <summary>How long his going takes, from the haft handed over to nothing left of him.</summary>
        public const float HoldTime = 0.5f, FadeTime = 1.9f;

        static readonly Color Pale = new Color(0.86f, 0.78f, 1f);
        static readonly Color Mote = new Color(0.82f, 0.66f, 1f, 0.95f);

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
            t = 0f;
            halo = GetComponent<Highlight>();
            if (halo != null) haloColor = halo.glowColor;
            Story.GotPart(RunSession.Current, World != null ? World.config : null, "hammerhandle");
            // a nod, a breath of light: then he goes
            Fx.Motes(Center, Mote, 6, 0.35f, 0.6f);
            LevelLighting.Flash(Center, Mote, 3.5f, 0.8f, 0.5f);
        }

        /// <summary>
        /// His going: a moment's stillness, then he comes apart from the feet up into motes of violet light that
        /// drift up and away - growing pale and thin and rising a little as he goes - and is gone in a last soft
        /// burst of light.
        /// </summary>
        public override void Tick(float dt)
        {
            if (!given) return;
            t += dt;
            if (t < HoldTime) return;
            float k = Mathf.Clamp01((t - HoldTime) / FadeTime);
            if (sr != null)
            {
                var c = Color.Lerp(Color.white, Pale, k);
                c.a = 1f - k * k;
                sr.color = c;
                sr.transform.localScale = new Vector3(1f - 0.4f * k, 1f + 0.12f * k, 1f);
            }
            if (halo != null) { var hc = haloColor; hc.a = haloColor.a * (1f - k); halo.glowColor = hc; }
            pos.y += 0.22f * dt;            // lifted a little as he goes
            // the motes come off him from the feet up: where the picture is already gone, nothing is left to fly
            moteT -= dt;
            if (moteT <= 0f)
            {
                moteT = 0.05f;
                float h = size.y * (0.1f + 0.9f * k);
                Fx.Motes(new Vector2(pos.x, pos.y + h * 0.5f), Mote, 2, size.x * 0.45f * (1f - 0.4f * k), h * 0.5f);
            }
            if (k >= 1f)
            {
                Fx.Motes(Center, Mote, 22, 0.4f, 0.7f);
                Fx.Sparks(Center, Pale, 12, 3f);
                LevelLighting.Flash(Center, Mote, 5f, 1.4f, 0.7f);
                Sfx.Play("idol", pos, 0.6f, 0.75f);
                Despawn();
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
