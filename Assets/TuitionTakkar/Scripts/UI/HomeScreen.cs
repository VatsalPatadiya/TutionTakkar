using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Landing page (mockup O1): profile + marks + settings, Play Online chalkboard,
    /// Batch Ladder, Pass &amp; Play / Practice, daily double strip, Pouch and Leaderboard tiles.
    /// </summary>
    public static class HomeScreen
    {
        static readonly Color Chalk = Pal.H("#2E4A3C");
        static readonly Color ChalkEdge = Pal.H("#6B4A2B");

        public static VisualElement Build(GameManager gm)
        {
            var save = gm.Save;
            var root = UIKit.Col();
            root.name = BackNav.HomeName;
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            // ---------- header ----------
            var top = UIKit.Row().St(s =>
            {
                s.height = 64; s.backgroundColor = Pal.Night; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 0, 14, 0, 14);
                s.flexShrink = 0;
            });
            var prof = UIKit.Row();
            bool account = OnlineServices.HasAccount(save);
            string who = account ? OnlineServices.DisplayName(save) : "Guest";
            prof.Add(UIKit.Avatar(who.Substring(0, 1).ToUpperInvariant(), Pal.Yellow, 38f));
            var pcol = UIKit.Col().St(s => s.marginLeft = 10);
            pcol.Add(UIKit.Strong(who, 15f, Color.white));
            var signIn = UIKit.Btn(account ? "Online" : "Sign in", () => gm.ShowOnline(), Pal.H("#3E7CB1"), Color.white, UIKit.BodyBold, 11f, 22f, 11f);
            UIKit.Pad(signIn.style, 0, 10, 0, 10);
            signIn.style.marginTop = 3;
            signIn.style.alignSelf = Align.FlexStart;
            pcol.Add(signIn);
            prof.Add(pcol);

            var rightTop = UIKit.Row();
            var chip = PouchScreen.MarksChip(save.marks, true);
            chip.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.UI.ShowOverlay(PouchScreen.EarnSheet(gm)); });
            rightTop.Add(chip);
            var gear = UIKit.IconBtn(() => gm.UI.ShowOverlay(Settings(gm)), new LineIcon(LineIcon.Kind.Gear, 24f, 24f, Color.white), Pal.NightSoft, "Settings");
            gear.style.marginLeft = 10;
            rightTop.Add(gear);
            top.Add(prof, rightTop);
            root.Add(top);

            // ---------- title (smaller: the game modes get the room) ----------
            var titleWrap = UIKit.Col().St(s => { s.alignItems = Align.Center; UIKit.Pad(s, 14, 20, 0, 20); s.flexShrink = 0; });
            var tape = UIKit.El().St(s => { s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.92f); UIKit.Pad(s, 5, 20, 3, 20); });
            UIKit.Rot(tape, -2f);
            tape.Add(UIKit.Disp("TUITION TAKKAR", 30f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 1f));
            titleWrap.Add(tape);
            root.Add(titleWrap);

            var body = UIKit.Col().St(s => { UIKit.Pad(s, 14, 18, 0, 18); s.flexShrink = 0; });

            // ---------- play online: the main action, with a real button ----------
            var online = UIKit.Col().St(s =>
            {
                s.backgroundColor = Chalk; UIKit.Pad(s, 14, 16, 16, 16); UIKit.Radius(s, 10);
                UIKit.Border(s, 6f, ChalkEdge);
            });
            var ot = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            var otl = UIKit.Col();
            otl.Add(UIKit.Disp("PLAY ONLINE", 30f, Color.white));
            otl.Add(UIKit.HandText("2, 3 or 4 classmates", 17f, Pal.H("#E6EFE9")));
            ot.Add(otl);
            ot.Add(new LineIcon(LineIcon.Kind.People, 52f, 46f, Color.white));
            online.Add(ot);
            int on = 0;
            if (Friends.Ready) foreach (var f in Friends.List) if (Friends.StateOf(f) != Friends.State.Offline) on++;
            if (on > 0)
                online.Add(UIKit.Strong(on + (on == 1 ? " friend online" : " friends online"), 13f, Pal.H("#7CE3A4")).St(s => s.marginTop = 4));
            var playNow = UIKit.Btn("PLAY NOW", () => gm.ShowOnline(), Pal.Yellow, Pal.Ink, UIKit.Display, 26f, 58f, 14f, Pal.H("#C7A928"));
            playNow.style.marginTop = 12;
            online.Add(playNow);
            online.RegisterCallback<ClickEvent>(e => { if (e.target is Button) return; gm.Audio.Ui(); gm.ShowOnline(); });
            UIKit.Rot(online, -0.6f);
            body.Add(online);

            // ---------- batch ladder: who's next and one big button ----------
            var ladder = StickyCard(Pal.Yellow, 0.8f);
            ladder.style.marginTop = 16;
            var lrow = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            var r = save.CurrentRival;
            int lp = Ladder.PosInBatch(save.ladderIndex);
            lrow.Add(UIKit.Avatar(r.initial, r.avatar, 50f));
            var lcol = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; s.marginLeft = 10; s.marginRight = 8; });
            lcol.Add(UIKit.Disp("BATCH LADDER", 24f, Pal.Ink));
            var nx = UIKit.Row();
            nx.Add(UIKit.Strong((Ladder.IsBoss(save.ladderIndex) ? "Boss " : "vs ") + r.name, 15f, Pal.Ink));
            nx.Add(new CoinIcon(16f).St(s => s.marginLeft = 8));
            nx.Add(UIKit.Strong("+" + r.reward, 15f, Pal.Ink).St(s => s.marginLeft = 3));
            lcol.Add(nx);
            lrow.Add(lcol);
            lrow.Add(UIKit.Btn("PLAY", () => gm.ShowRival(), Pal.Red, Color.white, UIKit.Display, 20f, 52f, 26f, Pal.RedDark).St(s => s.width = 86));
            ladder.Add(lrow);
            var dots = UIKit.Row().St(s => s.marginTop = 10);
            for (int i = 0; i < Ladder.PerBatch; i++)
            {
                var d = UIKit.Dot(12f, Pal.Ink, i < lp, Pal.Ink);
                if (i > 0) d.style.marginLeft = 5;
                dots.Add(d);
            }
            dots.Add(UIKit.Strong((lp + 1) + " / " + Ladder.PerBatch, 13f, Pal.Ink).St(s => s.marginLeft = 8));
            ladder.Add(dots);
            ladder.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return;
                gm.Audio.Ui();
                gm.ShowRival();
            });
            body.Add(ladder);

            // ---------- pass & play / practice: icon + two words ----------
            var grid = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 16; s.justifyContent = Justify.SpaceBetween; });
            var pp = SmallSticky(Pal.Pink, 1.5f, LineIcon.Kind.Phone, "PASS & PLAY", "one phone");
            pp.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.StartPassAndPlay(); });
            var pr = SmallSticky(Pal.Sky, -1f, LineIcon.Kind.Robot, "PRACTICE", "vs bot · " + Catalog.PracticeNames[Mathf.Clamp(save.practiceDifficulty, 0, 2)]);
            pr.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.UI.ShowOverlay(PracticePicker(gm)); });
            grid.Add(pp, pr);
            body.Add(grid);

            // ---------- daily double: bigger, darker ----------
            if (Economy.DoubleAvailable(save))
            {
                var dd = UIKit.Row().St(s =>
                {
                    s.marginTop = 16; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 12); UIKit.Pad(s, 10, 14, 10, 14);
                    UIKit.Border(s, 2f, Pal.Dash);
                });
                dd.Add(UIKit.Disp("×2", 28f, Pal.Orange));
                var ddc = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
                ddc.Add(UIKit.Strong("First win today pays double", 15f, Pal.Ink));
                ddc.Add(UIKit.Strong("ladder or practice", 13f, Pal.InkSoft));
                dd.Add(ddc);
                body.Add(dd);
            }
            root.Add(body);

            root.Add(UIKit.Grow());

            // ---------- bottom: one row of big icon buttons ----------
            var nav = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginLeft = 14; s.marginRight = 14; s.marginBottom = 20; s.marginTop = 12; s.flexShrink = 0;
                s.justifyContent = Justify.SpaceBetween;
            });
            bool signedIn = Friends.Allowed(save);
            nav.Add(NavTile(LineIcon.Kind.Pouch, "PENS", Pal.Pouch, Pal.PouchDark, 0, () => gm.ShowPouch(0)));
            if (signedIn)
            {
                int req = Friends.Ready ? Friends.Incoming.Count : 0;
                nav.Add(NavTile(LineIcon.Kind.People, "FRIENDS", Pal.Night, Pal.H("#0E1A1F"), req, () => FriendsUI.Show(gm, req > 0 ? 1 : 0)));
                nav.Add(NavTile(LineIcon.Kind.Trophy, "RANKS", Pal.H("#6B4A9A"), Pal.H("#4E3573"), 0, () => gm.ShowLeaderboard()));
                nav.Add(NavTile(LineIcon.Kind.Person, "ME", Pal.Green, Pal.H("#134A37"), 0, () => ProfileScreens.ShowProfile(gm, () => gm.ShowHome())));
            }
            else
            {
                nav.Add(NavTile(LineIcon.Kind.Trophy, "RANKS", Pal.H("#6B4A9A"), Pal.H("#4E3573"), 0, () => gm.ShowLeaderboard()));
                var sign = UIKit.Col().St(s =>
                {
                    s.width = Length.Percent(48); s.height = 80; s.backgroundColor = Color.white; UIKit.Radius(s, 16);
                    s.borderBottomWidth = 5; s.borderBottomColor = Pal.Dash; s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                });
                sign.Add(UIKit.Disp("SIGN IN", 18f, Pal.Ink, TextAnchor.MiddleCenter));
                sign.Add(UIKit.Strong("friends & profile", 12f, Pal.InkSoft, TextAnchor.MiddleCenter));
                sign.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.ShowOnline(); });
                nav.Add(sign);
            }
            root.Add(nav);
            return root;
        }

        // ---------- overlays ----------

        /// <summary>Pick Easy / Medium / Hard (with what each pays), then start.</summary>
        static VisualElement PracticePicker(GameManager gm)
        {
            var o = Sheet(out var c);
            c.Add(UIKit.Disp("PRACTICE", 30f, Pal.Ink));
            c.Add(UIKit.HandText("pick how tough the bot is.", 17f, Pal.InkSoft).St(s => s.marginTop = 2));
            for (int i = 0; i < 3; i++)
            {
                int level = i;
                bool on = gm.Save.practiceDifficulty == i;
                var row = UIKit.Row().St(s =>
                {
                    s.marginTop = 10; s.height = 58; UIKit.Pad(s, 0, 14, 0, 14); UIKit.Radius(s, 12);
                    s.justifyContent = Justify.SpaceBetween;
                    s.backgroundColor = on ? Pal.Ink : Color.white; UIKit.Border(s, 2f, Pal.Ink);
                });
                var col = on ? Color.white : Pal.Ink;
                row.Add(UIKit.Disp(Catalog.PracticeNames[i].ToUpperInvariant(), 22f, col));
                var pay = UIKit.Row();
                pay.Add(UIKit.Plain("win ", 12f, on ? Pal.Mist : Pal.InkSoft));
                pay.Add(new CoinIcon(16f));
                pay.Add(UIKit.Strong("+" + Economy.PracticeReward(i), 15f, col).St(s => s.marginLeft = 4));
                row.Add(pay);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    gm.Audio.Ui();
                    gm.Save.practiceDifficulty = level;
                    gm.Save.Save();
                    gm.UI.HideOverlay();
                    gm.StartPractice(level);
                });
                c.Add(row);
            }
            var cancel = UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 48f, 12f);
            cancel.style.marginTop = 10;
            c.Add(cancel);
            return o;
        }

        static VisualElement Settings(GameManager gm)
        {
            var save = gm.Save;
            var o = Sheet(out var c);
            c.Add(UIKit.Disp("SETTINGS", 30f, Pal.Ink));
            c.Add(Toggle("Sound", () => save.soundOn, v => save.soundOn = v, gm).St(s => s.marginTop = 12));
            c.Add(Toggle("Vibration", () => save.hapticsOn, v => save.hapticsOn = v, gm).St(s => s.marginTop = 10));
            c.Add(Toggle("Reminders (7 pm)", () => save.remindersOn, v => Reminders.SetOn(save, v), gm).St(s => s.marginTop = 10));
            var done = UIKit.PrimaryBtn("DONE", () => gm.UI.HideOverlay(), 52f);
            done.style.marginTop = 18;
            c.Add(done);
            return o;
        }

        static VisualElement Toggle(string label, System.Func<bool> get, System.Action<bool> set, GameManager gm)
        {
            var row = UIKit.Row().St(s =>
            {
                s.height = 54; UIKit.Pad(s, 0, 14, 0, 14); UIKit.Radius(s, 12); s.backgroundColor = Color.white;
                s.justifyContent = Justify.SpaceBetween; UIKit.Border(s, 2f, Pal.Dash);
            });
            row.Add(UIKit.Strong(label, 16f, Pal.Ink));
            var b = UIKit.Btn("", null, Pal.Green, Color.white, UIKit.BodyBold, 13f, 34f, 17f);
            b.style.width = 70;
            void Paint()
            {
                bool on = get();
                b.text = on ? "ON" : "OFF";
                b.style.backgroundColor = on ? Pal.Green : Pal.Locked;
            }
            Paint();
            b.clicked += () =>
            {
                set(!get());
                gm.ApplySettings();
                gm.Save.Save();
                Paint();
            };
            row.Add(b);
            return row;
        }

        /// <summary>Dimmed backdrop + bottom sheet on paper; tapping the backdrop closes it.</summary>
        static VisualElement Sheet(out VisualElement card)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f); s.justifyContent = Justify.FlexEnd; });
            o.pickingMode = PickingMode.Position;
            var c = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Pad(s, 22, 20, 28, 20);
                s.borderTopLeftRadius = s.borderTopRightRadius = 22;
            });
            c.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            o.RegisterCallback<ClickEvent>(_ => GameManager.I?.UI.HideOverlay());
            o.Add(c);
            UIRoot.PopIn(c);
            card = c;
            return o;
        }

        // ---------- pieces ----------

        static VisualElement Tile(Color bg, Color edge)
        {
            var e = UIKit.Col().St(s =>
            {
                s.width = Length.Percent(48); s.minHeight = 110; s.backgroundColor = bg; UIKit.Pad(s, 12, 14, 12, 14);
                UIKit.Radius(s, 16); s.borderBottomWidth = 6; s.borderBottomColor = edge;
            });
            return e;
        }

        static VisualElement StickyCard(Color c, float rot)
        {
            var e = UIKit.Col().St(s =>
            {
                s.backgroundColor = c; UIKit.Pad(s, 14, 16, 12, 16);
                s.borderBottomWidth = 5; s.borderBottomColor = new Color(0f, 0f, 0f, 0.12f);
            });
            UIKit.Rot(e, rot);
            return e;
        }

        static VisualElement SmallSticky(Color c, float rot, LineIcon.Kind icon, string title, string sub)
        {
            var e = StickyCard(c, rot);
            e.style.width = Length.Percent(47);
            e.style.minHeight = 104;
            UIKit.Pad(e.style, 12, 14, 12, 14);
            e.Add(new LineIcon(icon, 32f, 32f, Pal.Ink));
            e.Add(UIKit.Disp(title, 21f, Pal.Ink).St(s => s.marginTop = 4));
            e.Add(UIKit.Strong(sub, 14f, Pal.InkSoft));
            return e;
        }

        /// <summary>A big square icon button for the bottom row, with an optional red count.</summary>
        static VisualElement NavTile(LineIcon.Kind icon, string label, Color bg, Color edge, int badge, System.Action go)
        {
            var e = UIKit.Col().St(s =>
            {
                s.width = Length.Percent(23); s.height = 80; s.backgroundColor = bg; UIKit.Radius(s, 16);
                s.borderBottomWidth = 5; s.borderBottomColor = edge; s.alignItems = Align.Center; s.justifyContent = Justify.Center;
            });
            e.Add(new LineIcon(icon, 32f, 32f, Color.white));
            e.Add(UIKit.Disp(label, 14f, Color.white, TextAnchor.MiddleCenter).St(s => s.marginTop = 3));
            if (badge > 0)
                e.Add(UIKit.Strong(badge.ToString(), 12f, Color.white, TextAnchor.MiddleCenter).St(s =>
                {
                    s.position = Position.Absolute; s.top = -6; s.right = -4; s.minWidth = 22; s.height = 22;
                    s.backgroundColor = Pal.Red; UIKit.Radius(s, 11); UIKit.Border(s, 2f, Color.white);
                }));
            e.RegisterCallback<ClickEvent>(_ => { GameManager.I?.Audio.Ui(); go?.Invoke(); });
            return e;
        }
    }
}
