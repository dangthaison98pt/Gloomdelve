using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>Applies pixel-art import settings to generated PNGs (point filter, 16 PPU, pivot, border).</summary>
    public class ArtImport : AssetPostprocessor
    {
        public struct Settings
        {
            public Vector2 pivot;
            public Vector4 border;
            public bool smooth;   // modern UI art: bilinear filter, 100 PPU
        }

        public static readonly Dictionary<string, Settings> Pending = new Dictionary<string, Settings>();

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Game/Art/")) return;
            if (!Pending.TryGetValue(assetPath, out var s)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = s.smooth ? 100 : 16;
            ti.filterMode = s.smooth ? FilterMode.Bilinear : FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = false;
            var ts = new TextureImporterSettings();
            ti.ReadTextureSettings(ts);
            ts.spriteAlignment = (int)SpriteAlignment.Custom;
            ts.spritePivot = s.pivot;
            ts.spriteMeshType = SpriteMeshType.FullRect;
            ts.spriteBorder = s.border;
            ts.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(ts);
        }
    }
}
