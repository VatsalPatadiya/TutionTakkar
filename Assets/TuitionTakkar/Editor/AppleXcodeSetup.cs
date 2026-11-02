#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// iPhone build: adds the "Sign in with Apple" capability (entitlement) and the AuthenticationServices
    /// framework to the Xcode project, so nobody has to tick it by hand after every build.
    /// </summary>
    static class AppleXcodeSetup
    {
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
            proj.WriteToFile(projPath);

            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/TuitionTakkar.entitlements", null, main);
            caps.AddSignInWithApple();
            caps.WriteToFile();
            UnityEngine.Debug.Log("[TuitionTakkar] Xcode: Sign in with Apple capability added");
        }
    }
}
#endif
