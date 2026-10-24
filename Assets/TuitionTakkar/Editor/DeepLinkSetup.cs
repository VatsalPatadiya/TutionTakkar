using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Invite links (see DeepLinks.cs):
    ///  • Android: adds two intent filters to the game's main activity in the generated Gradle
    ///    project: tuitiontakkar://join/… and the verified App Link https://penfighter-6be8b.web.app/join/…
    ///  • iOS: registers the tuitiontakkar:// URL scheme in Player Settings.
    /// </summary>
    class DeepLinkSetup : IPostGenerateGradleAndroidProject, IPreprocessBuildWithReport
    {
        const string Scheme = "tuitiontakkar";
        const string Host = "penfighter-6be8b.web.app";
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 10;

        [InitializeOnLoadMethod]
        static void EnsureIosScheme() => EditorApplication.delayCall += EnsureIosSchemeNow;

        public void OnPreprocessBuild(BuildReport report) => EnsureIosSchemeNow();

        static void EnsureIosSchemeNow()
        {
            var schemes = PlayerSettings.iOS.iOSUrlSchemes ?? new string[0];
            if (!schemes.Contains(Scheme))
                PlayerSettings.iOS.iOSUrlSchemes = schemes.Concat(new[] { Scheme }).ToArray();
        }

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var candidates = new[]
            {
                Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml"),
                Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml"),
            };
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                if (Patch(path)) return;
            }
            UnityEngine.Debug.LogWarning("[TuitionTakkar] invite links: couldn't find the main activity in the Android manifest.");
        }

        static bool Patch(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);

            XmlElement activity = null;
            foreach (XmlElement a in doc.SelectNodes("/manifest/application/activity", ns))
            {
                if (a.SelectSingleNode("intent-filter/category[@android:name='android.intent.category.LAUNCHER']", ns) != null)
                {
                    activity = a;
                    break;
                }
            }
            if (activity == null) return false;

            if (activity.SelectSingleNode("intent-filter/data[@android:scheme='" + Scheme + "']", ns) == null)
                activity.AppendChild(Filter(doc, false, Scheme, "join", null));
            if (activity.SelectSingleNode("intent-filter/data[@android:host='" + Host + "']", ns) == null)
                activity.AppendChild(Filter(doc, true, "https", Host, "/join"));

            doc.Save(path);
            return true;
        }

        static XmlElement Filter(XmlDocument doc, bool autoVerify, string scheme, string host, string pathPrefix)
        {
            var f = doc.CreateElement("intent-filter");
            if (autoVerify) f.SetAttribute("autoVerify", AndroidNs, "true");
            f.AppendChild(Named(doc, "action", "android.intent.action.VIEW"));
            f.AppendChild(Named(doc, "category", "android.intent.category.DEFAULT"));
            f.AppendChild(Named(doc, "category", "android.intent.category.BROWSABLE"));
            var d = doc.CreateElement("data");
            d.SetAttribute("scheme", AndroidNs, scheme);
            d.SetAttribute("host", AndroidNs, host);
            if (pathPrefix != null) d.SetAttribute("pathPrefix", AndroidNs, pathPrefix);
            f.AppendChild(d);
            return f;
        }

        static XmlElement Named(XmlDocument doc, string tag, string name)
        {
            var e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }
    }
}
