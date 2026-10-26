using System.Collections;
using UnityEngine;

namespace TuitionTakkar
{
    public enum MatchMode { Ladder, Practice, PassAndPlay, Online }

    public class MatchSetup
    {
        public MatchSetup() : this(2) { }

        public MatchSetup(int players)
        {
            pens = new PenDef[players];
            names = new string[players];
            isBot = new bool[players];
        }

        public int Players => pens.Length;
        /// <summary>3–4 player online match: one round, last pen standing.</summary>
        public bool IsParty => pens.Length > 2;

        public MatchMode mode;
        public RivalDef rival;           // ladder only
        public BotProfile bot;           // ladder + practice
        public int practiceLevel;
        public readonly PenDef[] pens;
        public readonly string[] names;
        public readonly bool[] isBot;
        /// <summary>Online: which pen index is this phone's player (0 or 1).</summary>
        public int localIndex;
        /// <summary>Online: the room link that sends and receives shots. Null offline.</summary>
        public OnlineLink net;
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
        public bool Running => running;

        public void Init(GameManager manager) => gm = manager;

        bool Online => setup != null && setup.mode == MatchMode.Online && setup.net != null;
        /// <summary>This phone shoots now (always true offline: hot-seat or vs bot handled elsewhere).</summary>
        bool LocalTurn => !Online || shooter == setup.localIndex;
        float TurnLimit => Online ? GameConfig.OnlineTurnTimeLimit : GameConfig.TurnTimeLimit;
        /// <summary>Shots fired this match; online messages are matched by this number.</summary>
        int turnNo;
        /// <summary>Online: auto shots this phone's player has used this match.</summary>
        int autoShotsUsed;

        public void Begin(MatchSetup s)
        {
            Stop();
            setup = s;
            score[0] = score[1] = 0;
            knockOffs[0] = knockOffs[1] = 0;
            round = 1;
            firstShooter = 0;
            turnNo = 0;
            autoShotsUsed = 0;
            running = true;
            gm.World.SpawnPens(s.pens);
            if (s.IsParty)
            {
                alive = new bool[s.Players];
                place = new int[s.Players];
                for (int i = 0; i < alive.Length; i++) alive[i] = true;
                aliveCount = s.Players;
            }
            else
            {
                // The planner also takes the automatic shot when a human runs out of time,
                // so every mode gets one (including Pass & Play).
                brain = new BotBrain();
                brain.Prepare(s.pens, gm.World.DeskMaterial, gm.World.FloorMaterial);
            }
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
            if (Online) gm.World.ResetPens(setup.net.JitterFor(round)); // same start on every phone
            else gm.World.ResetPens();
            shooter = firstShooter;
            if (setup.IsParty) gm.UI.Hud.SetParty(setup, alive, place);
            else gm.UI.Hud.SetMatch(setup, score, round);
            BeginTurn();
        }

        void BeginTurn()
        {
            if (!running) return;
            Shooting = false;
            HoldPens(true);
            gm.UI.Hud.SetTurn(setup, shooter);
            if (Online && !LocalTurn) turnTimer = StartCoroutine(RemoteTurn());
            else if (setup.isBot[shooter]) StartCoroutine(BotTurn());
            else
            {
                gm.Input.Enable(gm.World.Pens[shooter], OnHumanShot);
                turnTimer = StartCoroutine(TurnTimer());
            }
        }

        Coroutine turnTimer;
#if UNITY_EDITOR
        /// <summary>Editor test menu: set the running turn clock to this many seconds.</summary>
        public static float DevSetSeconds;
#endif

        /// <summary>
        /// 60 seconds to shoot. Last 10: red "hurry!", a tick and a buzz each second.
        /// At zero: fire the aim being held, else take an automatic (Easy) shot.
        /// Stops while paused.
        /// </summary>
        IEnumerator TurnTimer()
        {
            float left = TurnLimit;
            int lastWhole = Mathf.CeilToInt(left);
            gm.UI.Hud.SetTimer(left, TurnLimit);
            while (left > 0f)
            {
                yield return null;
                if (gm.Paused && !Online) continue;
                left -= Time.unscaledDeltaTime;
#if UNITY_EDITOR
                if (DevSetSeconds > 0f) { left = DevSetSeconds; DevSetSeconds = 0f; } // editor test menu: set the clock
#endif
                gm.UI.Hud.SetTimer(Mathf.Max(0f, left), TurnLimit);
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
            if (Online)
            {
                // Online, everyone else waits on this phone: only a few auto shots per match.
                autoShotsUsed++;
                if (autoShotsUsed > GameConfig.OnlineMaxAutoShots)
                {
                    setup.net.ForfeitSelf(autoShotsUsed);
                    yield break;
                }
            }
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
            if (brain != null)
                yield return brain.Plan(gm.World.Snapshot(), shooter, profile, s => { plan = s; got = true; }, autoShot);
            else
            {
                plan = PartyAutoShot();
                got = true;
            }
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
                if (autoShot) gm.UI.Hud.ShowAutoShot(plan.power, gm.UI.Hud.PanelPoint(gm.World.Cam, pen.Rb.position),
                    Online ? GameConfig.OnlineMaxAutoShots - autoShotsUsed : -1);
                yield return null;
            }
            gm.Aim.Hide();
            gm.UI.Hud.HideAutoShot();
            Fire(plan);
        }

