using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Outdoor sky: a gradient that always fills the view, mountain/hill silhouettes with parallax and drifting clouds.
    /// Lives under the camera; GameDirector enables it only in outdoor areas.
    /// </summary>
    public class SkyBackdrop : MonoBehaviour
    {
        public Camera cam;
        public SpriteRenderer gradient;
        public SpriteRenderer[] layers;
        [Tooltip("0 = fixed to the camera, 1 = fixed to the world.")] public float[] parallax = { 0.1f, 0.25f };
        public float[] layerHeight = { -1.5f, -3.2f };
        [Tooltip("Sun disc: stays at a fixed spot of the view.")] public SpriteRenderer sun;
        public Vector2 sunViewPos = new Vector2(0.55f, 0.5f);
        public SpriteRenderer[] clouds;
        public float cloudSpeed = 0.35f;

        float levelMidY;

        [Tooltip("Night skies: the moon that takes the sun's place, and the stars (a picture that fills the view).")]
        public Sprite moonSprite;
        public SpriteRenderer stars;
        Sprite sunSprite;

        /// <summary>Lays a colour over the whole backdrop (white = as drawn). A dark colour makes it night: the sun
        /// gives way to the moon and the stars come out.</summary>
        public void SetTint(Color c)
        {
            bool night = c.r + c.g + c.b < 1.8f;
            if (gradient != null) gradient.color = c;
            if (sun != null)
            {
                if (sunSprite == null) sunSprite = sun.sprite;
                sun.sprite = night && moonSprite != null ? moonSprite : sunSprite;
                sun.color = night ? new Color(0.93f, 0.95f, 1f) : c;
            }
            if (stars != null) stars.gameObject.SetActive(night);
            if (layers != null) foreach (var l in layers) if (l != null) l.color = c;
            if (clouds != null) foreach (var l in clouds) if (l != null) l.color = c;
        }
        float[] cloudX;

        public void Setup(float levelHeight) { levelMidY = levelHeight * 0.5f; }

        void Start()
        {
            cloudX = new float[clouds != null ? clouds.Length : 0];
            for (int i = 0; i < cloudX.Length; i++) cloudX[i] = i * 9f + Random.value * 4f;
        }

        void LateUpdate()
        {
            if (cam == null) cam = GetComponentInParent<Camera>();
            if (cam == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var cp = cam.transform.position;
            transform.position = new Vector3(cp.x, cp.y, 0f);

            if (gradient != null)
            {
                gradient.drawMode = SpriteDrawMode.Sliced;
                gradient.size = new Vector2(w * 2f + 2f, h * 2f + 2f);
                gradient.transform.localPosition = Vector3.zero;
            }

            if (stars != null) stars.transform.localPosition = Vector3.zero;
            float dy = (cp.y - levelMidY) * 0.12f;
            if (sun != null) sun.transform.localPosition = new Vector3(w * sunViewPos.x, h * sunViewPos.y - dy * 0.3f, 0f);
            for (int i = 0; layers != null && i < layers.Length; i++)
            {
                var l = layers[i];
                if (l == null || l.sprite == null) continue;
                float tileW = l.sprite.bounds.size.x;
                l.drawMode = SpriteDrawMode.Tiled;
                l.size = new Vector2(w * 2f + tileW * 2f, l.sprite.bounds.size.y);
                float p = i < parallax.Length ? parallax[i] : 0.2f;
                float off = Mathf.Repeat(cp.x * p, tileW);
                float y = (i < layerHeight.Length ? layerHeight[i] : -2f) - dy * (1f + i);
                l.transform.localPosition = new Vector3(-off, y, 0f);
            }

            for (int i = 0; clouds != null && i < clouds.Length; i++)
            {
                var c = clouds[i];
                if (c == null) continue;
                cloudX[i] += cloudSpeed * (1f + i * 0.3f) * Time.deltaTime;
                float span = w * 2f + 8f;
                float x = Mathf.Repeat(cloudX[i] - cp.x * 0.05f, span) - span * 0.5f;
                c.transform.localPosition = new Vector3(x, h * (0.25f + 0.18f * (i % 3)) - dy * 0.5f, 0f);
            }
        }
    }
}
