using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A villager of Brindlemoor in a cage, somewhere off the main path of a level. Standing by the cage and
    /// pressing Enter frees them: they go home to the village for good (see Villagers) and help every later run.
    /// Who it is depends on who is still lost; when everyone is home the cage is simply not there.
    /// </summary>
    public class Captive : Entity, IInteractable
    {
        [Tooltip("One picture per villager, in the order of Villagers.All.")] public Sprite[] looks;
        [Tooltip("The cage drawn over the villager (a child object).")] public SpriteRenderer cage;

        VillagerDef who;
        bool freed;
        float leaveTimer;

        public bool UpTriggers => false;
        public VillagerDef Who => who;
        public bool Freed => freed;

        public override void OnSpawn()
        {
            base.OnSpawn();
            var dir = GameDirector.Instance;
            int seed = dir != null && dir.Level != null ? dir.Level.seed : 0;
            var session = RunSession.Current;
            who = Villagers.Enabled && (session == null || !session.villagerFreed) ? Villagers.NextCaptive(seed) : null;
            if (who == null) { Despawn(); return; }
            int i = Villagers.IndexOf(who.id);
            if (sr != null && looks != null && i >= 0 && i < looks.Length && looks[i] != null) sr.sprite = looks[i];
        }

        public string GetPrompt(PlayerController p)
        {
            if (who == null || freed) return null;
            return $"[enter]  free {who.name} {who.role}      \"{who.cagedLine}\"";
        }

        public void Interact(PlayerController p)
        {
            if (who == null || freed) return;
            freed = true;
            leaveTimer = 1.6f;
            Villagers.Rescue(who.id);
            var session = RunSession.Current;
            if (session != null) { session.villagerFreed = true; SaveSystem.CheckpointFlags(session); }      // one villager a run, even if the level is entered again
            if (cage != null) cage.enabled = false;
            Fx.Debris(Center, new Color(0.6f, 0.6f, 0.65f), 12);
            Fx.Shake(0.25f);
            Sfx.PlayUI("buy");
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), "THANK YOU!", new Color(1f, 0.92f, 0.6f), 1.4f);
            var dir = GameDirector.Instance;
            if (dir != null && dir.hud != null)
                dir.hud.ShowBanner($"{who.name} {who.role} is free", $"{who.place}. Visit Brindlemoor.", 4.5f);
        }

        public override void Tick(float dt)
        {
            if (!freed) return;
            leaveTimer -= dt;
            if (leaveTimer <= 0f)
            {
                Fx.Dust(pos, 8);        // gone down the mountain, home
                Despawn();
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
