using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>Raw game screens for the store screenshots (Builds/store-raw), framed with captions afterwards.</summary>
    static class DevStoreShots
    {
        [MenuItem("Tuition Takkar/Dev: Store screenshots (raw)")]
        static async void Run()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory("Builds/store-raw");
            async System.Threading.Tasks.Task Shot(string n, int wait)
            {
                await System.Threading.Tasks.Task.Delay(wait);
                gm.UI.Hud.SetDebugShot(""); // editor-only "slow frame" note
                ScreenCapture.CaptureScreenshot("Builds/store-raw/" + n + ".png");
                await System.Threading.Tasks.Task.Delay(500);
                Debug.Log("[store shots] " + n);
            }
            void Aim(int index, Vector3 toward, float power)
            {
                var pen = gm.World.Pens[index];
                if (pen == null) return;
                var c = pen.Rb.position;
                var dir = toward - c; dir.y = 0f; dir.Normalize();
                Vector3 grab = Flick.StrikePoint(pen.Def, c, pen.Rb.rotation, 0.15f);
                Vector3 finger = grab - dir * (0.11f * GameConfig.WorldScale) * (0.4f + power);
                finger.y = 0f;
                gm.Aim.Show(grab, finger, dir, power, pen.Length * 0.5f);
            }
            var gen = PenParts.Generated;
            PenDef G(int i) => gen[(i * 97) % gen.Count];

            // 1. A 2-player match, pulling back to flick.
            gm.StartPractice(1);
            await System.Threading.Tasks.Task.Delay(2600);
            Aim(0, gm.World.Pens[1].Rb.position, 0.92f);
            await Shot("1-aim", 250);
            gm.Aim.Hide();

            // 2. 4 players on the deeper party desk, someone lining up a shot.
            var s4 = new MatchSetup(4) { mode = MatchMode.Online, localIndex = 0, net = null, desk = Desks.All[6], bot = Catalog.PracticeBots[1] };
            string[] names = { "You", "Riya_07", "Kabir", "Guest_x9f2" };
            for (int i = 0; i < 4; i++) { s4.pens[i] = i == 0 ? Catalog.Pen(gm.Save.equipped) ?? G(3) : G(i * 5 + 1); s4.names[i] = names[i]; s4.isBot[i] = i > 0; }
            gm.StartMatch(s4);
            await System.Threading.Tasks.Task.Delay(2800);
            Aim(0, gm.World.Pens[2].Rb.position, 0.85f);
            await Shot("2-party", 250);
            gm.Aim.Hide();
            gm.ShowHome();

            // 3. VS screen with the pot.
            var s2 = new MatchSetup(2) { mode = MatchMode.Online, localIndex = 0, desk = Desks.All[5] };
            s2.pens[0] = s2.pens[1] = Catalog.Pen(Catalog.StarterId);
            s2.names[0] = "You"; s2.names[1] = "Riya_07";
            s2.faces = new[] { AvatarParts.MyCode(gm.Save), AvatarParts.Random("riya-store").ToString() };
            s2.levels = new[] { 12, 14 };
            gm.UI.ShowScreen(VsScreen.Intro(s2, false));
            await Shot("3-vs", 2600);

            // 4. Face maker on the hair tab.
            FaceEditor.Show(gm);
            typeof(FaceEditor).GetField("tab", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, "hair");
            typeof(FaceEditor).GetMethod("Paint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { gm });
            await Shot("4-face", 2600);

            // 5. Pen maker.
            gm.ShowPouch(2);
            await Shot("5-pen", 1600);

            // 6. Desks.
            DeskScreens.ShowChooser(gm, DeskScreens.Mode.Quick);
            await Shot("6-desks", 1600);

            // 7. Home.
            gm.ShowHome();
            gm.UI.HideOverlay();
            await Shot("7-home", 1400);
            Debug.Log("[store shots] done");
        }

        [MenuItem("Tuition Takkar/Set iPhone only (no iPad)")]
        static void IphoneOnly()
        {
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            AssetDatabase.SaveAssets();
            Debug.Log("[TuitionTakkar] iOS target: iPhone only");
        }
    }
}
