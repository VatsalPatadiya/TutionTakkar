using System;
using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>One finished online match, for the profile's recent list (newest first, 10 kept).</summary>
    [Serializable]
    public class MatchRecord
    {
        public bool won;
        public string vs = "";
        public string desk = "";
        public string score = "";
        public int net;          // marks won minus the entry fee
        public bool free;        // friendly desk: nothing at stake
        public bool left;        // this player left early
        public long at;          // unix seconds
    }

    /// <summary>
    /// Everything the profile shows (mockup P1). Online stats count online matches only; pen-fight
    /// stats count online and Batch Ladder matches (real rivals, not practice or pass and play).
    /// Saved on the phone with SaveData; Google players also publish a summary to Cloud Save so the
    /// players in their match can see it.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        // ---------- online ----------
        public int played, won, lost, streak, bestStreak;
        public int partyPlayed, party1st, party2nd;
        public int potWon, biggestPot;
        public int highestDesk = -1;               // best desk index won at
        public int[] tierWins = new int[5];        // Classic, Silver, Gold, Platinum, Diamond
        public List<string> deskIds = new List<string>();
        public List<int> deskPlays = new List<int>();
        public int finished;                       // matches finished without leaving (fair play)
        public int roomsHosted;
        // ---------- pen fights (online + ladder) ----------
        public int knockOffs, ownFalls, flawless, comebacks, oneFlick, survivor;
        // ---------- other ----------
        public int bosses;
        public int daysPlayed;
        public int friends;
        public string since = "";                  // "Oct 2026": first time online
        public List<MatchRecord> recent = new List<MatchRecord>();
        // ---------- badges ----------
        public int[] badgeTier = new int[Badges.Count];   // 0 none, 1 bronze, 2 silver, 3 gold
        public int titleBadge = -1;
        public string showcasePen = "";

        public void Fix()
        {
            if (tierWins == null || tierWins.Length != 5) tierWins = new int[5];
            deskIds ??= new List<string>();
            deskPlays ??= new List<int>();
            while (deskPlays.Count < deskIds.Count) deskPlays.Add(0);
            recent ??= new List<MatchRecord>();
            if (badgeTier == null || badgeTier.Length != Badges.Count)
            {
                var b = new int[Badges.Count];
                if (badgeTier != null) Array.Copy(badgeTier, b, Mathf.Min(badgeTier.Length, b.Length));
                badgeTier = b;
            }
            since ??= "";
            showcasePen ??= "";
        }

        public int WinRate => played > 0 ? Mathf.RoundToInt(100f * won / played) : 0;
        public float KnockRatio => ownFalls > 0 ? knockOffs / (float)ownFalls : knockOffs;

        public string FavouriteDesk(out int plays)
        {
            plays = 0;
            string best = "";
            for (int i = 0; i < deskIds.Count; i++)
                if (deskPlays[i] > plays) { plays = deskPlays[i]; best = deskIds[i]; }
            return best;
        }

        public int HighestTierWon
        {
            get
            {
                for (int t = 4; t >= 0; t--) if (tierWins[t] > 0) return t;
                return -1;
            }
        }

        public int BadgesEarned
        {
            get { int n = 0; foreach (var t in badgeTier) n += t; return n; }
        }
    }

    /// <summary>Counts pen-fight stats while a match is played (MatchController calls these).</summary>
    public static class StatsTracker
    {
        static bool on;
        static int me;
        static bool comebackArmed;

        static SaveData Save => GameManager.I != null ? GameManager.I.Save : null;

        public static void Begin(MatchSetup s)
        {
            on = s != null && (s.mode == MatchMode.Online || s.mode == MatchMode.Ladder);
            me = s == null ? 0 : s.mode == MatchMode.Online ? s.localIndex : 0;
            comebackArmed = false;
        }

        public static void Stop() => on = false;

        /// <summary>2 players: a round ended on this shot.</summary>
        public static void Round(int shooter, bool selfOut, bool oppOut, int shotsInRound, int winner, bool winnerOverhanging, int[] score)
        {
            if (!on || Save == null) return;
            var st = Save.stats;
            bool myPenOut = shooter == me ? selfOut : oppOut;
            if (myPenOut) st.ownFalls++;
            if (shooter == me && !selfOut && oppOut) st.knockOffs++;
            if (winner == me)
            {
                if (shooter == me && shotsInRound == 1 && !selfOut) st.oneFlick++;
                if (winnerOverhanging) st.survivor++;
            }
            if (score[1 - me] >= 2 && score[me] == 0) comebackArmed = true;
        }

        /// <summary>3–4 players: pens that fell on this shot.</summary>
        public static void PartyShot(int shooter, List<int> fell)
        {
            if (!on || Save == null) return;
            var st = Save.stats;
            foreach (int i in fell)
            {
                if (i == me) st.ownFalls++;
                else if (shooter == me) st.knockOffs++;
            }
        }

        /// <summary>A 2-player match ended normally (online or ladder).</summary>
        public static void MatchEnd(int winner, int[] score)
        {
            if (!on || Save == null) return;
            var st = Save.stats;
            if (winner == me)
            {
                if (score[1 - me] == 0) st.flawless++;
                if (comebackArmed) st.comebacks++;
            }
            on = false;
        }
    }

    /// <summary>
    /// 20 badges, each with bronze, silver and gold levels and a marks reward per level. Progress
    /// comes from PlayerStats and the save. Reached levels are paid once and queued for the
    /// "new badge" screen.
    /// </summary>
    public static class Badges
    {
        public const int Count = 20;
        public static readonly int[] Reward = { 0, 100, 500, 2000 };
        public static readonly string[] TierNames = { "", "BRONZE", "SILVER", "GOLD" };

        public class Def
        {
            public string name, icon, what;
            public int[] at;                  // bronze, silver, gold
            public string[] labels;           // shown instead of numbers (optional)
            public Func<SaveData, int> value;
        }

        static string K(int v) => v >= 100000 ? (v / 100000) + "L" : v >= 1000 ? (v / 1000) + "K" : v.ToString();

        public static readonly Def[] All =
        {
            new Def { name = "CHAMPION", icon = "trophy", what = "win online matches", at = new[] { 10, 100, 1000 }, value = s => s.stats.won },
            new Def { name = "DESK DESTROYER", icon = "drop", what = "knock pens off the desk", at = new[] { 50, 500, 5000 }, value = s => s.stats.knockOffs },
            new Def { name = "ON FIRE", icon = "flame", what = "win in a row", at = new[] { 3, 7, 15 }, value = s => s.stats.bestStreak },
            new Def { name = "UNTOUCHABLE", icon = "shield", what = "win without losing a round", at = new[] { 5, 50, 250 }, value = s => s.stats.flawless },
            new Def { name = "COMEBACK KID", icon = "arrow", what = "win from 0–2 down", at = new[] { 1, 10, 50 }, value = s => s.stats.comebacks },
            new Def { name = "LAST PEN", icon = "crown", what = "finish 1st in 3–4 player matches", at = new[] { 5, 50, 250 }, value = s => s.stats.party1st },
            new Def { name = "HIGH ROLLER", icon = "coin", what = "win one big pot", at = new[] { 1000, 50000, 200000 }, value = s => s.stats.biggestPot },
            new Def { name = "POT COLLECTOR", icon = "stack", what = "marks won from pots", at = new[] { 10000, 100000, 1000000 }, value = s => s.stats.potWon },
            new Def { name = "DESK EXPLORER", icon = "compass", what = "play at different desks", at = new[] { 5, 15, 28 }, value = s => s.stats.deskIds.Count },
            new Def { name = "DIAMOND CLASS", icon = "gem", what = "win at a Gold, Platinum, Diamond desk", at = new[] { 3, 4, 5 }, labels = new[] { "Gold", "Plat", "Dia" }, value = s => s.stats.HighestTierWon + 1 },
            new Def { name = "BATCH BOSS", icon = "skull", what = "beat Batch Ladder bosses", at = new[] { 1, 5, 20 }, value = s => s.stats.bosses },
            new Def { name = "PEN COLLECTOR", icon = "bag", what = "own pens", at = new[] { 10, 100, 300 }, value = s => s.owned.Count },
            new Def { name = "WORKSHOP WIZARD", icon = "wrench", what = "make pens in the workshop", at = new[] { 1, 10, 50 }, value = PensMade },
            new Def { name = "ONE FLICK", icon = "bolt", what = "win a round with your first flick", at = new[] { 1, 25, 100 }, value = s => s.stats.oneFlick },
            new Def { name = "SURVIVOR", icon = "sword", what = "win a round with your pen hanging off", at = new[] { 1, 10, 50 }, value = s => s.stats.survivor },
            new Def { name = "CLASSMATES", icon = "party", what = "make friends", at = new[] { 5, 20, 50 }, value = s => s.stats.friends },
            new Def { name = "ROOM HOST", icon = "door", what = "host private matches", at = new[] { 5, 50, 250 }, value = s => s.stats.roomsHosted },
            new Def { name = "REGULAR", icon = "cal", what = "days played", at = new[] { 7, 30, 100 }, value = s => s.stats.daysPlayed },
            new Def { name = "TOPPER", icon = "star", what = "reach online level", at = new[] { 10, 50, 100 }, value = s => OnlineLevel.Level(s.onlineXp) },
            new Def { name = "FAIR PLAY", icon = "heart", what = "finish matches without leaving", at = new[] { 25, 250, 1000 }, value = s => s.stats.finished },
        };

        public static int PensMade(SaveData s)
        {
            int n = 0;
            foreach (var id in s.owned) if (id.StartsWith("c.")) n++;
            return n;
        }

        public static string Goal(Def d, int tier) => d.labels != null ? d.labels[tier] : K(d.at[tier]);
        public static string Value(Def d, int v) => d.labels != null ? (v >= d.at[0] ? d.labels[Mathf.Clamp(v - d.at[0], 0, 2)] : "-") : K(v);

        /// <summary>Levels reached but not yet shown: (badge, tier).</summary>
        public static readonly Queue<(int badge, int tier)> Pending = new Queue<(int, int)>();

        /// <summary>Pays any newly reached levels (marks) and queues them for the "new badge" screen.</summary>
        public static int Check(SaveData s)
        {
            int found = 0;
            for (int i = 0; i < Count; i++)
            {
                int v = All[i].value(s);
                int reached = 0;
                for (int t = 0; t < 3; t++) if (v >= All[i].at[t]) reached = t + 1;
                while (s.stats.badgeTier[i] < reached)
                {
                    s.stats.badgeTier[i]++;
                    s.marks += Reward[s.stats.badgeTier[i]];
                    Pending.Enqueue((i, s.stats.badgeTier[i]));
                    found++;
                }
            }
            if (found > 0) s.Save();
            return found;
        }

        /// <summary>The best earned badge, for the profile title when none was chosen.</summary>
        public static int BestBadge(SaveData s)
        {
            int best = -1, bt = 0;
            for (int i = 0; i < Count; i++)
                if (s.stats.badgeTier[i] > bt) { bt = s.stats.badgeTier[i]; best = i; }
            return best;
        }

        public static string Title(SaveData s)
        {
            int b = s.stats.titleBadge >= 0 && s.stats.badgeTier[s.stats.titleBadge] > 0 ? s.stats.titleBadge : BestBadge(s);
            if (b < 0) return "";
            return "★ " + TierNames[s.stats.badgeTier[b]] + " " + All[b].name;
        }

        public static Color Metal(int tier) => tier switch
        {
            1 => Pal.H("#C27A3F"),
            2 => Pal.H("#9AA5B1"),
            3 => Pal.H("#D4A72C"),
            _ => Pal.H("#AEB8B4"),
        };

        public static Color MetalLight(int tier) => tier switch
        {
            1 => Pal.H("#E3A671"),
            2 => Pal.H("#E6EBF0"),
            3 => Pal.H("#FFE48A"),
            _ => Pal.H("#DDE3E0"),
        };

        public static Color MetalDark(int tier) => tier switch
        {
            1 => Pal.H("#6B3B16"),
            2 => Pal.H("#5F6B78"),
            3 => Pal.H("#8A6410"),
            _ => Pal.H("#7C8784"),
        };
    }

    /// <summary>Recording finished online matches into the stats.</summary>
    public static class StatsRecorder
    {
        public static void Online(SaveData s, MatchSetup setup, int place, string vs, string score, int net, int pot, bool left = false)
        {
            var st = s.stats;
            var d = setup.desk;
            if (string.IsNullOrEmpty(st.since)) st.since = DateTime.Now.ToString("MMM yyyy");
            bool won = place == 1;
            st.played++;
            if (won) { st.won++; st.streak++; st.bestStreak = Mathf.Max(st.bestStreak, st.streak); }
            else { st.lost++; st.streak = 0; }
            if (setup.IsParty)
            {
                st.partyPlayed++;
                if (place == 1) st.party1st++;
                else if (place == 2) st.party2nd++;
            }
            if (!left) st.finished++;
            if (d != null)
            {
                int k = st.deskIds.IndexOf(d.id);
                if (k < 0) { st.deskIds.Add(d.id); st.deskPlays.Add(1); }
                else st.deskPlays[k]++;
                if (won)
                {
                    st.tierWins[(int)d.tier]++;
                    if (d.index > st.highestDesk) st.highestDesk = d.index;
                }
                if (pot > 0)
                {
                    st.potWon += pot;
                    st.biggestPot = Mathf.Max(st.biggestPot, pot);
                }
            }
            st.recent.Insert(0, new MatchRecord
            {
                won = won, vs = vs ?? "", desk = d != null ? d.name : "", score = score ?? "", net = net,
                free = d == null || d.Free, left = left, at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            while (st.recent.Count > 10) st.recent.RemoveAt(st.recent.Count - 1);
            s.Save();
            Badges.Check(s);
            ProfileCloud.Publish(s);
        }

        public static string Ago(long at)
        {
            var span = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(at);
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " min ago";
            if (span.TotalDays < 1) return (int)span.TotalHours + " h ago";
            if (span.TotalDays < 2) return "yesterday";
            return (int)span.TotalDays + " days ago";
        }
    }
}
