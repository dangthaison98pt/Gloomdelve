using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// One of the street's firs, the one by the village hall (it looks like the others). Shaken (Enter), it sways
    /// and sheds needles and snow. Shaken five times running - each shake hard on the last - something small and
    /// blue drops out of it: the Blue Ocarina (OcarinaPickup), which earns "Ocarina of GOAT". After that it is
    /// a fir like the others: nothing to do with it any more (for good in this save, once the ocarina is in the bag -
    /// and for the rest of the visit as soon as it has dropped).
    /// </summary>
    public class ShakeTree : Entity, IInteractable
    {
        [Tooltip("Its outline while the player is in reach.")] public Sprite outline;
        public SpriteRenderer glow;
        [Tooltip("Shakes in a row that bring the ocarina down.")] public int shakesNeeded = 5;
        [Tooltip("At most this long between two shakes for them to count as running.")] public float shakeGap = 1.5f;

        float sinceShake = 99f, swayT = 99f;
        int run;
        bool dropped;

        public int ShakesInARow => run;
        public bool Dropped => dropped;

        /// <summary>It has given up its ocarina: it is only a tree now (no prompt, no outline).</summary>
        public bool Spent => dropped || Villagers.HasKey(Villagers.KeyOcarina);

        public string GetPrompt(PlayerController p) => Spent ? null : "[enter]  Shake the tree";

        public void Interact(PlayerController p)
        {
            if (p == null || Spent || p.motor.mode != MotorMode.Normal || !p.motor.grounded) return;
            run = sinceShake <= shakeGap ? run + 1 : 1;
            sinceShake = 0f;
            swayT = 0f;
            Sfx.Play("rustle", pos, 0.8f, Random.Range(0.9f, 1.1f));
            var crown = new Vector2(pos.x, pos.y + 2.4f);
            Fx.Debris(crown + new Vector2(Random.Range(-0.8f, 0.8f), 0f), new Color(0.2f, 0.42f, 0.3f), 5);         // needles
            Fx.Debris(crown + new Vector2(Random.Range(-0.8f, 0.8f), 0.4f), new Color(0.92f, 0.95f, 1f), 4);       // snow off them
            if (run >= shakesNeeded && !dropped) Drop();
        }

        void Drop()
        {
            dropped = true; run = 0;
            var cfg = World.config;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("ocarina") : null;
            if (prefab == null) return;
            var at = new Vector2(pos.x + 0.5f, pos.y + 2.3f);
            var o = World.Spawn(prefab, at, new SpawnPoint("ocarina", new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y))));
            if (o != null) { o.pos = at; o.prevPos = at; o.vel = new Vector2(0.8f, 2f); }
            Sfx.Play("chirp", pos, 0.5f, 0.7f);
        }

        public override void Tick(float dt) { sinceShake += dt; swayT += dt; }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null)
            {
                // a hard sway that dies away: the crown moves, the foot stays (the picture's pivot is its foot)
                float a = swayT < 0.9f ? Mathf.Sin(swayT * 26f) * 4f * (1f - swayT / 0.9f) : 0f;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                if (glow != null) glow.transform.localRotation = sr.transform.localRotation;
            }
            InReach.Show(glow, Spent ? null : outline, this);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
