using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    static class DevFaceShots
    {
        static void SetTab(string t) =>
            typeof(FaceEditor).GetField("tab", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, t);
        static void Repaint(GameManager gm) =>
            typeof(FaceEditor).GetMethod("Paint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { gm });

        [MenuItem("Tuition Takkar/Dev: Face shots (home, intro, editor)")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/face-test");
            async System.Threading.Tasks.Task Shot(string n, int wait = 1800)
            {
                await System.Threading.Tasks.Task.Delay(wait);
                ScreenCapture.CaptureScreenshot("Builds/face-test/" + n + ".png");
                await System.Threading.Tasks.Task.Delay(500);
                Debug.Log("[face shots] " + n);
            }
            gm.Save.faceIntroSeen = false;
            gm.ShowHome();
            await Shot("1-home-intro", 2600);
            gm.UI.HideOverlay();
            await Shot("2-home", 1200);
            FaceEditor.Show(gm);
            await Shot("3-editor-face");
            SetTab("hair"); Repaint(gm);
            await Shot("4-editor-hair", 2600);
            SetTab("hat"); Repaint(gm);
            await Shot("5-editor-hat", 2200);
            SetTab("bg"); Repaint(gm);
            await Shot("6-editor-back", 2000);
            ProfileScreens.ShowProfile(gm, () => gm.ShowHome());
            await Shot("7-profile");
            gm.ShowHome();
            Debug.Log("[face shots] done");
        }
    }
}
