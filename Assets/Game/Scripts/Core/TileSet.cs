using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gloomdelve
{
    /// <summary>Visual tiles for one area. Autotile index = exposed-side mask (1=top,2=right,4=bottom,8=left).</summary>
    [CreateAssetMenu(menuName = "Brindlemoor/Tile Set", fileName = "TileSet")]
    public class TileSet : ScriptableObject
    {
        [Tooltip("16 masks x 2 variants.")] public TileBase[] dirt = new TileBase[32];
        [Tooltip("16 masks.")] public TileBase[] bedrock = new TileBase[16];
        [Tooltip("16 masks (final vault bricks).")] public TileBase[] brick = new TileBase[16];
        public TileBase platform;
        public TileBase ladder;
        public TileBase spikes;
        public TileBase trapBlock;
        public TileBase ventBlock;
        [Tooltip("Cracked rubble (bomb gates).")] public TileBase rubble;
        public TileBase goldOverlay;
        public TileBase gemOverlay;
        public TileBase[] background = new TileBase[4];
        public TileBase shopBackground;
        [Tooltip("Contact shadows on the back wall, indexed by solid-neighbour mask (1=up,2=right,4=down,8=left).")]
        public TileBase[] shade = new TileBase[16];
        public TileBase[] decorFloor;
        public TileBase[] decorCeiling;
        [Range(0, 1)] public float decorChance = 0.18f;
        [Tooltip("Outdoor set: no wall background above the terrain surface (the sky backdrop shows through).")]
        public bool outdoor;
    }
}
