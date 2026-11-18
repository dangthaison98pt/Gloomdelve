using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// The game's typeface: EB Garamond (SIL Open Font License, the file ships with the game in Assets/Game/Fonts)
    /// turned into a TextMesh Pro font asset, plus the three looks every text uses:
    ///   plain  - nothing behind the letters,
    ///   shadow - a soft dark halo so text stays readable over the game,
    ///   title  - a heavier drop shadow for big headings.
    /// Arrows and other signs the typeface lacks come from TMP's Liberation Sans fallback.
    /// </summary>
    public static class TmpFonts
    {
        const string Dir = "Assets/Game/Fonts/";
        const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

        public static TMP_FontAsset font;
        public static Material plain, shadow, title, menu;

        public static void Build()
        {
            string path = Dir + "Serif SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Font>(Dir + "EBGaramond.ttf");
                if (src == null) throw new System.Exception("Font file missing: " + Dir + "EBGaramond.ttf");
                font = TMP_FontAsset.CreateFontAsset(src, 96, 10, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "Serif SDF";
                AssetDatabase.CreateAsset(font, path);
                font.material.name = "Serif SDF Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                font.atlasTextures[0].name = "Serif SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            }
            var full = Shader.Find("TextMeshPro/Distance Field");
            if (full != null) font.material.shader = full;
            var sb = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            foreach (int code in new[] { 0xA0, 0xB7, 0x2014, 0x2013, 0x2019, 0x2018, 0x201C, 0x201D, 0x2026 }) sb.Append((char)code);
            font.TryAddCharacters(sb.ToString(), out _);
            var fb = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            font.fallbackFontAssetTable = fb != null ? new List<TMP_FontAsset> { fb } : new List<TMP_FontAsset>();
            font.boldSpacing = 2.5f;          // TMP spreads bold letters out by 7 by default: too loose for figures
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);

            plain = font.material;
            shadow = Preset("Serif SDF - Shadow", new Color(0.02f, 0.03f, 0.1f, 0.92f), 0.35f, -0.35f, 0.45f, 0.3f);
            title = Preset("Serif SDF - Title", new Color(0.02f, 0.03f, 0.1f, 0.8f), 0.5f, -0.9f, 0.2f, 0.25f);
            // the words of the title-screen list: no hard edge, just a soft drop shadow (the dark shade behind
            // the list is what lifts them off the snowy picture)
            menu = Preset("Serif SDF - Menu", new Color(0.02f, 0.03f, 0.1f, 0.85f), 0.4f, -0.7f, 0.25f, 0.35f);
            menu.DisableKeyword("OUTLINE_ON");
            menu.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            menu.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
        }

        static Material Preset(string name, Color color, float ox, float oy, float dilate, float softness)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = font.material.shader;
            m.CopyPropertiesFromMaterial(font.material);
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, ox);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, oy);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
