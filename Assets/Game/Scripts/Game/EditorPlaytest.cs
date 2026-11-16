namespace Gloomdelve
{
    /// <summary>
    /// "Play this room" requests from the Room Template Editor. The request lives in the editor's SessionState so it
    /// survives the domain reload when play mode starts. In a player build this is always inactive.
    /// </summary>
    public static class EditorPlaytest
    {
#if UNITY_EDITOR
        const string K = "Gloomdelve.Playtest.";

        public static bool Active => UnityEditor.SessionState.GetBool(K + "active", false);
        public static int Seed => UnityEditor.SessionState.GetInt(K + "seed", 1);
        public static int StageIndex => UnityEditor.SessionState.GetInt(K + "stage", 0);
        public static string RoomName => UnityEditor.SessionState.GetString(K + "room", "");
        public static bool GodMode => UnityEditor.SessionState.GetBool(K + "god", false);

        public static void Begin(int seed, int stageIndex, string roomName, bool godMode)
        {
            UnityEditor.SessionState.SetBool(K + "active", true);
            UnityEditor.SessionState.SetInt(K + "seed", seed);
            UnityEditor.SessionState.SetInt(K + "stage", stageIndex);
            UnityEditor.SessionState.SetString(K + "room", roomName ?? "");
            UnityEditor.SessionState.SetBool(K + "god", godMode);
        }

        public static void End() => UnityEditor.SessionState.SetBool(K + "active", false);
#else
        public static bool Active => false;
        public static int Seed => 0;
        public static int StageIndex => 0;
        public static string RoomName => "";
        public static bool GodMode => false;
#endif
    }
}
