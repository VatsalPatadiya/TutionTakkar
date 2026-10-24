using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// One online room (a Unity Lobby): Quick Match rooms are public and start when full,
    /// private rooms have a 6-letter code and start when the host says so.
    ///
    /// Room data:  kind (S1, public) = "quick" | "room"
    ///             state (member)    = "wait" | "play"
    ///             seed / order      = set by the host when the match starts
    /// Player data: name, pen
    /// </summary>
    public class OnlineRoom
    {
        public const string KindQuick = "quick", KindRoom = "room";
        public const int MinPlayers = 2;
        /// <summary>Quick Match: after this long the host may start with fewer players.</summary>
        public const float QuickStartAfter = 45f;

        public static OnlineRoom Current { get; private set; }

        public Lobby Lobby { get; private set; }
        public bool IsQuick { get; private set; }
        public float JoinedAt { get; private set; }
        public bool Closed { get; private set; }

        /// <summary>Anything in the room changed (players, data, host).</summary>
        public event Action Changed;
        /// <summary>The room is gone for us (left, kicked, deleted). Argument: why, for the player.</summary>
        public event Action<string> Ended;

        ILobbyEvents events;
        readonly GameManager gm;

        OnlineRoom(GameManager gm, Lobby lobby, bool quick)
        {
            this.gm = gm;
            Lobby = lobby;
            IsQuick = quick;
            JoinedAt = Time.realtimeSinceStartup;
        }

        // ---------- info ----------

        public string MyId => OnlineServices.PlayerId;
        public bool IsHost => Lobby != null && Lobby.HostId == MyId;
        /// <summary>
        /// Who made the room. The lobby service moves "host" to someone else when the maker's app
        /// goes to the background for a few seconds (e.g. sharing the code on WhatsApp), so the
        /// room remembers its maker and the borrowed host hands it straight back.
        /// </summary>
        public string Owner => Data("owner", Lobby?.HostId ?? "");
        public bool IsOwner => Owner == MyId;
        /// <summary>This phone runs the room: it is the host and the maker (or the maker has left).</summary>
        public bool InCharge => IsHost && (IsOwner || Find(Owner) == null);
        public string Code => Lobby?.LobbyCode ?? "";
        public int Seats => Lobby?.MaxPlayers ?? 0;
        public List<Player> Players => Lobby?.Players ?? new List<Player>();
        public bool Full => Players.Count >= Seats;
        public string State => Data("state", "wait");
        public bool Playing => State == "play";

        public string Data(string key, string fallback = "")
        {
            if (Lobby?.Data != null && Lobby.Data.TryGetValue(key, out var d) && d != null) return d.Value;
            return fallback;
        }

        public static string PlayerData(Player p, string key, string fallback = "")
        {
            if (p?.Data != null && p.Data.TryGetValue(key, out var d) && d != null) return d.Value;
            return fallback;
        }

        public static string NameOf(Player p) => PlayerData(p, "name", "Player");
        public static string PenOf(Player p) => PlayerData(p, "pen", Catalog.StarterId);

        /// <summary>Host may start: private room with 2+, or Quick Match full / waited long enough.</summary>
        public bool CanStart =>
            InCharge && !Playing && Players.Count >= MinPlayers &&
            (!IsQuick || Full || Time.realtimeSinceStartup - JoinedAt >= QuickStartAfter);

        // ---------- create / join ----------

        static Player Me(GameManager gm) => new Player(data: new Dictionary<string, PlayerDataObject>
        {
            { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, OnlineServices.DisplayName(gm.Save)) },
            { "pen", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, gm.Save.equipped) },
        });

        public static async Task<OnlineRoom> CreateAsync(GameManager gm, int seats, bool quick)
        {
            await LeaveCurrentAsync();
            await OnlineServices.InitAsync();
            var options = new CreateLobbyOptions
            {
                IsPrivate = !quick,
                Player = Me(gm),
                Data = new Dictionary<string, DataObject>
                {
                    { "kind", new DataObject(DataObject.VisibilityOptions.Public, quick ? KindQuick : KindRoom, DataObject.IndexOptions.S1) },
                    { "state", new DataObject(DataObject.VisibilityOptions.Member, "wait") },
                    { "owner", new DataObject(DataObject.VisibilityOptions.Member, OnlineServices.PlayerId) },
                },
            };
            var lobby = await LobbyService.Instance.CreateLobbyAsync(quick ? "Quick match" : "Private room", seats, options);
            return await AttachAsync(gm, lobby, quick);
        }

        /// <summary>Joins an open Quick Match room for this many players, or opens a new one.</summary>
        public static async Task<OnlineRoom> QuickMatchAsync(GameManager gm, int seats)
        {
            await LeaveCurrentAsync();
            await OnlineServices.InitAsync();
            try
            {
                var lobby = await LobbyService.Instance.QuickJoinLobbyAsync(new QuickJoinLobbyOptions
                {
                    Player = Me(gm),
                    Filter = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.S1, KindQuick, QueryFilter.OpOptions.EQ),
                        new QueryFilter(QueryFilter.FieldOptions.MaxPlayers, seats.ToString(), QueryFilter.OpOptions.EQ),
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    },
                });
                return await AttachAsync(gm, lobby, true);
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.NoOpenLobbies)
            {
                return await CreateAsync(gm, seats, true);
            }
        }

        public static async Task<OnlineRoom> JoinByCodeAsync(GameManager gm, string code)
        {
            await LeaveCurrentAsync();
            await OnlineServices.InitAsync();
            var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code.Trim().ToUpperInvariant(),
                new JoinLobbyByCodeOptions { Player = Me(gm) });
            return await AttachAsync(gm, lobby, false);
        }

        static async Task<OnlineRoom> AttachAsync(GameManager gm, Lobby lobby, bool quick)
        {
            var room = new OnlineRoom(gm, lobby, quick);
            Current = room;
            await room.SubscribeAsync();
            room.KeepAlive();
            return room;
        }

        async Task SubscribeAsync()
        {
            var cb = new LobbyEventCallbacks();
            cb.LobbyChanged += changes =>
            {
                if (Closed) return;
                if (changes.LobbyDeleted) { End("the room was closed."); return; }
                changes.ApplyToLobby(Lobby);
                HandBackIfBorrowed();
                Changed?.Invoke();
            };
            cb.KickedFromLobby += () => End("you were removed from the room.");
            cb.LobbyEventConnectionStateChanged += state =>
            {
                if (state == LobbyEventConnectionState.Unsynced || state == LobbyEventConnectionState.Error)
                    _ = RefreshAsync();
            };
            try
            {
                events = await LobbyService.Instance.SubscribeToLobbyEventsAsync(Lobby.Id, cb);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Room events: " + e.Message + " (falling back to polling)");
            }
        }

        /// <summary>Host heartbeat (rooms close after 30 s without one) and a slow safety poll.</summary>
        async void KeepAlive()
        {
            float lastBeat = 0f, lastPoll = Time.realtimeSinceStartup;
            while (!Closed && Current == this)
            {
                await Task.Delay(1000);
                if (Closed || Current != this) break;
                float now = Time.realtimeSinceStartup;
                try
                {
                    if (IsHost && now - lastBeat >= 15f)
                    {
                        lastBeat = now;
                        await LobbyService.Instance.SendHeartbeatPingAsync(Lobby.Id);
                    }
                    if (now - lastPoll >= (events == null ? 3f : 10f))
                    {
                        lastPoll = now;
                        await RefreshAsync();
                    }
                    if (!Playing) Changed?.Invoke(); // ticks the waiting timers on screen
                }
                catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    End("the room was closed.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Room keep-alive: " + e.Message);
                }
            }
        }

        public async Task RefreshAsync()
        {
            if (Closed) return;
            Lobby = await LobbyService.Instance.GetLobbyAsync(Lobby.Id);
            if (!Players.Exists(p => p.Id == MyId)) { End("you were removed from the room."); return; }
            HandBackIfBorrowed();
            Changed?.Invoke();
        }

        // ---------- host hand-back ----------

        string handedFor;
        bool handing;

        /// <summary>
        /// We were made host while the room's maker was away: give it back once they are in the
        /// room (again once each time they come back, so a maker who is really gone isn't chased).
        /// </summary>
        async void HandBackIfBorrowed()
        {
            if (handing || Closed || Lobby == null || !IsHost || IsOwner || Playing) return;
            var owner = Find(Owner);
            if (owner == null) return;
            string back = PlayerData(owner, "back", "");
            if (back == handedFor) return;
            handing = true;
            handedFor = back;
            try
            {
                Lobby = await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions { HostId = Owner });
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                handedFor = null;
                Debug.LogWarning("Hand host back: " + e.Message);
            }
            finally
            {
                handing = false;
            }
        }

        /// <summary>
        /// The app came back from the background (share sheet, a call…): say we're here so a
        /// borrowed host hands the room back, and catch up on anything missed.
        /// </summary>
        public async void OnResume()
        {
            if (Closed || Playing) return;
            try
            {
                await SetMyDataAsync(new Dictionary<string, string> { { "back", DateTime.UtcNow.Ticks.ToString() } });
                await RefreshAsync();
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                End("the room was closed.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Room resume: " + e.Message);
            }
        }

        // ---------- updates ----------

        /// <summary>Host: lock the room and start. Everyone sees state = play and opens the desk.</summary>
        public async Task StartAsync()
        {
            if (!IsHost) return;
            var order = new List<string>();
            foreach (var p in Players) order.Add(p.Id);
            Lobby = await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions
            {
                IsLocked = true,
                Data = new Dictionary<string, DataObject>
                {
                    { "state", new DataObject(DataObject.VisibilityOptions.Member, "play") },
                    { "seed", new DataObject(DataObject.VisibilityOptions.Member, UnityEngine.Random.Range(1, int.MaxValue).ToString()) },
                    { "order", new DataObject(DataObject.VisibilityOptions.Member, string.Join(",", order)) },
                },
            });
            Changed?.Invoke();
        }

        /// <summary>Host: back to waiting after a match (Play again).</summary>
        public async Task ReopenAsync()
        {
            if (!IsHost) return;
            Lobby = await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions
            {
                IsLocked = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "state", new DataObject(DataObject.VisibilityOptions.Member, "wait") },
                },
            });
            Changed?.Invoke();
        }

        /// <summary>Writes some of my own player data (shots, results). Visible to the room.</summary>
        public async Task SetMyDataAsync(Dictionary<string, string> values)
        {
            var data = new Dictionary<string, PlayerDataObject>();
            foreach (var kv in values) data[kv.Key] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, kv.Value);
            Lobby = await LobbyService.Instance.UpdatePlayerAsync(Lobby.Id, MyId, new UpdatePlayerOptions { Data = data });
        }

        public Player Find(string playerId) => Players.Find(p => p.Id == playerId);

        // ---------- leave ----------

        public async Task LeaveAsync()
        {
            if (Closed) return;
            Closed = true;
            if (Current == this) Current = null;
            try { if (events != null) await events.UnsubscribeAsync(); } catch (Exception) { }
            try { await LobbyService.Instance.RemovePlayerAsync(Lobby.Id, MyId); } catch (Exception e) { Debug.LogWarning("Leave room: " + e.Message); }
        }

        public static async Task LeaveCurrentAsync()
        {
            if (Current != null) await Current.LeaveAsync();
        }

        void End(string why)
        {
            if (Closed) return;
            Closed = true;
            if (Current == this) Current = null;
            try { events?.UnsubscribeAsync(); } catch (Exception) { }
            Ended?.Invoke(why);
        }

        /// <summary>Room-code / join errors in words a player understands.</summary>
        public static string Explain(Exception e)
        {
            if (e is AggregateException ae && ae.InnerException != null) e = ae.InnerException;
            if (e is LobbyServiceException le)
            {
                switch (le.Reason)
                {
                    case LobbyExceptionReason.LobbyNotFound: return "no room with that code. check the letters and try again.";
                    case LobbyExceptionReason.LobbyFull: return "that room is full.";
                    case LobbyExceptionReason.LobbyLocked: return "that match has already started.";
                    case LobbyExceptionReason.RateLimited: return "too fast! wait a second and try again.";
                }
            }
            return OnlineServices.Explain(e);
        }
    }
}
