using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A straight beam of light between two points: one stretched, unlit sprite. Whoever fires beams (the light
    /// lens, the glint) keeps one of these and calls Show / Hide; it is a picture only - what the beam does to
    /// those standing in it is the owner's business.
    /// </summary>
    public class BeamVisual : MonoBehaviour
    {
        SpriteRenderer sr;
        static Sprite white, disc;

        /// <summary>A hidden beam belonging to 'owner', drawn with 'material' (an unlit one, so it glows in the dark) just in front of it.</summary>
        public static BeamVisual Create(Entity owner, Material material)
        {
            var go = new GameObject("Beam");
            var b = go.AddComponent<BeamVisual>();
            b.sr = go.AddComponent<SpriteRenderer>();
            if (white == null) white = World.Instance != null && World.Instance.config != null ? World.Instance.config.unitSquare : null;
            b.sr.sprite = white;
            var from = owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            if (material != null) b.sr.sharedMaterial = material;
            else if (from != null) b.sr.sharedMaterial = from.sharedMaterial;
            if (from != null) { b.sr.sortingLayerID = from.sortingLayerID; b.sr.sortingOrder = from.sortingOrder + 20; }
            b.sr.enabled = false;
            return b;
        }

        public void Show(Vector2 a, Vector2 b, float width, Color color)
        {
            if (sr == null) return;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) { sr.enabled = false; return; }
            transform.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(len, width, 1f);
            sr.sprite = white;
            sr.color = color;
            sr.enabled = true;
        }

        /// <summary>Where it is drawn among the other sprites (by default just in front of its owner).</summary>
        public void SetOrder(int order) { if (sr != null) sr.sortingOrder = order; }

        /// <summary>
        /// A round spot of light instead of a line: for the end of a beam (where it strikes, where it turns, where
        /// it comes from), so that the stretched picture's square end does not show.
        /// </summary>
        public void ShowDot(Vector2 at, float diameter, Color color)
        {
            if (sr == null) return;
            if (disc == null) disc = World.Instance != null && World.Instance.config != null ? World.Instance.config.unitDisc : null;
            transform.position = new Vector3(at.x, at.y, 0f);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(diameter, diameter, 1f);
            sr.sprite = disc;
            sr.color = color;
            sr.enabled = true;
        }

        public void Hide() { if (sr != null) sr.enabled = false; }

        /// <summary>Distance from point p to the segment a-b.</summary>
        public static float DistanceTo(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
