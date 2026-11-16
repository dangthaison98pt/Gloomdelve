using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Sfx Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips;
            [Range(0, 2)] public float volume = 1f;
            [Range(0, 0.5f)] public float pitchJitter = 0.06f;
            [Tooltip("Minimum seconds between two plays of this sound.")] public float minInterval = 0.03f;
        }

        [Serializable]
        public class Music
        {
            public string id;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 0.7f;
        }

        public List<Entry> sounds = new List<Entry>();
        public List<Music> music = new List<Music>();

        Dictionary<string, Entry> map;

        public Entry Get(string id)
        {
            if (map == null || map.Count != sounds.Count)
            {
                map = new Dictionary<string, Entry>();
                foreach (var e in sounds) if (e != null && !string.IsNullOrEmpty(e.id)) map[e.id] = e;
            }
            return map.TryGetValue(id, out var r) ? r : null;
        }

        public Music GetMusic(string id) => music.Find(m => m.id == id);
    }
}
