using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Unity Gaming Services in one place: start-up, sign-in (silent guest session first,
    /// then Google linked onto it so nothing is lost), the public name, and the
    /// "total_wins" leaderboard.
    /// </summary>
    public static class OnlineServices
    {
        public const string LeaderboardId = "total_wins";
        /// <summary>Same wins, reset every Monday 00:00 (set up in the Unity dashboard).</summary>
        public const string WeeklyId = "weekly_wins";
        public const int NameMin = 3, NameMax = 14;

        static Task initTask;

        public static IAuthenticationService Auth => AuthenticationService.Instance;
        public static bool SignedIn => UnityServices.State == ServicesInitializationState.Initialized && Auth.IsSignedIn;
        public static string PlayerId => SignedIn ? Auth.PlayerId : "";

        /// <summary>Starts the services and restores the saved session (or makes a guest one).</summary>
        public static Task InitAsync()
        {
            if (initTask == null || initTask.IsFaulted || initTask.IsCanceled) initTask = InitInternal();
            return initTask;
        }

        static async Task InitInternal()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!Auth.IsSignedIn)
                await Auth.SignInAnonymouslyAsync(); // reuses the cached session if there is one
        }

        // ---------- Google ----------

        /// <summary>True once this phone plays online: with Google, or as a guest.</summary>
        public static bool HasAccount(SaveData s) => s.googleLinked || s.devGuest || s.onlineGuest;

        /// <summary>Only Google players go on the leaderboard.</summary>
        public static bool OnLeaderboard(SaveData s) => s.googleLinked;

        public static bool IsGuest(SaveData s) => !s.googleLinked && (s.onlineGuest || s.devGuest);

        // ---------- guest ----------

        public const string GuestPrefix = "Guest_";

        /// <summary>"Guest_" + 6 letters/numbers taken from the player id, so every guest's name is different.</summary>
        public static string GuestName(string playerId)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (playerId ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
                if (sb.Length == 6) break;
            }
            while (sb.Length < 6) sb.Append((char)('a' + UnityEngine.Random.Range(0, 26)));
            return GuestPrefix + sb;
        }

        /// <summary>Plays online straight away with the phone's own anonymous account and a Guest_ name.</summary>
        public static async Task SignInGuestAsync(SaveData save)
        {
            await InitAsync();
            string name = GuestName(Auth.PlayerId);
            try { await Auth.UpdatePlayerNameAsync(name); }
            catch (Exception e) { Debug.LogWarning("Guest name: " + e.Message); }
            save.onlineGuest = true;
            save.onlineName = name;
            save.Save();
        }

        /// <summary>
        /// Google account picker, then: link Google onto the current session. If that Google
        /// account already belongs to a player (new phone, reinstall), switch to that player.
        /// </summary>
        public static async Task<string> SignInWithGoogleAsync(SaveData save)
        {
            await InitAsync();
            var g = await GoogleSignIn.RequestAsync();
            try
            {
                await Auth.LinkWithGoogleAsync(g.idToken);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Auth.SignOut(true);
                await Auth.SignInWithGoogleAsync(g.idToken);
            }
            save.googleLinked = true;
            save.devGuest = false;
            save.onlineGuest = false;
            await RefreshNameAsync(save);
            // A guest who signs in picks a real name (the Guest_ one was only for guests).
            if (save.onlineName.StartsWith(GuestPrefix)) save.onlineName = "";
            save.Save();
            return g.displayName;
        }

        /// <summary>Editor and dev builds only: play online without Google, for testing.</summary>
        public static async Task SignInDevGuestAsync(SaveData save)
        {
            await InitAsync();
            save.devGuest = true;
            await RefreshNameAsync(save);
            save.Save();
        }

        public static void SignOut(SaveData save)
        {
            if (SignedIn) Auth.SignOut(true);
            GoogleSignIn.SignOut();
            save.googleLinked = false;
            save.devGuest = false;
            save.onlineGuest = false;
            save.onlineName = "";
            save.Save();
            initTask = null;
        }

        // ---------- name ----------

        /// <summary>The name shown to other players, without Unity's "#1234" tag.</summary>
        public static string DisplayName(SaveData s) => string.IsNullOrEmpty(s.onlineName) ? "Player" : s.onlineName;

        public static string Clean(string uniName)
        {
            if (string.IsNullOrEmpty(uniName)) return "";
            int hash = uniName.LastIndexOf('#');
            return hash > 0 ? uniName.Substring(0, hash) : uniName;
        }

        /// <summary>null when fine, otherwise what is wrong with it.</summary>
        public static string NameProblem(string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length < NameMin) return NameMin + " letters at least";
            if (name.Length > NameMax) return NameMax + " letters at most";
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]+$")) return "letters, numbers and _ only";
            return null;
        }

        /// <summary>Picks up the name already on the account (e.g. after signing in on a new phone).</summary>
        static async Task RefreshNameAsync(SaveData save)
        {
            try
            {
                string n = Clean(await Auth.GetPlayerNameAsync(false));
                if (NameProblem(n) == null) save.onlineName = n;
            }
            catch (Exception e) { Debug.LogWarning("Name: " + e.Message); }
        }

        public static async Task SetNameAsync(SaveData save, string name)
        {
            name = name.Trim();
            await InitAsync();
            await Auth.UpdatePlayerNameAsync(name);
            save.onlineName = name;
            save.Save();
        }

        // ---------- leaderboard ----------

        public struct Row
        {
            public int rank;     // 1-based
            public string name;
            public int wins;
            public bool me;
        }

        /// <summary>
        /// Adds one online win to the all-time and weekly boards. Returns the new all-time total (or -1).
        /// Google players only: guests never reach the leaderboard.
        /// </summary>
        public static async Task<int> ReportWinAsync(SaveData save)
        {
            int total = -1;
            if (!OnLeaderboard(save)) return total;
            try
            {
                await InitAsync();
                var e = await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, 1);
                total = (int)e.Score;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Leaderboard: " + e.Message);
            }
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(WeeklyId, 1);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Weekly leaderboard: " + e.Message);
            }
            return total;
        }

        public static async Task<List<Row>> TopAsync(int count = 50, string board = LeaderboardId)
        {
            await InitAsync();
            var page = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = count });
            var rows = new List<Row>();
            foreach (var e in page.Results) rows.Add(ToRow(e));
            return rows;
        }

        /// <summary>This player's row, or null before their first online win.</summary>
        public static async Task<Row?> MeAsync(string board = LeaderboardId)
        {
            try
            {
                await InitAsync();
                return ToRow(await LeaderboardsService.Instance.GetPlayerScoreAsync(board));
            }
            catch (Exception)
            {
                return null; // no entry yet
            }
        }

        /// <summary>Time until the weekly board resets (next Monday 00:00 on this phone).</summary>
        public static TimeSpan UntilWeeklyReset()
        {
            var now = DateTime.Now;
            int days = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (days == 0) days = 7;
            return now.Date.AddDays(days) - now;
        }

        static Row ToRow(LeaderboardEntry e) => new Row
        {
            rank = e.Rank + 1,
            name = Clean(e.PlayerName),
            wins = (int)e.Score,
            me = SignedIn && e.PlayerId == Auth.PlayerId,
        };

        /// <summary>A short, friendly message for an exception from any online call.</summary>
        public static string Explain(Exception e)
        {
            if (e is AggregateException ae && ae.InnerException != null) e = ae.InnerException;
            if (e is GoogleSignIn.SignInException g)
            {
                switch (g.Code)
                {
                    case "cancelled": return "sign-in cancelled.";
                    case "no_account": return "no Google account on this phone. add one in Settings, then try again.";
                    case "unsupported": return g.Message;
                    default: return "Google sign-in didn't work. check your internet and try again.";
                }
            }
            if (!NetPopups.IsOnline) return "no internet. turn on wi-fi or mobile data.";
            if (e is RequestFailedException rf) return "the server said no (" + rf.ErrorCode + "). try again in a moment.";
            return "something went wrong. try again.";
        }
    }
}
