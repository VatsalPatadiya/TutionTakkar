using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    static class DevIdeaCardShot
    {
        [MenuItem("Tuition Takkar/Dev: Me tab idea card shot")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/fix-test");
            bool was = gm.Save.googleLinked;
            gm.Save.googleLinked = true; // look as a signed-in player (not saved)
            ProfileScreens.ShowProfile(gm, () => gm.ShowHome());
            await System.Threading.Tasks.Task.Delay(800);
            var sv = gm.UI.ScreenLayer.Q<ScrollView>();
            if (sv != null) sv.scrollOffset = new Vector2(0, 99999);
            await System.Threading.Tasks.Task.Delay(800);
            ScreenCapture.CaptureScreenshot("Builds/fix-test/me-idea.png");
            await System.Threading.Tasks.Task.Delay(500);
            gm.Save.googleLinked = was;
            gm.ShowHome();
            Debug.Log("[idea card shot] done");
        }
    }
}
