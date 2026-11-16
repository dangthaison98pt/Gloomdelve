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
        [Tooltip("Gold dropped on death (0 = none). Paid out as chunks (150) and nuggets (50).")] public int goldDrop = 50;
        [Tooltip("Chance to drop a heart on death instead of the gold.")] [Range(0, 1)] public float heartDropChance = 0.05f;
        /// <summary>Called in by a boss during its fight: killing it pays no gold and no heart (no farming the arena).</summary>
        [System.NonSerialized] public bool summoned;

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
            var w = World;
            Vector2 at = Center;
            if (summoned) { Despawn(); return; }
            // One reward per kill: now and then a heart, otherwise gold.
            if (heartDropChance > 0f && Random.value < heartDropChance)
            {
                var h = w.SpawnById("heart", at);
                if (h != null) h.vel = new Vector2(0f, 5.5f);
                Despawn();
                return;
            }
            int chunks = goldDrop / 150, nuggets = Mathf.CeilToInt((goldDrop - chunks * 150) / 50f), n = chunks + nuggets;
            for (int i = 0; i < n; i++)
            {
                var e = w.SpawnById(i < chunks ? "gold_big" : "gold_small", at);
                if (e != null) e.vel = new Vector2(n > 1 ? (i - (n - 1) * 0.5f) * 1.8f : 0f, 4f + (i % 2));
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

        protected bool WallAhead(int dir, float extra = 0f)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.08f + extra);
            return World.Grid.IsSolid(Mathf.FloorToInt(fx), Mathf.FloorToInt(pos.y + 0.2f));
        }

        protected bool EdgeAhead(int dir, float extra = 0f)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.08f + extra);
            int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(pos.y - 0.1f);
            return !World.Grid.IsSolid(x, y) && !World.Grid.IsPlatformTop(x, y);
        }

        protected bool SpikesAhead(int dir, float extra = 0f)
        {
            float fx = pos.x + dir * (size.x * 0.5f + 0.4f + extra);
            return World.Grid.IsSpike(Mathf.FloorToInt(fx), Mathf.FloorToInt(pos.y + 0.1f));
        }

        /// <summary>A wall, a ledge or spikes right in front: a patroller should not walk this way.</summary>
        protected bool BlockedAhead(int dir, float extra = 0f) => WallAhead(dir, extra) || EdgeAhead(dir, extra) || SpikesAhead(dir, extra);

        /// <summary>A patroller only turns around when it can walk at least this far (tiles) the other way.</summary>
        protected const float MinPatrolRoom = 0.75f;

        /// <summary>
        /// Patrol steering shared by ground walkers. Returns the direction to walk this step (-1 / +1), or 0 when
        /// there is no real room either way (e.g. standing on a single tile): then the enemy stands still and just
        /// turns to face the player instead of shuffling left-right every few steps.
        /// </summary>
        bool patrolStuck;

        protected int PatrolStep()
        {
            if (patrolStuck)
            {
                // still boxed in on both sides? (terrain may have been destroyed since)
                if (BlockedAhead(facing, MinPatrolRoom) && BlockedAhead(-facing, MinPatrolRoom))
                {
                    var p = Player;
                    if (p != null && !p.IsDead && Mathf.Abs(p.pos.x - pos.x) > 0.4f) facing = p.pos.x > pos.x ? 1 : -1;
                    return 0;
                }
                patrolStuck = false;
            }
            if (!BlockedAhead(facing)) return facing;
            if (!BlockedAhead(-facing, MinPatrolRoom)) { facing = -facing; return facing; }
            patrolStuck = true;
            return 0;
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
