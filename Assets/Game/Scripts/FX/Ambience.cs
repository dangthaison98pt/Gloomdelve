using UnityEngine;

namespace Gloomdelve
{
    public enum AmbientParticles { None, Dust, Embers, Pollen, Spirits, Snow }

    /// <summary>
    /// Drifting atmosphere particles around the camera: dust motes in caves, rising embers in the mines, pollen
    /// outdoors, pale spirits in the vault. Chosen per area (AreaConfig.ambientParticles). Visual only.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        public static Ambience Instance { get; private set; }

        public Sprite pixel;
        [Tooltip("Lit material: motes only show where there is light.")] public Material litMaterial;
        [Tooltip("Unlit material: embers / spirits glow in the dark.")] public Material glowMaterial;
        public int poolSize = 180;

        struct Mote { public Vector2 pos, vel; public float phase, size, alpha; }
        Mote[] motes;
        SpriteRenderer[] renderers;
        AmbientParticles kind = AmbientParticles.None;
        int count;
        Color baseColor;
        Camera cam;
        bool openSky;

        void Awake()
        {
            Instance = this;
            motes = new Mote[poolSize];
            renderers = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("mote");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = pixel;
                r.sortingOrder = 30;
                r.enabled = false;
                renderers[i] = r;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Setup(AreaConfig area)
        {
            kind = area != null ? area.ambientParticles : AmbientParticles.None;
            count = kind == AmbientParticles.None ? 0 : Mathf.Clamp(area.ambientParticleCount, 0, poolSize);
            cam = Camera.main;
            // snow under the open sky catches the light of the lamps and the moon: it shows everywhere, not only where a light falls
            openSky = area != null && area.outdoor;
            bool glow = kind == AmbientParticles.Embers || kind == AmbientParticles.Spirits || (kind == AmbientParticles.Snow && openSky);
            switch (kind)
            {
                case AmbientParticles.Embers: baseColor = new Color(1f, 0.55f, 0.18f); break;
                case AmbientParticles.Pollen: baseColor = new Color(1f, 0.97f, 0.75f); break;
                case AmbientParticles.Spirits: baseColor = new Color(0.55f, 0.8f, 1f); break;
                case AmbientParticles.Snow: baseColor = new Color(0.92f, 0.97f, 1f); break;
                default: baseColor = new Color(1f, 0.96f, 0.85f); break;
            }
            for (int i = 0; i < poolSize; i++)
            {
                renderers[i].enabled = i < count;
                var m = glow ? glowMaterial : litMaterial;
                if (m != null) renderers[i].sharedMaterial = m;
                if (i < count) Respawn(i, true);
            }
        }

        Rect View()
        {
            float h = cam.orthographicSize + 1.5f, w = cam.orthographicSize * cam.aspect + 1.5f;
            Vector2 c = cam.transform.position;
            return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
        }

        void Respawn(int i, bool anywhere)
        {
            var v = View();
            ref var m = ref motes[i];
            m.phase = Random.value * 20f;
            m.size = 1f;
            switch (kind)
            {
                case AmbientParticles.Embers:
                    m.pos = new Vector2(Random.Range(v.xMin, v.xMax), anywhere ? Random.Range(v.yMin, v.yMax) : v.yMin);
                    m.vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.4f));
                    m.alpha = Random.Range(0.5f, 0.95f);
                    m.size = Random.value < 0.25f ? 2f : 1f;
                    break;
                case AmbientParticles.Pollen:
                    m.pos = new Vector2(anywhere ? Random.Range(v.xMin, v.xMax) : v.xMax, Random.Range(v.yMin, v.yMax));
                    m.vel = new Vector2(Random.Range(-0.9f, -0.3f), Random.Range(-0.25f, 0.05f));
                    m.alpha = Random.Range(0.35f, 0.75f);
                    break;
                case AmbientParticles.Snow:
                    m.pos = new Vector2(Random.Range(v.xMin, v.xMax), anywhere ? Random.Range(v.yMin, v.yMax) : v.yMax);
                    m.vel = new Vector2(Random.Range(-0.5f, 0.1f), Random.Range(-1.5f, -0.6f));
                    m.alpha = Random.Range(0.5f, 0.95f);
                    m.size = Random.value < 0.3f ? 2f : 1f;
                    if (openSky)
                    {
                        // a proper snowfall: flakes near and far, the big ones falling faster
                        float r = Random.value;
                        m.size = r < 0.12f ? 3f : r < 0.5f ? 2f : 1f;
                        m.vel = new Vector2(Random.Range(-0.7f, -0.1f), -Random.Range(0.7f, 1.3f) * (0.7f + m.size * 0.3f));
                        m.alpha = m.size >= 3f ? Random.Range(0.75f, 1f) : Random.Range(0.45f, 0.9f);
                    }
                    break;
                case AmbientParticles.Spirits:
                    m.pos = new Vector2(Random.Range(v.xMin, v.xMax), Random.Range(v.yMin, v.yMax));
                    m.vel = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.08f, 0.3f));
                    m.alpha = Random.Range(0.3f, 0.7f);
                    break;
                default:
                    m.pos = new Vector2(Random.Range(v.xMin, v.xMax), Random.Range(v.yMin, v.yMax));
                    m.vel = new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.12f, 0.06f));
                    m.alpha = Random.Range(0.25f, 0.6f);
                    break;
            }
        }

        void LateUpdate()
        {
            if (count == 0 || cam == null) return;
            float dt = Time.deltaTime, t = Time.time;
            var v = View();
            for (int i = 0; i < count; i++)
            {
                ref var m = ref motes[i];
                float sway = Mathf.Sin(t * 0.9f + m.phase) * 0.25f;
                m.pos += (m.vel + new Vector2(sway, 0f)) * dt;
                if (!v.Contains(m.pos))
                {
                    if (kind == AmbientParticles.Snow || kind == AmbientParticles.Pollen)
                    {
                        // Falling and drifting motes wrap round the view: one that leaves at the bottom or a side
                        // comes back in at the other, so the air is as full wherever the camera goes - walking
                        // along never opens up an empty stretch that has to fill from the top.
                        // (A jump of the camera further than one view is the one case that needs a fresh scatter.)
                        if (Mathf.Abs(m.pos.x - v.center.x) > v.width * 1.5f || Mathf.Abs(m.pos.y - v.center.y) > v.height * 1.5f) Respawn(i, true);
                        else
                        {
                            if (m.pos.x < v.xMin) m.pos.x += v.width; else if (m.pos.x > v.xMax) m.pos.x -= v.width;
                            if (m.pos.y < v.yMin) m.pos.y += v.height; else if (m.pos.y > v.yMax) m.pos.y -= v.height;
                        }
                    }
                    else Respawn(i, kind == AmbientParticles.Dust || kind == AmbientParticles.Spirits);
                }
                var r = renderers[i];
                r.transform.position = m.pos;
                r.transform.localScale = new Vector3(m.size, m.size, 1f);
                float tw = kind == AmbientParticles.Dust || kind == AmbientParticles.Snow ? 1f : 0.65f + 0.35f * Mathf.Sin(t * 2.3f + m.phase * 3f);
                r.color = new Color(baseColor.r, baseColor.g, baseColor.b, m.alpha * tw);
            }
        }
    }
}
