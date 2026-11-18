using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Smoke from a chimney: a few soft puffs that rise, lean with the wind, grow and fade, one after another
    /// without end. Put it where the smoke should come out; switch it with SetOn (a house whose fire is lit).
    /// Visual only.
    /// </summary>
    public class ChimneySmoke : MonoBehaviour
    {
        public Sprite[] frames;
        public Material material;
        public int puffs = 7;
        [Tooltip("Seconds a puff lives.")] public float life = 5f;
        [Tooltip("How fast it rises and how far the wind carries it, in tiles per second.")] public float rise = 0.62f, drift = -0.26f;
        public Color color = new Color(0.74f, 0.78f, 0.9f, 0.6f);
        public int sortingOrder = 2;

        SpriteRenderer[] rs;
        float[] age;
        bool on = true;

        void Awake()
        {
            rs = new SpriteRenderer[Mathf.Max(1, puffs)];
            age = new float[rs.Length];
            for (int i = 0; i < rs.Length; i++)
            {
                var go = new GameObject("puff");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (frames != null && frames.Length > 0) r.sprite = frames[0];
                if (material != null) r.sharedMaterial = material;
                r.sortingOrder = sortingOrder;
                rs[i] = r;
                age[i] = life * i / rs.Length;        // already spread out along the plume on the first frame
            }
            Apply();
        }

        public void SetOn(bool value)
        {
            on = value;
            if (rs != null) foreach (var r in rs) if (r != null) r.enabled = on;
        }

        void Update()
        {
            if (!on || rs == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < rs.Length; i++)
            {
                age[i] += dt;
                if (age[i] > life) age[i] -= life;
            }
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < rs.Length; i++)
            {
                float a = age[i], t = a / life;
                var r = rs[i];
                r.transform.localPosition = new Vector3(drift * a * (0.6f + t) + Mathf.Sin(a * 1.3f + i * 2.1f) * 0.1f, rise * a, 0f);
                float s = 1.3f + t * 2.6f;        // the puff picture is half a tile: it opens out to about two tiles
                r.transform.localScale = new Vector3(s, s, 1f);
                var c = color;
                c.a *= Mathf.Clamp01(t * 7f) * (1f - t);      // quickly there, slowly gone
                r.color = c;
                if (frames != null && frames.Length > 1) r.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * frames.Length))];
            }
        }
    }
}
