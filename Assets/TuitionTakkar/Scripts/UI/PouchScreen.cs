using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    public static class PouchScreen
    {
        public static VisualElement Build(GameManager gm)
        {
            var save = gm.Save;
            var root = UIKit.Col();
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            // Zip pouch header
            var head = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Pouch; UIKit.Pad(s, 16, 16, 24, 16); s.flexShrink = 0;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 28;
                s.borderBottomWidth = 6; s.borderBottomColor = Pal.PouchDark;
            });
            var row = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            row.Add(UIKit.IconBtn(() => gm.ShowHome(), UIKit.BackIcon(), Pal.PouchDark, "Back"));
            row.Add(UIKit.Strong(save.owned.Count + " of " + Catalog.Pens.Count + " pens", 14f, Pal.PouchText, TextAnchor.MiddleRight));
            head.Add(row);
            head.Add(UIKit.Disp("MY PENCIL POUCH", 38f, Color.white).St(s => s.marginTop = 10));
            head.Add(UIKit.HandText("win a match, keep their pen.", 17f, Pal.PouchText).St(s => s.marginTop = 2));
            var zip = UIKit.DashedLine(new Color(1, 1, 1, 0.4f));
            UIKit.Abs(zip, 20, null, 20, 8);
            head.Add(zip);
            root.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 22, 16, 24, 16); });

            foreach (var pen in Catalog.Pens)
                grid.Add(Card(gm, pen));

            scroll.Add(grid);
            root.Add(scroll);
            return root;
        }

        static VisualElement Card(GameManager gm, PenDef pen)
        {
            var save = gm.Save;
            bool owned = save.Owns(pen.id);
            bool equipped = save.equipped == pen.id;
            bool prize = pen.id == "golden";

            var c = UIKit.Col().St(s =>
            {
                s.width = Length.Percent(48); s.minHeight = 172; s.marginBottom = 14; UIKit.Pad(s, 12); UIKit.Radius(s, 14);
                s.backgroundColor = prize && !owned ? Pal.Night : owned ? Pal.Paper : new Color(1f, 0.99f, 0.96f, 0.55f);
                if (equipped) UIKit.Border(s, 3f, Pal.Ink);
                else if (!owned && !prize) UIKit.Border(s, 2f, Pal.Locked);
            });

            c.Add(PenIcon.Build(pen, 140f, !owned && !prize));
            var nameColor = prize && !owned ? Pal.Yellow : owned ? Pal.Ink : Pal.LockedText;
            c.Add(UIKit.Disp(pen.name.ToUpperInvariant(), 19f, nameColor).St(s => s.marginTop = 8));
            c.Add(UIKit.Plain(pen.traits, 13f, prize && !owned ? Pal.MistLight : owned ? Pal.InkSoft : Pal.LockedText).St(s => s.marginTop = 4));
            c.Add(UIKit.Grow());

            if (equipped)
            {
                c.Add(UIKit.Strong("EQUIPPED", 12f, Color.white, TextAnchor.MiddleCenter)
                    .St(s => { s.alignSelf = Align.FlexStart; s.backgroundColor = Pal.Ink; UIKit.Pad(s, 6, 10, 6, 10); UIKit.Radius(s, 10); s.marginTop = 8; }));
            }
            else if (owned)
            {
                var b = UIKit.Btn("Equip", () =>
                {
                    save.equipped = pen.id;
                    save.Save();
                    gm.ShowPouch();
                }, Pal.Yellow, Pal.Ink, UIKit.BodyBold, 14f, 44f, 10f, null, Pal.Ink);
                b.style.marginTop = 8;
                c.Add(b);
            }
            else
            {
                var rival = Catalog.RivalForPen(pen.id);
                string need = prize ? "Beat all " + Catalog.Rivals.Count : rival != null ? "Beat " + rival.name : "Locked";
                c.Add(UIKit.Strong(need, 13f, prize ? Pal.MistLight : Pal.LockedText).St(s => s.marginTop = 8));
            }
            return c;
        }
    }
}
