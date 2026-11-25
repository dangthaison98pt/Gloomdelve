using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A little chest in the room inside the kennel (KennelRoom puts it there). Opened (Enter), it holds the two
    /// little dolls: the player takes them out and holds them up (DollsPickup), and "It Takes Two" is earned. Once
    /// they are found it stays open and empty, with nothing more to do.
    /// </summary>
    public class DollsChest : Entity, IInteractable
    {
        public Sprite closed, open;
        [Tooltip("Its outline while the player is in reach (shut).")] public Sprite outline;
        public SpriteRenderer glow;

        public bool Opened { get; private set; }

        public override void OnSpawn()
        {
            base.OnSpawn();
            Opened = Villagers.HasKey(Villagers.KeyDolls);
            Show();
        }

        void Show() { if (sr != null) sr.sprite = Opened ? open : closed; }

        public string GetPrompt(PlayerController p) => Opened ? null : "[enter]  Open the chest";

        public void Interact(PlayerController p)
        {
            if (Opened || p == null || p.motor.mode != MotorMode.Normal || !p.motor.grounded) return;
            Opened = true;
            Show();
            Sfx.Play("creak", pos, 0.6f, 1.3f);
            Fx.Dust(new Vector2(pos.x, pos.y + 0.6f), 3);
            // the dolls, out of the chest and up over the head
            var cfg = World.config;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("dolls") : null;
            if (prefab == null) { Villagers.GiveKey(Villagers.KeyDolls); Achievements.Unlock(Achievements.ItTakesTwo); return; }
            var at = new Vector2(pos.x, pos.y + 0.5f);
            var dolls = World.Spawn(prefab, at, new SpawnPoint("dolls", new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)))) as DollsPickup;
            if (dolls != null) dolls.Interact(p);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            InReach.Show(glow, outline, this);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
