using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Something in a room worth a closer look - a letter, a shelf, a hearth. A small glint marks it; standing by
    /// it offers "look at ...", and Enter shows what the player notices. The houses of Brindlemoor are full of
    /// them (VillageInterior puts them out): together they say a good deal about the people who live there, and
    /// about a winter that is not quite an ordinary winter.
    /// </summary>
    public class Curio : Entity, IInteractable
    {
        public string label = "this";
        [TextArea] public string text = "";

        float t;
        Color baseTint = Color.white;

        public bool UpTriggers => false;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            if (string.IsNullOrEmpty(sp.text)) return;
            int cut = sp.text.IndexOf('|');
            if (cut < 0) { text = sp.text; return; }
            label = sp.text.Substring(0, cut);
            text = sp.text.Substring(cut + 1);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            t = (pos.x * 0.37f) % 1f * 6f;          // the glints do not all pulse together
            if (sr != null) baseTint = sr.color;
        }

        public override void Tick(float dt)
        {
            t += dt;
            if (sr != null) sr.color = new Color(baseTint.r, baseTint.g, baseTint.b, 0.35f + 0.5f * Mathf.Abs(Mathf.Sin(t * 1.6f)));
        }

        public string GetPrompt(PlayerController p) => $"[enter]  look at {label}";

        public void Interact(PlayerController p)
        {
            var dir = GameDirector.Instance;
            if (dir == null || dir.hud == null) return;
            dir.hud.ShowMessage(text, Mathf.Clamp(3f + text.Length * 0.045f, 4f, 10f));
            Sfx.PlayUI("menu_select", 0.8f);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
