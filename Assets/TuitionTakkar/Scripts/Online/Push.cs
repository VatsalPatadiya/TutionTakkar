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

        /// <summary>This phone's notification token ("" in the editor or before Firebase is ready).</summary>
        public static string Token()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass(Bridge))
                    return bridge.CallStatic<string>("token") ?? "";
            }
            catch (Exception e) { Debug.LogWarning("Push token: " + e.Message); }
#endif
            return "";
        }

        /// <summary>Tell Android whether the game is on screen (open game: invites become pop-ups, not tray notifications).</summary>
        public static void SetForeground(bool on)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass(Bridge))
                    bridge.CallStatic("setForeground", on);
            }
            catch (Exception e) { Debug.LogWarning("Push foreground: " + e.Message); }
#endif
        }

        /// <summary>Notifications that arrived while the game was open, as key/value data (empty when none).</summary>
        public static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> TakeInbox()
        {
            var list = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string all;
                using (var bridge = new AndroidJavaClass(Bridge))
                    all = bridge.CallStatic<string>("takeInbox");
                if (string.IsNullOrEmpty(all)) return list;
                foreach (var msg in all.Split('\u001d'))
                {
                    var d = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (var kv in msg.Split('\u001e'))
                    {
                        int i = kv.IndexOf('\u001f');
                        if (i > 0) d[kv.Substring(0, i)] = kv.Substring(i + 1);
                    }
                    if (d.Count > 0) list.Add(d);
                }
            }
            catch (Exception e) { Debug.LogWarning("Push inbox: " + e.Message); }
#endif
            return list;
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
