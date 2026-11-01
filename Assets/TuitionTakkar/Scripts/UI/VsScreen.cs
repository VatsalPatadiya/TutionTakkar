using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Quick Match "VS" screens (8 Ball Pool style, Tuition Takkar look):
    /// while waiting, your sticker card VS a card that flicks through classmates' faces; when a
    /// seat is filled the flicking slows and lands on them; at the start every player's entry fee
    /// flies into the pot in the middle, then the match begins.
    /// </summary>
    public static class VsScreen
    {
        static readonly Color Board = Pal.H("#2F4A3A");
        static readonly Color BoardEdge = Pal.H("#7A5534");

        // ---------- pieces ----------

        /// <summary>A tilted sticker card: face, level badge, name.</summary>
        public static VisualElement Card(string face, string name, int level, float size, bool me, float tilt, out VisualElement disc, out Label nameLabel, out Label levelLabel)
        {
            var card = UIKit.Col().St(s =>
            {
                s.alignItems = Align.Center; s.backgroundColor = me ? Pal.Yellow : Pal.Paper;
                UIKit.Pad(s, 10, 6, 8, 6); UIKit.Radius(s, 16); UIKit.Border(s, 4f, Pal.Ink);
                s.borderBottomWidth = 7; s.width = size + 24; s.flexShrink = 0;
            });
            UIKit.Rot(card, tilt);
            disc = Faces.Avatar(face, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), Pal.H("#EEF2F0"), size);
            card.Add(disc);
            var lv = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = -10; s.top = -10; s.width = 34; s.height = 34; UIKit.Radius(s, 17);
                s.backgroundColor = Pal.Pouch; UIKit.Border(s, 3f, Pal.Ink); s.alignItems = Align.Center; s.justifyContent = Justify.Center;
                s.display = level > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            });
            levelLabel = UIKit.Disp(level > 0 ? level.ToString() : "", 15f, Color.white, TextAnchor.MiddleCenter);
            lv.Add(levelLabel);
            card.Add(lv);
            nameLabel = UIKit.Disp(Short(name), 17f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
            {
                s.marginTop = 6; s.overflow = Overflow.Hidden; s.maxWidth = size + 10; s.unityTextOverflowPosition = TextOverflowPosition.End; s.textOverflow = TextOverflow.Ellipsis;
                s.whiteSpace = WhiteSpace.NoWrap;
            });
            card.Add(nameLabel);
            return card;
        }

        static string Short(string n) => string.IsNullOrEmpty(n) ? "" : n.Length > 13 ? n.Substring(0, 12) + "…" : n;


        /// <summary>
        /// Spins a card through doodle stickers while searching (no fake names or levels). steps &lt; 0:
        /// forever (until the card leaves the screen); otherwise it slows down and calls land, which
        /// shows the real classmate's face, name and level.
        /// </summary>
        static void Flick(VisualElement disc, Label name, Label level, Action land = null, int steps = -1)
        {
            var r = new System.Random();
            float sz = disc.style.width.value.value;
            if (sz <= 0f) sz = 64f;
            var doodle = new DoodleIcon(DoodleIcon.Stickers[0], sz * 0.66f);
            doodle.style.position = Position.Absolute;
            doodle.style.left = doodle.style.top = sz * 0.17f;
            foreach (var ch in disc.Children()) ch.style.display = DisplayStyle.None;
            disc.Add(doodle);
            name.text = "· · ·";
            if (level != null) level.parent.style.display = DisplayStyle.None;
            int i = 0, last = -1;
            void Step()
            {
                if (disc.panel == null) return;
                int k;
                do k = r.Next(DoodleIcon.Stickers.Length); while (k == last);
                last = k;
                doodle.SetKind(DoodleIcon.Stickers[k]);
                disc.style.backgroundImage = StyleKeyword.None;
                disc.style.backgroundColor = DoodleIcon.StickerBacks[k];
                i++;
                if (steps >= 0 && i >= steps)
                {
                    doodle.RemoveFromHierarchy();
                    foreach (var ch in disc.Children()) ch.style.display = DisplayStyle.Flex;
                    land?.Invoke();
                    return;
                }
                // Steady while searching; on a match, slows down for the last few spins.
                long wait = steps < 0 ? 140 : 75 + (long)(Mathf.Max(0, i - (steps - 6)) * 40);
                disc.schedule.Execute(Step).StartingIn(wait);
            }
            disc.schedule.Execute(Step).StartingIn(30);
        }

        static VisualElement CoinLine(int amount, out Label label)
        {
            var row = UIKit.Row().St(s => { s.marginTop = 10; s.alignSelf = Align.Center; s.backgroundColor = new Color(0, 0, 0, 0.35f); UIKit.Pad(s, 4, 12, 4, 8); UIKit.Radius(s, 14); });
            row.Add(new CoinIcon(20f));
            label = UIKit.Disp(Desks.Money(amount), 18f, Pal.Yellow);
            label.style.marginLeft = 6;
            row.Add(label);
            return row;
        }

        static VisualElement VsText(float size) => UIKit.Disp("VS", size, Pal.Yellow, TextAnchor.MiddleCenter).St(s =>
        {
            s.unityTextOutlineColor = Pal.Ink; s.unityTextOutlineWidth = 0.25f; s.marginLeft = 6; s.marginRight = 6;
        });

        static VisualElement Banner(DeskDef d, int seats)
        {
            var col = UIKit.Col().St(s => { s.alignItems = Align.Center; s.marginBottom = 14; });
            col.Add(UIKit.Strong((d != null ? "D" + (d.index + 1) + " · " : "") + seats + " PLAYERS", 12f, Pal.Mist, TextAnchor.MiddleCenter).St(s => s.letterSpacing = 1f));
            var name = UIKit.Disp(d != null ? d.name.ToUpperInvariant() : "QUICK MATCH", 34f, Color.white, TextAnchor.MiddleCenter);
            col.Add(name);
            if (d != null)
                col.Add(UIKit.Strong(d.fee > 0 ? "entry " + Desks.Money(d.fee) + " · pot " + Desks.Money(d.Pot(seats)) : "friendly · no entry fee", 14f, Pal.Yellow, TextAnchor.MiddleCenter).St(s => s.marginTop = 2));
            return col;
        }

        static VisualElement Stage()
        {
            return UIKit.Col().St(s =>
            {
                s.backgroundColor = Board; UIKit.Border(s, 8f, BoardEdge); UIKit.Radius(s, 14); UIKit.Pad(s, 18, 12, 20, 12);
                s.alignItems = Align.Stretch;
            });
        }

        // ---------- waiting (room screen) ----------

        static readonly HashSet<string> landed = new HashSet<string>();

        /// <summary>The waiting board for a Quick Match room: me VS flicking cards; joiners land on their card.</summary>
        public static VisualElement Waiting(GameManager gm, OnlineRoom room)
        {
            var stage = Stage();
            stage.Add(Banner(room.Desk, room.Seats));
            bool two = room.Seats <= 2;
            float size = two ? 84f : 64f;
            var cards = UIKit.Row().St(s => { s.justifyContent = Justify.Center; s.flexWrap = two ? Wrap.NoWrap : Wrap.Wrap; s.alignItems = Align.Center; });

            var players = room.Players;
            var myLevel = OnlineLevel.Level(gm.Save.onlineXp);
            for (int i = 0; i < room.Seats; i++)
            {
                if (two && i == 1) cards.Add(VsText(38f));
                VisualElement card;
                if (i < players.Count)
                {
                    var p = players[i];
                    bool me = p.Id == room.MyId;
                    int lv = me ? myLevel : OnlineRoom.LevelOf(p);
                    bool landing = !me && !landed.Contains(p.Id);
                    // A classmate who just joined starts as a sticker (no face yet), then lands on them.
                    card = Card(landing ? null : OnlineRoom.FaceOf(p), landing ? "" : OnlineRoom.NameOf(p), landing ? 0 : lv, size, me, me ? -2f : 2f, out var disc, out var nl, out var ll);
                    if (landing && landed.Add(p.Id))
                    {
                        // New classmate: flick a little, then land on them.
                        string face = OnlineRoom.FaceOf(p), name = OnlineRoom.NameOf(p);
                        Flick(disc, nl, ll, () =>
                        {
                            Faces.Fill(disc, face); nl.text = Short(name);
                            ll.text = lv > 0 ? lv.ToString() : ""; ll.parent.style.display = lv > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                            UIRoot.PopIn(disc.parent);
                            Haptics.Pulse(18, 200);
                        }, 12);
                    }
                }
                else
                {
                    card = Card(null, "", 0, size, false, 2f, out var disc, out var nl, out var ll);
                    Flick(disc, nl, ll);
                }
                card.style.marginLeft = card.style.marginRight = two ? 4 : 6;
                card.style.marginBottom = two ? 0 : 12;
                cards.Add(card);
            }
            stage.Add(cards);

            float waited = Time.realtimeSinceStartup - room.JoinedAt;
            var status = UIKit.HandText("finding classmates… " + Clock(waited), 19f, Pal.H("#E6EFE9"), TextAnchor.MiddleCenter).St(s => s.marginTop = 16);
            stage.Add(status);
            // Keep the clock moving between room updates.
            status.schedule.Execute(() =>
            {
                if (status.panel == null || room.Closed) return;
                status.text = (room.Full ? "everyone's here!" : "finding classmates… ") + (room.Full ? "" : Clock(Time.realtimeSinceStartup - room.JoinedAt));
            }).Every(500);
            return stage;
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ---------- match start: stakes into the pot ----------

        /// <summary>
        /// The start-of-match screen: every player's card with their entry fee under it; the coins fly
        /// into the pot in the middle and the pot counts up. landOthers: other cards flick and land first
        /// (computer classmates that just "joined"). Takes about 2 s (3 s with landing).
        /// </summary>
        public static VisualElement Intro(MatchSetup s, bool landOthers)
        {
            var root = UIKit.Col().St(st => { st.backgroundColor = Pal.Night; st.justifyContent = Justify.Center; UIKit.Pad(st, 30, 16, 30, 16); });
            root.pickingMode = PickingMode.Position;
            var stage = Stage();
            int n = s.Players;
            var d = s.desk;
            stage.Add(Banner(d, n));
            bool two = n <= 2;
            float size = two ? 84f : 64f;
            int fee = d != null ? d.fee : 0;
            int pot = d != null ? d.Pot(n) : 0;

            var cards = UIKit.Row().St(st => { st.justifyContent = Justify.Center; st.flexWrap = two ? Wrap.NoWrap : Wrap.Wrap; st.alignItems = Align.FlexStart; });
            var coinLabels = new List<Label>();
            var coinRows = new List<VisualElement>();
            var potBox = UIKit.Col().St(st =>
            {
                st.alignItems = Align.Center; st.justifyContent = Justify.Center; st.width = 66; st.height = 74; st.backgroundColor = Pal.Pouch;
                UIKit.Radius(st, 16); UIKit.Border(st, 4f, Pal.Ink); st.alignSelf = Align.Center;
            });
            potBox.Add(new CoinIcon(30f));
            var potLabel = UIKit.Disp("0", 20f, Pal.Yellow, TextAnchor.MiddleCenter);
            potBox.Add(potLabel);
            potBox.Add(UIKit.Strong("POT", 10f, Pal.Mist, TextAnchor.MiddleCenter));

            for (int i = 0; i < n; i++)
            {
                if (two && i == 1)
                {
                    var mid = UIKit.Col().St(st => { st.alignItems = Align.Center; st.marginTop = 18; st.flexShrink = 0; });
                    mid.Add(VsText(36f));
                    if (fee > 0) mid.Add(potBox.St(st => st.marginTop = 8));
                    cards.Add(mid);
                }
                bool me = i == s.localIndex;
                string face = s.faces != null && i < s.faces.Length ? s.faces[i] : null;
                int lv = s.levels != null && i < s.levels.Length ? s.levels[i] : 0;
                string name = me ? "You" : s.names[i];
                var col = UIKit.Col().St(st => { st.alignItems = Align.Center; st.marginLeft = st.marginRight = two ? 2 : 6; st.marginBottom = two ? 0 : 10; });
                bool spin = landOthers && !me;
                var card = Card(spin ? null : face, spin ? "" : name, spin ? 0 : lv, size, me, me ? -2f : 2f, out var disc, out var nl, out var ll);
                col.Add(card);
                if (fee > 0)
                {
                    var cl = CoinLine(fee, out var lab);
                    col.Add(cl);
                    coinLabels.Add(lab);
                    coinRows.Add(cl);
                }
                if (landOthers && !me)
                {
                    nl.text = ""; ll.parent.style.display = DisplayStyle.None;
                    string fname = name;
                    Flick(disc, nl, ll, () =>
                    {
                        Faces.Fill(disc, face); nl.text = Short(fname);
                        ll.text = lv > 0 ? lv.ToString() : ""; ll.parent.style.display = lv > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                        UIRoot.PopIn(disc.parent);
                        Haptics.Pulse(18, 200);
                    }, 9 + i * 2);
                }
                cards.Add(col);
            }
            stage.Add(cards);
            if (!two && fee > 0) stage.Add(potBox.St(st => st.marginTop = 6));
            if (fee <= 0) stage.Add(UIKit.HandText("friendly match · no entry fee", 18f, Pal.H("#E6EFE9"), TextAnchor.MiddleCenter).St(st => st.marginTop = 12));
            root.Add(stage);
            root.Add(UIKit.HandText(s.localIndex == 0 ? "you flick first." : "get ready…", 19f, Pal.MistLight, TextAnchor.MiddleCenter).St(st => st.marginTop = 14));

            // Coins fly into the pot.
            var fx = UIKit.El();
            fx.pickingMode = PickingMode.Ignore;
            fx.style.position = Position.Absolute; fx.style.left = fx.style.top = fx.style.right = fx.style.bottom = 0;
            root.Add(fx);
            long start = landOthers ? 1700 : 500;
            if (fee > 0)
                root.schedule.Execute(() =>
                {
                    if (root.panel == null) return;
                    var target = potBox.worldBound.center - fx.worldBound.position;
                    for (int c = 0; c < coinRows.Count; c++)
                    {
                        var from = coinRows[c].worldBound.center - fx.worldBound.position;
                        for (int k = 0; k < 5; k++)
                        {
                            var coin = new CoinIcon(22f);
                            coin.style.position = Position.Absolute;
                            coin.style.left = from.x - 11 + (k - 2) * 4; coin.style.top = from.y - 11;
                            coin.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("translate"), new StylePropertyName("opacity") };
                            coin.style.transitionDuration = new List<TimeValue> { new TimeValue(0.55f, TimeUnit.Second), new TimeValue(0.55f, TimeUnit.Second) };
                            coin.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseIn), new EasingFunction(EasingMode.EaseIn) };
                            var delta = target - from;
                            fx.Add(coin);
                            int kk = k;
                            coin.schedule.Execute(() =>
                            {
                                coin.style.translate = new Translate(delta.x - (kk - 2) * 4, delta.y);
                                coin.style.opacity = 0.2f;
                            }).StartingIn(30 + k * 70);
                            coin.schedule.Execute(() => coin.RemoveFromHierarchy()).StartingIn(700 + k * 70);
                        }
                        coinLabels[c].text = "0";
                    }
                    // Pot counts up while the coins land.
                    int steps = 12;
                    for (int t = 1; t <= steps; t++)
                    {
                        int v = pot * t / steps;
                        potLabel.schedule.Execute(() => potLabel.text = Desks.Money(v)).StartingIn(300 + t * 45);
                    }
                    potBox.schedule.Execute(() => { UIRoot.PopIn(potBox); Haptics.Pulse(24, 220); GameManager.I?.Audio.Ui(); }).StartingIn(900);
                }).StartingIn(start);
            return root;
        }
    }
}
