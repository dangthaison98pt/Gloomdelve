using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gloomdelve
{
    /// <summary>
    /// A small point light carried by an entity (lit bombs, flame vents, gems, the Heartstone, the shopkeeper's lamp).
    /// Add it to a prefab and tune the fields; scripts may switch it with SetOn. Visual only.
    /// </summary>
    public class GlowLight : MonoBehaviour
    {
        public Color color = new Color(1f, 0.8f, 0.5f);
        public float radius = 3f;
        public float intensity = 1f;
        [Range(0, 1)] public float flicker = 0.15f;
        [Tooltip("Slow breathing pulse instead of a nervous flicker.")] public bool pulse;
        public Vector2 offset = new Vector2(0f, 0.3f);
        public bool startOn = true;

        Light2D light2d;
        float seed;
        bool on;

        void Awake()
        {
            light2d = LevelLighting.MakeLight(transform, "GlowLight",Light2D.LightType.Point, color, intensity, radius * 0.15f, radius);
            light2d.transform.localPosition = offset;
            seed = Random.value * 50f;
            on = startOn;
            light2d.enabled = on;
        }

        public void SetOn(bool value)
        {
            if (on == value) return;
            on = value;
            if (light2d != null) light2d.enabled = value;
        }

        void LateUpdate()
        {
            if (!on || light2d == null) return;
            float n = pulse ? 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f + seed) : Mathf.PerlinNoise(Time.time * 9f + seed, seed);
            light2d.intensity = intensity * (1f - flicker + flicker * 2f * n);
        }
    }
}
