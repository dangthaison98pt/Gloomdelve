using UnityEngine;
using static Gloomdelve.EditorTools.Px;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The pieces of the Blue Ocarina's playing screen (OcarinaScreen): a hole of the ocarina (a disc, tinted by
    /// the screen: blue at rest, bright while it sounds) and the head of a written note (a slanted oval).
    /// </summary>
    public static partial class ArtGenerator
    {
        static void OcarinaUiArt()
        {
            {
                var p = new Px(28, 28);
                p.Disc(13.5f, 13.5f, 13.5f, C(150, 160, 190));
                p.Disc(13.5f, 13.5f, 12f, C(255, 255, 255));
                p.Disc(13.5f, 15f, 9f, C(236, 240, 250));
                p.Disc(10f, 18f, 3f, C(255, 255, 255));
                Save(p, "UI/ocarina_hole", PCenter);
            }
            {
                var p = new Px(14, 10);
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 14; x++)
                    {
                        float dx = x - 6.5f, dy = y - 4.5f;
                        float rx = dx * 0.94f + dy * 0.34f, ry = -dx * 0.34f + dy * 0.94f;        // tilted a little, as written notes are
                        if ((rx * rx) / 42f + (ry * ry) / 14f <= 1f) p.Set(x, y, C(255, 255, 255));
                    }
                Save(p, "UI/note_head", PCenter);
            }
        }
    }
}
