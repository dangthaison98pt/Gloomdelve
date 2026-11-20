using UnityEngine;

namespace Gloomdelve
{
    public class Door : Entity, IInteractable
    {
        public bool isExit = true;
        public Sprite openSprite;
        public Sprite lockedSprite;
        [Tooltip("Stone gatehouse drawn behind the door when it stands in the open (outdoor surface), so it never looks like a door in mid-air.")]
        public Sprite housingSprite;
        [System.NonSerialized] public bool locked;
        /// <summary>Prompt shown while locked (set by whoever locks it).</summary>
        [System.NonSerialized] public string lockedPrompt;

        public override void OnSpawn()
        {
            base.OnSpawn();
            Refresh();
            var lr = World != null ? World.levelRenderer : null;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.RoundToInt(pos.y);
            if (housingSprite != null && sr != null && lr != null && !lr.HasBackWall(cx, cy) && !lr.HasBackWall(cx, cy + 1))
            {
                var go = new GameObject("Housing");
                go.transform.SetParent(transform, false);
                var h = go.AddComponent<SpriteRenderer>();
                h.sprite = housingSprite;
                h.sharedMaterial = sr.sharedMaterial;
                h.sortingLayerID = sr.sortingLayerID;
                h.sortingOrder = sr.sortingOrder - 1;
            }
        }

        public void SetLocked(bool l) { locked = l; Refresh(); }

        void Refresh()
        {
            if (sr != null && isExit) sr.sprite = locked && lockedSprite != null ? lockedSprite : openSprite;
        }

        public string GetPrompt(PlayerController p)
        {
            if (!isExit) return null;
            if (locked) return string.IsNullOrEmpty(lockedPrompt) ? "The gate is sealed. Bring the Sunstone here!" : lockedPrompt;
            return "[enter]  Enter";
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
