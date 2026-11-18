using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Plays a few sprites in a loop on a UI Image (the traveller's scarf blowing in the wind on the title
    /// picture). Runs on unscaled time. Put it on the Image and fill in the frames.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MenuSpriteAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 9f;

        Image image;

        /// <summary>Which frame is showing right now.</summary>
        public int Frame { get; private set; }

        void Awake() { image = GetComponent<Image>(); }

        void Update()
        {
            if (frames == null || frames.Length == 0 || image == null) return;
            Frame = (int)(Time.unscaledTime * fps) % frames.Length;
            image.sprite = frames[Frame];
        }
    }
}
