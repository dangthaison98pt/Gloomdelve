using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Cyclic falling block: drops when something passes beneath, slams, then slowly winches back up.</summary>
    public class Crusher : Entity
    {
        public float triggerRange = 7f;
        public float warnTime = 0.35f;
        public float dropGravity = 70f;
        public float restTime = 1.1f;
        public float riseSpeed = 2.2f;
        public float cooldown = 0.8f;
        public int damage = 3;
        public SpriteRenderer chain;

        enum State { Idle, Warn, Drop, Rest, Rise }
        State state;
        float timer;
        Vector2 home;
        readonly List<Entity> tmp = new List<Entity>();
        readonly HashSet<Entity> hit = new HashSet<Entity>();

        public override void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y + 1f - size.y);
        }

        public override void OnSpawn() { base.OnSpawn(); home = pos; }

        public override void Tick(float dt)
        {
            switch (state)
            {
                case State.Idle:
                    timer -= dt;
                    if (timer > 0f) break;
                    var list = World.Entities;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var e = list[i];
                        if (e == null || e.Dead || !(e is PlayerController || e.IsEnemy) || e is Wraith) continue;
                        if (e is PlayerController pc && pc.IsDead) continue;
                        if (Mathf.Abs(e.pos.x - pos.x) < 0.9f && e.pos.y < pos.y && pos.y - e.pos.y < triggerRange && World.LineClear(Center, e.Center))
                        { state = State.Warn; timer = warnTime; Sfx.Play("crusher_warn", pos); break; }
                    }
                    break;
                case State.Warn:
                    timer -= dt;
                    pos = home + new Vector2(Mathf.Sin(timer * 80f) * 0.04f, 0f);
                    if (timer <= 0f) { pos = home; state = State.Drop; vel = Vector2.zero; hit.Clear(); }
                    break;
                case State.Drop:
                {
                    var b = StepPhysics(dt, dropGravity, 24f, true);
                    World.Query(Bounds, tmp, e => e != this && e.CanBeHitByItems && !hit.Contains(e));
                    foreach (var e in tmp)
                    {
                        hit.Add(e);
                        e.TakeDamage(new DamageInfo(damage, DamageType.Crush, this, new Vector2(Mathf.Sign(e.pos.x - pos.x) * 5f, 2f), displayName));
                    }
                    if (b.landed || grounded)
                    {
                        state = State.Rest; timer = restTime;
                        Fx.Shake(0.3f); Fx.Dust(pos, 6);
                        Sfx.Play("crusher_slam", pos);
                    }
                    break;
                }
                case State.Rest:
                    timer -= dt;
                    if (timer <= 0f) state = State.Rise;
                    break;
                case State.Rise:
                    pos = Vector2.MoveTowards(pos, home, riseSpeed * dt);
                    if ((pos - home).sqrMagnitude < 0.0001f) { pos = home; state = State.Idle; timer = cooldown; grounded = false; }
                    break;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (chain != null)
            {
                float len = Mathf.Max(0.01f, home.y + size.y - transform.position.y - size.y);
                chain.size = new Vector2(chain.size.x, len + 0.2f);
                chain.transform.localPosition = new Vector3(0f, size.y + len * 0.5f, 0f);
                chain.enabled = len > 0.05f;
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Fx.Debris(Center, new Color(0.45f, 0.4f, 0.45f), 10);
            Despawn();
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
