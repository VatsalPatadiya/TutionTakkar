using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;

namespace TuitionTakkar.EditorTools
{
    /// <summary>Tuition Takkar ▸ Install APK on Connected Phones: adb install -r on every USB-connected phone.</summary>
    static class InstallApk
    {
        const string Apk = "Builds/TuitionTakkar.apk";
        const string Log = "Builds/install.txt";
        const string Package = "com.unitygame.tuitiontakkar";

        [MenuItem("Tuition Takkar/Install APK on Connected Phones")]
        static void Install()
        {
            var log = new StringBuilder();
            string adb = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb");
            if (!File.Exists(Apk)) log.AppendLine("no APK: build it first");
            else
            {
                string devices = Run(adb, "devices");
                log.AppendLine("$ adb devices\n" + devices);
                foreach (var line in devices.Split('\n'))
                {
                    var parts = line.Trim().Split('\t');
                    if (parts.Length != 2 || parts[1] != "device") continue;
                    string serial = parts[0];
                    log.AppendLine("$ adb -s " + serial + " install -r\n" + Run(adb, "-s " + serial + " install -r \"" + Path.GetFullPath(Apk) + "\""));
                    // Test phones: allow notifications and open the game once so it registers for push.
                    log.AppendLine("$ grant notifications\n" + Run(adb, "-s " + serial + " shell pm grant " + Package + " android.permission.POST_NOTIFICATIONS"));
                    log.AppendLine("$ launch\n" + Run(adb, "-s " + serial + " shell monkey -p " + Package + " -c android.intent.category.LAUNCHER 1"));
                }
            }
            File.WriteAllText(Log, log.ToString());
            UnityEngine.Debug.Log("[TuitionTakkar] install\n" + log);
        }

        [MenuItem("Tuition Takkar/Check Push on Connected Phones")]
        static void PushLog()
        {
            var log = new StringBuilder();
            string adb = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb");
            foreach (var line in Run(adb, "devices").Split('\n'))
            {
                var parts = line.Trim().Split('\t');
                if (parts.Length != 2 || parts[1] != "device") continue;
                log.AppendLine("== " + parts[0]);
                log.AppendLine(Run(adb, "-s " + parts[0] + " logcat -d -s TTPush:I FirebaseMessaging:* FirebaseInstanceId:* Unity:W"));
            }
            File.WriteAllText("Builds/push-log.txt", log.ToString());
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
