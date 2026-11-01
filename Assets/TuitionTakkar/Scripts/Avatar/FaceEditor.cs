using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// "MY FACE": big preview with RANDOM, one tab per part, a colour row, a grid of choices you can
    /// try on (locked ones show their price), and one big SAVE that buys what you picked.
    /// </summary>
    public static class FaceEditor
    {
        static AvatarParts.Face face;
        static string tab = "face";
        static Action back;

        public static void Show(GameManager gm, Action onBack = null)
        {
            back = onBack ?? (() => gm.ShowHome());
            face = AvatarParts.Parse(AvatarParts.MyCode(gm.Save)) ?? AvatarParts.Random(new System.Random());
            tab = "face";
            Paint(gm);
        }

        static int Cost(SaveData s, AvatarParts.Face f, List<string> items)
        {
            int total = 0;
            foreach (var layer in AvatarParts.Order)
            {
                var p = AvatarParts.Get(f.PartId(layer));
                if (p != null && !AvatarParts.Owns(s, p)) { total += p.price; items?.Add(p.id); }
            }
            foreach (var k in AvatarParts.ColorKeys)
            {
                int i = f.Color(k);
                if (!AvatarParts.OwnsColor(s, k, i)) { total += AvatarParts.ColorPrice(k, i); items?.Add(AvatarParts.ColorItem(k, i)); }
            }
            return total;
        }

        static void Paint(GameManager gm)
        {
            var save = gm.Save;
            var root = OnlineScreens.Screen();
            root.name = "FaceEditor";
            root.Add(OnlineScreens.Header("MY FACE", "make it look like you", () => back()));

            // ---------- preview ----------
            var top = UIKit.Row().St(s => { UIKit.Pad(s, 14, 16, 6, 16); s.alignItems = Align.Center; s.flexShrink = 0; });
            var big = UIKit.El().St(s =>
            {
                s.width = 150; s.height = 150; UIKit.Radius(s, 75); UIKit.Border(s, 5f, Pal.Ink); s.backgroundColor = Pal.Paper; s.flexShrink = 0;
            });
            UIKit.Rot(big, -2f);
            Faces.Fill(big, face.ToString());
            top.Add(big);
            var side = UIKit.Col().St(s => { s.flexGrow = 1; s.marginLeft = 16; });
            var rnd = UIKit.Btn("RANDOM", () => { face = AvatarParts.Random(new System.Random()); Paint(gm); },
                Pal.Yellow, Pal.Ink, UIKit.Display, 22f, 54f, 14f, Pal.H("#C7A928"));
            side.Add(rnd);
            var wallet = UIKit.Row().St(s => { s.marginTop = 12; s.alignSelf = Align.FlexStart; s.backgroundColor = Pal.Night; UIKit.Pad(s, 6, 12, 6, 10); UIKit.Radius(s, 16); });
            wallet.Add(new CoinIcon(20f));
            wallet.Add(UIKit.Strong(Desks.Money(save.marks), 16f, Color.white).St(s => s.marginLeft = 6));
            side.Add(wallet);
            top.Add(side);
            root.Add(top);

            // ---------- tabs ----------
            var tabs = new ScrollView(ScrollViewMode.Horizontal);
            tabs.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            tabs.style.flexShrink = 0;
            tabs.style.marginTop = 8;
            var trow = UIKit.Row().St(s => UIKit.Pad(s, 0, 12, 0, 12));
            foreach (var t in AvatarParts.Tabs)
            {
                string layer = t;
                bool on = layer == tab;
                var b = UIKit.Btn(AvatarParts.TabName(layer), () => { tab = layer; Paint(gm); },
                    on ? Pal.Yellow : Pal.Paper, Pal.Ink, UIKit.Display, 16f, 42f, 12f, on ? Pal.H("#C7A928") : Pal.H("#C9D1D9"));
                b.style.marginRight = 8;
                UIKit.Pad(b.style, 0, 14, 0, 14);
                trow.Add(b);
            }
            tabs.Add(trow);
            root.Add(tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 12, 16, 16, 16));

            // ---------- colours ----------
            string ck = AvatarParts.ColorFor(tab);
            if (ck != null)
            {
                var crow = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.marginBottom = 10; });
                var pal = AvatarParts.Palette(ck);
                for (int i = 0; i < pal.Length; i++)
                {
                    int ci = i;
                    bool sel = face.Color(ck) == ci;
                    int price = AvatarParts.ColorPrice(ck, ci);
                    bool owned = AvatarParts.OwnsColor(save, ck, ci);
                    var sw = new Button(() => { face.colors[ck] = ci; Paint(gm); }).St(s =>
                    {
                        s.width = 44; s.height = 44; UIKit.Radius(s, 22); s.backgroundColor = Pal.H(pal[ci]);
                        UIKit.Border(s, sel ? 4f : 2f, sel ? Pal.Ink : Pal.H("#C9D1D9"));
                        s.marginRight = 8; s.marginBottom = 8; UIKit.Pad(s, 0);
                    });
                    if (price > 0 && !owned)
                        sw.Add(UIKit.Strong(price.ToString(), 10f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
                        {
                            s.position = Position.Absolute; s.bottom = -6; s.left = -2; s.right = -2; s.backgroundColor = Pal.Yellow; UIKit.Radius(s, 6);
                        }));
                    crow.Add(sw);
                }
                body.Add(crow);
            }

            // ---------- choices ----------
            var grid = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.FlexStart; });
            var options = new List<AvatarParts.Part>(AvatarParts.Of(tab));
            bool optional = AvatarParts.Optional.Contains(tab);
            if (optional) options.Insert(0, null);
            foreach (var part in options)
            {
                var p = part;
                string num = p == null ? "" : p.Num;
                bool sel = (face.parts.TryGetValue(tab, out var cur) ? cur : "") == num;
                var trial = face.Copy();
                trial.parts[tab] = num;
                bool owned = AvatarParts.Owns(save, p);
                var cell = new Button(() => { face.parts[tab] = num; Paint(gm); }).St(s =>
                {
                    s.width = Length.Percent(31); s.marginRight = Length.Percent(2); s.marginBottom = 10; s.height = 118;
                    s.backgroundColor = Pal.Paper; UIKit.Radius(s, 14); UIKit.Border(s, sel ? 4f : 2f, sel ? Pal.Ink : Pal.H("#D5DBE0"));
                    UIKit.Pad(s, 6); s.alignItems = Align.Center; s.justifyContent = Justify.FlexStart;
                });
                var thumb = UIKit.El().St(s => { s.width = 80; s.height = 80; UIKit.Radius(s, 40); s.backgroundColor = Pal.H("#EEF2F0"); });
                Faces.Fill(thumb, trial.ToString());
                cell.Add(thumb);
                string label = p == null ? "NONE" : p.Free ? "FREE" : owned ? "YOURS" : null;
                if (label != null)
                    cell.Add(UIKit.Strong(label, 12f, sel ? Pal.Ink : Pal.InkSoft, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
                else
                {
                    var price = UIKit.Row().St(s => { s.marginTop = 4; s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 2, 8, 2, 6); UIKit.Radius(s, 8); });
                    price.Add(new CoinIcon(14f));
                    price.Add(UIKit.Strong(Desks.Money(p.price), 12f, Pal.Ink).St(s => s.marginLeft = 4));
                    cell.Add(price);
                }
                grid.Add(cell);
            }
            body.Add(grid);
            scroll.Add(body);
            root.Add(scroll);

            // ---------- save ----------
            var foot = UIKit.Col().St(s => { UIKit.Pad(s, 8, 16, 22, 16); s.flexShrink = 0; });
            int cost = Cost(save, face, null);
            var saveBtn = UIKit.PrimaryBtn(cost > 0 ? "BUY & SAVE · " + Desks.Money(cost) : "SAVE MY FACE", () => DoSave(gm), 62f);
            saveBtn.style.fontSize = 24;
            foot.Add(saveBtn);
            root.Add(foot);

            gm.UI.ShowScreen(root);
        }

        static void DoSave(GameManager gm)
        {
            var save = gm.Save;
            var items = new List<string>();
            int cost = Cost(save, face, items);
            if (cost > save.marks)
            {
                gm.UI.ShowOverlay(OnlineScreens.Error(gm, "NOT ENOUGH MARKS",
                    "this face costs " + Desks.Money(cost) + " marks. you have " + Desks.Money(save.marks) + ". win a few matches, or swap the locked parts for free ones."));
                return;
            }
            if (cost > 0)
            {
                var o = OnlineScreens.Sheet(out var c);
                c.Add(UIKit.Disp("BUY & SAVE?", 28f, Pal.Ink));
                var line = UIKit.Row().St(s => s.marginTop = 8);
                line.Add(new CoinIcon(22f));
                line.Add(UIKit.Disp(Desks.Money(cost), 26f, Pal.Ink).St(s => s.marginLeft = 6));
                line.Add(UIKit.Strong("  for " + items.Count + (items.Count == 1 ? " new item" : " new items"), 15f, Pal.InkSoft));
                c.Add(line);
                var buy = UIKit.PrimaryBtn("BUY & SAVE", () =>
                {
                    save.marks -= cost;
                    foreach (var it in items) if (!save.faceOwned.Contains(it)) save.faceOwned.Add(it);
                    gm.UI.HideOverlay();
                    Commit(gm);
                }, 56f);
                buy.style.marginTop = 16;
                c.Add(buy);
                var no = UIKit.Btn("Not now", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 16f, 48f);
                no.style.marginTop = 6;
                c.Add(no);
                gm.UI.ShowOverlay(o);
                return;
            }
            Commit(gm);
        }

        static void Commit(GameManager gm)
        {
            gm.Save.face = face.ToString();
            gm.Save.faceIntroSeen = true;
            gm.Save.Save();
            ProfileCloud.Publish(gm.Save);
            gm.Audio.Ui();
            Haptics.Pulse(20, 180);
            back();
        }

        // ---------- first time on the home screen ----------

        /// <summary>Once per player: "this is you! make your face" with their random face.</summary>
        public static void MaybeIntro(GameManager gm)
        {
            var save = gm.Save;
            if (save.faceIntroSeen || !AvatarParts.Ready) return;
            save.faceIntroSeen = true;
            save.Save();
            var o = OnlineScreens.Sheet(out var c);
            c.style.alignItems = Align.Center;
            var big = UIKit.El().St(s => { s.width = 120; s.height = 120; UIKit.Radius(s, 60); UIKit.Border(s, 5f, Pal.Ink); s.backgroundColor = Pal.Paper; });
            UIKit.Rot(big, -3f);
            Faces.Fill(big, AvatarParts.MyCode(save));
            c.Add(big);
            c.Add(UIKit.Disp("THIS IS YOU!", 30f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            c.Add(UIKit.HandText("we drew you a face. make it look like you?", 18f, Pal.Ink, TextAnchor.MiddleCenter));
            var make = UIKit.PrimaryBtn("MAKE MY FACE", () => { gm.UI.HideOverlay(); Show(gm); }, 56f);
            make.style.marginTop = 16; make.style.alignSelf = Align.Stretch;
            c.Add(make);
            var later = UIKit.Btn("Later", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 16f, 48f);
            later.style.marginTop = 6; later.style.alignSelf = Align.Stretch;
            c.Add(later);
            gm.UI.ShowOverlay(o);
        }
    }
}
