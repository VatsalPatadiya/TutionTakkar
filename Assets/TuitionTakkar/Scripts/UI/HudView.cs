using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// In-match HUD (mockups TE / TP): a centred cork board at the top with the players' cards
    /// and a pause badge pinned to its corner; the turn timer is the active card's border,
    /// draining clockwise (red in the last 10 seconds, with the desk edge glowing). A quiet
    /// "your turn" label on the desk, a power ring at your finger, a hint beside your pen and
    /// "edge!" warnings. No wall clock and no power ruler.
    /// </summary>
    public class HudView
    {
        public VisualElement Root { get; }
        readonly GameManager gm;

        // Layout (panel units, the 390 x 844 reference).
        const float WallHeight = 66f;               // C1: one slim cork strip (was a 168 tall board)
        const float TapeTop = WallHeight + 18f;     // "your turn" label at the far end of the desk
        const float PauseTop = WallHeight + 8f;     // pause button just under the strip, right side
        const float DeskNearEdgeGap = 26f;                     // just the desk's front edge (no ruler any more)

        float topInset, bottomInset;
        public float TopClearance => WallHeight + 12f + topInset;
        public float BottomClearance => DeskNearEdgeGap + bottomInset;

        readonly VisualElement wall;
        readonly VisualElement wallContent;
        readonly Sticky left, right;
        readonly Label roundLabel;
        readonly VisualElement tape;      // quiet "your turn" label on the desk
        readonly Label tapeText;
        readonly VisualElement deskGlow;  // red edge round the desk in the last 10 seconds
        readonly Label autoChip;
        bool humanTurn;
        int shooterNow = -1;
        readonly PowerRing ring;
        readonly Button pauseBtn;
        readonly Label penHint;
        readonly EdgeWarning[] edges = new EdgeWarning[4];
        Label debugShot;   // development builds only: last shot's numbers, for tuning from a mirrored phone

        class Sticky
        {
            public VisualElement root;
            public Label name, score, initial, secs;
            public bool active;
            public BorderTimer timer;
            public VisualElement[] dots = new VisualElement[GameConfig.RoundsToWin];
        }

        /// <summary>The seconds badge under "turn!" on the active card.</summary>
        static Label SecondsBadge()
        {
            var l = UIKit.Disp("", 13f, Color.white, TextAnchor.MiddleCenter).St(s =>
            {
                s.minWidth = 28; s.height = 22; UIKit.Radius(s, 11); s.backgroundColor = Pal.Ink;
                UIKit.Pad(s, 0, 5, 0, 5); s.display = DisplayStyle.None;
            });
            l.pickingMode = PickingMode.Ignore;
            return l;
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

            // C1: one slim cork strip across the top: your pill on the left, theirs on the right, the round
            // tag in the middle (3–4 players: a mini card each).
            var cork = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0; s.bottom = 0;
                s.borderBottomWidth = 5; s.borderBottomColor = Pal.H("#7A4E26");
                s.backgroundColor = Pal.H("#C99A61");
                s.backgroundImage = new StyleBackground(ArtLib.Cork);
                s.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                s.backgroundSize = new BackgroundSize(new Length(32f), new Length(32f));
            });
            cork.pickingMode = PickingMode.Ignore;
            var pair = UIKit.Row().St(s =>
            {
                s.position = Position.Absolute; s.left = 10; s.right = 10; s.top = 0; s.bottom = 0;
                s.justifyContent = Justify.SpaceBetween;
            });
            pair.pickingMode = PickingMode.Ignore;
            left = MakePill(Pal.Yellow, false);
            right = MakePill(Pal.Teal, true);
            roundLabel = UIKit.Text("round 1\nof 5", UIKit.HandBold, 12f, Pal.H("#3A230E"), TextAnchor.MiddleCenter, false).St(s =>
            {
                s.backgroundColor = Pal.H("#FBF8EF"); UIKit.Pad(s, 2, 7, 2, 7); s.whiteSpace = WhiteSpace.Normal;
            });
            roundLabel.pickingMode = PickingMode.Ignore;
            pair.Add(left.root, roundLabel, right.root);
            cork.Add(pair);
            partyRow = UIKit.Row().St(s =>
            {
                s.position = Position.Absolute; s.left = 8; s.right = 8; s.top = 0; s.bottom = 0;
                s.display = DisplayStyle.None;
            });
            partyRow.pickingMode = PickingMode.Ignore;
            cork.Add(partyRow);
            wall.Add(cork);


            // Everything on the wall sits in one container so notch insets can push it down together.
            wallContent = UIKit.El("WallContent");
            UIKit.Fill(wallContent);
            wallContent.pickingMode = PickingMode.Ignore;
            foreach (var child in new System.Collections.Generic.List<VisualElement>(wall.Children()))
                wallContent.Add(child);
            wall.Add(wallContent);
            Root.Add(wall);

            // Pause: a round button just under the strip on the right (over the floor beside the desk's far end).
            pauseBtn = UIKit.Btn("", () => gm.Pause(), Pal.Paper, Pal.Ink, UIKit.Body, 10f, 36f, 18f);
            pauseBtn.style.width = 36;
            UIKit.Pad(pauseBtn.style, 0);
            pauseBtn.style.position = Position.Absolute;
            pauseBtn.style.right = 10;
            pauseBtn.style.top = PauseTop;
            pauseBtn.style.alignItems = Align.Center;
            pauseBtn.style.justifyContent = Justify.Center;
            pauseBtn.style.borderBottomWidth = 3; pauseBtn.style.borderBottomColor = new Color(0f, 0f, 0f, 0.2f);
            pauseBtn.tooltip = "Pause";
            var bars = UIKit.Row();
            bars.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
                bars.Add(UIKit.El().St(s => { s.width = 4; s.height = 13; UIKit.Radius(s, 1); s.backgroundColor = Pal.Ink; s.marginLeft = 2; s.marginRight = 2; }));
            pauseBtn.Add(bars);
            Root.Add(pauseBtn);

            // ----- quiet turn label on the desk -----
            tape = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.top = TapeTop; s.left = 0; s.right = 0; s.alignItems = Align.Center;
            });
            tape.pickingMode = PickingMode.Ignore;
            tapeText = UIKit.Text("your turn", UIKit.HandBold, 17f, new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.65f), TextAnchor.MiddleCenter, false);
            tape.Add(tapeText);
            Root.Add(tape);

            // Red glow round the desk edge in the last 10 seconds (placed over the desk each update).
            deskGlow = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; UIKit.Border(s, 4f, new Color(0.85f, 0.28f, 0.18f, 0.6f)); UIKit.Radius(s, 6);
                s.display = DisplayStyle.None;
            });
            deskGlow.pickingMode = PickingMode.Ignore;
            Root.Add(deskGlow);

            autoChip = UIKit.Strong("AUTO SHOT", 12f, Color.white, TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.backgroundColor = Pal.Ink; UIKit.Pad(s, 6, 10, 6, 10); UIKit.Radius(s, 14);
                s.letterSpacing = 0.4f; s.display = DisplayStyle.None;
            });
            autoChip.pickingMode = PickingMode.Ignore;
            UIKit.Rot(autoChip, -3f);
            Root.Add(autoChip);

            // Development builds: a small readout of the last shot (power, how far each pen moved,
            // distance to the edge) so tuning can be done from a mirrored phone without logcat.
            if (Debug.isDebugBuild)
            {
                debugShot = UIKit.Text("", UIKit.BodyBold, 10f, Color.white, TextAnchor.UpperLeft, true).St(s =>
                {
                    s.position = Position.Absolute; s.left = 8; s.right = 8;
                    s.bottom = DeskNearEdgeGap + 40;
                    s.backgroundColor = new Color(0.06f, 0.1f, 0.12f, 0.72f);
                    UIKit.Pad(s, 4, 6, 4, 6); UIKit.Radius(s, 4);
                    s.display = DisplayStyle.None;
                });
                debugShot.pickingMode = PickingMode.Ignore;
                Root.Add(debugShot);
            }

            // Keep the ring and hint on top of everything.
            ring.BringToFront();
            penHint.BringToFront();

            // Nothing on the HUD may swallow a touch meant for a pen (only the pause badge is a button).
            UIKit.PassThrough(Root);
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
            pauseBtn.style.top = PauseTop + top;
        }

        // ---------- stickies ----------

        static VisualElement Avatar(out Label initial, float size)
        {
            var av = UIKit.El().St(s =>
            {
                s.width = size; s.height = size; UIKit.Radius(s, size * 0.5f); s.backgroundColor = Color.white;
                UIKit.Border(s, 2f, Pal.Ink); s.alignItems = Align.Center; s.justifyContent = Justify.Center; s.flexShrink = 0;
            });
            av.pickingMode = PickingMode.Ignore;
            initial = UIKit.Disp("Y", size * 0.5f, Pal.Ink, TextAnchor.MiddleCenter);
            av.Add(initial);
            return av;
        }

        /// <summary>C1 pill: avatar, name over round dots, big score, seconds badge; the border is the timer.</summary>
        Sticky MakePill(Color bg, bool mirrored)
        {
            var st = new Sticky();
            st.root = UIKit.Row().St(s =>
            {
                s.backgroundColor = bg; UIKit.Pad(s, 6, 9, 6, 9); UIKit.Radius(s, 4);
                s.borderBottomWidth = 3; s.borderBottomColor = new Color(0f, 0f, 0f, 0.15f);
                s.flexDirection = mirrored ? FlexDirection.RowReverse : FlexDirection.Row;
            });
            st.root.pickingMode = PickingMode.Ignore;
            var av = Avatar(out st.initial, 26f);

            var col = UIKit.Col().St(s => { s.marginLeft = mirrored ? 0 : 7; s.marginRight = mirrored ? 7 : 0; s.alignItems = mirrored ? Align.FlexEnd : Align.FlexStart; });
            col.pickingMode = PickingMode.Ignore;
            st.name = UIKit.Disp("YOU", 13f, Pal.Ink);
            var dots = UIKit.Row().St(s => s.marginTop = 3);
            for (int i = 0; i < st.dots.Length; i++)
            {
                st.dots[i] = UIKit.Dot(9f, Pal.Green, false, Pal.Ink);
                if (i > 0) st.dots[i].style.marginLeft = 3;
                dots.Add(st.dots[i]);
            }
            col.Add(st.name, dots);

            st.score = UIKit.Disp("0", 30f, Pal.Ink).St(s => { s.marginLeft = 7; s.marginRight = 7; });
            st.secs = SecondsBadge();
            st.timer = new BorderTimer();
            st.root.Add(st.timer, av, col, st.score, st.secs);
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
                UIKit.Border(st.dots[i].style, on ? 0f : 2f, Pal.Ink);
            }
        }

        /// <summary>The shooter's pill: its border becomes the timer (when someone is on the clock).</summary>
        void MarkTurn(Sticky st, bool active, bool timed)
        {
            st.active = active;
            st.root.style.opacity = active ? 1f : 0.85f;
            st.timer.Shown = active && timed;
            st.timer.Set(1f, Pal.Ink);
            st.secs.style.display = active && timed ? DisplayStyle.Flex : DisplayStyle.None;
            st.secs.style.backgroundColor = Pal.Ink;
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
        readonly List<BorderTimer> partyTimers = new List<BorderTimer>();
        readonly List<Label> partySecs = new List<Label>();
        readonly List<Label> partyStatus = new List<Label>();
        bool[] partyAlive;
        Label eliminated;

        static readonly Color[] PartyColors = { Pal.Yellow, Pal.Pink, Pal.Sky, Pal.H("#C7E8B5") };

        /// <summary>C4: a mini card per player in the strip (you first, yellow): name, then in play / seconds / OUT · place.</summary>
        public void SetParty(MatchSetup s, bool[] alive, int[] place)
        {
            left.root.parent.style.display = DisplayStyle.None;
            partyRow.style.display = DisplayStyle.Flex;
            partyRow.Clear();
            partyCards.Clear();
            partyTimers.Clear();
            partySecs.Clear();
            partyStatus.Clear();
            partyAlive = (bool[])alive.Clone();
            int n = s.Players;
            int pensLeft = 0;
            for (int k = 0; k < n; k++)
            {
                int i = (s.localIndex + k) % n;
                if (alive[i]) pensLeft++;
                bool me = i == s.localIndex;
                var card = UIKit.Row().St(st =>
                {
                    st.flexGrow = 1; st.flexBasis = 0; st.height = 50; UIKit.Radius(st, 4); UIKit.Pad(st, 0, 6, 0, 6);
                    st.backgroundColor = me ? Pal.Yellow : PartyColors[1 + (k - 1) % 3];
                    st.borderBottomWidth = 3; st.borderBottomColor = new Color(0f, 0f, 0f, 0.15f);
                    if (k > 0) st.marginLeft = 6;
                });
                card.style.alignSelf = Align.Center;
                card.pickingMode = PickingMode.Ignore;
                var timer = new BorderTimer();
                card.Add(timer);
                string name = DisplayName(s, i);
                var av = Avatar(out var initial, 22f);
                initial.text = (name.StartsWith("Player ") && name.Length > 7 ? name.Substring(7, 1) : name.Substring(0, 1)).ToUpperInvariant();
                card.Add(av);
                var col = UIKit.Col().St(st => { st.marginLeft = 5; st.flexShrink = 1; st.overflow = Overflow.Hidden; });
                col.pickingMode = PickingMode.Ignore;
                col.Add(UIKit.Disp((name.Length > 7 ? name.Substring(0, 7) : name).ToUpperInvariant(), 11f, Pal.Ink));
                var status = UIKit.Text("", UIKit.HandBold, 11f, Pal.Ink, TextAnchor.MiddleLeft, false);
                var secs = SecondsBadge();
                secs.style.fontSize = 11; secs.style.height = 16; secs.style.minWidth = 22; secs.style.marginTop = 1;
                col.Add(status, secs);
                card.Add(col);
                if (!alive[i])
                {
                    card.style.opacity = 0.5f;
                    status.text = place[i] == 1 ? "winner" : "OUT · " + Ordinal(place[i]);
                    status.style.color = Pal.Red;
                }
                partyRow.Add(card);
                while (partyCards.Count <= i) { partyCards.Add(null); partyTimers.Add(null); partySecs.Add(null); partyStatus.Add(null); }
                partyCards[i] = card; partyTimers[i] = timer; partySecs[i] = secs; partyStatus[i] = status;
            }
            for (int i = 0; i < n; i++) if (alive[i]) { partyStatus[i].text = "in play"; partyStatus[i].style.color = new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.7f); }
            partyNote = pensLeft <= 2 && n > 2 ? "final two" : "last pen standing";
            UIKit.PassThrough(partyRow);
            if (shooterNow >= 0 && shooterNow < n) MarkPartyTurn(shooterNow, humanTurn);
            HideAim();
        }

        string partyNote = "";

        void MarkPartyTurn(int shooter, bool timed)
        {
            for (int i = 0; i < partyCards.Count; i++)
            {
                if (partyCards[i] == null) continue;
                bool active = i == shooter && (partyAlive == null || partyAlive[i]);
                partyTimers[i].Shown = active && timed;
                partyTimers[i].Set(1f, Pal.Ink);
                partySecs[i].style.display = active && timed ? DisplayStyle.Flex : DisplayStyle.None;
                partySecs[i].style.backgroundColor = Pal.Ink;
                if (partyAlive == null || partyAlive[i])
                {
                    partyStatus[i].text = active ? "turn!" : "in play";
                    partyStatus[i].style.color = active ? Pal.H("#B23A26") : new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.7f);
                    partyStatus[i].style.display = active && timed ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
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
            right.root.parent.style.display = DisplayStyle.Flex;
            left.root.style.display = DisplayStyle.Flex;
            partyRow.style.display = DisplayStyle.None;
            Color rightColor = s.mode == MatchMode.Ladder && s.rival != null ? s.rival.avatar
                : s.mode == MatchMode.PassAndPlay ? Pal.Pink : Pal.Teal;
            // Left card is always this phone's player (online player 1 included).
            int me = s.mode == MatchMode.Online ? s.localIndex : 0, them = 1 - me;
            SetSticky(left, DisplayName(s, me), score[me], Pal.Yellow);
            SetSticky(right, DisplayName(s, them), score[them], rightColor);
            roundLabel.text = "round " + round + "\nof " + (GameConfig.RoundsToWin * 2 - 1);
            HideAim();
        }

        public void SetTurn(MatchSetup s, int shooter)
        {
            string who = DisplayName(s, shooter);
            humanTurn = !s.isBot[shooter];
            shooterNow = shooter;
            int me = s.mode == MatchMode.Online ? s.localIndex : 0;
            if (s.IsParty) MarkPartyTurn(shooter, humanTurn);
            else
            {
                MarkTurn(left, shooter == me, humanTurn);
                MarkTurn(right, shooter != me, humanTurn);
            }
            tapeText.text = s.mode != MatchMode.PassAndPlay && shooter == me ? "your turn" : who.ToLowerInvariant() + "'s turn";
            if (s.IsParty && partyNote.Length > 0) tapeText.text += " · " + partyNote;
            StyleTape(TapeLook.Normal);
            HideAutoShot();
            float limit = s.mode == MatchMode.Online ? GameConfig.OnlineTurnTimeLimit : GameConfig.TurnTimeLimit;
            if (humanTurn) SetTimer(limit, limit);
            UIRoot.PopIn(tape);
        }

        // ---------- turn timer: the active card's border ----------

        enum TapeLook { Normal, Urgent, TimeUp }
        TapeLook look = TapeLook.Normal;
        string normalLabel = "your turn";

        void StyleTape(TapeLook l)
        {
            if (l == TapeLook.Normal) normalLabel = tapeText.text;
            look = l;
            tapeText.style.color = l == TapeLook.Normal ? new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.65f) : Pal.Red;
            tapeText.style.fontSize = l == TapeLook.Normal ? 17f : 20f;
            if (l != TapeLook.Urgent) deskGlow.style.display = DisplayStyle.None;
        }

        void ActiveTimer(out BorderTimer timer, out Label secs, out VisualElement card)
        {
            timer = null; secs = null; card = null;
            if (partyRow.style.display == DisplayStyle.Flex)
            {
                if (shooterNow >= 0 && shooterNow < partyTimers.Count && partyTimers[shooterNow] != null)
                { timer = partyTimers[shooterNow]; secs = partySecs[shooterNow]; card = partyCards[shooterNow]; }
                return;
            }
            var st = left.active ? left : right;
            timer = st.timer; secs = st.secs; card = st.root;
        }

        /// <summary>Drains the active card's border; red, a pulse and a glowing desk edge in the last 10 seconds.</summary>
        public void SetTimer(float secondsLeft, float limit)
        {
            if (!humanTurn) return;
            ActiveTimer(out var timer, out var secs, out var card);
            bool urgent = secondsLeft <= GameConfig.TurnTimeUrgent;
            var col = urgent ? Pal.H("#D9472F") : Pal.Ink;
            if (timer != null)
            {
                timer.Shown = true;
                timer.Set(secondsLeft / Mathf.Max(0.01f, limit), col);
            }
            if (secs != null)
            {
                secs.style.display = DisplayStyle.Flex;
                secs.text = Mathf.CeilToInt(secondsLeft).ToString();
                secs.style.backgroundColor = col;
            }

            if (urgent && look != TapeLook.Urgent)
            {
                StyleTape(TapeLook.Urgent);
                tapeText.text = "hurry!";
            }
            else if (!urgent && look != TapeLook.Normal)
            {
                tapeText.text = normalLabel;
                StyleTape(TapeLook.Normal);
            }
            if (urgent)
            {
                // Pulse once a second: the card swells a little, the desk edge glows.
                float frac = secondsLeft - Mathf.Floor(secondsLeft);
                if (card != null) card.style.scale = new Scale(Vector3.one * (1f + 0.05f * frac));
                PlaceDeskGlow();
                deskGlow.style.opacity = 0.35f + 0.65f * frac;
            }
            else if (card != null) card.style.scale = new Scale(Vector3.one);
        }

        void PlaceDeskGlow()
        {
            var cam = gm.World != null ? gm.World.Cam : null;
            if (cam == null || Root.layout.width < 1f) return;
            float hw = BenchWorld.HalfW, hd = BenchWorld.HalfD;
            var a = ToPanel(cam, new Vector3(-hw, 0f, hd));
            var b = ToPanel(cam, new Vector3(hw, 0f, hd));
            var c = ToPanel(cam, new Vector3(-hw, 0f, -hd));
            var d = ToPanel(cam, new Vector3(hw, 0f, -hd));
            float x0 = Mathf.Min(a.x, c.x), x1 = Mathf.Max(b.x, d.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(c.y, d.y);
            var st = deskGlow.style;
            st.left = x0; st.top = y0; st.width = x1 - x0; st.height = y1 - y0;
            st.display = DisplayStyle.Flex;
        }

        /// <summary>Time's up: the game is about to shoot for you.</summary>
        public void SetTimeUp()
        {
            StyleTape(TapeLook.TimeUp);
            tapeText.text = "time's up!";
            ActiveTimer(out var timer, out var secs, out var card);
            if (timer != null) timer.Set(0f, Pal.H("#D9472F"));
            if (secs != null) secs.style.display = DisplayStyle.None;
            if (card != null) card.style.scale = new Scale(Vector3.one);
            UIRoot.PopIn(tape);
        }

        /// <summary>Navy "AUTO SHOT · 55%" chip beside your pen while the auto shot aims.</summary>
        /// <param name="left">Online: auto shots left after this one (-1 offline, where there is no limit).</param>
        public void ShowAutoShot(float power, Vector2 penPanel, int left = -1)
        {
            autoChip.text = "AUTO SHOT · " + Mathf.RoundToInt(power * 100f) + "%"
                + (left < 0 ? "" : left == 0 ? "\nlast one! next time you lose" : "\n" + left + " left this match");
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

        /// <summary>Power now lives only in the ring at your finger (no ruler); kept for callers.</summary>
        public void SetPower(float power, float strikeT) { }

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
