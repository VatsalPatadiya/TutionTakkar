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
        /// <summary>Beat the boss: the whole batch is done (see batchDone).</summary>
        public bool becameChampion;
        public int batchDone;
        public RivalDef nextRival;
        public RewardResult rewards;  // marks earned and how (empty for Pass & Play)
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
                title = m.becameChampion ? Ladder.BatchName(m.batchDone).ToUpperInvariant() + " DONE!" : s0.rival.name.ToUpperInvariant() + " IS DONE";
                hand = m.becameChampion ? Ladder.BatchName(m.batchDone + 1) + " · " + Ladder.Subtitle(m.batchDone + 1).ToLowerInvariant() + " is open."
                    : m.wonPen != null ? "their pen goes in your pouch." : "you already had their pen: bonus marks instead.";
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
            bool compact = s0.mode != MatchMode.PassAndPlay;
            var card = UIKit.Col().St(s => { s.marginTop = compact ? 16 : 24; UIKit.Pad(s, compact ? 14 : 22, 18, compact ? 14 : 22, 18); });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);
            var top = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            top.Add(UIKit.Disp(show.name.ToUpperInvariant(), 26f, Pal.Ink));
            if (m.wonPen != null)
                top.Add(UIKit.Strong("NEW", 12f, Pal.Ink, TextAnchor.MiddleCenter).St(s => { s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 5, 10, 5, 10); UIKit.Radius(s, 12); }));
            card.Add(top);
            var pi = PenIcon.Build(show, compact ? 230f : 290f);
            UIKit.Rot(pi, -5f);
            pi.style.alignSelf = Align.Center;
            pi.style.marginTop = 14;
            pi.style.marginBottom = 10;
            card.Add(pi);
            card.Add(UIKit.HandText(show.blurb, 17f, Pal.Ink));
            if (!compact)
            {
                var stats = UIKit.Col().St(s => s.marginTop = 12);
                stats.Add(UIKit.StatBar("Weight", show.statWeight, Pal.Ink, Pal.GraphLine));
                stats.Add(UIKit.StatBar("Grip", show.statGrip, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 8));
                stats.Add(UIKit.StatBar("Spin", show.statSpin, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 8));
                card.Add(stats);
            }
            root.Add(card);
            UIRoot.PopIn(card, 120);

            // Marks earned, line by line (ladder and practice; Pass & Play earns none).
            if (s0.mode != MatchMode.PassAndPlay && m.rewards != null)
                root.Add(RewardsPanel(gm, m));

            if (s0.mode == MatchMode.Ladder && humanWon && m.nextRival != null)
            {
                var next = UIKit.Row().St(s =>
                {
                    s.marginTop = 12; UIKit.Pad(s, 10, 14, 10, 14); UIKit.Radius(s, 14); UIKit.Border(s, 2f, Pal.H("#4E6670"));
                });
                next.Add(UIKit.Avatar(m.nextRival.initial, m.nextRival.avatar, 40f));
                var nc = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
                nc.Add(UIKit.Strong("Next: " + m.nextRival.name, 15f, Color.white),
                    UIKit.Plain(Ladder.BatchName(Ladder.BatchOf(gm.Save.ladderIndex)) + " · rival " + (Ladder.PosInBatch(gm.Save.ladderIndex) + 1) + " of " + Ladder.PerBatch
                        + (Ladder.IsBoss(gm.Save.ladderIndex) ? " · BOSS" : ""), 13f, Pal.MistLight));
                next.Add(nc);
                root.Add(next);
            }

            root.Add(UIKit.Grow());

            Button left, right;
            if (s0.mode == MatchMode.Ladder && humanWon)
            {
                left = UIKit.Btn("Open pouch", () => gm.ShowPouch(0), Pal.Night, Color.white, UIKit.BodyBold, 16f, 56f, 14f, null, Color.white);
                right = UIKit.PrimaryBtn(m.becameChampion ? "NEXT BATCH" : "NEXT RIVAL", () => gm.ShowRival(), 56f);
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
            // One big obvious next step; the other choice is a quieter button under it.
            right.style.height = 66; right.style.fontSize = 28;
            root.Add(right);
            left.style.marginTop = 10; left.style.height = 50;
            root.Add(left);
            return root;
        }
        /// <summary>"Match win +20 / Knock-offs +15 / First win today ×2 / TOTAL +70" and the next pen to save for.</summary>
        static VisualElement RewardsPanel(GameManager gm, MatchOutcome m)
        {
            var r = m.rewards;
            var p = UIKit.Col().St(s => { s.marginTop = 16; s.backgroundColor = Pal.NightSoft; UIKit.Radius(s, 14); UIKit.Pad(s, 12, 16, 12, 16); });
            p.Add(UIKit.Strong("MARKS EARNED", 12f, Pal.Mist).St(s => s.letterSpacing = 0.8f));

            foreach (var line in r.lines)
            {
                var row = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.marginTop = 6; });
                var left = UIKit.Col().St(s => s.flexShrink = 1);
                left.Add(UIKit.Strong(line.label, 15f, Color.white));
                if (!string.IsNullOrEmpty(line.note)) left.Add(UIKit.Plain(line.note, 12f, Pal.MistLight));
                row.Add(left);
                row.Add(UIKit.Disp(line.isMultiplier ? "×2" : "+" + line.amount, 20f, Pal.H("#8FE0B5"), TextAnchor.MiddleRight));
                p.Add(row);
            }

            var total = UIKit.Row().St(s =>
            {
                s.justifyContent = Justify.SpaceBetween; s.marginTop = 10; s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 6, 12, 6, 12);
            });
            total.Add(UIKit.Disp("TOTAL", 20f, Pal.Ink));
            var tr = UIKit.Row();
            tr.Add(new CoinIcon(22f));
            tr.Add(UIKit.Disp("+" + r.total, 24f, Pal.Ink).St(s => s.marginLeft = 6));
            total.Add(tr);
            UIKit.Rot(total, -1f);
            p.Add(total);
            p.Add(UIKit.Plain("marks " + m.marksBefore + " → " + m.marksAfter, 13f, Pal.MistLight, TextAnchor.MiddleRight).St(s => s.marginTop = 6));

            // Progress to the next pen worth saving for.
            var goal = Economy.NextPenGoal(gm.Save);
            if (goal != null)
            {
                int price = Economy.Price(goal.id);
                int have = gm.Save.marks;
                var g = UIKit.Row().St(s => s.marginTop = 8);
                g.Add(PenIcon.Build(goal, 70f));
                var gc = UIKit.Col().St(s => { s.flexGrow = 1; s.marginLeft = 10; });
                gc.Add(UIKit.Strong(have >= price ? goal.name + " is yours to buy!" : "Next pen: " + goal.name, 13f, Color.white));
                var bar = UIKit.El().St(s => { s.height = 8; s.marginTop = 4; UIKit.Radius(s, 4); s.backgroundColor = Pal.Night; s.overflow = Overflow.Hidden; });
                bar.Add(UIKit.El().St(s => { s.height = 8; s.width = Length.Percent(Mathf.Clamp01(have / (float)price) * 100f); s.backgroundColor = Pal.H("#8FE0B5"); }));
                gc.Add(bar);
                gc.Add(UIKit.Plain(have >= price ? "open the tuck shop" : Mathf.Min(have, price) + " / " + price + " marks · " + (price - have) + " to go", 11f, Pal.MistLight).St(s => s.marginTop = 2));
                g.Add(gc);
                p.Add(g);
            }
            return p;
        }
    }
}
