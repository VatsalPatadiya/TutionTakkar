using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Tuition Takkar ▸ Build Release APK and Install: a non-development APK signed with the release keystore
    /// (Player Settings ▸ Publishing Settings; the passwords are entered there by hand and never stored here),
    /// written to Builds/Release. Prints the signing certificate fingerprints (needed in Firebase for Google
    /// sign-in), then updates the app on every connected phone (uninstalling first only if the old one
    /// was signed with a different key), allows notifications and launches it.
    /// </summary>
    static class ReleaseApk
    {
        const string Dir = "Builds/Release";
        const string Output = Dir + "/TuitionTakkar-release.apk";
        const string Info = Dir + "/release-info.txt";
        const string Package = "com.unitygame.tuitiontakkar";

        [MenuItem("Tuition Takkar/Build Release APK")]
        static void BuildOnly() => Build(false);

        [MenuItem("Tuition Takkar/Build Release APK and Install")]
        static void BuildAndInstall() => Build(true);

        static void Build(bool install)
        {
            Directory.CreateDirectory(Dir);
            var log = new StringBuilder();
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystoreName) ||
                string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName) ||
                string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                Write("NOT BUILT: set the release keystore, both passwords and the key alias in Player Settings > Publishing Settings first.");
                return;
            }

            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, Package);
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(), locationPathName = Output, target = BuildTarget.Android, options = BuildOptions.None,
            });
            var sum = report.summary;
            log.AppendLine("[TuitionTakkar] RELEASE APK build " + sum.result + " in " + (int)sum.totalTime.TotalSeconds + " s, errors " +
                           sum.totalErrors + " -> " + Path.GetFullPath(Output));
            log.AppendLine("version " + PlayerSettings.bundleVersion + " (code " + PlayerSettings.Android.bundleVersionCode + "), alias " +
                           PlayerSettings.Android.keyaliasName);
            if (sum.result != BuildResult.Succeeded) { Write(log.ToString()); return; }

            string keytool = Path.Combine(AndroidExternalToolsSettings.jdkRootPath, "bin", "keytool");
            log.AppendLine("\n$ signing certificate\n" + Run(keytool, "-printcert -jarfile \"" + Path.GetFullPath(Output) + "\""));

            if (!install) { Write(log.ToString()); return; }
            string adb = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb");
            string devices = Run(adb, "devices");
            log.AppendLine("$ adb devices\n" + devices);
            foreach (var line in devices.Split('\n'))
            {
                var parts = line.Trim().Split('\t');
                if (parts.Length != 2 || parts[1] != "device") continue;
                string serial = parts[0];
                log.AppendLine("== " + serial);
                // Update in place (keeps the phone's game data). Only when the old app was signed with a
                // different key (the debug test builds) does it have to be uninstalled first.
                string r = Run(adb, "-s " + serial + " install -r \"" + Path.GetFullPath(Output) + "\"");
                log.AppendLine("$ install -r\n" + r);
                if (r.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") || r.Contains("signatures do not match"))
                {
                    log.AppendLine("$ uninstall (old build signed with another key)\n" + Run(adb, "-s " + serial + " uninstall " + Package));
                    log.AppendLine("$ install\n" + Run(adb, "-s " + serial + " install \"" + Path.GetFullPath(Output) + "\""));
                }
                log.AppendLine("$ grant notifications\n" + Run(adb, "-s " + serial + " shell pm grant " + Package + " android.permission.POST_NOTIFICATIONS"));
                Run(adb, "-s " + serial + " shell monkey -p " + Package + " -c android.intent.category.LAUNCHER 1");
            }
            Write(log.ToString());
        }

        static void Write(string text)
        {
            File.WriteAllText(Info, text);
            UnityEngine.Debug.Log(text);
        }

        static string Run(string exe, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                });
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(180000);
                return o;
            }
            catch (System.Exception e) { return "failed: " + e.Message; }
        }
    }
}
