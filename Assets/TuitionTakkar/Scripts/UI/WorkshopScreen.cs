using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Pen workshop (pouch tab 3): pick a pen type, build parts (barrel, grip, cap), colours,
    /// finish and pattern. The preview, the stats (with + / − against the pen you use now),
    /// the power bar and the price update as you choose; "Create" spends the marks and puts
    /// the pen in your pouch. Same look as the rest of the pouch.
    /// </summary>
    public class WorkshopScreen
    {
        public VisualElement Body { get; }
        public VisualElement Footer { get; }

        readonly GameManager gm;
        static PenParts.Spec spec = new PenParts.Spec { type = 0, body = 1, capColour = 15 };
        static bool specSet;

        readonly VisualElement preview, stats;
        readonly Label penName, penType, totalLabel, breakdown;
        readonly Button createBtn;
        readonly VisualElement[] sections = new VisualElement[9];

        public WorkshopScreen(GameManager manager)
        {
            gm = manager;
            if (!specSet)
            {
                specSet = true;
                // Start from the pen you use, if you designed it; else a blue click pen.
                if (PenParts.TryDecode(gm.Save.equipped, out var mine)) spec = mine;
            }

            Body = UIKit.Col();

            // ----- preview card -----
            var card = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Pad(s, 14); UIKit.Radius(s, 4); s.alignItems = Align.Center;
                s.borderBottomWidth = 4; s.borderBottomColor = new Color(0f, 0f, 0f, 0.1f);
            });
            UIKit.Rot(card, -0.6f);
            preview = UIKit.Col().St(s => { s.height = 46; s.justifyContent = Justify.Center; });
            card.Add(preview);
            penName = UIKit.Disp("", 22f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 8);
            penType = UIKit.Plain("", 12f, Pal.InkSoft, TextAnchor.MiddleCenter);
            card.Add(penName, penType);

            var nameField = new TextField { maxLength = 14, value = spec.name ?? "" };
            nameField.textEdition.placeholder = "name your pen";
            nameField.style.width = Length.Percent(80);
            nameField.style.marginTop = 8;
            nameField.style.unityFontDefinition = FontDefinition.FromFont(UIKit.BodyBold);
            nameField.style.fontSize = 15;
            nameField.RegisterValueChangedCallback(e => { spec.name = PenParts.CleanName(e.newValue); Refresh(); });
            card.Add(nameField);
            Body.Add(card);

            // ----- stats -----
            stats = UIKit.Col().St(s =>
            {
                s.marginTop = 14; s.backgroundColor = Color.white; UIKit.Border(s, 2f, Pal.Ink); UIKit.Radius(s, 12); UIKit.Pad(s, 10, 14, 12, 14);
            });
            Body.Add(stats);

            // ----- parts -----
            for (int i = 0; i < sections.Length; i++)
            {
                sections[i] = UIKit.Col().St(s => s.marginTop = 14);
                Body.Add(sections[i]);
            }

            // ----- footer: price + create -----
            Footer = UIKit.Row().St(s =>
            {
                s.flexShrink = 0; s.backgroundColor = Pal.Pouch; UIKit.Pad(s, 12, 16, 16, 16);
                s.borderTopLeftRadius = s.borderTopRightRadius = 18;
            });
            var priceCol = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; });
            var totalRow = UIKit.Row();
            totalRow.Add(new CoinIcon(20f));
            totalLabel = UIKit.Disp("0", 26f, Color.white).St(s => s.marginLeft = 6);
            totalRow.Add(totalLabel);
            priceCol.Add(totalRow);
            breakdown = UIKit.Plain("", 11f, Pal.PouchText);
            priceCol.Add(breakdown);
            Footer.Add(priceCol);
            createBtn = UIKit.PrimaryBtn("CREATE", () => gm.UI.ShowOverlay(ConfirmSheet()), 54f);
            createBtn.style.width = 150;
            Footer.Add(createBtn);

            Refresh();
        }

        // ---------- refresh everything that depends on the choices ----------

        void Refresh()
        {
            var save = gm.Save;
            var pen = PenParts.Build(spec, PenParts.Encode(spec), PenRarity.Epic, PenParts.CustomBudget);
            pen.custom = true;
            var type = PenParts.Types[spec.type];

            preview.Clear();
            preview.Add(PenIcon.Build(pen, 280f));
            penName.text = (string.IsNullOrEmpty(spec.name) ? "My " + type.key : spec.name).ToUpperInvariant();
            penType.text = type.name + " · " + PenParts.Finishes[spec.finish].name.ToLowerInvariant()
                + (spec.pattern != 0 ? " · " + PenParts.Patterns[spec.pattern].name.ToLowerInvariant() : "");

            // Stats against the pen you use now.
            var now = Catalog.Pen(save.equipped);
            var st = PenParts.StatsOf(spec, PenParts.CustomBudget);
            stats.Clear();
            var head = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
            head.Add(UIKit.Strong("HOW IT PLAYS", 12f, Pal.InkSoft).St(s => s.letterSpacing = 0.5f));
            head.Add(UIKit.Plain("vs your " + now.name, 11f, Pal.InkSoft));
            stats.Add(head);
            stats.Add(StatRow("Weight", st.weight, now.statWeight));
            stats.Add(StatRow("Grip", st.grip, now.statGrip));
            stats.Add(StatRow("Spin", st.spin, now.statSpin));

            var power = UIKit.Row().St(s => s.marginTop = 10);
            power.Add(UIKit.Strong("Power", 14f, Pal.Ink).St(s => s.width = 70f));
            var bar = UIKit.El().St(s => { s.flexGrow = 1; s.height = 14f; s.backgroundColor = Pal.GraphLine; UIKit.Radius(s, 7f); s.overflow = Overflow.Hidden; });
            float pw = st.Power / PenParts.MaxPower;
            bar.Add(UIKit.El().St(s => { s.width = Length.Percent(Mathf.Clamp01(pw) * 100f); s.height = 14f; s.backgroundColor = pw > .88f ? Pal.Red : pw > .78f ? Pal.Orange : Pal.Green; }));
            // Budget line: the most a workshop pen may reach.
            bar.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = Length.Percent(PenParts.CustomBudget / PenParts.MaxPower * 100f); s.top = 0; s.bottom = 0; s.width = 2; s.backgroundColor = Pal.Ink; }));
            power.Add(bar);
            power.Add(UIKit.Strong(Mathf.RoundToInt(st.Power * 50f).ToString(), 14f, Pal.Ink).St(s => { s.width = 34; s.unityTextAlign = TextAnchor.MiddleRight; }));
            stats.Add(power);
            stats.Add(UIKit.Plain(st.capped ? "power limit reached: stats trimmed to keep matches fair."
                : "more of one stat means less of another. colours and finishes don't change play.", 11f, st.capped ? Pal.Red : Pal.InkSoft).St(s =>
            {
                s.marginTop = 6; s.whiteSpace = WhiteSpace.Normal;
            }));

            // Parts.
            Options(0, "PEN TYPE", "decides size and how it plays", PenParts.Types, spec.type, v => spec.type = v);
            Options(1, "BARREL", "changes weight and spin", PenParts.Barrels, spec.barrel, v => spec.barrel = v);
            Options(2, "GRIP", "changes grip", PenParts.Grips, spec.grip, v => spec.grip = v);
            Options(3, "CAP", "changes weight and wobble", PenParts.Caps, spec.cap, v => spec.cap = v);
            Looks(4, "FINISH", PenParts.Finishes, spec.finish, v => spec.finish = v);
            Looks(5, "PATTERN", PenParts.Patterns, spec.pattern, v => spec.pattern = v);
            Swatches(6, "BARREL COLOUR", spec.body, v => spec.body = v, spec.finish == (int)PenFinish.Gold ? "gold finish sets the barrel colour" : null);
            Swatches(7, "CAP COLOUR", spec.capColour, v => spec.capColour = v, spec.cap == 1 ? "a metal cap is always silver" : null);
            sections[8].Clear();

            int cost = PenParts.Cost(spec, out int parts, out int looks, out int premium);
            totalLabel.text = cost.ToString();
            breakdown.text = "parts " + parts + " · looks " + looks + " · power " + premium;
            bool owned = save.Owns(PenParts.Encode(spec));
            bool afford = save.marks >= cost;
            createBtn.text = owned ? "IN POUCH" : "CREATE";
            createBtn.SetEnabled(!owned);
            createBtn.style.backgroundColor = owned || !afford ? Pal.H("#E9E2D0") : Pal.Red;
            createBtn.style.color = owned || !afford ? Pal.H("#8A7A5A") : Color.white;
        }

        VisualElement StatRow(string label, float value, float now)
        {
            var row = UIKit.Row().St(s => s.marginTop = 6);
            var bar = UIKit.StatBar(label, value, Pal.Ink, Pal.GraphLine);
            bar.style.flexGrow = 1;
            row.Add(bar);
            int d = Mathf.RoundToInt((value - now) * 10f);
            string t = d == 0 ? "=" : (d > 0 ? "+" : "−") + Mathf.Abs(d);
            row.Add(UIKit.Strong(t, 13f, d > 0 ? Pal.Green : d < 0 ? Pal.Red : Pal.InkSoft).St(s => { s.width = 34; s.unityTextAlign = TextAnchor.MiddleRight; }));
            return row;
        }

        void Header(VisualElement sec, string title, string sub)
        {
            var h = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.marginBottom = 6; s.alignItems = Align.FlexEnd; });
            h.Add(UIKit.Disp(title, 18f, Pal.Ink));
            if (!string.IsNullOrEmpty(sub)) h.Add(UIKit.HandText(sub, 14f, Pal.BenchText));
            sec.Add(h);
        }

        static Button Choice(string title, string note, int price, bool on, Action pick)
        {
            var b = UIKit.Btn("", on ? null : pick, on ? Pal.Yellow : Pal.Paper, Pal.Ink, UIKit.BodyBold, 13f, 58f, 10f, null, on ? Pal.Ink : Pal.H("#C9BFA8"));
            b.style.width = Length.Percent(48);
            b.style.height = StyleKeyword.Auto;
            b.style.minHeight = 54;
            b.style.marginBottom = 8;
            b.style.flexDirection = FlexDirection.Column;
            b.style.alignItems = Align.FlexStart;
            b.style.justifyContent = Justify.Center;
            UIKit.Pad(b.style, 6, 10, 6, 10);
            var top = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.alignSelf = Align.Stretch; });
            top.Add(UIKit.Strong(title, 14f, Pal.Ink));
            var pr = UIKit.Row();
            if (price > 0) { pr.Add(new CoinIcon(13f)); pr.Add(UIKit.Strong(price.ToString(), 12f, Pal.Ink).St(s => s.marginLeft = 3)); }
            else pr.Add(UIKit.Strong("free", 12f, Pal.InkSoft));
            top.Add(pr);
            b.Add(top);
            if (!string.IsNullOrEmpty(note)) b.Add(UIKit.Plain(note, 11f, Pal.InkSoft).St(s => { s.marginTop = 2; s.whiteSpace = WhiteSpace.Normal; }));
            foreach (var child in b.Children()) child.pickingMode = PickingMode.Ignore;
            return b;
        }

        void Options(int i, string title, string sub, PenParts.Part[] parts, int current, Action<int> set)
        {
            var sec = sections[i];
            sec.Clear();
            Header(sec, title, sub);
            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; });
            for (int k = 0; k < parts.Length; k++)
            {
                int kk = k;
                grid.Add(Choice(parts[k].name, parts[k].note, parts[k].price, k == current, () => { set(kk); Refresh(); }));
            }
            sec.Add(grid);
        }

        void Looks(int i, string title, PenParts.Look[] looks, int current, Action<int> set)
        {
            var sec = sections[i];
            sec.Clear();
            Header(sec, title, "looks only");
            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; });
            for (int k = 0; k < looks.Length; k++)
            {
                int kk = k;
                grid.Add(Choice(looks[k].name, null, looks[k].price, k == current, () => { set(kk); Refresh(); }));
            }
            sec.Add(grid);
        }

        void Swatches(int i, string title, int current, Action<int> set, string note)
        {
            var sec = sections[i];
            sec.Clear();
            Header(sec, title, note ?? "free");
            var row = UIKit.Row().St(s => s.flexWrap = Wrap.Wrap);
            for (int k = 0; k < PenParts.Colours.Length; k++)
            {
                int kk = k;
                var b = UIKit.Btn("", () => { set(kk); Refresh(); }, PenParts.Colours[k], Pal.Ink, UIKit.Body, 10f, 36f, 18f, null, k == current ? Pal.Ink : Pal.H("#C9BFA8"));
                b.style.width = 36;
                b.style.marginRight = 8; b.style.marginBottom = 8;
                if (k == current) UIKit.Border(b.style, 4f, Pal.Ink);
                row.Add(b);
            }
            sec.Add(row);
        }

        // ---------- pay and make it ----------

        VisualElement ConfirmSheet()
        {
            var save = gm.Save;
            string id = PenParts.Encode(spec);
            var pen = Catalog.Pen(id);
            int cost = PenParts.Cost(spec);
            bool afford = save.marks >= cost;
            var o = PouchScreen.Sheet(gm, out var p);

            var icon = PenIcon.Build(pen, 280f);
            icon.style.alignSelf = Align.Center;
            UIKit.Rot(icon, -4f);
            p.Add(icon);
            p.Add(UIKit.Disp(pen.name.ToUpperInvariant(), 30f, Pal.Ink).St(s => s.marginTop = 14));
            p.Add(UIKit.HandText(pen.blurb, 17f, Pal.Ink).St(s => s.marginTop = 2));
            var st = UIKit.Col().St(s => s.marginTop = 12);
            st.Add(UIKit.StatBar("Weight", pen.statWeight, Pal.Ink, Pal.GraphLine));
            st.Add(UIKit.StatBar("Grip", pen.statGrip, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 6));
            st.Add(UIKit.StatBar("Spin", pen.statSpin, Pal.Ink, Pal.GraphLine).St(s => s.marginTop = 6));
            p.Add(st);
            p.Add(PouchScreen.Ledger(save.marks, cost, "Make this pen"));
            p.Add(UIKit.Plain(afford ? "goes straight into your pouch with a full tank of ink, and you start using it." : "you need " + (cost - save.marks) + " more marks",
                12f, afford ? Pal.InkSoft : Pal.Red, TextAnchor.MiddleCenter).St(s => { s.marginTop = 8; s.whiteSpace = WhiteSpace.Normal; }));

            p.Add(PouchScreen.SheetButtons(gm, "MAKE FOR " + cost, afford, () =>
            {
                if (save.marks < cost || save.Owns(id)) return;
                save.marks -= cost;
                save.AddPen(id);
                save.equipped = id;
                save.Save();
                gm.Audio.Stamp();
                Haptics.Win();
                gm.UI.HideOverlay();
                gm.ShowPouch(0);
            }));
            return o;
        }
    }
}
