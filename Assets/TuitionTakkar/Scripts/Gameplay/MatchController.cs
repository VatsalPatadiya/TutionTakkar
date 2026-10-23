using System.Collections;
using UnityEngine;

namespace TuitionTakkar
{
    public enum MatchMode { Ladder, Practice, PassAndPlay }

    public class MatchSetup
    {
        public MatchMode mode;
        public RivalDef rival;           // ladder only
        public BotProfile bot;           // ladder + practice
        public int practiceLevel;
        public readonly PenDef[] pens = new PenDef[2];
        public readonly string[] names = new string[2];
        public readonly bool[] isBot = new bool[2];
    }

    public class RoundSummary
    {
        public int round;
        public int winner;
        public bool loserFellAlone;   // loser's pen was knocked off
        public bool shooterSelfOut;   // shooter's own pen went off (alone or with the other)
        public bool bothOut;
        public int shots;
        public float winningPower;
        public int[] score;
        public bool matchOver;
    }

    /// <summary>
    /// Rules: flick your own pen. Their pen falls and yours stays: you win the
    /// round. Your pen falls (even together with theirs): you lose it.
    /// A pen only counts as out once it really drops off the bench.
    /// First to 3 rounds wins (best of 5). The loser of a round shoots first next.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        GameManager gm;
        BotBrain brain;
        MatchSetup setup;
        readonly int[] score = new int[2];
        int round;
        int shooter;
        int firstShooter;
        int shots;
        float lastPower;
        bool running;
        float teeterTime;

        public MatchSetup Setup => setup;
        public int Round => round;
        public int[] Score => score;
        public int Shooter => shooter;
        public bool Shooting { get; private set; }

        public void Init(GameManager manager) => gm = manager;

        public void Begin(MatchSetup s)
        {
            Stop();
            setup = s;
            score[0] = score[1] = 0;
            round = 1;
            firstShooter = 0;
            running = true;
            gm.World.SpawnPens(s.pens[0], s.pens[1]);
            // The planner also takes the automatic shot when a human runs out of time,
            // so every mode gets one (including Pass & Play).
            brain = new BotBrain();
            brain.Prepare(s.pens, gm.World.DeskMaterial, gm.World.FloorMaterial);
            StartRound();
        }

        public void Stop()
        {
            StopAllCoroutines();
            turnTimer = null;
            gm?.UI.Hud.HideAutoShot();
            running = false;
            Shooting = false;
            gm?.Input.Disable();
            gm?.Aim.Hide();
            brain?.Dispose();
            brain = null;
            Time.timeScale = 1f;
        }

        void StartRound()
        {
            shots = 0;
            gm.World.ResetPens();
            shooter = firstShooter;
            gm.UI.Hud.SetMatch(setup, score, round);
            BeginTurn();
        }

        void BeginTurn()
        {
            if (!running) return;
            Shooting = false;
            HoldPens(true);
            gm.UI.Hud.SetTurn(setup, shooter);
            if (setup.isBot[shooter]) StartCoroutine(BotTurn());
            else
            {
                gm.Input.Enable(gm.World.Pens[shooter], OnHumanShot);
                turnTimer = StartCoroutine(TurnTimer());
            }
        }

        Coroutine turnTimer;

        /// <summary>
        /// 60 seconds to shoot. Last 10: red "hurry!", a tick and a buzz each second.
        /// At zero: fire the aim being held, else take an automatic (Easy) shot.
        /// Stops while paused.
        /// </summary>
        IEnumerator TurnTimer()
        {
            float left = GameConfig.TurnTimeLimit;
            int lastWhole = Mathf.CeilToInt(left);
            gm.UI.Hud.SetTimer(left, GameConfig.TurnTimeLimit);
            while (left > 0f)
            {
                yield return null;
                if (gm.Paused) continue;
                left -= Time.unscaledDeltaTime;
                gm.UI.Hud.SetTimer(Mathf.Max(0f, left), GameConfig.TurnTimeLimit);
                int whole = Mathf.CeilToInt(left);
                if (whole != lastWhole)
                {
                    lastWhole = whole;
                    if (whole <= GameConfig.TurnTimeUrgent && whole > 0) GameEvents.RaiseTimerTick(whole);
                }
            }
            turnTimer = null;

            if (gm.Input.ReleaseNow()) yield break; // their pull becomes the shot
            gm.Input.Disable();
            gm.UI.Hud.SetTimeUp();
            yield return PlanAndFire(GameConfig.AutoShotProfile, true);
        }

        void StopTurnTimer()
        {
            if (turnTimer != null) StopCoroutine(turnTimer);
            turnTimer = null;
        }

        void OnHumanShot(Shot s)
        {
            StopTurnTimer();
            gm.Input.Disable();
            Fire(s);
        }

