using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    static class DevResultShots
    {
        [MenuItem("Tuition Takkar/Dev: Result screen shots (v2)")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/ui-improved");
            foreach (var win in new[] { true, false })
            {
                string name = win ? "21-result-win" : "22-result-lose";
                try
                {
                    var rival = gm.Save.CurrentRival;
                    var s = new MatchSetup(2) { mode = MatchMode.Ladder, rival = rival };
                    s.isBot[1] = true;
                    s.pens[0] = Catalog.Pen("starter");
                    s.pens[1] = Catalog.Pen(rival.penId) ?? Catalog.Pen("zippy07");
                    s.names[0] = "You"; s.names[1] = rival.name;
                    var o = new MatchOutcome { setup = s, winner = win ? 0 : 1, score = win ? new[] { 3, 1 } : new[] { 1, 3 },
                        marksBefore = gm.Save.marks, marksAfter = gm.Save.marks + (win ? 120 : 2), nextRival = rival, rewards = new RewardResult() };
                    o.rewards.lines.Add(win ? new RewardLine { label = "Match win", note = "beat the rival", amount = 100 } : new RewardLine { label = "For trying", note = "lost the match", amount = 2 });
                    if (win) o.rewards.lines.Add(new RewardLine { label = "Knock-offs", note = "3 pens off", amount = 20 });
                    o.rewards.total = win ? 120 : 2;
                    gm.UI.ShowScreen(MatchEndScreen.Build(gm, o));
                }
                catch (System.Exception e) { Debug.LogWarning("[result v2] " + name + " FAILED " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace); }
                await System.Threading.Tasks.Task.Delay(1500);
                ScreenCapture.CaptureScreenshot("Builds/ui-improved/" + name + ".png");
                await System.Threading.Tasks.Task.Delay(800);
            }
            gm.ShowHome();
            Debug.Log("[result v2] done");
        }
    }
}
