using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Smooth follow with look-ahead, look up/down, level bounds clamping and trauma-based shake.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public float smoothTime = 0.12f;
        public float lookAhead = 1.2f;
        public float verticalBias = 0.6f;

        Camera cam;
        PlayerController target;
        Rect bounds;
        Vector2 velocity;
        Vector2 current;
        float trauma;
        float lookAheadX;
        bool hasBounds;
        float baseSize;

        void Awake() { cam = GetComponent<Camera>(); }

        public void SetTarget(PlayerController p, Rect levelBounds, float orthoSize)
        {
            target = p;
            bounds = levelBounds;
            hasBounds = true;
            cam.orthographicSize = orthoSize;
            baseSize = orthoSize;
            current = p != null ? p.Center : levelBounds.center;
            Snap();
        }

        /// <summary>Keeps the camera to another part of the level from now on (a room of a house, the street) and jumps there.</summary>
        /// <param name="zoomOut">1 = the usual view; above 1 shows more (a house seen whole, both storeys).</param>
        public void SetBounds(Rect r, float zoomOut = 1f)
        {
            if (baseSize > 0f) cam.orthographicSize = baseSize * Mathf.Max(0.5f, zoomOut);
            bounds = r;
            hasBounds = true;
            if (target != null) current = target.Center;
            velocity = Vector2.zero;
            lookAheadX = 0f;
            Snap();
        }

        public void AddShake(float amount) { trauma = Mathf.Clamp01(trauma + amount); }

        void Snap()
        {
            var p = Clamp(current);
            transform.position = new Vector3(p.x, p.y, -10f);
        }

        Vector2 Clamp(Vector2 p)
        {
            if (!hasBounds) return p;
            float h = cam.orthographicSize, w = h * cam.aspect;
            float minX = bounds.xMin + w, maxX = bounds.xMax - w;
            float minY = bounds.yMin + h, maxY = bounds.yMax - h;
            p.x = minX > maxX ? bounds.center.x : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? bounds.center.y : Mathf.Clamp(p.y, minY, maxY);
            return p;
        }

        void LateUpdate()
        {
            if (target != null)
            {
                Vector2 goal = (Vector2)target.transform.position + new Vector2(0f, verticalBias);
                float wantAhead = Mathf.Abs(target.vel.x) > 1f ? Mathf.Sign(target.vel.x) * lookAhead : lookAheadX * 0.98f;
                lookAheadX = Mathf.MoveTowards(lookAheadX, wantAhead, Time.deltaTime * 3f);
                goal.x += lookAheadX;
                goal.y += target.LookOffset;
                current = Vector2.SmoothDamp(current, goal, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime > 0 ? Time.deltaTime : 0f);
            }
            Vector2 p = Clamp(current);
            if (trauma > 0f)
            {
                float s = trauma * trauma * 0.6f;
                float t = Time.unscaledTime * 40f;
                p += new Vector2((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * s);
                trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.6f);
            }
            transform.position = new Vector3(p.x, p.y, -10f);
        }
    }
}
