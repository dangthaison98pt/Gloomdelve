using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Base of every boss. A boss registers itself with the GameDirector (health bar, sealed exit), shrugs off
    /// knockback and stuns, takes at most maxHitDamage per hit (so one bomb never ends the fight) and tells the
    /// director when it dies. Each boss only writes its own attack pattern in Tick.
    /// To add one: subclass, make a prefab, register it, and name it in a boss arena file ("boss: id").
    /// </summary>
    public abstract class Boss : Enemy
    {
        [Header("Boss")]
        [Tooltip("Largest damage a single hit can do.")] public int maxHitDamage = 3;
        [Tooltip("Gold chunks dropped on death.")] public int goldChunks = 4;
        [Tooltip("Prefab id dropped on death along with the gold (the max-health item). Empty = nothing.")] public string rewardId = "heartvessel";

        [Tooltip("Projectile prefab used by the shots of this boss (venom, embers, feathers, orbs).")] public string bossShotId = "gloomorb";

        [Header("Stomping")]
        [Tooltip("Stomps in a row that count before the boss answers back (each boss in its own way).")]
        public int freeStomps = 2;
        [Tooltip("Leaving its head alone for this many seconds starts the count again.")]
        public float stompCounterWindow = 1.5f;
        float stompGuard;
        int stompsInARow;

        /// <summary>How the body moves during the counter-attack.</summary>
        public enum CounterMotion { Lunge, Buck, Burst, Flap }
        [Header("Counter-attack animation")]
        [Tooltip("Pose held during the counter (usually the boss's attack frame).")] public SpriteAnim animCounter;
        [Tooltip("One-shot effect played over its head: fangs, impact, frost, gust.")] public SpriteAnim counterFx;
        public CounterMotion counterMotion = CounterMotion.Lunge;
        [Tooltip("Seconds the counter pose lasts; the boss does nothing else meanwhile.")] public float counterTime = 0.5f;
        public float counterFxScale = 1.4f;
        [Tooltip("Play the effect around the body (a burst) instead of rising from the top of the head.")] public bool counterFxOnBody;
        float counterT;
        int counterAway = 1;
        bool counterPosed;

        /// <summary>In the middle of its counter-attack animation.</summary>
        public bool Countering => counterT > 0f;

        public float HealthFraction => maxHealth > 0 ? Mathf.Clamp01(health / (float)maxHealth) : 0f;
        public override bool IsHazard => true;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = -1;
            if (AutoTest.Active) { Despawn(); return; }      // the test bot only walks the arena
            if (GameDirector.Instance != null) GameDirector.Instance.RegisterBoss(this);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            stompGuard -= dt;
            counterT -= dt;
        }

        /// <summary>
        /// Call first thing in a boss's Tick (after base.Tick). While the counter-attack plays it holds the pose,
        /// keeps the boss in place and returns true: the boss skips its normal behaviour for that step.
        /// </summary>
        protected bool HoldCounterPose(float dt, bool onGround = true)
        {
            if (counterT <= 0f) return false;
            if (animCounter != null) anim?.Play(animCounter);
            if (onGround)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 40f * dt);
                var b = StepPhysics(dt, gravity);
                if (b.landed) OnLandedDuringCounter();      // a jump that ends while the pose is held still has to finish
            }
            else vel = Vector2.zero;
            return true;
        }

        /// <summary>The boss came down from a jump while holding its counter pose: finish whatever that landing does
        /// (its normal Tick, which usually handles it, is skipped during the pose).</summary>
        protected virtual void OnLandedDuringCounter() { }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr == null) return;
            var tr = sr.transform;
            if (counterT <= 0f)
            {
                if (counterPosed) { counterPosed = false; tr.localScale = Vector3.one; tr.localRotation = Quaternion.identity; }
                return;
            }
            counterPosed = true;
            float k = 1f - Mathf.Clamp01(counterT / Mathf.Max(0.01f, counterTime));     // 0 -> 1 through the move
            float swing = Mathf.Sin(k * Mathf.PI);                                       // out and back
            switch (counterMotion)
            {
                case CounterMotion.Lunge:   // rears straight up
                    tr.localScale = new Vector3(1f - 0.18f * swing, 1f + 0.4f * swing, 1f);
                    break;
                case CounterMotion.Buck:    // throws its back up towards the side the player flies off
                    tr.localRotation = Quaternion.Euler(0f, 0f, -counterAway * 26f * swing);
                    tr.localScale = new Vector3(1f, 1f + 0.12f * swing, 1f);
                    break;
                case CounterMotion.Burst:   // shrinks in, then bursts outward
                    float b = k < 0.25f ? -0.12f * (k / 0.25f) : -0.12f + 0.4f * Mathf.Sin((k - 0.25f) / 0.75f * Mathf.PI);
                    tr.localScale = Vector3.one * (1f + b);
                    break;
                case CounterMotion.Flap:    // two hard wing beats
                    float beat = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f));
                    tr.localScale = new Vector3(1f + 0.32f * beat, 1f - 0.14f * beat, 1f);
                    break;
            }
        }

        /// <summary>
        /// A couple of stomps in a row are fair (freeStomps). One more while the boss still remembers them is
        /// answered by its own counter (OnGreedyStomp) - and so is every further one until the player leaves its
        /// head alone for stompCounterWindow seconds.
        /// </summary>
        public override bool CounterStomp(PlayerController p)
        {
            if (stompGuard <= 0f) stompsInARow = 0;
            stompsInARow++;
            stompGuard = stompCounterWindow;
            if (stompsInARow <= freeStomps || Dead) return false;
            Tell();
            counterAway = p.pos.x >= pos.x ? 1 : -1;
            counterT = counterTime;
            if (animCounter != null) anim?.Play(animCounter);
            // the effect sprite is 2 tiles tall: sit its lower edge on the head, or centre it on the body
            Vector2 fxAt = counterFxOnBody ? Center : new Vector2(pos.x, pos.y + size.y + counterFxScale * 0.9f);
            Fx.PlayAnim(counterFx, fxAt, counterFxScale, counterAway < 0);
            OnGreedyStomp(p, counterAway);
            return true;
        }

        /// <summary>The boss's answer to being stomped again and again. 'away' is the side the player is on (+1 right).
        /// Default: throws the player off without hurting.</summary>
        protected virtual void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 7f, 9f), 0.2f);
            Fx.Shake(0.3f);
            Sfx.Play("thud", pos);
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            return base.TakeDamage(new DamageInfo(Mathf.Min(d.amount, maxHitDamage), d.type, d.source, Vector2.zero, d.cause));
        }

        protected override void OnHurt(in DamageInfo d) { }

        protected override void Die(in DamageInfo d)
        {
            var dir = GameDirector.Instance;
            var w = World;
            Vector2 at = Center;
            Fx.Shake(0.7f);
            Fx.Debris(at, bloodColor, 16);
            base.Die(d);
            if (w != null && !string.IsNullOrEmpty(rewardId))
            {
                var r = w.SpawnById(rewardId, at);
                if (r != null) r.vel = new Vector2(0f, 7f);
            }
            if (w != null)
                for (int i = 0; i < goldChunks; i++)
                {
                    var g = w.SpawnById("gold_big", at);
                    if (g != null) g.vel = new Vector2((i - (goldChunks - 1) * 0.5f) * 2.2f, 6f + i % 2);
                }
            if (dir != null) dir.OnBossDefeated(this);
        }

        protected PlayerController Target
        {
            get { var p = Player; return p != null && !p.IsDead ? p : null; }
        }

        /// <summary>Number of living entities of a type (minions a boss has summoned).</summary>
        protected int CountAlive<T>() where T : Entity
        {
            int n = 0;
            foreach (var e in World.Entities) if (e is T && !e.Dead && e != this) n++;
            return n;
        }

        /// <summary>Makes a rock spike fall from the ceiling above column x (no ceiling there: nothing happens).</summary>
        protected void DropFromCeiling(int x)
        {
            var g = World.Grid;
            x = Mathf.Clamp(x, 1, g.Width - 2);
            var prefab = World.config.prefabs.Get("stalactite");
            if (prefab == null) return;
            for (int y = Mathf.FloorToInt(pos.y) + 2; y < g.Height - 1; y++)
            {
                if (!g.IsEmpty(x, y)) return;
                if (g.IsSolid(x, y + 1))
                {
                    World.Spawn(prefab, new Vector2(x + 0.5f, y), new SpawnPoint("stalactite", new Vector2Int(x, y), 0, 1));
                    return;
                }
            }
        }

        /// <summary>The tell before a move: a "!" over the boss.</summary>
        protected void Tell()
        {
            Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.45f), "!", new Color(1f, 0.85f, 0.3f), 0.55f);
        }

        /// <summary>Fires one straight shot.</summary>
        protected BossShot FireShot(Vector2 from, Vector2 velocity, string shotId = null, Color? tint = null)
        {
            var s = World.SpawnById(string.IsNullOrEmpty(shotId) ? bossShotId : shotId, from) as BossShot;
            if (s == null) return null;
            s.vel = velocity; s.owner = this;
            if (tint.HasValue) s.Tint(tint.Value);
            return s;
        }

        /// <summary>Fires a fan of shots aimed at a point; spread is the angle between neighbours in degrees.</summary>
        protected void FanAt(Vector2 from, Vector2 target, int count, float spread, float speed, string shotId = null)
        {
            Vector2 d = target - from;
            float baseAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + (i - (count - 1) * 0.5f) * spread) * Mathf.Deg2Rad;
                FireShot(from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, shotId);
            }
        }

        /// <summary>Two shots that run along the floor to the left and right of the boss: jump over them.</summary>
        protected void Shockwave(float speed, Color tint)
        {
            float y = pos.y + 0.2f, hw = size.x * 0.5f + 0.3f;
            FireShot(new Vector2(pos.x - hw, y), new Vector2(-speed, 0f), "shockwave", tint);
            FireShot(new Vector2(pos.x + hw, y), new Vector2(speed, 0f), "shockwave", tint);
            Fx.Dust(pos, 10);
        }

        /// <summary>Launch velocity that lands a thrown object (gravity g) on a point after roughly time t.</summary>
        protected static Vector2 Lob(Vector2 from, Vector2 to, float gravity, float t)
        {
            Vector2 d = to - from;
            return new Vector2(d.x / t, d.y / t + 0.5f * gravity * t);
        }
    }
}
