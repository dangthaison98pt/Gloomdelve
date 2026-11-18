using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The elder of Brindlemoor, outside the village hall. The first time the player talks to him he hands over the
    /// Gold Pouch (a key item: see Villagers); after that he tells how many of the villagers are still lost.
    /// </summary>
    public class Elder : Entity, IInteractable
    {
        public const string ElderName = "Elder Aldous";

        public bool UpTriggers => false;

        public string GetPrompt(PlayerController p)
        {
            if (!Villagers.Enabled) return null;
            if (!Villagers.HasKey(Villagers.KeyPouch)) return $"[enter]  talk to {ElderName}";
            int lost = 0;
            foreach (var v in Villagers.All) if (!Villagers.IsRescued(v.id)) lost++;
            return lost > 0
                ? $"{ElderName}:  \"{lost} of ours went up the mountain and did not come back. Look for them off the beaten path.\""
                : $"{ElderName}:  \"Everyone is home. Brindlemoor has not been this full in years.\"";
        }

        public void Interact(PlayerController p)
        {
            if (!Villagers.Enabled || Villagers.HasKey(Villagers.KeyPouch)) return;
            Villagers.GiveKey(Villagers.KeyPouch);
            Sfx.PlayUI("buy");
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 12, 4f);
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "GOLD POUCH", new Color(1f, 0.9f, 0.5f), 1.6f);
            var dir = GameDirector.Instance;
            if (dir != null && dir.hud != null)
                dir.hud.ShowBanner("Gold Pouch", $"Gold you carry in it is never lost (it holds ${Villagers.PouchCapacity}). Spend it here in the village.", 6f);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
