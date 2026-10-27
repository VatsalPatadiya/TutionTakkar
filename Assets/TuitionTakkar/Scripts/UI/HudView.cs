using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// In-match HUD (mockup K9): a classroom wall with a compact blackboard, both players chalked
    /// side by side ("vs"), an arrow and a chalk circle timer by whoever is shooting, yellow boxes
    /// for rounds won (3–4 players: a 2 x 2 grid of names, out pens crossed off), a
    /// chalk ledge, a torn paper chit with "your turn" in blue ballpoint, a sketched paper pause
    /// button, a pencil power ring at your finger, a hint beside your pen and "edge!" warnings.
    /// The desk glows red at the edge in the last 10 seconds.
    /// </summary>
    public class HudView
    {
        public VisualElement Root { get; }
        readonly GameManager gm;

        // Layout (panel units, the 390 x 844 reference), as in K9: a short board leaves the desk more room.
        const float BoardTopMin = 44f;     // blackboard top without a notch
        const float BoardHeight = 128f;
        const float BoardSide = 14f;
        const float WallBelowBoard = 42f;  // ledge + a strip of wall under the board
        const float PauseTopMin = 12f;
        const float DeskNearEdgeGap = 26f; // just the desk's front edge (legs may run off the bottom)

        float topInset, bottomInset;
        float boardTop = BoardTopMin;
        float WallHeight => boardTop + BoardHeight + WallBelowBoard;
        public float TopClearance => WallHeight + 22f;
        public float BottomClearance => DeskNearEdgeGap + bottomInset;

        readonly VisualElement wall, wallShade, board, boardShadow, ledge;
        readonly VisualElement duoBox, partyBox;
        readonly Label title, dateNote, thought, notesL;
        readonly Label plate;
        readonly ChalkRow left, right;
        readonly VisualElement chitRow;
        readonly PaperChit chit;
        readonly SketchArrow chitArrow;
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

        /// <summary>One chalked line on the board: arrow (whose turn), name, score, round boxes, timer.</summary>
        class ChalkRow
        {
            public VisualElement root;
            public SketchArrow arrow;
            public Label name, score;
            public VisualElement strike;
            public ChalkBox[] boxes;
            public ChalkTimer timer;
            public bool active;
            public int player = -1;
        }

        /// <summary>A player's name on the blackboard was tapped (online: opens their profile).</summary>
        public event System.Action<int> NameTapped;

        void Tappable(ChalkRow r)
        {
            r.root.pickingMode = PickingMode.Position;
            r.root.RegisterCallback<ClickEvent>(_ => { if (r.player >= 0) NameTapped?.Invoke(r.player); });
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

            // ----- aim layer (behind the wall) -----
            for (int i = 0; i < edges.Length; i++) edges[i] = MakeEdgeWarning();

            penHint = UIKit.Text("grab near the cap = spin", UIKit.HandBold, 16f, Sketch.Ballpoint, TextAnchor.MiddleLeft, false);
            penHint.style.position = Position.Absolute;
            penHint.pickingMode = PickingMode.Ignore;
            penHint.style.display = DisplayStyle.None;
            UIKit.Rot(penHint, -4f);
            Root.Add(penHint);

            ring = new PowerRing();
            ring.style.display = DisplayStyle.None;
            Root.Add(ring);

            // ----- classroom wall -----
            wallShade = UIKit.El().St(s =>
            {
                // The wall's shadow falling on the floor tiles.
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.height = 120;
                s.backgroundImage = new StyleBackground(ArtLib.Gradient(new Color(0f, 0f, 0f, 0.2f), new Color(0f, 0f, 0f, 0f)));
            });
            Root.Add(wallShade);

            wall = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 0;
                s.backgroundColor = Pal.H("#51614F");
                s.backgroundImage = new StyleBackground(ArtLib.Gradient(Pal.H("#566655"), Pal.H("#4B5A4A")));
            });
            wall.Add(UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 0; s.height = 10;
                s.backgroundColor = Pal.H("#3A453A");
            }));
            Root.Add(wall);

            // Blackboard: wooden frame lit from above (lighter top, darker bottom), a drop shadow, green slate.
            boardShadow = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = BoardSide + 2; s.right = BoardSide - 2; s.height = BoardHeight;
                s.backgroundColor = new Color(0f, 0f, 0f, 0.28f); UIKit.Radius(s, 4);
            });
            Root.Add(boardShadow);
            board = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = BoardSide; s.right = BoardSide; s.height = BoardHeight;
                s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 7;
                s.borderTopColor = Pal.H("#5A3E27"); s.borderLeftColor = Pal.H("#4E3522");
                s.borderRightColor = Pal.H("#3F2A1A"); s.borderBottomColor = Pal.H("#362416");
                UIKit.Radius(s, 3);
                s.backgroundColor = Pal.H("#1F2C26");
                s.backgroundImage = new StyleBackground(ArtLib.Chalkboard);
                s.overflow = Overflow.Hidden;
            });
            Root.Add(board);

            // Chalk notes left over from class, small in the bottom corners.
            notesL = Sketch.Chalk("Std. 9-A", 10f, new Color(1f, 1f, 1f, 0.4f)).St(s => { s.position = Position.Absolute; s.left = 10; s.bottom = 3; });
            dateNote = Sketch.Chalk("Date: " + System.DateTime.Now.ToString("dd/MM"), 10f, new Color(1f, 1f, 1f, 0.4f)).St(s => { s.position = Position.Absolute; s.right = 10; s.bottom = 3; });
            // Leftover lesson, rubbed out but still faintly there.
            thought = Sketch.Chalk("\"practice makes a man perfect\"", 10f, new Color(1f, 1f, 1f, 0.32f), TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 3;
            });
            board.Add(thought, notesL, dateNote);

            // Centred heading: "PEN FIGHT · round 2 of 5" / "LAST PEN STANDING".
            title = Sketch.Chalk("PEN FIGHT", 13f, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 4;
            });
            board.Add(title);

            // 2 players: you on the left, them on the right, "vs" between.
            duoBox = UIKit.Row(Align.FlexStart).St(s =>
            {
                s.position = Position.Absolute; s.left = 14; s.right = 14; s.top = 24; s.justifyContent = Justify.SpaceBetween;
            });
            left = MakeColumn(false);
            right = MakeColumn(true);
            var vs = Sketch.Chalk("vs", 26f, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter).St(s => s.marginTop = 16);
            duoBox.Add(left.root, vs, right.root);
            board.Add(duoBox);

            // 3–4 players: a 2 x 2 grid of names (filled by SetParty).
            partyBox = UIKit.Row().St(s =>
            {
                s.position = Position.Absolute; s.left = 12; s.right = 12; s.top = 26; s.flexWrap = Wrap.Wrap;
                s.display = DisplayStyle.None;
            });
            board.Add(partyBox);

            // Chalk ledge under the board, with two sticks of chalk.
            ledge = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 50; s.right = 50; s.height = 8;
                s.backgroundColor = Pal.H("#8A6440"); s.borderBottomWidth = 2; s.borderBottomColor = Pal.H("#5E4024");
            });
            ledge.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 60; s.top = -5; s.width = 20; s.height = 5; UIKit.Radius(s, 2); s.backgroundColor = Sketch.ChalkWhite; }));
            ledge.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = 90; s.top = -4; s.width = 14; s.height = 5; UIKit.Radius(s, 2); s.backgroundColor = Sketch.ChalkYellow; }));
            Root.Add(ledge);

            // Premium desks: a metal plate on the board's frame ("GOLD DESK").
            plate = UIKit.Strong("", 9f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
            {
                s.height = 15;
                UIKit.Pad(s, 0, 8, 0, 8); UIKit.Radius(s, 3); s.letterSpacing = 1.2f; s.display = DisplayStyle.None;
                UIKit.Border(s, 1f, new Color(0f, 0f, 0f, 0.35f));
            });
            var plateRow = UIKit.Row().St(s => { s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = -10; s.justifyContent = Justify.Center; });
            plateRow.pickingMode = PickingMode.Ignore;
            plateRow.Add(plate);
            ledge.Add(plateRow);

            // Pause: a sketched paper square, top right (just over the board's frame).
            pauseBtn = new Button(() => gm.Pause()) { tooltip = "Pause" };
            pauseBtn.style.position = Position.Absolute;
            pauseBtn.style.right = 18;
            pauseBtn.style.width = 44; pauseBtn.style.height = 44;
            pauseBtn.style.backgroundColor = Color.clear;
            UIKit.Border(pauseBtn.style, 0f, Color.clear);
            UIKit.Pad(pauseBtn.style, 0);
            pauseBtn.style.marginLeft = pauseBtn.style.marginRight = pauseBtn.style.marginTop = pauseBtn.style.marginBottom = 0;
            pauseBtn.style.alignItems = Align.FlexStart; pauseBtn.style.justifyContent = Justify.FlexStart;
            var sq = new SketchSquare(38f);
            sq.pickingMode = PickingMode.Ignore;
            sq.Add(new PencilPause());
            pauseBtn.Add(sq);
            Root.Add(pauseBtn);

            // ----- the torn paper chit under the board: "your turn" -----
            chitRow = UIKit.Row().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.justifyContent = Justify.Center;
            });
            chit = new PaperChit("your turn", 20f);
            chitArrow = new SketchArrow(Sketch.Ballpoint, 20f, 14f, 2.6f).St(s => s.marginRight = 6);
            chit.Insert(0, chitArrow);
            tapeText = chit.label;
            UIKit.Rot(chit, -2f);
            chitRow.Add(chit);
            Root.Add(chitRow);

            // Red-pen outline round the desk in the last 10 seconds (K11), traced over the desk's
            // real (tilted) corners each update.
            deskGlow = UIKit.El().St(s => s.display = DisplayStyle.None);
            UIKit.Fill(deskGlow);
            deskGlow.pickingMode = PickingMode.Ignore;
            deskGlow.generateVisualContent += DrawDeskOutline;
            Root.Add(deskGlow);

            autoChip = UIKit.Strong("AUTO SHOT", 12f, Color.white, TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.backgroundColor = Sketch.Graphite; UIKit.Pad(s, 6, 10, 6, 10); UIKit.Radius(s, 14);
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

            Layout();
            // Nothing on the HUD may swallow a touch meant for a pen (only the pause button is a button).
            UIKit.PassThrough(Root);
            Tappable(left);
            Tappable(right);
        }

        // ---------- layout ----------

        /// <summary>Push the wall clear of notches / home indicators (panel units).</summary>
        public void ApplyInsets(float top, float bottom)
        {
            topInset = top;
            bottomInset = bottom;
            Layout();
        }

        void Layout()
        {
            float pauseTop = Mathf.Max(PauseTopMin, topInset + 4f);
            boardTop = Mathf.Max(BoardTopMin, pauseTop + 26f);   // the pause button overlaps the frame a little
            float wallH = WallHeight;
            wall.style.height = wallH;
            wallShade.style.top = wallH;
            board.style.top = boardTop;
            boardShadow.style.top = boardTop + 7f;
            ledge.style.top = boardTop + BoardHeight - 2f;
            chitRow.style.top = boardTop + BoardHeight + 14f;
            pauseBtn.style.top = pauseTop;
        }

        // ---------- the desk being played ----------

        /// <summary>Online desks: the room's wall, a metal board frame and plate for premium tiers, the desk and pot in chalk.</summary>
        public void SetDesk(DeskDef d, int pot)
        {
            var wallTop = Pal.H("#566655");
            var wallBottom = Pal.H("#4B5A4A");
            Color fT = Pal.H("#5A3E27"), fL = Pal.H("#4E3522"), fR = Pal.H("#3F2A1A"), fB = Pal.H("#362416");
            if (d != null && d.Premium)
            {
                wallTop = Color.Lerp(wallTop, d.wall, 0.55f) * 0.9f;
                wallBottom = Color.Lerp(wallBottom, d.wall, 0.55f) * 0.78f;
                wallTop.a = wallBottom.a = 1f;
                var m = d.Trim.Value;
                fT = Color.Lerp(m, Color.white, 0.25f); fL = m; fR = Color.Lerp(m, Color.black, 0.25f); fB = Color.Lerp(m, Color.black, 0.4f);
            }
            wall.style.backgroundImage = new StyleBackground(ArtLib.Gradient(wallTop, wallBottom));
            board.style.borderTopColor = fT; board.style.borderLeftColor = fL;
            board.style.borderRightColor = fR; board.style.borderBottomColor = fB;
            ledge.style.backgroundColor = d != null && d.Premium ? fL : Pal.H("#8A6440");
            ledge.style.borderBottomColor = d != null && d.Premium ? fB : Pal.H("#5E4024");

            if (d != null && d.Premium)
            {
                plate.text = Desks.TierName(d.tier) + " DESK";
                plate.style.backgroundColor = Color.Lerp(d.Trim.Value, Color.white, 0.15f);
                plate.style.display = DisplayStyle.Flex;
            }
            else plate.style.display = DisplayStyle.None;

            if (d == null)
            {
                thought.text = "\"practice makes a man perfect\"";
                notesL.text = "Std. 9-A";
            }
            else
            {
                thought.text = d.name + (pot > 0 ? " · pot " + Desks.Money(pot) : " · friendly");
                notesL.text = d.Number;
            }
        }

        // ---------- chalk rows ----------

        /// <summary>K9 column: (arrow) name (timer) over a big score and the round boxes. Mirrored = right side.</summary>
        ChalkRow MakeColumn(bool mirrored)
        {
            var r = new ChalkRow();
            var align = mirrored ? Align.FlexEnd : Align.FlexStart;
            r.root = UIKit.Col().St(s => s.alignItems = align);
            var top = UIKit.Row().St(s => s.height = 28);
            r.arrow = new SketchArrow(Sketch.ChalkYellow, 18f, 14f, 2.8f).St(s => { s.marginRight = 5; s.display = DisplayStyle.None; });
            r.name = Sketch.Chalk("", 22f, Color.white);
            r.strike = UIKit.El();
            r.timer = new ChalkTimer(26f).St(s => { s.marginLeft = 6; s.display = DisplayStyle.None; });
            top.Add(r.arrow, r.name, r.timer);
            var bottom = UIKit.Row().St(s => { s.marginTop = 2; s.flexDirection = mirrored ? FlexDirection.RowReverse : FlexDirection.Row; });
            r.score = Sketch.Chalk("", 34f, Color.white);
            var boxes = UIKit.Row().St(s => { s.marginLeft = mirrored ? 0 : 8; s.marginRight = mirrored ? 8 : 0; });
            r.boxes = new ChalkBox[3];   // best of 5 at most; SetRow hides the spare one at best-of-3 desks
            for (int i = 0; i < r.boxes.Length; i++)
            {
                r.boxes[i] = new ChalkBox();
                if (i > 0) r.boxes[i].style.marginLeft = 4;
                boxes.Add(r.boxes[i]);
            }
            bottom.Add(r.score, boxes);
            r.root.Add(top, bottom);
            return r;
        }

        /// <summary>3–4 players: one cell of the 2 x 2 grid: (arrow) name (timer), or the name crossed off with its place.</summary>
        ChalkRow MakeCell()
        {
            var r = new ChalkRow { boxes = new ChalkBox[0] };
            r.root = UIKit.Row().St(s => { s.width = Length.Percent(50); s.height = 40; });
            r.arrow = new SketchArrow(Sketch.ChalkYellow, 16f, 13f, 2.6f).St(s => { s.marginRight = 4; s.display = DisplayStyle.None; });
            var nameBox = UIKit.El().St(s => s.justifyContent = Justify.Center);
            r.name = Sketch.Chalk("", 21f, Color.white);
            r.strike = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = -2; s.top = 13; s.height = 2; s.width = 60;
                s.backgroundColor = new Color(1f, 1f, 1f, 0.85f); s.display = DisplayStyle.None;
            });
            UIKit.Rot(r.strike, -3f);
            nameBox.Add(r.name, r.strike);
            r.score = Sketch.Chalk("", 14f, Sketch.ChalkRed).St(s => s.marginLeft = 5);
            r.timer = new ChalkTimer(26f).St(s => { s.marginLeft = 6; s.display = DisplayStyle.None; });
            r.root.Add(r.arrow, nameBox, r.score, r.timer);
            return r;
        }

        static string Short(string name, int max) => name.Length > max ? name.Substring(0, max) : name;

        void SetRow(ChalkRow r, string name, Color chalk, int score, bool showScore)
        {
            r.name.text = Short(name, 8);
            r.name.style.color = chalk;
            r.score.text = showScore ? score.ToString() : "";
            for (int i = 0; i < r.boxes.Length; i++)
            {
                r.boxes[i].Won = i < score;
                r.boxes[i].style.display = i < GameConfig.RoundsToWin ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>The shooter's row gets the arrow (and the chalk timer when someone is on the clock).</summary>
        void MarkTurn(ChalkRow r, bool active, bool timed)
        {
            r.active = active;
            r.arrow.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            r.timer.style.display = active && timed ? DisplayStyle.Flex : DisplayStyle.None;
            r.timer.style.scale = new Scale(Vector3.one);
        }

        public static string DisplayName(MatchSetup s, int i)
        {
            if (s.mode == MatchMode.PassAndPlay) return "Player " + (i + 1);
            if (s.mode == MatchMode.Online) return i == s.localIndex ? "You" : s.names[i];
            return i == 0 ? "You" : s.names[1];
        }

        /// <summary>Chalk name: "P1" style for pass and play so it fits the board.</summary>
        static string ChalkName(MatchSetup s, int i)
        {
            string n = DisplayName(s, i);
            return n.StartsWith("Player ") ? "player " + n.Substring(7) : n;
        }

        // ---------- 3–4 players ----------

        readonly List<ChalkRow> partyRows = new List<ChalkRow>();
        bool[] partyAlive;
        bool partyMode;
        Label eliminated;

        static readonly Color[] PartyChalk = { Sketch.ChalkYellow, Sketch.ChalkPink, Sketch.ChalkBlue, Sketch.ChalkGreen };

        /// <summary>K2: LAST PEN STANDING, a row per player (you first, yellow); out pens crossed out with their place.</summary>
        public void SetParty(MatchSetup s, bool[] alive, int[] place)
        {
            partyMode = true;
            duoBox.style.display = DisplayStyle.None;
            partyBox.style.display = DisplayStyle.Flex;
            partyBox.Clear();
            partyRows.Clear();
            partyAlive = (bool[])alive.Clone();
            int n = s.Players;
            int pensLeft = 0;
            for (int i = 0; i < n; i++) if (alive[i]) pensLeft++;
            title.text = pensLeft <= 2 && n > 2 ? "FINAL TWO" : "LAST PEN STANDING · " + n + " pens";
            for (int i = 0; i < n; i++) partyRows.Add(null);
            for (int k = 0; k < n; k++)
            {
                int i = (s.localIndex + k) % n;
                var r = MakeCell();
                SetRow(r, ChalkName(s, i), PartyChalk[k % PartyChalk.Length], 0, false);
                if (!alive[i])
                {
                    r.name.style.opacity = 0.55f;
                    r.strike.style.display = DisplayStyle.Flex;
                    r.strike.style.width = Mathf.Min(90f, 10f * r.name.text.Length);
                    r.score.text = place[i] == 1 ? "1st!" : Ordinal(place[i]);
                }
                partyRows[i] = r;
                partyBox.Add(r.root);
            }
            UIKit.PassThrough(partyBox);
            for (int i = 0; i < n; i++)
                if (partyRows[i] != null) { partyRows[i].player = i; Tappable(partyRows[i]); }
            if (shooterNow >= 0 && shooterNow < n) MarkPartyTurn(shooterNow, humanTurn);
            HideAim();
        }

        void MarkPartyTurn(int shooter, bool timed)
        {
            for (int i = 0; i < partyRows.Count; i++)
            {
                if (partyRows[i] == null) continue;
                bool active = i == shooter && (partyAlive == null || partyAlive[i]);
                MarkTurn(partyRows[i], active, timed);
            }
        }

        public static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        /// <summary>"RiyaFlicks is out! (4th)" across the middle for a moment.</summary>
        public void ShowEliminated(string name, int place, bool me)
        {
            eliminated?.RemoveFromHierarchy();
            eliminated = UIKit.Text(me ? "YOU'RE OUT! (" + Ordinal(place) + ")" : name.ToUpperInvariant() + " IS OUT! (" + Ordinal(place) + ")",
                UIKit.Marker, 24f, me ? Sketch.RedPen : Sketch.Graphite, TextAnchor.MiddleCenter, true).St(st =>
            {
                st.position = Position.Absolute; st.left = 24; st.right = 24; st.top = Length.Percent(46);
                st.backgroundColor = Sketch.PaperC; UIKit.Pad(st, 10, 12, 10, 12); UIKit.Radius(st, 4);
                UIKit.Border(st, 2.6f, me ? Sketch.RedPen : Sketch.Graphite);
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
            partyMode = false;
            duoBox.style.display = DisplayStyle.Flex;
            partyBox.style.display = DisplayStyle.None;
            Color theirChalk = s.mode == MatchMode.PassAndPlay ? Sketch.ChalkPink
                : s.mode == MatchMode.Ladder && s.rival != null ? Color.Lerp(s.rival.avatar, Color.white, 0.35f) : Sketch.ChalkPink;
            // The top row is always this phone's player (online player 1 included).
            int me = s.mode == MatchMode.Online ? s.localIndex : 0, them = 1 - me;
            left.player = me;
            right.player = them;
            SetRow(left, ChalkName(s, me), Sketch.ChalkYellow, score[me], true);
            SetRow(right, ChalkName(s, them), theirChalk, score[them], true);
            title.text = "PEN FIGHT · round " + round + " of " + (GameConfig.RoundsToWin * 2 - 1);
            HideAim();
        }

        public void SetTurn(MatchSetup s, int shooter)
        {
            string who = ChalkName(s, shooter);
            humanTurn = !s.isBot[shooter];
            shooterNow = shooter;
            int me = s.mode == MatchMode.Online ? s.localIndex : 0;
            if (s.IsParty) MarkPartyTurn(shooter, humanTurn);
            else
            {
                MarkTurn(left, shooter == me, humanTurn);
                MarkTurn(right, shooter != me, humanTurn);
            }
            bool mine = s.mode != MatchMode.PassAndPlay && shooter == me;
            tapeText.text = mine ? "your turn" : who.ToLowerInvariant() + "'s turn";
            chitArrow.style.display = mine ? DisplayStyle.Flex : DisplayStyle.None;
            normalLabel = tapeText.text;
            normalArrow = mine;
            UIKit.Rot(chit, shooter % 2 == 0 ? -2f : 1.5f);
            StyleTape(TapeLook.Normal);
            HideAutoShot();
            float limit = s.mode == MatchMode.Online ? GameConfig.OnlineTurnTimeLimit : GameConfig.TurnTimeLimit;
            if (humanTurn) SetTimer(limit, limit);
            UIRoot.PopIn(chit);
        }

        // ---------- turn timer: the chalk circle by the shooter's name ----------

        enum TapeLook { Normal, Urgent, TimeUp }
        TapeLook look = TapeLook.Normal;
        string normalLabel = "your turn";
        bool normalArrow;

        void StyleTape(TapeLook l)
        {
            look = l;
            tapeText.style.color = l == TapeLook.Normal ? Sketch.Ballpoint : Sketch.RedPen;
            if (l != TapeLook.Normal) chitArrow.style.display = DisplayStyle.None;
            if (l != TapeLook.Urgent) deskGlow.style.display = DisplayStyle.None;
        }

        ChalkRow ActiveRow()
        {
            if (partyMode)
                return shooterNow >= 0 && shooterNow < partyRows.Count ? partyRows[shooterNow] : null;
            return left.active ? left : right.active ? right : null;
        }

        /// <summary>Drains the chalk circle; red chalk, a pulse and a glowing desk edge in the last 10 seconds.</summary>
        public void SetTimer(float secondsLeft, float limit)
        {
            if (!humanTurn) return;
            var row = ActiveRow();
            bool urgent = secondsLeft <= GameConfig.TurnTimeUrgent;
            var col = urgent ? Sketch.ChalkRed : Sketch.ChalkYellow;
            if (row != null)
            {
                row.timer.style.display = DisplayStyle.Flex;
                row.timer.Set(secondsLeft / Mathf.Max(0.01f, limit), Mathf.CeilToInt(secondsLeft), col);
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
                chitArrow.style.display = normalArrow ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (urgent)
            {
                // Pulse once a second: the chalk circle swells a little, the desk edge glows.
                float frac = secondsLeft - Mathf.Floor(secondsLeft);
                if (row != null) row.timer.style.scale = new Scale(Vector3.one * (1f + 0.12f * frac));
                PlaceDeskGlow();
                deskGlow.style.opacity = 0.55f + 0.45f * frac;
            }
            else if (row != null) row.timer.style.scale = new Scale(Vector3.one);
        }

        readonly Vector2[] deskCorners = new Vector2[4];

        void PlaceDeskGlow()
        {
            var cam = gm.World != null ? gm.World.Cam : null;
            if (cam == null || Root.layout.width < 1f) return;
            float hw = BenchWorld.HalfW + BenchWorld.RimSize, hd = BenchWorld.HalfD + BenchWorld.RimSize;
            deskCorners[0] = ToPanel(cam, new Vector3(-hw, 0f, hd));
            deskCorners[1] = ToPanel(cam, new Vector3(hw, 0f, hd));
            deskCorners[2] = ToPanel(cam, new Vector3(hw, 0f, -hd));
            deskCorners[3] = ToPanel(cam, new Vector3(-hw, 0f, -hd));
            deskGlow.style.display = DisplayStyle.Flex;
            deskGlow.MarkDirtyRepaint();
        }

        /// <summary>Dashed red-pen line along the desk's four edges.</summary>
        void DrawDeskOutline(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.strokeColor = Sketch.A(Sketch.RedPen, 0.9f);
            p.lineWidth = 3.5f;
            p.lineCap = LineCap.Round;
            const float dash = 11f, gap = 7f;
            for (int e = 0; e < 4; e++)
            {
                var a = deskCorners[e];
                var b = deskCorners[(e + 1) % 4];
                float len = Vector2.Distance(a, b);
                if (len < 1f) continue;
                var dir = (b - a) / len;
                for (float t = 0f; t < len; t += dash + gap)
                {
                    p.BeginPath();
                    p.MoveTo(a + dir * t);
                    p.LineTo(a + dir * Mathf.Min(len, t + dash));
                    p.Stroke();
                }
            }
        }

        /// <summary>Time's up: the game is about to shoot for you.</summary>
        public void SetTimeUp()
        {
            StyleTape(TapeLook.TimeUp);
            tapeText.text = "time's up!";
            var row = ActiveRow();
            if (row != null)
            {
                row.timer.Set(0f, 0, Sketch.ChalkRed);
                row.timer.style.scale = new Scale(Vector3.one);
            }
            UIRoot.PopIn(chit);
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
