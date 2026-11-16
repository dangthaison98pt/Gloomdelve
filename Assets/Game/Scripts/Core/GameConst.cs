namespace Gloomdelve
{
    public static class GameConst
    {
        /// <summary>Fixed simulation step. The motor, the level validator and the bot all use exactly this value.</summary>
        public const float Dt = 1f / 60f;
        public const int PPU = 16;
        public const string GameName = "Gloomdelve";
        public const string Version = "0.1.0";
        public const string SceneMenu = "MainMenu";
        public const string SceneGame = "Game";
    }

    public enum LevelKind { Area, Final, Tutorial }

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
