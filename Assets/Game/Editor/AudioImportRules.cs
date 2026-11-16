using UnityEditor;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// Every sound of the game is imported as mono (the game has no stereo content, and mono halves the memory).
    /// Applies to anything under Assets/Game/Audio, including clips added later.
    /// Normalize is off so forcing mono never changes the loudness the clips were authored with.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Game/Audio/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            var so = new SerializedObject(imp);
            var norm = so.FindProperty("m_Normalize");
            if (norm != null && norm.boolValue) { norm.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
