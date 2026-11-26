using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The whole mix, made louder where it is heard (on the AudioListener; AudioManager puts it there). The sound
    /// files are mixed quiet and Unity will not play a sound above full volume, so even with every slider at 100%
    /// the game was soft: this raises everything by a fixed gain, and rounds off what would go past full scale
    /// (a soft limiter: loud moments are pressed down smoothly instead of crackling). The files are not touched.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class LoudnessBoost : MonoBehaviour
    {
        [Tooltip("How much louder the whole mix is (4.44 = about +13 dB): with the master and the sound sliders at 75% (0.5625 together), the game is as loud as 2.5 times at 100%.")] public float gain = 4.44f;
        [Tooltip("Above this level the limiter starts to round the sound off (full scale is 1).")] [Range(0.3f, 0.95f)] public float knee = 0.7f;

        volatile float g = 4.44f, k = 0.7f;

        void Update() { g = gain; k = knee; }

        /// <summary>One sample through the gain and the limiter (never past full scale).</summary>
        public static float Shape(float x, float gain, float knee)
        {
            x *= gain;
            float a = x < 0f ? -x : x;
            if (a <= knee) return x;
            float over = (a - knee) / (1f - knee);
            float y = knee + (1f - knee) * (float)System.Math.Tanh(over);
            return x < 0f ? -y : y;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float gg = g, kk = k;
            if (gg == 1f) return;
            for (int i = 0; i < data.Length; i++) data[i] = Shape(data[i], gg, kk);
        }
    }
}
