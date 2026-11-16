using UnityEngine;

namespace Gloomdelve
{
    public abstract class Enemy : Entity
    {
        [Header("Enemy")]
        public int contactDamage = 1;
        public bool stompable = true;
        public float gravity = 40f;
        public float knockbackScale = 1f;
        public Color bloodColor = new Color(0.55f, 0.1f, 0.12f);
        [Tooltip("Gold dropped on death (0 = none).")] public int goldDrop = 0;

        [System.NonSerialized] public float stunTimer;
        protected float hurtTimer;

        public override bool IsEnemy => true;
        public override bool CanBeHitByItems => !Dead;
        public override bool IsStompable => stompable;
        public override int ContactDamage => stunTimer > 0f ? 0 : contactDamage;
        public virtual DamageType ContactType => DamageType.Contact;
        public override bool TriggersTraps => vel.sqrMagnitude > 0.25f;

        protected PlayerController Player => World != null ? World.Player : null;

        public override void Tick(float dt)
        {
            hurtTimer -= dt;
            stunTimer -= dt;
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (Dead) return false;
            if (hurtTimer > 0f && d.type != DamageType.Explosion && d.type != DamageType.Spikes) return false;
            health -= d.amount;
            hurtTimer = 0.2f;
            Flash(0.15f);
            Fx.Blood(Center, bloodColor, 5);
            if (d.knockback != Vector2.zero) { vel = d.knockback; grounded = false; }
            Sfx.Play("hit", pos);
            if (health <= 0) Die(d);
            else OnHurt(d);
            return true;
        }

        protected virtual void OnHurt(in DamageInfo d) { stunTimer = Mathf.Max(stunTimer, 0.3f); }

        protected virtual void Die(in DamageInfo d)
        {
            Fx.Blood(Center, bloodColor, 14);
            Sfx.Play("enemy_die", pos);
            if (RunSession.Current != null && (d.source is PlayerController || (d.source is Item it && it.thrower is PlayerController) || d.source is Bomb))
                RunSession.Current.kills++;
            if (goldDrop > 0)
            {
                var e = World.SpawnById(goldDrop >= 100 ? "gold_big" : "gold_small", Center);
                if (e != null) e.vel = new Vector2(0f, 4f);
            }
            Despawn();
        }

        protected bool CheckSpikes(in Body b)
        {
            if (BodyPhysics.SpikeHit(World.Grid, pos, size.x * 0.5f, b.preVelY))
            {
                TakeDamage(new DamageInfo(99, DamageType.Spikes, null, Vector2.zero, "spikes"));
                return true;
            }
            return false;
        }

        protected bool WallAhead(int dir)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.08f);
            return World.Grid.IsSolid(Mathf.FloorToInt(fx), Mathf.FloorToInt(pos.y + 0.2f));
        }

        protected bool EdgeAhead(int dir)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.08f);
            int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(pos.y - 0.1f);
            return !World.Grid.IsSolid(x, y) && !World.Grid.IsPlatformTop(x, y);
        }

        protected bool SpikesAhead(int dir)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.4f);
            return World.Grid.IsSpike(Mathf.FloorToInt(fx), Mathf.FloorToInt(pos.y + 0.1f));
        }

        protected bool SeesPlayer(float range)
        {
            var p = Player;
            if (p == null || p.IsDead) return false;
            if ((p.Center - Center).sqrMagnitude > range * range) return false;
            return World.LineClear(Center, p.Center);
        }

        /// <summary>Place hanging from the ceiling of its cell.</summary>
        protected void HangFromCeiling(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y + 1f - size.y);
        }
    }
}
