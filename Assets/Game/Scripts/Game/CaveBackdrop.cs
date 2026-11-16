using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Depth for underground areas: large rock silhouettes tiled in both directions behind the terrain, each layer
    /// sliding at its own parallax factor as the camera moves. Lives under the camera; GameDirector enables it in
    /// indoor areas (outdoor areas use SkyBackdrop).
    /// </summary>
    public class CaveBackdrop : MonoBehaviour
    {
        public Camera cam;
        public SpriteRenderer[] layers;
        [Tooltip("0 = fixed to the camera, 1 = fixed to the world.")] public float[] parallax = { 0.45f, 0.7f };

        public void SetTint(Color c)
        {
            if (layers == null) return;
            foreach (var l in layers) if (l != null) l.color = new Color(c.r, c.g, c.b, l.color.a);
        }

        void LateUpdate()
        {
            if (cam == null) cam = GetComponentInParent<Camera>();
            if (cam == null || layers == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var cp = cam.transform.position;
            transform.position = new Vector3(cp.x, cp.y, 0f);
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                if (l == null || l.sprite == null) continue;
                Vector2 tile = l.sprite.bounds.size;
                l.drawMode = SpriteDrawMode.Tiled;
                // constant size (whole tiles) so the tiling anchor never moves; shifted by less than one tile
                float sx = (Mathf.Ceil(w * 2f / tile.x) + 2f) * tile.x, sy = (Mathf.Ceil(h * 2f / tile.y) + 2f) * tile.y;
                l.size = new Vector2(sx, sy);
                float p = i < parallax.Length ? parallax[i] : 0.5f;
                float ox = Mathf.Repeat(cp.x * p, tile.x), oy = Mathf.Repeat(cp.y * p, tile.y);
                l.transform.localPosition = new Vector3(tile.x * 0.5f - ox, tile.y * 0.5f - oy, 0f);
            }
        }
    }
}
