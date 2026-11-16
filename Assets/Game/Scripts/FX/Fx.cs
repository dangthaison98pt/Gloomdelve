using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Lightweight pooled pixel particles, explosion flashes and floating text.</summary>
    public class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        public Sprite pixel;
        public Sprite[] smokeFrames;
        public SpriteAnim explosionAnim;
        public PixelFont font;
        public Material spriteMaterial;
        [Tooltip("Unlit material for things that glow: sparks, explosions, floating text.")] public Material glowMaterial;
        public int maxParticles = 500;
        public CameraFollow cameraFollow;

        struct P
        {
            public Vector2 pos, vel;
            public float life, maxLife, gravity, size;
            public Color color;
            public bool smoke;
            public bool active;
        }

        P[] ps;
        SpriteRenderer[] renderers;
        int cursor;
        readonly List<(SpriteAnimator a, float t)> booms = new List<(SpriteAnimator, float)>();
        readonly List<(PixelText t, float life, Vector2 vel)> texts = new List<(PixelText, float, Vector2)>();
        readonly Stack<SpriteAnimator> boomPool = new Stack<SpriteAnimator>();
        readonly Stack<PixelText> textPool = new Stack<PixelText>();

        void Awake()
        {
            Instance = this;
            ps = new P[maxParticles];
            renderers = new SpriteRenderer[maxParticles];
            for (int i = 0; i < maxParticles; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = pixel;
                if (spriteMaterial != null) r.sharedMaterial = spriteMaterial;
                r.sortingOrder = 40;
                r.enabled = false;
                renderers[i] = r;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Material Glow => glowMaterial != null ? glowMaterial : spriteMaterial;

        void Emit(Vector2 pos, Vector2 vel, Color c, float life, float gravity, float size, bool smoke = false, bool glow = false)
        {
            int i = cursor;
            cursor = (cursor + 1) % maxParticles;
            ps[i] = new P { pos = pos, vel = vel, life = life, maxLife = life, gravity = gravity, size = size, color = c, active = true, smoke = smoke };
            var r = renderers[i];
            r.enabled = true;
            r.sprite = smoke && smokeFrames != null && smokeFrames.Length > 0 ? smokeFrames[0] : pixel;
            r.color = c;
            var mat = glow ? Glow : spriteMaterial;
            if (mat != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
            r.transform.localScale = smoke ? Vector3.one : new Vector3(size, size, 1f);
            r.transform.position = pos;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < maxParticles; i++)
            {
                if (!ps[i].active) continue;
                ref P p = ref ps[i];
                p.life -= dt;
                var r = renderers[i];
                if (p.life <= 0f) { p.active = false; r.enabled = false; continue; }
                p.vel.y -= p.gravity * dt;
                p.vel *= p.smoke ? (1f - 2f * dt) : 1f;
                p.pos += p.vel * dt;
                float t = p.life / p.maxLife;
                if (p.smoke && smokeFrames != null && smokeFrames.Length > 0)
                    r.sprite = smokeFrames[Mathf.Clamp((int)((1f - t) * smokeFrames.Length), 0, smokeFrames.Length - 1)];
                var c = p.color; c.a *= Mathf.Clamp01(t * 2f);
                r.color = c;
                r.transform.position = p.pos;
            }

            for (int i = booms.Count - 1; i >= 0; i--)
            {
                var (a, t) = booms[i];
                t -= dt;
                if (t <= 0f) { a.gameObject.SetActive(false); boomPool.Push(a); booms.RemoveAt(i); }
                else booms[i] = (a, t);
            }

            for (int i = texts.Count - 1; i >= 0; i--)
            {
                var (pt, life, vel) = texts[i];
                life -= dt;
                if (life <= 0f) { pt.gameObject.SetActive(false); textPool.Push(pt); texts.RemoveAt(i); continue; }
                pt.transform.position += (Vector3)(vel * dt);
                pt.SetAlpha(Mathf.Clamp01(life * 2f));
                texts[i] = (pt, life, vel * (1f - dt * 2f));
            }
        }

        static float R(float a, float b) => Random.Range(a, b);

        // ---------------------------------------------------------------- static API

        public static void Dust(Vector2 pos, int n = 4)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
                Instance.Emit(pos + new Vector2(R(-0.3f, 0.3f), 0.05f), new Vector2(R(-1.5f, 1.5f), R(0.3f, 1.5f)),
                    new Color(0.75f, 0.68f, 0.58f, 0.8f), R(0.25f, 0.5f), 2f, 1f);
        }

        public static void Debris(Vector2 pos, Color c, int n = 6)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
                Instance.Emit(pos + new Vector2(R(-0.4f, 0.4f), R(-0.4f, 0.4f)), new Vector2(R(-4f, 4f), R(1f, 7f)),
                    c * R(0.8f, 1.15f), R(0.4f, 0.9f), 22f, R(1.5f, 3f));
        }

        public static void Sparks(Vector2 pos, Color c, int n = 6, float speed = 5f)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle.normalized * R(speed * 0.4f, speed);
                Instance.Emit(pos, d, c, R(0.15f, 0.4f), 6f, 1f, false, true);
            }
        }

        /// <summary>Slow glowing embers that drift up and fade (explosions, fire).</summary>
        public static void Embers(Vector2 pos, int n = 6)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
                Instance.Emit(pos + Random.insideUnitCircle * 0.6f, new Vector2(R(-1.2f, 1.2f), R(1f, 3.5f)),
                    new Color(1f, R(0.45f, 0.75f), 0.15f), R(0.6f, 1.4f), -0.8f, R(1f, 2f), false, true);
        }

        public static void Blood(Vector2 pos, Color c, int n = 8)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
                Instance.Emit(pos, new Vector2(R(-4f, 4f), R(1f, 6f)), c, R(0.3f, 0.7f), 25f, R(1f, 2f));
        }

        public static void Smoke(Vector2 pos, int n = 3)
        {
            if (Instance == null) return;
            for (int i = 0; i < n; i++)
                Instance.Emit(pos + Random.insideUnitCircle * 0.3f, new Vector2(R(-0.8f, 0.8f), R(0.5f, 1.6f)),
                    new Color(0.8f, 0.8f, 0.8f, 0.8f), R(0.5f, 0.9f), -0.5f, 1f, true);
        }

        public static void Explosion(Vector2 pos, float radius)
        {
            if (Instance == null) return;
            var inst = Instance;
            SpriteAnimator a;
            if (inst.boomPool.Count > 0) a = inst.boomPool.Pop();
            else
            {
                var go = new GameObject("boom");
                go.transform.SetParent(inst.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (inst.Glow != null) r.sharedMaterial = inst.Glow;
                r.sortingOrder = 45;
                a = go.AddComponent<SpriteAnimator>();
            }
            a.gameObject.SetActive(true);
            a.transform.position = pos;
            a.transform.localScale = Vector3.one * (radius / 2.3f);
            a.Play(inst.explosionAnim, true);
            float len = inst.explosionAnim != null ? inst.explosionAnim.frames.Length / inst.explosionAnim.fps : 0.4f;
            inst.booms.Add((a, len));
            Smoke(pos, 9);
            Sparks(pos, new Color(1f, 0.8f, 0.3f), 22, 11f);
            Embers(pos, 10);
            LevelLighting.Flash(pos, new Color(1f, 0.72f, 0.35f), radius * 3.2f, 2.4f, 0.45f);
        }

        public static void Text(Vector2 pos, string text, Color c, float life = 1.0f)
        {
            if (Instance == null || Instance.font == null) return;
            var inst = Instance;
            PixelText pt;
            if (inst.textPool.Count > 0) pt = inst.textPool.Pop();
            else
            {
                var go = new GameObject("ftext");
                go.transform.SetParent(inst.transform, false);
                pt = go.AddComponent<PixelText>();
                pt.font = inst.font;
                pt.material = inst.Glow;
                pt.sortingOrder = 70;
            }
            pt.gameObject.SetActive(true);
            pt.transform.position = pos;
            pt.Set(text, c);
            inst.texts.Add((pt, life, new Vector2(0f, 1.6f)));
        }

        public static void Shake(float amount)
        {
            if (Instance != null && Instance.cameraFollow != null && SaveSystem.Settings.screenShake)
                Instance.cameraFollow.AddShake(amount);
        }
    }
}
