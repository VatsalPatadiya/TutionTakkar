using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Choose your desk (8 Ball Pool style tables): a swipeable carousel of the 28 desks with
    /// entry fee, pot, rules and lock state, an all-desks list by tier, and the "not enough
    /// marks" sheet (daily pocket money, a cheaper desk, or earn on the ladder).
    /// </summary>
    public static class DeskScreens
    {
        public enum Mode { Quick, Room }

        static int shown = -1;

        static int Seats(Mode mode, OnlineRoom room) => mode == Mode.Room && room != null ? room.Seats : OnlineScreens.seats;

        public static void ShowChooser(GameManager gm, Mode mode, OnlineRoom room = null, int index = -1)
        {
            var save = gm.Save;
            if (index >= 0) shown = index;
            else if (mode == Mode.Room && room != null) shown = room.Desk.index;
            else if (shown < 0) shown = (Desks.ById(save.lastDesk) ?? Desks.All[1]).index;
            shown = Mathf.Clamp(shown, 0, Desks.All.Count - 1);
            int seats = Seats(mode, room);

            var root = OnlineScreens.Screen();
            Action back = () =>
            {
                if (mode == Mode.Room && room != null && !room.Closed) OnlineScreens.ShowRoom(gm, room);
                else OnlineScreens.ShowPlay(gm);
            };
            root.Add(OnlineScreens.Header(mode == Mode.Room ? "PICK THE DESK" : "CHOOSE YOUR DESK",
                mode == Mode.Room ? "everyone in the room plays here" : seats + " players · winner takes the pot", back));
            root.Add(LevelStrip(save));

            var content = UIKit.Col().St(s => { s.flexGrow = 1; UIKit.Pad(s, 12, 14, 18, 14); });
            root.Add(content);

            void Paint()
            {
                content.Clear();
                var d = Desks.All[shown];
                var block = Desks.Check(save, d, mode == Mode.Room && room != null && !room.IsOwner);

                // Carousel: ‹ card ›, swipe left/right too.
                var row = UIKit.Row(Align.Center).St(s => s.justifyContent = Justify.SpaceBetween);
                row.Add(Arrow(false, shown > 0, () => { shown--; gm.Audio.Ui(); Paint(); }));
                var card = DeskCard(d, seats, block, save);
                card.style.flexGrow = 1;
                card.style.marginLeft = card.style.marginRight = 6;
                row.Add(card);
                row.Add(Arrow(true, shown < Desks.All.Count - 1, () => { shown++; gm.Audio.Ui(); Paint(); }));
                float downX = 0f;
                bool down = false;
                card.RegisterCallback<PointerDownEvent>(e => { downX = e.position.x; down = true; });
                card.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!down) return;
                    down = false;
                    float dx = e.position.x - downX;
                    if (dx < -40f && shown < Desks.All.Count - 1) { shown++; gm.Audio.Ui(); Paint(); }
                    else if (dx > 40f && shown > 0) { shown--; gm.Audio.Ui(); Paint(); }
                });
                content.Add(row);
                UIRoot.PopIn(card);

                // Tier dots: where this desk sits among the 28.
                var dots = UIKit.Row().St(s => { s.marginTop = 10; s.justifyContent = Justify.Center; s.flexWrap = Wrap.Wrap; });
                for (int i = 0; i < Desks.All.Count; i++)
                {
                    var di = Desks.All[i];
                    int at = i;
                    var dot = UIKit.El().St(s =>
                    {
                        s.width = i == shown ? 14 : 7; s.height = 7; UIKit.Radius(s, 4); s.marginLeft = s.marginRight = 1.5f;
                        s.backgroundColor = Desks.TierColour(di.tier);
                        s.opacity = i == shown ? 1f : Desks.Check(save, di) == Desks.Block.None ? 0.75f : 0.3f;
                    });
                    dot.RegisterCallback<ClickEvent>(_ => { shown = at; Paint(); });
                    dots.Add(dot);
                }
                content.Add(dots);
                content.Add(UIKit.Grow());

                // Main button.
                Button go;
                if (block == Desks.Block.None)
                {
                    string label = mode == Mode.Room ? "SIT EVERYONE HERE" : d.Free ? "PLAY FREE" : "PLAY · " + Desks.Money(d.fee);
                    go = UIKit.PrimaryBtn(label, () =>
                    {
                        if (mode == Mode.Room) PickForRoom(gm, room, d);
                        else
                        {
                            save.lastDesk = d.id;
                            save.Save();
                            OnlineScreens.DoQuick(gm, d);
                        }
                    }, 58f);
                }
                else if (block == Desks.Block.Marks)
                    go = UIKit.Btn("NEED " + Desks.Money(d.fee - save.marks) + " MORE MARKS", () => gm.UI.ShowOverlay(NotEnough(gm, d, mode, room)),
                        Pal.Yellow, Pal.Ink, UIKit.Display, 20f, 58f, 16f, Pal.H("#C7A928"));
                else if (block == Desks.Block.Guest)
                    go = UIKit.Btn("SIGN IN WITH GOOGLE TO SIT HERE", () => OnlineScreens.SignInFromDesk(gm), Color.white, Pal.Ink, UIKit.BodyBold, 15f, 58f, 16f, Pal.H("#C9D3DE"), Pal.Ink);
                else
                {
                    go = UIKit.Btn("OPENS AT LEVEL " + d.level, null, Pal.Locked, Color.white, UIKit.Display, 20f, 58f, 16f, Pal.LockedText);
                    go.SetEnabled(false);
                }
                content.Add(go);

                var links = UIKit.Row().St(s => { s.marginTop = 6; s.justifyContent = Justify.SpaceBetween; });
                links.Add(UIKit.Btn("All desks ›", () => ShowAll(gm, mode, room), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 44f, 10f));
                var best = Desks.BestAffordable(save);
                if (best.index != shown && mode == Mode.Quick)
                    links.Add(UIKit.Btn("Best for me: " + best.Number + " ›", () => { shown = best.index; Paint(); }, Color.clear, Pal.BenchText, UIKit.BodyBold, 13f, 44f, 10f));
                content.Add(links);
            }

            gm.UI.ShowScreen(root);
            Paint();
        }

        static async void PickForRoom(GameManager gm, OnlineRoom room, DeskDef d)
        {
            if (room == null || room.Closed) { OnlineScreens.ShowPlay(gm); return; }
            gm.UI.ShowOverlay(OnlineScreens.Busy("moving to " + d.name + "…"));
            try
            {
                await room.SetDeskAsync(d);
                gm.UI.HideOverlay();
                OnlineScreens.ShowRoom(gm, room);
            }
            catch (Exception e)
            {
                gm.UI.ShowOverlay(OnlineScreens.Error(gm, "COULDN'T CHANGE DESK", OnlineRoom.Explain(e)));
            }
        }

        static VisualElement Arrow(bool right, bool on, Action go)
        {
            var b = UIKit.Btn(right ? "›" : "‹", on ? go : null, on ? Pal.Paper : new Color(1, 1, 1, 0.3f), Pal.Ink, UIKit.Display, 28f, 54f, 27f, null, on ? Pal.Ink : (Color?)null);
            b.style.width = 40;
            b.style.flexShrink = 0;
            b.style.opacity = on ? 1f : 0.35f;
            Pad0(b);
            return b;
        }

        static void Pad0(VisualElement e) => UIKit.Pad(e.style, 0);

        /// <summary>"LEVEL 7 ▮▮▮▯ 340 / 165 XP · 1,250 marks"</summary>
        static VisualElement LevelStrip(SaveData save)
        {
            int lv = OnlineLevel.Level(save.onlineXp);
            var (into, need) = OnlineLevel.Progress(save.onlineXp);
            var strip = UIKit.Row().St(s =>
            {
                s.marginLeft = 16; s.marginRight = 16; s.marginTop = 12; s.flexShrink = 0;
                s.backgroundColor = Pal.Paper; UIKit.Radius(s, 12); UIKit.Pad(s, 8, 12, 8, 12); UIKit.Border(s, 2f, Pal.Ink);
            });
            strip.Add(UIKit.Disp("LV " + lv, 20f, Pal.Ink).St(s => s.width = 58));
            var mid = UIKit.Col().St(s => { s.flexGrow = 1; s.marginRight = 10; });
            var track = UIKit.El().St(s => { s.height = 9; UIKit.Radius(s, 5); s.backgroundColor = Pal.GraphLine; s.overflow = Overflow.Hidden; });
            track.Add(UIKit.El().St(s => { s.height = 9; s.width = Length.Percent(100f * into / Mathf.Max(1, need)); s.backgroundColor = Pal.Green; }));
            mid.Add(track);
            mid.Add(UIKit.Plain(into + " / " + need + " XP to level " + (lv + 1), 11f, Pal.InkSoft).St(s => s.marginTop = 2));
            strip.Add(mid);
            var coins = UIKit.Row();
            coins.Add(new CoinIcon(18f));
            coins.Add(UIKit.Disp(Desks.Money(save.marks), 18f, Pal.Ink).St(s => s.marginLeft = 4));
            strip.Add(coins);
            return strip;
        }

        // =====================================================================
        // the desk card
        // =====================================================================

        public static VisualElement DeskCard(DeskDef d, int seats, Desks.Block block, SaveData save)
        {
            var card = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16); UIKit.Border(s, 2f, Pal.Ink); s.overflow = Overflow.Hidden;
                s.borderBottomWidth = 5;
            });

            // The room with the desk in it, seen from above.
            var roomEl = UIKit.El().St(s =>
            {
                s.height = 250; s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                s.backgroundImage = new StyleBackground(ArtLib.Gradient(d.wall, Color.Lerp(d.floorA, d.floorB, 0.35f)));
            });
            roomEl.Add(DeskPicture(d, 0.86f));

            var chip = UIKit.Strong(d.Number + " · " + Desks.TierName(d.tier), 11f, d.Premium ? Pal.Ink : Color.white, TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.left = 10; s.top = 10; UIKit.Pad(s, 3, 9, 3, 9); UIKit.Radius(s, 8); s.letterSpacing = 1f;
                s.backgroundColor = d.Premium ? Color.Lerp(d.Trim.Value, Color.white, 0.1f) : Desks.TierColour(d.tier);
            });
            roomEl.Add(chip);
            if (d.size < 1f)
                roomEl.Add(UIKit.Strong("SMALLER DESK", 10f, Color.white, TextAnchor.MiddleCenter).St(s =>
                {
                    s.position = Position.Absolute; s.right = 10; s.top = 10; UIKit.Pad(s, 3, 8, 3, 8); UIKit.Radius(s, 8); s.backgroundColor = Pal.Red;
                }));
            if (block == Desks.Block.Level || block == Desks.Block.Guest)
            {
                var lockEl = UIKit.Col().St(s =>
                {
                    s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0; s.bottom = 0;
                    s.backgroundColor = new Color(0.06f, 0.08f, 0.1f, 0.55f); s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                });
                lockEl.Add(Padlock());
                lockEl.Add(UIKit.Disp(block == Desks.Block.Guest ? "GOOGLE PLAYERS ONLY" : "LEVEL " + d.level, 22f, Color.white, TextAnchor.MiddleCenter).St(s => s.marginTop = 8));
                if (block == Desks.Block.Level)
                    lockEl.Add(UIKit.Plain("you're level " + OnlineLevel.Level(save.onlineXp) + " · win online to level up", 12f, Pal.MistLight, TextAnchor.MiddleCenter));
                roomEl.Add(lockEl);
            }
            card.Add(roomEl);

            var info = UIKit.Col().St(s => UIKit.Pad(s, 12, 14, 14, 14));
            info.Add(UIKit.Disp(d.name.ToUpperInvariant(), 26f, Pal.Ink));
            info.Add(UIKit.HandText(d.where, 16f, Pal.InkSoft));
            var facts = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 10; s.justifyContent = Justify.SpaceBetween; });
            facts.Add(Fact("ENTRY", d.Free ? "FREE" : Desks.Money(d.fee), !d.Free, block == Desks.Block.Marks ? Pal.Red : Pal.Ink));
            string win = d.Free ? "+" + Economy.OnlineWin : seats > 2 ? Desks.Money(d.Prize(seats, 1)) : Desks.Money(d.Pot(seats));
            facts.Add(Fact(d.Free ? "WIN" : seats > 2 ? "1ST TAKES" : "WIN POT", win, true, Pal.Green));
            facts.Add(Fact("TURN", Mathf.RoundToInt(d.turnSeconds) + " s", false, Pal.Ink));
            facts.Add(Fact("MATCH", seats > 2 ? "1 round" : "best of " + d.BestOf, false, Pal.Ink));
            info.Add(facts);
            if (!d.Free && seats > 2)
                info.Add(UIKit.Plain("2nd takes " + Desks.Money(d.Prize(seats, 2)) + " · the rest get nothing", 11f, Pal.InkSoft).St(s => s.marginTop = 6));
            card.Add(info);
            return card;
        }

        static VisualElement Fact(string label, string value, bool coin, Color c)
        {
            var col = UIKit.Col().St(s =>
            {
                s.width = Length.Percent(24); s.alignItems = Align.Center; s.backgroundColor = Color.white; UIKit.Radius(s, 10);
                UIKit.Pad(s, 6, 2, 6, 2); UIKit.Border(s, 1.5f, Pal.Dash);
            });
            col.Add(UIKit.Strong(label, 9f, Pal.InkSoft, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 0.6f));
            var r = UIKit.Row().St(s => { s.marginTop = 2; s.justifyContent = Justify.Center; });
            if (coin) r.Add(new CoinIcon(13f).St(s => s.marginRight = 3));
            r.Add(UIKit.Disp(value, 16f, c, TextAnchor.MiddleCenter));
            col.Add(r);
            return col;
        }

        /// <summary>The desk drawn from above: rug, rim (with metal trim), the top's own picture, the pen groove.</summary>
        public static VisualElement DeskPicture(DeskDef d, float scale)
        {
            float w = 150f * scale * Mathf.Lerp(1f, d.size, 0.6f), h = 250f * scale * Mathf.Lerp(1f, d.size, 0.6f);
            var holder = UIKit.El().St(s => { s.width = w + 70f * scale; s.height = h + 24f * scale; s.alignItems = Align.Center; s.justifyContent = Justify.Center; });
            if (d.rug.HasValue)
                holder.Add(UIKit.El().St(s =>
                {
                    s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0; s.bottom = 0; UIKit.Radius(s, 6);
                    s.backgroundColor = d.rug.Value; UIKit.Border(s, 4f * scale, d.Trim ?? Color.Lerp(d.rug.Value, Color.white, 0.3f));
                }));
            // Soft shadow under the desk.
            holder.Add(UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.width = w; s.height = h; s.left = 35f * scale + 6f; s.top = 12f * scale + 8f;
                UIKit.Radius(s, 10); s.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            }));
            var desk = UIKit.Col().St(s =>
            {
                s.width = w; s.height = h; UIKit.Radius(s, 9); s.backgroundColor = d.edge;
                UIKit.Pad(s, 4f, 5f, 6f, 5f);
                if (d.Trim.HasValue) UIKit.Border(s, 2.5f, d.Trim.Value);
                s.borderBottomWidth = 6; s.borderBottomColor = Color.Lerp(d.edge, Color.black, 0.45f);
            });
            // Groove strip with a pencil.
            var strip = UIKit.Row().St(s => { s.height = 20f * scale; s.alignItems = Align.Center; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 0, 4, 0, 6); });
            var groove = UIKit.El().St(s => { s.width = Length.Percent(62); s.height = 6f * scale; UIKit.Radius(s, 3); s.backgroundColor = Color.Lerp(d.edge, Color.black, 0.6f); });
            groove.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 6; s.width = Length.Percent(60); s.top = -1; s.height = 5f * scale; UIKit.Radius(s, 2); s.backgroundColor = Pal.H("#F2B705"); }));
            strip.Add(groove);
            strip.Add(UIKit.El().St(s =>
            {
                float c = 13f * scale;
                s.width = c * (d.Premium ? 1.6f : 1f); s.height = c; UIKit.Radius(s, c * 0.5f);
                s.backgroundColor = d.Trim ?? Pal.H("#14100C"); UIKit.Border(s, 1.5f, d.Trim.HasValue ? Color.Lerp(d.Trim.Value, Color.black, 0.35f) : Pal.H("#9C7A3C"));
            }));
            desk.Add(strip);
            var top = UIKit.El().St(s =>
            {
                s.flexGrow = 1; UIKit.Radius(s, 3);
                s.backgroundImage = new StyleBackground(DeskArt.Preview(d));
                if (d.Trim.HasValue) UIKit.Border(s, 1.5f, Color.Lerp(d.Trim.Value, Color.white, 0.2f));
            });
            desk.Add(top);
            holder.Add(desk);
            if (d.Sparkle)
            {
                var spots = new[] { new Vector2(0.1f, 0.08f), new Vector2(0.88f, 0.2f), new Vector2(0.15f, 0.8f), new Vector2(0.85f, 0.9f) };
                foreach (var p in spots)
                {
                    var star = UIKit.El().St(s =>
                    {
                        s.position = Position.Absolute; s.left = Length.Percent(p.x * 100f); s.top = Length.Percent(p.y * 100f);
                        s.width = 10; s.height = 10; s.backgroundImage = new StyleBackground(DeskArt.Star);
                    });
                    holder.Add(star);
                }
            }
            return holder;
        }

        static VisualElement Padlock()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = 34; e.style.height = 40;
            e.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.strokeColor = Color.white;
                p.lineWidth = 4f;
                p.BeginPath();
                p.Arc(new Vector2(17, 16), 10f, new Angle(180, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
                p.LineTo(new Vector2(27, 20));
                p.MoveTo(new Vector2(7, 20));
                p.LineTo(new Vector2(7, 16));
                p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath();
                p.MoveTo(new Vector2(3, 19)); p.LineTo(new Vector2(31, 19)); p.LineTo(new Vector2(31, 39)); p.LineTo(new Vector2(3, 39)); p.ClosePath();
                p.Fill();
            };
            return e;
        }

        // =====================================================================
        // all desks
        // =====================================================================

        static void ShowAll(GameManager gm, Mode mode, OnlineRoom room)
        {
            var save = gm.Save;
            var root = OnlineScreens.Screen();
            root.Add(OnlineScreens.Header("ALL DESKS", "28 desks · bigger pots higher up", () => ShowChooser(gm, mode, room, shown)));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            var body = UIKit.Col().St(s => UIKit.Pad(s, 12, 16, 26, 16));
            int seats = Seats(mode, room);
            DeskTier? tier = null;
            foreach (var d in Desks.All)
            {
                if (tier != d.tier)
                {
                    tier = d.tier;
                    int first = d.index + 1, last = first;
                    foreach (var o in Desks.All) if (o.tier == d.tier) last = o.index + 1;
                    var head = UIKit.Row().St(s => { s.marginTop = d.index == 0 ? 4 : 18; s.marginBottom = 4; });
                    head.Add(UIKit.El().St(s => { s.width = 12; s.height = 12; UIKit.Radius(s, 6); s.backgroundColor = Desks.TierColour(d.tier); s.marginRight = 8; }));
                    head.Add(UIKit.Disp(Desks.TierName(d.tier) + " DESKS", 20f, Pal.Ink));
                    head.Add(UIKit.Plain("  D" + first + "–" + last, 12f, Pal.BenchText));
                    body.Add(head);
                }
                body.Add(ListRow(gm, d, seats, save, mode, room));
            }
            scroll.Add(body);
            root.Add(scroll);
            gm.UI.ShowScreen(root);
            // Start near the desk being looked at.
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0f, Mathf.Max(0f, shown * 74f - 120f))).StartingIn(30);
        }

        static VisualElement ListRow(GameManager gm, DeskDef d, int seats, SaveData save, Mode mode, OnlineRoom room)
        {
            var block = Desks.Check(save, d);
            bool here = d.index == shown;
            var row = UIKit.Row().St(s =>
            {
                s.marginTop = 6; UIKit.Pad(s, 8, 12, 8, 8); UIKit.Radius(s, 12); s.height = 68;
                s.backgroundColor = here ? Pal.Yellow : Pal.Paper; UIKit.Border(s, here ? 2f : 1.5f, here ? Pal.Ink : Pal.Dash);
                s.opacity = block == Desks.Block.Level || block == Desks.Block.Guest ? 0.65f : 1f;
            });
            var mini = UIKit.El().St(s =>
            {
                s.width = 34; s.height = 52; UIKit.Radius(s, 4); s.backgroundImage = new StyleBackground(DeskArt.Preview(d));
                UIKit.Border(s, 3f, d.Trim ?? d.edge); s.flexShrink = 0;
            });
            row.Add(mini);
            var col = UIKit.Col().St(s => { s.marginLeft = 10; s.flexGrow = 1; s.flexShrink = 1; });
            col.Add(UIKit.Strong(d.Number + "  " + d.name, 15f, Pal.Ink));
            string rules = Mathf.RoundToInt(d.turnSeconds) + " s turns · best of " + d.BestOf + (d.size < 1f ? " · smaller desk" : "");
            col.Add(UIKit.Plain(rules, 11f, Pal.InkSoft));
            row.Add(col);
            var right = UIKit.Col().St(s => { s.alignItems = Align.FlexEnd; s.flexShrink = 0; });
            if (block == Desks.Block.Level) right.Add(UIKit.Strong("LV " + d.level, 14f, Pal.LockedText));
            else if (block == Desks.Block.Guest) right.Add(UIKit.Strong("GOOGLE", 12f, Pal.LockedText));
            var fee = UIKit.Row();
            if (!d.Free) fee.Add(new CoinIcon(14f).St(s => s.marginRight = 3));
            fee.Add(UIKit.Disp(d.Free ? "FREE" : Desks.Money(d.fee), 16f, block == Desks.Block.Marks ? Pal.Red : Pal.Ink));
            right.Add(fee);
            if (!d.Free) right.Add(UIKit.Plain("win " + Desks.Money(seats > 2 ? d.Prize(seats, 1) : d.Pot(seats)), 11f, Pal.Green));
            row.Add(right);
            row.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); ShowChooser(gm, mode, room, d.index); });
            return row;
        }

        // =====================================================================
        // not enough marks
        // =====================================================================

        static VisualElement NotEnough(GameManager gm, DeskDef d, Mode mode, OnlineRoom room)
        {
            var save = gm.Save;
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp("NOT ENOUGH MARKS", 28f, Pal.Ink));
            c.Add(UIKit.HandText(d.name + " needs " + Desks.Money(d.fee) + " marks to sit down. you have " + Desks.Money(save.marks) + ".", 18f, Pal.Ink).St(s => s.marginTop = 4));

            if (Desks.PocketMoneyReady(save))
            {
                var pocket = UIKit.Btn("Take today's pocket money  +" + Desks.PocketMoney, () =>
                {
                    Desks.TakePocketMoney(save);
                    gm.Audio.Stamp();
                    gm.UI.HideOverlay();
                    ShowChooser(gm, mode, room, shown);
                }, Pal.Yellow, Pal.Ink, UIKit.BodyBold, 16f, 54f, 14f, Pal.H("#C7A928"), Pal.Ink);
                pocket.style.marginTop = 16;
                c.Add(pocket);
            }
            else
                c.Add(UIKit.Plain("today's pocket money is taken. more tomorrow!", 13f, Pal.InkSoft).St(s => s.marginTop = 14));

            var best = Desks.BestAffordable(save);
            if (best.index < d.index)
            {
                var cheaper = UIKit.Btn("Try " + best.name + (best.Free ? " (free)" : " · " + Desks.Money(best.fee)), () =>
                {
                    gm.UI.HideOverlay();
                    ShowChooser(gm, mode, room, best.index);
                }, Color.white, Pal.Ink, UIKit.BodyBold, 15f, 52f, 14f, null, Pal.Ink);
                cheaper.style.marginTop = 10;
                c.Add(cheaper);
            }
            var ladder = UIKit.Btn("Earn marks on the Batch Ladder", () =>
            {
                gm.UI.HideOverlay();
                gm.ShowRival();
            }, Color.white, Pal.Ink, UIKit.BodyBold, 15f, 52f, 14f, null, Pal.Ink);
            ladder.style.marginTop = 10;
            c.Add(ladder);
            c.Add(UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f).St(s => s.marginTop = 6));
            return o;
        }
    }
}
