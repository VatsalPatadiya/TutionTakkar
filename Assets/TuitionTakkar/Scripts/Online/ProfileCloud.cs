using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>What other players in the same match see (mockup P2). Google players only.</summary>
    [Serializable]
    public class PublicProfile
    {
        public string name = "";
        public string title = "";
        public int level, played, won, bestStreak, knockOffs, flawless;
        public int highestTier = -1;
        public string pen = "";
        public int[] badgeIds = new int[0];
        public int[] badgeTiers = new int[0];
        public string friendId = "";

        public int WinRate => played > 0 ? Mathf.RoundToInt(100f * won / played) : 0;
    }

    /// <summary>
    /// Publishes this player's summary to Cloud Save as public player data (anyone signed in can
    /// read it by player id) and loads other players' summaries. Full stats, recent matches and the
    /// friend ID stay on the phone.
    /// </summary>
    public static class ProfileCloud
    {
        const string Key = "profile";

        public static PublicProfile Build(SaveData s)
        {
            var st = s.stats;
            var top = new List<(int i, int t)>();
            for (int i = 0; i < Badges.Count; i++) if (st.badgeTier[i] > 0) top.Add((i, st.badgeTier[i]));
            top.Sort((a, b) => b.t.CompareTo(a.t));
            if (top.Count > 4) top.RemoveRange(4, top.Count - 4);
            var p = new PublicProfile
            {
                name = OnlineServices.DisplayName(s),
                title = Badges.Title(s),
                level = OnlineLevel.Level(s.onlineXp),
                played = st.played, won = st.won, bestStreak = st.bestStreak, knockOffs = st.knockOffs, flawless = st.flawless,
                highestTier = st.HighestTierWon,
                pen = ProfileScreens.ShowcasePen(s).id,
                badgeIds = top.ConvertAll(x => x.i).ToArray(),
                badgeTiers = top.ConvertAll(x => x.t).ToArray(),
            };
            return p;
        }

        static bool busy, again;

        /// <summary>Saves the public summary (Google players only; guests have no online profile).</summary>
        public static async void Publish(SaveData s)
        {
            if (!OnlineServices.OnLeaderboard(s) || !OnlineServices.SignedIn) return;
            if (busy) { again = true; return; }
            busy = true;
            try
            {
                do
                {
                    again = false;
                    var data = new Dictionary<string, object> { { Key, JsonUtility.ToJson(Build(s)) } };
                    await CloudSaveService.Instance.Data.Player.SaveAsync(data, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
                } while (again);
            }
            catch (Exception e) { Debug.LogWarning("Profile publish: " + e.Message); }
            busy = false;
        }

        /// <summary>Another player's public summary, or null (guest, not published, offline).</summary>
        public static async Task<PublicProfile> LoadAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            try
            {
                await OnlineServices.InitAsync();
                var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key },
                    new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(new PublicReadAccessClassOptions(playerId)));
                if (res != null && res.TryGetValue(Key, out var item))
                    return JsonUtility.FromJson<PublicProfile>(item.Value.GetAsString());
            }
            catch (Exception e) { Debug.LogWarning("Profile load: " + e.Message); }
            return null;
        }
    }
}
