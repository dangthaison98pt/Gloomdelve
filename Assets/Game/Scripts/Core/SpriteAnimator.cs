using UnityEngine;

namespace Gloomdelve
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteAnim initial;
        public float speed = 1f;

        SpriteRenderer sr;
        SpriteAnim current;
        float time;

        public SpriteAnim Current => current;
        public bool Finished => current != null && !current.loop && time * current.fps >= current.frames.Length;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (initial != null) Play(initial, true);
        }

        public void Play(SpriteAnim clip, bool restart = false)
        {
            if (clip == null) return;
            if (clip == current && !restart) return;
            current = clip;
            time = 0f;
            Apply();
        }

        public void SetFrame(SpriteAnim clip, int frame)
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;
            current = clip;
            time = frame / Mathf.Max(0.01f, clip.fps);
            Apply();
        }

        void Update()
        {
            if (current == null) return;
            time += Time.deltaTime * speed;
            Apply();
        }

        void Apply()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (current == null || current.frames == null || current.frames.Length == 0) return;
            int n = current.frames.Length;
            int f = Mathf.FloorToInt(time * current.fps);
            f = current.loop ? ((f % n) + n) % n : Mathf.Clamp(f, 0, n - 1);
            sr.sprite = current.frames[f];
        }
    }
}
