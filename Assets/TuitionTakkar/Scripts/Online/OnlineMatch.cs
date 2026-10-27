using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Starts an online match from a room, and shows the result (mockup O8):
    /// winner gets +1 on the Total Wins leaderboard and 30 marks, the other player 2.
    /// </summary>
    public static class OnlineMatch
    {
        /// <summary>Biggest room the online desk supports so far (3–4 players come next).</summary>
        public const int MaxPlayers = 4;

        /// <summary>The room seed of the match this phone last started (so a finished match isn't restarted).</summary>
        public static string LastMatchId = "";

        static OnlineLink link;
        static Reactions reactions;
        static ConnectionWatch watch;

        static void DropReactions()
        {
            reactions?.Detach();
            reactions = null;
            watch?.Detach();
            watch = null;
        }

        /// <summary>The app came back from the background: catch up with the room straight away.</summary>
        public static void OnResume() => link?.Nudge(99f);

        /// <summary>Left, dropped or counted out of a running match: a loss in the stats (the fee is gone).</summary>
        static void RecordLeft(GameManager gm)
        {
            var s = gm.Match.Setup;
            if (s == null || s.mode != MatchMode.Online || !gm.Match.Running) return;
            StatsTracker.Stop();
            int them = s.IsParty ? -1 : 1 - s.localIndex;
            StatsRecorder.Online(gm.Save, s, s.Players, them >= 0 ? s.names[them] : s.Players + " players", "left", -s.feePaid, 0, true);
        }

        /// <summary>No connection for too long: the match is lost.</summary>
        static async void GiveUp(GameManager gm, OnlineLink l)
        {
            if (link != l) return;
            RecordLeft(gm);
            link = null;
            DropReactions();
            l.Detach();
            gm.Match.Stop();
            gm.ShowHome();
            gm.UI.ShowOverlay(OnlineScreens.Error(gm, "CONNECTION LOST", "you were offline too long, so the match was lost. check your internet and try again."));
            try { await l.Room.LeaveAsync(); } catch (Exception) { }
        }

        public static void Begin(GameManager gm, OnlineRoom room)
        {
            link?.Detach();
            DropReactions();
            var l = new OnlineLink(gm, room);
            link = l;
            LastMatchId = l.MatchId;

            int n = Mathf.Clamp(l.Order.Count, 2, MaxPlayers);
            var s = new MatchSetup(n) { mode = MatchMode.Online, localIndex = l.LocalIndex, net = l };
            for (int i = 0; i < n; i++)
            {
                var p = room.Find(l.PlayerIdAt(i));
                s.pens[i] = Catalog.Pen(OnlineRoom.PenOf(p));
                s.names[i] = OnlineRoom.NameOf(p);
            }
            // Same pen twice? Recolour the later one, identically on every phone.
            var recolours = new[] { ("#2A9D8F", "#16324F"), ("#C8414B", "#1F2A44"), ("#7B5EA7", "#4A3570") };
            for (int i = 1; i < n; i++)
                for (int j = 0; j < i; j++)
                    if (s.pens[i].id == s.pens[j].id)
                    {
                        var c = recolours[(i - 1) % recolours.Length];
                        s.pens[i] = s.pens[i].Recolor(Pal.H(c.Item1), Pal.H(c.Item2));
                        break;
                    }

            // Sit down: the entry fee goes into the pot now that the match is really starting.
            s.desk = room.Desk;
            s.feePaid = Mathf.Clamp(s.desk.fee, 0, gm.Save.marks);
            if (s.feePaid > 0)
            {
                gm.Save.marks -= s.feePaid;
                gm.Save.Save();
            }

            gm.UI.ShowScreen(Intro(s));
            _ = Friends.SetPresenceAsync(gm, "match", s.desk != null ? s.desk.name : "");
            gm.UI.ScreenLayer.schedule.Execute(() =>
            {
                if (link != l || room.Closed) { Refund(gm, s); return; }
                gm.StartMatch(s);
                reactions = new Reactions(gm, l);
                gm.UI.Hud.Root.Add(reactions.Root);
                watch = new ConnectionWatch(gm, l, () => GiveUp(gm, l));
                gm.UI.Hud.Root.Add(watch.Root);
            }).StartingIn(2200);
        }

        /// <summary>The match never got going (room closed in the intro): the entry fee comes back.</summary>
        static void Refund(GameManager gm, MatchSetup s)
        {
            if (s.feePaid <= 0) return;
            gm.Save.marks += s.feePaid;
            s.feePaid = 0;
            gm.Save.Save();
        }

        /// <summary>Desk name, tier and pot on the intro card.</summary>
        static VisualElement PotLine(MatchSetup s)
        {
            var d = s.desk;
            var col = UIKit.Col().St(st => { st.alignItems = Align.Center; st.marginBottom = 14; });
            if (d == null) return col;
            var tier = UIKit.Strong(d.Number + " · " + Desks.TierName(d.tier) + " DESK", 11f, Pal.Ink, TextAnchor.MiddleCenter).St(st =>
            {
                st.backgroundColor = d.Trim ?? Pal.H("#E9D9B4"); UIKit.Pad(st, 3, 10, 3, 10); UIKit.Radius(st, 6); st.letterSpacing = 1f;
            });
            col.Add(tier);
            col.Add(UIKit.Disp(d.name.ToUpperInvariant(), 26f, Color.white, TextAnchor.MiddleCenter).St(st => st.marginTop = 6));
            if (d.Free)
                col.Add(UIKit.HandText("friendly desk · nothing at stake", 17f, Pal.MistLight, TextAnchor.MiddleCenter));
            else
            {
                var pot = UIKit.Row().St(st => { st.marginTop = 6; st.justifyContent = Justify.Center; st.alignItems = Align.Center; });
                pot.Add(new CoinIcon(26f));
                pot.Add(UIKit.Disp(Desks.Money(d.Pot(s.Players)), 34f, Pal.Yellow).St(st => st.marginLeft = 8));
                pot.Add(UIKit.Strong(" POT", 14f, Pal.Yellow));
                col.Add(pot);
                string split = s.Players > 2 ? "1st takes " + Desks.Money(d.Prize(s.Players, 1)) + " · 2nd takes " + Desks.Money(d.Prize(s.Players, 2)) : "winner takes it all";
                col.Add(UIKit.HandText("you paid " + Desks.Money(s.feePaid) + " to sit down. " + split + ".", 16f, Pal.MistLight, TextAnchor.MiddleCenter).St(st => st.marginTop = 2));
            }
            return col;
        }

        /// <summary>"YOU vs RiyaFlicks" card while both phones get ready.</summary>
        static VisualElement Intro(MatchSetup s)
        {
            var root = UIKit.Col().St(st => { st.backgroundColor = Pal.Night; st.justifyContent = Justify.Center; st.alignItems = Align.Center; UIKit.Pad(st, 40, 24, 40, 24); });
            root.pickingMode = PickingMode.Position;
            root.Add(PotLine(s));
            if (s.IsParty)
            {
                root.Add(UIKit.Strong("ONLINE · " + s.Players + " PLAYERS · LAST PEN STANDING", 13f, Pal.Mist, TextAnchor.MiddleCenter).St(st => st.letterSpacing = 1f));
                var grid = UIKit.Row().St(st => { st.marginTop = 24; st.flexWrap = Wrap.Wrap; st.justifyContent = Justify.Center; });
                var cols = new[] { Pal.Yellow, Pal.Pink, Pal.Sky, Pal.Teal };
                for (int i = 0; i < s.Players; i++)
                {
                    var side = Side(i == s.localIndex ? "YOU" : s.names[i], s.pens[i], i == s.localIndex ? Pal.Yellow : cols[i % cols.Length], i % 2 == 0 ? -3f : 3f);
                    side.style.marginLeft = side.style.marginRight = 6;
                    side.style.marginBottom = 12;
                    grid.Add(side);
                }
                root.Add(grid);
                root.Add(UIKit.HandText(s.localIndex == 0 ? "you flick first. flick at any pen." : s.names[0] + " flicks first. flick at any pen.", 19f, Pal.MistLight, TextAnchor.MiddleCenter).St(st => st.marginTop = 16));
                UIRoot.PopIn(grid);
                return root;
            }
            root.Add(UIKit.Strong("ONLINE MATCH · BEST OF " + (GameConfig.RoundsToWin * 2 - 1), 13f, Pal.Mist, TextAnchor.MiddleCenter).St(st => st.letterSpacing = 1f));
            int me = s.localIndex, them = 1 - me;
            var row = UIKit.Row().St(st => { st.marginTop = 24; st.justifyContent = Justify.Center; });
            row.Add(Side("YOU", s.pens[me], Pal.Yellow, -3f));
            row.Add(UIKit.Disp("VS", 34f, Pal.Yellow, TextAnchor.MiddleCenter).St(st => { st.marginLeft = 14; st.marginRight = 14; }));
            row.Add(Side(s.names[them], s.pens[them], Pal.Sky, 3f));
            root.Add(row);
            root.Add(UIKit.HandText(s.localIndex == 0 ? "you flick first." : s.names[0] + " flicks first.", 19f, Pal.MistLight, TextAnchor.MiddleCenter).St(st => st.marginTop = 24));
            UIRoot.PopIn(row);
            return root;
        }

        static VisualElement Side(string name, PenDef pen, Color c, float rot)
        {
            var col = UIKit.Col().St(st => { st.alignItems = Align.Center; st.backgroundColor = c; UIKit.Pad(st, 12, 12, 12, 12); st.width = 140; });
            UIKit.Rot(col, rot);
            col.Add(UIKit.Disp(name.ToUpperInvariant(), 20f, Pal.Ink, TextAnchor.MiddleCenter));
            col.Add(PenIcon.Build(pen, 110f).St(st => st.marginTop = 8));
            return col;
        }

        // =====================================================================
        // result (O8)
        // =====================================================================

        /// <summary>reason: null for a normal finish, or why the other player was out (left, dropped).</summary>
        public static void Finish(GameManager gm, MatchSetup s, int winner, int[] score, string reason)
        {
            var l = s.net;
            l?.Detach();
            DropReactions();
            if (link == l) link = null;
            var room = l?.Room;

            bool iWon = winner == s.localIndex;
            var pay = Settle(gm, s, iWon ? 1 : 2, true);
            if (iWon) gm.Save.onlineWins++;
            gm.Save.Save();
            {
                int me0 = s.localIndex, them0 = 1 - me0;
                StatsRecorder.Online(gm.Save, s, iWon ? 1 : 2, s.names[them0], score[me0] + "–" + score[them0], pay.net, pay.staked ? pay.won : 0);
            }

            var root = UIKit.Col().St(st => { st.backgroundColor = Pal.Night; UIKit.Pad(st, 44, 20, 26, 20); });
            root.pickingMode = PickingMode.Position;

            var card = UIKit.Col().St(st => { UIKit.Pad(st, 22, 20, 22, 20); st.alignItems = Align.Center; });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);
            int me = s.localIndex, them = 1 - me;
            card.Add(UIKit.Strong("ONLINE · " + score[me] + "–" + score[them], 13f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.letterSpacing = 1f));
            card.Add(UIKit.Disp(iWon ? "YOU WIN!" : s.names[winner].ToUpperInvariant() + " WINS", 40f, Pal.Ink, TextAnchor.MiddleCenter).St(st => st.marginTop = 6));
            if (!string.IsNullOrEmpty(reason))
                card.Add(UIKit.HandText(reason, 18f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.marginTop = 4));
            else
                card.Add(UIKit.HandText(iWon ? "last pen standing. well flicked." : "so close. one more?", 18f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.marginTop = 4));

            var stamp = UIKit.El().St(st =>
            {
                UIKit.Border(st, 5f, iWon ? Pal.Green : Pal.Red); UIKit.Radius(st, 8); UIKit.Pad(st, 6, 16, 6, 16); st.marginTop = 14;
            });
            stamp.Add(UIKit.Disp(iWon ? "WINNER" : "GOOD GAME", 28f, iWon ? Pal.Green : Pal.Red, TextAnchor.MiddleCenter).St(st => st.letterSpacing = 2f));
            UIKit.Rot(stamp, -8f);
            card.Add(stamp);
            UIRoot.PopIn(stamp, 200);
            stamp.schedule.Execute(() => gm.Audio.Stamp()).StartingIn(220);

            // Rewards
            var rewards = UIKit.Row(Align.Stretch).St(st => { st.marginTop = 18; st.alignSelf = Align.Stretch; st.justifyContent = Justify.SpaceBetween; });
            Label winsText = null;
            if (iWon)
            {
                var w = Box(Pal.Yellow);
                w.Add(UIKit.Disp("+1", 30f, Pal.Ink, TextAnchor.MiddleCenter));
                winsText = UIKit.Strong("online win · " + gm.Save.onlineWins + " total", 12f, Pal.Ink, TextAnchor.MiddleCenter);
                w.Add(winsText);
                rewards.Add(w);
            }
            rewards.Add(MarksBox(gm, pay, iWon, 24f, 30f));
            card.Add(rewards);
            card.Add(XpLine(pay));
            var rankLine = UIKit.HandText(iWon ? "updating the leaderboard…" : "", 16f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.marginTop = 10);
            card.Add(rankLine);
            root.Add(card);
            UIRoot.PopIn(card, 80);

            root.Add(UIKit.Grow());
            var buttons = UIKit.Row().St(st => st.justifyContent = Justify.SpaceBetween);
            var home = UIKit.Btn("HOME", () => LeaveToHome(gm, room), Pal.Night, Color.white, UIKit.Display, 20f, 56f, 16f, null, Color.white);
            home.style.width = Length.Percent(38);
            bool roomAlive = room != null && !room.Closed;
            var again = UIKit.PrimaryBtn(roomAlive ? "PLAY AGAIN" : "PLAY ONLINE", () => PlayAgain(gm, room), 56f);
            again.style.width = Length.Percent(58);
            buttons.Add(home, again);
            root.Add(buttons);

            gm.UI.ShowScreen(root);
            if (iWon) { ReportWin(gm, winsText, rankLine); Reminders.AskAfterWin(gm); }
            ProfileScreens.ShowPendingBadges(gm, 2200);
            _ = Friends.SetPresenceAsync(gm, "home");
        }

        // =====================================================================
        // 3–4 player result: places 1st..4th
        // =====================================================================

        public static void FinishParty(GameManager gm, MatchSetup s, int[] places, int winner, string reason)
        {
            var l = s.net;
            l?.Detach();
            DropReactions();
            if (link == l) link = null;
            var room = l?.Room;

            int mine = places[s.localIndex];
            bool iWon = mine == 1;
            bool real = s.mode == MatchMode.Online; // the editor's hot-seat test pays nothing
            var pay = Settle(gm, s, mine, real);
            if (iWon && real) gm.Save.onlineWins++;
            gm.Save.Save();
            if (real) StatsRecorder.Online(gm.Save, s, mine, s.Players + " players", HudView.Ordinal(mine), pay.net, pay.staked ? pay.won : 0);

            var root = UIKit.Col().St(st => { st.backgroundColor = Pal.Night; UIKit.Pad(st, 44, 20, 26, 20); });
            root.pickingMode = PickingMode.Position;
            var card = UIKit.Col().St(st => { UIKit.Pad(st, 22, 18, 20, 18); st.alignItems = Align.Center; });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);
            card.Add(UIKit.Strong("ONLINE · " + s.Players + " PLAYERS", 13f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.letterSpacing = 1f));
            card.Add(UIKit.Disp("LAST PEN\nSTANDING!", 36f, Pal.Ink, TextAnchor.MiddleCenter).St(st => st.marginTop = 4));
            card.Add(UIKit.Disp(iWon ? "YOU WIN" : "YOU CAME " + HudView.Ordinal(mine).ToUpperInvariant(), 26f, iWon ? Pal.Green : Pal.Red, TextAnchor.MiddleCenter).St(st => st.marginTop = 6));
            if (!string.IsNullOrEmpty(reason)) card.Add(UIKit.HandText(reason, 16f, Pal.InkSoft, TextAnchor.MiddleCenter));

            // Places, best first.
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < places.Length; i++) order.Add(i);
            order.Sort((a, b) => places[a].CompareTo(places[b]));
            var list = UIKit.Col().St(st => { st.alignSelf = Align.Stretch; st.marginTop = 12; });
            foreach (int i in order)
            {
                bool me = i == s.localIndex;
                var medal = places[i] == 1 ? Pal.H("#E6B422") : places[i] == 2 ? Pal.H("#B7C0C7") : places[i] == 3 ? Pal.H("#C8875A") : Pal.InkSoft;
                var row = UIKit.Row().St(st =>
                {
                    st.marginTop = 6; UIKit.Pad(st, 6, 10, 6, 10); UIKit.Radius(st, 10);
                    st.backgroundColor = me ? Pal.Yellow : Color.white; UIKit.Border(st, me ? 2f : 0f, Pal.Ink);
                });
                row.Add(UIKit.Disp(HudView.Ordinal(places[i]), 20f, medal).St(st => st.width = 52));
                row.Add(UIKit.Strong(s.names[i] + (me ? " (you)" : ""), 15f, Pal.Ink).St(st => { st.flexGrow = 1; st.flexShrink = 1; }));
                row.Add(PenIcon.Build(s.pens[i], 80f));
                list.Add(row);
            }
            card.Add(list);

            var rewards = UIKit.Row(Align.Stretch).St(st => { st.marginTop = 14; st.alignSelf = Align.Stretch; st.justifyContent = Justify.SpaceBetween; });
            Label winsText = null;
            if (iWon)
            {
                var w = Box(Pal.Yellow);
                w.Add(UIKit.Disp("+1", 28f, Pal.Ink, TextAnchor.MiddleCenter));
                winsText = UIKit.Strong("online win · " + gm.Save.onlineWins + " total", 12f, Pal.Ink, TextAnchor.MiddleCenter);
                w.Add(winsText);
                rewards.Add(w);
            }
            rewards.Add(MarksBox(gm, pay, iWon, 22f, 28f));
            card.Add(rewards);
            if (real) card.Add(XpLine(pay));
            var rankLine = UIKit.HandText(iWon ? "updating the leaderboard…" : "", 15f, Pal.InkSoft, TextAnchor.MiddleCenter).St(st => st.marginTop = 8);
            card.Add(rankLine);
            root.Add(card);
            UIRoot.PopIn(card, 80);

            root.Add(UIKit.Grow());
            var buttons = UIKit.Row().St(st => st.justifyContent = Justify.SpaceBetween);
            var home = UIKit.Btn("HOME", () => LeaveToHome(gm, room), Pal.Night, Color.white, UIKit.Display, 20f, 56f, 16f, null, Color.white);
            home.style.width = Length.Percent(38);
            bool roomAlive = room != null && !room.Closed;
            var again = UIKit.PrimaryBtn(roomAlive ? "PLAY AGAIN" : "PLAY ONLINE", () => PlayAgain(gm, room), 56f);
            again.style.width = Length.Percent(58);
            buttons.Add(home, again);
            root.Add(buttons);

            gm.UI.ShowScreen(root);
            gm.Audio.Stamp();
            if (iWon && real) { ReportWin(gm, winsText, rankLine); Reminders.AskAfterWin(gm); }
            ProfileScreens.ShowPendingBadges(gm, 2200);
            _ = Friends.SetPresenceAsync(gm, "home");
        }

        /// <summary>This phone was counted out (away too long, or the room closed).</summary>
        public static async void Kicked(GameManager gm, string reason)
        {
            RecordLeft(gm);
            var l = link;
            link = null;
            DropReactions();
            l?.Detach();
            gm.ShowHome();
            gm.UI.ShowOverlay(OnlineScreens.Error(gm, "OUT OF THE MATCH", reason));
            if (l != null)
            {
                try { await l.Room.LeaveAsync(); } catch (Exception) { }
            }
        }

        // =====================================================================
        // the pot
        // =====================================================================

        public struct Payout
        {
            public int start;       // marks before sitting down
            public int won;         // marks paid out now (prize or old reward)
            public int net;         // won - fee
            public int fee;
            public bool staked;     // a paid desk
            public int xp, levelBefore, levelAfter;
            public DeskDef opened;  // a new desk opened by levelling up
        }

        /// <summary>Pays the prize for a place (pot split) or the old friendly rewards, and the XP.</summary>
        static Payout Settle(GameManager gm, MatchSetup s, int place, bool real)
        {
            var d = s.desk;
            var p = new Payout { fee = s.feePaid, start = gm.Save.marks + s.feePaid };
            if (!real) return p;
            p.staked = d != null && !d.Free;
            if (p.staked) p.won = d.Prize(s.Players, place);
            else p.won = place == 1 ? Economy.OnlineWin : place == 2 && s.Players > 2 ? Economy.OnlineSecond : Economy.LossConsolation;
            p.net = p.won - p.fee;
            gm.Save.marks += p.won;

            p.levelBefore = OnlineLevel.Level(gm.Save.onlineXp);
            p.xp = OnlineLevel.ForMatch(d, place, s.Players);
            gm.Save.onlineXp += p.xp;
            p.levelAfter = OnlineLevel.Level(gm.Save.onlineXp);
            foreach (var desk in Desks.All)
                if (desk.level > p.levelBefore && desk.level <= p.levelAfter) p.opened = desk;
            s.feePaid = 0;
            return p;
        }

        static VisualElement MarksBox(GameManager gm, Payout pay, bool iWon, float coin, float size)
        {
            var m = Box(Pal.Paper);
            var mr = UIKit.Row().St(st => st.justifyContent = Justify.Center);
            mr.Add(new CoinIcon(coin));
            string big = pay.staked ? (pay.net >= 0 ? "+" + Desks.Money(pay.won) : "-" + Desks.Money(-pay.net)) : "+" + pay.won;
            mr.Add(UIKit.Disp(big, size, pay.staked && pay.net < 0 ? Pal.Red : Pal.Ink).St(st => st.marginLeft = 6));
            m.Add(mr);
            string what = !pay.staked ? "marks" : pay.won > 0 ? (iWon ? "pot won" : "your share") : "entry lost";
            m.Add(UIKit.Strong(what + " · " + Desks.Money(pay.start) + " → " + Desks.Money(gm.Save.marks), 12f, Pal.Ink, TextAnchor.MiddleCenter));
            if (!iWon) m.style.width = Length.Percent(100);
            return m;
        }

        static VisualElement XpLine(Payout pay)
        {
            var col = UIKit.Col().St(st => { st.marginTop = 10; st.alignSelf = Align.Stretch; st.alignItems = Align.Center; });
            if (pay.xp <= 0) return col;
            string lv = pay.levelAfter > pay.levelBefore ? "LEVEL UP! you're level " + pay.levelAfter : "level " + pay.levelAfter;
            col.Add(UIKit.Strong("+" + pay.xp + " XP · " + lv, 13f, pay.levelAfter > pay.levelBefore ? Pal.Green : Pal.InkSoft, TextAnchor.MiddleCenter));
            if (pay.opened != null)
                col.Add(UIKit.HandText("new desk open: " + pay.opened.name + "!", 16f, Pal.Ink, TextAnchor.MiddleCenter));
            return col;
        }

        static VisualElement Box(Color c)
        {
            return UIKit.Col().St(st =>
            {
                st.width = Length.Percent(48); st.backgroundColor = c; UIKit.Radius(st, 12); UIKit.Pad(st, 10, 8, 10, 8);
                st.alignItems = Align.Center; UIKit.Border(st, 2f, Pal.Ink);
            });
        }

        static async void ReportWin(GameManager gm, Label winsText, Label rankLine)
        {
            if (!OnlineServices.OnLeaderboard(gm.Save))
            {
                // Guests: the win is real, but only Google players go on the leaderboard.
                if (rankLine != null) rankLine.text = "playing as guest: sign in with Google to put your wins on the leaderboard.";
                return;
            }
            int total = await OnlineServices.ReportWinAsync(gm.Save);
            if (total > 0)
            {
                gm.Save.onlineWins = total;
                gm.Save.Save();
            }
            var me = await OnlineServices.MeAsync();
            if (rankLine.panel == null) return;
            if (total > 0 && winsText != null) winsText.text = "online win · " + total + " total";
            rankLine.text = me.HasValue ? "you're #" + me.Value.rank + " on the leaderboard" : total > 0 ? "" : "couldn't reach the leaderboard. your win is kept for next time.";
        }

        static async void PlayAgain(GameManager gm, OnlineRoom room)
        {
            gm.World.ClearPens();
            if (room == null || room.Closed)
            {
                OnlineScreens.ShowPlay(gm);
                return;
            }
            try
            {
                if (room.IsHost) await room.ReopenAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Reopen room: " + e.Message);
            }
            OnlineScreens.ShowRoom(gm, room);
        }

        static async void LeaveToHome(GameManager gm, OnlineRoom room)
        {
            gm.ShowHome();
            if (room != null) await room.LeaveAsync();
        }

        /// <summary>Pause button during an online match: time can't stop, so offer to leave.</summary>
        public static VisualElement LeaveSheet(GameManager gm)
        {
            var o = UIKit.Col().St(st => { st.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f); st.justifyContent = Justify.FlexEnd; });
            o.pickingMode = PickingMode.Position;
            var c = UIKit.Col().St(st =>
            {
                st.backgroundColor = Pal.Paper; UIKit.Pad(st, 22, 20, 30, 20);
                st.borderTopLeftRadius = st.borderTopRightRadius = 22;
            });
            c.Add(UIKit.Disp("LEAVE THE MATCH?", 28f, Pal.Ink));
            c.Add(UIKit.HandText("the match keeps going on the other phone. leaving counts as a loss.", 18f, Pal.Ink).St(st => st.marginTop = 6));
            var stay = UIKit.PrimaryBtn("KEEP PLAYING", () => gm.UI.HideOverlay(), 56f);
            stay.style.marginTop = 16;
            c.Add(stay);
            var leave = UIKit.Btn("LEAVE MATCH", async () =>
            {
                var l = link;
                link = null;
                DropReactions();
                RecordLeft(gm);
                gm.Match.Stop();
                l?.Detach();
                gm.UI.HideOverlay();
                gm.ShowHome();
                if (l != null)
                {
                    l.SendQuit();
                    await l.Room.LeaveAsync();
                }
            }, Color.clear, Pal.Red, UIKit.Display, 20f, 54f, 16f, null, Pal.Red);
            leave.style.marginTop = 10;
            c.Add(leave);
            o.Add(c);
            UIRoot.PopIn(c);
            return o;
        }
    }
}
