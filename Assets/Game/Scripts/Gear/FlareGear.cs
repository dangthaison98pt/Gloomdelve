using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Flares, sold by Bera the Lamplighter three at a time (the shop offer FlareGear.OfferId gives the gear and
    /// the flares: ShopItem.Apply). Use: throw one lit, like a bomb. DOWN + use: set it down at your feet. UP + use:
    /// throw it high. It burns for half a minute (Flare). Bought for one climb; what is not thrown is lost with it.
    /// </summary>
    public class FlareGear : GearBehaviour
    {
        /// <summary>The gear id (EquipmentDef), the counter of flares left, and the shop offer.</summary>
        public const string Id = "flare", AmmoId = "flares", OfferId = "flares";

        public override int Ammo(RunState run) => run.GetResource(AmmoId);

        public override void Use(PlayerController p, FrameInput fi)
        {
            var run = RunSession.Current;
            if (run == null) return;
            if (run.GetResource(AmmoId) <= 0)
            {
                Sfx.Play("error", p.pos);
                Fx.Text(p.Center + Vector2.up * 0.9f, "NO FLARES", new Color(1f, 0.5f, 0.45f), 0.9f);
                return;
            }
            var prefab = p.World.config.prefabs.Get(Id) as Flare;
            if (prefab == null) return;
            var f = p.World.Spawn(prefab, p.HoldPos);
            if (f == null) return;
            run.AddResource(AmmoId, -1);
            var cfg = p.cfg;
            if (fi.down && p.grounded)
            {
                f.pos = new Vector2(p.pos.x + p.facing * 0.3f, p.pos.y + 0.02f);
                f.Place(Vector2.zero);
            }
            else
            {
                Vector2 v = fi.up ? new Vector2(p.facing * cfg.bombThrowUpVelocity.x, cfg.bombThrowUpVelocity.y)
                                  : new Vector2(p.facing * cfg.bombThrowVelocity.x, cfg.bombThrowVelocity.y);
                v.x += p.vel.x * 0.4f;
                f.Throw(p, v);
                p.PlayThrowAnim(0.2f);
                Sfx.Play("throw", p.pos);
            }
            Sfx.Play("flame", p.pos, 0.5f, 1.4f);
        }
    }
}
