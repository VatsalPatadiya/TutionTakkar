using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    static class DevPenShots
    {
        static void SetStep(int s) =>
            typeof(WorkshopScreen).GetField("step", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, s);

        [MenuItem("Tuition Takkar/Dev: Pen maker shots")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/pen-test");
            async System.Threading.Tasks.Task Shot(string n, int wait = 1500)
            {
                await System.Threading.Tasks.Task.Delay(wait);
                ScreenCapture.CaptureScreenshot("Builds/pen-test/" + n + ".png");
                await System.Threading.Tasks.Task.Delay(500);
                Debug.Log("[pen shots] " + n);
            }
            for (int s = 0; s < 4; s++)
            {
                SetStep(s);
                gm.ShowPouch(2);
                await Shot("step" + s);
            }
            SetStep(0);
            gm.ShowHome();
            Debug.Log("[pen shots] done");
        }
    }
}
