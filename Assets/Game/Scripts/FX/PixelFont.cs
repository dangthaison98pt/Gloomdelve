using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Pixel Font", fileName = "PixelFont")]
    public class PixelFont : ScriptableObject
    {
        public string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ$+-!?.:/";
        public Sprite[] glyphs;
        [Tooltip("Advance per character in world units.")] public float advance = 4f / 16f;

        public Sprite Get(char c)
        {
            c = char.ToUpperInvariant(c);
            int i = characters.IndexOf(c);
            return i >= 0 && glyphs != null && i < glyphs.Length ? glyphs[i] : null;
        }
    }
}
