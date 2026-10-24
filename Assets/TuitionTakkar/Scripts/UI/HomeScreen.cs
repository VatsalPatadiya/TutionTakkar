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

            // ---------- title ----------
            var titleWrap = UIKit.Col().St(s => { s.alignItems = Align.Center; UIKit.Pad(s, 16, 20, 0, 20); s.flexShrink = 0; });
            var tape = UIKit.El().St(s => { s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.92f); UIKit.Pad(s, 6, 22, 4, 22); });
            UIKit.Rot(tape, -2f);
            tape.Add(UIKit.Disp("TUITION TAKKAR", 34f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 1f));
            titleWrap.Add(tape);
            titleWrap.Add(UIKit.HandText("flick their pen off the bench. keep it.", 17f, Pal.BenchText, TextAnchor.MiddleCenter)
                .St(s => s.marginTop = 4));
            root.Add(titleWrap);

            var body = UIKit.Col().St(s => { UIKit.Pad(s, 14, 20, 0, 20); s.flexShrink = 0; });

            // ---------- play online (chalkboard) ----------
            var online = UIKit.Row().St(s =>
            {
                s.backgroundColor = Chalk; UIKit.Pad(s, 14, 16, 14, 16); UIKit.Radius(s, 10);
                UIKit.Border(s, 5f, ChalkEdge); s.justifyContent = Justify.SpaceBetween;
            });
            var oc = UIKit.Col().St(s => s.flexShrink = 1);
            var ot = UIKit.Row();
            ot.Add(UIKit.Disp("PLAY ONLINE", 28f, Color.white));
            ot.Add(UIKit.Strong("NEW", 11f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
            {
                s.marginLeft = 8; s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 3, 8, 3, 8); UIKit.Radius(s, 10);
            }));
            oc.Add(ot);
            oc.Add(UIKit.HandText("2, 3 or 4 classmates · room code or quick match", 16f, Pal.H("#E6EFE9")).St(s => s.marginTop = 2));
            online.Add(oc);
            online.Add(new LineIcon(LineIcon.Kind.People, 48f, 44f, Color.white));
            online.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.ShowOnline(); });
            UIKit.Rot(online, -0.6f);
            body.Add(online);

            // ---------- batch ladder ----------
            var ladder = StickyCard(Pal.Yellow, 1f);
            ladder.style.marginTop = 14;
            var lrow = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            var lcol = UIKit.Col().St(s => { s.flexShrink = 1; s.marginRight = 8; });
            lcol.Add(UIKit.Disp("BATCH LADDER", 26f, Pal.Ink));
            if (save.LadderComplete)
                lcol.Add(UIKit.HandText("batch champion! replay the final anytime.", 16f, Pal.Ink).St(s => s.marginTop = 2));
            else
            {
                var r = save.CurrentRival;
                var nx = UIKit.Row().St(s => s.marginTop = 4);
                nx.Add(UIKit.Avatar(r.initial, r.avatar, 26f));
                nx.Add(UIKit.Strong("Next: " + r.name, 14f, Pal.Ink).St(s => s.marginLeft = 6));
                var rw = UIKit.Row().St(s => { s.marginLeft = 8; s.backgroundColor = new Color(1f, 1f, 1f, 0.6f); UIKit.Pad(s, 2, 8, 2, 6); UIKit.Radius(s, 10); });
                rw.Add(new CoinIcon(14f));
                rw.Add(UIKit.Strong("+" + r.reward, 12f, Pal.Ink).St(s => s.marginLeft = 4));
                nx.Add(rw);
                lcol.Add(nx);
            }
            lrow.Add(lcol);
            lrow.Add(UIKit.Btn("Play", () => gm.ShowRival(), Pal.Red, Color.white, UIKit.BodyBold, 15f, 44f, 22f, Pal.RedDark));
            ladder.Add(lrow);
            var dots = UIKit.Row().St(s => s.marginTop = 10);
            for (int i = 0; i < Catalog.Rivals.Count; i++)
            {
                var d = UIKit.Dot(12f, Pal.Ink, i < save.ladderIndex, Pal.Ink);
                if (i > 0) d.style.marginLeft = 5;
                dots.Add(d);
            }
            dots.Add(UIKit.Strong(Mathf.Min(save.ladderIndex + 1, Catalog.Rivals.Count) + " / " + Catalog.Rivals.Count, 12f, Pal.Ink)
                .St(s => s.marginLeft = 8));
            ladder.Add(dots);
            ladder.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return; // Play already handled it
                gm.Audio.Ui();
                gm.ShowRival();
            });
            body.Add(ladder);

            // ---------- pass & play / practice ----------
            var grid = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 14; s.justifyContent = Justify.SpaceBetween; });
            var pp = SmallSticky(Pal.Pink, 1.5f, "PASS & PLAY", "2 players, one phone");
            pp.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.StartPassAndPlay(); });
            var pr = SmallSticky(Pal.Sky, -1f, "PRACTICE", "vs bot · " + Catalog.PracticeNames[Mathf.Clamp(save.practiceDifficulty, 0, 2)]);
            pr.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.UI.ShowOverlay(PracticePicker(gm)); });
            grid.Add(pp, pr);
            body.Add(grid);

            // ---------- daily double ----------
            if (Economy.DoubleAvailable(save))
            {
                var dd = UIKit.Row().St(s =>
                {
                    s.marginTop = 14; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 12); UIKit.Pad(s, 8, 12, 8, 12);
                    UIKit.Border(s, 2f, Pal.Dash);
                });
                dd.Add(UIKit.Disp("×2", 22f, Pal.Orange));
                var ddc = UIKit.Col().St(s => { s.marginLeft = 10; s.flexShrink = 1; });
                ddc.Add(UIKit.Strong("First win today pays double", 13f, Pal.Ink));
                ddc.Add(UIKit.Plain("ladder or practice · resets at midnight", 11f, Pal.InkSoft));
                dd.Add(ddc);
                body.Add(dd);
            }
            root.Add(body);

            root.Add(UIKit.Grow());

            // ---------- bottom tiles ----------
            var tiles = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginLeft = 20; s.marginRight = 20; s.marginBottom = 22; s.marginTop = 12; s.flexShrink = 0;
                s.justifyContent = Justify.SpaceBetween;
            });

            var pouch = Tile(Pal.Pouch, Pal.PouchDark);
            pouch.Add(UIKit.Disp("PENCIL POUCH", 20f, Color.white));
            pouch.Add(UIKit.Plain(save.owned.Count + " pens · " + Catalog.Pen(save.equipped).name, 12f, Pal.PouchText).St(s => s.marginTop = 2));
            var shop = UIKit.Btn("Tuck shop ›", () => gm.ShowPouch(1), Pal.Yellow, Pal.Ink, UIKit.BodyBold, 12f, 28f, 14f);
            shop.style.marginTop = 8;
            shop.style.alignSelf = Align.FlexStart;
            pouch.Add(shop);
            pouch.RegisterCallback<ClickEvent>(e => { if (e.target is Button) return; gm.Audio.Ui(); gm.ShowPouch(0); });

            var board = Tile(Pal.H("#6B4A9A"), Pal.H("#4E3573"));
            var bt = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            bt.Add(UIKit.Disp("LEADERBOARD", 20f, Color.white));
            board.Add(bt);
            board.Add(UIKit.Plain("top classmates by wins", 12f, Pal.H("#E5DCF3")).St(s => s.marginTop = 2));
            var tr = new LineIcon(LineIcon.Kind.Trophy, 30f, 30f, Pal.Yellow);
            tr.style.marginTop = 6;
            board.Add(tr);
            board.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.ShowLeaderboard(); });

            tiles.Add(pouch, board);
            root.Add(tiles);
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

        static VisualElement SmallSticky(Color c, float rot, string title, string sub)
        {
            var e = StickyCard(c, rot);
            e.style.width = Length.Percent(47);
            e.style.minHeight = 96;
            UIKit.Pad(e.style, 14, 14, 14, 14);
            e.Add(UIKit.Disp(title, 21f, Pal.Ink));
            e.Add(UIKit.HandText(sub, 15f, Pal.Ink).St(s => s.marginTop = 4));
            return e;
        }
    }
}
