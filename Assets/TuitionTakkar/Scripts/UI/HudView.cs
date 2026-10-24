using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// In-match HUD laid out like "D · Tuition desk, front view": a classroom wall strip
    /// at the top (tube light, a wide cork board with two big score sticky notes and a
    /// round tag, and a live wall clock you tap to pause), a turn tape on the desk, a
    /// 15 cm ruler power gauge with soft / good / smash zones ("P1"), a power ring at your finger,
    /// a hint beside your pen and "edge!" warnings.
    /// </summary>
    public class HudView
    {
        public VisualElement Root { get; }
        readonly GameManager gm;

        // Layout (panel units, the 390 x 844 reference).
        const float WallHeight = 150f;       // desk top starts just under this
        const float TapeTop = 168f;
        const float RulerBottom = 10f;
        const float RulerHeight = 54f;
        const float GaugeLeft = 70f;        // ruler space taken by the POWER badge
        const float GaugeRight = 8f;
        const float SoftZone = 0.4f, GoodZone = 0.8f;
        const float DeskNearEdgeGap = 84f;   // room kept under the desk top for its front edge + ruler

        float topInset, bottomInset;
        public float TopClearance => WallHeight + topInset;
        public float BottomClearance => DeskNearEdgeGap + bottomInset;

        readonly VisualElement wall;
        readonly VisualElement wallContent;
        readonly Sticky left, right;
        readonly Label roundLabel;
        readonly VisualElement tape;      // whole turn-tape block (tape + timer bar)
        readonly VisualElement tapeRow;   // the strip of masking tape itself
        readonly Label tapeText;
        readonly VisualElement timerGroup;
        readonly TimerIcon timerIcon;
        readonly Label timerText;
        readonly VisualElement timerTrack;
        readonly VisualElement timerFill;
        readonly VisualElement urgentRing;
        readonly Label autoChip;
        bool humanTurn;
        readonly VisualElement ruler;       // frame (not clipped) holding the gauge + marker
        readonly VisualElement powerFill;
        readonly VisualElement powerMarker;
        readonly Label powerPct;
        readonly PowerRing ring;
        readonly Label penHint;
        readonly EdgeWarning[] edges = new EdgeWarning[4];
        Label debugShot;   // development builds only: last shot's numbers, for tuning from a mirrored phone

        class Sticky
        {
            public VisualElement root;
            public Label name, score, initial, turnTag;
            public VisualElement[] dots = new VisualElement[GameConfig.RoundsToWin];
        }

        class EdgeWarning
        {
            public VisualElement bar;
            public VisualElement[] dashes = new VisualElement[6];
            public Label label;
        }

        public HudView(GameManager manager)
        {
            gm = manager;
            Root = UIKit.El("Hud");
            UIKit.Fill(Root);
            Root.pickingMode = PickingMode.Ignore;

            // ----- aim layer (behind the bars) -----
            for (int i = 0; i < edges.Length; i++) edges[i] = MakeEdgeWarning();

            penHint = UIKit.Text("grab near the cap = spin", UIKit.HandBold, 16f, Pal.Ink, TextAnchor.MiddleLeft, false);
            penHint.style.position = Position.Absolute;
            penHint.pickingMode = PickingMode.Ignore;
            penHint.style.display = DisplayStyle.None;
            UIKit.Rot(penHint, -4f);
            Root.Add(penHint);

            ring = new PowerRing();
            ring.style.display = DisplayStyle.None;
            Root.Add(ring);

            // ----- classroom wall strip -----
            wall = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0; s.height = WallHeight;
                s.backgroundColor = Pal.H("#E8E0CB");
            });
            wall.pickingMode = PickingMode.Ignore;

            // Wide cork notice board with the two big score sticky notes pinned on it.
            var cork = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 12; s.top = 18; s.width = 284; s.height = 128;
                UIKit.Border(s, 6f, Pal.H("#7A4E26"));
                s.backgroundColor = Pal.H("#C99A61");
                s.backgroundImage = new StyleBackground(ArtLib.Cork);
                s.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                s.backgroundSize = new BackgroundSize(new Length(32f), new Length(32f));
            });
            cork.pickingMode = PickingMode.Ignore;
            left = MakeSticky(Pal.Yellow, -3f, Pal.H("#D9472F"));
            UIKit.Abs(left.root, 6, 6);
            right = MakeSticky(Pal.Teal, 2.5f, Pal.Pouch);
            UIKit.Abs(right.root, 142, 6);
            cork.Add(left.root, right.root);
            // 3–4 player online: one small card per player instead of the two big notes.
            partyRow = UIKit.Row(Align.Stretch).St(s =>
            {
                s.position = Position.Absolute; s.left = 4; s.right = 4; s.top = 6; s.bottom = 10;
                s.justifyContent = Justify.SpaceBetween; s.display = DisplayStyle.None;
            });
            partyRow.pickingMode = PickingMode.Ignore;
            cork.Add(partyRow);
            wall.Add(cork);

            // Paper tag hanging off the bottom of the board.
            roundLabel = UIKit.Text("round 1 · best of 5", UIKit.HandBold, 12f, Pal.H("#3A230E"), TextAnchor.MiddleCenter, false).St(s =>
            {
                s.position = Position.Absolute; s.left = 92; s.top = 124; s.width = 124;
                s.backgroundColor = Pal.H("#FBF8EF"); UIKit.Border(s, 1.5f, Pal.H("#B9A57A")); UIKit.Pad(s, 1, 6, 1, 6);
            });
            UIKit.Rot(roundLabel, -1f);
            roundLabel.pickingMode = PickingMode.Ignore;
            wall.Add(roundLabel);

            // Live wall clock = pause button.
            var clockBtn = UIKit.Btn("", () => gm.Pause(), Color.clear, Pal.Ink, UIKit.Body, 10f, 74f, 37f);
            clockBtn.style.width = 74;
            UIKit.Pad(clockBtn.style, 0);
            clockBtn.style.position = Position.Absolute;
            clockBtn.style.right = 12;
            clockBtn.style.top = 22;
            clockBtn.style.alignItems = Align.Center;
            clockBtn.style.justifyContent = Justify.Center;
            clockBtn.tooltip = "Pause";
            clockBtn.Add(new ClockFace(74f));
            wall.Add(clockBtn);
            var pauseLabel = UIKit.Strong("TAP TO PAUSE", 9f, Pal.H("#5A4A30"), TextAnchor.MiddleCenter).St(s => s.letterSpacing = 0.6f);
            UIKit.Abs(pauseLabel, null, 102, 8);
            pauseLabel.style.width = 82;
            pauseLabel.pickingMode = PickingMode.Ignore;
            wall.Add(pauseLabel);

            // Skirting line where the wall meets the floor, beside the desk.
            wall.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 0; s.height = 4; s.backgroundColor = Pal.H("#8C7C5E"); }));

            // Everything on the wall sits in one container so notch insets can push it down together.
            wallContent = UIKit.El("WallContent");
            UIKit.Fill(wallContent);
            wallContent.pickingMode = PickingMode.Ignore;
            foreach (var child in new System.Collections.Generic.List<VisualElement>(wall.Children()))
                wallContent.Add(child);
            wall.Add(wallContent);
            Root.Add(wall);

            // ----- turn tape on the desk, with the 60-second turn timer (T1-T3) -----
            tape = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.top = TapeTop; s.left = Length.Percent(50);
                s.width = 200; s.marginLeft = -100;
            });
            tape.pickingMode = PickingMode.Ignore;
            UIKit.Rot(tape, -2f);

            urgentRing = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = -12; s.right = -12; s.top = -8; s.bottom = -6;
                UIKit.Border(s, 3f, new Color(0.85f, 0.28f, 0.18f, 0.45f)); UIKit.Radius(s, 10);
                s.display = DisplayStyle.None;
            });
            urgentRing.pickingMode = PickingMode.Ignore;

            tapeRow = UIKit.Row().St(s =>
            {
                s.height = 36; s.justifyContent = Justify.SpaceBetween; UIKit.Pad(s, 0, 12, 0, 12);
                s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.96f);
            });
            tapeRow.pickingMode = PickingMode.Ignore;
            tapeText = UIKit.Text("your turn", UIKit.HandBold, 18f, Pal.Ink, TextAnchor.MiddleLeft, false);
            timerGroup = UIKit.Row();
            timerGroup.pickingMode = PickingMode.Ignore;
            timerIcon = new TimerIcon();
            timerText = UIKit.Disp("1:00", 22f, Pal.Ink, TextAnchor.MiddleRight);
            timerText.style.marginLeft = 5;
            timerGroup.Add(timerIcon, timerText);
            tapeRow.Add(tapeText, timerGroup);

            timerTrack = UIKit.El().St(s =>
            {
                s.height = 6; s.marginTop = 5; UIKit.Radius(s, 3); s.overflow = Overflow.Hidden;
                s.backgroundColor = new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.18f);
            });
            timerTrack.pickingMode = PickingMode.Ignore;
            timerFill = UIKit.El().St(s => { s.height = 6; s.width = Length.Percent(100); UIKit.Radius(s, 3); s.backgroundColor = Pal.Ink; });
            timerTrack.Add(timerFill);

            tape.Add(urgentRing, tapeRow, timerTrack);
            Root.Add(tape);

            autoChip = UIKit.Strong("AUTO SHOT", 12f, Color.white, TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.backgroundColor = Pal.Ink; UIKit.Pad(s, 6, 10, 6, 10); UIKit.Radius(s, 14);
                s.letterSpacing = 0.4f; s.display = DisplayStyle.None;
            });
            autoChip.pickingMode = PickingMode.Ignore;
            UIKit.Rot(autoChip, -3f);
            Root.Add(autoChip);

            // ----- 15 cm ruler power gauge: POWER badge, soft / good / smash zones -----
            ruler = UIKit.El("PowerRuler").St(s =>
            {
                s.position = Position.Absolute; s.left = 12; s.right = 12; s.bottom = RulerBottom; s.height = RulerHeight;
            });
            ruler.pickingMode = PickingMode.Ignore;

            var body = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0; s.bottom = 0;
                s.backgroundColor = Pal.H("#F1DFB0"); UIKit.Radius(s, 4); s.overflow = Overflow.Hidden;
                s.borderBottomWidth = 3; s.borderBottomColor = Pal.H("#A88A55");
            });
            body.pickingMode = PickingMode.Ignore;

            // Faint zone colours behind the fill.
            var zones = UIKit.Row(Align.Stretch).St(s =>
            {
                s.position = Position.Absolute; s.left = GaugeLeft; s.right = GaugeRight; s.top = 20; s.height = 16;
                UIKit.Radius(s, 8); s.overflow = Overflow.Hidden; s.opacity = 0.3f;
            });
            zones.pickingMode = PickingMode.Ignore;
            zones.Add(Zone(SoftZone, "#7BC47F"), Zone(GoodZone - SoftZone, "#FFD23F"), Zone(1f - GoodZone, "#E8563A"));

            var fillTrack = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = GaugeLeft; s.right = GaugeRight; s.top = 20; s.height = 16;
            });
            fillTrack.pickingMode = PickingMode.Ignore;
            powerFill = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.top = 0; s.bottom = 0; s.width = Length.Percent(0);
                UIKit.Radius(s, 8); s.backgroundColor = PowerColor(0f);
            });
            powerFill.pickingMode = PickingMode.Ignore;
            fillTrack.Add(powerFill);

            var ticks = new RulerTicks();
            UIKit.Abs(ticks, GaugeLeft, 0, GaugeRight, null);
            ticks.style.height = 13;

            var zoneLabels = UIKit.Row().St(s =>
            {
                s.position = Position.Absolute; s.left = GaugeLeft; s.right = GaugeRight; s.bottom = 2;
            });
            zoneLabels.pickingMode = PickingMode.Ignore;
            zoneLabels.Add(ZoneLabel("SOFT", SoftZone, Pal.H("#6B4A22")),
                           ZoneLabel("GOOD", GoodZone - SoftZone, Pal.H("#6B4A22")),
                           ZoneLabel("SMASH", 1f - GoodZone, Pal.Red));

            var badge = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.left = 6; s.top = 5; s.width = 56; s.height = 44;
                UIKit.Radius(s, 6); s.backgroundColor = Pal.Ink;
                s.alignItems = Align.Center; s.justifyContent = Justify.Center;
            });
            badge.pickingMode = PickingMode.Ignore;
            badge.Add(UIKit.Strong("POWER", 8f, Pal.H("#BFD3CB"), TextAnchor.MiddleCenter).St(s => s.letterSpacing = 0.8f));
            powerPct = UIKit.Disp("0%", 22f, Pal.Yellow, TextAnchor.MiddleCenter);
            badge.Add(powerPct);

            body.Add(zones, fillTrack, ticks, zoneLabels, badge);

            // Little arrow above the ruler pointing at the current power.
            var markerTrack = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = GaugeLeft; s.right = GaugeRight; s.top = -9; s.height = 10;
            });
            markerTrack.pickingMode = PickingMode.Ignore;
            powerMarker = new PowerArrow();
            powerMarker.style.position = Position.Absolute;
            powerMarker.style.left = Length.Percent(0);
            powerMarker.style.marginLeft = -8;
            powerMarker.style.top = 0;
            powerMarker.style.display = DisplayStyle.None;
            markerTrack.Add(powerMarker);

            ruler.Add(body, markerTrack);
            Root.Add(ruler);

            // Development builds: a small readout of the last shot (power, how far each pen moved,
            // distance to the edge) so tuning can be done from a mirrored phone without logcat.
            if (Debug.isDebugBuild)
            {
                debugShot = UIKit.Text("", UIKit.BodyBold, 10f, Color.white, TextAnchor.UpperLeft, true).St(s =>
                {
                    s.position = Position.Absolute; s.left = 8; s.right = 8;
                    s.bottom = RulerBottom + RulerHeight + 10;
                    s.backgroundColor = new Color(0.06f, 0.1f, 0.12f, 0.72f);
                    UIKit.Pad(s, 4, 6, 4, 6); UIKit.Radius(s, 4);
                    s.display = DisplayStyle.None;
                });
                debugShot.pickingMode = PickingMode.Ignore;
                Root.Add(debugShot);
            }

            // Your finger can drag down over the ruler: keep the ring and hint on top of it.
            ring.BringToFront();
            penHint.BringToFront();

            // Nothing on the HUD may swallow a touch meant for a pen (only the pause clock is a button).
            UIKit.PassThrough(Root);
        }

        static VisualElement Zone(float share, string hex)
        {
            var z = UIKit.El().St(s => { s.flexGrow = share; s.flexBasis = 0; s.backgroundColor = Pal.H(hex); });
            z.pickingMode = PickingMode.Ignore;
            return z;
        }

        static VisualElement ZoneLabel(string text, float share, Color c)
        {
            var l = UIKit.Strong(text, 9f, c).St(s => { s.flexGrow = share; s.flexBasis = 0; s.letterSpacing = 0.6f; });
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Green (soft) → yellow (good) → orange-red (smash).</summary>
        static Color PowerColor(float p)
        {
            var green = Pal.H("#5DAE62");
            var yellow = Pal.H("#F2C230");
            var orange = Pal.H("#F08A2E");
            var red = Pal.H("#E8563A");
            if (p <= SoftZone) return Color.Lerp(green, Color.Lerp(green, yellow, 0.5f), p / SoftZone);
            if (p <= GoodZone) return Color.Lerp(Color.Lerp(green, yellow, 0.5f), orange, (p - SoftZone) / (GoodZone - SoftZone));
            return Color.Lerp(orange, red, (p - GoodZone) / (1f - GoodZone));
        }

        // ---------- layout ----------

        /// <summary>Push the bars clear of notches / home indicators (panel units).</summary>
        public void ApplyInsets(float top, float bottom)
        {
            topInset = top;
            bottomInset = bottom;
            wall.style.height = WallHeight + top;
            wallContent.style.top = top; // keep the board, round tag and clock below the notch
            tape.style.top = TapeTop + top;
            ruler.style.bottom = RulerBottom + bottom;
        }

        // ---------- stickies ----------

        Sticky MakeSticky(Color bg, float rot, Color pin)
        {
            var st = new Sticky();
            st.root = UIKit.Col().St(s =>
            {
                s.width = 126; s.height = 100; s.backgroundColor = bg; UIKit.Pad(s, 9, 10, 9, 10);
            });
            UIKit.Rot(st.root, rot);
            st.root.pickingMode = PickingMode.Ignore;
            st.root.Add(UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 57; s.top = -6; s.width = 12; s.height = 12;
                UIKit.Radius(s, 6); s.backgroundColor = pin;
                s.borderBottomWidth = 2; s.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);
            }));

            var head = UIKit.Row();
            head.pickingMode = PickingMode.Ignore;
            var av = UIKit.El().St(s =>
            {
                s.width = 22; s.height = 22; UIKit.Radius(s, 11); s.backgroundColor = Color.white;
                UIKit.Border(s, 2f, Pal.Ink); s.alignItems = Align.Center; s.justifyContent = Justify.Center; s.flexShrink = 0;
            });
            av.pickingMode = PickingMode.Ignore;
            st.initial = UIKit.Disp("Y", 12f, Pal.Ink, TextAnchor.MiddleCenter);
            av.Add(st.initial);
            st.name = UIKit.Strong("YOU", 12f, Pal.Ink).St(s => { s.letterSpacing = 0.6f; s.marginLeft = 6; });
            head.Add(av, st.name);

            st.score = UIKit.Disp("0", 44f, Pal.Ink).St(s => s.marginTop = 2);
            var dots = UIKit.Row().St(s => s.marginTop = 3);
            for (int i = 0; i < st.dots.Length; i++)
            {
                st.dots[i] = UIKit.Dot(14f, Pal.Green, false, Pal.Ink);
                if (i > 0) st.dots[i].style.marginLeft = 4;
                dots.Add(st.dots[i]);
            }

            st.turnTag = UIKit.Text("turn!", UIKit.HandBold, 12f, Pal.H("#B23A26"), TextAnchor.MiddleRight, false);
            UIKit.Abs(st.turnTag, null, 6, 8);
            UIKit.Rot(st.turnTag, -6f);
            st.turnTag.style.display = DisplayStyle.None;

            st.root.Add(head, st.score, dots, st.turnTag);
            return st;
        }

        void SetSticky(Sticky st, string name, int score, Color bg)
        {
            st.root.style.backgroundColor = bg;
            st.name.text = name.ToUpperInvariant();
            st.initial.text = name.Length > 0 ? name.Substring(name.StartsWith("Player ") ? 7 : 0, 1).ToUpperInvariant() : "?";
            st.score.text = score.ToString();
            for (int i = 0; i < st.dots.Length; i++)
            {
                bool on = i < score;
                st.dots[i].style.backgroundColor = on ? Pal.Green : Color.clear;
                UIKit.Border(st.dots[i].style, on ? 0f : 2.5f, Pal.Ink);
            }
        }

        /// <summary>White outline + "turn!" on the shooter's note; the other one fades a little.</summary>
        void MarkTurn(Sticky st, bool active)
        {
            UIKit.Border(st.root.style, active ? 3f : 0f, Color.white);
            st.root.style.opacity = active ? 1f : 0.92f;
            st.turnTag.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static string DisplayName(MatchSetup s, int i)
        {
            if (s.mode == MatchMode.PassAndPlay) return "Player " + (i + 1);
            if (s.mode == MatchMode.Online) return i == s.localIndex ? "You" : s.names[i];
            return i == 0 ? "You" : s.names[1];
        }

        // ---------- 3–4 player cards ----------

        readonly VisualElement partyRow;
        readonly List<VisualElement> partyCards = new List<VisualElement>();
        Label eliminated;

        /// <summary>Mockup O7: a small card per player, yellow for you, dimmed with a place once out.</summary>
        public void SetParty(MatchSetup s, bool[] alive, int[] place)
        {
            left.root.style.display = DisplayStyle.None;
            right.root.style.display = DisplayStyle.None;
            partyRow.style.display = DisplayStyle.Flex;
            partyRow.Clear();
            partyCards.Clear();
            var colors = new[] { Pal.Yellow, Pal.Pink, Pal.Sky, Pal.Teal };
            int left2 = 0;
            for (int i = 0; i < s.Players; i++)
            {
                if (alive[i]) left2++;
                bool me = i == s.localIndex;
                var card = UIKit.Col().St(st =>
                {
                    st.width = Length.Percent(100f / s.Players - 2f); st.backgroundColor = me ? Pal.Yellow : Pal.Paper;
                    UIKit.Pad(st, 6, 5, 5, 5); st.alignItems = Align.Center;
                    st.borderBottomWidth = 3; st.borderBottomColor = new Color(0, 0, 0, 0.18f);
                    st.opacity = alive[i] ? 1f : 0.55f;
                });
                card.pickingMode = PickingMode.Ignore;
                UIKit.Rot(card, i % 2 == 0 ? -2f : 2f);
                string name = DisplayName(s, i);
                var av = UIKit.El().St(st =>
                {
                    st.width = 24; st.height = 24; UIKit.Radius(st, 12); st.backgroundColor = colors[i % colors.Length];
                    UIKit.Border(st, 2f, Pal.Ink); st.alignItems = Align.Center; st.justifyContent = Justify.Center;
                });
                av.pickingMode = PickingMode.Ignore;
                av.Add(UIKit.Disp(name.Substring(0, 1).ToUpperInvariant(), 12f, Pal.Ink, TextAnchor.MiddleCenter));
                card.Add(av);
                card.Add(UIKit.Strong(name.Length > 9 ? name.Substring(0, 9) : name, 10f, Pal.Ink, TextAnchor.MiddleCenter).St(st => st.marginTop = 2));
                string status = alive[i] ? "in" : "out · " + Ordinal(place[i]);
                if (!alive[i] && place[i] == 1) status = "winner";
                card.Add(UIKit.Text(status, UIKit.HandBold, 13f, alive[i] ? Pal.Green : Pal.Red, TextAnchor.MiddleCenter, false));
                partyRow.Add(card);
                partyCards.Add(card);
            }
            roundLabel.text = left2 + " pens left · last one wins";
            UIKit.PassThrough(partyRow);
            SetPower(0f, 0f);
            HideAim();
        }

        public static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        /// <summary>"RiyaFlicks is out! (4th)" across the middle for a moment.</summary>
        public void ShowEliminated(string name, int place, bool me)
        {
            eliminated?.RemoveFromHierarchy();
            eliminated = UIKit.Disp(me ? "YOU'RE OUT! (" + Ordinal(place) + ")" : name.ToUpperInvariant() + " IS OUT! (" + Ordinal(place) + ")",
                26f, me ? Pal.Red : Pal.Ink, TextAnchor.MiddleCenter).St(st =>
            {
                st.position = Position.Absolute; st.left = 20; st.right = 20; st.top = Length.Percent(42);
                st.backgroundColor = Pal.Paper; UIKit.Pad(st, 10, 12, 10, 12); UIKit.Radius(st, 10);
                UIKit.Border(st, 3f, me ? Pal.Red : Pal.Ink);
            });
            eliminated.pickingMode = PickingMode.Ignore;
            UIKit.Rot(eliminated, -3f);
            Root.Add(eliminated);
            UIRoot.PopIn(eliminated);
            var e = eliminated;
            e.schedule.Execute(() => e.RemoveFromHierarchy()).StartingIn(1800);
        }

        public void SetMatch(MatchSetup s, int[] score, int round)
        {
            left.root.style.display = DisplayStyle.Flex;
            right.root.style.display = DisplayStyle.Flex;
            partyRow.style.display = DisplayStyle.None;
            Color rightColor = s.mode == MatchMode.Ladder && s.rival != null ? s.rival.avatar
                : s.mode == MatchMode.PassAndPlay ? Pal.Pink : Pal.Teal;
            // Left sticky is always this phone's player (online player 1 included).
            int me = s.mode == MatchMode.Online ? s.localIndex : 0, them = 1 - me;
            SetSticky(left, DisplayName(s, me), score[me], Pal.Yellow);
            SetSticky(right, DisplayName(s, them), score[them], rightColor);
            roundLabel.text = "round " + round + " · best of " + (GameConfig.RoundsToWin * 2 - 1);
            SetPower(0f, 0f);
            HideAim();
        }

        public void SetTurn(MatchSetup s, int shooter)
        {
            string who = DisplayName(s, shooter);
            humanTurn = !s.isBot[shooter];
            int me = s.mode == MatchMode.Online ? s.localIndex : 0;
            MarkTurn(left, shooter == me);
            MarkTurn(right, shooter != me);
            for (int i = 0; i < partyCards.Count; i++)
                UIKit.Border(partyCards[i].style, i == shooter ? 3f : 0f, Color.white);
            tapeText.text = s.mode != MatchMode.PassAndPlay && shooter == me ? "your turn" : who.ToLowerInvariant() + "'s turn";
            UIKit.Rot(tape, shooter == me ? -2f : 2f);
            StyleTape(TapeLook.Normal);
            timerGroup.style.display = humanTurn ? DisplayStyle.Flex : DisplayStyle.None;
            timerTrack.style.display = humanTurn ? DisplayStyle.Flex : DisplayStyle.None;
            tapeRow.style.justifyContent = humanTurn ? Justify.SpaceBetween : Justify.Center;
            HideAutoShot();
            float limit = s.mode == MatchMode.Online ? GameConfig.OnlineTurnTimeLimit : GameConfig.TurnTimeLimit;
            if (humanTurn) SetTimer(limit, limit);
            UIRoot.PopIn(tape);
        }

        // ---------- turn timer ----------

        enum TapeLook { Normal, Urgent, TimeUp }
        TapeLook look = TapeLook.Normal;
        string normalLabel = "your turn";

        void StyleTape(TapeLook l)
        {
            if (l == TapeLook.Normal) normalLabel = tapeText.text;
            look = l;
            var bg = l == TapeLook.Urgent ? Pal.H("#FFE1D8") : l == TapeLook.TimeUp ? Pal.Ink : new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.96f);
            var fg = l == TapeLook.Urgent ? Pal.Red : l == TapeLook.TimeUp ? Color.white : Pal.Ink;
            var accent = l == TapeLook.Urgent ? Pal.Red : l == TapeLook.TimeUp ? Pal.Yellow : Pal.Ink;
            tapeRow.style.backgroundColor = bg;
            UIKit.Border(tapeRow.style, l == TapeLook.Urgent ? 2f : 0f, Pal.H("#D9472F"));
            tapeText.style.color = fg;
            timerText.style.color = accent;
            timerIcon.Set(l == TapeLook.TimeUp, accent);
            timerFill.style.backgroundColor = l == TapeLook.Normal ? Pal.Ink : Pal.H("#D9472F");
            urgentRing.style.display = l == TapeLook.Urgent ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>T1 / T2: time left on the tape and the shrinking bar.</summary>
        public void SetTimer(float secondsLeft, float limit)
        {
            if (!humanTurn) return;
            int whole = Mathf.CeilToInt(secondsLeft);
            timerText.text = (whole / 60) + ":" + (whole % 60).ToString("00");
            timerFill.style.width = Length.Percent(Mathf.Clamp01(secondsLeft / limit) * 100f);

            bool urgent = secondsLeft <= GameConfig.TurnTimeUrgent;
            if (urgent && look != TapeLook.Urgent)
            {
                StyleTape(TapeLook.Urgent);
                tapeText.text = "hurry!";
                UIKit.Rot(tape, -3f);
            }
            else if (!urgent && look != TapeLook.Normal)
            {
                tapeText.text = normalLabel;
                StyleTape(TapeLook.Normal);
            }
            if (urgent)
            {
                // Pulse the red outline once a second.
                float frac = secondsLeft - Mathf.Floor(secondsLeft);
                urgentRing.style.opacity = 0.35f + 0.65f * frac;
            }
        }

        /// <summary>T3: time's up, the game is about to shoot for you.</summary>
        public void SetTimeUp()
        {
            StyleTape(TapeLook.TimeUp);
            tapeText.text = "time's up!";
            timerText.text = "";
            timerFill.style.width = Length.Percent(0);
            UIRoot.PopIn(tape);
        }

        /// <summary>Navy "AUTO SHOT · 55%" chip beside your pen while the auto shot aims.</summary>
        public void ShowAutoShot(float power, Vector2 penPanel)
        {
            autoChip.text = "AUTO SHOT · " + Mathf.RoundToInt(power * 100f) + "%";
            autoChip.style.display = DisplayStyle.Flex;
            float panelW = Root.layout.width > 1f ? Root.layout.width : 390f;
            autoChip.style.left = Mathf.Clamp(penPanel.x + 12f, 8f, panelW - 150f);
            autoChip.style.top = penPanel.y + 20f;
        }

        public void HideAutoShot()
        {
            if (autoChip != null) autoChip.style.display = DisplayStyle.None;
        }

        // ---------- power + aim ----------

        public void SetPower(float power, float strikeT)
        {
            int pct = Mathf.RoundToInt(power * 100f);
            powerFill.style.width = Length.Percent(pct);
            powerFill.style.backgroundColor = PowerColor(Mathf.Clamp01(power));
            powerPct.text = pct + "%";
            powerPct.style.color = power > GoodZone ? Pal.H("#FF8A6B") : Pal.Yellow;
            powerMarker.style.display = pct > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            powerMarker.style.left = Length.Percent(pct);
        }

        /// <summary>Human aiming: ring at the finger, hint beside the pen.</summary>
        public void SetAim(AimInfo a)
        {
            SetPower(a.power, a.strikeT);

            ring.style.display = DisplayStyle.Flex;
            ring.MoveTo(a.fingerPanel);
            ring.Value = a.power;

            bool spin = Mathf.Abs(a.strikeT) > 0.5f;
            penHint.text = a.power >= 0.9f ? "careful, yours can follow it off"
                : spin ? (a.strikeT > 0f ? "grab near the cap = spin" : "grab near the tip = spin") : "middle = straight push";
            penHint.style.display = DisplayStyle.Flex;
            // Put the hint beside the pen, on the side away from the finger.
            bool fingerRight = a.fingerPanel.x > a.penPanel.x;
            float width = 190f;
            float x = fingerRight ? a.penPanel.x - width - 24f : a.penPanel.x + 24f;
            float panelW = Root.layout.width > 1f ? Root.layout.width : 390f;
            penHint.style.left = Mathf.Clamp(x, 8f, panelW - width - 8f);
            penHint.style.top = a.penPanel.y + 14f;
            penHint.style.width = width;
            penHint.style.unityTextAlign = fingerRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        public void HideAim()
        {
            ring.style.display = DisplayStyle.None;
            ring.Value = 0f;
            penHint.style.display = DisplayStyle.None;
        }

        // ---------- edge warnings ----------

        EdgeWarning MakeEdgeWarning()
        {
            var w = new EdgeWarning();
            w.bar = UIKit.El().St(s => { s.position = Position.Absolute; s.justifyContent = Justify.SpaceBetween; s.display = DisplayStyle.None; });
            w.bar.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < w.dashes.Length; i++)
            {
                w.dashes[i] = UIKit.El().St(s => { s.backgroundColor = Pal.H("#D9472F"); UIKit.Radius(s, 3); s.flexShrink = 0; });
                w.bar.Add(w.dashes[i]);
            }
            Root.Add(w.bar);
            w.label = UIKit.Text("edge!", UIKit.HandBold, 18f, Pal.Red, TextAnchor.MiddleCenter, false);
            w.label.style.position = Position.Absolute;
            w.label.style.display = DisplayStyle.None;
            w.label.pickingMode = PickingMode.Ignore;
            UIKit.Rot(w.label, -8f);
            Root.Add(w.label);
            return w;
        }

        Vector2 ToPanel(Camera cam, Vector3 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            var size = Root.layout.size;
            return new Vector2(sp.x * size.x / Screen.width, (Screen.height - sp.y) * size.y / Screen.height);
        }

        public Vector2 PanelPoint(Camera cam, Vector3 world) => ToPanel(cam, world);

        /// <summary>Show a red dashed bar on the edge next to any pen within ~6 cm of it.</summary>
        public void UpdateEdges(BenchWorld world, Camera cam)
        {
            float warn = GameConfig.DangerZone, halfSpan = 0.45f;
            for (int i = 0; i < edges.Length; i++)
            {
                var w = edges[i];
                var pen = i < world.Pens.Length ? world.Pens[i] : null;
                bool show = false;
                if (pen != null && !world.IsOut(pen) && Root.layout.width > 1f)
                {
                    var c = pen.Rb.position;
                    float dx = BenchWorld.HalfW - Mathf.Abs(c.x);
                    float dz = BenchWorld.HalfD - Mathf.Abs(c.z);
                    if (Mathf.Min(dx, dz) < warn)
                    {
                        show = true;
                        if (dx <= dz)
                        {
                            float ex = Mathf.Sign(c.x) * (BenchWorld.HalfW + BenchWorld.RimSize * 0.5f);
                            var a = ToPanel(cam, new Vector3(ex, 0f, c.z + halfSpan));
                            var b = ToPanel(cam, new Vector3(ex, 0f, c.z - halfSpan));
                            PlaceBar(w, true, (a.x + b.x) * 0.5f, Mathf.Min(a.y, b.y), Mathf.Abs(b.y - a.y));
                            bool rightEdge = c.x > 0f;
                            w.label.style.left = rightEdge ? a.x - 64f : a.x + 14f;
                            w.label.style.top = Mathf.Max(a.y, b.y) + 4f;
                        }
                        else
                        {
                            float ez = Mathf.Sign(c.z) * (BenchWorld.HalfD + BenchWorld.RimSize * 0.5f);
                            var a = ToPanel(cam, new Vector3(c.x - halfSpan, 0f, ez));
                            var b = ToPanel(cam, new Vector3(c.x + halfSpan, 0f, ez));
                            PlaceBar(w, false, Mathf.Min(a.x, b.x), (a.y + b.y) * 0.5f, Mathf.Abs(b.x - a.x));
                            bool farEdge = c.z > 0f;
                            w.label.style.left = Mathf.Max(a.x, b.x) + 6f;
                            w.label.style.top = farEdge ? a.y + 6f : a.y - 30f;
                        }
                    }
                }
                var d = show ? DisplayStyle.Flex : DisplayStyle.None;
                w.bar.style.display = d;
                w.label.style.display = d;
            }
        }

        static void PlaceBar(EdgeWarning w, bool vertical, float x, float y, float length)
        {
            const float thick = 7f;
            var s = w.bar.style;
            s.flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row;
            if (vertical)
            {
                s.left = x - thick * 0.5f; s.top = y; s.width = thick; s.height = length;
            }
            else
            {
                s.left = x; s.top = y - thick * 0.5f; s.width = length; s.height = thick;
            }
            float dash = Mathf.Max(4f, length / (w.dashes.Length * 1.6f));
            foreach (var e in w.dashes)
            {
                e.style.width = vertical ? thick : dash;
                e.style.height = vertical ? dash : thick;
            }
        }

        /// <summary>Development builds: show the last shot's numbers above the power ruler.</summary>
        public void SetDebugShot(string text)
        {
            if (debugShot == null) return;
            debugShot.text = text;
            debugShot.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void HideEdges()
        {
            foreach (var w in edges)
            {
                w.bar.style.display = DisplayStyle.None;
                w.label.style.display = DisplayStyle.None;
            }
        }

        // ---------- pause ----------

        public VisualElement BuildPause()
        {
            var o = UIKit.El().St(s => { s.backgroundColor = new Color(0.06f, 0.1f, 0.12f, 0.7f); s.alignItems = Align.Center; s.justifyContent = Justify.Center; });
            o.pickingMode = PickingMode.Position;
            var card = UIKit.Col().St(s => { s.width = 300; UIKit.Pad(s, 24, 20, 20, 20); s.alignItems = Align.Stretch; });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, -1f);
            card.Add(UIKit.Disp("PAUSED", 36f, Pal.Ink, TextAnchor.MiddleCenter));
            card.Add(UIKit.HandText("sir is looking the other way.", 18f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginBottom = 16));
            card.Add(UIKit.PrimaryBtn("RESUME", () => gm.Resume(), 56f));
            card.Add(UIKit.Btn("Quit to home", () => gm.QuitToHome(), Pal.Paper, Pal.Ink, UIKit.BodyBold, 16f, 50f, 14f, null, Pal.Ink)
                .St(s => s.marginTop = 12));
            o.Add(card);
            UIRoot.PopIn(card);
            return o;
        }
    }
}
