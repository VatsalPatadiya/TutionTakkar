using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// The wire between phones during an online match, carried in Lobby player data.
    /// Each player only ever writes their own data:
    ///   shot = "match|turn|shooter|pen0;pen1|strikeT|dirX|dirZ|power"   (sent when they flick)
    ///   rest = "match|turn|shooter|pen0;pen1"                          (where everything stopped)
    ///   quit = "match"                                                 (they left the match)
    /// pen = "x,y,z,qx,qy,qz,qw,flat,out" with round-trip floats, so every phone starts each
    /// flick from bit-identical positions. The shooter's "rest" is the final word.
    /// </summary>
    public class OnlineLink
    {
        public readonly OnlineRoom Room;
        public readonly string MatchId;           // the room's seed for this match
        public readonly List<string> Order;       // player ids by pen index
        public readonly int LocalIndex;
        readonly int seed;
        GameManager gm;
        bool over;

        public OnlineLink(GameManager gm, OnlineRoom room)
        {
            this.gm = gm;
            Room = room;
            MatchId = room.Data("seed", "1");
            int.TryParse(MatchId, out seed);
            Order = new List<string>(room.Data("order", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            LocalIndex = Mathf.Max(0, Order.IndexOf(room.MyId));
            room.Changed += OnRoomChanged;
            room.Ended += OnRoomEnded;
        }

        public void Detach()
        {
            over = true;
            Room.Changed -= OnRoomChanged;
            Room.Ended -= OnRoomEnded;
        }

        public string PlayerIdAt(int index) => index >= 0 && index < Order.Count ? Order[index] : "";

        /// <summary>Same small random twist of the starting pens on every phone.</summary>
        public float JitterFor(int round) => (float)(new System.Random(seed * 31 + round * 7919).NextDouble() * 8.0 - 4.0);

        // ---------- sending ----------

        public void SendShot(int turn, int shooter, BenchWorld world, Shot s)
        {
            var sb = new StringBuilder();
            sb.Append(MatchId).Append('|').Append(turn).Append('|').Append(shooter).Append('|')
              .Append(EncodePens(Capture(world))).Append('|')
              .Append(F(s.strikeT)).Append('|').Append(F(s.dir.x)).Append('|').Append(F(s.dir.z)).Append('|').Append(F(s.power));
            Send("shot", sb.ToString());
        }

        public void SendRest(int turn, int shooter, NetPen[] pens)
        {
            Send("rest", MatchId + "|" + turn + "|" + shooter + "|" + EncodePens(pens));
        }

        public void SendQuit() => Send("quit", MatchId);

        // ---------- reactions ----------

        /// <summary>(pen index, reaction code) from another player.</summary>
        public event Action<int, string> Reacted;
        int reactionSeq;
        readonly Dictionary<string, int> seenReaction = new Dictionary<string, int>();

        public void SendReaction(string code) => Send("emote", MatchId + "|" + (++reactionSeq) + "|" + code);

        void CheckReactions()
        {
            for (int i = 0; i < Order.Count; i++)
            {
                if (i == LocalIndex) continue;
                var parts = Read("emote", i);
                if (parts == null || parts.Length < 3 || !int.TryParse(parts[1], out int seq)) continue;
                seenReaction.TryGetValue(Order[i], out int seen);
                if (seq <= seen) continue;
                seenReaction[Order[i]] = seq;
                if (seen > 0 || seq == 1) Reacted?.Invoke(i, parts[2]); // ignore an old one we find on joining
            }
        }

        /// <summary>True while sends or refreshes are failing (no connection to the room).</summary>
        public bool NetFailing { get; private set; }

        /// <summary>Keeps retrying for as long as the connection-lost popup allows (about 40 s).</summary>
        async void Send(string key, string value)
        {
            float start = Time.realtimeSinceStartup;
            int attempt = 0;
            while (!Room.Closed && !over && Time.realtimeSinceStartup - start < ConnectionWatch.GiveUpAfter + 10f)
            {
                attempt++;
                try
                {
                    await Room.SetMyDataAsync(new Dictionary<string, string> { { key, value } });
                    NetFailing = false;
                    return;
                }
                catch (Exception e)
                {
                    NetFailing = true;
                    Debug.LogWarning("Online send " + key + " (try " + attempt + "): " + e.Message);
                    await Task.Delay(Mathf.Min(1000 * attempt, 3000));
                }
            }
        }

        // ---------- receiving ----------

        public bool TryGetShot(int turn, int shooter, out NetPen[] pens, out Shot shot)
        {
            pens = null;
            shot = default;
            var parts = Read("shot", shooter);
            if (parts == null || parts.Length < 8 || parts[1] != turn.ToString()) return false;
            pens = DecodePens(parts[3]);
            if (pens == null) return false;
            float dx = P(parts[5]), dz = P(parts[6]);
            shot = new Shot { strikeT = P(parts[4]), dir = new Vector3(dx, 0f, dz), power = P(parts[7]) };
            return true;
        }

        public bool TryGetRest(int turn, int shooter, out NetPen[] pens)
        {
            pens = null;
            var parts = Read("rest", shooter);
            if (parts == null || parts.Length < 4 || parts[1] != turn.ToString()) return false;
            pens = DecodePens(parts[3]);
            return pens != null;
        }

        /// <summary>A message from this match by the player at that pen index, split on '|'.</summary>
        string[] Read(string key, int index)
        {
            var p = Room.Find(PlayerIdAt(index));
            if (p == null) return null;
            string v = OnlineRoom.PlayerData(p, key, null);
            if (string.IsNullOrEmpty(v)) return null;
            var parts = v.Split('|');
            return parts.Length > 0 && parts[0] == MatchId ? parts : null;
        }

        float lastNudge;

        /// <summary>While waiting on the other phone, ask the server directly now and then (events can lag).</summary>
        public async void Nudge(float waited)
        {
            if (waited < 2f || Time.realtimeSinceStartup - lastNudge < 2f || Room.Closed) return;
            lastNudge = Time.realtimeSinceStartup;
            try
            {
                await Room.RefreshAsync();
                NetFailing = false;
            }
            catch (Exception e)
            {
                NetFailing = true;
                Debug.LogWarning("Online refresh: " + e.Message);
            }
        }

        // ---------- the other phone going away ----------

        readonly HashSet<int> removed = new HashSet<int>();
        bool Party => Order.Count > 2;

        void OnRoomChanged()
        {
            if (over) return;
            CheckReactions();
            if (!gm.Match.Running) return;
            // Did another phone give up waiting for us (we were away too long)?
            for (int i = 0; i < Order.Count; i++)
            {
                if (i == LocalIndex) continue;
                var f = Read("forfeit", i);
                if (f != null && f.Length >= 2 && f[1] == LocalIndex.ToString())
                {
                    LoseByForfeit(Party ? "you were away too long, so your pen was counted out."
                                        : "you were away too long, so the match went to " + NameAt(i) + ".");
                    return;
                }
            }
            for (int i = 0; i < Order.Count; i++)
            {
                if (i == LocalIndex) continue;
                var p = Room.Find(Order[i]);
                bool gone = p == null || OnlineRoom.PlayerData(p, "quit", "") == MatchId;
                if (!gone) continue;
                if (Party)
                {
                    if (removed.Add(i)) gm.Match.RemovePartyPlayer(i);
                    if (over || !gm.Match.Running) return;
                    continue;
                }
                WinByForfeit(NameAt(i) + " left the match.", i);
                return;
            }
        }

        void OnRoomEnded(string why)
        {
            if (over || !gm.Match.Running) return;
            if (Party)
            {
                over = true;
                gm.Match.Stop();
                OnlineMatch.Kicked(gm, "the room closed: " + why);
                return;
            }
            WinByForfeit("the other player's connection dropped.", 1 - LocalIndex);
        }

        /// <summary>The other phone stopped answering for too long.</summary>
        public void OpponentTimedOut(int quitter)
        {
            Send("forfeit", MatchId + "|" + quitter); // tells them, in case they come back
            if (Party)
            {
                if (removed.Add(quitter)) gm.Match.RemovePartyPlayer(quitter);
                return;
            }
            WinByForfeit(NameAt(quitter) + " stopped responding.", quitter);
        }

        string NameAt(int index)
        {
            var setup = gm.Match.Setup;
            return setup != null && index >= 0 && index < setup.names.Length ? setup.names[index] : "the other player";
        }

        void LoseByForfeit(string reason)
        {
            if (over) return;
            over = true;
            if (Party)
            {
                gm.Match.Stop();
                OnlineMatch.Kicked(gm, reason);
                return;
            }
            var setup = gm.Match.Setup;
            var score = new[] { gm.Match.Score[0], gm.Match.Score[1] };
            gm.Match.Stop();
            OnlineMatch.Finish(gm, setup, 1 - LocalIndex, score, reason);
        }

        void WinByForfeit(string reason, int quitter)
        {
            if (over) return;
            over = true;
            var setup = gm.Match.Setup;
            var score = new[] { gm.Match.Score[0], gm.Match.Score[1] };
            gm.Match.Stop();
            OnlineMatch.Finish(gm, setup, 1 - quitter, score, reason);
        }

        // ---------- pen states ----------

        public struct NetPen
        {
            public Vector3 pos;
            public Quaternion rot;
            public bool flat;
            public bool isOut;
        }

        public static NetPen[] Capture(BenchWorld w)
        {
            var r = new NetPen[w.Pens.Length];
            for (int i = 0; i < r.Length; i++)
            {
                var p = w.Pens[i];
                r[i] = new NetPen { pos = p.Rb.position, rot = p.Rb.rotation, flat = p.IsFlat, isOut = w.IsOut(p) };
            }
            return r;
        }

        public static void Apply(BenchWorld w, NetPen[] pens)
        {
            for (int i = 0; i < pens.Length && i < w.Pens.Length; i++)
                w.Pens[i].ApplyNetState(pens[i].pos, pens[i].rot, pens[i].flat, pens[i].isOut);
        }

        static string EncodePens(NetPen[] pens)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pens.Length; i++)
            {
                if (i > 0) sb.Append(';');
                var p = pens[i];
                sb.Append(F(p.pos.x)).Append(',').Append(F(p.pos.y)).Append(',').Append(F(p.pos.z)).Append(',')
                  .Append(F(p.rot.x)).Append(',').Append(F(p.rot.y)).Append(',').Append(F(p.rot.z)).Append(',').Append(F(p.rot.w)).Append(',')
                  .Append(p.flat ? '1' : '0').Append(',').Append(p.isOut ? '1' : '0');
            }
            return sb.ToString();
        }

        static NetPen[] DecodePens(string s)
        {
            try
            {
                var each = s.Split(';');
                var r = new NetPen[each.Length];
                for (int i = 0; i < each.Length; i++)
                {
                    var v = each[i].Split(',');
                    r[i] = new NetPen
                    {
                        pos = new Vector3(P(v[0]), P(v[1]), P(v[2])),
                        rot = new Quaternion(P(v[3]), P(v[4]), P(v[5]), P(v[6])),
                        flat = v[7] == "1",
                        isOut = v[8] == "1",
                    };
                }
                return r;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Online: bad pen data: " + e.Message);
                return null;
            }
        }

        static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static float P(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
