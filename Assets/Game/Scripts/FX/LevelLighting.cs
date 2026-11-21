using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gloomdelve
{
    /// <summary>
    /// 2D lighting of a level (URP 2D renderer): one global ambient light per area, a warm light that follows the
    /// player, flickering wall torches placed deterministically from the level layout, and short light flashes
    /// (explosions). Purely visual - nothing here touches the simulation.
    /// Per-area look: AreaConfig > Lighting &amp; atmosphere.
    /// </summary>
    public class LevelLighting : MonoBehaviour
    {
        public static LevelLighting Instance { get; private set; }

        [Tooltip("Flame animation frames of a wall torch.")] public Sprite[] torchFrames;
        public Sprite torchBracket;
        [Tooltip("Unlit material (flames are not darkened by the ambient light).")] public Material glowMaterial;
        public Material litMaterial;
        public float torchRadius = 6f;
        public float torchIntensity = 0.95f;
        public int minTorchSpacing = 7;

        Light2D global, playerLight;
        float playerBase, playerRadius;
        // light drunk by gloom moths: the smallest factor asked for this frame, and the smoothed value in use
        float dimWanted = 1f, dim = 1f;
        int dimFrame = -1;
        /// <summary>Current size of the player's light relative to normal (1 = untouched).</summary>
        public static float PlayerLightScale => Instance != null ? Instance.dim : 1f;
        /// <summary>Reach of the player's light right now (tiles), with equipment and moths counted in.</summary>
        public static float PlayerLightRadius => Instance != null && Instance.playerLight != null ? Instance.playerLight.pointLightOuterRadius : 0f;

        /// <summary>
        /// Shrinks the light the player carries to this fraction for as long as it keeps being called (every
        /// simulation step); it grows back by itself a moment after the calls stop. Visual only.
        /// </summary>
        public static void DimPlayerLight(float fraction)
        {
            var inst = Instance;
            if (inst == null) return;
            inst.dimWanted = Mathf.Min(inst.dimWanted, Mathf.Clamp(fraction, 0.1f, 1f));
            inst.dimFrame = Time.frameCount;
        }
        Transform torchRoot;

        struct Torch { public Light2D light; public SpriteRenderer flame; public float seed; }
        readonly List<Torch> torches = new List<Torch>();

        struct FlashLight { public Light2D light; public float life, maxLife, intensity; }
        FlashLight[] flashes;
        int flashCursor;

        void Awake()
        {
            Instance = this;
            global = MakeLight(transform, "GlobalLight", Light2D.LightType.Global, Color.white, 1f, 0f, 0f);
            playerLight = MakeLight(transform, "PlayerLight", Light2D.LightType.Point, new Color(1f, 0.9f, 0.75f), 0.8f, 1.2f, 7.5f);
            flashes = new FlashLight[6];
            for (int i = 0; i < flashes.Length; i++)
            {
                flashes[i].light = MakeLight(transform, "Flash" + i, Light2D.LightType.Point, Color.white, 0f, 0.5f, 6f);
                flashes[i].light.enabled = false;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static Light2D MakeLight(Transform parent, string name, Light2D.LightType type, Color color, float intensity, float inner, float outer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = type;
            l.color = color;
            l.intensity = intensity;
            l.shadowsEnabled = false;
            if (type == Light2D.LightType.Point)
            {
                l.pointLightInnerRadius = inner;
                l.pointLightOuterRadius = outer;
                l.falloffIntensity = 0.55f;
            }
            return l;
        }

        float ambientBase = 0.6f;

        /// <summary>Turns the level's own light down (0..1 of what the area gives it; 1 = as it was). The Eclipse King's last third.</summary>
        public static void SetAmbientScale(float k)
        {
            var inst = Instance;
            if (inst == null || inst.global == null) return;
            inst.global.intensity = inst.ambientBase * Mathf.Clamp01(k);
        }

        /// <summary>Applies the area's ambient light and places torches for this level.</summary>
        public void Setup(LevelData level, AreaConfig area)
        {
            float ambient = area != null && area.ambientIntensity >= 0f ? area.ambientIntensity : 0.6f;
            global.color = area != null ? area.ambientColor : Color.white;
            global.intensity = ambient;
            ambientBase = ambient;
            playerBase = area != null ? area.playerLightIntensity : 0.8f;
            playerRadius = area != null ? area.playerLightRadius : 7.5f;
            playerLight.pointLightOuterRadius = playerRadius;
            dim = dimWanted = 1f;
            playerLight.intensity = playerBase;
            playerLight.enabled = playerBase > 0.01f;

            if (torchRoot != null) Destroy(torchRoot.gameObject);
            torches.Clear();
            torchRoot = new GameObject("Torches").transform;
            torchRoot.SetParent(transform, false);
            if (area == null || area.torchChance <= 0f || torchFrames == null || torchFrames.Length == 0) return;

            var g = level.grid;
            var placed = new List<Vector2Int>();
            for (int y = 2; y < g.Height - 3; y++)
                for (int x = 2; x < g.Width - 2; x++)
                {
                    // a wall torch hangs two cells above a floor, with free air around it
                    if (!g.IsSolid(x, y - 1) || !g.IsEmpty(x, y) || !g.IsEmpty(x, y + 1)) continue;
                    if ((Rng.Hash(x * 31 + 7, y * 17 + level.seed) % 1000) / 1000f > area.torchChance) continue;
                    bool near = false;
                    foreach (var p in placed)
                        if (Mathf.Abs(p.x - x) + Mathf.Abs(p.y - y) < minTorchSpacing) { near = true; break; }
                    if (near) continue;
                    placed.Add(new Vector2Int(x, y));
                    AddTorch(new Vector2(x + 0.5f, y + 1.35f), area.torchColor, x * 0.37f + y * 1.3f);
                }
        }

        void AddTorch(Vector2 pos, Color color, float seed)
        {
            var go = new GameObject("Torch");
            go.transform.SetParent(torchRoot, false);
            go.transform.position = pos;
            if (torchBracket != null)
            {
                var b = go.AddComponent<SpriteRenderer>();
                b.sprite = torchBracket; b.sortingOrder = -6;
                if (litMaterial != null) b.sharedMaterial = litMaterial;
            }
            var fgo = new GameObject("Flame");
            fgo.transform.SetParent(go.transform, false);
            // The bracket's pivot is 9 px up its 12 px sprite and the cup rim ends 2 px above that:
            // the flame (bottom pivot) starts 1 px inside the cup so it never floats.
            fgo.transform.localPosition = new Vector3(0f, 1f / 16f, 0f);
            var f = fgo.AddComponent<SpriteRenderer>();
            f.sprite = torchFrames[0]; f.sortingOrder = -5;
            if (glowMaterial != null) f.sharedMaterial = glowMaterial;
            var l = MakeLight(go.transform, "Light", Light2D.LightType.Point, color, torchIntensity, 0.6f, torchRadius);
            l.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            torches.Add(new Torch { light = l, flame = f, seed = seed });
        }

        /// <summary>A short burst of light (explosions, pickups).</summary>
        public static void Flash(Vector2 pos, Color color, float radius, float intensity, float duration)
        {
            var inst = Instance;
            if (inst == null) return;
            ref var f = ref inst.flashes[inst.flashCursor];
            inst.flashCursor = (inst.flashCursor + 1) % inst.flashes.Length;
            f.life = f.maxLife = Mathf.Max(0.05f, duration);
            f.intensity = intensity;
            f.light.color = color;
            f.light.pointLightOuterRadius = radius;
            f.light.pointLightInnerRadius = radius * 0.2f;
            f.light.intensity = intensity;
            f.light.transform.position = pos;
            f.light.enabled = true;
        }

        void LateUpdate()
        {
            float t = Time.time;
            var dir = GameDirector.Instance;
            var player = dir != null ? dir.Player : null;
            if (player != null && playerLight.enabled)
            {
                playerLight.transform.position = (Vector2)player.transform.position + new Vector2(0f, 0.6f);
                if (Time.frameCount - dimFrame > 6) dimWanted = 1f;        // nobody is drinking the light any more
                dim = Mathf.MoveTowards(dim, dimWanted, Time.deltaTime * (dimWanted < dim ? 1.6f : 0.8f));
                var run = RunSession.Current;
                float reach = 1f + (run != null ? Mathf.Max(0f, run.stats.lightBonus) : 0f);      // a lantern carries further
                playerLight.pointLightOuterRadius = playerRadius * reach * dim;
                playerLight.intensity = playerBase * (0.5f + 0.5f * dim) * (0.96f + 0.04f * Mathf.PerlinNoise(t * 3f, 0.3f));
            }
            for (int i = 0; i < torches.Count; i++)
            {
                var tc = torches[i];
                float n = Mathf.PerlinNoise(t * 5f + tc.seed, tc.seed * 0.7f);
                tc.light.intensity = torchIntensity * (0.8f + 0.35f * n);
                tc.flame.sprite = torchFrames[(int)(t * 9f + tc.seed * 3f) % torchFrames.Length];
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < flashes.Length; i++)
            {
                if (flashes[i].life <= 0f) continue;
                flashes[i].life -= dt;
                if (flashes[i].life <= 0f) { flashes[i].light.enabled = false; continue; }
                float k = flashes[i].life / flashes[i].maxLife;
                flashes[i].light.intensity = flashes[i].intensity * k * k;
            }
        }
    }
}
