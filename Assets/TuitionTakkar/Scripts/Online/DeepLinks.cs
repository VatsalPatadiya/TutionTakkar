using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Room invites. Sharing builds a link like
    ///   https://penfighter-6be8b.web.app/join/ABC123?from=Vatsal
    /// Tapping it opens the game (Android App Link, or the landing page's "Open" button which
    /// uses tuitiontakkar://join/ABC123), and the game goes straight to Play Online with the
    /// Join Room sheet already filled in. One tap on JOIN and they're in.
    /// </summary>
    public static class DeepLinks
    {
        public const string Scheme = "tuitiontakkar";
        public const string Host = "penfighter-6be8b.web.app";

        static readonly Regex CodeRx = new Regex(@"(?:join(?:/|\?code=|\?c=)|[?&]code=)([A-Za-z0-9]{6})", RegexOptions.IgnoreCase);
        static readonly Regex FromRx = new Regex(@"[?&]from=([^&#]+)", RegexOptions.IgnoreCase);

        static GameManager gm;
        static string pendingCode, pendingFrom;
        static int pendingFriends = -1; // tapped a friend notification: Friends tab to open
        static bool ready;

        public static bool HasPending => !string.IsNullOrEmpty(pendingCode) || pendingFriends >= 0;

        /// <summary>Invite link for a room code.</summary>
        public static string InviteUrl(string code, string fromName)
        {
            string url = "https://" + Host + "/join/" + code.ToUpperInvariant();
            if (!string.IsNullOrEmpty(fromName)) url += "?from=" + Uri.EscapeDataString(fromName);
            return url;
        }

        /// <summary>Call in GameManager.Awake. Picks up a link the app was launched with.</summary>
        public static void Init(GameManager manager)
        {
            gm = manager;
            Application.deepLinkActivated -= OnLink;
            Application.deepLinkActivated += OnLink;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) Store(Application.absoluteURL);
        }

        /// <summary>
        /// Call whenever the home screen is shown (after the splash, after a match).
        /// Opens the join sheet if an invite is waiting.
        /// </summary>
        public static void OnHomeShown()
        {
            ready = true;
            TryOpen();
        }

        /// <summary>Open a link from elsewhere (a tapped push notification).</summary>
        public static void Open(string url) => OnLink(url);

        static void OnLink(string url)
        {
            Store(url);
            TryOpen();
        }

        static void Store(string url)
        {
            // tuitiontakkar://friends or tuitiontakkar://friends/requests (friend notifications)
            if (!string.IsNullOrEmpty(url) && url.IndexOf("://friends", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pendingFriends = url.IndexOf("requests", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
                return;
            }
            if (!Parse(url, out pendingCode, out pendingFrom)) return;
            Debug.Log("[TuitionTakkar] invite link for room " + pendingCode);
        }

        public static bool Parse(string url, out string code, out string from)
        {
            code = from = null;
            if (string.IsNullOrEmpty(url)) return false;
            var m = CodeRx.Match(url);
            if (!m.Success) return false;
            code = m.Groups[1].Value.ToUpperInvariant();
            var f = FromRx.Match(url);
            if (f.Success)
            {
                try { from = Uri.UnescapeDataString(f.Groups[1].Value.Replace('+', ' ')).Trim(); }
                catch (Exception) { from = null; }
                if (from != null && from.Length > 14) from = from.Substring(0, 14);
            }
            return true;
        }

        static void TryOpen()
        {
            if (!HasPending || gm == null || !ready) return;
            // Mid-match: keep it for the next time the home screen shows.
            if (gm.Match != null && gm.Match.Running) return;
            if (pendingFriends >= 0)
            {
                int tab = pendingFriends;
                pendingFriends = -1;
                FriendsUI.Show(gm, tab);
                return;
            }
            string code = pendingCode, from = pendingFrom;
            pendingCode = pendingFrom = null;
            OnlineScreens.JoinFromInvite(gm, code, from);
        }
    }
}
