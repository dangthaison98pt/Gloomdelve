using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Gives the texts of the title screen a serif typeface taken from the system (the first of 'families' that is
    /// installed). The rest of the UI keeps the built-in font. If none is found the texts simply stay as they are.
    /// </summary>
    public class SerifFont : MonoBehaviour
    {
        public string[] families = { "Palatino Linotype", "Book Antiqua", "Georgia", "Constantia", "Times New Roman" };
        public Text[] texts;

        static Font cached;

        void Awake()
        {
            if (cached == null)
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                foreach (var f in families)
                    if (installed.Contains(f)) { cached = Font.CreateDynamicFontFromOSFont(f, 64); break; }
            }
            if (cached == null || texts == null) return;
            foreach (var t in texts) if (t != null) t.font = cached;
        }
    }
}
