using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Use: throw a lit bomb. DOWN + use: place it. UP + use: throw high.</summary>
    public class BombGear : GearBehaviour
    {
        public override int Ammo(RunState run) => run.bombs;

        public override void Use(PlayerController p, FrameInput fi)
        {
            var run = RunSession.Current;
            var cfg = p.cfg;
            if (run == null) return;
            if (run.bombs <= 0) { Sfx.Play("error", p.pos); return; }
            var prefab = p.World.config.prefabs.Get("bomb") as Bomb;
            if (prefab == null) return;
            run.bombs--;
            var b = p.World.Spawn(prefab, p.HoldPos);
            if (b == null) return;
            b.Light(p, run.stats.blastRadiusBonus);
            if (fi.down && p.grounded)
            {
                b.pos = new Vector2(p.pos.x + p.facing * 0.3f, p.pos.y + 0.02f);
                b.Place(Vector2.zero);
                Sfx.Play("bomb_place", p.pos);
            }
            else
            {
                Vector2 v = fi.up ? new Vector2(p.facing * cfg.bombThrowUpVelocity.x, cfg.bombThrowUpVelocity.y)
                                  : new Vector2(p.facing * cfg.bombThrowVelocity.x, cfg.bombThrowVelocity.y);
                v.x += p.vel.x * 0.4f;
                b.Throw(p, v);
                p.PlayThrowAnim(0.2f);
                Sfx.Play("throw", p.pos);
            }
        }
    }

    /// <summary>Use: throw a rope up (hooks onto a ceiling or a wall beside it). DOWN + use at a ledge: drop it down.</summary>
    public class RopeGear : GearBehaviour
    {
        public override int Ammo(RunState run) => run.ropes;

        public override void Use(PlayerController p, FrameInput fi)
        {
            var run = RunSession.Current;
            if (run == null) return;
            if (run.ropes <= 0) { Sfx.Play("error", p.pos); return; }
            var prefab = p.World.config.prefabs.Get("rope") as RopeProjectile;
            if (prefab == null) return;
            var g = p.World.Grid;
            if (fi.down && p.grounded)
            {
                int c = Mathf.FloorToInt(p.pos.x + p.facing * (p.HalfWidth + 0.45f));
                int r = Mathf.FloorToInt(p.pos.y + 0.1f);
                if (!g.IsSolid(c, r) && !g.IsSolid(c, r - 1) && !g.IsPlatformTop(c, r - 1))
                {
                    run.ropes--;
                    var rp = p.World.Spawn(prefab, new Vector2(c + 0.5f, r));
                    rp?.DeployDown(c, r);
                    return;
                }
            }
            int cx = Mathf.FloorToInt(p.pos.x);
            int cy = Mathf.FloorToInt(p.pos.y + 0.5f);
            if (g.IsSolid(cx, cy)) { Sfx.Play("error", p.pos); return; }
            run.ropes--;
            var up = p.World.Spawn(prefab, new Vector2(cx + 0.5f, p.pos.y + 0.4f));
            up?.LaunchUp();
            Sfx.Play("rope_throw", p.pos);
        }
    }

    /// <summary>Passive: while equipped, holding jump when falling glides. Never used up.</summary>
    public class GliderGear : GearBehaviour
    {
        public override bool AffectsMovement => true;

        public override void Use(PlayerController p, FrameInput fi)
        {
            Fx.Text(p.Center + Vector2.up * 0.9f, "HOLD JUMP WHILE FALLING", new Color(0.8f, 0.95f, 1f), 1.2f);
        }

        public override void ModifyMotor(PlayerController p, ref MotorInput mi) => mi.glide = true;

        public override void ApplyToValidator(ref LevelValidator.Options o) => o.glide = true;
    }

    /// <summary>
    /// The Recall Charm, carried from the start of every run. Use: back to where this level began, through a
    /// short fade. Levels branch, and some branches are drops with no way up again - the charm is the way out of a
    /// wrong turn. Never used up, but it needs a few breaths between uses, and it will not carry the Sunstone or
    /// anything else that takes two hands.
    /// </summary>
    public class RecallGear : GearBehaviour
    {
        public const float Cooldown = 8f;
        float readyAt;

        /// <summary>Seconds until it can be used again (0 = ready).</summary>
        public float Wait => Mathf.Max(0f, readyAt - Time.time);
        public void MakeReady() { readyAt = 0f; }

        public override void Use(PlayerController p, FrameInput fi)
        {
            var dir = GameDirector.Instance;
            var lvl = dir != null ? dir.Level : null;
            if (lvl == null || ScreenFade.Busy) return;
            if (p.held != null && p.held.heavy)
            {
                Sfx.Play("error", p.pos);
                Fx.Text(p.Center + Vector2.up * 0.9f, "TOO HEAVY TO CARRY BACK", new Color(1f, 0.5f, 0.45f), 1.2f);
                return;
            }
            if (Time.time < readyAt)
            {
                Sfx.Play("error", p.pos);
                Fx.Text(p.Center + Vector2.up * 0.9f, $"NOT YET  ({Mathf.CeilToInt(readyAt - Time.time)})", new Color(0.6f, 0.9f, 1f), 0.9f);
                return;
            }
            readyAt = Time.time + Cooldown;
            var dest = new Vector2(lvl.entrance.x + 0.5f, lvl.entrance.y);
            Sfx.PlayUI("pickup", 0.9f);
            Fx.Sparks(p.Center, new Color(0.5f, 0.95f, 1f), 14, 5f);
            ScreenFade.Through(() =>
            {
                if (p == null || p.IsDead) return;
                VillageInterior.Place(p, dest);
                if (dir.cameraFollow != null) dir.cameraFollow.SetBounds(new Rect(0, 0, lvl.Width, lvl.Height));
                Fx.Sparks(dest + Vector2.up * 0.6f, new Color(0.5f, 0.95f, 1f), 14, 5f);
            });
        }
    }
}
