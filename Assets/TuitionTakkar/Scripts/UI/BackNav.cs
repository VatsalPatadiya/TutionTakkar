using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// The phone's back button / back gesture (Unity sees it as Escape):
    /// a pop-up closes (its Close / Cancel / Later... button), in a match it pauses (online: the
    /// "leave? your opponent wins" sheet), a screen goes back the way its own back arrow would,
    /// and on the home screen it asks before closing the game.
    /// </summary>
    public static class BackNav
    {
        public const string HomeName = "Home";
        public const string SplashName = "Splash";

        // Buttons that dismiss a pop-up without doing anything else.
        static readonly string[] CloseWords =
        {
            "close", "done", "ok", "cancel", "not now", "later", "maybe later", "keep playing", "ignore", "resume", "no", "back", "stay",
        };

        static float lastAt;

        public static void Handle(GameManager gm)
        {
            if (gm == null || gm.UI == null) return;
            // One gesture can arrive as two key presses on some phones.
            if (Time.unscaledTime - lastAt < 0.25f) return;
            lastAt = Time.unscaledTime;
            var ui = gm.UI;

            // 1. a pop-up on top
            if (ui.OverlayLayer.childCount > 0)
            {
                var top = ui.OverlayLayer[ui.OverlayLayer.childCount - 1];
                if (top.userData is Action back) { back(); return; }
                var b = FindCloseButton(top);
                if (b != null) { Click(b); return; }
                if (top.ClassListContains(UIRoot.SheetClass)) ui.HideOverlay();
                return; // busy spinners, round results: nothing to go back to
            }

            // 2. in a match
            if (ui.Hud.Root.style.display == DisplayStyle.Flex && ui.ScreenLayer.childCount == 0)
            {
                gm.Pause();
                return;
            }

            // 3. a screen
            if (ui.ScreenLayer.childCount == 0) return;
            var screen = ui.ScreenLayer[ui.ScreenLayer.childCount - 1];
            if (screen.name == SplashName) return;
            if (screen.name == HomeName) { ui.ShowOverlay(ExitSheet(gm)); return; }
            Button backBtn = null;
            screen.Query<Button>().ForEach(x => { if (backBtn == null && x.tooltip == "Back" && x.enabledInHierarchy) backBtn = x; });
            if (backBtn != null) { Click(backBtn); return; }
            gm.ShowHome();
        }

        static Button FindCloseButton(VisualElement root)
        {
            Button found = null;
            root.Query<Button>().ForEach(b =>
            {
                if (found != null || !b.enabledInHierarchy || b.resolvedStyle.display == DisplayStyle.None) return;
                string t = (b.text ?? "").Trim().ToLowerInvariant();
                foreach (var w in CloseWords) if (t == w) { found = b; return; }
            });
            return found;
        }

        /// <summary>Presses a button the same way a tap would.</summary>
        static void Click(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        static VisualElement ExitSheet(GameManager gm)
        {
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp("LEAVING ALREADY?", 28f, Pal.Ink));
            c.Add(UIKit.HandText("are you sure you want to exit the game?", 18f, Pal.Ink).St(s => s.marginTop = 6));
            var stay = UIKit.PrimaryBtn("STAY", () => gm.UI.HideOverlay(), 56f);
            stay.style.marginTop = 16;
            c.Add(stay);
            var exit = UIKit.Btn("EXIT", () =>
            {
                gm.Save.Save();
                Application.Quit();
            }, Color.clear, Pal.Red, UIKit.Display, 20f, 54f, 16f, null, Pal.Red);
            exit.style.marginTop = 10;
            c.Add(exit);
            return o;
        }
    }
}
