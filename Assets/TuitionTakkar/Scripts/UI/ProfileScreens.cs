using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Player profile (mockups P1–P3, B1–B2): my profile with stats, desks, badges and recent
    /// matches; the opponent card in a match; all badges; the "new badge" screen.
    /// Full profiles are seen only by their owner and by the players in the same match.
    /// </summary>
    public static class ProfileScreens
    {
        static readonly Color Green = Pal.H("#1E6B4F"), Red = Pal.H("#B23A26"), Purple = Pal.H("#5B2A86");
        static readonly Color Soft = Pal.H("#5B6B78");

        // =====================================================================
        // pieces
        // =====================================================================

        public static PenDef ShowcasePen(SaveData s)
        {
            if (!string.IsNullOrEmpty(s.stats.showcasePen) && s.Owns(s.stats.showcasePen)) return Catalog.Pen(s.stats.showcasePen);
            PenDef best = Catalog.Pen(s.equipped);
            foreach (var id in s.owned)
            {
                var p = Catalog.Pen(id);
                if (p != null && p.rarity > best.rarity) best = p;
            }
            return best;
        }

        static int profileTab;

        static VisualElement Section(string title, float mt = 14f)
        {
            var c = UIKit.Col().St(s =>
            {
                s.marginTop = mt; s.marginLeft = 16; s.marginRight = 16; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16);
                UIKit.Pad(s, 12, 14, 14, 14); s.borderBottomWidth = 4; s.borderBottomColor = new Color(0.12f, 0.16f, 0.27f, 0.1f);
            });
            c.Add(UIKit.Disp(title, 17f, Pal.Ink).St(s => s.letterSpacing = 0.4f));
            return c;
        }

        static VisualElement Grid(params (string label, string value, Color bg, Color fg)[] cells) => GridN(3, cells);

        static VisualElement GridN(int cols, params (string label, string value, Color bg, Color fg)[] cells)
        {
            var g = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; s.marginTop = 4; });
            float w = cols == 3 ? 31.8f : 48.5f;
            foreach (var (label, value, bg, fg) in cells)
            {
                var cell = UIKit.Col().St(s =>
                {
                    s.width = Length.Percent(w); s.marginTop = 8; s.backgroundColor = bg; UIKit.Radius(s, 12); UIKit.Pad(s, 8, 4, 8, 4);
                    s.alignItems = Align.Center;
                });
                cell.Add(UIKit.Disp(value, value.Length > 5 ? 19f : 24f, fg, TextAnchor.MiddleCenter));
                cell.Add(UIKit.Strong(label, 11f, Pal.InkSoft, TextAnchor.MiddleCenter).St(s => { s.letterSpacing = 0.2f; s.marginTop = 2; s.whiteSpace = WhiteSpace.Normal; }));
                g.Add(cell);
            }
            return g;
        }

        static string N(int v) => Desks.Money(v);

        static VisualElement Chip(string text, Color bg, Color fg, float size = 11f)
        {
            return UIKit.Strong(text, size, fg, TextAnchor.MiddleCenter).St(s =>
            {
                s.backgroundColor = bg; UIKit.Pad(s, 2, 8, 2, 8); UIKit.Radius(s, 9); s.flexShrink = 0;
            });
        }

        /// <summary>The level number on a small blue disc at the avatar's corner.</summary>
        static VisualElement AvatarWithLevel(string initial, Color bg, float size, int level, Color edge)
        {
            var holder = UIKit.El().St(s => { s.width = size; s.height = size; s.flexShrink = 0; });
            holder.Add(UIKit.Avatar(initial, bg, size));
            if (level > 0)
            {
                float d = Mathf.Round(size * 0.4f);
                var lv = UIKit.El().St(s =>
                {
                    s.position = Position.Absolute; s.right = -6; s.bottom = -4; s.width = d; s.height = d; UIKit.Radius(s, d * 0.5f);
                    s.backgroundColor = Pal.Pouch; UIKit.Border(s, 3f, edge); s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                });
                lv.Add(UIKit.Disp(level.ToString(), d * 0.46f, Color.white, TextAnchor.MiddleCenter));
                holder.Add(lv);
            }
            return holder;
        }

        public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        // ---------- medals (Painter2D) ----------

        static readonly Dictionary<string, float[][]> IconLines = new Dictionary<string, float[][]>
        {
            ["trophy"] = new[] { new float[] { 8, 4, 16, 4, 16, 9, 14, 12, 10, 12, 8, 9, 8, 4 }, new float[] { 8, 5, 5, 5, 5, 7, 8, 10 }, new float[] { 16, 5, 19, 5, 19, 7, 16, 10 }, new float[] { 12, 12, 12, 17 }, new float[] { 9, 20, 15, 20 }, new float[] { 10, 17, 14, 17 } },
            ["drop"] = new[] { new float[] { 4, 7, 15, 7, 18, 10 }, new float[] { 18, 13, 18, 19 }, new float[] { 15.5f, 16.5f, 18, 19, 20.5f, 16.5f } },
            ["flame"] = new[] { new float[] { 12, 3, 13, 6, 16, 9, 16, 13, 14.8f, 15.8f, 12, 17, 9.2f, 15.8f, 8, 13, 8.5f, 10.5f, 10, 9, 10.5f, 11.5f, 12, 12, 11.5f, 8, 12, 3 } },
            ["shield"] = new[] { new float[] { 12, 3, 19, 6, 19, 11, 17, 16, 12, 20, 7, 16, 5, 11, 5, 6, 12, 3 }, new float[] { 9, 12, 11, 14, 15, 10 } },
            ["arrow"] = new[] { new float[] { 4, 18, 10, 12, 14, 16, 20, 8 }, new float[] { 15, 8, 20, 8, 20, 13 } },
            ["crown"] = new[] { new float[] { 4, 17, 3, 8, 8, 12, 12, 6, 16, 12, 21, 8, 20, 17, 4, 17 }, new float[] { 4, 20, 20, 20 } },
            ["coin"] = new[] { new float[] { 12, 8, 12, 16 }, new float[] { 14.5f, 10, 10.5f, 10, 9.5f, 11.5f, 10.5f, 13, 13.5f, 13, 14.5f, 14.5f, 13.5f, 16, 9.5f, 16 } },
            ["stack"] = new[] { new float[] { 6, 5, 18, 5, 18, 9, 6, 9, 6, 5 }, new float[] { 6, 10, 18, 10, 18, 14, 6, 14, 6, 10 }, new float[] { 6, 15, 18, 15, 18, 19, 6, 19, 6, 15 } },
            ["compass"] = new[] { new float[] { 15, 9, 13, 14, 9, 15, 11, 10, 15, 9 } },
            ["gem"] = new[] { new float[] { 6, 4, 18, 4, 21, 9, 12, 20, 3, 9, 6, 4 }, new float[] { 3, 9, 21, 9 }, new float[] { 9, 4, 12, 20, 15, 4 } },
            ["skull"] = new[] { new float[] { 5, 14, 5, 10, 7, 6, 12, 4, 17, 6, 19, 10, 19, 14, 17, 15, 17, 18, 7, 18, 7, 15, 5, 14 } },
            ["bag"] = new[] { new float[] { 5, 9, 19, 9, 18, 20, 6, 20, 5, 9 }, new float[] { 9, 9, 9, 7, 10, 5, 12, 4, 14, 5, 15, 7, 15, 9 } },
            ["wrench"] = new[] { new float[] { 4, 20, 13, 11 } },
            ["bolt"] = new[] { new float[] { 13, 3, 5, 14, 11, 14, 10, 21, 18, 10, 12, 10, 13, 3 } },
            ["party"] = new[] { new float[] { 3, 20, 4, 17, 8, 15, 12, 17, 13, 20 }, new float[] { 11, 20, 12, 17, 16, 15, 20, 17, 21, 20 } },
            ["heart"] = new[] { new float[] { 12, 20, 6, 15, 4, 10, 6, 6, 9, 5.5f, 12, 8, 15, 5.5f, 18, 6, 20, 10, 18, 15, 12, 20 } },
            ["door"] = new[] { new float[] { 6, 21, 6, 4, 16, 4, 16, 21 }, new float[] { 3, 21, 21, 21 } },
            ["cal"] = new[] { new float[] { 4, 5, 20, 5, 20, 20, 4, 20, 4, 5 }, new float[] { 4, 10, 20, 10 }, new float[] { 9, 3, 9, 7 }, new float[] { 15, 3, 15, 7 } },
            ["star"] = new[] { new float[] { 12, 3, 14.6f, 8.6f, 20.6f, 9.3f, 16.1f, 13.4f, 17.3f, 19.4f, 12, 16.4f, 6.7f, 19.4f, 7.9f, 13.4f, 3.4f, 9.3f, 9.4f, 8.6f, 12, 3 } },
            ["sword"] = new[] { new float[] { 4, 20, 9, 15 }, new float[] { 7, 13, 11, 17 }, new float[] { 14, 4, 20, 4, 20, 10, 11, 19, 5, 13, 14, 4 } },
        };

        static readonly Dictionary<string, float[][]> IconCircles = new Dictionary<string, float[][]>
        {
            ["coin"] = new[] { new float[] { 12, 12, 7 } },
            ["compass"] = new[] { new float[] { 12, 12, 8 } },
            ["skull"] = new[] { new float[] { 9.5f, 11, 1.5f }, new float[] { 14.5f, 11, 1.5f } },
            ["wrench"] = new[] { new float[] { 16, 8, 4 } },
            ["party"] = new[] { new float[] { 8, 9, 3 }, new float[] { 16, 9, 3 } },
        };

        /// <summary>A round medal with two ribbon tails and the badge's icon. tier 0 = locked (grey).</summary>
        public static VisualElement Medal(string icon, int tier, float size)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = size; e.style.height = size * 1.18f; e.style.flexShrink = 0;
            Color a = Badges.MetalLight(tier), b = Badges.Metal(tier), c = Badges.MetalDark(tier);
            Color r1 = tier > 0 ? Pal.H("#C8242E") : Pal.H("#B9C2BF"), r2 = tier > 0 ? Pal.H("#1F4FA8") : Pal.H("#A4AEAB");
            e.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                float k = size / 48f;
                Vector2 P(float x, float y) => new Vector2(x * k, y * k);
                void Poly(Color col, params float[] xy)
                {
                    p.fillColor = col;
                    p.BeginPath();
                    p.MoveTo(P(xy[0], xy[1]));
                    for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                    p.ClosePath();
                    p.Fill();
                }
                Poly(r1, 14, 34, 10, 55, 18, 50, 22, 56, 24, 38);
                Poly(r2, 34, 34, 38, 55, 30, 50, 26, 56, 24, 38);
                p.fillColor = b; p.BeginPath(); p.Arc(P(24, 22), 20 * k, 0, 360); p.Fill();
                p.fillColor = a; p.BeginPath(); p.Arc(P(24, 22), 17 * k, 0, 360); p.Fill();
                p.strokeColor = c; p.lineWidth = 1.2f * k; p.BeginPath(); p.Arc(P(24, 22), 15.5f * k, 0, 360); p.Stroke();
                // Icon on a 24 grid, placed at (12, 10) on the 48 grid.
                p.strokeColor = c; p.lineWidth = 2.2f * k; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                Vector2 I(float x, float y) => P(12 + x, 10 + y);
                if (IconLines.TryGetValue(icon, out var lines))
                    foreach (var l in lines)
                    {
                        p.BeginPath();
                        p.MoveTo(I(l[0], l[1]));
                        for (int i = 2; i + 1 < l.Length; i += 2) p.LineTo(I(l[i], l[i + 1]));
                        p.Stroke();
                    }
                if (IconCircles.TryGetValue(icon, out var circles))
                    foreach (var cc in circles)
                    {
                        p.BeginPath();
                        p.Arc(I(cc[0], cc[1]), cc[2] * k, 0, 360);
                        p.Stroke();
                    }
            };
            return e;
        }

        // =====================================================================
        // P1 my profile
        // =====================================================================

        public static void ShowProfile(GameManager gm, Action back = null)
        {
            if (!Friends.Allowed(gm.Save)) { if (back != null) back(); else OnlineScreens.ShowPlay(gm); return; } // guests have no profile
            var save = gm.Save;
            var st = save.stats;
            back ??= () => OnlineScreens.ShowPlay(gm);
            Badges.Check(save);
            ShowPendingBadges(gm, 600);
            var root = OnlineScreens.Screen();
            bool guest = !OnlineServices.OnLeaderboard(save);
            root.Add(OnlineScreens.Header("MY PROFILE", guest ? "playing as guest · saved on this phone" : "only you can see all of this", back));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var body = UIKit.Col().St(s => s.paddingBottom = 26);

            // ----- header card -----
            int lv = OnlineLevel.Level(save.onlineXp);
            var (into, need) = OnlineLevel.Progress(save.onlineXp);
            var hdr = UIKit.Col().St(s =>
            {
                s.marginTop = 14; s.marginLeft = 16; s.marginRight = 16; s.backgroundColor = Pal.Night; UIKit.Radius(s, 18); UIKit.Pad(s, 16);
            });
            var top = UIKit.Row();
            string name = OnlineServices.DisplayName(save);
            top.Add(AvatarWithLevel(Initial(name), Pal.Yellow, 76f, lv, Pal.Night));
            var who = UIKit.Col().St(s => { s.marginLeft = 14; s.flexShrink = 1; });
            who.Add(UIKit.Disp(name.ToUpperInvariant(), 26f, Color.white));
            string title = Badges.Title(save);
            if (!string.IsNullOrEmpty(title)) who.Add(UIKit.Strong(title, 12f, Pal.Yellow).St(s => s.marginTop = 3));
            if (!string.IsNullOrEmpty(save.friendId))
                who.Add(IdChip(save.friendId, true).St(s => { s.marginTop = 6; s.alignSelf = Align.FlexStart; }));
            who.Add(UIKit.Strong((guest ? "Guest" : "Google") + (string.IsNullOrEmpty(st.since) ? "" : " · member since " + st.since), 12f, Pal.Mist).St(s => s.marginTop = 6));
            top.Add(who);
            hdr.Add(top);
            var lvRow = UIKit.Row().St(s => s.marginTop = 14);
            lvRow.Add(UIKit.Strong("LEVEL " + lv, 12f, Pal.Mist));
            var track = UIKit.El().St(s => { s.flexGrow = 1; s.height = 10; s.marginLeft = 10; s.marginRight = 10; UIKit.Radius(s, 5); s.backgroundColor = new Color(1, 1, 1, 0.15f); s.overflow = Overflow.Hidden; });
            track.Add(UIKit.El().St(s => { s.height = 10; s.width = Length.Percent(100f * into / Mathf.Max(1, need)); s.backgroundColor = Pal.H("#2EB872"); }));
            lvRow.Add(track);
            lvRow.Add(UIKit.Strong(into + " / " + need + " XP", 12f, Pal.Mist));
            hdr.Add(lvRow);
            var pen = ShowcasePen(save);
            var penRow = UIKit.Row().St(s =>
            {
                s.marginTop = 14; s.justifyContent = Justify.SpaceBetween; UIKit.Radius(s, 12); UIKit.Pad(s, 8, 12, 8, 12);
                s.backgroundColor = new Color(1, 1, 1, 0.07f); UIKit.Border(s, 1f, new Color(1, 1, 1, 0.25f));
            });
            var pc = UIKit.Col().St(s => s.flexShrink = 1);
            pc.Add(UIKit.Strong("SHOWCASE PEN · tap to change", 10f, Pal.Mist).St(s => s.letterSpacing = 0.6f));
            pc.Add(UIKit.Disp(pen.name + " · " + PenParts.RarityName(pen.rarity).ToLowerInvariant(), 15f, Color.white));
            penRow.Add(pc);
            penRow.Add(PenIcon.Build(pen, 140f));
            penRow.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.UI.ShowOverlay(PickShowcase(gm, () => ShowProfile(gm, back))); });
            hdr.Add(penRow);
            body.Add(hdr);

            // ----- tabs: Stats / Badges / Matches (less scrolling) -----
            var tabs = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginLeft = 16; s.marginRight = 16; s.marginTop = 14; s.backgroundColor = Pal.Night; UIKit.Radius(s, 14); UIKit.Pad(s, 4);
            });
            string[] tabNames = { "STATS", "BADGES · " + st.BadgesEarned, "MATCHES" };
            for (int t = 0; t < 3; t++)
            {
                int ti = t;
                bool sel = profileTab == t;
                var tb = UIKit.Btn(tabNames[t], sel ? null : () => { profileTab = ti; ShowProfile(gm, back); },
                    sel ? Pal.Yellow : Color.clear, sel ? Pal.Ink : Pal.MistLight, UIKit.Display, 15f, 42f, 10f);
                tb.style.flexGrow = 1; tb.style.flexBasis = 0;
                tabs.Add(tb);
            }
            body.Add(tabs);

            if (profileTab == 0)
            {
                // the three numbers that matter, big
                var hi = UIKit.Row(Align.Stretch).St(s => { s.marginLeft = 16; s.marginRight = 16; s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; });
                foreach (var (label, value, fg) in new[] { ("WINS", N(st.won), Green), ("WIN RATE", st.WinRate + "%", Pal.Ink), ("BEST STREAK", st.bestStreak.ToString(), Pal.H("#C25A10")) })
                {
                    var b = UIKit.Col().St(s =>
                    {
                        s.width = Length.Percent(31.8f); s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16); UIKit.Pad(s, 10, 4, 10, 4); s.alignItems = Align.Center;
                        s.borderBottomWidth = 4; s.borderBottomColor = new Color(0.12f, 0.16f, 0.27f, 0.1f);
                    });
                    b.Add(UIKit.Disp(value, 32f, fg, TextAnchor.MiddleCenter));
                    b.Add(UIKit.Strong(label, 12f, Pal.InkSoft, TextAnchor.MiddleCenter));
                    hi.Add(b);
                }
                body.Add(hi);
            }

            // ----- stats -----
            var online = Section("ONLINE MATCHES");
            online.Add(Grid(("PLAYED", N(st.played), Pal.H("#EEF2F6"), Pal.Ink), ("WON", N(st.won), Pal.H("#DFF0E5"), Green), ("WIN %", st.WinRate + "%", Pal.H("#FFF4C2"), Pal.Ink),
                            ("LOST", N(st.lost), Pal.H("#F9E1DC"), Red), ("STREAK", st.streak.ToString(), Pal.H("#FFE8D6"), Pal.H("#C25A10")), ("BEST STREAK", st.bestStreak.ToString(), Pal.H("#FFE8D6"), Pal.H("#C25A10"))));
            if (profileTab == 0) body.Add(online);

            var fights = Section("PEN FIGHTS");
            fights.Add(Grid(("PENS KNOCKED OFF", N(st.knockOffs), Pal.H("#DFF0E5"), Green), ("MY PEN FELL", N(st.ownFalls), Pal.H("#F9E1DC"), Red), ("KNOCK-OFF RATIO", st.KnockRatio.ToString("0.0"), Pal.H("#EEF2F6"), Pal.Ink),
                            ("FLAWLESS WINS", N(st.flawless), Pal.H("#E6E0F5"), Purple), ("COMEBACKS", N(st.comebacks), Pal.H("#E6E0F5"), Purple), ("ONE-FLICK ROUNDS", N(st.oneFlick), Pal.H("#E6E0F5"), Purple)));
            if (profileTab == 0) body.Add(fights);

            var party = Section("3–4 PLAYER MATCHES");
            party.Add(Grid(("PLAYED", N(st.partyPlayed), Pal.H("#EEF2F6"), Pal.Ink), ("1ST PLACE", N(st.party1st), Pal.H("#FFF1C2"), Pal.H("#8A6410")), ("2ND PLACE", N(st.party2nd), Pal.H("#E9EDF1"), Pal.H("#5F6B78"))));
            if (profileTab == 0) body.Add(party);

            var money = Section("MARKS");
            money.Add(GridN(2, ("WON FROM POTS", N(st.potWon), Pal.H("#DFF0E5"), Green), ("BIGGEST POT", N(st.biggestPot), Pal.H("#FFF1C2"), Pal.H("#8A6410"))));
            var hd = UIKit.Row().St(s => { s.marginTop = 8; s.justifyContent = Justify.SpaceBetween; s.backgroundColor = Pal.H("#EEF2F6"); UIKit.Radius(s, 12); UIKit.Pad(s, 8, 12, 8, 12); });
            hd.Add(UIKit.Strong("HIGHEST DESK WON", 11f, Pal.InkSoft));
            if (st.highestDesk >= 0)
            {
                var d = Desks.All[st.highestDesk];
                var r = UIKit.Row();
                r.Add(Chip(Desks.TierName(d.tier), d.Trim ?? Pal.H("#E9E2D0"), Pal.Ink, 10f));
                r.Add(UIKit.Disp(d.name, 15f, Pal.Ink).St(s => s.marginLeft = 6));
                hd.Add(r);
            }
            else hd.Add(UIKit.Strong("win a match to set it", 12f, Soft));
            money.Add(hd);
            if (profileTab == 0) body.Add(money);

            var desks = Section("WINS BY DESK");
            int most = 1;
            foreach (var w in st.tierWins) most = Mathf.Max(most, w);
            for (int t = 0; t < 5; t++)
            {
                var tier = (DeskTier)t;
                int w = st.tierWins[t];
                var row = UIKit.Row().St(s => s.marginTop = 7);
                row.Add(UIKit.Disp(Desks.TierName(tier), 11f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
                {
                    s.width = 78; UIKit.Pad(s, 3, 0, 3, 0); UIKit.Radius(s, 6); s.letterSpacing = 0.8f;
                    s.backgroundColor = t == 0 ? Pal.H("#E9E2D0") : Color.Lerp(Desks.TierColour(tier), Color.white, 0.35f);
                }));
                var bar = UIKit.El().St(s => { s.flexGrow = 1; s.height = 12; s.marginLeft = 8; s.marginRight = 8; UIKit.Radius(s, 6); s.backgroundColor = Pal.H("#EEF2F6"); s.overflow = Overflow.Hidden; });
                bar.Add(UIKit.El().St(s => { s.height = 12; s.width = Length.Percent(100f * w / most); s.backgroundColor = Desks.TierColour(tier); }));
                row.Add(bar);
                row.Add(UIKit.Disp(w.ToString(), 15f, w > 0 ? Pal.Ink : Pal.H("#9AA5B1"), TextAnchor.MiddleRight).St(s => s.width = 30));
                desks.Add(row);
            }
            string fav = st.FavouriteDesk(out int favPlays);
            if (!string.IsNullOrEmpty(fav))
                desks.Add(UIKit.Strong("favourite desk: " + (Desks.ById(fav)?.name ?? fav) + " · " + favPlays + " matches", 12f, Pal.InkSoft).St(s => s.marginTop = 8));
            if (profileTab == 0) body.Add(desks);

            // ----- badges strip -----
            var bsec = Section("BADGES");
            var all = UIKit.Strong(st.BadgesEarned + " of 60 · all ›", 13f, Green).St(s => { s.position = Position.Absolute; s.right = 14; s.top = 13; });
            bsec.Add(all);
            bsec.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); ShowBadges(gm, () => ShowProfile(gm, back)); });
            var brow = UIKit.Row().St(s => { s.marginTop = 10; s.justifyContent = Justify.SpaceBetween; });
            var order = new List<int>();
            for (int i = 0; i < Badges.Count; i++) order.Add(i);
            order.Sort((x, y) => st.badgeTier[y].CompareTo(st.badgeTier[x]));
            for (int k = 0; k < 6; k++)
            {
                int i = order[k];
                var cell = UIKit.Col().St(s => { s.width = 52; s.alignItems = Align.Center; });
                cell.Add(Medal(Badges.All[i].icon, st.badgeTier[i], 42f));
                cell.Add(UIKit.Strong(Badges.All[i].name, 8.5f, Pal.InkSoft, TextAnchor.MiddleCenter).St(s => s.marginTop = 2));
                brow.Add(cell);
            }
            bsec.Add(brow);
            if (profileTab == 1)
            {
                // all badges right here, big and readable
                bsec.Clear();
                bsec.Add(UIKit.Disp("BADGES", 17f, Pal.Ink));
                bsec.Add(UIKit.Strong(st.BadgesEarned + " of 60 earned · tap one to show it as your title", 13f, Pal.InkSoft).St(s => s.whiteSpace = WhiteSpace.Normal));
                var gridB = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.SpaceBetween; s.marginTop = 6; });
                foreach (int i in order)
                {
                    var cell = UIKit.Col().St(s => { s.width = Length.Percent(31.8f); s.alignItems = Align.Center; s.marginTop = 12; });
                    cell.Add(Medal(Badges.All[i].icon, st.badgeTier[i], 58f));
                    cell.Add(UIKit.Strong(Badges.All[i].name, 11f, st.badgeTier[i] > 0 ? Pal.Ink : Soft, TextAnchor.MiddleCenter).St(s => { s.marginTop = 4; s.whiteSpace = WhiteSpace.Normal; }));
                    gridB.Add(cell);
                }
                bsec.Add(gridB);
                var allB = UIKit.Btn("BADGE DETAILS", () => ShowBadges(gm, () => ShowProfile(gm, back)), Pal.Yellow, Pal.Ink, UIKit.Display, 18f, 50f, 12f, Pal.H("#C7A928"));
                allB.style.marginTop = 14;
                bsec.Add(allB);
                body.Add(bsec);
            }

            var ladder = Section("BATCH LADDER & PENS");
            ladder.Add(Grid(("BATCH", (Ladder.BatchOf(save.ladderIndex) + 1).ToString(), Pal.H("#EEF2F6"), Pal.Ink), ("BOSSES BEATEN", N(st.bosses), Pal.H("#F9E1DC"), Red),
                            ("PENS OWNED", save.owned.Count + "/600", Pal.H("#EEF2F6"), Pal.Ink), ("PENS MADE", Badges.PensMade(save).ToString(), Pal.H("#E6E0F5"), Purple),
                            ("RAREST", RarestName(save), Pal.H("#FFF1C2"), Pal.H("#8A6410")), ("DAYS PLAYED", N(st.daysPlayed), Pal.H("#DFF0E5"), Green)));
            if (profileTab == 0) body.Add(ladder);

            // ----- recent matches -----
            var rec = Section("RECENT MATCHES");
            if (st.recent.Count == 0)
                rec.Add(UIKit.HandText("no online matches yet. go flick someone!", 16f, Pal.InkSoft).St(s => s.marginTop = 8));
            for (int k = 0; k < st.recent.Count; k++)
            {
                var m = st.recent[k];
                var row = UIKit.Row().St(s =>
                {
                    UIKit.Pad(s, 8, 0, 8, 0);
                    if (k > 0) { s.borderTopWidth = 1; s.borderTopColor = Pal.H("#D5DDE4"); }
                });
                row.Add(UIKit.Disp(m.won ? "W" : "L", 15f, Color.white, TextAnchor.MiddleCenter).St(s =>
                {
                    s.width = 26; s.height = 26; UIKit.Radius(s, 8); s.backgroundColor = m.won ? Green : Red; s.flexShrink = 0;
                }));
                var c = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; s.marginLeft = 10; });
                c.Add(UIKit.Strong("vs " + m.vs + " · " + m.score, 14f, Pal.Ink));
                c.Add(UIKit.Strong(m.desk + " · " + StatsRecorder.Ago(m.at), 11f, Soft));
                row.Add(c);
                string money2 = m.free ? "free" : m.net >= 0 ? "+" + N(m.net) : "-" + N(-m.net);
                row.Add(UIKit.Disp(money2, 15f, m.free ? Soft : m.net >= 0 ? Green : Red));
                rec.Add(row);
            }
            rec.Add(UIKit.Strong("only you can see this list", 12f, Soft).St(s => s.marginTop = 6));
            if (profileTab == 2) body.Add(rec);

            scroll.Add(body);
            root.Add(scroll);
            gm.UI.ShowScreen(root);
        }

        static string RarestName(SaveData s)
        {
            var r = PenRarity.Common;
            foreach (var id in s.owned) { var p = Catalog.Pen(id); if (p != null && p.rarity > r) r = p.rarity; }
            return PenParts.RarityName(r).ToUpperInvariant();
        }

        public static VisualElement IdChip(string code, bool dark)
        {
            var chip = UIKit.Row().St(s =>
            {
                UIKit.Radius(s, 10); UIKit.Pad(s, 4, 10, 4, 10);
                s.backgroundColor = dark ? Pal.NightSoft : Pal.H("#F3E7C2");
            });
            chip.Add(UIKit.Strong(code, 14f, dark ? Pal.Yellow : Pal.Ink).St(s => s.letterSpacing = 1.5f));
            chip.Add(UIKit.Strong("copy", 11f, dark ? Pal.Mist : Pal.H("#6B5216")).St(s => s.marginLeft = 8));
            chip.RegisterCallback<ClickEvent>(e =>
            {
                GUIUtility.systemCopyBuffer = code;
                GameManager.I?.Audio.Ui();
                if (chip.childCount > 1) ((Label)chip[1]).text = "copied!";
                e.StopPropagation();
            });
            return chip;
        }

        /// <summary>Choose which owned pen others see on your profile.</summary>
        static VisualElement PickShowcase(GameManager gm, Action done)
        {
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp("SHOWCASE PEN", 26f, Pal.Ink));
            c.Add(UIKit.HandText("the pen players see on your profile.", 16f, Pal.InkSoft));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 420; scroll.style.marginTop = 10;
            var pens = new List<PenDef>();
            foreach (var id in gm.Save.owned) { var p = Catalog.Pen(id); if (p != null) pens.Add(p); }
            pens.Sort((a, b) => b.rarity.CompareTo(a.rarity));
            var cur = ShowcasePen(gm.Save);
            foreach (var p in pens)
            {
                var pp = p;
                var row = UIKit.Row().St(s =>
                {
                    s.marginTop = 6; UIKit.Pad(s, 8, 10, 8, 10); UIKit.Radius(s, 12);
                    s.backgroundColor = p.id == cur.id ? Pal.Yellow : Color.white; UIKit.Border(s, p.id == cur.id ? 2f : 1f, p.id == cur.id ? Pal.Ink : Pal.Dash);
                });
                row.Add(PenIcon.Build(p, 120f));
                var col = UIKit.Col().St(s => { s.marginLeft = 10; s.flexShrink = 1; });
                col.Add(UIKit.Strong(p.name, 14f, Pal.Ink));
                col.Add(UIKit.Strong(PenParts.RarityName(p.rarity), 11f, PenParts.RarityColour(p.rarity)));
                row.Add(col);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    gm.Audio.Ui();
                    gm.Save.stats.showcasePen = pp.id;
                    gm.Save.Save();
                    ProfileCloud.Publish(gm.Save);
                    gm.UI.HideOverlay();
                    done();
                });
                scroll.Add(row);
            }
            c.Add(scroll);
            c.Add(UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f).St(s => s.marginTop = 6));
            return o;
        }

        // =====================================================================
        // B1 all badges
        // =====================================================================

        public static void ShowBadges(GameManager gm, Action back)
        {
            var save = gm.Save;
            var st = save.stats;
            var root = OnlineScreens.Screen();
            root.Add(OnlineScreens.Header("BADGES", st.BadgesEarned + " / 60 earned", back));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 12, 16, 26, 16));

            var legend = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 14); UIKit.Pad(s, 8, 12, 8, 12); });
            for (int t = 1; t <= 3; t++)
            {
                var l = UIKit.Row();
                l.Add(Medal("star", t, 20f));
                l.Add(UIKit.Strong(Badges.TierNames[t].ToLowerInvariant() + " · +" + N(Badges.Reward[t]), 12f, Pal.InkSoft).St(s => s.marginLeft = 4));
                legend.Add(l);
            }
            body.Add(legend);
            if (st.titleBadge < 0 || st.badgeTier[st.titleBadge] == 0)
                body.Add(UIKit.Plain("tap an earned badge to show it as your title", 12f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));

            for (int i = 0; i < Badges.Count; i++)
            {
                int bi = i;
                var d = Badges.All[i];
                int t = st.badgeTier[i];
                int v = d.value(save);
                int next = Mathf.Min(t, 2);
                int goal = d.at[next];
                int from = t == 0 ? 0 : d.at[t - 1];
                float pct = t >= 3 ? 1f : Mathf.Clamp01((v - (d.labels != null ? 0 : 0)) / (float)Mathf.Max(1, goal));
                if (d.labels != null) pct = t >= 3 ? 1f : v >= goal ? 1f : (float)Mathf.Max(0, v - 2) / Mathf.Max(1, goal - 2);
                bool isTitle = st.titleBadge == i && t > 0;
                var row = UIKit.Row().St(s =>
                {
                    s.marginTop = 8; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 14); UIKit.Pad(s, 10);
                    s.borderBottomWidth = 3; s.borderBottomColor = new Color(0.12f, 0.16f, 0.27f, 0.08f);
                    if (isTitle) UIKit.Border(s, 2f, Pal.Yellow);
                });
                row.Add(Medal(d.icon, t, 46f));
                var c = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; s.marginLeft = 10; });
                var head = UIKit.Row().St(s => s.justifyContent = Justify.SpaceBetween);
                head.Add(UIKit.Disp(d.name + (isTitle ? "  ★ TITLE" : ""), 14f, t > 0 ? Pal.Ink : Pal.H("#7C8784")));
                var pips = UIKit.Row();
                for (int k = 1; k <= 3; k++)
                {
                    bool got = t >= k;
                    pips.Add(UIKit.El().St(s =>
                    {
                        s.width = 14; s.height = 14; UIKit.Radius(s, 7); s.marginLeft = 3;
                        s.backgroundColor = got ? Badges.Metal(k) : Pal.H("#E1E6EA"); UIKit.Border(s, 1.5f, got ? Badges.MetalDark(k) : Pal.H("#C9D1D9"));
                    }));
                }
                head.Add(pips);
                c.Add(head);
                c.Add(UIKit.Strong(d.what, 12f, Soft));
                var bar = UIKit.Row().St(s => s.marginTop = 5);
                var tr = UIKit.El().St(s => { s.flexGrow = 1; s.height = 10; UIKit.Radius(s, 5); s.backgroundColor = Pal.H("#E1E6EA"); s.overflow = Overflow.Hidden; });
                tr.Add(UIKit.El().St(s => { s.height = 10; s.width = Length.Percent(pct * 100f); s.backgroundColor = t >= 3 ? Green : Badges.Metal(t + 1); }));
                bar.Add(tr);
                string progress = t >= 3 ? "done ✓" : Badges.Value(d, v) + "/" + Badges.Goal(d, next);
                bar.Add(UIKit.Strong(progress, 12f, Pal.InkSoft).St(s => s.marginLeft = 6));
                c.Add(bar);
                row.Add(c);
                if (t > 0)
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        gm.Audio.Ui();
                        st.titleBadge = bi;
                        save.Save();
                        ProfileCloud.Publish(save);
                        ShowBadges(gm, back);
                    });
                body.Add(row);
            }
            scroll.Add(body);
            root.Add(scroll);
            gm.UI.ShowScreen(root);
        }

        // =====================================================================
        // B2 new badge
        // =====================================================================

        /// <summary>Shows queued "new badge" screens one by one, once nothing else is on top.</summary>
        public static void ShowPendingBadges(GameManager gm, int delayMs = 1400)
        {
            if (Badges.Pending.Count == 0) return;
            gm.UI.ScreenLayer.schedule.Execute(() =>
            {
                if (Badges.Pending.Count == 0) return;
                if (gm.UI.OverlayLayer.childCount > 0) { ShowPendingBadges(gm, 800); return; }
                if (Badges.Pending.Count > 3)
                {
                    var all = new List<(int, int)>();
                    while (Badges.Pending.Count > 0) all.Add(Badges.Pending.Dequeue());
                    gm.UI.ShowOverlay(EarnedMany(gm, all));
                    return;
                }
                var (b, t) = Badges.Pending.Dequeue();
                gm.UI.ShowOverlay(Earned(gm, b, t));
            }).StartingIn(delayMs);
        }

        /// <summary>Several badges at once (e.g. the first time): one summary instead of a pop-up each.</summary>
        static VisualElement EarnedMany(GameManager gm, List<(int badge, int tier)> list)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = Pal.Night; s.alignItems = Align.Center; UIKit.Pad(s, 90, 20, 40, 20); });
            o.pickingMode = PickingMode.Position;
            o.Add(UIKit.Disp(list.Count + " NEW BADGES!", 30f, Pal.Yellow, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 1f));
            int total = 0;
            foreach (var (b, t) in list) total += Badges.Reward[t];
            var grid = UIKit.Row().St(s => { s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.Center; s.marginTop = 18; });
            var seen = new HashSet<int>();
            for (int k = list.Count - 1; k >= 0; k--)
            {
                var (b, t) = list[k];
                if (!seen.Add(b)) continue;   // show each badge once, at its highest new level
                var cell = UIKit.Col().St(s => { s.width = 76; s.alignItems = Align.Center; s.marginBottom = 10; });
                cell.Add(Medal(Badges.All[b].icon, t, 50f));
                cell.Add(UIKit.Strong(Badges.All[b].name, 9f, Pal.Mist, TextAnchor.MiddleCenter).St(s => s.marginTop = 2));
                grid.Add(cell);
            }
            o.Add(grid);
            var reward = UIKit.Row().St(s => { s.marginTop = 12; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16); UIKit.Pad(s, 12, 22, 12, 22); });
            reward.Add(new CoinIcon(26f));
            reward.Add(UIKit.Disp("+" + N(total), 30f, Pal.Ink).St(s => s.marginLeft = 8));
            reward.Add(UIKit.Strong("marks", 13f, Pal.InkSoft).St(s => s.marginLeft = 6));
            o.Add(reward);
            o.Add(UIKit.Grow());
            var collect = UIKit.PrimaryBtn("COLLECT", () => gm.UI.HideOverlay());
            collect.style.alignSelf = Align.Stretch;
            o.Add(collect);
            gm.Audio.Stamp();
            return o;
        }

        static VisualElement Earned(GameManager gm, int badge, int tier)
        {
            var d = Badges.All[badge];
            var o = UIKit.Col().St(s => { s.backgroundColor = Pal.Night; s.alignItems = Align.Center; UIKit.Pad(s, 110, 24, 40, 24); });
            o.pickingMode = PickingMode.Position;
            o.Add(UIKit.Disp("NEW BADGE!", 18f, Pal.Yellow, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 2f));
            var m = Medal(d.icon, tier, 150f);
            m.style.marginTop = 18;
            o.Add(m);
            UIRoot.PopIn(m, 120);
            o.Add(UIKit.Disp(d.name, 34f, Color.white, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            o.Add(Chip(Badges.TierNames[tier], Badges.MetalLight(tier), Pal.Ink, 12f).St(s => s.marginTop = 6));
            o.Add(UIKit.HandText(d.what + ": " + Badges.Goal(d, tier - 1) + ".", 19f, Pal.Mist, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            var reward = UIKit.Row().St(s => { s.marginTop = 22; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16); UIKit.Pad(s, 12, 22, 12, 22); });
            reward.Add(new CoinIcon(26f));
            reward.Add(UIKit.Disp("+" + N(Badges.Reward[tier]), 30f, Pal.Ink).St(s => s.marginLeft = 8));
            reward.Add(UIKit.Strong("marks", 13f, Pal.InkSoft).St(s => s.marginLeft = 6));
            o.Add(reward);
            o.Add(UIKit.Grow());
            var collect = UIKit.PrimaryBtn("COLLECT", () => { gm.UI.HideOverlay(); ShowPendingBadges(gm, 300); });
            collect.style.alignSelf = Align.Stretch;
            o.Add(collect);
            o.Add(UIKit.Btn("show it as my title", () =>
            {
                gm.Save.stats.titleBadge = badge;
                gm.Save.Save();
                ProfileCloud.Publish(gm.Save);
                gm.UI.HideOverlay();
                ShowPendingBadges(gm, 300);
            }, Color.clear, Pal.Mist, UIKit.BodyBold, 14f, 44f, 10f).St(s => s.marginTop = 6));
            gm.Audio.Stamp();
            return o;
        }

        // =====================================================================
        // P2 / P3 opponent card in a match
        // =====================================================================

        /// <summary>Tap a name on the blackboard: that player's profile (only during the match).</summary>
        public static async void ShowOpponent(GameManager gm, string playerId, string name, PenDef pen)
        {
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.HandText("opening " + name + "'s profile…", 18f, Pal.InkSoft));
            gm.UI.ShowOverlay(o);
            var (p, failed) = await ProfileCloud.TryLoadAsync(playerId);
            if (o.panel == null) return;
            c.Clear();
            if (failed)
            {
                // Couldn't reach the server: say so (don't call a signed-in player a guest).
                c.Add(UIKit.Disp(name.ToUpperInvariant(), 24f, Pal.Ink));
                c.Add(UIKit.HandText("couldn't load " + name + "'s profile. check your internet and try again.", 17f, Pal.InkSoft).St(s => { s.marginTop = 6; s.whiteSpace = WhiteSpace.Normal; }));
                var retry = UIKit.PrimaryBtn("TRY AGAIN", () => { gm.UI.HideOverlay(); ShowOpponent(gm, playerId, name, pen); }, 54f);
                retry.style.marginTop = 14;
                c.Add(retry);
                var shut = UIKit.Btn("CLOSE", () => gm.UI.HideOverlay(), Pal.Night, Color.white, UIKit.Display, 20f, 52f, 16f, null, null);
                shut.style.marginTop = 10;
                c.Add(shut);
                return;
            }
            bool guest = p == null;
            var top = UIKit.Row();
            top.Add(AvatarWithLevel(Initial(name), guest ? Pal.H("#C6E3B4") : OnlineScreens.AvatarColor(playerId), 60f, guest ? 0 : p.level, Pal.Paper));
            var who = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; s.flexGrow = 1; });
            who.Add(UIKit.Disp(name.ToUpperInvariant(), 24f, Pal.Ink));
            var chips = UIKit.Row().St(s => s.marginTop = 5);
            if (guest) chips.Add(Chip("playing as guest", Pal.H("#EEF2F6"), Pal.Ink));
            else
            {
                chips.Add(Chip("LEVEL " + p.level, Pal.Pouch, Color.white));
                if (!string.IsNullOrEmpty(p.title)) chips.Add(Chip(p.title, Pal.H("#FFF1C2"), Pal.H("#8A6410"), 10f).St(s => s.marginLeft = 6));
            }
            who.Add(chips);
            top.Add(who);
            top.Add(UIKit.Strong("in this match", 11f, Green));
            c.Add(top);

            if (guest)
            {
                var box = UIKit.Row().St(s => { s.marginTop = 14; s.backgroundColor = Pal.H("#EEF2F6"); UIKit.Radius(s, 14); UIKit.Pad(s, 14); });
                box.Add(UIKit.Strong("guests don't have an online profile yet. their stats stay on their phone until they sign in.", 14f, Pal.InkSoft).St(s => { s.flexShrink = 1; s.whiteSpace = WhiteSpace.Normal; }));
                c.Add(box);
                c.Add(UIKit.Strong("guests can't be added as friends", 12f, Soft, TextAnchor.MiddleCenter).St(s => s.marginTop = 14));
            }
            else
            {
                string tierName = p.highestTier >= 0 ? Desks.TierName((DeskTier)p.highestTier).Substring(0, Mathf.Min(4, Desks.TierName((DeskTier)p.highestTier).Length)) : "-";
                c.Add(Grid(("WON", N(p.won), Pal.H("#DFF0E5"), Green), ("WIN %", p.WinRate + "%", Pal.H("#FFF4C2"), Pal.Ink), ("BEST STREAK", p.bestStreak.ToString(), Pal.H("#FFE8D6"), Pal.H("#C25A10")),
                           ("KNOCKED OFF", N(p.knockOffs), Pal.H("#DFF0E5"), Green), ("FLAWLESS", N(p.flawless), Pal.H("#E6E0F5"), Purple), ("HIGHEST DESK", tierName, Pal.H("#EEF2F6"), Pal.Ink)));
                var showPen = !string.IsNullOrEmpty(p.pen) ? Catalog.Pen(p.pen) : pen;
                if (showPen != null)
                {
                    var pr = UIKit.Row().St(s => { s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; s.backgroundColor = Pal.H("#EEF2F6"); UIKit.Radius(s, 12); UIKit.Pad(s, 8, 12, 8, 12); });
                    pr.Add(UIKit.Strong("SHOWCASE PEN", 10f, Pal.InkSoft).St(s => s.letterSpacing = 0.6f));
                    pr.Add(PenIcon.Build(showPen, 140f));
                    c.Add(pr);
                }
                if (p.badgeIds != null && p.badgeIds.Length > 0)
                {
                    var br = UIKit.Row().St(s => { s.marginTop = 12; s.justifyContent = Justify.Center; });
                    for (int k = 0; k < p.badgeIds.Length && k < p.badgeTiers.Length; k++)
                    {
                        int bi = Mathf.Clamp(p.badgeIds[k], 0, Badges.Count - 1);
                        br.Add(Medal(Badges.All[bi].icon, p.badgeTiers[k], 40f).St(s => { s.marginLeft = 5; s.marginRight = 5; }));
                    }
                    c.Add(br);
                }
                var add = FriendsUI.AddFriendButton(gm, playerId, name);
                if (add != null) c.Add(add.St(s => s.marginTop = 14));
            }
            var close = UIKit.Btn("CLOSE", () => gm.UI.HideOverlay(), Pal.Night, Color.white, UIKit.Display, 20f, 52f, 16f, null, null);
            close.style.marginTop = 10;
            c.Add(close);
            c.Add(UIKit.Strong("only players in this match can see this", 11f, Soft, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));
        }
    }
}
