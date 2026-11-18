using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Plays a few sprites in a loop on a SpriteRenderer (a fire burning). Visual only.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlicker : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 9f;

        SpriteRenderer sr;
        float offset;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            offset = Random.value * 10f;        // two fires never burn in step
        }

        void Update()
        {
            if (frames == null || frames.Length == 0 || sr == null) return;
            sr.sprite = frames[(int)((Time.time + offset) * fps) % frames.Length];
        }
    }
}
