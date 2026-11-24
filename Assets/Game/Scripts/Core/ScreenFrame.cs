using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The 16:9 frame the game is laid out in. A screen narrower than 16:9 (a portrait screen, 16:10, 4:3) gets the
    /// game and every screen of it in a 16:9 band across its middle, with dark bars above and below, so it looks
    /// just as it does on a wide screen. A wider screen uses all of it (the frame is the whole screen).
    /// Anything that turns a screen position into something else goes through the frame.
    /// </summary>
    public static class ScreenFrame
    {
        public static int W => Screen.width;
        public static int H => Mathf.Min(Screen.height, Mathf.RoundToInt(Screen.width * 9f / 16f));
        /// <summary>Bottom edge of the frame, in pixels from the bottom of the screen.</summary>
        public static int Y0 => (Screen.height - H) / 2;
        public static bool Letterboxed => H < Screen.height;
        /// <summary>The frame in screen pixels.</summary>
        public static Rect PixelRect => new Rect(0f, Y0, W, H);
        /// <summary>The frame as a camera viewport (0..1 of the screen).</summary>
        public static Rect Viewport => new Rect(0f, Y0 / (float)Mathf.Max(1, Screen.height), 1f, H / (float)Mathf.Max(1, Screen.height));
        /// <summary>Screen pixels to pixels inside the frame (origin at its bottom left).</summary>
        public static Vector2 ToFrame(Vector2 screen) => new Vector2(screen.x, screen.y - Y0);
    }
}