        IEnumerator BotTurn()
        {
            yield return new WaitForSeconds(GameConfig.BotThinkMin);
            yield return PlanAndFire(FairBot(setup.bot), false);
        }

        /// <summary>
        /// Keeps every computer rival beatable: for each round the bot leads by, and for a
        /// round that drags on, its aim and power get shakier and it makes more mistakes.
        /// It never gets better than its own profile.
        /// </summary>
        BotProfile FairBot(BotProfile b)
        {
            int lead = Mathf.Max(0, score[shooter] - score[1 - shooter]);
            float tired = Mathf.Max(0, shots - GameConfig.BotTiredAfterShots) * 0.25f;
            float k = Mathf.Min(lead + tired, 3f);
            if (k <= 0f) return b;
            return new BotProfile(
                b.aimNoiseDeg * (1f + GameConfig.BotLeadAimMul * k),
                b.powerNoise + GameConfig.BotLeadPowerNoise * k,
                b.caution * Mathf.Max(0.2f, 1f - GameConfig.BotLeadCalm * k),
                b.aggression,
                Mathf.Min(0.7f, b.mistakeChance + GameConfig.BotLeadMistake * k));
        }

        /// <summary>Plan a shot with the bot planner, show its aim for a moment, then fire it.</summary>
        IEnumerator PlanAndFire(BotProfile profile, bool autoShot)
        {
            Shot plan = default;
            bool got = false;
            yield return brain.Plan(gm.World.Snapshot(), shooter, profile, s => { plan = s; got = true; }, autoShot);
            if (!got) yield break;
            if (autoShot) plan.power = Mathf.Min(plan.power, GameConfig.AutoShotMaxPower);

            // Show the aim briefly so the shot feels fair and readable.
            var pen = gm.World.Pens[shooter];
            float telegraph = autoShot ? GameConfig.AutoShotTelegraph : GameConfig.BotTelegraph;
            float t = 0f;
            while (t < telegraph)
            {
                t += Time.deltaTime;
                float grow = Mathf.SmoothStep(0f, 1f, t / telegraph);
                var grab = Flick.StrikePoint(pen.Def, pen.Rb.position, pen.Rb.rotation, plan.strikeT);
                gm.Aim.Show(grab, null, plan.dir, plan.power * grow, pen.Length * 0.5f);
                gm.UI.Hud.SetPower(plan.power * grow, plan.strikeT);
                if (autoShot) gm.UI.Hud.ShowAutoShot(plan.power, gm.UI.Hud.PanelPoint(gm.World.Cam, pen.Rb.position));
                yield return null;
            }
            gm.Aim.Hide();
            gm.UI.Hud.HideAutoShot();
            Fire(plan);
        }

        void Fire(Shot s)
        {
            shots++;
            lastPower = s.power;
            Shooting = true;
            gm.UI.Hud.SetPower(0f, 0f);
            HoldPens(false);
            shotStart[0] = gm.World.Pens[0].Rb.position;
            shotStart[1] = gm.World.Pens[1].Rb.position;
            lastImpulse = Flick.Impulse(gm.World.Pens[shooter].Def, s.power);
            Flick.Apply(gm.World.Pens[shooter].Rb, gm.World.Pens[shooter].Def, s);
            GameEvents.RaiseFlicked(s.power);
            StartCoroutine(WaitSettle());
        }

        IEnumerator WaitSettle()
        {
            yield return new WaitForFixedUpdate();
            float quiet = 0f, t = 0f;
            teeterTime = 0f;
            while (true)
            {
                if (gm.Paused)
                {
                    yield return null;
                    continue;
                }
                t += Time.unscaledDeltaTime;
                UpdateTeeter();
                quiet = gm.World.AnyMoving() ? 0f : quiet + Time.unscaledDeltaTime;
                float hold = gm.World.AnyOverhanging() ? GameConfig.SettleHoldOverhang : GameConfig.SettleHold;
                if (quiet >= hold || t >= GameConfig.SettleTimeout) break;
                yield return null;
            }
            Time.timeScale = 1f;
            gm.Rig.SetPushIn(0f);
            Resolve();
        }

