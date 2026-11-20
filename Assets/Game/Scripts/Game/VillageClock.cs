using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Brindlemoor keeps the player's own time: by the clock of their machine it is day in the village, or dusk, or
    /// night, or dawn. Only the light changes - the sky, the colour and strength of the daylight, how much the
    /// player's own lantern adds - and it is settled once, when the village is built (it does not creep while the
    /// player stands in the street). The village's own settings (AreaConfig) are its night.
    /// </summary>
    public static class VillageClock
    {
        public enum Part { Night, Dawn, Day, Dusk }

        /// <summary>An hour to use instead of the machine's ("-villageTest H" on the command line, tests); -1 = the real one.</summary>
        public static int testHour = -1;
        /// <summary>Where along the street to stand with "-villageTest" ("-villageX N", in cells); 0 = the middle.</summary>
        public static float testX;

        public static int Hour => testHour >= 0 ? testHour % 24 : System.DateTime.Now.Hour;

        /// <summary>Dawn 5-6, day 7-16, dusk 17-18, night 19-4.</summary>
        public static Part PartOf(int hour)
        {
            if (hour >= 7 && hour <= 16) return Part.Day;
            if (hour == 5 || hour == 6) return Part.Dawn;
            if (hour == 17 || hour == 18) return Part.Dusk;
            return Part.Night;
        }

        /// <summary>The village stage as it is at this hour: the same stage, its area a copy with that hour's light.</summary>
        public static StageDef For(StageDef village, int hour)
        {
            var part = PartOf(hour);
            if (village == null || village.area == null || part == Part.Night) return village;
            var a = Object.Instantiate(village.area);
            a.name = village.area.name;
            if (part == Part.Day)
            {
                a.skyTint = Color.white;
                a.ambientColor = new Color(1f, 0.97f, 0.9f); a.ambientIntensity = 1.05f;
                a.playerLightIntensity = 0f;
                a.cameraBackground = new Color(0.56f, 0.74f, 0.92f);
            }
            else
            {
                // the sun low: warm light, long shadows; a little rosier in the morning
                bool dawn = part == Part.Dawn;
                a.skyTint = dawn ? new Color(1f, 0.78f, 0.74f) : new Color(1f, 0.68f, 0.5f);
                a.ambientColor = dawn ? new Color(1f, 0.86f, 0.82f) : new Color(1f, 0.8f, 0.66f); a.ambientIntensity = 0.82f;
                a.playerLightIntensity = 0.1f;
                a.cameraBackground = dawn ? new Color(0.7f, 0.56f, 0.6f) : new Color(0.72f, 0.46f, 0.38f);
            }
            var st = village.Named(village.displayName);
            st.area = a;
            return st;
        }

        /// <summary>The middle of the village street (for looking at the village from the command line).</summary>
        public static bool Street(TileGrid g, out Vector2 at)
        {
            at = default;
            if (g == null) return false;
            int col = Mathf.Min(g.Width - 2, 5), y = g.Height - 1;
            while (y > 0 && g.IsSolid(col, y)) y--;
            while (y > 0 && !g.IsSolid(col, y)) y--;
            at = new Vector2(testX > 0f ? Mathf.Clamp(testX, 2f, g.Width - 3f) : g.Width * 0.5f, y + 1);
            return y + 1 < g.Height - 1;
        }
    }
}
