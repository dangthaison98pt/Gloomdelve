using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The elder of Brindlemoor, in the village hall. He has no business of his own in the code: when an open quest
    /// wants him spoken to (QuestLog.TalkStep) he says what that quest has him say, and the quest moves on when
    /// he has been heard out - the first of them ends with the Ember Pouch. Otherwise he tells how many of the
    /// villagers are still lost.
    /// Once the pouch is handed over he also makes bags larger (Bags): the prompt beside him then offers a word
    /// with him or "Upgrade" (IInteractChoices), and that opens a screen with the four of them - bomb bag,
    /// rope bag, pebble bag, Ember Pouch - each at the price of its next enlarging.
    /// </summary>
    public class Elder : Entity, IInteractChoices, IQuestTarget
    {
        /// <summary>He looks at the player when they are near (VillagerHome.FacePlayer).</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            VillagerHome.FacePlayer(this, sr);
        }

        public const string ElderName = "Elder Aldous";
        public string QuestId => "elder";

        public string GetPrompt(PlayerController p)
        {
            if (!Villagers.Enabled) return null;
            return $"[enter]  {ElderName}";
        }

        /// <summary>What he says over his work on the bags.</summary>
        public const string BagLine = "Bring me [ember] and I will let your bags out. Five more each time, and it stays so.";

        /// <summary>What he has to say once the pouch is handed over (he says it when spoken to).</summary>
        public static string Line()
        {
            int lost = 0;
            foreach (var v in Villagers.All) if (!Villagers.IsRescued(v.id)) lost++;
            return lost > 0
                ? $"{lost} of ours went up the mountain and did not come back. Look for them off the beaten path."
                : "Everyone is home. Brindlemoor has not been this full in years.";
        }

        public void Interact(PlayerController p)
        {
            if (!Villagers.Enabled) return;
            var dir = GameDirector.Instance;
            var hud = dir != null ? dir.hud : null;
            Sfx.PlayUI("menu_select", 0.8f);
            // a quest has something for him to say: it is done with this step once he has been heard to the end
            if (QuestLog.TalkStep(QuestId, out var step))
            {
                if (hud != null) hud.ShowTalk(ElderName, step.lines, 0f, () => QuestLog.Notify(QuestTrigger.Talk, QuestId));
                else QuestLog.Notify(QuestTrigger.Talk, QuestId);
                return;
            }
            if (hud != null) hud.ShowTalk(ElderName, Line());
        }

        /// <summary>A word with him, or his work on the bags - once there is a pouch to pay him from, and no quest has him speak first.</summary>
        public string[] Choices(PlayerController p)
        {
            if (!Villagers.Enabled || !Villagers.HasKey(Villagers.KeyPouch) || QuestLog.TalkStep(QuestId, out _)) return null;
            var dir = GameDirector.Instance;
            if (dir == null || dir.villageShop == null) return null;
            return new[] { "Talk", "Upgrade" };
        }

        public string ChoiceTitle => ElderName;

        public void Choose(PlayerController p, int index)
        {
            var dir = GameDirector.Instance;
            if (index == 1 && dir != null && dir.villageShop != null)
            {
                Sfx.PlayUI("menu_select", 0.8f);
                dir.villageShop.Open(ElderName, BagLine, VillageShopPanel.BagWork(World.config));
                return;
            }
            Interact(p);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
