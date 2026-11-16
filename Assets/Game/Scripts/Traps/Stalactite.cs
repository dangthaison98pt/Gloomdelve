using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Falling hazard: hangs from the ceiling, shakes when something passes below, then drops and shatters.</summary>
    public class Stalactite : Entity
    {
        public float triggerRange = 8f;
        public float shakeTime = 0.45f;
        public float gravity = 45f;
        public int damage = 2;
        public Color debrisColor = new Color(0.6f, 0.5f, 0.4f);
        [Tooltip("param=1 in the spawn point makes it start falling immediately (collapse debris).")]
        public bool startFalling;

        enum State { Hang, Shake, Fall }
        State state;
        float timer;
        Vector2 home;
        readonly List<Entity> tmp = new List<Entity>();

        public override void Configure(SpawnPoint sp)
        {
            pos = new Vector2(sp.cell.x + 0.5f, sp.cell.y + 1f - size.y);
            if (sp.param == 1) startFalling = true;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            home = pos;
            if (startFalling) state = State.Fall;
        }

        public override void Tick(float dt)
        {
            switch (state)
            {
                case State.Hang:
                {
                    var g = World.Grid;
                    if (!g.IsSolid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + size.y + 0.1f))) { StartShake(); break; }
                    var list = World.Entities;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var e = list[i];
                        if (e == null || e.Dead || !(e is PlayerController || e.IsEnemy) || e is Wraith || e is Bat) continue;
                        if (e is PlayerController pc && pc.IsDead) continue;
                        if (Mathf.Abs(e.pos.x - pos.x) < 0.75f && e.pos.y < pos.y && pos.y - e.pos.y < triggerRange && World.LineClear(Center, e.Center))
                        { StartShake(); break; }
                    }
                    break;
                }
                case State.Shake:
                    timer -= dt;
                    pos = home + new Vector2(Mathf.Sin(timer * 90f) * 0.05f, 0f);
                    if (timer <= 0f) { pos = home; state = State.Fall; vel = Vector2.zero; }
                    break;
                case State.Fall:
                {
                    var b = StepPhysics(dt, gravity, 20f, true);
                    var r = Bounds;
                    World.Query(r, tmp, e => e != this && e.CanBeHitByItems);
                    foreach (var e in tmp)
                    {
                        e.TakeDamage(new DamageInfo(damage, DamageType.Falling, this, new Vector2(0f, -2f), displayName));
                        Shatter();
                        return;
                    }
                    if (b.landed || grounded || b.hitLeft || b.hitRight) Shatter();
                    if (pos.y < -3f) Despawn();
                    break;
                }
            }
        }

        void StartShake()
        {
            state = State.Shake;
            timer = shakeTime;
            Sfx.Play("crack", pos);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), debrisColor, 2);
        }

        void Shatter()
        {
            if (Dead) return;
            Fx.Debris(Center, debrisColor, 8);
            Sfx.Play("shatter", pos);
            Despawn();
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) => Shatter();
        public override void OnWhipped(PlayerController p, int damage, int dir) { if (state == State.Fall) Shatter(); }
    }
}
