using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public static class RivalScreen
    {
        public static VisualElement Build(GameManager gm, RivalDef rival)
        {
            var save = gm.Save;
            int index = Ladder.IndexOf(rival);
            int batch = Ladder.BatchOf(index), pos = Ladder.PosInBatch(index);
            bool boss = Ladder.IsBoss(index);
            bool ownsPen = save.Owns(rival.penId);
            var myPen = Catalog.Pen(save.equipped);
            var theirPen = Catalog.Pen(rival.penId);

            var root = UIKit.Col().St(s => UIKit.Pad(s, 16, 18, 26, 18));
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            var top = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            top.Add(UIKit.IconBtn(() => gm.ShowHome(), UIKit.BackIcon(), Pal.Night, "Back"));
            var tc = UIKit.Col().St(s => s.alignItems = Align.Center);
            tc.Add(UIKit.Strong(Ladder.BatchName(batch).ToUpperInvariant() + " · " + (boss ? "BOSS" : "RIVAL " + (pos + 1) + " OF " + Ladder.PerBatch), 14f, Pal.BenchText, TextAnchor.MiddleCenter)
                .St(s => s.letterSpacing = 0.6f));
            tc.Add(UIKit.HandText(Ladder.Subtitle(batch).ToLowerInvariant(), 14f, Pal.BenchText, TextAnchor.MiddleCenter));
            top.Add(tc);
            top.Add(UIKit.El().St(s => s.width = 44));
            root.Add(top);
            root.Add(BatchStrip(save, batch, pos));

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
            info.Add(UIKit.KeyValue("Win", ownsPen ? "+" + (rival.reward + rival.reward / 2) + " marks (you have their pen)" : "Keep " + theirPen.name + " · +" + rival.reward + " marks", Pal.Green));
            if (boss)
            {
                info.Add(UIKit.DashedLine(Pal.Dash).St(s => s.marginBottom = 8));
                info.Add(UIKit.KeyValue("Batch bonus", "+" + Ladder.BatchBonus(batch) + " marks · opens " + Ladder.BatchName(batch + 1), Pal.Green));
            }
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

        /// <summary>The batch's 10 rivals: beaten (ticked), you're here (ringed), still locked ("?").</summary>
        static VisualElement BatchStrip(SaveData save, int batch, int pos)
        {
            var row = UIKit.Row().St(s => { s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; });
            for (int i = 0; i < Ladder.PerBatch; i++)
            {
                var r = Ladder.RivalAt(batch * Ladder.PerBatch + i);
                bool beaten = i < pos, current = i == pos;
                var dot = UIKit.Col().St(s =>
                {
                    s.width = 30; s.height = 30; UIKit.Radius(s, 15); s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                    s.backgroundColor = beaten || current ? r.avatar : Pal.Locked;
                    UIKit.Border(s, current ? 3f : beaten ? 3f : 1.5f, current ? Pal.Red : beaten ? Pal.Green : Pal.Ink);
                    s.opacity = beaten ? 0.75f : 1f;
                });
                dot.Add(UIKit.Disp(beaten || current ? r.initial : "?", 14f, current || beaten ? Pal.Ink : Pal.LockedText, TextAnchor.MiddleCenter));
                row.Add(dot);
            }
            return row;
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
