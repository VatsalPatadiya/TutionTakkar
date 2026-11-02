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
        public string face = "";
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
                face = AvatarParts.MyCode(s),
                badgeIds = top.ConvertAll(x => x.i).ToArray(),
                badgeTiers = top.ConvertAll(x => x.t).ToArray(),
            };
            return p;
        }

        static bool busy, again;

        /// <summary>
        /// The account owns the avatar: the first time this phone plays as a signed-in player, the
        /// account's saved face replaces the phone's (silently). A new account (nothing saved yet)
        /// keeps the face picked on this phone. Until this check has worked, nothing is published,
        /// so a phone can never overwrite the account's face. Returns false if it couldn't check.
        /// </summary>
        public static async Task<bool> EnsureAccountAsync(SaveData s)
        {
            if (!OnlineServices.OnLeaderboard(s) || !OnlineServices.SignedIn) return false;
            string me = OnlineServices.PlayerId;
            if (string.IsNullOrEmpty(me)) return false;
            if (s.accountChecked == me) return true;
            var (p, failed) = await TryLoadAsync(me);
            if (failed || OnlineServices.PlayerId != me) return false;
            if (p != null && AvatarParts.Parse(p.face) != null)
            {
                s.face = p.face;
                s.faceIntroSeen = true;
                OwnFaceParts(s, AvatarParts.Parse(p.face)); // bought on the account, so usable here too
            }
            s.accountChecked = me;
            s.Save();
            return true;
        }

        /// <summary>Signed in, but neither the account nor this phone has a face picked yet.</summary>
        public static bool NeedsFace(SaveData s) =>
            OnlineServices.OnLeaderboard(s) && s.accountChecked == OnlineServices.PlayerId && AvatarParts.Parse(s.face) == null;

        static void OwnFaceParts(SaveData s, AvatarParts.Face f)
        {
            foreach (var layer in AvatarParts.Order)
            {
                var part = AvatarParts.Get(f.PartId(layer));
                if (part != null && !part.Free && !s.faceOwned.Contains(part.id)) s.faceOwned.Add(part.id);
            }
            foreach (var k in AvatarParts.ColorKeys)
            {
                int i = f.Color(k);
                if (AvatarParts.ColorPrice(k, i) > 0 && !s.faceOwned.Contains(AvatarParts.ColorItem(k, i))) s.faceOwned.Add(AvatarParts.ColorItem(k, i));
            }
        }

        /// <summary>Saves the public summary (signed-in players only; guests have no online profile).</summary>
        public static async void Publish(SaveData s)
        {
            if (!OnlineServices.OnLeaderboard(s) || !OnlineServices.SignedIn) return;
            if (s.accountChecked != OnlineServices.PlayerId)
            {
                // Not checked yet (or it failed): check first, publish only once it worked.
                if (await EnsureAccountAsync(s)) Publish(s);
                return;
            }
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
        public static async Task<PublicProfile> LoadAsync(string playerId) => (await TryLoadAsync(playerId)).profile;

        /// <summary>
        /// Another player's public summary. failed = couldn't reach the server (not the same as a
        /// guest, who simply has no profile). Tries twice before giving up.
        /// </summary>
        public static async Task<(PublicProfile profile, bool failed)> TryLoadAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return (null, false);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    await OnlineServices.InitAsync();
                    var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key },
                        new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(new PublicReadAccessClassOptions(playerId)));
                    if (res != null && res.TryGetValue(Key, out var item))
                        return (JsonUtility.FromJson<PublicProfile>(item.Value.GetAsString()), false);
                    return (null, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Profile load (try " + (attempt + 1) + "): " + e.Message);
                    if (attempt == 0) await Task.Delay(800);
                }
            }
            return (null, true);
        }
    }
}
