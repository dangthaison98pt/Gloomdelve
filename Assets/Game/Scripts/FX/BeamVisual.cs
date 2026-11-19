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
            if (white == null)
                white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                                      new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
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
            if (disc == null)
            {
                const int n = 16;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                        px[y * n + x] = dx * dx + dy * dy <= n * n * 0.25f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                    }
                tex.SetPixels32(px); tex.Apply();
                disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
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
