using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public static class HomeScreen
    {
        public static VisualElement Build(GameManager gm)
        {
            var save = gm.Save;
            var root = UIKit.Col();
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            // Top bar: profile + tube light + settings
            var top = UIKit.Row().St(s =>
            {
                s.height = 64; s.backgroundColor = Pal.Night; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 0, 16, 0, 16);
                s.flexShrink = 0;
            });
            var prof = UIKit.Row();
            var badge = UIKit.El().St(s =>
            {
                s.width = 36; s.height = 36; UIKit.Radius(s, 18); s.backgroundColor = Pal.Yellow;
                s.alignItems = Align.Center; s.justifyContent = Justify.Center;
            });
            badge.Add(UIKit.Strong("G", 15f, Pal.Ink, TextAnchor.MiddleCenter));
            prof.Add(badge);
            var pcol = UIKit.Col().St(s => s.marginLeft = 10);
            pcol.Add(UIKit.Strong("Guest", 15f, Color.white), UIKit.Plain(save.marks + " marks", 13f, Pal.Mist));
            prof.Add(pcol);

            var rightTop = UIKit.Row();
            rightTop.Add(UIKit.El().St(s => { s.width = 64; s.height = 6; UIKit.Radius(s, 3); s.backgroundColor = Pal.H("#F4FBF7"); s.marginRight = 10; }));
            var sound = UIKit.Btn(save.soundOn ? "Sound on" : "Sound off", null, Pal.NightSoft, Color.white, UIKit.BodyBold, 12f, 44f, 12f);
            sound.clicked += () =>
            {
                save.soundOn = !save.soundOn;
                gm.ApplySettings();
                save.Save();
                sound.text = save.soundOn ? "Sound on" : "Sound off";
            };
            rightTop.Add(sound);
            top.Add(prof, rightTop);
            root.Add(top);

            // Title on masking tape
            var titleWrap = UIKit.Col().St(s => { s.alignItems = Align.Center; UIKit.Pad(s, 26, 20, 0, 20); s.flexShrink = 0; });
            var tape = UIKit.El().St(s => { s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.92f); UIKit.Pad(s, 10, 26, 8, 26); });
            UIKit.Rot(tape, -2f);
            tape.Add(UIKit.Disp("TUITION\nTAKKAR", 46f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 1f));
            titleWrap.Add(tape);
            titleWrap.Add(UIKit.HandText("flick their pen off the bench. keep it.", 19f, Pal.BenchText, TextAnchor.MiddleCenter)
                .St(s => s.marginTop = 8));
            root.Add(titleWrap);

            var body = UIKit.Col().St(s => { UIKit.Pad(s, 24, 22, 0, 22); s.flexShrink = 0; });

            // Batch ladder sticky (yellow)
            var ladder = StickyCard(Pal.Yellow, -1.2f);
            var lrow = UIKit.Row(Align.FlexStart).St(s => s.justifyContent = Justify.SpaceBetween);
            var lcol = UIKit.Col().St(s => { s.flexShrink = 1; s.marginRight = 8; });
            lcol.Add(UIKit.Disp("BATCH LADDER", 28f, Pal.Ink));
            string sub = save.LadderComplete ? "batch champion! replay the final anytime." : "beat 10 batchmates. win the Golden Nib.";
            lcol.Add(UIKit.HandText(sub, 17f, Pal.Ink).St(s => s.marginTop = 4));
            lrow.Add(lcol);
            lrow.Add(UIKit.Btn("Play", () => gm.ShowRival(), Pal.Red, Color.white, UIKit.BodyBold, 15f, 44f, 22f));
            ladder.Add(lrow);
            var dots = UIKit.Row().St(s => s.marginTop = 14);
            for (int i = 0; i < Catalog.Rivals.Count; i++)
            {
                var d = UIKit.Dot(14f, Pal.Ink, i < save.ladderIndex, Pal.Ink);
                if (i > 0) d.style.marginLeft = 6;
                dots.Add(d);
            }
            dots.Add(UIKit.Strong(Mathf.Min(save.ladderIndex + 1, Catalog.Rivals.Count) + " / " + Catalog.Rivals.Count, 13f, Pal.Ink)
                .St(s => s.marginLeft = 8));
            ladder.Add(dots);
            ladder.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return; // the Play button already handled it
                gm.Audio.Ui();
                gm.ShowRival();
            });
            body.Add(ladder);

            // Two smaller stickies
            var grid = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 18; s.justifyContent = Justify.SpaceBetween; });
            var pp = SmallSticky(Pal.Pink, 1.5f, "PASS & PLAY", "2 players, one phone");
            pp.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.StartPassAndPlay(); });
            var pr = SmallSticky(Pal.Sky, -1f, "PRACTICE", "vs bot, pick how tough");
            pr.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.StartPractice(save.practiceDifficulty); });
            grid.Add(pp, pr);
            body.Add(grid);

            // Difficulty chips
            var diff = UIKit.Col().St(s => { s.marginTop = 18; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 12); UIKit.Pad(s, 12, 14, 12, 14); });
            diff.Add(UIKit.Strong("PRACTICE BOT", 13f, Pal.InkSoft).St(s => s.letterSpacing = 0.6f));
            var chips = UIKit.Row().St(s => { s.marginTop = 8; s.justifyContent = Justify.SpaceBetween; });
            var chipButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int level = i;
                var b = UIKit.Btn(Catalog.PracticeNames[i], null, Pal.Paper, Pal.Ink, UIKit.BodyBold, 15f, 44f, 10f, null, Pal.Ink);
                b.style.width = Length.Percent(31.5f);
                b.clicked += () =>
                {
                    save.practiceDifficulty = level;
                    save.Save();
                    for (int k = 0; k < 3; k++) StyleChip(chipButtons[k], k == level);
                };
                chipButtons[i] = b;
                StyleChip(b, i == save.practiceDifficulty);
                chips.Add(b);
            }
            diff.Add(chips);
            body.Add(diff);
            root.Add(body);

            root.Add(UIKit.Grow());

            // Pencil pouch bar
            var pouch = UIKit.Row().St(s =>
            {
                s.marginLeft = 22; s.marginRight = 22; s.marginBottom = 26; s.height = 72; s.flexShrink = 0;
                s.backgroundColor = Pal.Pouch; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 0, 18, 0, 18);
                s.borderTopLeftRadius = s.borderTopRightRadius = 22; s.borderBottomLeftRadius = s.borderBottomRightRadius = 16;
                s.borderBottomWidth = 6; s.borderBottomColor = Pal.PouchDark;
            });
            var pc = UIKit.Col();
            pc.Add(UIKit.Disp("MY PENCIL POUCH", 22f, Color.white));
            pc.Add(UIKit.Plain(save.owned.Count + " pens · " + Catalog.Pen(save.equipped).name + " equipped", 14f, Pal.PouchText));
            pouch.Add(pc, UIKit.Chevron(Color.white));
            pouch.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.ShowPouch(); });
            root.Add(pouch);

            return root;
        }

        static void StyleChip(Button b, bool on)
        {
            b.style.backgroundColor = on ? Pal.Ink : Pal.Paper;
            b.style.color = on ? Color.white : Pal.Ink;
        }

        static VisualElement StickyCard(Color c, float rot)
        {
            var e = UIKit.Col().St(s =>
            {
                s.backgroundColor = c; UIKit.Pad(s, 18, 18, 16, 18);
                s.borderBottomWidth = 5; s.borderBottomColor = new Color(0f, 0f, 0f, 0.12f);
            });
            UIKit.Rot(e, rot);
            return e;
        }

        static VisualElement SmallSticky(Color c, float rot, string title, string sub)
        {
            var e = StickyCard(c, rot);
            e.style.width = Length.Percent(47);
            e.style.minHeight = 140;
            UIKit.Pad(e.style, 16, 14, 16, 14);
            e.Add(UIKit.Disp(title, 22f, Pal.Ink));
            e.Add(UIKit.HandText(sub, 16f, Pal.Ink).St(s => s.marginTop = 6));
            return e;
        }
    }
}
