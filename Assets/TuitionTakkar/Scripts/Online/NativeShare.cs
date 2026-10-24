using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// The phone's own share sheet (WhatsApp, Telegram, Messages, Instagram, copy…).
    /// Android: an ACTION_SEND chooser. iOS: UIActivityViewController (Plugins/iOS/TTShare.mm).
    /// Editor: copies the text and logs it.
    /// </summary>
    public static class NativeShare
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TT_Share(string text);
#endif

        /// <summary>Opens the share sheet. False if it couldn't (the text is copied instead).</summary>
        public static bool Share(string text, string subject = "Tuition Takkar")
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject).Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Invite a classmate"))
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                TT_Share(text);
                return true;
#else
                GUIUtility.systemCopyBuffer = text;
                Debug.Log("[TuitionTakkar] share sheet (editor, copied):\n" + text);
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("Share failed: " + e.Message);
                GUIUtility.systemCopyBuffer = text;
                return false;
            }
        }
    }
}