        /// <summary>Slow time down for a moment when a pen hangs over the edge.</summary>
        void UpdateTeeter()
        {
            bool teeter = false;
            foreach (var p in gm.World.Pens)
                if (gm.World.IsOverhanging(p) && p.Rb.linearVelocity.magnitude < 3f) teeter = true;

            if (teeter && teeterTime < GameConfig.TeeterMaxSeconds)
            {
                teeterTime += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, GameConfig.TeeterSlowMo, Time.unscaledDeltaTime * 4f);
                gm.Rig.SetPushIn(1.2f);
            }
            else
            {
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 3f);
                gm.Rig.SetPushIn(0f);
            }
        }

        void Resolve()
        {
            Shooting = false;
            var w = gm.World;
            bool selfOut = w.IsOut(w.Pens[shooter]);
            bool oppOut = w.IsOut(w.Pens[1 - shooter]);
            LogShot(selfOut, oppOut);

            if (!selfOut && !oppOut)
            {
                StartCoroutine(SeparateThenNextTurn());
                return;
            }

            int winner = selfOut ? 1 - shooter : shooter;
            score[winner]++;
            firstShooter = 1 - winner;

            var sum = new RoundSummary
            {
                round = round,
                winner = winner,
                shooterSelfOut = selfOut,
                bothOut = selfOut && oppOut,
                loserFellAlone = !selfOut && oppOut,
                shots = shots,
                winningPower = lastPower,
                score = new[] { score[0], score[1] },
                matchOver = score[winner] >= GameConfig.RoundsToWin
            };
            gm.UI.Hud.SetMatch(setup, score, round);
            gm.OnRoundOver(sum);
        }

        /// <summary>
        /// Between shots both pens are held still (kinematic): while someone aims, nothing may
        /// creep, drift or tip off the desk. They are let go the instant a flick is fired.
        /// </summary>
        void HoldPens(bool hold)
        {
            foreach (var p in gm.World.Pens)
            {
                if (p == null || gm.World.IsOut(p)) continue;
                var rb = p.Rb;
                if (hold)
                {
                    if (rb.isKinematic) continue;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                else if (rb.isKinematic)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.WakeUp();
                }
            }
        }

        /// <summary>Pens left touching are slid a few millimetres apart, then the other player shoots.</summary>
        IEnumerator SeparateThenNextTurn()
        {
            var w = gm.World;
            if (GameConfig.SeparateTouchingPens && w.SeparationNeeded(GameConfig.MinPenGap, out var push0, out var push1))
            {
                var pushes = new[] { push0, push1 };
                var from = new[] { w.Pens[0].Rb.position, w.Pens[1].Rb.position };
                foreach (var p in w.Pens) p.Rb.isKinematic = true;
                float t = 0f;
                while (t < GameConfig.SeparateSeconds)
                {
                    t += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, t / GameConfig.SeparateSeconds);
                    for (int i = 0; i < 2; i++) w.Pens[i].Rb.MovePosition(from[i] + pushes[i] * k);
                    yield return null;
                }
                for (int i = 0; i < 2; i++)
                {
                    var rb = w.Pens[i].Rb;
                    rb.position = from[i] + pushes[i];
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                yield return new WaitForFixedUpdate();
            }
            if (!running) yield break;
            shooter = 1 - shooter;
            BeginTurn();
        }

        readonly Vector3[] shotStart = new Vector3[2];
        float lastImpulse;

        /// <summary>One Console line per shot, so knock-offs can be tuned from real play.</summary>
        void LogShot(bool selfOut, bool oppOut)
        {
            var w = gm.World;
            int other = 1 - shooter;
            var me = w.Pens[shooter].Rb.position;
            var them = w.Pens[other].Rb.position;
            float gap = Vector3.Distance(me, them) * 10f;
            gm.UI.Hud.SetDebugShot(string.Format(
                "#{0} {1} {2:0}% | own moved {3:0.0}cm, edge {4:0.0}cm{5}\nother moved {6:0.0}cm, edge {7:0.0}cm{8} | pens {9:0.0}cm apart",
                shots, setup.names[shooter].ToUpperInvariant(), lastPower * 100f,
                Vector3.Distance(me, shotStart[shooter]) * 10f, BenchWorld.EdgeDistance(me) * 10f, selfOut ? " OUT" : "",
                Vector3.Distance(them, shotStart[other]) * 10f, BenchWorld.EdgeDistance(them) * 10f, oppOut ? " OUT" : "", gap));
            Debug.Log(string.Format(
                "[TuitionTakkar] {0} shot at {1:0}% (impulse {2:0.000}) | own pen moved {3:0.0} cm, edge gap {4:0.0} cm, out={5} | other pen moved {6:0.0} cm, edge gap {7:0.0} cm, out={8}",
                setup.names[shooter], lastPower * 100f, lastImpulse,
                Vector3.Distance(me, shotStart[shooter]) * 10f, BenchWorld.EdgeDistance(me) * 10f, selfOut,
                Vector3.Distance(them, shotStart[other]) * 10f, BenchWorld.EdgeDistance(them) * 10f, oppOut));
        }

        /// <summary>Called by the round-result screen's button.</summary>
        public void Continue()
        {
            if (!running) return;
            if (score[0] >= GameConfig.RoundsToWin || score[1] >= GameConfig.RoundsToWin)
            {
                int winner = score[0] >= GameConfig.RoundsToWin ? 0 : 1;
                var s = setup;
                int[] final = { score[0], score[1] };
                Stop();
                gm.OnMatchOver(s, winner, final);
                return;
            }
            round++;
            StartRound();
        }
    }
}
