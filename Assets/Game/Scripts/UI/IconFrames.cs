using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// For every generated picture, the part of its sheet that is actually drawn on (0..1 across the sheet). The
    /// screen uses it to show a thing's picture large and in the middle of its square (HUD.FitIcon): a small
    /// thing drawn low in a big sheet would otherwise sit small in a corner. Written by the project builder.
    /// </summary>
    public class IconFrames : ScriptableObject
    {
        public List<Sprite> sprites = new List<Sprite>();
        public List<Rect> frames = new List<Rect>();
        Dictionary<Sprite, Rect> map;

        public bool Find(Sprite s, out Rect frame)
        {
            if (map == null)
            {
                map = new Dictionary<Sprite, Rect>();
                for (int i = 0; i < sprites.Count && i < frames.Count; i++) if (sprites[i] != null) map[sprites[i]] = frames[i];
            }
            if (s != null && map.TryGetValue(s, out frame)) return true;
            frame = new Rect(0f, 0f, 1f, 1f);
            return false;
        }
    }
}
