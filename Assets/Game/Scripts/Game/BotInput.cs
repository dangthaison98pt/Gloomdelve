using System.Collections.Generic;

namespace Gloomdelve
{
    /// <summary>Replays a recorded list of inputs (from LevelValidator) through the real PlayerController.</summary>
    public class BotInput : IFrameInputSource
    {
        readonly List<FrameInput> frames;
        int index;

        public BotInput(List<FrameInput> frames) { this.frames = frames; }

        public bool Finished => index >= frames.Count;
        public int Index => index;
        public int Count => frames.Count;

        public FrameInput Next()
        {
            if (index >= frames.Count) return default;
            return frames[index++];
        }
    }
}
