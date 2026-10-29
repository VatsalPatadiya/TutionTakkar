using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Gets a Google ID token from the phone. Android uses Credential Manager through
    /// Plugins/Android/GoogleSignIn.androidlib. iPhone comes later (needs the Apple account).
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
#if UNITY_ANDROID && !UNITY_EDITOR
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
