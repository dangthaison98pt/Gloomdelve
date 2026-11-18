namespace Gloomdelve
{
    public static class GameConst
    {
        /// <summary>Fixed simulation step. The motor, the level validator and the bot all use exactly this value.</summary>
        public const float Dt = 1f / 60f;
        public const int PPU = 16;
        /// <summary>The series is named after the hero's village; each game adds its own title after the colon.</summary>
        public const string SeriesName = "Brindlemoor";
        public const string PartName = "The Sunstone Hunt";
        public const string GameName = SeriesName + ": " + PartName;
        public const string HeroName = "Brann";
        public const string Version = "0.1.0";
        public const string SceneMenu = "MainMenu";
        public const string SceneGame = "Game";
    }

    /// <summary>Area: generated level. Boss: hand-made arena, exit sealed until the boss dies. Rest: camp with a chest and a shop.</summary>
    public enum LevelKind { Area, Final, Tutorial, Boss, Rest }

    public enum DamageType { Generic, Whip, Stomp, Thrown, Explosion, Spikes, Arrow, Fall, Crush, Fire, Contact, Wraith, Falling }

    public struct DamageInfo
    {
        public int amount;
        public DamageType type;
        public Entity source;
        public UnityEngine.Vector2 knockback;
        public string cause;

        public DamageInfo(int amount, DamageType type, Entity source, UnityEngine.Vector2 knockback, string cause)
        {
            this.amount = amount; this.type = type; this.source = source; this.knockback = knockback; this.cause = cause;
        }
    }
}
