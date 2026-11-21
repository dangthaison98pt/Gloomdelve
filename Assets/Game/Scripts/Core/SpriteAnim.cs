using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Frame-based sprite animation clip (editable in the Inspector).</summary>
    [CreateAssetMenu(menuName = "Brindlemoor/Sprite Anim", fileName = "Anim")]
    public class SpriteAnim : ScriptableObject
    {
        public Sprite[] frames;
        public float fps = 8f;
        public bool loop = true;
    }
}
