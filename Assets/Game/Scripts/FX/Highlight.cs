using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Makes collectibles read as "take me" at a glance (and never as background decor):
    ///  * a soft pulsing glow behind the sprite, coloured by type,
    ///  * optional bobbing (auto-collected pickups: gold, gems, hearts...),
    ///  * optional bouncing arrow above it when the player stands close and could pick it up (DOWN + X).
    /// </summary>
    public class Highlight : MonoBehaviour
    {
        public Color glowColor = new Color(1f, 1f, 1f, 0.55f);
        public float glowScale = 1.6f;
        public bool bob;
        public bool showHint;
        public Sprite glowSprite;
        public Sprite hintSprite;
        public Material material;

        SpriteRenderer main, glow, hint;
        Entity entity;
        Item item;
        float phase, baseY, glowY;

        void Start()
        {
            entity = GetComponent<Entity>();
            item = entity as Item;
            main = GetComponentInChildren<SpriteRenderer>();
            phase = (transform.position.x * 1.7f + transform.position.y) % 6.28f;
            if (main == null) return;
            baseY = main.transform.localPosition.y;

            var g = new GameObject("Glow");
            g.transform.SetParent(transform, false);
            glow = g.AddComponent<SpriteRenderer>();
            glow.sprite = glowSprite;
            if (material != null) glow.sharedMaterial = material;
            glow.sortingOrder = main.sortingOrder - 1;
            float h = entity != null ? Mathf.Max(entity.size.x, entity.size.y) : 0.5f;
            g.transform.localScale = Vector3.one * h * glowScale;
            glowY = entity != null ? entity.size.y * 0.5f : 0.25f;
            g.transform.localPosition = new Vector3(0f, glowY, 0f);

            if (showHint && hintSprite != null)
            {
                var a = new GameObject("Hint");
                a.transform.SetParent(transform, false);
                hint = a.AddComponent<SpriteRenderer>();
                hint.sprite = hintSprite;
                if (material != null) hint.sharedMaterial = material;
                hint.sortingOrder = 65;
                hint.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (main == null || glow == null) return;
            float t = Time.time * 3f + phase;
            bool held = item != null && item.holder != null;
            glow.enabled = !held && main.enabled;
            var c = glowColor;
            c.a = glowColor.a * (0.55f + 0.45f * Mathf.Sin(t));
            glow.color = c;

            // bob only upwards from the resting position (never dips into the ground)
            float off = bob && !held ? (0.5f + 0.5f * Mathf.Sin(t * 0.8f)) * 0.12f : 0f;
            main.transform.localPosition = new Vector3(main.transform.localPosition.x, baseY + off, 0f);
            glow.transform.localPosition = new Vector3(0f, glowY + off, 0f);

            if (hint != null)
            {
                var w = World.Instance;
                var p = w != null ? w.Player : null;
                bool near = p != null && !p.IsDead && p.held == null && item != null && item.CanPickUp &&
                            Mathf.Abs(p.pos.x - entity.pos.x) < 1.3f && Mathf.Abs(p.pos.y - entity.pos.y) < 0.9f;
                hint.enabled = near;
                if (near)
                    hint.transform.localPosition = new Vector3(0f, entity.size.y + 0.45f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.15f, 0f);
            }
        }
    }
}
