using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks: a scuttling beetle that eats metal. Left alone it patrols and nips like a snake. But a lit bomb
    /// nearby is a meal: it runs to it and swallows it - the bomb is gone - then glows red and runs at the player
    /// with the fuse still burning inside it, and blows up. So a careless bomb comes back at you; a clever one
    /// turns the beetle into a bomb you can lead into other creatures (or just kill before it reaches you).
    /// </summary>
    public class ScrapBeetle : Snake
    {
        [Tooltip("It notices a lit bomb within this distance (and about level with it).")] public float smellRange = 4.5f;
        public float rushSpeed = 3.6f;
        [Tooltip("Seconds between swallowing the bomb and the blast.")] public float primedTime = 1.6f;
        public float primedSpeed = 4.2f;
        public float blastRadius = 2.2f;
        public SpriteAnim animPrimed;

        bool primed;
        float fuse;

        public bool Primed => primed;

        Bomb NearestBomb()
        {
            Bomb best = null; float bd = smellRange * smellRange;
            foreach (var e in World.Entities)
            {
                if (!(e is Bomb b) || b.Dead || !b.IsLit) continue;
                if (Mathf.Abs(b.pos.y - pos.y) > 1.2f) continue;
                float d = (b.pos - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public override void Tick(float dt)
        {
            if (primed)
            {
                hurtTimer -= dt; stunTimer -= dt;
                fuse -= dt;
                var p = Player;
                if (grounded && p != null && !p.IsDead)
                {
                    if (Mathf.Abs(p.pos.x - pos.x) > 0.2f) facing = p.pos.x > pos.x ? 1 : -1;
                    vel.x = WallAhead(facing) || EdgeAhead(facing) ? 0f : facing * primedSpeed;
                }
                StepPhysics(dt, gravity);
                if (fuse % 0.2f < dt) Flash(0.08f);
                if (fuse <= 0f) Burst();
                return;
            }
            var bomb = stunTimer <= 0f ? NearestBomb() : null;
            if (bomb != null)
            {
                hurtTimer -= dt; stunTimer -= dt;
                if (grounded)
                {
                    if (Mathf.Abs(bomb.pos.x - pos.x) > 0.1f) facing = bomb.pos.x > pos.x ? 1 : -1;
                    vel.x = WallAhead(facing) || EdgeAhead(facing) ? 0f : facing * rushSpeed;
                }
                StepPhysics(dt, gravity);
                if (Overlaps(bomb))
                {
                    bomb.Despawn();
                    primed = true;
                    fuse = primedTime;
                    anim?.Play(animPrimed);
                    Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.35f), "!", new Color(1f, 0.4f, 0.3f), 0.5f);
                    Sfx.Play("fuse", pos, 0.8f, 1.4f);
                }
                return;
            }
            base.Tick(dt);
        }

        void Burst()
        {
            var w = World;
            Vector2 at = Center;
            Despawn();                      // gone before the blast, so it is not "killed" by it (no gold for this)
            w.Explode(at, blastRadius, this);
        }

        protected override void Die(in DamageInfo d)
        {
            // killed with the bomb inside: it still goes off, where it died
            if (primed) { var w = World; Vector2 at = Center; base.Die(d); w.Explode(at, blastRadius, this); return; }
            base.Die(d);
        }
    }
}
