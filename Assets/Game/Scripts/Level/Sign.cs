using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Tutorial / lore sign: shows its message while you stand in front of it.</summary>
    public class Sign : Entity, IInteractable
    {
        [TextArea] public string message;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            if (!string.IsNullOrEmpty(sp.text)) message = sp.text;
        }

        public string GetPrompt(PlayerController p) => message;
        public void Interact(PlayerController p) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
