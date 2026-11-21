using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The keeper of one of Brindlemoor's shops, inside their house (see VillageInterior): they stand behind the
    /// counter. Standing by them the prompt offers a word with them or their shop (IInteractChoices: no screen
    /// opens to choose); the shop is a screen with their wares for this run, priced in embers (VillageShopPanel;
    /// see Villagers). Nothing is laid out on the floor: that is how shops look on the mountain.
    /// Only there once the player has freed them. Which villager it is comes from the number of the building.
    /// </summary>
    public class VillagerHome : Entity, IInteractChoices, IQuestTarget
    {
        [Tooltip("One picture per villager, in the order of Villagers.All.")] public Sprite[] looks;

        int index;
        VillagerDef who;
        bool home;

        public bool Home => home;
        public string QuestId => who != null ? who.id : "";
        public VillagerDef Who => who;
        public int WareCount { get; private set; }

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            index = sp.param;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            who = index >= 0 && index < Villagers.All.Length ? Villagers.All[index] : null;
            home = who != null && Villagers.IsRescued(who.id);
            if (!home) { Despawn(); return; }       // nobody keeps the place while they are lost
            if (sr != null && looks != null && index < looks.Length && looks[index] != null) sr.sprite = looks[index];
            SetOutWares();
        }

        /// <summary>Their wares for this run (counted here; shown when they are spoken to).</summary>
        void SetOutWares()
        {
            WareCount = VillageShopPanel.WaresOf(who, World.config, RunSession.Current).Count;
        }

        public string GetPrompt(PlayerController p)
        {
            if (who == null) return null;
            return home ? $"[enter]  {who.name}" : null;
        }

        /// <summary>They speak when spoken to, not at whoever walks past.</summary>
        public void Interact(PlayerController p)
        {
            var dir = GameDirector.Instance;
            if (who == null || !home || dir == null || dir.hud == null) return;
            // (a quest may have something for them to say instead of their usual line)
            if (QuestLog.TalkStep(QuestId, out var step)) dir.hud.ShowTalk($"{who.name} {who.role}", step.lines, 0f, () => QuestLog.Notify(QuestTrigger.Talk, QuestId));
            else dir.hud.ShowTalk($"{who.name} {who.role}", who.homeLine, 0f, () => QuestLog.Notify(QuestTrigger.Talk, QuestId));
            Sfx.PlayUI("menu_select", 0.8f);
        }
        /// <summary>A word with them, or their shop - unless a quest has something for them to say: then that comes first.</summary>
        public string[] Choices(PlayerController p)
        {
            if (who == null || !home || QuestLog.TalkStep(QuestId, out _)) return null;
            var dir = GameDirector.Instance;
            if (dir == null || dir.villageShop == null || WareCount == 0) return null;
            return new[] { "Talk", "Shop" };
        }

        public string ChoiceTitle => who != null ? $"{who.name} {who.role}" : "";

        public void Choose(PlayerController p, int index)
        {
            var dir = GameDirector.Instance;
            if (index == 1 && dir != null && dir.villageShop != null)
            {
                Sfx.PlayUI("menu_select", 0.8f);
                dir.villageShop.Open($"{who.name} {who.role}", "For this climb. Paid in embers.", VillageShopPanel.WaresOf(who, World.config, RunSession.Current));
                return;
            }
            Interact(p);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
