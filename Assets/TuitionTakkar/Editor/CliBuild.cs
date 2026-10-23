using System.IO;
using UnityEditor;

namespace TuitionTakkar.EditorTools
{
    static class CliBuild
    {
        public static void BuildAndroid()
        {
            PlayerSettings.applicationIdentifier = "com.techiebutler.tuitiontakkar";
            var outDir = "Build/Android";
            Directory.CreateDirectory(outDir);
            var outPath = outDir + "/TuitionTakkar.apk";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            var summary = report.summary;
            UnityEngine.Debug.Log($"Build result: {summary.result}, errors={summary.totalErrors}, size={summary.totalSize}, path={summary.outputPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
