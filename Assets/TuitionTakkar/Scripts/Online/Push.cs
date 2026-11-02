using System;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Firebase Cloud Messaging (Android, Plugins/Android/Push.androidlib). Every phone joins
    /// the "all" topic, so a campaign sent from the Firebase console to topic "all" reaches
    /// every player. A notification can carry custom data key "link"
    /// (e.g. tuitiontakkar://join/ABC123), which opens that screen when tapped.
    /// iPhone: Unity Mobile Notifications asks permission and gets the Apple (APNs) token;
    /// Plugins/iOS/TTPush.mm hands it to Firebase, which returns the same kind of token as
    /// Android, so the same Cloud Code scripts reach both. Taps and in-game arrivals go through
    /// iOSNotificationCenter.
    /// </summary>
    public static class Push
    {
        /// <summary>iPhone: where friend notifications stand, shown in Settings (helps when they don't arrive).</summary>
        public static string Status = "sign in to get friend notifications";

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void TT_PushInit();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void TT_PushSetApns(string hex);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern string TT_PushToken();

        static readonly System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> iosInbox =
            new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
        static bool iosForeground = true, askedPermission;
        static string apnsToken = "";
        static string lastTapped = "";

        /// <summary>Asks for notification permission once (signed-in players, when the friends features start).</summary>
        public static void EnsurePermission()
        {
            if (askedPermission) return;
            askedPermission = true;
            Status = "asking iPhone for permission…";
            MainThreadRun(AskPermission());
        }

        static System.Collections.IEnumerator AskPermission()
        {
            using (var req = new Unity.Notifications.iOS.AuthorizationRequest(
                Unity.Notifications.iOS.AuthorizationOption.Alert | Unity.Notifications.iOS.AuthorizationOption.Badge | Unity.Notifications.iOS.AuthorizationOption.Sound, true))
            {
                float t0 = Time.realtimeSinceStartup;
                while (!req.IsFinished && Time.realtimeSinceStartup - t0 < 60f) yield return null;
                if (!string.IsNullOrEmpty(req.DeviceToken))
                {
                    Status = "got the iPhone token, saving…";
                    apnsToken = req.DeviceToken;
                    try { TT_PushSetApns(req.DeviceToken); } catch (Exception e) { Debug.LogWarning("Push apns: " + e.Message); }
                    Friends.UploadPushToken(); // the Apple token: the server sends iPhone notifications straight to Apple
                }
                else
                {
                    Status = !req.Granted ? "turned off: allow them in iPhone Settings › Tuition Takkar"
                           : "allowed, but Apple sent no token" + (string.IsNullOrEmpty(req.Error) ? " (check the internet)" : ": " + req.Error);
                    Debug.Log("[TuitionTakkar] iPhone notifications: " + (req.Granted ? "no device token yet" : "not allowed") + " " + req.Error);
                }
            }
            // Firebase turns the Apple token into a messaging token a moment later: save it when it appears.
            for (int i = 0; i < 30 && string.IsNullOrEmpty(Token()); i++) yield return new WaitForSecondsRealtime(1f);
            if (!string.IsNullOrEmpty(Token())) Friends.UploadPushToken();
        }

        static void MainThreadRun(System.Collections.IEnumerator routine)
        {
            var host = UnityEngine.Object.FindFirstObjectByType<MainThread>();
            if (host != null) host.StartCoroutine(routine);
            else if (GameManager.I != null) GameManager.I.StartCoroutine(routine);
        }
        /// <summary>iPhone: the Apple (APNs) device token, hex ("" before permission).</summary>
        public static string ApnsToken() => apnsToken ?? "";
#else
        /// <summary>Android asks at start-up; nothing to do here.</summary>
        public static void EnsurePermission() { }
        public static string ApnsToken() => "";
#endif

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
#elif UNITY_IOS && !UNITY_EDITOR
            try { TT_PushInit(); } catch (Exception e) { Debug.LogWarning("Push init: " + e.Message); }
            // A notification while the game is open: an in-game pop-up instead of a banner (like Android).
            Unity.Notifications.iOS.iOSNotificationCenter.OnRemoteNotificationReceived += n =>
            {
                var d = n.UserInfo != null ? new System.Collections.Generic.Dictionary<string, string>(n.UserInfo) : new System.Collections.Generic.Dictionary<string, string>();
                if (iosForeground && d.Count > 0) iosInbox.Add(d);
            };
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
#elif UNITY_IOS && !UNITY_EDITOR
            try { return TT_PushToken() ?? ""; } catch (Exception e) { Debug.LogWarning("Push token: " + e.Message); }
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
#elif UNITY_IOS && !UNITY_EDITOR
            iosForeground = on;
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
#elif UNITY_IOS && !UNITY_EDITOR
            list.AddRange(iosInbox);
            iosInbox.Clear();
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
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                var n = Unity.Notifications.iOS.iOSNotificationCenter.GetLastRespondedNotification();
                if (n == null || n.Identifier == lastTapped) return;
                lastTapped = n.Identifier;
                if (n.UserInfo != null && n.UserInfo.TryGetValue("link", out var link) && !string.IsNullOrEmpty(link)) DeepLinks.Open(link);
            }
            catch (Exception e) { Debug.LogWarning("Push link: " + e.Message); }
#endif
        }
    }
}
