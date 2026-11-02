#if UNITY_IOS
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Every iPhone build: Sign in with Apple, push notifications and invite links (Associated Domains)
    /// capabilities; Firebase Messaging from Swift Package Manager; GoogleService-Info.plist in the app.
    /// Nothing has to be clicked in Xcode by hand (the Xcode project is rebuilt each time).
    /// </summary>
    static class AppleXcodeSetup
    {
        static readonly string[] LinkDomains = { "applinks:tuitiontakkar.web.app", "applinks:penfighter-6be8b.web.app" };

        [PostProcessBuild(100)]
        static void OnBuilt(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            string fw = proj.GetUnityFrameworkTargetGuid();

            proj.AddFrameworkToProject(fw, "AuthenticationServices.framework", true);
            proj.AddFrameworkToProject(fw, "UserNotifications.framework", false);

            // Firebase Messaging (turns the Apple token into a messaging token). Linked into the MAIN app:
            // Xcode can't reliably build Swift-package libraries into UnityFramework ("Build input files cannot
            // be found: FirebaseMessaging.o"). TTPush.mm finds Firebase at run time, so UnityFramework doesn't need it.
            string pkg = proj.AddRemotePackageReferenceAtVersionUpToNextMajor("https://github.com/firebase/firebase-ios-sdk", "11.0.0");
            proj.AddRemotePackageFrameworkToProject(main, "FirebaseMessaging", pkg, false);
            proj.AddRemotePackageFrameworkToProject(main, "FirebaseCore", pkg, false);
            proj.AddBuildProperty(main, "OTHER_LDFLAGS", "-ObjC"); // keep Firebase's classes (nothing references them by name)

            // Firebase reads GoogleService-Info.plist from the app.
            const string plist = "GoogleService-Info.plist";
            if (File.Exists("Assets/" + plist))
            {
                File.Copy("Assets/" + plist, Path.Combine(path, plist), true);
                proj.AddFileToBuild(main, proj.AddFile(plist, plist));
            }
            else UnityEngine.Debug.LogWarning("[TuitionTakkar] Xcode: Assets/" + plist + " missing, iPhone notifications won't work");
            proj.WriteToFile(projPath);

            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/TuitionTakkar.entitlements", null, main);
            caps.AddSignInWithApple();
            caps.AddPushNotifications(true); // development for Xcode test builds; App Store export switches it to production
            caps.AddAssociatedDomains(LinkDomains);
            caps.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            caps.WriteToFile();

            MoveAppSettingsOffProject(projPath, main);

            // Unity's own "ReleaseForRunning" / "ReleaseForProfiling" configurations break Swift packages
            // (Xcode builds Firebase under "Release" and then can't find it). Run and Profile with plain Release.
            foreach (var scheme in Directory.GetFiles(Path.Combine(path, "Unity-iPhone.xcodeproj"), "*.xcscheme", SearchOption.AllDirectories))
            {
                string s = File.ReadAllText(scheme);
                string fixedScheme = System.Text.RegularExpressions.Regex.Replace(s,
                    "buildConfiguration\\s*=\\s*\"ReleaseFor(Running|Profiling)\"", "buildConfiguration=\"Release\"");
                if (fixedScheme != s) File.WriteAllText(scheme, fixedScheme);
            }
            UnityEngine.Debug.Log("[TuitionTakkar] Xcode: Sign in with Apple, push, invite links and Firebase added");
        }

        static readonly string[] AppOnlyKeys = { "PRODUCT_NAME", "INFOPLIST_FILE", "CODE_SIGN_ENTITLEMENTS" };

        /// <summary>
        /// Unity puts the app's name, Info.plist and entitlements at PROJECT level. Swift packages inherit
        /// project settings, so Firebase's resource bundles were built under the app's name and then
        /// "couldn't be opened because there is no such file". Moves exactly those three settings from the
        /// project to the app target (per configuration), as the very last step, straight in the file.
        /// </summary>
        static void MoveAppSettingsOffProject(string projPath, string mainTarget)
        {
            string t = File.ReadAllText(projPath);
            var cfgsOf = new System.Func<string, System.Collections.Generic.List<string>>(listId =>
            {
                var m = Regex.Match(t, Regex.Escape(listId) + @" /\*[^*]*\*/ = \{[^}]*?buildConfigurations = \(([^)]*)\);", RegexOptions.Singleline);
                var ids = new System.Collections.Generic.List<string>();
                if (m.Success) foreach (Match id in Regex.Matches(m.Groups[1].Value, @"\b[0-9A-F]{24}\b")) ids.Add(id.Value);
                return ids;
            });
            var projList = Regex.Match(t, @"isa = PBXProject;.*?buildConfigurationList = ([0-9A-F]{24})", RegexOptions.Singleline);
            var mainList = Regex.Match(t, Regex.Escape(mainTarget) + @" /\*[^*]*\*/ = \{\s*isa = PBXNativeTarget;.*?buildConfigurationList = ([0-9A-F]{24})", RegexOptions.Singleline);
            if (!projList.Success || !mainList.Success) { UnityEngine.Debug.LogWarning("[TuitionTakkar] Xcode: couldn't find build configurations"); return; }

            // Config block: "<id> /* Name */ = { isa = XCBuildConfiguration; buildSettings = { ... }; name = Name; };"
            Regex Block(string id) => new Regex(Regex.Escape(id) + @" /\* (\w+) \*/ = \{\s*isa = XCBuildConfiguration;\s*buildSettings = \{(.*?)\n\t\t\t\};", RegexOptions.Singleline);

            var moved = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            foreach (var id in cfgsOf(projList.Groups[1].Value))
            {
                var m = Block(id).Match(t);
                if (!m.Success) continue;
                var values = new System.Collections.Generic.Dictionary<string, string>();
                string body = m.Groups[2].Value;
                foreach (var key in AppOnlyKeys)
                {
                    var kv = Regex.Match(body, @"\n\t*" + key + @" = ([^;]*);");
                    if (kv.Success) { values[key] = kv.Groups[1].Value; body = body.Remove(kv.Index, kv.Length); }
                }
                body += "\n\t\t\t\tPRODUCT_NAME = \"$(TARGET_NAME)\";";
                moved[m.Groups[1].Value] = values;
                t = t.Substring(0, m.Groups[2].Index) + body + t.Substring(m.Groups[2].Index + m.Groups[2].Length);
            }
            foreach (var id in cfgsOf(mainList.Groups[1].Value))
            {
                var m = Block(id).Match(t);
                if (!m.Success || !moved.TryGetValue(m.Groups[1].Value, out var values)) continue;
                string body = m.Groups[2].Value;
                foreach (var kv in values)
                    if (!Regex.IsMatch(body, @"\n\t*" + kv.Key + @" = ")) body += "\n\t\t\t\t" + kv.Key + " = " + kv.Value + ";";
                t = t.Substring(0, m.Groups[2].Index) + body + t.Substring(m.Groups[2].Index + m.Groups[2].Length);
            }
            File.WriteAllText(projPath, t);
        }
    }
}
#endif
