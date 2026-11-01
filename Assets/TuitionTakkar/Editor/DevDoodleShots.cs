using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    static class DevDoodleShots
    {
        [MenuItem("Tuition Takkar/Dev: Doodle + VS spin shots")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/doodle-test");
            async System.Threading.Tasks.Task Shot(string n, int wait)
            {
                await System.Threading.Tasks.Task.Delay(wait);
                ScreenCapture.CaptureScreenshot("Builds/doodle-test/" + n + ".png");
                await System.Threading.Tasks.Task.Delay(400);
            }
            // All icons.
            var sheet = UIKit.Col().St(s => { s.backgroundColor = Pal.Paper; UIKit.Pad(s, 40, 20, 40, 20); s.justifyContent = Justify.Center; });
            sheet.pickingMode = PickingMode.Position;
            var kinds = (DoodleIcon.Kind[])System.Enum.GetValues(typeof(DoodleIcon.Kind));
            for (int row = 0; row < 4; row++)
            {
                var r = UIKit.Row().St(s => { s.justifyContent = Justify.Center; s.marginBottom = 20; });
                for (int k = 0; k < 3; k++)
                {
                    int i = row * 3 + k;
                    var disc = UIKit.El().St(s => { s.width = 110; s.height = 110; UIKit.Radius(s, 55); s.marginLeft = s.marginRight = 10; s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                        s.backgroundColor = i < 6 ? DoodleIcon.StickerBacks[i] : Color.white; UIKit.Border(s, 3f, Pal.Ink); });
                    disc.Add(new DoodleIcon(kinds[i], 80f));
                    r.Add(disc);
                }
                sheet.Add(r);
            }
            gm.UI.ShowScreen(sheet);
            await Shot("icons", 800);

            // VS intro with a classmate landing.
            var s2 = new MatchSetup(2) { mode = MatchMode.Online, localIndex = 0, desk = Desks.All[2] };
            s2.pens[0] = s2.pens[1] = Catalog.Pen(Catalog.StarterId);
            s2.names[0] = "You"; s2.names[1] = "Riya_07";
            s2.faces = new[] { AvatarParts.MyCode(gm.Save), AvatarParts.Random("riya").ToString() };
            s2.levels = new[] { 6, 8 };
            gm.UI.ShowScreen(VsScreen.Intro(s2, true));
            await Shot("vs-spin", 450);
            await Shot("vs-landed", 2600);
            gm.ShowHome();
            Debug.Log("[doodle shots] done");
        }
    }
}
