using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// App icon "L1 · Chalkboard TT" for Android and iPhone.
    /// The PNGs live in Assets/TuitionTakkar/Art/AppIcon. They are applied automatically the
    /// first time the project opens (and any time from Tuition Takkar > Apply App Icons).
    ///   icon-1024.png                 default icon: every iPhone size, the App Store, Android legacy
    ///   icon-round-512.png            Android round icon
    ///   icon-adaptive-background.png  Android 8+ adaptive icon (art kept inside the safe circle)
    ///   icon-adaptive-foreground.png  empty top layer
    /// </summary>
    public static class AppIcons
    {
        public const string Dir = "Assets/TuitionTakkar/Art/AppIcon/";

        [InitializeOnLoadMethod]
        static void AutoApply() => EditorApplication.delayCall += () => Apply(false);

        [MenuItem("Tuition Takkar/Apply App Icons")]
        static void ApplyFromMenu() => Apply(true);

        static Texture2D Load(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + file);

        static void Apply(bool force)
        {
            var main = Load("icon-1024.png");
            var round = Load("icon-round-512.png");
            var bg = Load("icon-adaptive-background.png");
            var fg = Load("icon-adaptive-foreground.png");
            if (main == null || round == null || bg == null || fg == null) return; // not imported yet

            var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (!force && current != null && current.Length > 0 && current[0] == main) return; // already set

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { main }, IconKind.Any);
            SetAndroid(AndroidPlatformIconKind.Legacy, main);
            SetAndroid(AndroidPlatformIconKind.Round, round);
            SetAndroid(AndroidPlatformIconKind.Adaptive, bg, fg);
            AssetDatabase.SaveAssets();
            Debug.Log("[TuitionTakkar] App icons applied (Android legacy, round and adaptive; default icon for iPhone).");
        }

        static void SetAndroid(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
    }

    /// <summary>Icon PNGs import at full quality, uncompressed and unscaled.</summary>
    class AppIconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AppIcons.Dir)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.isReadable = true;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
