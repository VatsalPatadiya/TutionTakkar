using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// iPhone: signing with the Techiebutler Apple team, the tuitiontakkar:// link scheme, and a one-click
    /// "Build iPhone (Xcode project)" into Builds/iOS. Open Builds/iOS/Unity-iPhone.xcodeproj in Xcode and press Run.
    /// </summary>
    static class IosSigning
    {
        public const string TeamId = "87CTM2H52V";
        // A fresh folder each build: Xcode projects from an older build can't simply be overwritten.
        static string OutDir => "Builds/iOS-" + System.DateTime.Now.ToString("MMdd-HHmm");

        [MenuItem("Tuition Takkar/Set iPhone signing team")]
        static void Apply()
        {
            PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.unitygame.tuitiontakkar");
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            var schemes = PlayerSettings.iOS.iOSUrlSchemes ?? new string[0];
            if (!schemes.Contains("tuitiontakkar")) PlayerSettings.iOS.iOSUrlSchemes = schemes.Concat(new[] { "tuitiontakkar" }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[TuitionTakkar] iOS settings: team " + TeamId + ", automatic signing, iPhone only, tuitiontakkar:// links");
        }

        [MenuItem("Tuition Takkar/Build iPhone (Xcode project)")]
        static void Build()
        {
            Apply();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string outDir = OutDir;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            var r = report.summary;
            string msg = "[TuitionTakkar] IPHONE build " + r.result + " in " + Mathf.RoundToInt((float)r.totalTime.TotalSeconds) + " s, errors " + r.totalErrors + " -> " + System.IO.Path.GetFullPath(outDir);
            Debug.Log(msg);
            System.IO.File.WriteAllText("Builds/ios-info.txt", msg);
            if (r.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(outDir + "/Unity-iPhone.xcodeproj");
        }
    }
}
