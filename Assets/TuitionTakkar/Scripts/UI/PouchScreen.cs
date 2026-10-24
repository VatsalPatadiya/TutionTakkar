using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Pencil pouch with two tabs: "My pens" (equip, ink left, refill) and the
    /// "Tuck shop" (buy pens with marks). Mockups S2–S4 on the design canvas.
    /// </summary>
    public static class PouchScreen
    {
        public static VisualElement Build(GameManager gm, int tab = 0)
        {
            var save = gm.Save;
            var root = UIKit.Col();
            UIKit.Laminate(root);
            root.pickingMode = PickingMode.Position;

            // Zip pouch header: back, marks, title, tabs.
            var head = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Pouch; UIKit.Pad(s, 16, 16, 18, 16); s.flexShrink = 0;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 24;
                s.borderBottomWidth = 6; s.borderBottomColor = Pal.PouchDark;
            });
            var row = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            row.Add(UIKit.IconBtn(() => gm.ShowHome(), UIKit.BackIcon(), Pal.PouchDark, "Back"));
            row.Add(MarksChip(save.marks, true));
            head.Add(row);
            head.Add(UIKit.Disp("MY PENCIL POUCH", 34f, Color.white).St(s => s.marginTop = 8));
            head.Add(UIKit.HandText(tab == 0 ? "win a match, keep their pen." : "win matches, earn marks, buy pens.", 16f, Pal.PouchText).St(s => s.marginTop = 2));

            var tabs = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginTop = 12; s.backgroundColor = Pal.PouchDark; UIKit.Radius(s, 14); UIKit.Pad(s, 4);
            });
            tabs.Add(Tab("My pens · " + save.owned.Count, tab == 0, () => gm.ShowPouch(0)));
            tabs.Add(Tab("Tuck shop", tab == 1, () => gm.ShowPouch(1)));
            head.Add(tabs);
            root.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 16, 16, 24, 16));

            if (tab == 1)
            {
                var hint = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.marginBottom = 12; });
                hint.Add(UIKit.HandText(Economy.DoubleAvailable(save) ? "today's first win pays double." : "double marks used today. back tomorrow.", 15f, Pal.BenchText));
                var earn = UIKit.Btn("How to earn ›", () => gm.UI.ShowOverlay(EarnSheet(gm)), Color.clear, Pal.Red, UIKit.BodyBold, 13f, 32f, 8f);
                hint.Add(earn);
                body.Add(hint);
            }

            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; });
            int i = 0;
            foreach (var pen in Catalog.Pens)
            {
                bool owned = save.Owns(pen.id);
                if (tab == 0 && !owned) continue;
                if (tab == 1 && pen.id == Catalog.StarterId) continue;
                grid.Add(tab == 0 ? OwnedCard(gm, pen, i) : ShopCard(gm, pen, i));
                i++;
            }
            body.Add(grid);

            if (tab == 0)
                body.Add(UIKit.Btn("Tuck shop: buy more pens ›", () => gm.ShowPouch(1), Pal.Yellow, Pal.Ink, UIKit.BodyBold, 15f, 48f, 12f, null, Pal.Ink)
                    .St(s => s.marginTop = 6));

            scroll.Add(body);
            root.Add(scroll);
            return root;
        }

        // ---------- shared bits ----------

        public static VisualElement MarksChip(int marks, bool dark)
        {
            var chip = UIKit.Row().St(s =>
            {
                s.height = 36; UIKit.Pad(s, 0, 12, 0, 8); UIKit.Radius(s, 18);
                s.backgroundColor = dark ? new Color(1f, 1f, 1f, 0.14f) : Pal.Paper;
            });
            chip.Add(new CoinIcon(20f));
            chip.Add(UIKit.Strong(marks.ToString(), 15f, dark ? Color.white : Pal.Ink).St(s => s.marginLeft = 6));
            return chip;
        }

        static Button Tab(string text, bool on, System.Action go)
        {
            var b = UIKit.Btn(text, on ? null : go, on ? Pal.Paper : Color.clear, on ? Pal.Ink : Pal.PouchText, UIKit.BodyBold, 14f, 38f, 10f);
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            return b;
        }

        static VisualElement Card(Color bg, int i)
        {
            var c = UIKit.Col().St(s =>
            {
                s.width = Length.Percent(48); s.minHeight = 168; s.marginBottom = 14; UIKit.Pad(s, 12);
                s.backgroundColor = bg;
                s.borderBottomWidth = 4; s.borderBottomColor = new Color(0f, 0f, 0f, 0.1f);
            });
            UIKit.Rot(c, i % 2 == 0 ? -0.8f : 0.8f);
            return c;
        }

        // ---------- My pens ----------

        static VisualElement OwnedCard(GameManager gm, PenDef pen, int i)
        {
            var save = gm.Save;
            bool equipped = save.equipped == pen.id;
            bool usesInk = Economy.UsesInk(pen.id);
            int ink = save.Ink(pen.id);

            var c = Card(Pal.Paper, i);
            if (equipped) UIKit.Border(c.style, 3f, Pal.Ink);
            c.Add(PenIcon.Build(pen, 140f));
            c.Add(UIKit.Disp(pen.name.ToUpperInvariant(), 18f, Pal.Ink).St(s => s.marginTop = 8));
            c.Add(UIKit.Plain(pen.traits, 12f, Pal.InkSoft).St(s => s.marginTop = 2));

            if (usesInk)
            {
                c.Add(UIKit.Strong(ink <= 0 ? "OUT OF INK · plays as Starter" : ink <= Economy.InkLowAt ? "INK LOW" : "INK", 10f,
                    ink <= Economy.InkLowAt ? (ink <= 0 ? Pal.Red : Pal.Orange) : Pal.InkSoft).St(s => { s.marginTop = 8; s.letterSpacing = 0.5f; }));
                c.Add(new InkBar(ink, Economy.InkTank).St(s => s.marginTop = 3));
            }
            else c.Add(UIKit.Plain("never needs ink", 12f, Pal.InkSoft).St(s => s.marginTop = 8));

            c.Add(UIKit.Grow());
            var actions = UIKit.Row().St(s => { s.marginTop = 8; s.justifyContent = Justify.SpaceBetween; });
            if (equipped)
                actions.Add(UIKit.Strong("EQUIPPED", 11f, Color.white, TextAnchor.MiddleCenter)
                    .St(s => { s.backgroundColor = Pal.Ink; UIKit.Pad(s, 6, 8, 6, 8); UIKit.Radius(s, 10); }));
            else
                actions.Add(UIKit.Btn("Equip", () => { save.equipped = pen.id; save.Save(); gm.ShowPouch(0); },
                    Pal.Yellow, Pal.Ink, UIKit.BodyBold, 13f, 34f, 10f, null, Pal.Ink).St(s => s.width = 64));

            if (usesInk && ink < Economy.InkTank)
                actions.Add(UIKit.Btn("Refill", () => gm.UI.ShowOverlay(RefillSheet(gm, pen)),
                    Pal.Pouch, Color.white, UIKit.BodyBold, 13f, 34f, 10f).St(s => s.width = 64));
            c.Add(actions);
            return c;
        }

        // ---------- Tuck shop ----------

        static VisualElement ShopCard(GameManager gm, PenDef pen, int i)
        {
            var save = gm.Save;
            bool owned = save.Owns(pen.id);
            bool forSale = Economy.ForSale(pen.id);
            int price = Economy.Price(pen.id);
            bool afford = save.marks >= price;

            var c = Card(owned ? Pal.Paper : forSale ? Color.white : Pal.H("#F3E7C2"), i);
            c.Add(PenIcon.Build(pen, 140f));
            c.Add(UIKit.Disp(pen.name.ToUpperInvariant(), 18f, Pal.Ink).St(s => s.marginTop = 8));
            c.Add(UIKit.Plain(pen.traits, 12f, Pal.InkSoft).St(s => s.marginTop = 2));
            c.Add(UIKit.Grow());

            if (owned)
            {
                c.Add(UIKit.Strong("In your pouch", 13f, Pal.Green).St(s => s.marginTop = 8));
            }
            else if (!forSale)
            {
                var rival = Catalog.RivalForPen(pen.id);
                c.Add(UIKit.Strong(rival != null ? "Beat " + rival.name + " to win" : "Not for sale", 12f, Pal.H("#6B5216")).St(s => s.marginTop = 8));
            }
            else
            {
                var b = UIKit.Btn("", () => gm.UI.ShowOverlay(BuySheet(gm, pen)),
                    afford ? Pal.Red : Pal.H("#E9E2D0"), afford ? Color.white : Pal.H("#8A7A5A"), UIKit.BodyBold, 14f, 34f, 17f);
                b.style.flexDirection = FlexDirection.Row;
                b.style.alignItems = Align.Center;
                b.style.alignSelf = Align.FlexStart;
                b.style.marginTop = 8;
                b.Add(new CoinIcon(16f));
                b.Add(UIKit.Strong(price.ToString(), 14f, afford ? Color.white : Pal.H("#8A7A5A")).St(s => s.marginLeft = 5));
                c.Add(b);
            }
            return c;
        }

        // ---------- sheets ----------

        static VisualElement Sheet(GameManager gm, out VisualElement panel)
        {
            var o = UIKit.El().St(s => s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f));
            o.pickingMode = PickingMode.Position;
            o.RegisterCallback<ClickEvent>(e => { if (e.target == o) gm.UI.HideOverlay(); });
            panel = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 0;
                s.borderTopLeftRadius = s.borderTopRightRadius = 22; UIKit.Pad(s, 22, 22, 28, 22);
            });
            UIKit.GraphPaper(panel);
            o.Add(panel);
            UIRoot.PopIn(panel);
            return o;
        }

        static VisualElement LedgerRow(string k, string v, Color vc, bool rule = false)
        {
            var r = UIKit.Row().St(s =>
            {
                s.justifyContent = Justify.SpaceBetween; s.paddingTop = 6; s.paddingBottom = 2;
                if (rule) { s.borderTopWidth = 2; s.borderTopColor = Pal.Dash; s.marginTop = 4; }
            });
            r.Add(UIKit.Strong(k, 15f, Pal.Ink));
            var right = UIKit.Row();
            right.Add(new CoinIcon(16f));
            right.Add(UIKit.Strong(v, 15f, vc).St(s => s.marginLeft = 5));
            r.Add(right);
            return r;
        }

        static VisualElement Ledger(int before, int cost, string what)
        {
            var box = UIKit.Col().St(s =>
            {
                s.marginTop = 16; s.backgroundColor = Color.white; UIKit.Border(s, 2f, Pal.Ink); UIKit.Radius(s, 12); UIKit.Pad(s, 8, 14, 10, 14);
            });
            box.Add(LedgerRow("Your marks", before.ToString(), Pal.Ink));
            box.Add(LedgerRow(what, "−" + cost, Pal.Red));
            box.Add(LedgerRow("Left after", Mathf.Max(0, before - cost).ToString(), Pal.Ink, true));
            return box;
        }

        static VisualElement SheetButtons(GameManager gm, string action, bool enabled, System.Action onConfirm)
        {
            var row = UIKit.Row().St(s => { s.marginTop = 18; s.justifyContent = Justify.SpaceBetween; });
            var later = UIKit.Btn("LATER", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.Display, 18f, 56f, 16f, null, Pal.Ink);
            later.style.width = 110;
            var go = UIKit.PrimaryBtn(action, enabled ? onConfirm : null, 56f);
            go.style.flexGrow = 1;
            go.style.marginLeft = 10;
            if (!enabled) { go.style.backgroundColor = Pal.H("#E9E2D0"); go.style.color = Pal.H("#8A7A5A"); go.style.borderBottomColor = Pal.H("#C9BFA8"); }
            row.Add(later, go);
            return row;
        }

        static VisualElement BuySheet(GameManager gm, PenDef pen)
        {
            var save = gm.Save;
            int price = Economy.Price(pen.id);
            bool afford = save.marks >= price;
            var o = Sheet(gm, out var p);

            var icon = PenIcon.Build(pen, 280f);
            icon.style.alignSelf = Align.Center;
            UIKit.Rot(icon, -4f);
            p.Add(icon);
            p.Add(UIKit.Disp(pen.name.ToUpperInvariant(), 30f, Pal.Ink).St(s => s.marginTop = 14));
            p.Add(UIKit.HandText(pen.blurb, 17f, Pal.Ink).St(s => s.marginTop = 2));
            var stats = UIKit.Col().St(s => s.marginTop = 12);
            stats.Add(UIKit.StatBar("Weight", pen.statWeight, Pal.Ink, Pal.GraphLine));
            stats.Add(UIKit.StatBar("Grip", pen.statGrip, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 6));
            stats.Add(UIKit.StatBar("Spin", pen.statSpin, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 6));
            p.Add(stats);
            p.Add(Ledger(save.marks, price, pen.name));

            var rival = Catalog.RivalForPen(pen.id);
            string note = !afford ? "you need " + (price - save.marks) + " more marks" : rival != null ? "or win it free by beating " + rival.name + " in the Batch Ladder" : "comes with a full tank of ink (" + Economy.InkTank + " matches)";
            p.Add(UIKit.Plain(note, 12f, afford ? Pal.InkSoft : Pal.Red, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));

            p.Add(SheetButtons(gm, "BUY FOR " + price, afford, () =>
            {
                if (save.marks < price || save.Owns(pen.id)) return;
                save.marks -= price;
                save.AddPen(pen.id);
                save.Save();
                gm.Audio.Stamp();
                Haptics.Win();
                gm.ShowPouch(0);
            }));
            return o;
        }

        static VisualElement RefillSheet(GameManager gm, PenDef pen)
        {
            var save = gm.Save;
            int ink = save.Ink(pen.id);
            int cost = Economy.RefillCost(pen.id, ink);
            bool afford = save.marks >= cost;
            var o = Sheet(gm, out var p);

            var icon = PenIcon.Build(pen, 240f);
            icon.style.alignSelf = Align.Center;
            p.Add(icon);
            p.Add(UIKit.Disp("REFILL " + pen.name.ToUpperInvariant(), 26f, Pal.Ink).St(s => s.marginTop = 14));
            p.Add(UIKit.HandText(ink <= 0 ? "out of ink: it plays like a Starter Click until you refill." : "top it up so it keeps its full powers.", 16f, Pal.Ink).St(s => s.marginTop = 2));
            p.Add(UIKit.Strong("INK NOW", 11f, Pal.InkSoft).St(s => { s.marginTop = 12; s.letterSpacing = 0.5f; }));
            p.Add(new InkBar(ink, Economy.InkTank).St(s => s.marginTop = 3));
            p.Add(UIKit.Strong("AFTER REFILL", 11f, Pal.InkSoft).St(s => { s.marginTop = 8; s.letterSpacing = 0.5f; }));
            p.Add(new InkBar(Economy.InkTank, Economy.InkTank).St(s => s.marginTop = 3));
            p.Add(Ledger(save.marks, cost, "Full tank"));
            if (!afford)
                p.Add(UIKit.Plain("you need " + (cost - save.marks) + " more marks", 12f, Pal.Red, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));

            p.Add(SheetButtons(gm, "REFILL FOR " + cost, afford, () =>
            {
                if (save.marks < cost) return;
                save.marks -= cost;
                save.SetInk(pen.id, Economy.InkTank);
                save.Save();
                gm.Audio.Stamp();
                gm.ShowPouch(0);
            }));
            return o;
        }

        public static VisualElement EarnSheet(GameManager gm)
        {
            var o = Sheet(gm, out var p);
            p.Add(UIKit.Disp("HOW TO EARN MARKS", 26f, Pal.Ink));
            p.Add(UIKit.HandText("every win puts marks in your pocket.", 16f, Pal.Ink).St(s => s.marginTop = 2));
            var list = UIKit.Col().St(s => s.marginTop = 10);
            list.Add(EarnRow("Practice win", "Easy " + Economy.PracticeReward(0) + " · Medium " + Economy.PracticeReward(1) + " · Hard " + Economy.PracticeReward(2), Economy.PracticeReward(0) + "–" + Economy.PracticeReward(2)));
            list.Add(EarnRow("Batch Ladder win", "grows with each rival, plus their pen", "25–100"));
            list.Add(EarnRow("Knock-off bonus", "each rival pen you send off", "+" + Economy.KnockOffBonus));
            list.Add(EarnRow("First win of the day", "doubles that match", "×2"));
            list.Add(EarnRow("Lost a match", "something for trying", "+" + Economy.LossConsolation));
            list.Add(EarnRow("Pass & Play", "two players on one phone could farm them", "0"));
            p.Add(list);
            var ok = UIKit.PrimaryBtn("GOT IT", () => gm.UI.HideOverlay(), 56f);
            ok.style.marginTop = 16;
            p.Add(ok);
            return o;
        }

        static VisualElement EarnRow(string title, string sub, string value)
        {
            var r = UIKit.Row().St(s =>
            {
                s.marginTop = 8; s.backgroundColor = Color.white; UIKit.Radius(s, 10); UIKit.Pad(s, 8, 12, 8, 12);
                s.justifyContent = Justify.SpaceBetween;
            });
            var col = UIKit.Col().St(s => { s.flexShrink = 1; s.marginRight = 8; });
            col.Add(UIKit.Strong(title, 14f, Pal.Ink), UIKit.Plain(sub, 11f, Pal.InkSoft));
            r.Add(col);
            var v = UIKit.Row();
            v.Add(new CoinIcon(15f));
            v.Add(UIKit.Disp(value, 18f, Pal.Ink).St(s => s.marginLeft = 4));
            r.Add(v);
            return r;
        }
    }
}
