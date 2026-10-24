using System;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Firebase Cloud Messaging (Android, Plugins/Android/Push.androidlib). Every phone joins
    /// the "all" topic, so a campaign sent from the Firebase console to topic "all" reaches
    /// every player. A notification can carry custom data key "link"
    /// (e.g. tuitiontakkar://join/ABC123), which opens that screen when tapped.
    /// iOS comes later (needs the Apple developer account for APNs).
    /// </summary>
    public static class Push
    {
        const string Bridge = "com.unitygame.push.PushBridge";

        public static void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(Bridge))
                {
                    bridge.CallStatic("init", activity);
                }
            }
            catch (Exception e) { Debug.LogWarning("Push init: " + e.Message); }
#endif
            CheckTappedLink();
        }

        /// <summary>The game was opened by tapping a notification that carried a link.</summary>
        public static void CheckTappedLink()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(Bridge))
                {
                    string link = bridge.CallStatic<string>("takeLink", activity);
                    if (!string.IsNullOrEmpty(link)) DeepLinks.Open(link);
                }
            }
            catch (Exception e) { Debug.LogWarning("Push link: " + e.Message); }
#endif
        }
    }
}
