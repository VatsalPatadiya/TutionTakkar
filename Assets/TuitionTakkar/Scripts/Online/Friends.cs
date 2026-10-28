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
        /// <summary>Someone sent me a friend request (player id, name).</summary>
        public static event Action<string, string> RequestReceived;
        /// <summary>Someone accepted the request I sent (player id, name).</summary>
        public static event Action<string, string> RequestAccepted;

        // Who I had asked, so a new friendship can be told apart from one I accepted myself.
        static readonly HashSet<string> askedIds = new HashSet<string>();
        // Pop-ups already shown (a friend event can arrive twice: Friends service + phone notification).
        static readonly Dictionary<string, float> seen = new Dictionary<string, float>();

        static Task initTask;
        static bool sdkStarted;

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
                var f = FriendsService.Instance;
                if (sdkStarted)
                {
                    // Second start (after switching account): the service is up, just reload the lists.
                    await f.ForceRelationshipsRefreshAsync();
                    Ready = true;
                    UpdateCount(gm);
                    await EnsureFriendIdAsync(gm.Save);
                    await SetPresenceAsync(gm, "home");
                    UploadPushToken();
                    Changed?.Invoke();
                    return;
                }
                await f.InitializeAsync(new Unity.Services.Friends.Options.InitializeOptions().WithEvents(true).WithMemberPresence(true).WithMemberProfile(true));
                sdkStarted = true;
                f.RelationshipAdded += e => MainThread.Run(() => { OnAdded(e?.Relationship); UpdateCount(gm); Changed?.Invoke(); });
                f.RelationshipDeleted += _ => MainThread.Run(() => { UpdateCount(gm); Changed?.Invoke(); });
                f.PresenceUpdated += _ => MainThread.Run(() => Changed?.Invoke());
                f.MessageReceived += e =>
                {
                    RoomInvite inv = null;
                    try { inv = e.GetAs<RoomInvite>(); } catch (Exception) { }
                    if (inv == null || inv.kind != "invite") return;
                    MainThread.Run(() => ShowInvite(inv));
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
            askedIds.Clear();
        }

        /// <summary>
        /// Starts friends quietly when the game opens or right after Google sign-in, so invites
        /// and friend requests pop up on any screen (not only after opening Play Online).
        /// </summary>
        public static async void Boot(GameManager gm)
        {
            if (!Allowed(gm.Save)) return;
            FriendsUI.Hook(gm);
            SocialRunner.Ensure(gm);
            try { await InitAsync(gm); }
            catch (Exception e) { Debug.LogWarning("Friends boot: " + e.Message); }
            ProfileCloud.Publish(gm.Save);
        }

        /// <summary>Back in the game after a while: catch up on requests and presence.</summary>
        public static async void Refresh(GameManager gm)
        {
            if (!Ready) { Boot(gm); return; }
            try { await FriendsService.Instance.ForceRelationshipsRefreshAsync(); } catch (Exception e) { Debug.LogWarning("Friends refresh: " + e.Message); }
            await SetPresenceAsync(gm, gm.Match.Running ? "match" : "home");
            UpdateCount(gm);
            Changed?.Invoke();
        }

        static void OnAdded(Relationship r)
        {
            if (r?.Member == null) return;
            string id = r.Member.Id;
            if (r.Type == RelationshipType.FriendRequest)
            {
                bool incoming = r.Member.Role == MemberRole.Source;
                foreach (var x in FriendsService.Instance.IncomingFriendRequests) if (x.Member.Id == id) incoming = true;
                if (incoming) Notify("request:" + id, () => RequestReceived?.Invoke(id, NameOf(r)));
            }
            else if (r.Type == RelationshipType.Friend && askedIds.Contains(id))
                Notify("accepted:" + id, () => RequestAccepted?.Invoke(id, NameOf(r)));
        }

        /// <summary>Runs a pop-up once per key per minute.</summary>
        public static void Notify(string key, Action show)
        {
            float now = Time.realtimeSinceStartup;
            if (seen.TryGetValue(key, out var at) && now - at < 60f) return;
            seen[key] = now;
            show?.Invoke();
        }

        /// <summary>A notification that arrived while the game was open (the phone hands it to the game).</summary>
        public static void FromPush(Dictionary<string, string> d)
        {
            d.TryGetValue("kind", out var kind);
            d.TryGetValue("from", out var from);
            d.TryGetValue("fromId", out var fromId);
            from = string.IsNullOrEmpty(from) ? "A friend" : from;
            switch (kind)
            {
                case "invite":
                    d.TryGetValue("code", out var code);
                    if (string.IsNullOrEmpty(code)) return;
                    var inv = new RoomInvite { code = code, from = from, fromId = fromId ?? "", at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                    if (d.TryGetValue("deskId", out var desk)) inv.deskId = desk;
                    if (d.TryGetValue("seated", out var st) && int.TryParse(st, out var si)) inv.seated = si;
                    if (d.TryGetValue("seats", out var ss) && int.TryParse(ss, out var sn)) inv.seats = sn;
                    ShowInvite(inv);
                    break;
                case "friend_request":
                    if (!string.IsNullOrEmpty(fromId)) Notify("request:" + fromId, () => RequestReceived?.Invoke(fromId, from));
                    break;
                case "friend_accepted":
                    if (!string.IsNullOrEmpty(fromId)) Notify("accepted:" + fromId, () => RequestAccepted?.Invoke(fromId, from));
                    break;
            }
        }

        static void ShowInvite(RoomInvite inv)
        {
            if (inv == null || string.IsNullOrEmpty(inv.code)) return;
            Notify("invite:" + inv.code + ":" + inv.fromId, () => InviteReceived?.Invoke(inv));
        }

        static void UpdateCount(GameManager gm)
        {
            if (!Ready) return;
            foreach (var r in FriendsService.Instance.OutgoingFriendRequests) askedIds.Add(r.Member.Id);
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
                if (!theyAsked) askedIds.Add(playerId);
                Changed?.Invoke();
                Tell(playerId, theyAsked ? "accepted" : "request");
                return null;
            }
            catch (Exception e) { return Explain(e); }
        }

        /// <summary>Phone notification (or pop-up, if their game is open) for a request / accepted request.</summary>
        static async void Tell(string playerId, string kind)
        {
            try
            {
                var gm = GameManager.I;
                string me = gm != null ? OnlineServices.DisplayName(gm.Save) : "A classmate";
                var r = await CloudCodeService.Instance.CallEndpointAsync<PushResult>("friend_push", new Dictionary<string, object>
                {
                    { "to", playerId }, { "kind", kind }, { "from", me },
                });
                if (r != null && !r.sent) Debug.Log("Friend push not sent: " + r.error);
            }
            catch (Exception e) { Debug.LogWarning("Friend push: " + e.Message); }
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
            // Both ways every time: the in-game message (instant when their game is open) and the
            // phone notification (their presence can look "online" a while after they closed the game).
            // Their phone shows one pop-up either way: duplicates are dropped.
            bool messaged = false;
            if (StateOf(friend) != State.Offline)
            {
                try { await FriendsService.Instance.MessageAsync(friend.Member.Id, inv); messaged = true; }
                catch (Exception e) { Debug.LogWarning("Invite message: " + e.Message); }
            }
            try
            {
                var r = await CloudCodeService.Instance.CallEndpointAsync<PushResult>("invite_push", new Dictionary<string, object>
                {
                    { "to", friend.Member.Id }, { "code", inv.code }, { "from", inv.from }, { "desk", room.Desk.name },
                    { "deskId", room.Desk.id }, { "fee", room.Desk.fee }, { "seated", inv.seated }, { "seats", inv.seats },
                });
                if ((r != null && r.sent) || messaged) return null;
                return r != null && !string.IsNullOrEmpty(r.error) ? r.error : "couldn't reach their phone. share the room code instead.";
            }
            catch (Exception e) { return messaged ? null : Explain(e); }
        }

        // ---------- push token ----------

        /// <summary>Stores this phone's notification token (player data, private) so invites can reach it.</summary>
        static bool uploading;

        public static async void UploadPushToken()
        {
            string token = Push.Token();
            if (string.IsNullOrEmpty(token) || uploading || !OnlineServices.SignedIn) return;
            var gm = GameManager.I;
            if (gm == null || !Allowed(gm.Save)) return;
            if (PlayerPrefs.GetString("tt_push_token_sent", "") == token + OnlineServices.PlayerId) return;
            try
            {
                uploading = true;
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "fcmToken", token } });
                PlayerPrefs.SetString("tt_push_token_sent", token + OnlineServices.PlayerId);
                PlayerPrefs.Save();
                Debug.Log("[TuitionTakkar] push token saved for this player");
            }
            catch (Exception e) { Debug.LogWarning("Push token: " + e.Message); }
            uploading = false;
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
