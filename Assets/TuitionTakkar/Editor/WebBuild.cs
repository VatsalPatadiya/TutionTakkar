using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Tuition Takkar ▸ Build Web: the browser version of the game, written into LinkSite/public/play
    /// so Firebase Hosting serves it at https://penfighter-6be8b.web.app/play/?code=ABC123.
    /// Gzip with the decompression fallback, so it works without special hosting headers.
    /// Building switches the editor to the Web target; the APK menu switches back to Android.
    /// Result: Builds/web-info.txt
    /// </summary>
    static class WebBuild
    {
        const string Output = "LinkSite/public/play";
        const string Info = "Builds/web-info.txt";

        [MenuItem("Tuition Takkar/Build Web (into LinkSite/public/play)")]
        static void Build()
        {
            Directory.CreateDirectory("Builds");
            File.WriteAllText(Info, "building...");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(), locationPathName = Output, target = BuildTarget.WebGL, options = BuildOptions.None,
            });
            var sum = report.summary;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[TuitionTakkar] WEB build " + sum.result + " in " + (int)sum.totalTime.TotalSeconds + " s, errors " + sum.totalErrors);
            if (Directory.Exists(Output + "/Build"))
                foreach (var f in Directory.GetFiles(Output + "/Build"))
                    sb.AppendLine(Path.GetFileName(f) + "  " + (new FileInfo(f).Length / 1024) + " KB");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) sb.AppendLine("ERROR " + m.content);
            File.WriteAllText(Info, sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
