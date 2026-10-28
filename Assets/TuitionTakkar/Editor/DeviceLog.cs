using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;

namespace TuitionTakkar.EditorTools
{
    /// <summary>Tuition Takkar ▸ Save Game Log from Connected Phones: the game's log lines to Builds/device-log.txt.</summary>
    static class DeviceLog
    {
        [MenuItem("Tuition Takkar/Save Game Log from Connected Phones")]
        static void Save()
        {
            var log = new StringBuilder();
            string adb = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb");
            foreach (var line in Run(adb, "devices").Split('\n'))
            {
                var parts = line.Trim().Split('\t');
                if (parts.Length != 2 || parts[1] != "device") continue;
                log.AppendLine("===== " + parts[0]);
                log.AppendLine(Run(adb, "-s " + parts[0] + " logcat -d -v time -s Unity:* TTPush:* FirebaseMessaging:* AndroidRuntime:E"));
            }
            File.WriteAllText("Builds/device-log.txt", log.ToString());
            UnityEngine.Debug.Log("[TuitionTakkar] device log saved to Builds/device-log.txt (" + log.Length + " chars)");
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
                p.WaitForExit(60000);
                return o;
            }
            catch (System.Exception e) { return "failed: " + e.Message; }
        }
    }
}
