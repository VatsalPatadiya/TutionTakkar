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
            ShowHome();
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
        }

        public void ShowRival()
        {
            Match.Stop();
            UI.ShowScreen(RivalScreen.Build(this, Save.CurrentRival));
        }

        public void ShowPouch()
        {
            Match.Stop();
            World.ClearPens();
            UI.ShowScreen(PouchScreen.Build(this));
        }

        public void StartLadder(RivalDef rival)
        {
            var s = new MatchSetup { mode = MatchMode.Ladder, rival = rival, bot = rival.bot };
            s.pens[0] = Catalog.Pen(Save.equipped);
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
            s.pens[0] = Catalog.Pen(Save.equipped);
            s.pens[1] = Catalog.Pen(Catalog.StarterId).Recolor(Pal.H("#2A9D8F"), Pal.H("#16324F"));
            s.names[0] = "You";
            s.names[1] = Catalog.PracticeNames[level] + " bot";
            s.isBot[1] = true;
            StartMatch(s);
        }

        public void StartPassAndPlay()
        {
            var s = new MatchSetup { mode = MatchMode.PassAndPlay };
            s.pens[0] = Catalog.Pen(Save.equipped);
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

        void StartMatch(MatchSetup s)
        {
            Paused = false;
            Time.timeScale = 1f;
            UI.ShowHud();
            World.Rig.Fit();
            Match.Begin(s);
        }

        // ---------- match callbacks ----------

        public void OnRoundOver(RoundSummary sum)
        {
            Input.Disable();
            UI.ShowOverlay(RoundResultView.Build(this, sum, Match.Setup));
        }

        public void OnMatchOver(MatchSetup s, int winner, int[] score)
        {
            var o = new MatchOutcome { setup = s, winner = winner, score = score, marksBefore = Save.marks };
            bool humanWon = s.mode == MatchMode.PassAndPlay || !s.isBot[winner];

            if (s.mode == MatchMode.Ladder)
            {
                var rival = s.rival;
                if (humanWon)
                {
                    bool firstWin = !Save.Owns(rival.penId);
                    if (firstWin)
                    {
                        Save.owned.Add(rival.penId);
                        o.wonPen = Catalog.Pen(rival.penId);
                        Save.marks += rival.reward;
                    }
                    else Save.marks += rival.reward / 2;

                    int idx = Catalog.Rivals.IndexOf(rival);
                    if (idx == Save.ladderIndex) Save.ladderIndex++;
                    o.becameChampion = firstWin && idx == Catalog.Rivals.Count - 1;
                    o.nextRival = Save.LadderComplete ? null : Save.CurrentRival;
                }
                else Save.marks = Mathf.Max(0, Save.marks - Catalog.LossPenalty);
                Save.Save();
            }

            o.marksAfter = Save.marks;
            if (humanWon) { Audio.Win(); Haptics.Win(); }
            else Audio.Lose();

            World.ClearPens();
            UI.ShowScreen(MatchEndScreen.Build(this, o));
        }

        // ---------- pause ----------

        public void Pause()
        {
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
