using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The elder of Brindlemoor, in the village hall. He has no business of his own in the code: when an open quest
    /// wants him spoken to (QuestLog.TalkStep) he says what that quest has him say, and the quest moves on when
    /// he has been heard out - the first of them ends with the Ember Pouch. Otherwise he tells how many of the
    /// villagers are still lost.
    /// </summary>
    public class Elder : Entity, IInteractable, IQuestTarget
    {
        public const string ElderName = "Elder Aldous";
        public string QuestId => "elder";

        public string GetPrompt(PlayerController p)
        {
            if (!Villagers.Enabled) return null;
            return $"[enter]  {ElderName}";
        }

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

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
