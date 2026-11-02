#if UNITY_IOS
using System.IO;
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

            // Firebase Messaging (turns the Apple token into a messaging token).
            string pkg = proj.AddRemotePackageReferenceAtVersionUpToNextMajor("https://github.com/firebase/firebase-ios-sdk", "11.0.0");
            proj.AddRemotePackageFrameworkToProject(fw, "FirebaseMessaging", pkg, false);
            proj.AddRemotePackageFrameworkToProject(fw, "FirebaseCore", pkg, false);
            proj.AddBuildProperty(fw, "OTHER_LDFLAGS", "-ObjC");

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

            // Unity's own "ReleaseForRunning" / "ReleaseForProfiling" configurations break Swift packages
            // (Xcode builds Firebase under "Release" and then can't find it). Run and Profile with plain Release.
            foreach (var scheme in Directory.GetFiles(Path.Combine(path, "Unity-iPhone.xcodeproj"), "*.xcscheme", SearchOption.AllDirectories))
            {
                string s = File.ReadAllText(scheme);
                string fixedScheme = s.Replace("buildConfiguration = \"ReleaseForRunning\"", "buildConfiguration = \"Release\"")
                                      .Replace("buildConfiguration = \"ReleaseForProfiling\"", "buildConfiguration = \"Release\"");
                if (fixedScheme != s) File.WriteAllText(scheme, fixedScheme);
            }
            UnityEngine.Debug.Log("[TuitionTakkar] Xcode: Sign in with Apple, push, invite links and Firebase added");
        }
    }
}
#endif
