using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public class MatchOutcome
    {
        public MatchSetup setup;
        public int winner;
        public int[] score;
        public int marksBefore, marksAfter;
        public PenDef wonPen;        // ladder: the pen you took (null on rematch / loss)
        public bool becameChampion;
        public RivalDef nextRival;
    }

    public static class MatchEndScreen
    {
        public static VisualElement Build(GameManager gm, MatchOutcome m)
        {
            var s0 = m.setup;
            bool humanWon = s0.mode == MatchMode.PassAndPlay || !s0.isBot[m.winner];
            int a = m.score[m.winner], b = m.score[1 - m.winner];

            var root = UIKit.Col().St(s => { s.backgroundColor = Pal.Night; UIKit.Pad(s, 34, 20, 26, 20); });
            root.pickingMode = PickingMode.Position;

            string kicker, title, hand;
            if (s0.mode == MatchMode.PassAndPlay)
            {
                kicker = "MATCH OVER · " + a + "–" + b;
                title = HudView.DisplayName(s0, m.winner).ToUpperInvariant() + " WINS";
                hand = "loser picks up both pens.";
            }
            else if (s0.mode == MatchMode.Practice)
            {
                kicker = (humanWon ? "MATCH WON · " : "MATCH LOST · ") + a + "–" + b;
                title = humanWon ? "BOT BEATEN" : "BOT WINS";
                hand = humanWon ? "try a tougher bot next." : "practice makes perfect. apparently.";
            }
            else if (humanWon)
            {
                kicker = "MATCH WON · " + a + "–" + b;
                title = m.becameChampion ? "BATCH CHAMPION" : s0.rival.name.ToUpperInvariant() + " IS DONE";
                hand = m.wonPen != null ? "their pen goes in your pouch." : "rematch won. marks banked.";
            }
            else
            {
                kicker = "MATCH LOST · " + b + "–" + a;
                title = s0.rival.name.ToUpperInvariant() + " TOOK IT";
                hand = "\"" + s0.rival.quipWhenTheyWin + "\"";
            }

            var head = UIKit.Col().St(s => s.alignItems = Align.Center);
            head.Add(UIKit.Strong(kicker, 13f, Pal.Mist, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 0.8f));
            head.Add(UIKit.Disp(title, 44f, humanWon ? Pal.Yellow : Pal.H("#F5B7A1"), TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            head.Add(UIKit.HandText(hand, 19f, Pal.MistLight, TextAnchor.MiddleCenter).St(s => s.marginTop = 4));
            root.Add(head);

            // Pen card (won pen, or the pen you played with)
            PenDef show = m.wonPen ?? s0.pens[m.winner];
            var card = UIKit.Col().St(s => { s.marginTop = 24; UIKit.Pad(s, 22, 18, 22, 18); });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);
            var top = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            top.Add(UIKit.Disp(show.name.ToUpperInvariant(), 26f, Pal.Ink));
            if (m.wonPen != null)
                top.Add(UIKit.Strong("NEW", 12f, Pal.Ink, TextAnchor.MiddleCenter).St(s => { s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 5, 10, 5, 10); UIKit.Radius(s, 12); }));
            card.Add(top);
            var pi = PenIcon.Build(show, 290f);
            UIKit.Rot(pi, -5f);
            pi.style.alignSelf = Align.Center;
            pi.style.marginTop = 14;
            pi.style.marginBottom = 10;
            card.Add(pi);
            card.Add(UIKit.HandText(show.blurb, 17f, Pal.Ink));
            var stats = UIKit.Col().St(s => s.marginTop = 12);
            stats.Add(UIKit.StatBar("Weight", show.statWeight, Pal.Ink, Pal.GraphLine));
            stats.Add(UIKit.StatBar("Grip", show.statGrip, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 8));
            stats.Add(UIKit.StatBar("Spin", show.statSpin, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 8));
            card.Add(stats);
            root.Add(card);
            UIRoot.PopIn(card, 120);

            if (s0.mode == MatchMode.Ladder)
            {
                var strip = UIKit.Row().St(s =>
                {
                    s.marginTop = 22; s.backgroundColor = Pal.NightSoft; UIKit.Radius(s, 14); UIKit.Pad(s, 14, 16, 14, 16);
                    s.justifyContent = Justify.SpaceBetween;
                });
                var mk = UIKit.Col();
                mk.Add(UIKit.Strong("MARKS", 13f, Pal.Mist), UIKit.Disp(m.marksBefore + " → " + m.marksAfter, 26f, Color.white));
                var ld = UIKit.Col().St(s => s.alignItems = Align.FlexEnd);
                ld.Add(UIKit.Strong("LADDER", 13f, Pal.Mist, TextAnchor.UpperRight),
                    UIKit.Disp(Mathf.Min(gm.Save.ladderIndex + (gm.Save.LadderComplete ? 0 : 1), Catalog.Rivals.Count) + " / " + Catalog.Rivals.Count, 26f, Color.white, TextAnchor.UpperRight));
                strip.Add(mk, ld);
                root.Add(strip);

                if (humanWon && m.nextRival != null && !m.becameChampion)
                {
                    var next = UIKit.Row().St(s =>
                    {
                        s.marginTop = 14; UIKit.Pad(s, 12, 14, 12, 14); UIKit.Radius(s, 14); UIKit.Border(s, 2f, Pal.H("#4E6670"));
                    });
                    next.Add(UIKit.Avatar(m.nextRival.initial, m.nextRival.avatar, 48f));
                    var nc = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
                    nc.Add(UIKit.Strong("Next: " + m.nextRival.name, 15f, Color.white), UIKit.Plain(m.nextRival.blurb, 14f, Pal.MistLight));
                    next.Add(nc);
                    root.Add(next);
                }
            }

            root.Add(UIKit.Grow());

            var buttons = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            Button left, right;
            if (s0.mode == MatchMode.Ladder && humanWon)
            {
                left = UIKit.Btn("Open pouch", () => gm.ShowPouch(), Pal.Night, Color.white, UIKit.BodyBold, 16f, 56f, 14f, null, Color.white);
                right = UIKit.PrimaryBtn(m.becameChampion ? "HOME" : "NEXT RIVAL", () => { if (m.becameChampion) gm.ShowHome(); else gm.ShowRival(); }, 56f);
            }
            else if (s0.mode == MatchMode.Ladder)
            {
                left = UIKit.Btn("Home", () => gm.ShowHome(), Pal.Night, Color.white, UIKit.BodyBold, 16f, 56f, 14f, null, Color.white);
                right = UIKit.PrimaryBtn("TRY AGAIN", () => gm.ShowRival(), 56f);
            }
            else
            {
                left = UIKit.Btn("Home", () => gm.ShowHome(), Pal.Night, Color.white, UIKit.BodyBold, 16f, 56f, 14f, null, Color.white);
                right = UIKit.PrimaryBtn("REMATCH", () => gm.Rematch(s0), 56f);
            }
            left.style.width = Length.Percent(48);
            right.style.width = Length.Percent(48);
            buttons.Add(left, right);
            root.Add(buttons);
            return root;
        }
    }
}
