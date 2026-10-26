using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Online screens (mockups O2–O9): sign in, pick a name, Play Online, finding classmates,
    /// private room and the leaderboard. The match itself lives in OnlineMatch.
    /// </summary>
    public static class OnlineScreens
    {
        static readonly Color Chalk = Pal.H("#2E4A3C");
        static readonly Color ChalkEdge = Pal.H("#6B4A2B");
        static readonly Color Cork = Pal.H("#C89B6A");
        static int seats = 2;
        static string suggestedName = "";

        // =====================================================================
        // entry
        // =====================================================================

        /// <summary>Play Online from the home screen. leaderboard: open the board instead.</summary>
        public static async void Enter(GameManager gm, bool leaderboard = false)
        {
            if (!NetPopups.IsOnline) { gm.UI.ShowOverlay(NetPopups.NoInternet(gm)); return; }
            gm.UI.ShowOverlay(Busy("connecting…"));
            try
            {
                await OnlineServices.InitAsync();
            }
            catch (Exception e)
            {
                gm.UI.ShowOverlay(Error(gm, "CAN'T CONNECT", OnlineServices.Explain(e)));
                return;
            }
            gm.UI.HideOverlay();
            if (leaderboard && OnlineServices.HasAccount(gm.Save)) ShowLeaderboard(gm);
            else Next(gm);
        }

        /// <summary>Sign in → name → Play Online, whichever is still missing.</summary>
        static void Next(GameManager gm)
        {
            var s = gm.Save;
            if (!OnlineServices.HasAccount(s)) gm.UI.ShowScreen(SignIn(gm));
            else if (OnlineServices.NameProblem(s.onlineName) != null) gm.UI.ShowScreen(Name(gm));
            else ShowPlay(gm);
        }

        public static void ShowPlay(GameManager gm)
        {
            gm.UI.ShowScreen(Play(gm));
            if (inviteCode == null) return;
            string code = inviteCode, from = inviteFrom;
            inviteCode = inviteFrom = null;
            gm.UI.ShowOverlay(JoinSheet(gm, code, from));
        }

        static string inviteCode, inviteFrom;

        /// <summary>
        /// An invite link was tapped: sign in / pick a name if needed, then Play Online
        /// with the Join sheet filled in.
        /// </summary>
        public static void JoinFromInvite(GameManager gm, string code, string from)
        {
            inviteCode = code;
            inviteFrom = from;
            Enter(gm);
        }

        // =====================================================================
        // O2 sign in
        // =====================================================================

        static VisualElement SignIn(GameManager gm)
        {
            var root = Screen();
            root.Add(Header("PLAY ONLINE", null, () => gm.ShowHome()));

            var body = UIKit.Col().St(s => { UIKit.Pad(s, 26, 22, 0, 22); });
            var card = Paper(-1f);
            card.Add(UIKit.Disp("HOW DO YOU\nWANT TO PLAY?", 34f, Pal.Ink));
            card.Add(UIKit.HandText("sign in with Google to get on the class leaderboard, or jump in as a guest.", 18f, Pal.Ink).St(s => s.marginTop = 6));

            var google = UIKit.Btn("", () => DoGoogle(gm), Color.white, Pal.Ink, UIKit.BodyBold, 17f, 58f, 14f, Pal.H("#C9D3DE"), Pal.Ink);
            google.style.marginTop = 20;
            var gRow = UIKit.Row().St(s => { s.justifyContent = Justify.Center; s.flexGrow = 1; });
            gRow.pickingMode = PickingMode.Ignore;
            gRow.Add(GoogleG(24f));
            gRow.Add(UIKit.Strong("Continue with Google", 17f, Pal.Ink).St(s => s.marginLeft = 10));
            google.Add(gRow);
            card.Add(google);
            var gPoints = UIKit.Col().St(s => s.marginTop = 8);
            foreach (var p in new[] { "your wins go on the leaderboard", "pick your own name, keep it on a new phone", "your email is never shown to other players" })
                gPoints.Add(UIKit.Plain("•  " + p, 13f, Pal.InkSoft).St(s => s.marginTop = 3));
            card.Add(gPoints);

            var guest = UIKit.Btn("Continue as Guest", () => DoGuest(gm), Pal.Yellow, Pal.Ink, UIKit.BodyBold, 17f, 58f, 14f, Pal.H("#C7A928"), Pal.Ink);
            guest.style.marginTop = 18;
            card.Add(guest);
            var qPoints = UIKit.Col().St(s => s.marginTop = 8);
            foreach (var p in new[] { "play right away as Guest_xxxxxx", "quick match, rooms and invites all work", "guest wins don't go on the leaderboard (sign in later any time)" })
                qPoints.Add(UIKit.Plain("•  " + p, 13f, Pal.InkSoft).St(s => s.marginTop = 3));
            card.Add(qPoints);
            body.Add(card);

            body.Add(UIKit.Btn("Not now", () => gm.ShowHome(), Color.clear, Pal.Ink, UIKit.BodyBold, 16f, 48f, 12f).St(s => s.marginTop = 8));
            root.Add(body);
            return root;
        }

        static async void DoGoogle(GameManager gm)
        {
            gm.UI.ShowOverlay(Busy("signing in…"));
            try
            {
                string gname = await OnlineServices.SignInWithGoogleAsync(gm.Save);
                suggestedName = SuggestName(gname);
                gm.UI.HideOverlay();
                Next(gm);
            }
            catch (Exception e)
            {
                if (e is GoogleSignIn.SignInException g && g.Cancelled) { gm.UI.HideOverlay(); return; }
                Debug.LogWarning("Google sign-in: " + e);
                gm.UI.ShowOverlay(Error(gm, "SIGN-IN DIDN'T WORK", OnlineServices.Explain(e)));
            }
        }

        static async void DoGuest(GameManager gm)
        {
            gm.UI.ShowOverlay(Busy("getting you a seat…"));
            try
            {
                await OnlineServices.SignInGuestAsync(gm.Save);
                gm.UI.HideOverlay();
                Next(gm);
            }
            catch (Exception e)
            {
                gm.UI.ShowOverlay(Error(gm, "SIGN-IN DIDN'T WORK", OnlineServices.Explain(e)));
            }
        }

        /// <summary>"Vatsal Patadiya" → "Vatsal" (letters/numbers only, 3–14 long).</summary>
        static string SuggestName(string google)
        {
            if (string.IsNullOrEmpty(google)) return "";
            var first = google.Trim().Split(' ')[0];
            var clean = System.Text.RegularExpressions.Regex.Replace(first, "[^A-Za-z0-9_]", "");
            if (clean.Length > OnlineServices.NameMax) clean = clean.Substring(0, OnlineServices.NameMax);
            return OnlineServices.NameProblem(clean) == null ? clean : "";
        }

        // =====================================================================
        // O3 name
        // =====================================================================

        static VisualElement Name(GameManager gm)
        {
            var save = gm.Save;
            var root = Screen();
            root.Add(Header("YOUR NAME", null, () => gm.ShowHome()));

            var body = UIKit.Col().St(s => UIKit.Pad(s, 26, 22, 0, 22));
            var card = Paper(0.8f);
            card.Add(UIKit.Disp("WHAT SHOULD THE\nCLASS CALL YOU?", 30f, Pal.Ink));
            card.Add(UIKit.HandText("shown on the leaderboard and in rooms. doesn't have to be your real name.", 17f, Pal.Ink).St(s => s.marginTop = 6));
            card.Add(UIKit.Strong("DISPLAY NAME", 12f, Pal.InkSoft).St(s => { s.marginTop = 16; s.letterSpacing = 0.8f; }));

            string start = !string.IsNullOrEmpty(save.onlineName) ? save.onlineName : suggestedName;
            var field = Field(start, OnlineServices.NameMax);
            field.style.marginTop = 6;
            card.Add(field);
            var hint = UIKit.Plain("3–14 letters, numbers or _", 12f, Pal.InkSoft).St(s => s.marginTop = 6);
            card.Add(hint);

            var preview = UIKit.Row().St(s =>
            {
                s.marginTop = 16; UIKit.Pad(s, 10, 12, 10, 12); s.backgroundColor = Color.white; UIKit.Radius(s, 12); UIKit.Border(s, 2f, Pal.Dash);
            });
            var avatar = UIKit.Avatar(Initial(start), Pal.Yellow, 44f);
            preview.Add(avatar);
            var pc = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
            var shown = UIKit.Strong(string.IsNullOrEmpty(start) ? "your name" : start, 16f, Pal.Ink);
            pc.Add(shown);
            pc.Add(PenIcon.Build(Catalog.Pen(save.equipped), 150f).St(s => s.marginTop = 4));
            preview.Add(pc);
            card.Add(preview);
            body.Add(card);

            var go = UIKit.PrimaryBtn("CONTINUE", null, 58f);
            go.style.marginTop = 20;
            body.Add(go);

            void Check()
            {
                string v = field.value?.Trim() ?? "";
                string problem = OnlineServices.NameProblem(v);
                hint.text = problem ?? "looks good!";
                hint.style.color = problem == null || v.Length == 0 ? Pal.Green : Pal.Red;
                if (v.Length == 0) { hint.text = "3–14 letters, numbers or _"; hint.style.color = Pal.InkSoft; }
                shown.text = v.Length == 0 ? "your name" : v;
                avatar.Q<Label>().text = Initial(v);
                go.SetEnabled(problem == null);
                go.style.opacity = problem == null ? 1f : 0.5f;
            }
            field.RegisterValueChangedCallback(_ => Check());
            Check();

            go.clicked += async () =>
            {
                string v = field.value.Trim();
                if (OnlineServices.NameProblem(v) != null) return;
                gm.UI.ShowOverlay(Busy("saving…"));
                try
                {
                    await OnlineServices.SetNameAsync(save, v);
                    gm.UI.HideOverlay();
                    ShowPlay(gm);
                }
                catch (Exception e)
                {
                    gm.UI.ShowOverlay(Error(gm, "COULDN'T SAVE", OnlineServices.Explain(e)));
                }
            };

            root.Add(body);
            return root;
        }

        static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        // =====================================================================
        // O4 Play Online
        // =====================================================================

        static VisualElement Play(GameManager gm)
        {
            var save = gm.Save;
            var root = Screen();
            bool guest = OnlineServices.IsGuest(save);
            root.Add(Header("PLAY ONLINE", (guest ? "playing as " : "signed in as ") + OnlineServices.DisplayName(save), () => gm.ShowHome()));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 20, 20, 24, 20));

            // Quick match (chalkboard)
            var quick = UIKit.Col().St(s =>
            {
                s.backgroundColor = Chalk; UIKit.Pad(s, 16, 16, 16, 16); UIKit.Radius(s, 10); UIKit.Border(s, 5f, ChalkEdge);
            });
            UIKit.Rot(quick, -0.6f);
            quick.Add(UIKit.Disp("QUICK MATCH", 28f, Color.white));
            quick.Add(UIKit.HandText("random classmates. last pen standing wins.", 16f, Pal.H("#E6EFE9")));
            var segs = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; });
            var segButtons = new List<VisualElement>();
            for (int n = 2; n <= 4; n++)
            {
                int count = n;
                bool ready = count <= OnlineMatch.MaxPlayers;
                var seg = UIKit.Col().St(s =>
                {
                    s.width = Length.Percent(31.5f); s.height = 64; s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                    UIKit.Radius(s, 12);
                });
                seg.Add(UIKit.Disp(count.ToString(), 26f, Pal.Ink, TextAnchor.MiddleCenter));
                seg.Add(UIKit.Strong(ready ? "PLAYERS" : "SOON", 10f, Pal.InkSoft, TextAnchor.MiddleCenter));
                if (ready)
                    seg.RegisterCallback<ClickEvent>(_ =>
                    {
                        gm.Audio.Ui();
                        seats = count;
                        for (int k = 0; k < segButtons.Count; k++) PaintSeg(segButtons[k], k + 2 == seats, k + 2 <= OnlineMatch.MaxPlayers);
                    });
                segButtons.Add(seg);
                segs.Add(seg);
            }
            seats = Mathf.Clamp(seats, 2, OnlineMatch.MaxPlayers);
            for (int k = 0; k < segButtons.Count; k++) PaintSeg(segButtons[k], k + 2 == seats, k + 2 <= OnlineMatch.MaxPlayers);
            quick.Add(segs);
            var find = UIKit.Btn("FIND A MATCH", () => DoQuick(gm), Pal.Yellow, Pal.Ink, UIKit.Display, 22f, 58f, 16f, Pal.H("#C7A928"));
            find.style.marginTop = 14;
            quick.Add(find);
            quick.Add(UIKit.Plain(OnlineMatch.MaxPlayers < 4 ? "3 and 4 player desks are coming next" : "bigger desk for 3 and 4 players", 12f, Pal.MistLight, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));
            body.Add(quick);

            // Play with friends
            var friends = Paper(0.6f);
            friends.style.marginTop = 18;
            friends.Add(UIKit.Disp("PLAY WITH FRIENDS", 24f, Pal.Ink));
            friends.Add(UIKit.HandText("make a room, share the code.", 16f, Pal.Ink));
            var fr = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; });
            var create = UIKit.Btn("+  Create room", () => DoCreate(gm), Pal.Yellow, Pal.Ink, UIKit.BodyBold, 15f, 54f, 12f, null, Pal.Ink);
            create.style.width = Length.Percent(48);
            var join = UIKit.Btn("Join with code", () => gm.UI.ShowOverlay(JoinSheet(gm)), Color.white, Pal.Ink, UIKit.BodyBold, 15f, 54f, 12f, null, Pal.Ink);
            join.style.width = Length.Percent(48);
            fr.Add(create, join);
            friends.Add(fr);
            body.Add(friends);

            // Leaderboard strip
            var lb = UIKit.Row().St(s =>
            {
                s.marginTop = 18; s.justifyContent = Justify.SpaceBetween; s.backgroundColor = Pal.Night; UIKit.Radius(s, 14); UIKit.Pad(s, 14, 16, 14, 16);
            });
            var lc = UIKit.Col().St(s => { s.flexShrink = 1; s.marginRight = 8; });
            var winsLabel = UIKit.Strong(guest ? "Playing as guest" : "You · " + save.onlineWins + " online wins", 15f, Color.white);
            var rankLabel = UIKit.Plain(guest ? "guest wins don't go on the leaderboard" : "checking the board…", 12f, Pal.Mist);
            rankLabel.style.whiteSpace = WhiteSpace.Normal;
            lc.Add(winsLabel, rankLabel);
            lb.Add(lc);
            lb.Add(UIKit.Strong("Leaderboard ›", 14f, Pal.Yellow));
            lb.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); ShowLeaderboard(gm); });
            body.Add(lb);
            if (guest)
            {
                // Sign in later: Google is linked to this same account, nothing is lost.
                var up = UIKit.Btn("", () => DoGoogle(gm), Color.white, Pal.Ink, UIKit.BodyBold, 15f, 52f, 12f, Pal.H("#C9D3DE"), Pal.Ink);
                up.style.marginTop = 12;
                var uRow = UIKit.Row().St(s => { s.justifyContent = Justify.Center; s.flexGrow = 1; });
                uRow.pickingMode = PickingMode.Ignore;
                uRow.Add(GoogleG(20f));
                uRow.Add(UIKit.Strong("Sign in with Google to get on the board", 14f, Pal.Ink).St(s => s.marginLeft = 8));
                up.Add(uRow);
                body.Add(up);
            }
            else LoadMyRank(gm, winsLabel, rankLabel);

            var signOut = UIKit.Btn(guest ? "Stop playing as guest" : "Sign out", () =>
            {
                OnlineServices.SignOut(save);
                gm.ShowHome();
            }, Color.clear, Pal.BenchText, UIKit.BodyBold, 13f, 40f, 10f);
            signOut.style.marginTop = 16;
            body.Add(signOut);

            scroll.Add(body);
            root.Add(scroll);
            return root;
        }

        static void PaintSeg(VisualElement seg, bool on, bool ready)
        {
            seg.style.backgroundColor = on ? Pal.Yellow : ready ? Pal.Paper : new Color(1f, 1f, 1f, 0.25f);
            seg.style.opacity = ready ? 1f : 0.6f;
            UIKit.Border(seg.style, on ? 3f : 0f, Pal.Ink);
        }

        static async void LoadMyRank(GameManager gm, Label wins, Label rank)
        {
            var me = await OnlineServices.MeAsync();
            if (wins.panel == null) return;
            if (me.HasValue)
            {
                gm.Save.onlineWins = me.Value.wins;
                gm.Save.Save();
                wins.text = "You · " + me.Value.wins + " online wins";
                rank.text = "#" + me.Value.rank + " on the board";
            }
            else rank.text = "win a match to get on the board";
        }

        static async void DoQuick(GameManager gm)
        {
            gm.UI.ShowOverlay(Busy("finding a room…"));
            try
            {
                var room = await OnlineRoom.QuickMatchAsync(gm, seats);
                gm.UI.HideOverlay();
                ShowRoom(gm, room);
            }
            catch (Exception e)
            {
                gm.UI.ShowOverlay(Error(gm, "NO LUCK", OnlineRoom.Explain(e)));
            }
        }

        static async void DoCreate(GameManager gm)
        {
            gm.UI.ShowOverlay(Busy("making a room…"));
            try
            {
                var room = await OnlineRoom.CreateAsync(gm, OnlineMatch.MaxPlayers, false);
                gm.UI.HideOverlay();
                ShowRoom(gm, room);
            }
            catch (Exception e)
            {
                gm.UI.ShowOverlay(Error(gm, "NO ROOM", OnlineRoom.Explain(e)));
            }
        }

        static VisualElement JoinSheet(GameManager gm, string code = "", string from = null)
        {
            bool invited = !string.IsNullOrEmpty(code);
            var o = Sheet(out var c);
            c.Add(UIKit.Disp(invited ? "YOU'RE INVITED!" : "JOIN A ROOM", 28f, Pal.Ink));
            c.Add(UIKit.HandText(!invited ? "type the 6-letter code your friend shared."
                : !string.IsNullOrEmpty(from) ? from + " saved you a seat. tap join to sit down."
                : "a classmate saved you a seat. tap join to sit down.", 16f, Pal.InkSoft));
            var field = Field(code ?? "", 6);
            field.style.marginTop = 12;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null) { input.style.fontSize = 30; input.style.unityTextAlign = TextAnchor.MiddleCenter; input.style.letterSpacing = 6; }
            c.Add(field);
            var go = UIKit.PrimaryBtn("JOIN", null, 56f);
            go.style.marginTop = 14;
            go.clicked += async () =>
            {
                string code = (field.value ?? "").Trim().ToUpperInvariant();
                if (code.Length < 6) return;
                gm.UI.ShowOverlay(Busy("joining " + code + "…"));
                try
                {
                    var room = await OnlineRoom.JoinByCodeAsync(gm, code);
                    gm.UI.HideOverlay();
                    ShowRoom(gm, room);
                }
                catch (Exception e)
                {
                    gm.UI.ShowOverlay(Error(gm, "CAN'T JOIN", OnlineRoom.Explain(e)));
                }
            };
            c.Add(go);
            c.Add(UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f).St(s => s.marginTop = 6));
            if (!invited) field.schedule.Execute(() => field.Focus()).StartingIn(200);
            return o;
        }

        // =====================================================================
        // O5 / O6 waiting: Quick Match finding + private room
        // =====================================================================

        public static void ShowRoom(GameManager gm, OnlineRoom room)
        {
            var root = Screen();
            root.Add(Header(room.IsQuick ? "QUICK MATCH" : "PRIVATE ROOM", room.Seats + " players · last pen standing", () => Leave(gm, room)));
            var content = UIKit.Col().St(s => { s.flexGrow = 1; UIKit.Pad(s, 20, 20, 24, 20); });
            root.Add(content);

            bool started = false;
            void Paint()
            {
                if (room.Closed) return;
                if (room.Playing && !started && room.Data("seed") != OnlineMatch.LastMatchId)
                {
                    started = true;
                    OnlineMatch.Begin(gm, room);
                    return;
                }
                content.Clear();
                if (room.IsQuick) PaintQuick(gm, room, content);
                else PaintPrivate(gm, room, content);
            }

            Action onChange = () => { if (root.panel != null) Paint(); };
            Action<string> onEnd = why =>
            {
                if (root.panel == null) return;
                ShowPlay(gm);
                gm.UI.ShowOverlay(Error(gm, "ROOM CLOSED", why));
            };
            room.Changed += onChange;
            room.Ended += onEnd;
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                room.Changed -= onChange;
                room.Ended -= onEnd;
            });

            gm.UI.ShowScreen(root);
            Paint();
        }

        static void PaintQuick(GameManager gm, OnlineRoom room, VisualElement content)
        {
            var board = UIKit.Col().St(s =>
            {
                s.backgroundColor = Chalk; UIKit.Pad(s, 18, 16, 18, 16); UIKit.Radius(s, 10); UIKit.Border(s, 5f, ChalkEdge);
            });
            board.Add(UIKit.HandText("finding classmates…", 20f, Color.white));
            int found = room.Players.Count;
            float waited = Time.realtimeSinceStartup - room.JoinedAt;
            var bar = UIKit.Row().St(s => { s.marginTop = 8; s.justifyContent = Justify.SpaceBetween; });
            bar.Add(UIKit.Strong(found + " of " + room.Seats + " found", 14f, Pal.Yellow));
            bar.Add(UIKit.Strong(Clock(waited), 14f, Pal.MistLight));
            board.Add(bar);
            var track = UIKit.El().St(s => { s.height = 8; s.marginTop = 6; UIKit.Radius(s, 4); s.backgroundColor = new Color(1, 1, 1, 0.15f); s.overflow = Overflow.Hidden; });
            track.Add(UIKit.El().St(s => { s.height = 8; s.width = Length.Percent(100f * found / Mathf.Max(1, room.Seats)); s.backgroundColor = Pal.Yellow; }));
            board.Add(track);

            var list = UIKit.Col().St(s => s.marginTop = 14);
            for (int i = 0; i < room.Seats; i++)
                list.Add(i < found ? SeatRow(room, room.Players[i], true) : EmptySeat("looking for a classmate…", true));
            board.Add(list);
            board.Add(UIKit.HandText("tip: watch your own pen near the edge. a flick that sends you off loses.", 15f, Pal.H("#E6EFE9")).St(s => s.marginTop = 12));
            content.Add(board);
            content.Add(UIKit.Grow());

            if (room.CanStart)
            {
                var start = UIKit.PrimaryBtn(room.Full ? "START MATCH" : "START WITH " + found, () => DoStart(gm, room), 56f);
                content.Add(start);
            }
            else if (room.InCharge && found >= OnlineRoom.MinPlayers)
                content.Add(UIKit.Plain("you can start with fewer players in " + Mathf.CeilToInt(OnlineRoom.QuickStartAfter - waited) + " s", 12f, Pal.BenchText, TextAnchor.MiddleCenter));
            else if (found >= OnlineRoom.MinPlayers)
                content.Add(UIKit.Plain("waiting for more classmates…", 12f, Pal.BenchText, TextAnchor.MiddleCenter));

            var cancel = UIKit.Btn("CANCEL", () => Leave(gm, room), Pal.Paper, Pal.Ink, UIKit.Display, 20f, 54f, 14f, null, Pal.Ink);
            cancel.style.marginTop = 10;
            content.Add(cancel);
        }

        /// <summary>Phone share sheet with the invite link (opens the game on the Join sheet).</summary>
        static void ShareInvite(GameManager gm, string code)
        {
            string me = gm.Save.onlineName;
            string msg = "Pen fight on Tuition Takkar! ✏️ Join my room: " + DeepLinks.InviteUrl(code, me) +
                         "\nor type code " + code + " in Play Online → Join with code.";
            NativeShare.Share(msg, "Join my Tuition Takkar room");
        }

        static void PaintPrivate(GameManager gm, OnlineRoom room, VisualElement content)
        {
            var cork = UIKit.Row().St(s =>
            {
                s.backgroundColor = Cork; UIKit.Pad(s, 16, 16, 16, 16); UIKit.Radius(s, 12); s.justifyContent = Justify.SpaceBetween;
                s.borderBottomWidth = 5; s.borderBottomColor = Pal.H("#8E6A42");
            });
            var codeCol = UIKit.Col();
            codeCol.Add(UIKit.Strong("ROOM CODE", 12f, Pal.Ink).St(s => s.letterSpacing = 1f));
            codeCol.Add(UIKit.Disp(room.Code, 40f, Pal.Ink).St(s => s.letterSpacing = 4f));
            cork.Add(codeCol);
            var copy = UIKit.Btn("Copy", null, Pal.Paper, Pal.Ink, UIKit.BodyBold, 14f, 44f, 12f, null, Pal.Ink);
            copy.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = room.Code;
                copy.text = "Copied!";
            };
            var share = UIKit.Btn("Share", () => ShareInvite(gm, room.Code), Pal.Ink, Color.white, UIKit.BodyBold, 14f, 44f, 12f, Pal.H("#0E1424"));
            share.style.marginTop = 8;
            var actions = UIKit.Col().St(s => s.alignItems = Align.Stretch);
            actions.Add(copy);
            actions.Add(share);
            cork.Add(actions);
            content.Add(cork);

            var head = UIKit.Row().St(s => { s.marginTop = 18; s.justifyContent = Justify.SpaceBetween; });
            head.Add(UIKit.Disp("CLASSMATES " + room.Players.Count + " / " + room.Seats, 22f, Pal.Ink));
            head.Add(UIKit.Plain("best of " + (GameConfig.RoundsToWin * 2 - 1), 12f, Pal.BenchText));
            content.Add(head);
            var list = UIKit.Col().St(s => s.marginTop = 8);
            for (int i = 0; i < room.Seats; i++)
                list.Add(i < room.Players.Count ? SeatRow(room, room.Players[i], false) : EmptySeat("empty seat · share the code", false));
            content.Add(list);
            content.Add(UIKit.Grow());

            if (room.InCharge)
            {
                var start = UIKit.PrimaryBtn("START MATCH", () => DoStart(gm, room), 56f);
                bool ok = room.CanStart;
                start.SetEnabled(ok);
                start.style.opacity = ok ? 1f : 0.5f;
                content.Add(start);
                content.Add(UIKit.Plain(ok ? "everyone in? start when ready." : "needs at least 2 players", 12f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            }
            else if (room.IsOwner)
                content.Add(UIKit.HandText("getting your room back…", 17f, Pal.BenchText, TextAnchor.MiddleCenter));
            else
                content.Add(UIKit.HandText("waiting for the host to start…", 17f, Pal.BenchText, TextAnchor.MiddleCenter));

            var leave = UIKit.Btn("LEAVE ROOM", () => Leave(gm, room), Pal.Paper, Pal.Ink, UIKit.Display, 18f, 50f, 14f, null, Pal.Ink);
            leave.style.marginTop = 10;
            content.Add(leave);
        }

        static VisualElement SeatRow(OnlineRoom room, Unity.Services.Lobbies.Models.Player p, bool dark)
        {
            string name = OnlineRoom.NameOf(p);
            bool me = p.Id == room.MyId;
            var row = UIKit.Row().St(s =>
            {
                s.marginTop = 8; UIKit.Pad(s, 8, 12, 8, 10); UIKit.Radius(s, 12);
                s.backgroundColor = dark ? new Color(1, 1, 1, 0.1f) : Color.white;
                if (me) UIKit.Border(s, 2f, Pal.Yellow);
            });
            row.Add(UIKit.Avatar(Initial(name), AvatarColor(p.Id), 36f));
            var col = UIKit.Col().St(s => { s.marginLeft = 10; s.flexGrow = 1; s.flexShrink = 1; });
            var nr = UIKit.Row();
            nr.Add(UIKit.Strong(name + (me ? " (you)" : ""), 15f, dark ? Color.white : Pal.Ink));
            if (p.Id == room.Owner && !room.IsQuick)
                nr.Add(UIKit.Strong("HOST", 10f, Pal.Ink, TextAnchor.MiddleCenter).St(s => { s.marginLeft = 6; s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 2, 6, 2, 6); UIKit.Radius(s, 6); }));
            col.Add(nr);
            col.Add(PenIcon.Build(Catalog.Pen(OnlineRoom.PenOf(p)), 110f).St(s => s.marginTop = 2));
            row.Add(col);
            return row;
        }

        static VisualElement EmptySeat(string text, bool dark)
        {
            var row = UIKit.Row().St(s =>
            {
                s.marginTop = 8; s.height = 56; UIKit.Pad(s, 0, 14, 0, 14); UIKit.Radius(s, 12);
                UIKit.Border(s, 2f, dark ? new Color(1, 1, 1, 0.3f) : Pal.Dash);
            });
            row.Add(UIKit.HandText(text, 16f, dark ? Pal.Mist : Pal.InkSoft));
            return row;
        }

        public static Color AvatarColor(string playerId)
        {
            var colors = new[] { Pal.Yellow, Pal.Pink, Pal.Sky, Pal.Teal, Pal.H("#C6E3B4"), Pal.H("#FFD6A5") };
            int h = 0;
            foreach (char ch in playerId ?? "") h = h * 31 + ch;
            return colors[Mathf.Abs(h) % colors.Length];
        }

        static async void DoStart(GameManager gm, OnlineRoom room)
        {
            try { await room.StartAsync(); }
            catch (Exception e) { gm.UI.ShowOverlay(Error(gm, "COULDN'T START", OnlineRoom.Explain(e))); }
        }

        public static async void Leave(GameManager gm, OnlineRoom room)
        {
            ShowPlay(gm);
            await room.LeaveAsync();
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // =====================================================================
        // O9 leaderboard
        // =====================================================================

        static bool weeklyTab = true;

        public static void ShowLeaderboard(GameManager gm)
        {
            var root = Screen();
            root.Add(Header("LEADERBOARD", "online match wins", () => ShowPlay(gm)));

            var tabs = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginLeft = 16; s.marginRight = 16; s.marginTop = 14; s.backgroundColor = Pal.Night; UIKit.Radius(s, 14); UIKit.Pad(s, 4); s.flexShrink = 0;
            });
            tabs.Add(BoardTab("This week", weeklyTab, () => { weeklyTab = true; ShowLeaderboard(gm); }));
            tabs.Add(BoardTab("All-time", !weeklyTab, () => { weeklyTab = false; ShowLeaderboard(gm); }));
            root.Add(tabs);
            if (weeklyTab)
            {
                var left = OnlineServices.UntilWeeklyReset();
                string when = left.Days > 0 ? left.Days + " days " + left.Hours + " h" : left.Hours + " h " + left.Minutes + " min";
                root.Add(UIKit.Plain("resets Monday 00:00 · " + when + " left", 12f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => { s.marginTop = 6; s.flexShrink = 0; }));
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 12, 16, 24, 16));
            body.Add(UIKit.HandText("loading the board…", 18f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => s.marginTop = 30));
            scroll.Add(body);
            root.Add(scroll);
            gm.UI.ShowScreen(root);
            LoadBoard(gm, body, weeklyTab ? OnlineServices.WeeklyId : OnlineServices.LeaderboardId);
        }

        static Button BoardTab(string text, bool on, Action go)
        {
            var b = UIKit.Btn(text, on ? null : go, on ? Pal.Paper : Color.clear, on ? Pal.Ink : Pal.MistLight, UIKit.BodyBold, 14f, 38f, 10f);
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            return b;
        }

        static async void LoadBoard(GameManager gm, VisualElement body, string board)
        {
            List<OnlineServices.Row> top;
            OnlineServices.Row? me;
            try
            {
                top = await OnlineServices.TopAsync(50, board);
                me = await OnlineServices.MeAsync(board);
            }
            catch (Exception e)
            {
                if (body.panel == null) return;
                body.Clear();
                string msg = board == OnlineServices.WeeklyId && NetPopups.IsOnline
                    ? "the weekly board isn't set up yet."
                    : OnlineServices.Explain(e);
                body.Add(UIKit.HandText(msg, 18f, Pal.Red, TextAnchor.MiddleCenter).St(s => s.marginTop = 30));
                return;
            }
            if (body.panel == null) return;
            body.Clear();

            var mine = UIKit.Row().St(s =>
            {
                s.backgroundColor = Pal.Yellow; UIKit.Radius(s, 14); UIKit.Pad(s, 12, 14, 12, 14); UIKit.Border(s, 2f, Pal.Ink);
                s.justifyContent = Justify.SpaceBetween; s.marginBottom = 14;
            });
            if (!OnlineServices.OnLeaderboard(gm.Save))
            {
                // Guests can look, but only Google players are on the board.
                mine.Add(UIKit.Strong("Playing as guest: sign in with Google to get on the board", 14f, Pal.Ink).St(s => { s.whiteSpace = WhiteSpace.Normal; s.flexShrink = 1; }));
                mine.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); DoGoogle(gm); });
                body.Add(mine);
            }
            else
            {
                mine.Add(UIKit.Strong(me.HasValue ? "You are #" + me.Value.rank : board == OnlineServices.WeeklyId ? "No wins this week yet" : "You're not on the board yet", 16f, Pal.Ink));
                mine.Add(UIKit.Disp((me.HasValue ? me.Value.wins : 0) + " WINS", 20f, Pal.Ink));
                body.Add(mine);
            }

            if (top.Count == 0)
            {
                body.Add(UIKit.HandText(board == OnlineServices.WeeklyId ? "nobody has won this week. be the first!" : "nobody has won online yet. be the first!", 18f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => s.marginTop = 20));
                return;
            }
            foreach (var r in top)
            {
                var row = UIKit.Row().St(s =>
                {
                    s.marginTop = 6; UIKit.Pad(s, 10, 14, 10, 12); UIKit.Radius(s, 12);
                    s.backgroundColor = r.me ? Pal.Yellow : Pal.Paper;
                    if (r.me) UIKit.Border(s, 2f, Pal.Ink);
                });
                var medal = r.rank == 1 ? Pal.H("#E6B422") : r.rank == 2 ? Pal.H("#B7C0C7") : r.rank == 3 ? Pal.H("#C8875A") : Pal.InkSoft;
                row.Add(UIKit.Disp("#" + r.rank, 20f, medal).St(s => s.width = 54));
                row.Add(UIKit.Avatar(Initial(r.name), r.me ? Color.white : Pal.Sky, 32f));
                row.Add(UIKit.Strong(r.name, 15f, Pal.Ink).St(s => { s.marginLeft = 10; s.flexGrow = 1; s.flexShrink = 1; }));
                row.Add(UIKit.Disp(r.wins.ToString(), 22f, Pal.Ink));
                row.Add(UIKit.Strong(" WINS", 10f, Pal.InkSoft));
                body.Add(row);
            }
        }

        // =====================================================================
        // shared pieces
        // =====================================================================

        static VisualElement Screen()
        {
            var root = UIKit.Col();
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;
            return root;
        }

        static VisualElement Header(string title, string sub, Action back)
        {
            var head = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Night; UIKit.Pad(s, 40, 16, 16, 16); s.flexShrink = 0;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 20;
            });
            var row = UIKit.Row();
            row.Add(UIKit.IconBtn(back, UIKit.BackIcon(), Pal.NightSoft, "Back"));
            var col = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
            col.Add(UIKit.Disp(title, 28f, Color.white));
            if (!string.IsNullOrEmpty(sub)) col.Add(UIKit.Plain(sub, 13f, Pal.Mist));
            row.Add(col);
            head.Add(row);
            return head;
        }

        static VisualElement Paper(float rot)
        {
            var c = UIKit.Col().St(s =>
            {
                UIKit.Pad(s, 22, 20, 20, 20);
                s.borderLeftWidth = 3; s.borderLeftColor = new Color(0.85f, 0.28f, 0.18f, 0.5f);
                s.borderBottomWidth = 5; s.borderBottomColor = new Color(0f, 0f, 0f, 0.15f);
            });
            UIKit.GraphPaper(c);
            UIKit.Rot(c, rot);
            return c;
        }

        static TextField Field(string value, int max)
        {
            var f = new TextField { maxLength = max, value = value ?? "" };
            f.style.marginLeft = f.style.marginRight = 0;
            f.style.unityFontDefinition = FontDefinition.FromFont(UIKit.BodyBold);
            f.style.fontSize = 22;
            f.style.color = Pal.Ink;
            var input = f.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Color.white;
                input.style.height = 56;
                UIKit.Pad(input.style, 0, 14, 0, 14);
                UIKit.Radius(input.style, 12);
                UIKit.Border(input.style, 2f, Pal.Ink);
                input.style.color = Pal.Ink;
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
            }
            return f;
        }

        /// <summary>Four-colour "G" drawn as four arcs.</summary>
        static VisualElement GoogleG(float size)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = size; e.style.height = size; e.style.flexShrink = 0;
            e.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                var c = new Vector2(size * 0.5f, size * 0.5f);
                float r = size * 0.36f;
                p.lineWidth = size * 0.18f;
                void Arc(Color col, float a0, float a1)
                {
                    p.strokeColor = col;
                    p.BeginPath();
                    p.Arc(c, r, new Angle(a0, AngleUnit.Degree), new Angle(a1, AngleUnit.Degree));
                    p.Stroke();
                }
                Arc(Pal.H("#EA4335"), -150f, -40f);
                Arc(Pal.H("#FBBC05"), 150f, 210f);
                Arc(Pal.H("#34A853"), 40f, 150f);
                Arc(Pal.H("#4285F4"), -10f, 40f);
                p.strokeColor = Pal.H("#4285F4");
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(new Vector2(c.x + r + p.lineWidth * 0.5f, c.y));
                p.Stroke();
            };
            return e;
        }

        public static VisualElement Busy(string text)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.55f); s.justifyContent = Justify.Center; s.alignItems = Align.Center; });
            o.pickingMode = PickingMode.Position;
            var card = UIKit.Col().St(s => { s.backgroundColor = Pal.Paper; UIKit.Pad(s, 18, 26, 18, 26); UIKit.Radius(s, 16); });
            card.Add(UIKit.HandText(text, 20f, Pal.Ink, TextAnchor.MiddleCenter));
            o.Add(card);
            return o;
        }

        public static VisualElement Error(GameManager gm, string title, string message)
        {
            var o = Sheet(out var c);
            c.Add(UIKit.Disp(title, 28f, Pal.Ink));
            c.Add(UIKit.HandText(message, 18f, Pal.Ink).St(s => s.marginTop = 6));
            var ok = UIKit.PrimaryBtn("OK", () => gm.UI.HideOverlay(), 54f);
            ok.style.marginTop = 16;
            c.Add(ok);
            return o;
        }

        static VisualElement Sheet(out VisualElement card)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f); s.justifyContent = Justify.FlexEnd; });
            o.pickingMode = PickingMode.Position;
            var c = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Pad(s, 22, 20, 30, 20);
                s.borderTopLeftRadius = s.borderTopRightRadius = 22;
            });
            o.Add(c);
            UIRoot.PopIn(c);
            card = c;
            return o;
        }
    }
}
