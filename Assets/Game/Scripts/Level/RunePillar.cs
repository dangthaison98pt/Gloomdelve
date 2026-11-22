using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A stone pillar on the mountain, carved with signs older than the village (Story). Nobody can read them;
    /// with a sheet of paper in the bag they can be copied, and the copy carried home to the elder. Once he has
    /// read it, a pillar looked at says what the signs say.
    /// </summary>
    public class RunePillar : Entity, IInteractable, IQuestTarget
    {
        [Tooltip("Picture of the copy of the signs (its item card, and the bag).")] public Sprite rubbingIcon;

        public string QuestId => "pillar";

        public const string RubbingName = "Copy of the Old Signs";
        public const string CannotRead = "Signs cut deep into the stone, older than Brindlemoor. Brann cannot read them - and has nothing to copy them onto.";
        public const string SameSigns = "The same signs as on the copy. Somebody in Brindlemoor may know what they say.";

        static int Sheets => RunSession.Current != null ? RunSession.Current.GetResource(Story.Paper) : 0;

        public string GetPrompt(PlayerController p)
        {
            if (Story.Decoded) return "[enter]  Read";
            if (!Story.HasRubbing && Sheets > 0) return "[enter]  Copy the signs";
            return "[enter]  Look";
        }

        public void Interact(PlayerController p)
        {
            var dir = GameDirector.Instance;
            var hud = dir != null ? dir.hud : null;
            var run = RunSession.Current;
            Sfx.PlayUI("menu_select", 0.8f);
            // the first pillar looked at is where the task begins
            QuestLog.Start(Story.QuestSigns);
            if (Story.Decoded)
            {
                if (hud != null) hud.ShowNote(Story.Reading, HUD.ReadTime(Story.Reading));
                return;
            }
            if (Story.HasRubbing)
            {
                if (hud != null) hud.ShowNote(SameSigns, HUD.ReadTime(SameSigns));
                return;
            }
            if (run != null && Sheets > 0)
            {
                QuestLog.Notify(QuestTrigger.Obtain, Story.Paper);      // (paper bought before the task was known still counts)
                run.AddResource(Story.Paper, -1);
                Villagers.GiveKey(Story.KeyRubbing);
                Fx.Sparks(Center, new Color(0.6f, 0.9f, 1f), 10, 3f);
                Sfx.PlayUI("idol");
                if (hud != null)
                    hud.Gain(Story.KeyRubbing, RubbingName, rubbingIcon, 1,
                        "The signs of the pillar, rubbed onto paper. It is never lost. Elder Aldous has read older things than this: take it to him.", true);
                return;
            }
            if (hud != null) hud.ShowNote(CannotRead, HUD.ReadTime(CannotRead));
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
