using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The keeper of one of Brindlemoor's shops, inside their house (see VillageInterior): they stand behind the
    /// counter with their wares set out beside them (bought with the gold the player carries; see Villagers).
    /// Only there once the player has freed them. Which villager it is comes from the number of the building.
    /// </summary>
    public class VillagerHome : Entity, IInteractable
    {
        [Tooltip("One picture per villager, in the order of Villagers.All.")] public Sprite[] looks;

        int index;
        VillagerDef who;
        bool home;

        public bool Home => home;
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

        /// <summary>Their wares for this run, on the ground to the right of where they stand.</summary>
        void SetOutWares()
        {
            var cfg = World.config;
            var run = RunSession.Current;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("shopitem") : null;
            if (cfg == null || cfg.shop == null || prefab == null) return;
            var wares = Villagers.WaresOf(who, cfg.shop, run != null ? run.seed : 0);
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            for (int i = 0; i < wares.Count; i++)
            {
                var cell = new Vector2Int(cx + 2 + i, cy);
                var item = World.Spawn(prefab, new Vector2(cell.x + 0.5f, cell.y), new SpawnPoint("shopitem", cell, 0, i, wares[i].id)) as ShopItem;
                if (item == null) continue;
                item.SetVillageWare(Villagers.EmberPrice(wares[i], who));
                WareCount++;
            }
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
            dir.hud.ShowTalk($"{who.name} {who.role}", who.homeLine, HUD.ReadTime(who.homeLine));
            Sfx.PlayUI("menu_select", 0.8f);
        }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
