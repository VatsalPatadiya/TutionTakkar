using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Starts offline 3- and 4-player matches against bots and screenshots the desk (no marks spent).</summary>
    static class DevDeskShots
    {
        [MenuItem("Tuition Takkar/Dev: Party desk shots (3 and 4 players)")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/desk-test");
            string[] pens = { "gel", "click", "marker", "fountain" };
            foreach (int n in new[] { 4, 3 })
            {
                var s = new MatchSetup(n) { mode = MatchMode.Online, localIndex = 0, net = null, desk = Desks.All[0], bot = Catalog.PracticeBots[0] };
                for (int i = 0; i < n; i++)
                {
                    s.pens[i] = Catalog.Pen(i == 0 ? gm.Save.equipped : pens[i]) ?? Catalog.Pen(Catalog.StarterId);
                    s.names[i] = i == 0 ? "YOU" : "Bot " + i;
                    s.isBot[i] = i > 0;
                }
                gm.StartMatch(s);
                await System.Threading.Tasks.Task.Delay(2500);
                ScreenCapture.CaptureScreenshot("Builds/desk-test/party" + n + ".png");
                await System.Threading.Tasks.Task.Delay(600);
                Debug.Log("[desk shots] " + n);
            }
            gm.ShowHome();
            Debug.Log("[desk shots] done");
        }
    }
}
