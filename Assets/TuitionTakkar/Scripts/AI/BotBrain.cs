using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TuitionTakkar
{
    /// <summary>
    /// The bot tries a spread of flicks in a hidden copy of the bench (its own
    /// physics scene), scores where the pens end up, picks one, then adds human
    /// error based on the rival's profile.
    /// </summary>
    public class BotBrain : IDisposable
    {
        Scene scene;
        PhysicsScene physics;
        readonly Rigidbody[] sim = new Rigidbody[2];
        PenDef[] defs;
        bool ready;

        struct Candidate
        {
            public Shot shot;
            public float score;
        }

        public void Prepare(PenDef[] penDefs, PhysicsMaterial deskMat, PhysicsMaterial floorMat)
        {
            Dispose();
            defs = penDefs;
            scene = SceneManager.CreateScene("TT_BotSim_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();

            var desk = new GameObject("SimDesk");
            SceneManager.MoveGameObjectToScene(desk, scene);
            desk.transform.position = new Vector3(0f, -GameConfig.DeskThickness * 0.5f, 0f);
            var dc = desk.AddComponent<BoxCollider>();
            dc.size = new Vector3(GameConfig.DeskWidth, GameConfig.DeskThickness, GameConfig.DeskDepth);
            dc.sharedMaterial = deskMat;

            var floor = new GameObject("SimFloor");
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(0f, -GameConfig.DeskHeight - 0.5f, 0f);
            var fc = floor.AddComponent<BoxCollider>();
            fc.size = new Vector3(60f, 1f, 60f);
            fc.sharedMaterial = floorMat;

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("SimPen" + i);
                SceneManager.MoveGameObjectToScene(go, scene);
                sim[i] = PenFactory.BuildPhysics(go, defs[i], false);
            }
            ready = true;
        }

        public void Dispose()
        {
            if (ready && scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            ready = false;
        }

        static float Gauss()
        {
            // Box-Muller, clamped so a bot never does something absurd.
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value);
            float u2 = UnityEngine.Random.value;
            float g = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
            return Mathf.Clamp(g, -2.5f, 2.5f);
        }

        static Vector3 DirFromAngle(float deg) =>
            new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad));

        /// <summary>
        /// Plans a shot within a time budget: the most likely flicks are tried first, at most
        /// a few milliseconds of simulation per frame, and planning stops once the budget is
        /// spent (keeping the best shot found so far). Quick mode is for the auto shot.
        /// </summary>
        public IEnumerator Plan(PenState[] start, int shooter, BotProfile bp, Action<Shot> done, bool quick = false)
        {
            int other = 1 - shooter;
            Vector3 me = start[shooter].pos, opp = start[other].pos;
            Vector3 to = opp - me;
            to.y = 0f;
            float baseAng = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;

            var list = new List<Candidate>();
            float[] angles = { -24f, -16f, -8f, -3f, 0f, 3f, 8f, 16f, 24f };
            float[] strikes = { -0.55f, 0f, 0.55f };
            float[] powers = { 0.3f, 0.5f, 0.7f, 0.85f, 1f };

            if (quick)
            {
                // Auto shot for an idle player: a few gentle options, scored with full caution.
                angles = new[] { -10f, -4f, 0f, 4f, 10f };
                strikes = new[] { 0f, 0.45f };
                powers = new[] { 0.3f, 0.42f, 0.55f };
            }

            foreach (var a in angles)
            foreach (var s in strikes)
            foreach (var p in powers)
                list.Add(new Candidate { shot = new Shot { dir = DirFromAngle(baseAng + a), strikeT = s, power = p } });
            // Most promising first (aimed straight at the other pen, firm power), so a
            // budget cut-off still leaves the good options evaluated.
            list.Sort((x, y) => Priority(x.shot, baseAng).CompareTo(Priority(y.shot, baseAng)));

            // "Play it safe" moves back toward the middle, only for cautious rivals
            // or when the bot's own pen is genuinely near an edge.
            Vector3 home = -me;
            home.y = 0f;
            bool wantsSafety = bp.caution >= 0.6f || BenchWorld.Danger(me) > 0.45f;
            if (wantsSafety && home.sqrMagnitude > 0.01f)
            {
                float homeAng = Mathf.Atan2(home.x, home.z) * Mathf.Rad2Deg;
                foreach (var p in new[] { 0.25f, 0.4f, 0.55f })
                    list.Add(new Candidate { shot = new Shot { dir = DirFromAngle(homeAng), strikeT = 0f, power = p } });
            }

            const float frameBudgetMs = 6f;
            float totalBudgetMs = quick ? GameConfig.AutoShotThinkMs : GameConfig.BotThinkMaxMs;
            var total = System.Diagnostics.Stopwatch.StartNew();
            var frame = System.Diagnostics.Stopwatch.StartNew();
            int evaluated = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                c.score = ready ? Evaluate(start, shooter, c.shot, bp) : 0f;
                list[i] = c;
                evaluated = i + 1;
                if (total.Elapsed.TotalMilliseconds >= totalBudgetMs) break;
                if (frame.Elapsed.TotalMilliseconds >= frameBudgetMs)
                {
                    yield return null;
                    frame.Restart();
                }
            }
            if (evaluated < list.Count) list.RemoveRange(evaluated, list.Count - evaluated);

            list.Sort((x, y) => y.score.CompareTo(x.score));
            int pick = 0;
            bool mistake = UnityEngine.Random.value < bp.mistakeChance;
            // A real mistake: any of the top 40% of ideas, not just the second-best one.
            if (mistake && list.Count > 1) pick = UnityEngine.Random.Range(1, Mathf.Max(2, Mathf.CeilToInt(list.Count * 0.4f)));
            var chosen = list[pick].shot;
            // Nerves: sometimes a mistake is an over-hit, which can send the bot's own pen off.
            if (mistake && !quick && UnityEngine.Random.value < 0.5f) chosen.power = Mathf.Min(1f, chosen.power + 0.2f);

            // Human error.
            float ang = Mathf.Atan2(chosen.dir.x, chosen.dir.z) * Mathf.Rad2Deg + Gauss() * bp.aimNoiseDeg;
            chosen.dir = DirFromAngle(ang);
            chosen.power = Mathf.Clamp(chosen.power + Gauss() * bp.powerNoise, 0.08f, 1f);
            chosen.strikeT = Mathf.Clamp(chosen.strikeT + Gauss() * 0.08f, -1f, 1f);
            done(chosen);
        }

        static float Priority(Shot s, float baseAng)
        {
            float ang = Mathf.Atan2(s.dir.x, s.dir.z) * Mathf.Rad2Deg;
            float off = Mathf.Abs(Mathf.DeltaAngle(ang, baseAng));
            return off + Mathf.Abs(s.strikeT) * 6f + Mathf.Abs(s.power - 0.75f) * 10f;
        }

        float Evaluate(PenState[] start, int shooter, Shot shot, BotProfile bp)
        {
            int other = 1 - shooter;
            for (int i = 0; i < 2; i++)
            {
                var rb = sim[i];
                rb.transform.SetPositionAndRotation(start[i].pos, start[i].rot);
                rb.position = start[i].pos;
                rb.rotation = start[i].rot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                PenFactory.Flatten(rb);
                rb.WakeUp();
            }

            Flick.Apply(sim[shooter], defs[shooter], shot);

            int quiet = 0;
            for (int step = 0; step < 360; step++) // 1.5 s of play at 240 Hz
            {
                physics.Simulate(GameConfig.FixedDt);
                for (int i = 0; i < 2; i++)
                {
                    PenFactory.ReleaseIfOverEdge(sim[i], defs[i]);
                    PenFactory.KeepOnDesk(sim[i], defs[i]);
                }
                bool still = true;
                for (int i = 0; i < 2; i++)
                {
                    var rb = sim[i];
                    if (rb.position.y < -GameConfig.OutDepth) continue;
                    if (rb.linearVelocity.magnitude > GameConfig.SettleLinear || rb.angularVelocity.magnitude > GameConfig.SettleAngular)
                        still = false;
                }
                quiet = still ? quiet + 1 : 0;
                if (quiet > 20) break;
                // Outcome already decided once a pen has dropped off.
                if (sim[0].position.y < -GameConfig.OutDepth || sim[1].position.y < -GameConfig.OutDepth) break;
            }

            Vector3 selfEnd = sim[shooter].position;
            Vector3 oppEnd = sim[other].position;
            bool selfOut = selfEnd.y < -GameConfig.OutDepth;
            bool oppOut = oppEnd.y < -GameConfig.OutDepth;

            if (selfOut) return -1000f - shot.power * 10f;
            if (oppOut) return 1000f - shot.power * 5f;

            float moved = Vector3.Distance(oppEnd, start[other].pos);
            float score = 0f;
            score += 220f * BenchWorld.Danger(oppEnd) * (0.5f + bp.aggression);
            score -= 260f * BenchWorld.Danger(selfEnd) * (0.5f + bp.caution);
            if (moved > 0.05f) score += 30f * bp.aggression;
            return score;
        }
    }
}
