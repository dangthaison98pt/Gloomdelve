using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    [CreateAssetMenu(menuName = "Gloomdelve/Prefab Registry", fileName = "PrefabRegistry")]
    public class PrefabRegistry : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public Entity prefab;
        }

        public PlayerController player;
        public List<Entry> entries = new List<Entry>();

        Dictionary<string, Entity> map;

        public Entity Get(string id)
        {
            if (map == null || map.Count != entries.Count)
            {
                map = new Dictionary<string, Entity>();
                foreach (var e in entries) if (e != null && e.prefab != null && !string.IsNullOrEmpty(e.id)) map[e.id] = e.prefab;
            }
            return id != null && map.TryGetValue(id, out var p) ? p : null;
        }
    }
}
