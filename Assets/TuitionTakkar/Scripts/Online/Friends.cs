using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>What each friend shares as their presence activity (shown on the friend card).</summary>
    [Serializable]
    public class FriendActivity
    {
        public string status = "home";   // home | match | room
        public string desk = "";         // desk name while playing
        public int level;
        public string friendId = "";
        public string name = "";
    }

    /// <summary>A room invite sent friend to friend (online: pop-up; offline: phone notification).</summary>
    [Serializable]
    public class RoomInvite
    {
        public string kind = "invite";
        public string code = "";
        public string from = "";
        public string fromId = "";
        public string deskId = "";
        public int seated;
        public int seats;
        public long at;
    }

    /// <summary>
    /// Friends for signed-in (Google, later Apple) players only, on Unity's Friends service:
    /// requests, friends (50 max), blocks, presence, room invites. The permanent player ID
    /// (TT-XXXXXX) comes from the Cloud Code script friend_id, and find_player turns an ID into
    /// a player. Guests never see any of this.
    /// </summary>
    public static class Friends
    {
        public const int MaxFriends = 50;
        public const int MaxPending = 10;
        public const int InviteSeconds = 300;

        public static bool Ready { get; private set; }
        public static event Action Changed;
        public static event Action<RoomInvite> InviteReceived;

        static Task initTask;

        public static bool Allowed(SaveData s) => OnlineServices.OnLeaderboard(s);

        public static IReadOnlyList<Relationship> List => Ready ? FriendsService.Instance.Friends : new List<Relationship>();
        public static IReadOnlyList<Relationship> Incoming => Ready ? FriendsService.Instance.IncomingFriendRequests : new List<Relationship>();
        public static IReadOnlyList<Relationship> Outgoing => Ready ? FriendsService.Instance.OutgoingFriendRequests : new List<Relationship>();
        public static IReadOnlyList<Relationship> Blocks => Ready ? FriendsService.Instance.Blocks : new List<Relationship>();

        public static Task InitAsync(GameManager gm)
        {
            if (!Allowed(gm.Save)) return Task.CompletedTask;
            return initTask ??= Init(gm);
        }

        static async Task Init(GameManager gm)
        {
            try
            {
                await OnlineServices.InitAsync();
                await FriendsService.Instance.InitializeAsync(new Unity.Services.Friends.Options.InitializeOptions().WithEvents(true).WithMemberPresence(true).WithMemberProfile(true));
                var f = FriendsService.Instance;
                f.RelationshipAdded += _ => MainThread.Run(() => { UpdateCount(gm); Changed?.Invoke(); });
                f.RelationshipDeleted += _ => MainThread.Run(() => { UpdateCount(gm); Changed?.Invoke(); });
                f.PresenceUpdated += _ => MainThread.Run(() => Changed?.Invoke());
                f.MessageReceived += e =>
                {
                    RoomInvite inv = null;
                    try { inv = e.GetAs<RoomInvite>(); } catch (Exception) { }
                    if (inv == null || inv.kind != "invite") return;
                    MainThread.Run(() => InviteReceived?.Invoke(inv));
                };
                Ready = true;
                UpdateCount(gm);
                await EnsureFriendIdAsync(gm.Save);
                await SetPresenceAsync(gm, "home");
                UploadPushToken();
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Friends init: " + e.Message);
                initTask = null;
                throw;
            }
        }

        public static void Reset()
        {
            Ready = false;
            initTask = null;
        }

        static void UpdateCount(GameManager gm)
        {
            if (!Ready) return;
            gm.Save.stats.friends = Mathf.Max(gm.Save.stats.friends, FriendsService.Instance.Friends.Count);
            gm.Save.Save();
            Badges.Check(gm.Save);
        }

        // ---------- the permanent player ID ----------

        [Serializable]
        class IdResult { public string id; }

        [Serializable]
        class FindResult { public string playerId; public string error; }

        public static async Task EnsureFriendIdAsync(SaveData s)
        {
            if (!string.IsNullOrEmpty(s.friendId)) return;
            try
            {
                var r = await CloudCodeService.Instance.CallEndpointAsync<IdResult>("friend_id", new Dictionary<string, object>());
                if (r != null && !string.IsNullOrEmpty(r.id))
                {
                    s.friendId = r.id;
                    s.Save();
                }
            }
            catch (Exception e) { Debug.LogWarning("Friend ID: " + e.Message); }
        }

        /// <summary>"7k3p9q", "tt-7K3P9Q" → "TT-7K3P9Q" (or "" if it can't be an ID).</summary>
        public static string CleanId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var t = raw.Trim().ToUpperInvariant().Replace(" ", "");
            if (t.StartsWith("TT-")) t = t.Substring(3);
            else if (t.StartsWith("TT")) t = t.Substring(2);
            if (t.Length != 6) return "";
            foreach (char ch in t) if (!char.IsLetterOrDigit(ch)) return "";
            return "TT-" + t;
        }

        /// <summary>Player id for a TT-XXXXXX code, or null when nobody has it.</summary>
        public static async Task<string> FindAsync(string code)
        {
            var r = await CloudCodeService.Instance.CallEndpointAsync<FindResult>("find_player", new Dictionary<string, object> { { "code", code } });
            return r != null && !string.IsNullOrEmpty(r.playerId) ? r.playerId : null;
        }

        // ---------- presence ----------

        public static async Task SetPresenceAsync(GameManager gm, string status, string desk = "")
        {
            if (!Ready) return;
            try
            {
                var a = new FriendActivity
                {
                    status = status, desk = desk ?? "", level = OnlineLevel.Level(gm.Save.onlineXp),
                    friendId = gm.Save.friendId ?? "", name = OnlineServices.DisplayName(gm.Save),
                };
                await FriendsService.Instance.SetPresenceAsync(status == "match" ? Availability.Busy : Availability.Online, a);
            }
            catch (Exception e) { Debug.LogWarning("Presence: " + e.Message); }
        }

        public static FriendActivity ActivityOf(Relationship r)
        {
            try { return r?.Member?.Presence?.GetActivity<FriendActivity>() ?? new FriendActivity(); }
            catch (Exception) { return new FriendActivity(); }
        }

        public enum State { Online, Playing, Offline }

        public static State StateOf(Relationship r)
        {
            var p = r?.Member?.Presence;
            if (p == null) return State.Offline;
            switch (p.Availability)
            {
                case Availability.Online: return State.Online;
                case Availability.Busy: return State.Playing;
                case Availability.Away: return State.Online;
                default: return State.Offline;
            }
        }

        public static DateTime LastSeen(Relationship r)
        {
            try { return r?.Member?.Presence?.LastSeen ?? DateTime.MinValue; }
            catch (Exception) { return DateTime.MinValue; }
        }

        public static string NameOf(Relationship r)
        {
            var a = ActivityOf(r);
            if (!string.IsNullOrEmpty(a.name)) return a.name;
            return OnlineServices.Clean(r?.Member?.Profile?.Name ?? "Player");
        }

        public static bool IsFriend(string playerId)
        {
            foreach (var r in List) if (r.Member.Id == playerId) return true;
            return false;
        }

        public static bool IsPending(string playerId)
        {
            foreach (var r in Outgoing) if (r.Member.Id == playerId) return true;
            return false;
        }

        // ---------- requests ----------

        /// <summary>Sends a friend request (or accepts theirs). Returns a message for the player, or null when fine.</summary>
        public static async Task<string> RequestAsync(string playerId)
        {
            if (!Ready) return "friends are still loading. try again in a moment.";
            if (playerId == OnlineServices.PlayerId) return "that's your own ID!";
            if (IsFriend(playerId)) return "you're already friends.";
            if (IsPending(playerId)) return "request already sent. waiting for them to accept.";
            bool theyAsked = false;
            foreach (var r in Incoming) if (r.Member.Id == playerId) theyAsked = true;
            if (!theyAsked && Outgoing.Count >= MaxPending) return "you have " + MaxPending + " requests waiting. cancel one first.";
            if (List.Count >= MaxFriends) return "your friend list is full (" + MaxFriends + ").";
            try
            {
                await FriendsService.Instance.AddFriendAsync(playerId);
                Changed?.Invoke();
                return null;
            }
            catch (Exception e) { return Explain(e); }
        }

        public static async Task<string> AcceptAsync(string playerId) => await RequestAsync(playerId);

        public static async Task DeclineAsync(string playerId)
        {
            try { await FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId); } catch (Exception e) { Debug.LogWarning("Decline: " + e.Message); }
            Changed?.Invoke();
        }

        public static async Task CancelAsync(string playerId)
        {
            try { await FriendsService.Instance.DeleteOutgoingFriendRequestAsync(playerId); } catch (Exception e) { Debug.LogWarning("Cancel: " + e.Message); }
            Changed?.Invoke();
        }

        public static async Task RemoveAsync(string playerId)
        {
            try { await FriendsService.Instance.DeleteFriendAsync(playerId); } catch (Exception e) { Debug.LogWarning("Remove: " + e.Message); }
            Changed?.Invoke();
        }

        public static async Task<string> BlockAsync(string playerId)
        {
            if (Blocks.Count >= MaxPending) return "you've blocked " + MaxPending + " players already.";
            try
            {
                await FriendsService.Instance.AddBlockAsync(playerId);
                Changed?.Invoke();
                return null;
            }
            catch (Exception e) { return Explain(e); }
        }

        // ---------- room invites ----------

        [Serializable]
        class PushResult { public bool sent; public string error; }

        /// <summary>
        /// Invites a friend to my room: a pop-up in their game if they're online, otherwise a phone
        /// notification (Cloud Code script invite_push). Returns null when sent.
        /// </summary>
        public static async Task<string> InviteAsync(GameManager gm, Relationship friend, OnlineRoom room)
        {
            var inv = new RoomInvite
            {
                code = room.Code, from = OnlineServices.DisplayName(gm.Save), fromId = OnlineServices.PlayerId,
                deskId = room.Desk.id, seated = room.Players.Count, seats = room.Seats, at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            try
            {
                if (StateOf(friend) != State.Offline)
                {
                    await FriendsService.Instance.MessageAsync(friend.Member.Id, inv);
                    return null;
                }
                var r = await CloudCodeService.Instance.CallEndpointAsync<PushResult>("invite_push", new Dictionary<string, object>
                {
                    { "to", friend.Member.Id }, { "code", inv.code }, { "from", inv.from }, { "desk", room.Desk.name },
                    { "fee", room.Desk.fee }, { "seated", inv.seated }, { "seats", inv.seats },
                });
                if (r != null && r.sent) return null;
                return r != null && !string.IsNullOrEmpty(r.error) ? r.error : "couldn't reach their phone. share the room code instead.";
            }
            catch (Exception e) { return Explain(e); }
        }

        // ---------- push token ----------

        /// <summary>Stores this phone's notification token (player data, private) so invites can reach it.</summary>
        public static async void UploadPushToken()
        {
            string token = Push.Token();
            if (string.IsNullOrEmpty(token)) return;
            if (PlayerPrefs.GetString("tt_push_token_sent", "") == token + OnlineServices.PlayerId) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "fcmToken", token } });
                PlayerPrefs.SetString("tt_push_token_sent", token + OnlineServices.PlayerId);
            }
            catch (Exception e) { Debug.LogWarning("Push token: " + e.Message); }
        }

        public static string Explain(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("409") || m.ToLowerInvariant().Contains("conflict")) return "that's not possible right now (limit reached, or already sent).";
            if (m.Contains("404")) return "no player with that ID.";
            if (m.Contains("403")) return "that player can't be added.";
            return OnlineServices.Explain(e);
        }
    }
}
