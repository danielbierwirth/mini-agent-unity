using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace VoiceAgent.Editor.Localization
{
    /// <summary>
    /// TextMeshPro setup for EN / KR / AR text: imports the TMP Essential Resources if missing, then builds the fallback
    /// font assets in Assets/Fonts and adds them to TMP's default font asset:
    ///
    /// - LiberationSans SDF (TMP Essentials, the TMP default): Latin base. Liberation Sans is metric-compatible with
    ///   Arial, the legacy uGUI Text default (LegacyRuntime.ttf), so English keeps its widths and layout after the migration.
    /// - NotoSansKR-Regular SDF: Korean fallback, dynamic with multi-atlas (11k Hangul syllables are added as they are used).
    /// - NotoSansArabic-Regular SDF: Arabic fallback, static, baked at 1024 x 1024 from Ranges/ArabicAll.txt (RTLTMPro's
    ///   list: Arabic letters, tashkeel, digits and the presentation forms RTLTMPro's shaping emits).
    ///
    /// Both are fallbacks of the base asset, after its own LiberationSans fallback: Korean first, then Arabic.
    /// </summary>
    [InitializeOnLoad]
    public static class RtlFontSetup
    {
        const string LatinFallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

        // Dynamic font assets keep every glyph Play mode rendered (Hangul especially), and the next asset save writes
        // them to disk. Clearing them after Play keeps the committed assets empty and git diffs clean; the glyphs are
        // generated again on demand.
        static RtlFontSetup() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ClearDynamicFonts();
        };

        public static void ClearDynamicFonts()
        {
            foreach (var path in new[] { KoreanPath, LatinFallbackPath })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (asset == null || asset.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
                asset.ClearFontAssetData(true);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        public const string Folder = "Assets/Fonts";
        public const string BasePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string KoreanPath = Folder + "/NotoSansKR-Regular SDF.asset";
        public const string ArabicPath = Folder + "/NotoSansArabic-Regular SDF.asset";
        const string ArabicRange = Folder + "/Ranges/ArabicAll.txt";
        const int ArabicAtlas = 1024;

        public static TMP_FontAsset Base => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BasePath);

        /// <summary>Imports TMP Essential Resources (TMP Settings, shaders) when the project has none.</summary>
        public static string ImportEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return "TMP Essential Resources already present";
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            return "importing TMP Essential Resources";
        }

        [MenuItem("VoiceAgent/Text/Build Font Assets")]
        static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null) return "TMP Settings missing: run ImportEssentials first";
            var log = new List<string>();
            var latin = Base;
            var korean = Create("NotoSansKR-Regular.otf", KoreanPath, 64, 6, 1024, true, log);
            var arabic = CreateArabic(log);

            var fallbacks = (latin.fallbackFontAssetTable ?? new List<TMP_FontAsset>())
                .Where(f => f != null && f.name != korean.name && f.name != arabic.name).ToList();
            fallbacks.Add(korean);
            fallbacks.Add(arabic);
            latin.fallbackFontAssetTable = fallbacks;
            EditorUtility.SetDirty(latin);

            var settings = new SerializedObject(Resources.Load<TMP_Settings>("TMP Settings"));
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = latin;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            log.Add($"{latin.name} fallbacks: {string.Join(", ", fallbacks.Select(f => f.name))}; TMP Settings default font asset = {latin.name}");
            return string.Join("\n", log);
        }

        static TMP_FontAsset Create(string fontFile, string path, int pointSize, int padding, int atlas, bool multiAtlas, List<string> log)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Folder}/{fontFile}");
            if (font == null) throw new FileNotFoundException($"{Folder}/{fontFile} is missing");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) AssetDatabase.DeleteAsset(path);

            var asset = TMP_FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA, atlas, atlas, AtlasPopulationMode.Dynamic, multiAtlas);
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTexture.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            log.Add($"{path}: dynamic, {atlas}x{atlas}, sampling {pointSize}pt, multi-atlas {multiAtlas}");
            return asset;
        }

        static TMP_FontAsset CreateArabic(List<string> log)
        {
            var unicodes = ParseRange(File.ReadAllText(ArabicRange));
            // The largest sampling size whose glyphs all fit one 1024 x 1024 atlas.
            foreach (var size in new[] { 72, 64, 56, 48 })
            {
                var asset = Create("NotoSansArabic-Regular.ttf", ArabicPath, size, 6, ArabicAtlas, false, new List<string>());
                asset.TryAddCharacters(unicodes, out var missing);
                // Code points the font has no glyph for at all are fine (U+FE75, U+FEFD, U+FEFE are unassigned);
                // any other missing one means the atlas is full at this size.
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{Folder}/NotoSansArabic-Regular.ttf");
                FontEngine.LoadFontFace(font, size);
                var unplaced = (missing ?? Array.Empty<uint>()).Where(u => FontEngine.TryGetGlyphIndex(u, out var glyph) && glyph != 0).ToArray();
                if (unplaced.Length > 0) continue;
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(asset);
                EditorUtility.SetDirty(asset.atlasTexture);
                AssetDatabase.SaveAssets();
                log.Add($"{ArabicPath}: static, {ArabicAtlas}x{ArabicAtlas}, sampling {size}pt, {asset.characterTable.Count} characters from ArabicAll.txt " +
                        $"(not in the font: {string.Join(" ", (missing ?? Array.Empty<uint>()).Select(u => $"U+{u:X4}"))})");
                return asset;
            }
            throw new InvalidOperationException("ArabicAll.txt doesn't fit a 1024 x 1024 atlas at 48pt");
        }

        /// <summary>RTLTMPro's range format: comma-separated hex code points and from-to ranges ("060C,0621-063A").</summary>
        public static uint[] ParseRange(string text)
        {
            var result = new List<uint>();
            foreach (var part in text.Trim('﻿', ' ', '\n', '\r').Split(','))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var dash = p.IndexOf('-');
                if (dash < 0)
                {
                    result.Add(uint.Parse(p, NumberStyles.HexNumber));
                    continue;
                }
                uint from = uint.Parse(p[..dash], NumberStyles.HexNumber), to = uint.Parse(p[(dash + 1)..], NumberStyles.HexNumber);
                for (var u = from; u <= to; u++) result.Add(u);
            }
            return result.Distinct().ToArray();
        }
    }
}
