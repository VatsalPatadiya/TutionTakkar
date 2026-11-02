using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Sign in with Apple (iPhone and iPad only). Plugins/iOS/TTAppleSignIn.mm shows Apple's sheet and
    /// returns an identity token, which OnlineServices gives to Unity Authentication. Errors use the same
    /// exception as Google, so the screens treat both the same way (cancelled = no message).
    /// </summary>
    public static class AppleSignIn
    {
        public struct Result
        {
            public string idToken;
            public string firstName; // only on the very first Apple sign-in
        }

#if UNITY_IOS && !UNITY_EDITOR
        delegate void Callback(int status, string token, string name, string error);
        [DllImport("__Internal")] static extern int TT_AppleSupported();
        [DllImport("__Internal")] static extern void TT_AppleSignIn(Callback cb);

        static TaskCompletionSource<Result> pending;

        [AOT.MonoPInvokeCallback(typeof(Callback))]
        static void OnResult(int status, string token, string name, string error)
        {
            var tcs = pending;
            pending = null;
            if (tcs == null) return;
            MainThread.Run(() =>
            {
                if (status == 1) tcs.TrySetResult(new Result { idToken = token, firstName = name });
                else if (status == 2) tcs.TrySetException(new GoogleSignIn.SignInException("cancelled", "cancelled"));
                else tcs.TrySetException(new GoogleSignIn.SignInException("failed", string.IsNullOrEmpty(error) ? "Apple sign-in didn't work" : error));
            });
        }
#endif

        public static bool Supported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try { return TT_AppleSupported() == 1; } catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        public static Task<Result> RequestAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            pending?.TrySetException(new GoogleSignIn.SignInException("cancelled", "replaced"));
            pending = new TaskCompletionSource<Result>();
            var t = pending.Task;
            try { TT_AppleSignIn(OnResult); }
            catch (Exception e) { pending = null; return Task.FromException<Result>(new GoogleSignIn.SignInException("failed", e.Message)); }
            return t;
#else
            return Task.FromException<Result>(new GoogleSignIn.SignInException("unsupported", "Sign in with Apple works on iPhone."));
#endif
        }
    }
}
