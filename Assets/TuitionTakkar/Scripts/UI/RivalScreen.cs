using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public static class RivalScreen
    {
        public static VisualElement Build(GameManager gm, RivalDef rival)
        {
            var save = gm.Save;
            int index = Catalog.Rivals.IndexOf(rival);
            bool replay = save.Owns(rival.penId);
            var myPen = Catalog.Pen(save.equipped);
            var theirPen = Catalog.Pen(rival.penId);

            var root = UIKit.Col().St(s => UIKit.Pad(s, 16, 18, 26, 18));
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            var top = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            top.Add(UIKit.IconBtn(() => gm.ShowHome(), UIKit.BackIcon(), Pal.Night, "Back"));
            top.Add(UIKit.Strong("BATCH LADDER · RIVAL " + (index + 1) + " OF " + Catalog.Rivals.Count, 14f, Pal.BenchText, TextAnchor.MiddleCenter)
                .St(s => s.letterSpacing = 0.6f));
            top.Add(UIKit.El().St(s => s.width = 44));
            root.Add(top);

            // Graph-paper card
            var card = UIKit.Col().St(s => { s.marginTop = 22; UIKit.Pad(s, 22, 20, 20, 20); s.borderBottomWidth = 6; s.borderBottomColor = new Color(0, 0, 0, 0.12f); });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -0.6f);
            var tape = UIKit.Tape(null, 100f, 3f);
            tape.style.position = Position.Absolute; tape.style.top = -14; tape.style.left = Length.Percent(50); tape.style.marginLeft = -50; tape.style.height = 24;
            card.Add(tape);

            var vs = UIKit.Row().St(s => s.justifyContent = Justify.SpaceAround);
            vs.Add(Person("Guest", myPen.name, "G", Pal.Yellow));
            var vsLabel = UIKit.Disp("VS", 40f, Pal.Red, TextAnchor.MiddleCenter);
            UIKit.Rot(vsLabel, -6f);
            vs.Add(vsLabel);
            vs.Add(Person(rival.name, theirPen.name, rival.initial, rival.avatar));
            card.Add(vs);

            var pens = UIKit.Col().St(s => { s.alignItems = Align.Center; s.marginTop = 18; });
            var p1 = PenIcon.Build(myPen, 230f); UIKit.Rot(p1, -4f);
            var p2 = PenIcon.Build(theirPen, 240f); UIKit.Rot(p2, 3f);
            p2.style.marginTop = 12;
            pens.Add(p1, p2);
            card.Add(pens);

            card.Add(UIKit.HandText("\"" + rival.blurb + "\"", 19f, Pal.Ink).St(s => s.marginTop = 16));

            var info = UIKit.Col().St(s => s.marginTop = 16);
            info.Add(UIKit.KeyValue("Format", "Best of " + (GameConfig.RoundsToWin * 2 - 1), Pal.Ink));
            info.Add(UIKit.DashedLine(Pal.Dash).St(s => s.marginBottom = 8));
            info.Add(UIKit.KeyValue("Win", replay ? "+" + rival.reward / 2 + " marks (rematch)" : "Keep " + theirPen.name + " · +" + rival.reward + " marks", Pal.Green));
            info.Add(UIKit.DashedLine(Pal.Dash).St(s => s.marginBottom = 8));
            info.Add(UIKit.KeyValue("Lose", "+" + Economy.LossConsolation + " marks for trying", Pal.InkSoft));
            card.Add(info);
            root.Add(card);
            UIRoot.PopIn(card);

            // How to play strip
            var how = UIKit.Row().St(s => { s.marginTop = 22; s.backgroundColor = Pal.Night; UIKit.Radius(s, 16); UIKit.Pad(s, 16); });
            how.Add(HowIcon());
            var hc = UIKit.Col().St(s => { s.marginLeft = 14; s.flexShrink = 1; });
            hc.Add(UIKit.Disp("PRESS · PULL · LET GO", 20f, Color.white));
            hc.Add(UIKit.Plain("Longer pull hits harder. Grab near an end to spin. Too hard, and your pen follows theirs off.", 14f, Pal.MistLight)
                .St(s => s.marginTop = 4));
            how.Add(hc);
            root.Add(how);

            root.Add(UIKit.Grow());
            root.Add(UIKit.PrimaryBtn("START MATCH", () => gm.StartLadder(rival), 60f));
            return root;
        }

        static VisualElement Person(string name, string pen, string initial, Color c)
        {
            var col = UIKit.Col().St(s => { s.alignItems = Align.Center; s.width = 110; });
            col.Add(UIKit.Avatar(initial, c));
            col.Add(UIKit.Strong(name, 15f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            col.Add(UIKit.Plain(pen, 13f, Pal.InkSoft, TextAnchor.MiddleCenter));
            return col;
        }

        static VisualElement HowIcon()
        {
            var box = UIKit.El().St(s => { s.width = 70; s.height = 70; s.flexShrink = 0; });
            box.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 8; s.top = 20; s.width = 54; s.height = 8; s.backgroundColor = Pal.Orange; UIKit.Radius(s, 4); }));
            box.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 26; s.top = 15; s.width = 18; s.height = 18; UIKit.Radius(s, 9); UIKit.Border(s, 2.5f, Pal.Yellow); }));
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                box.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 33.5f; s.top = 38 + k * 7; s.width = 3; s.height = 4; s.backgroundColor = Pal.Yellow; }));
            }
            box.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 34; s.top = 0; s.width = 2.5f; s.height = 12; s.backgroundColor = Color.white; }));
            return box;
        }
    }
}
