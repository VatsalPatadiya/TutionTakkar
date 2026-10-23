using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public static class RoundResultView
    {
        static string Possessive(MatchSetup s, int i)
        {
            if (s.mode != MatchMode.PassAndPlay && i == 0) return "YOUR";
            return HudView.DisplayName(s, i).ToUpperInvariant() + "'S";
        }

        public static VisualElement Build(GameManager gm, RoundSummary sum, MatchSetup setup)
        {
            int loser = 1 - sum.winner;
            int shooter = sum.shooterSelfOut ? loser : sum.winner;

            var o = UIKit.El().St(s => s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.62f));
            o.pickingMode = PickingMode.Position;

            var card = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.left = 24; s.right = 24; s.top = 150;
                UIKit.Pad(s, 28, 22, 24, 22); s.alignItems = Align.Center;
                s.borderBottomWidth = 6; s.borderBottomColor = new Color(0, 0, 0, 0.18f);
            });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);

            card.Add(UIKit.Strong("ROUND " + sum.round, 13f, Pal.InkSoft, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 0.8f));

            string headline;
            if (sum.bothOut) headline = "BOTH PENS\nWENT OVER";
            else if (sum.shooterSelfOut) headline = Possessive(setup, shooter) + " PEN\nFLEW OFF";
            else headline = Possessive(setup, loser) + " PEN\nHIT THE FLOOR";
            card.Add(UIKit.Disp(headline, 34f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));

            bool humanWon = setup.mode == MatchMode.PassAndPlay || !setup.isBot[sum.winner];
            string stampText = setup.mode == MatchMode.PassAndPlay
                ? (sum.winner == 0 ? "P1 TAKES IT" : "P2 TAKES IT")
                : humanWon ? "ROUND YOURS" : "ROUND LOST";
            Color stampColor = humanWon ? Pal.Green : Pal.Red;
            var stamp = UIKit.El().St(s =>
            {
                UIKit.Border(s, 5f, stampColor); UIKit.Radius(s, 8); UIKit.Pad(s, 6, 18, 6, 18);
                s.marginTop = 14; s.marginBottom = 6;
            });
            stamp.Add(UIKit.Disp(stampText, 30f, stampColor, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 2f));
            UIKit.Rot(stamp, -9f);
            card.Add(stamp);
            UIRoot.PopIn(stamp, 220);
            stamp.schedule.Execute(() => gm.Audio.Stamp()).StartingIn(240);

            if (sum.shooterSelfOut)
                card.Add(UIKit.HandText("your own pen falls = round lost", 16f, Pal.Red, TextAnchor.MiddleCenter).St(s => s.marginTop = 4));

            // Scores
            var scores = UIKit.Row().St(s => s.marginTop = 10);
            scores.Add(ScoreNote(HudView.DisplayName(setup, 0), sum.score[0], Pal.Yellow, -3f));
            scores.Add(UIKit.Text("vs", UIKit.HandBold, 22f, Pal.Ink, TextAnchor.MiddleCenter).St(s => { s.marginLeft = 18; s.marginRight = 18; }));
            Color rc = setup.rival != null ? setup.rival.avatar : setup.mode == MatchMode.PassAndPlay ? Pal.Pink : Pal.Teal;
            scores.Add(ScoreNote(HudView.DisplayName(setup, 1), sum.score[1], rc, 3f));
            card.Add(scores);

            var stats = UIKit.Col().St(s => { s.alignSelf = Align.Stretch; s.marginTop = 14; });
            stats.Add(UIKit.DashedLine(Pal.Dash).St(s => s.marginBottom = 10));
            stats.Add(UIKit.KeyValue("Shots this round", sum.shots.ToString(), Pal.Ink));
            if (!sum.shooterSelfOut)
                stats.Add(UIKit.KeyValue("Winning flick", Mathf.RoundToInt(sum.winningPower * 100f) + "% power", Pal.Ink));
            card.Add(stats);

            if (setup.mode == MatchMode.Ladder && setup.rival != null)
            {
                string quip = setup.isBot[sum.winner] ? setup.rival.quipWhenTheyWin : setup.rival.quipWhenTheyLose;
                card.Add(UIKit.HandText(setup.rival.name + ": \"" + quip + "\"", 18f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 4));
            }

            // Home (leave the match) + Next round / See result.
            var row = UIKit.Row(Align.Stretch).St(s => { s.alignSelf = Align.Stretch; s.marginTop = 16; });
            var home = UIKit.Btn("HOME", () =>
            {
                gm.UI.HideOverlay();
                gm.QuitToHome();
            }, Color.clear, Pal.Ink, UIKit.Display, 18f, 56f, 16f, null, Pal.Ink);
            home.style.width = 104;
            home.style.marginRight = 10;
            home.tooltip = "Go to home";
            var btn = UIKit.PrimaryBtn(sum.matchOver ? "SEE RESULT" : "NEXT ROUND", () =>
            {
                gm.UI.HideOverlay();
                gm.Match.Continue();
            }, 56f);
            btn.style.flexGrow = 1;
            row.Add(home, btn);
            card.Add(row);

            // Rule reminder inside the card, so it never sits on top of the power ruler.
            card.Add(UIKit.Plain("First to " + GameConfig.RoundsToWin + " rounds wins the match", 13f, Pal.InkSoft, TextAnchor.MiddleCenter)
                .St(s => s.marginTop = 10));

            o.Add(card);
            UIRoot.PopIn(card);
            return o;
        }

        static VisualElement ScoreNote(string name, int score, Color bg, float rot)
        {
            var n = UIKit.Col().St(s => { s.backgroundColor = bg; UIKit.Pad(s, 8, 14, 8, 14); s.alignItems = Align.Center; s.minWidth = 76; });
            UIKit.Rot(n, rot);
            n.Add(UIKit.Strong(name.ToUpperInvariant(), 12f, Pal.Ink, TextAnchor.MiddleCenter));
            n.Add(UIKit.Disp(score.ToString(), 36f, Pal.Ink, TextAnchor.MiddleCenter));
            return n;
        }
    }
}
