using UnityEngine;

namespace Gloomdelve
{
    public class Door : Entity, IInteractable
    {
        public bool isExit = true;
        public Sprite openSprite;
        public Sprite lockedSprite;
        [System.NonSerialized] public bool locked;

        public bool UpTriggers => isExit;

        public override void OnSpawn()
        {
            base.OnSpawn();
            Refresh();
        }

        public void SetLocked(bool l) { locked = l; Refresh(); }

        void Refresh()
        {
            if (sr != null && isExit) sr.sprite = locked && lockedSprite != null ? lockedSprite : openSprite;
        }

        public string GetPrompt(PlayerController p)
        {
            if (!isExit) return null;
            if (locked) return "The gate is sealed. Bring the Heartstone here!";
            return "UP / A : enter the exit";
        }

        public void Interact(PlayerController p)
        {
            if (!isExit) return;
            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerEnterExit(p, this);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
