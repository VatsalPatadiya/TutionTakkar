using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Pen workshop (pouch tab 3), made easy: a big preview, a RANDOM button and ready-made pens
    /// at the top, then three simple steps (1 COLOUR, 2 CAP, 3 GRIP) and an optional MORE step
    /// (pen type, barrel, finish, pattern). Just taps, no sliders. The price is always in the
    /// footer and MAKE IT spends the marks and puts the pen in your pouch.
    /// </summary>
    public class WorkshopScreen
    {
        public VisualElement Body { get; }
        public VisualElement Footer { get; }

        readonly GameManager gm;
        static PenParts.Spec spec = new PenParts.Spec { type = 0, body = 1, capColour = 15 };
        static bool specSet;
        static int step;   // 0 colour, 1 cap, 2 grip, 3 more

        readonly VisualElement preview, stats, steps, stepBody, readyRow;
        readonly Label penName, penType, totalLabel, breakdown;
        readonly Button createBtn;
        static readonly System.Random rng = new System.Random();

        static readonly string[] StepNames = { "COLOUR", "CAP", "GRIP", "MORE" };

        // Ready-made pens: one tap, then change anything you like.
        struct Ready { public string name; public PenParts.Spec spec; }
        static readonly Ready[] Presets =
        {
            new Ready { name = "Speedy", spec = new PenParts.Spec { type = 0, barrel = 0, grip = 1, cap = 0, body = 1, capColour = 15, finish = 0, pattern = 1, name = "Speedy" } },
            new Ready { name = "Tank", spec = new PenParts.Spec { type = 1, barrel = 1, grip = 0, cap = 1, body = 12, capColour = 13, finish = 1, pattern = 0, name = "Tank" } },
            new Ready { name = "Grippy", spec = new PenParts.Spec { type = 2, barrel = 2, grip = 2, cap = 0, body = 4, capColour = 2, finish = 0, pattern = 2, name = "Grippy" } },
            new Ready { name = "Spinner", spec = new PenParts.Spec { type = 3, barrel = 3, grip = 1, cap = 2, body = 7, capColour = 9, finish = 2, pattern = 0, name = "Spinner" } },
            new Ready { name = "Sparkle", spec = new PenParts.Spec { type = 0, barrel = 0, grip = 0, cap = 0, body = 9, capColour = 6, finish = 4, pattern = 4, name = "Sparkle" } },
            new Ready { name = "Topper", spec = new PenParts.Spec { type = 1, barrel = 2, grip = 3, cap = 3, body = 3, capColour = 0, finish = 6, pattern = 0, name = "Topper" } },
        };

        public WorkshopScreen(GameManager manager)
        {
            gm = manager;
            if (!specSet)
            {
                specSet = true;
                // Start from the pen you use, if you designed it; else a blue click pen.
                if (PenParts.TryDecode(gm.Save.equipped, out var mine)) spec = mine;
            }
            spec = Safe(spec);

            Body = UIKit.Col();

            // ----- big preview card -----
            var card = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Pad(s, 18, 14, 14, 14); UIKit.Radius(s, 4); s.alignItems = Align.Center;
                s.borderBottomWidth = 4; s.borderBottomColor = new Color(0f, 0f, 0f, 0.1f);
            });
            UIKit.Rot(card, -0.6f);
            preview = UIKit.Col().St(s => { s.height = 74; s.justifyContent = Justify.Center; });
            card.Add(preview);
            penName = UIKit.Disp("", 26f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 10);
            penType = UIKit.Plain("", 12f, Pal.InkSoft, TextAnchor.MiddleCenter);
            card.Add(penName, penType);

            var tools = UIKit.Row().St(s => { s.marginTop = 10; s.alignSelf = Align.Stretch; s.justifyContent = Justify.Center; });
            var dice = UIKit.Btn("RANDOM", Randomise, Pal.Night, Color.white, UIKit.Display, 18f, 46f, 12f, Pal.H("#0E171B"));
            dice.style.width = 140; dice.style.marginRight = 10;
            tools.Add(dice);
            var nameField = new TextField { maxLength = 14, value = spec.name ?? "" };
            nameField.textEdition.placeholder = "name it";
            nameField.style.flexGrow = 1; nameField.style.maxWidth = 170; nameField.style.height = 46;
            nameField.style.unityFontDefinition = FontDefinition.FromFont(UIKit.BodyBold);
            nameField.style.fontSize = 15;
            nameField.RegisterValueChangedCallback(e => { spec.name = PenParts.CleanName(e.newValue); Refresh(); });
            tools.Add(nameField);
            card.Add(tools);
            Body.Add(card);

            // ----- ready-made pens -----
            var readyHead = UIKit.Row().St(s => { s.marginTop = 16; s.justifyContent = Justify.SpaceBetween; s.alignItems = Align.FlexEnd; });
            readyHead.Add(UIKit.Disp("READY-MADE", 18f, Pal.Ink));
            readyHead.Add(UIKit.HandText("tap one, then change it", 14f, Pal.BenchText));
            Body.Add(readyHead);
            var readyScroll = new ScrollView(ScrollViewMode.Horizontal) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            readyScroll.style.marginTop = 6;
            readyRow = UIKit.Row();
            readyScroll.Add(readyRow);
            Body.Add(readyScroll);

            // ----- steps -----
            steps = UIKit.Row().St(s => { s.marginTop = 16; });
            Body.Add(steps);
            stepBody = UIKit.Col().St(s =>
            {
                s.marginTop = 8; s.backgroundColor = Color.white; UIKit.Border(s, 2f, Pal.Ink); UIKit.Radius(s, 12); UIKit.Pad(s, 12);
            });
            Body.Add(stepBody);

            // ----- how it plays (small) -----
            stats = UIKit.Col().St(s =>
            {
                s.marginTop = 14; s.backgroundColor = Color.white; UIKit.Border(s, 2f, Pal.Ink); UIKit.Radius(s, 12); UIKit.Pad(s, 10, 14, 12, 14);
            });
            Body.Add(stats);

            // ----- footer: price + make it -----
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
            createBtn = UIKit.PrimaryBtn("MAKE IT", () => gm.UI.ShowOverlay(ConfirmSheet()), 54f);
            createBtn.style.width = 160;
            Footer.Add(createBtn);

            Refresh();
        }

        /// <summary>Keeps every choice inside its list (presets and old saves can't break the screen).</summary>
        static PenParts.Spec Safe(PenParts.Spec p)
        {
            int W(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
            p.type = W(p.type, PenParts.Types.Length);
            p.barrel = W(p.barrel, PenParts.Barrels.Length);
            p.grip = W(p.grip, PenParts.Grips.Length);
            p.cap = W(p.cap, PenParts.Caps.Length);
            p.body = W(p.body, PenParts.Colours.Length);
            p.capColour = W(p.capColour, PenParts.Colours.Length);
            p.finish = W(p.finish, PenParts.Finishes.Length);
            p.pattern = W(p.pattern, PenParts.Patterns.Length);
            return p;
        }

        void Randomise()
        {
            var name = spec.name;
            spec = new PenParts.Spec
            {
                type = rng.Next(PenParts.Types.Length),
                barrel = rng.Next(PenParts.Barrels.Length),
                grip = rng.Next(PenParts.Grips.Length),
                cap = rng.Next(PenParts.Caps.Length),
                body = rng.Next(PenParts.Colours.Length),
                capColour = rng.Next(PenParts.Colours.Length),
                // Mostly plain looks, so a random pen isn't always the expensive one.
                finish = rng.NextDouble() < 0.6 ? 0 : rng.Next(PenParts.Finishes.Length),
                pattern = rng.NextDouble() < 0.6 ? 0 : rng.Next(PenParts.Patterns.Length),
                name = name,
            };
            gm.Audio.Ui();
            Haptics.AimTick();
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
            preview.Add(PenIcon.Build(pen, 330f));
            penName.text = (string.IsNullOrEmpty(spec.name) ? "My " + type.key : spec.name).ToUpperInvariant();
            penType.text = type.name + " · " + PenParts.Finishes[spec.finish].name.ToLowerInvariant()
                + (spec.pattern != 0 ? " · " + PenParts.Patterns[spec.pattern].name.ToLowerInvariant() : "");

            // Ready-made row.
            readyRow.Clear();
            foreach (var r in Presets)
            {
                var ps = Safe(r.spec);
                bool on = PenParts.Encode(ps) == PenParts.Encode(WithName(spec, ps.name));
                var b = UIKit.Btn("", () => { var keep = spec.name; spec = ps; if (!string.IsNullOrEmpty(keep) && keep != ps.name && !IsPresetName(keep)) spec.name = keep; gm.Audio.Ui(); Refresh(); },
                    on ? Pal.Yellow : Pal.Paper, Pal.Ink, UIKit.BodyBold, 13f, 74f, 12f, null, on ? Pal.Ink : Pal.H("#C9BFA8"));
                b.style.width = 108; b.style.height = 78; b.style.marginRight = 8;
                b.style.flexDirection = FlexDirection.Column; b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
                var pdef = PenParts.Build(ps, PenParts.Encode(ps), PenRarity.Epic, PenParts.CustomBudget);
                var icon = PenIcon.Build(pdef, 90f);
                icon.pickingMode = PickingMode.Ignore;
                b.Add(icon);
                var lbl = UIKit.Strong(r.name, 13f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6);
                lbl.pickingMode = PickingMode.Ignore;
                b.Add(lbl);
                readyRow.Add(b);
            }

            // Step chips.
            steps.Clear();
            for (int i = 0; i < StepNames.Length; i++)
            {
                int ii = i;
                bool on = i == step;
                string label = i < 3 ? (i + 1) + " " + StepNames[i] : StepNames[i];
                var chip = UIKit.Btn(label, () => { step = ii; gm.Audio.Ui(); Refresh(); }, on ? Pal.Ink : Pal.Paper, on ? Color.white : Pal.Ink,
                    UIKit.Display, 15f, 42f, 12f, null, on ? Pal.Ink : Pal.H("#C9BFA8"));
                chip.style.flexGrow = 1; chip.style.flexBasis = 0;
                if (i < StepNames.Length - 1) chip.style.marginRight = 6;
                steps.Add(chip);
            }

            // The step itself.
            stepBody.Clear();
            switch (step)
            {
                case 0:
                    Swatches(stepBody, "PEN COLOUR", spec.body, v => spec.body = v, spec.finish == (int)PenFinish.Gold ? "gold finish sets the colour" : "free");
                    break;
                case 1:
                    Options(stepBody, "CAP", "changes weight and wobble", PenParts.Caps, spec.cap, v => spec.cap = v);
                    Swatches(stepBody, "CAP COLOUR", spec.capColour, v => spec.capColour = v, spec.cap == 1 ? "a metal cap is always silver" : "free");
                    break;
                case 2:
                    Options(stepBody, "GRIP", "changes grip", PenParts.Grips, spec.grip, v => spec.grip = v);
                    break;
                default:
                    Options(stepBody, "PEN TYPE", "size and how it plays", PenParts.Types, spec.type, v => spec.type = v);
                    Options(stepBody, "BARREL", "weight and spin", PenParts.Barrels, spec.barrel, v => spec.barrel = v);
                    Looks(stepBody, "FINISH", PenParts.Finishes, spec.finish, v => spec.finish = v);
                    Looks(stepBody, "PATTERN", PenParts.Patterns, spec.pattern, v => spec.pattern = v);
                    break;
            }
            if (step < 3)
            {
                var nextBtn = UIKit.Btn(step < 2 ? "NEXT: " + StepNames[step + 1] : "MORE OPTIONS (optional)", () => { step++; gm.Audio.Ui(); Refresh(); },
                    Pal.Paper, Pal.Ink, UIKit.BodyBold, 14f, 40f, 10f, null, Pal.H("#C9BFA8"));
                nextBtn.style.marginTop = 6;
                stepBody.Add(nextBtn);
            }

            // How it plays, against the pen you use now.
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
            if (st.capped)
                stats.Add(UIKit.Plain("power limit reached: stats trimmed to keep matches fair.", 11f, Pal.Red).St(s => { s.marginTop = 6; s.whiteSpace = WhiteSpace.Normal; }));

            int cost = PenParts.Cost(spec, out int parts, out int looks, out int premium);
            totalLabel.text = cost.ToString();
            breakdown.text = "parts " + parts + " · looks " + looks + " · power " + premium;
            bool owned = save.Owns(PenParts.Encode(spec));
            bool afford = save.marks >= cost;
            createBtn.text = owned ? "IN POUCH" : "MAKE IT";
            createBtn.SetEnabled(!owned);
            createBtn.style.backgroundColor = owned || !afford ? Pal.H("#E9E2D0") : Pal.Red;
            createBtn.style.color = owned || !afford ? Pal.H("#8A7A5A") : Color.white;
        }

        static PenParts.Spec WithName(PenParts.Spec p, string name) { p.name = name; return p; }

        static bool IsPresetName(string n)
        {
            foreach (var r in Presets) if (r.name == n) return true;
            return false;
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

        static void Header(VisualElement sec, string title, string sub)
        {
            var h = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.marginBottom = 6; s.marginTop = 4; s.alignItems = Align.FlexEnd; });
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

        void Options(VisualElement sec, string title, string sub, PenParts.Part[] parts, int current, Action<int> set)
        {
            Header(sec, title, sub);
            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; });
            for (int k = 0; k < parts.Length; k++)
            {
                int kk = k;
                grid.Add(Choice(parts[k].name, parts[k].note, parts[k].price, k == current, () => { set(kk); gm.Audio.Ui(); Refresh(); }));
            }
            sec.Add(grid);
        }

        void Looks(VisualElement sec, string title, PenParts.Look[] looks, int current, Action<int> set)
        {
            Header(sec, title, "looks only");
            var grid = UIKit.Row(Align.Stretch).St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; });
            for (int k = 0; k < looks.Length; k++)
            {
                int kk = k;
                grid.Add(Choice(looks[k].name, null, looks[k].price, k == current, () => { set(kk); gm.Audio.Ui(); Refresh(); }));
            }
            sec.Add(grid);
        }

        void Swatches(VisualElement sec, string title, int current, Action<int> set, string note)
        {
            Header(sec, title, note);
            var row = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.FlexStart; });
            for (int k = 0; k < PenParts.Colours.Length; k++)
            {
                int kk = k;
                bool on = k == current;
                var b = UIKit.Btn("", () => { set(kk); gm.Audio.Ui(); Refresh(); }, PenParts.Colours[k], Pal.Ink, UIKit.Body, 10f, 46f, 23f, null, on ? Pal.Ink : Pal.H("#C9BFA8"));
                b.style.width = 46;
                b.style.marginRight = 8; b.style.marginBottom = 8;
                if (on) UIKit.Border(b.style, 4f, Pal.Ink);
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

            p.Add(PouchScreen.SheetButtons(gm, "MAKE IT · " + cost, afford, () =>
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
