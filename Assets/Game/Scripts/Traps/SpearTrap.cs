using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs trap: a slotted plate in the floor. On a steady beat spears come up through it: the tips
    /// show first (the warning), then the spears stand for a moment and go down again. Standing on the plate while
    /// they are up hurts and throws the player off; creatures are speared too. Cross it between two beats, or
    /// jump it. Neighbouring plates are out of step with each other.
    /// Placed by the level generator (AreaConfig.spearChance) on plain floor with room above to jump.
    /// </summary>
    public class SpearTrap : Entity
    {
        [Tooltip("Seconds the spears stay down.")] public float restTime = 1.7f;
        [Tooltip("Seconds the tips show before they come up: the warning.")] public float warnTime = 0.55f;
        [Tooltip("Seconds the spears stand.")] public float upTime = 0.7f;
        public int damage = 2;
        [Tooltip("Pictures: down, tips showing, up.")] public Sprite[] frames;

        float t;
        int shown = -1;

        public override bool IsHazard => true;
        public bool Up => Phase == 2;
        /// <summary>0 down, 1 warning, 2 up.</summary>
        public int Phase
        {
            get
            {
                float c = t % (restTime + warnTime + upTime);
                return c < restTime ? 0 : c < restTime + warnTime ? 1 : 2;
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            t = (Mathf.FloorToInt(pos.x) * 0.37f) % 1f * (restTime + warnTime + upTime);      // out of step with its neighbours
        }

        public override void Tick(float dt)
        {
            if (AutoTest.Active) return;            // the test bot replays a plan made without a clock
            int before = Phase;
            t += dt;
            int now = Phase;
            if (now == 2 && before != 2) Sfx.Play("arrow", pos, 0.6f, 0.7f);
            if (now != 2) return;
            var p = World.Player;
            if (p != null && !p.IsDead && Overlaps(p))
                p.TakeDamage(new DamageInfo(damage, DamageType.Crush, this, new Vector2((p.pos.x >= pos.x ? 1 : -1) * 4f, 9f), "spears"));
            Enemy hit = null;
            foreach (var e in World.Entities)
                if (e is Enemy en && !en.Dead && en.grounded && Overlaps(en)) { hit = en; break; }
            if (hit != null) hit.TakeDamage(new DamageInfo(1, DamageType.Crush, this, new Vector2(0f, 6f), "spears"));
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            int ph = Phase;
            if (sr == null || frames == null || ph == shown || ph >= frames.Length) return;
            shown = ph;
            sr.sprite = frames[ph];
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
