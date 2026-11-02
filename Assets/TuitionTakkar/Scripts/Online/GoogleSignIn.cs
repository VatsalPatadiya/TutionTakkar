using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Gets a Google ID token from the phone. Android uses Credential Manager through
    /// Plugins/Android/GoogleSignIn.androidlib. iPhone opens Google's page in Apple's secure sign-in
    /// browser (Plugins/iOS/TTGoogleSignIn.mm); Google returns the token to
    /// https://tuitiontakkar.web.app/auth/google, which hands it back to the app.
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

        /// <summary>The page Google sends the iPhone sign-in result to (must be an authorized redirect URI of the web client).</summary>
        public const string IosRedirect = "https://tuitiontakkar.web.app/auth/google";

        public static bool Supported =>
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
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
#elif UNITY_IOS && !UNITY_EDITOR
            iosPending?.TrySetException(new SignInException("cancelled", "replaced"));
            iosPending = new TaskCompletionSource<Result>();
            var t = iosPending.Task;
            string nonce = Guid.NewGuid().ToString("N");
            string url = "https://accounts.google.com/o/oauth2/v2/auth"
                + "?client_id=" + Uri.EscapeDataString(WebClientId)
                + "&redirect_uri=" + Uri.EscapeDataString(IosRedirect)
                + "&response_type=id_token"
                + "&scope=" + Uri.EscapeDataString("openid profile")
                + "&nonce=" + nonce
                + "&prompt=select_account";
            try { TT_GoogleSignIn(url, OnIosResult); }
            catch (Exception e) { iosPending = null; return Task.FromException<Result>(new SignInException("failed", e.Message)); }
            return t;
#else
            return Task.FromException<Result>(new SignInException("unsupported", "Google sign-in works in the phone build."));
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        delegate void IosCallback(int status, string token, string error);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void TT_GoogleSignIn(string authUrl, IosCallback cb);
        static TaskCompletionSource<Result> iosPending;

        [AOT.MonoPInvokeCallback(typeof(IosCallback))]
        static void OnIosResult(int status, string token, string error)
        {
            var tcs = iosPending;
            iosPending = null;
            if (tcs == null) return;
            MainThread.Run(() =>
            {
                if (status == 1) tcs.TrySetResult(new Result { idToken = token, displayName = NameIn(token) });
                else if (status == 2) tcs.TrySetException(new SignInException("cancelled", "cancelled"));
                else tcs.TrySetException(new SignInException("failed", string.IsNullOrEmpty(error) ? "Google sign-in didn't work" : error));
            });
        }
#endif

        [Serializable] class Claims { public string name; public string given_name; }

        /// <summary>The player's first name inside a Google ID token (only to suggest a game name).</summary>
        static string NameIn(string idToken)
        {
            try
            {
                var parts = idToken.Split('.');
                if (parts.Length < 2) return "";
                string b = parts[1].Replace('-', '+').Replace('_', '/');
                b = b.PadRight(b.Length + (4 - b.Length % 4) % 4, '=');
                var c = JsonUtility.FromJson<Claims>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b)));
                return !string.IsNullOrEmpty(c.given_name) ? c.given_name : c.name ?? "";
            }
            catch (Exception) { return ""; }
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
