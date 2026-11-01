using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    static class DevBotTest
    {
        /// <summary>Starts a 2-player Quick Match on the home desk, alone, and screenshots the bot fill-in.</summary>
        [MenuItem("Tuition Takkar/Dev: Quick match alone (bot test)")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/bot-test");
            if (!OnlineServices.SignedIn) await OnlineServices.SignInGuestAsync(gm.Save);
            var t = typeof(OnlineScreens);
            var seats = t.GetField("seats", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (seats != null) seats.SetValue(null, 2);
            t.GetMethod("DoQuick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
             .Invoke(null, new object[] { gm, Desks.Home });
            foreach (var (ms, name) in new[] { (12000, "1-waiting"), (16000, "2-intro"), (6000, "3-match"), (12000, "4-bot-turn") })
            {
                await System.Threading.Tasks.Task.Delay(ms);
                ScreenCapture.CaptureScreenshot("Builds/bot-test/" + name + ".png");
                Debug.Log("[bot test] " + name);
            }
            Debug.Log("[bot test] done");
        }
    }
}
