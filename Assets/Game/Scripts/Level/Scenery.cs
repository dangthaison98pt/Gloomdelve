using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Purely decorative entity (trees...). The spawn point's param picks the sprite variant.</summary>
    public class Scenery : Prop
    {
        public Sprite[] variants;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            if (variants != null && variants.Length > 0 && sr != null)
                sr.sprite = variants[Mathf.Abs(sp.param) % variants.Length];
        }
    }
}
