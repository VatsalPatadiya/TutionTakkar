using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Very short buzzes. Android gets precise millisecond pulses; iOS only has
    /// the long system buzz without a native plugin, so it is used for big moments only.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;

        static AndroidJavaObject Vibrator
        {
            get
            {
                if (vibrator != null) return vibrator;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdk = version.GetStatic<int>("SDK_INT");
                return vibrator;
            }
        }
#endif

        /// <summary>A single pulse. amplitude 1..255 (Android 8+).</summary>
        public static void Pulse(int ms, int amplitude = 160, bool big = false)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var v = Vibrator;
                if (v == null)
                {
                    // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission.
                    if (big) Handheld.Vibrate();
                    return;
                }
                if (sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var e = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, Mathf.Clamp(amplitude, 1, 255)))
                        v.Call("vibrate", e);
                }
                else v.Call("vibrate", (long)ms);
            }
            catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            if (big) Handheld.Vibrate();
#endif
        }

        public static void AimTick() => Pulse(8, 90);
        public static void Release(float power) => Pulse(10 + (int)(power * 6f), 120 + (int)(power * 80f));
        public static void Clack(float speed) { if (speed > 0.45f) Pulse(9, 180); }
        public static void Fall() => Pulse(40, 255, true);
        public static void Win() => Pulse(60, 220, true);
    }
}
