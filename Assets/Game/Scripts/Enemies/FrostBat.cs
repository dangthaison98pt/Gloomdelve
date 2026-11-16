using UnityEngine;

namespace Gloomdelve
{
    /// <summary>A bat of the frozen caves: when it dies it freezes solid and drops as an ice chunk you can throw.</summary>
    public class FrostBat : Bat
    {
        public string dropId = "icechunk";

        protected override void Die(in DamageInfo d)
        {
            Vector2 at = Center;
            var w = World;
            base.Die(d);
            var e = w != null ? w.SpawnById(dropId, at) : null;
            if (e != null) e.vel = new Vector2(0f, 2f);
        }
    }
}
