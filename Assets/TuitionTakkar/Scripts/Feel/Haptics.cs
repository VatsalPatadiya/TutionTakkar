using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Concurrent;
using System.Threading;
#endif

namespace TuitionTakkar
{
    /// <summary>
    /// Very short buzzes. Android gets precise millisecond pulses; iOS only has
    /// the long system buzz without a native plugin, so it is used for big moments only.
    ///
    /// On Android the vibrator call goes to a system service and can block for tens of
    /// milliseconds on some phones, so pulses are sent from a background thread, never
    /// from the frame that is drawing your aim.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        struct Job { public int ms, amp; public bool big; }

        static readonly BlockingCollection<Job> jobs = new BlockingCollection<Job>(new ConcurrentQueue<Job>(), 8);
        static Thread worker;
        static AndroidJavaObject vibrator;
        static int sdk;
        static bool noVibrator;

        static void EnsureWorker()
        {
            if (worker != null) return;
            worker = new Thread(Run) { IsBackground = true, Name = "Haptics" };
            worker.Start();
        }

        static void Run()
        {
            AndroidJNI.AttachCurrentThread();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdk = version.GetStatic<int>("SDK_INT");
            }
            catch (System.Exception) { vibrator = null; }
            noVibrator = vibrator == null;

            foreach (var j in jobs.GetConsumingEnumerable())
            {
                if (vibrator == null) continue;
                try
                {
                    if (sdk >= 26)
                    {
                        using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var e = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)j.ms, Mathf.Clamp(j.amp, 1, 255)))
                            vibrator.Call("vibrate", e);
                    }
                    else vibrator.Call("vibrate", (long)j.ms);
                }
                catch (System.Exception) { }
            }
        }
#endif

        /// <summary>A single pulse. amplitude 1..255 (Android 8+).</summary>
        public static void Pulse(int ms, int amplitude = 160, bool big = false)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (noVibrator)
            {
                // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission.
                if (big) Handheld.Vibrate();
                return;
            }
            EnsureWorker();
            jobs.TryAdd(new Job { ms = ms, amp = amplitude, big = big }); // dropped if 8 are already waiting
#elif UNITY_IOS && !UNITY_EDITOR
            if (big) Handheld.Vibrate();
#endif
        }

        public static void AimTick() => Pulse(8, 90);

        static float lastStretch;
        /// <summary>
        /// While pulling the pen back: one tick per 10% of power (step 1..10), longer and
        /// stronger as the pull grows, and a firm bump at full power. Spaced at least 35 ms apart.
        /// </summary>
        public static void Stretch(int step)
        {
            if (step >= 10) { Pulse(28, 230); lastStretch = Time.unscaledTime; return; }
            if (Time.unscaledTime - lastStretch < 0.035f) return;
            lastStretch = Time.unscaledTime;
            Pulse(10 + step, 70 + step * 14);
        }
        public static void Release(float power) => Pulse(10 + (int)(power * 6f), 120 + (int)(power * 80f));
        public static void Clack(float speed) { if (speed > 0.45f) Pulse(9, 180); }
        public static void Fall() => Pulse(40, 255, true);
        public static void Win() => Pulse(60, 220, true);
    }
}
