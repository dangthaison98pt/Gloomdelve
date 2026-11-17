using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The Foreman's stamping press: it appears under the ceiling of a column, rattles for a moment (the warning,
    /// with dust falling down the column), slams to the floor, stays down for a beat and goes back up. It hurts on
    /// the way down and while it is down. The Foreman sets off a row of them one after another: stand between.
    /// Spawn it with World.SpawnById at the top of the column and call Arm(floorY, delay).
    /// </summary>
    public class Press : Entity
    {
        public int damage = 2;
        [Tooltip("Rattle time before it comes down.")] public float warnTime = 0.6f;
        public float dropSpeed = 30f;
        public float holdTime = 0.35f;
        public float riseSpeed = 9f;

        enum Phase { Wait, Warn, Drop, Hold, Rise }
        Phase phase;
        float timer, topY, floorY;

        public override bool IsHazard => true;
        public bool Down => phase == Phase.Hold;

        public void Arm(float floor, float delay)
        {
            floorY = floor;
            topY = pos.y;
            phase = Phase.Wait;
            timer = delay;
            if (sr != null) sr.enabled = false;
        }

        public override void Tick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Wait:
                    if (timer <= 0f)
                    {
                        phase = Phase.Warn; timer = warnTime;
                        if (sr != null) sr.enabled = true;
                        Fx.Dust(new Vector2(pos.x, topY), 3);
                        Fx.Text(new Vector2(pos.x, floorY + 0.6f), "!", new Color(1f, 0.5f, 0.3f), warnTime);
                    }
                    break;
                case Phase.Warn:
                    pos = new Vector2(pos.x, topY + Mathf.Sin(timer * 70f) * 0.05f);
                    if (timer <= 0f) { phase = Phase.Drop; pos.y = topY; }
                    break;
                case Phase.Drop:
                    pos.y -= dropSpeed * dt;
                    if (pos.y <= floorY)
                    {
                        pos.y = floorY;
                        phase = Phase.Hold; timer = holdTime;
                        Fx.Shake(0.3f);
                        Fx.Dust(new Vector2(pos.x, floorY), 5);
                        Sfx.Play("thud", pos, 0.9f, 0.8f);
                    }
                    break;
                case Phase.Hold:
                    if (timer <= 0f) phase = Phase.Rise;
                    break;
                case Phase.Rise:
                    pos.y += riseSpeed * dt;
                    if (pos.y >= topY) { Despawn(); return; }
                    break;
            }
            if (phase == Phase.Drop || phase == Phase.Hold)
            {
                var p = World.Player;
                if (p != null && !p.IsDead && Overlaps(p))
                    p.TakeDamage(new DamageInfo(damage, DamageType.Crush, this, new Vector2((p.pos.x >= pos.x ? 1 : -1) * 6f, 5f), "a stamping press"));
            }
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
    }
}
