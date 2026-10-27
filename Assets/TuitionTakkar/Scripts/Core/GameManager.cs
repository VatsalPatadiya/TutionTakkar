using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Builds everything at startup and moves between screens.
    /// Drop it in any scene (or let Bootstrap add it) and press Play.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public SaveData Save { get; private set; }
        public BenchWorld World { get; private set; }
        public CameraRig Rig => World.Rig;
        public FlickInput Input { get; private set; }
        public AimVisual Aim { get; private set; }
        public MatchController Match { get; private set; }
        public UIRoot UI { get; private set; }
        public GameAudio Audio { get; private set; }
        public bool Paused { get; private set; }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;

            ConfigureRuntime();
            Save = SaveData.Load();

            World = new GameObject("BenchWorld").AddComponent<BenchWorld>();
            World.transform.SetParent(transform, false);
            World.Build();

            Aim = AimVisual.Create(World.transform);
            Audio = gameObject.AddComponent<GameAudio>();

            UI = gameObject.AddComponent<UIRoot>();
            UI.Build(this);

            Input = gameObject.AddComponent<FlickInput>();
            Input.Init(World, Aim, UI.GameLayer);
            Input.AimChanged += a => UI.Hud.SetAim(a);
            Input.AimEnded += () =>
            {
                UI.Hud.SetPower(0f, 0f);
                UI.Hud.HideAim();
            };

            Match = gameObject.AddComponent<MatchController>();
            Match.Init(this);

            WireFeel();
            ApplySettings();
            World.Rig.Fit();
            DeepLinks.Init(this);
            Push.Init();
            Reminders.Init();
            bool welcomeBack = Reminders.CheckComeback(Save);
            Reminders.Reschedule(Save);
            SplashScreen.Show(this, () =>
            {
                // An invite link wins over the welcome-back card (the marks are paid either way).
                bool invite = DeepLinks.HasPending;
                ShowHome();
                if (welcomeBack && !invite) UI.ShowOverlay(Reminders.WelcomeBack(this));
            });
        }

        static void ConfigureRuntime()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            // Android/iOS default to the "Medium" quality level, which has edge smoothing off:
            // thin pens then get jagged, stretched-looking outlines. 4x MSAA keeps them crisp.
            QualitySettings.antiAliasing = 4;
            Time.fixedDeltaTime = GameConfig.FixedDt;
            // Physics runs 240 times a second. After one slow frame Unity would otherwise try to
            // catch up with up to 80 physics steps at once, which makes the next frame slow too
            // (the "hang" while aiming). Cap the catch-up to 1/20 s.
            Time.maximumDeltaTime = 1f / 20f;
            Physics.gravity = GameConfig.Gravity;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.defaultContactOffset = 0.002f;
            Physics.bounceThreshold = 0.6f;
        }

        void WireFeel()
        {
            GameEvents.ClearAll();
            GameEvents.PenHitPen += s =>
            {
                Audio.Clack(s);
                Haptics.Clack(s);
                Rig.Shake(Mathf.Clamp01(s * 0.35f));
            };
            GameEvents.PenHitDesk += s => Audio.DeskTap(s);
            GameEvents.PenHitFloor += (p, s) =>
            {
                Audio.FloorThud(s);
                Haptics.Fall();
                Rig.Shake(0.7f);
            };
            GameEvents.Flicked += p => Haptics.Release(p);
            GameEvents.TimerTick += left =>
            {
                Audio.TimerTick(left);
                Haptics.Pulse(left <= 3 ? 22 : 12, left <= 3 ? 200 : 120);
            };
            GameEvents.AimStep += step =>
            {
                Audio.AimTick(step);
                Haptics.AimTick();
            };
        }

        void Update()
        {
            // Development builds: report any frame that takes longer than 1/10 s, so a freeze on
            // the phone shows up on screen with what was happening at that moment.
            if (Debug.isDebugBuild && Time.unscaledDeltaTime > 0.1f && Time.frameCount > 120)
            {
                string what = Input != null && Input.IsAiming ? "while aiming" : Match != null && Match.Shooting ? "during a shot" : "";
                Debug.LogWarning("[TuitionTakkar] slow frame " + Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) + " ms " + what);
                if (UI != null && UI.Hud.Root.style.display == UnityEngine.UIElements.DisplayStyle.Flex)
                {
                    UI.Hud.SetDebugShot("slow frame " + Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) + " ms " + what);
                }
            }

            // Slide noise follows the fastest pen still on the bench.
            float speed = 0f;
            if (Match != null && Match.Shooting)
                foreach (var p in World.Pens)
                    if (p != null && !World.IsOut(p))
                        speed = Mathf.Max(speed, p.Rb.linearVelocity.magnitude / GameConfig.WorldScale);
            Audio.SetSlide(speed);

            if (UI.Hud.Root.style.display.value == UnityEngine.UIElements.DisplayStyle.Flex)
                UI.Hud.UpdateEdges(World, World.Cam);
        }

        public void ApplySettings()
        {
            Audio.Muted = !Save.soundOn;
            Haptics.Enabled = Save.hapticsOn;
        }

        // ---------- navigation ----------

        public void ShowHome()
        {
            Match.Stop();
            World.ClearPens();
            UI.ShowScreen(HomeScreen.Build(this));
            DeepLinks.OnHomeShown();
        }

        public void ShowRival()
        {
            Match.Stop();
            UI.ShowScreen(RivalScreen.Build(this, Save.CurrentRival));
        }

        public void ShowPouch(int tab = 0)
        {
            Match.Stop();
            World.ClearPens();
            UI.ShowScreen(PouchScreen.Build(this, tab));
        }

        /// <summary>
        /// Anything online (Play Online, Leaderboard, Sign in): without internet, the
        /// no-internet popup; with internet, a "coming soon" note until online play ships.
        /// </summary>
        /// <summary>Play Online: no internet popup, or sign in / name / online lobby.</summary>
        public void ShowOnline() => OnlineScreens.Enter(this);

        public void ShowLeaderboard() => OnlineScreens.Enter(this, true);

        /// <summary>
        /// The player's pen for an offline match. Uses one match of ink; a dry pen plays
        /// with Starter Click stats (it keeps its colours) until it is refilled.
        /// </summary>
        PenDef PlayerPenForMatch()
        {
            var pen = Catalog.Pen(Save.equipped);
            if (!Economy.UsesInk(pen.id)) return pen;
            if (Save.UseInk(pen.id))
            {
                Save.Save();
                return pen;
            }
            return Catalog.Pen(Catalog.StarterId).Recolor(pen.color, pen.capColor);
        }

        public void StartLadder(RivalDef rival)
        {
            var s = new MatchSetup { mode = MatchMode.Ladder, rival = rival, bot = rival.bot };
            s.pens[0] = PlayerPenForMatch();
            s.pens[1] = Catalog.Pen(rival.penId);
            // Mirror match? Give the rival a recoloured copy so the pens are easy to tell apart.
            if (s.pens[0].id == s.pens[1].id) s.pens[1] = s.pens[1].Recolor(Pal.H("#2A9D8F"), Pal.H("#16324F"));
            s.names[0] = "You";
            s.names[1] = rival.name;
            s.isBot[1] = true;
            StartMatch(s);
        }

        public void StartPractice(int level)
        {
            level = Mathf.Clamp(level, 0, Catalog.PracticeBots.Length - 1);
            var s = new MatchSetup { mode = MatchMode.Practice, bot = Catalog.PracticeBots[level], practiceLevel = level };
            s.pens[0] = PlayerPenForMatch();
            s.pens[1] = Catalog.Pen(Catalog.StarterId).Recolor(Pal.H("#2A9D8F"), Pal.H("#16324F"));
            s.names[0] = "You";
            s.names[1] = Catalog.PracticeNames[level] + " bot";
            s.isBot[1] = true;
            StartMatch(s);
        }

        public void StartPassAndPlay()
        {
            var s = new MatchSetup { mode = MatchMode.PassAndPlay };
            s.pens[0] = PlayerPenForMatch();
            s.pens[1] = Catalog.Pen(Catalog.StarterId).Recolor(Pal.H("#C8414B"), Pal.H("#1F2A44"));
            s.names[0] = "Player 1";
            s.names[1] = "Player 2";
            StartMatch(s);
        }

        public void Rematch(MatchSetup old)
        {
            if (old.mode == MatchMode.Practice) StartPractice(old.practiceLevel);
            else if (old.mode == MatchMode.PassAndPlay) StartPassAndPlay();
            else ShowRival();
        }

        bool nameTapHooked;

        /// <summary>Online: tap a classmate's name on the blackboard to see their profile.</summary>
        void OnNameTapped(int i)
        {
            var s = Match.Setup;
            if (s == null || s.mode != MatchMode.Online || s.net == null || i == s.localIndex || i < 0 || i >= s.Players) return;
            Audio.Ui();
            ProfileScreens.ShowOpponent(this, s.net.PlayerIdAt(i), s.names[i], s.pens[i]);
        }

        public void StartMatch(MatchSetup s)
        {
            Paused = false;
            Time.timeScale = 1f;
            UI.ShowHud();
            // Online: every player looks at the desk from their own side (pen at the bottom).
            if (!nameTapHooked) { UI.Hud.NameTapped += OnNameTapped; nameTapHooked = true; }
            Desks.Active = s.mode == MatchMode.Online ? s.desk : null;
            World.SetDesk(Desks.ActiveOrHome, s.IsParty);
            UI.Hud.SetDesk(Desks.Active, s.desk != null ? s.desk.Pot(s.Players) : 0);
            World.Rig.yaw = s.mode == MatchMode.Online ? BenchWorld.YawFor(s.localIndex, s.Players) : 0f;
            World.Rig.Fit();
            Match.Begin(s);
        }

        // ---------- match callbacks ----------

        public void OnRoundOver(RoundSummary sum)
        {
            Input.Disable();
            UI.ShowOverlay(RoundResultView.Build(this, sum, Match.Setup));
        }

        public void OnMatchOver(MatchSetup s, int winner, int[] score, int[] knockOffs = null)
        {
            if (s.mode == MatchMode.Online)
            {
                OnlineMatch.Finish(this, s, winner, score, null);
                return;
            }
            var o = new MatchOutcome { setup = s, winner = winner, score = score, marksBefore = Save.marks };
            bool humanWon = s.mode == MatchMode.PassAndPlay || !s.isBot[winner];
            bool firstLadderWin = false;

            int batchBonus = 0, ownedPenBonus = 0;
            if (s.mode == MatchMode.Ladder && humanWon)
            {
                // Forward only: the rival played is always the current one.
                var rival = s.rival;
                int idx = Ladder.IndexOf(rival);
                firstLadderWin = idx == Save.ladderIndex;
                if (firstLadderWin)
                {
                    if (!Save.Owns(rival.penId))
                    {
                        Save.AddPen(rival.penId);
                        o.wonPen = Catalog.Pen(rival.penId);
                    }
                    else ownedPenBonus = rival.reward / 2;
                    if (Ladder.IsBoss(idx))
                    {
                        Save.stats.bosses++;
                        o.becameChampion = true;   // batch complete
                        o.batchDone = Ladder.BatchOf(idx);
                        batchBonus = Ladder.BatchBonus(o.batchDone);
                    }
                    Save.ladderIndex++;
                }
                o.nextRival = Save.CurrentRival;
            }

            // Marks: see Economy (match win, knock-offs, first win of the day, or 2 for trying).
            int knocks = knockOffs != null ? knockOffs[0] : 0;
            o.rewards = Economy.ForMatch(s, humanWon, knocks, firstLadderWin, Save, batchBonus, ownedPenBonus);
            Save.marks += o.rewards.total;
            Save.Save();

            o.marksAfter = Save.marks;
            if (humanWon) { Audio.Win(); Haptics.Win(); }
            else Audio.Lose();

            World.ClearPens();
            UI.ShowScreen(MatchEndScreen.Build(this, o));
            if (s.mode != MatchMode.PassAndPlay && s.mode != MatchMode.Practice) { Badges.Check(Save); ProfileScreens.ShowPendingBadges(this, 2200); }
            if (humanWon && s.mode != MatchMode.PassAndPlay) Reminders.AskAfterWin(this);
        }

        // ---------- pause ----------

        public void Pause()
        {
            // Online can't stop time (the other phone keeps going): offer to leave instead.
            if (Match.Setup != null && Match.Setup.mode == MatchMode.Online && UI.Hud.Root.style.display == UnityEngine.UIElements.DisplayStyle.Flex)
            {
                UI.ShowOverlay(OnlineMatch.LeaveSheet(this));
                return;
            }
            if (Paused) return;
            Paused = true;
            Time.timeScale = 0f;
            UI.ShowOverlay(UI.Hud.BuildPause());
        }

        public void Resume()
        {
            Paused = false;
            Time.timeScale = 1f;
            UI.HideOverlay();
        }

        public void QuitToHome()
        {
            Paused = false;
            Time.timeScale = 1f;
            ShowHome();
        }

        void OnApplicationPause(bool paused)
        {
            // Going to the background: book the next 7 PM reminders from today's state.
            if (paused && Save != null) Reminders.Reschedule(Save);
            // Back from the share sheet etc. while waiting in a room: reclaim the host seat.
            if (!paused) OnlineRoom.Current?.OnResume();
            if (!paused) Push.CheckTappedLink();
            if (Match != null && Match.Setup != null && Match.Setup.mode == MatchMode.Online)
            {
                if (!paused) OnlineMatch.OnResume();
                return;
            }
            if (paused && Match != null && Match.Shooting == false && UI != null && UI.Hud.Root.style.display == UnityEngine.UIElements.DisplayStyle.Flex)
                Pause();
        }

        void OnDestroy()
        {
            if (I == this)
            {
                GameEvents.ClearAll();
                Time.timeScale = 1f;
                I = null;
            }
        }
    }
}
