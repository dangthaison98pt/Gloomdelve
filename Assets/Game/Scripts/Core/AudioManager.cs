using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Plays generated SFX/music. Volumes come from SaveSystem.Settings.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public SfxLibrary library;
        public int voices = 20;

        AudioSource musicSource;
        AudioSource[] pool;
        int next;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        string currentMusic;
        float musicBaseVolume = 0.7f;
        float duck = 1f;
        bool musicHushed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
        }

        AudioListener boosted;

        /// <summary>The listener of the scene gets the loudness boost (LoudnessBoost: the files are mixed quiet).</summary>
        void BoostListener()
        {
            if (boosted != null && boosted.isActiveAndEnabled) return;
            var l = FindAnyObjectByType<AudioListener>();
            if (l == null) return;
            if (l.GetComponent<LoudnessBoost>() == null) l.gameObject.AddComponent<LoudnessBoost>();
            boosted = l;
        }

        void Update()
        {
            BoostListener();
            var s = SaveSystem.Settings;
            duck = musicHushed ? Mathf.MoveTowards(duck, 0f, Time.unscaledDeltaTime * 4f) : Mathf.MoveTowards(duck, 1f, Time.unscaledDeltaTime * 0.6f);
            musicSource.volume = s.masterVolume * s.musicVolume * musicBaseVolume * duck;
        }

        public void Play(string id, Vector2? worldPos = null, float volume = 1f, float pitch = 1f)
        {
            if (library == null || string.IsNullOrEmpty(id)) return;
            var e = library.Get(id);
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out var t) && now - t < e.minInterval) return;
            lastPlayed[id] = now;

            float vol = e.volume * volume;
            if (worldPos.HasValue)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float d = Vector2.Distance(cam.transform.position, worldPos.Value);
                    vol *= Mathf.Clamp01(1f - (d - 10f) / 14f);
                    if (vol <= 0.01f) return;
                }
            }
            var s = SaveSystem.Settings;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.clip = e.clips[Random.Range(0, e.clips.Length)];
            src.volume = Mathf.Clamp01(vol * s.masterVolume * s.sfxVolume);
            src.pitch = pitch * (1f + Random.Range(-e.pitchJitter, e.pitchJitter));
            src.Play();
        }

        public void PlayMusic(string id)
        {
            if (library == null) return;
            if (currentMusic == id && musicSource.isPlaying) return;
            currentMusic = id;
            var m = library.GetMusic(id);
            if (m == null || m.clip == null) { musicSource.Stop(); return; }
            musicBaseVolume = m.volume;
            musicSource.clip = m.clip;
            musicSource.Play();
        }

        public void StopMusic() { currentMusic = null; musicSource.Stop(); }

        /// <summary>The music fades out and stays silent until let go (the Blue Ocarina is being played); then it comes back slowly.</summary>
        public void HushMusic(bool on) { musicHushed = on; }
        public bool MusicHushed => musicHushed;

        /// <summary>Temporarily lowers music (e.g. on death / big moments).</summary>
        public void DuckMusic(float amount) { duck = Mathf.Clamp01(1f - amount); }

        public void SetMusicPitch(float p) { musicSource.pitch = p; }
    }

    /// <summary>Shorthand for gameplay code.</summary>
    public static class Sfx
    {
        public static void Play(string id, Vector2 pos, float volume = 1f, float pitch = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, pos, volume, pitch);
        }

        public static void PlayUI(string id, float volume = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, null, volume);
        }
    }
}
