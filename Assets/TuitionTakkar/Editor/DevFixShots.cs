using System.Reflection;
using Unity.Services.Lobbies;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    static class DevFixShots
    {
        [MenuItem("Tuition Takkar/Dev: Room-closed + suggestion shots")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/fix-test");
            async System.Threading.Tasks.Task Shot(string n, int wait = 1000)
            {
                await System.Threading.Tasks.Task.Delay(wait);
                ScreenCapture.CaptureScreenshot("Builds/fix-test/" + n + ".png");
                await System.Threading.Tasks.Task.Delay(400);
            }
            const BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic;
            gm.ShowHome();
            var failed = typeof(OnlineScreens).GetMethod("JoinFailed", F);
            var e1 = new LobbyServiceException(LobbyExceptionReason.LobbyNotFound, "gone");
            gm.UI.ShowOverlay((VisualElement)failed.Invoke(null, new object[] { gm, e1, "ABC123", "Vatsal", false }));
            await Shot("closed");
            var e2 = new LobbyServiceException(LobbyExceptionReason.LobbyLocked, "locked");
            gm.UI.ShowOverlay((VisualElement)failed.Invoke(null, new object[] { gm, e2, "ABC123", "Vatsal", false }));
            await Shot("started");
            gm.UI.ShowOverlay((VisualElement)typeof(HomeScreen).GetMethod("Settings", F).Invoke(null, new object[] { gm }));
            await Shot("settings");
            gm.UI.ShowOverlay((VisualElement)typeof(SuggestBox).GetMethod("Box", F).Invoke(null, new object[] { gm, 0, "Add a pen that looks like a rocket!" }));
            await Shot("suggest");
            gm.ShowHome();
            Debug.Log("[fix shots] done");
        }
    }
}
