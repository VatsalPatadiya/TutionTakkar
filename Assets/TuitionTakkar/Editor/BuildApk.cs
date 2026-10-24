using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>Tuition Takkar ▸ Build Android APK: builds Builds/TuitionTakkar.apk from the scenes in Build Settings.</summary>
    static class BuildApk
    {
        const string Output = "Builds/TuitionTakkar.apk";
        const string PackageName = "com.unitygame.tuitiontakkar";

        [MenuItem("Tuition Takkar/Build Android APK")]
        static void Build()
        {
            // Firebase, Google sign-in and invite links are all registered for this package name.
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Output,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            string line = "[TuitionTakkar] APK build " + sum.result + " in " + (int)sum.totalTime.TotalSeconds + " s, " +
                          (sum.totalSize / (1024 * 1024)) + " MB, errors " + sum.totalErrors + " → " + Path.GetFullPath(Output);
            File.WriteAllText("Builds/last-build.txt", line + "\n");
            if (sum.result == BuildResult.Succeeded) Debug.Log(line); else Debug.LogError(line);
        }
    }
}
