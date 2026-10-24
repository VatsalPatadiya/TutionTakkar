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

        static void Party(int n)
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null)
            {
                Debug.LogWarning("Press Play first.");
                return;
            }
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
