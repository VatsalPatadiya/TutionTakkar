using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Gets a Google ID token. Android uses Credential Manager through
    /// Plugins/Android/GoogleSignIn.androidlib. The web build asks the page (WebGLTemplates/TT,
    /// Google Identity Services) through Plugins/WebGL/TTGoogle.jslib, with the same web client ID,
    /// so the token works for the same Unity account. iPhone comes later (needs the Apple account).
    /// </summary>
    public static class GoogleSignIn
    {
        /// <summary>Web client ID from Firebase (penfighter). Unity Authentication checks tokens against it.</summary>
        public const string WebClientId = "239004855800-8l4c9jht8gndk954s4g2ar89bh29u0tu.apps.googleusercontent.com";

        public struct Result
        {
            public string idToken;
            public string displayName;
        }

        public class SignInException : Exception
        {
            public readonly string Code;
            public SignInException(string code, string message) : base(message) { Code = code; }
            public bool Cancelled => Code == "cancelled";
        }

        public static bool Supported =>
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public static Task<Result> RequestAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<Result>();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var bridge = new AndroidJavaClass("com.unitygame.googlesignin.GoogleSignInBridge"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    bridge.CallStatic("signIn", activity, WebClientId, new Listener(tcs));
                }
            }
            catch (Exception e)
            {
                tcs.TrySetException(new SignInException("failed", e.Message));
            }
            return tcs.Task;
#elif UNITY_WEBGL && !UNITY_EDITOR
            webPending?.TrySetException(new SignInException("cancelled", "replaced"));
            webPending = new TaskCompletionSource<Result>();
            var task = webPending.Task;
            TT_GoogleSignIn(WebClientId, OnWebResult);
            return task;
#else
            return Task.FromException<Result>(new SignInException("unsupported", "Google sign-in works in the phone build."));
#endif
        }

        public static void SignOut()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var bridge = new AndroidJavaClass("com.unitygame.googlesignin.GoogleSignInBridge"))
                    bridge.CallStatic("signOut", player.GetStatic<AndroidJavaObject>("currentActivity"));
            }
            catch (Exception e) { Debug.LogWarning("Google sign-out: " + e.Message); }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        public delegate void WebCallback(int ok, string text);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void TT_GoogleSignIn(string clientId, WebCallback callback);
        static TaskCompletionSource<Result> webPending;

        // ok = 1: "idToken\ndisplayName"; ok = 0: "code|message" (cancelled, webview, failed).
        [AOT.MonoPInvokeCallback(typeof(WebCallback))]
        static void OnWebResult(int ok, string text)
        {
            var tcs = webPending;
            webPending = null;
            if (tcs == null) return;
            text = text ?? "";
            if (ok == 1)
            {
                int nl = text.IndexOf('\n');
                tcs.TrySetResult(new Result { idToken = nl < 0 ? text : text.Substring(0, nl), displayName = nl < 0 ? "" : text.Substring(nl + 1) });
            }
            else
            {
                int bar = text.IndexOf('|');
                tcs.TrySetException(new SignInException(bar < 0 ? "failed" : text.Substring(0, bar), bar < 0 ? text : text.Substring(bar + 1)));
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        class Listener : AndroidJavaProxy
        {
            readonly TaskCompletionSource<Result> tcs;
            public Listener(TaskCompletionSource<Result> tcs) : base("com.unitygame.googlesignin.GoogleSignInBridge$Listener") { this.tcs = tcs; }

            // Called by Java on a background thread.
            void onSuccess(string idToken, string displayName) =>
                MainThread.Run(() => tcs.TrySetResult(new Result { idToken = idToken, displayName = displayName }));

            void onError(string code, string message) =>
                MainThread.Run(() => tcs.TrySetException(new SignInException(code, message)));
        }
#endif
    }
}