        void Fire(Shot s)
        {
            turnNo++;
            if (Online && LocalTurn) setup.net.SendShot(turnNo, shooter, gm.World, s);
            shots++;
            lastPower = s.power;
            Shooting = true;
            gm.UI.Hud.SetPower(0f, 0f);
            HoldPens(false);
            if (!setup.IsParty)
            {
                shotStart[0] = gm.World.Pens[0].Rb.position;
                shotStart[1] = gm.World.Pens[1].Rb.position;
            }
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
            if (Online)
            {
                yield return SyncRest();
                if (!running) yield break;
            }
            Resolve();
        }

        // ---------- online ----------

        /// <summary>
        /// The other phone's turn: show their clock, and when their shot arrives, put the pens
        /// exactly where they were on that phone and play the same flick here.
        /// </summary>
        IEnumerator RemoteTurn()
        {
            int expect = turnNo + 1;
            float waited = 0f;
            while (running)
            {
                if (setup.net.TryGetShot(expect, shooter, out var states, out var shot))
                {
                    turnTimer = null;
                    gm.UI.Hud.SetTimer(0f, TurnLimit);
                    OnlineLink.Apply(gm.World, states);
                    Fire(shot);
                    yield break;
                }
                waited += Mathf.Min(Time.unscaledDeltaTime, 0.25f); // a phone coming back from sleep isn't "waiting"
                gm.UI.Hud.SetTimer(Mathf.Max(0f, TurnLimit - waited), TurnLimit);
                setup.net.Nudge(waited);
                if (waited > TurnLimit + GameConfig.OnlineAbsentGrace)
                {
                    turnTimer = null;
                    setup.net.OpponentTimedOut(shooter);
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// After a shot settles, the shooter's phone is the referee: it sends where every pen
        /// ended up, and the other phone moves its pens to match before the round is judged.
        /// </summary>
        IEnumerator SyncRest()
        {
            if (LocalTurn)
            {
                var mine = OnlineLink.Capture(gm.World);
                setup.net.SendRest(turnNo, shooter, mine);
                OnlineLink.Apply(gm.World, mine); // zero the leftover drift exactly like the other phone does
                yield return new WaitForFixedUpdate();
                yield break;
            }
            float waited = 0f;
            while (running)
            {
                if (setup.net.TryGetRest(turnNo, shooter, out var states))
                {
                    OnlineLink.Apply(gm.World, states);
                    yield return new WaitForFixedUpdate();
                    yield break;
                }
                waited += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
                setup.net.Nudge(waited);
                if (waited > GameConfig.OnlineAbsentGrace)
                {
                    setup.net.OpponentTimedOut(shooter);
                    yield break;
                }
                yield return null;
            }
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
                gm.Rig.SetPushIn(0f); // no zoom: the whole desk stays framed the same (slow motion only)
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
            if (setup.IsParty)
            {
                ResolveParty();
                return;
            }
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
            if (!selfOut && oppOut) knockOffs[shooter]++; // a real knock-off, not the other pen's own mistake
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

        // ---------- 3–4 players: last pen standing ----------

        bool[] alive;
        int[] place;      // finishing place per pen index (1 = winner), 0 while still in
        int aliveCount;

        public bool[] Alive => alive;
        public int[] Places => place;

        /// <summary>
        /// Every pen that fell this shot is out. The shooter's own pen counts as falling first,
        /// so if the shooter goes over together with the last other pen, the other one wins.
        /// </summary>
        void ResolveParty()
        {
            var w = gm.World;
            var fell = new System.Collections.Generic.List<int>();
            if (alive[shooter] && w.IsOut(w.Pens[shooter])) fell.Add(shooter);
            for (int i = 0; i < alive.Length; i++)
                if (i != shooter && alive[i] && w.IsOut(w.Pens[i])) fell.Add(i);

            foreach (int i in fell)
            {
                place[i] = aliveCount;
                alive[i] = false;
                aliveCount--;
                gm.UI.Hud.ShowEliminated(HudView.DisplayName(setup, i), place[i], i == setup.localIndex);
            }
            if (fell.Count > 0) gm.Audio.Stamp();

            if (aliveCount <= 1)
            {
                // The last pen standing wins. If the final pens all fell together, the last one
                // to be counted out (never the shooter) takes first place.
                int winner = -1;
                for (int i = 0; i < alive.Length; i++) if (alive[i]) winner = i;
                if (winner < 0 && fell.Count > 0) winner = fell[fell.Count - 1];
                if (winner >= 0) place[winner] = 1;
                gm.UI.Hud.SetParty(setup, alive, place);
                var s = setup;
                var places = (int[])place.Clone();
                int win = winner;
                Stop();
                OnlineMatch.FinishParty(gm, s, places, win, null);
                return;
            }

            gm.UI.Hud.SetParty(setup, alive, place);
            StartCoroutine(SeparateThenNextTurn());
        }

        /// <summary>A player left or stopped answering in a 3–4 player match: their pen is out.</summary>
        public void RemovePartyPlayer(int index)
        {
            if (!running || setup == null || !setup.IsParty || !alive[index]) return;
            place[index] = aliveCount;
            alive[index] = false;
            aliveCount--;
            var pen = gm.World.Pens[index];
            pen.ApplyNetState(pen.Rb.position + Vector3.down * 20f, pen.Rb.rotation, false, true);
            gm.UI.Hud.ShowEliminated(HudView.DisplayName(setup, index), place[index], false);
            if (aliveCount <= 1)
            {
                int winner = System.Array.IndexOf(alive, true);
                if (winner >= 0) place[winner] = 1;
                var s = setup;
                var places = (int[])place.Clone();
                Stop();
                OnlineMatch.FinishParty(gm, s, places, winner, null);
                return;
            }
            gm.UI.Hud.SetParty(setup, alive, place);
            if (index == shooter && !Shooting)
            {
                StopAllCoroutines();
                turnTimer = null;
                gm.Input.Disable();
                gm.Aim.Hide();
                shooter = NextAlive(shooter);
                BeginTurn();
            }
        }

        int NextAlive(int from)
        {
            int n = setup.Players;
            for (int k = 1; k <= n; k++)
            {
                int i = (from + k) % n;
                if (alive[i]) return i;
            }
            return from;
        }

        /// <summary>Time ran out in a party match: a gentle flick toward the nearest pen still in.</summary>
        Shot PartyAutoShot()
        {
            var w = gm.World;
            var me = w.Pens[shooter].Rb.position;
            Vector3 best = -me;
            float bestD = float.MaxValue;
            for (int i = 0; i < w.Pens.Length; i++)
            {
                if (i == shooter || !alive[i]) continue;
                var d = w.Pens[i].Rb.position - me;
                d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = d; }
            }
            best.y = 0f;
            if (best.sqrMagnitude < 1e-6f) best = Vector3.forward;
            return new Shot { strikeT = 0f, dir = best.normalized, power = GameConfig.AutoShotMaxPower * 0.8f };
        }

        /// <summary>Pens left touching are slid a few millimetres apart, then the next player shoots.</summary>
        IEnumerator SeparateThenNextTurn()
        {
            if (setup.IsParty)
            {
                yield return SeparateParty();
                if (!running) yield break;
                shooter = NextAlive(shooter);
                BeginTurn();
                yield break;
            }
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

        /// <summary>Party desk: open up every touching pair (same steps on every phone).</summary>
        IEnumerator SeparateParty()
        {
            var w = gm.World;
            int n = w.Pens.Length;
            if (!GameConfig.SeparateTouchingPens) n = 0;
            var push = new Vector3[n];
            bool any = false;
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                {
                    if (!alive[a] || !alive[b]) continue;
                    if (w.SeparationNeeded(w.Pens[a], w.Pens[b], GameConfig.MinPenGap, out var pa, out var pb))
                    {
                        push[a] += pa;
                        push[b] += pb;
                        any = true;
                    }
                }
            if (!any) yield break;
            var from = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                from[i] = w.Pens[i].Rb.position;
                if (alive[i]) w.Pens[i].Rb.isKinematic = true;
            }
            float t = 0f;
            while (t < GameConfig.SeparateSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / GameConfig.SeparateSeconds);
                for (int i = 0; i < n; i++) if (alive[i]) w.Pens[i].Rb.MovePosition(from[i] + push[i] * k);
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                if (!alive[i]) continue;
                var rb = w.Pens[i].Rb;
                rb.position = from[i] + push[i];
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            yield return new WaitForFixedUpdate();
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

        /// <summary>Pens each player knocked off the desk this match (for the knock-off bonus).</summary>
        readonly int[] knockOffs = new int[2];

        /// <summary>Called by the round-result screen's button.</summary>
        public void Continue()
        {
            if (!running) return;
            if (score[0] >= GameConfig.RoundsToWin || score[1] >= GameConfig.RoundsToWin)
            {
                int winner = score[0] >= GameConfig.RoundsToWin ? 0 : 1;
                var s = setup;
                int[] final = { score[0], score[1] };
                int[] knocks = { knockOffs[0], knockOffs[1] };
                Stop();
                gm.OnMatchOver(s, winner, final, knocks);
                return;
            }
            round++;
            StartRound();
        }
    }
}
