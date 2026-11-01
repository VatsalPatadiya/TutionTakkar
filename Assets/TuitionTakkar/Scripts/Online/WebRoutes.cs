using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Browser only: each web screen gets its own address, and the browser's back button works.
    ///   /play/              home
    ///   /play/quick         Quick Match waiting room (open /play/quick?players=3 to start one straight away)
    ///   /play/room/ABC123   a friends' room (opening it joins that room)
    ///   /play/result        the end-of-match card
    /// Does nothing in the phone app or the editor.
    /// </summary>
    public static class WebRoutes
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TT_WebInit();
        [DllImport("__Internal")] static extern void TT_WebGo(string path, int replace);
        [DllImport("__Internal")] static extern int TT_WebBackCount();
        public const bool On = true;
#else
        static void TT_WebInit() { }
        static void TT_WebGo(string path, int replace) { }
        static int TT_WebBackCount() => 0;
        public const bool On = false;
#endif
        public const string PlayStore = "https://play.google.com/store/apps/details?id=com.unitygame.tuitiontakkar";

        static readonly Regex QuickRx = new Regex(@"/play/quick(?:\?.*?players=([2-4]))?", RegexOptions.IgnoreCase);

        static GameManager gm;
        static int pendingQuick;     // seats for a Quick Match asked for by the address
        static bool polling, handlingBack;
        static string current = "/play/";

        /// <summary>Call in GameManager.Awake (after DeepLinks.Init).</summary>
        public static void Init(GameManager manager)
        {
            gm = manager;
            if (!On) return;
            TT_WebInit();
            var url = Application.absoluteURL ?? "";
            var m = QuickRx.Match(url);
            if (m.Success) pendingQuick = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 2;
        }

        /// <summary>Home is on screen: start the Quick Match the address asked for (once).</summary>
        public static void OnHome()
        {
            if (!On || gm == null) return;
            StartPolling();
            Go("/play/");
            if (pendingQuick <= 0) return;
            int n = pendingQuick;
            pendingQuick = 0;
            OnlineScreens.QuickFromWeb(gm, n);
        }

        public static void Room(OnlineRoom room)
        {
            if (room == null) return;
            Go(room.IsQuick || string.IsNullOrEmpty(room.Code) ? "/play/quick" : "/play/room/" + room.Code);
        }

        public static void Result() => Go("/play/result");

        public static void Go(string path)
        {
            if (!On || path == current) return;
            current = path;
            TT_WebGo(path, handlingBack ? 1 : 0);
        }

        static void StartPolling()
        {
            if (polling || gm == null || gm.UI == null || gm.UI.Root == null) return;
            polling = true;
            gm.UI.Root.schedule.Execute(Poll).Every(150);
        }

        /// <summary>The browser's back button was pressed.</summary>
        static void Poll()
        {
            if (TT_WebBackCount() <= 0) return;
            if (gm.Match != null && gm.Match.Running)
            {
                // No leaving a match by accident: put the address back.
                var keep = current; current = ""; Go(keep);
                return;
            }
            handlingBack = true;
            try
            {
                var room = OnlineRoom.Current;
                current = "";
                if (room != null && !room.Closed) OnlineScreens.Leave(gm, room);
                gm.ShowHome();
            }
            finally { handlingBack = false; }
        }
    }
}
