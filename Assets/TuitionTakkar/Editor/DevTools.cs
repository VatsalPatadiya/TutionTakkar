using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Editor-only test helpers (not in builds).</summary>
    public static class DevTools
    {
        /// <summary>Play mode: a 3 or 4 pen desk on one screen (hot-seat), to check the party desk.</summary>
        [MenuItem("Tuition Takkar/Dev: 4-player desk (hot-seat)")]
        static void Party4() => Party(4);

        [MenuItem("Tuition Takkar/Dev: 3-player desk (hot-seat)")]
        static void Party3() => Party(3);

        [MenuItem("Tuition Takkar/Dev: 2-player desk (hot-seat)")]
        static void Party2() => Party(2);

        [MenuItem("Tuition Takkar/Dev: Skip turn clock to 8 s")]
        static void Skip() => MatchController.DevSetSeconds = 8f;

        /// <summary>Game view at a phone's portrait resolution (1170 x 2532), so layouts look like on a phone.</summary>
        [MenuItem("Tuition Takkar/Dev: Phone-size game view")]
        static void PhoneView()
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = single.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Android });
            var gType = group.GetType();
            int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var gs = gType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)gs.GetType().GetProperty("baseText").GetValue(gs) == "TT phone") { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kind = asm.GetType("UnityEditor.GameViewSizeType");
                var size = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new object[] { System.Enum.ToObject(kind, 1), 1170, 2532, "TT phone" });
                gType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = total;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(gv, new object[] { index, null });
        }

        [MenuItem("Tuition Takkar/Dev: Save game view screenshot")]
        static void Shot()
        {
            System.IO.Directory.CreateDirectory("Builds");
            ScreenCapture.CaptureScreenshot("Builds/editor-shot.png");
            Debug.Log("[TuitionTakkar] screenshot saved to Builds/editor-shot.png");
        }

        static void Party(int n)
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null)
            {
                Debug.LogWarning("Press Play first.");
                return;
            }
            Application.runInBackground = true; // keep playing while the editor isn't the front window
            var s = new MatchSetup(n) { mode = MatchMode.PassAndPlay };
            var ids = new[] { "starter", "zippy07", "trigrip", "gelglide" };
            for (int i = 0; i < n; i++)
            {
                s.pens[i] = Catalog.Pen(ids[i]);
                s.names[i] = "Player " + (i + 1);
            }
            gm.StartMatch(s);
        }
    }
}
