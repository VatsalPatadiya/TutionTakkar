using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Tuition Takkar ▸ Feel Lab: runs standard flicks in a hidden physics scene with the game's
    /// real pens, desk material and flick maths, and writes how the pens slide, spin and stop
    /// to Builds/feel-lab.txt. Used to tune the motion against real pen-fight footage.
    /// </summary>
    static class FeelLab
    {
        const string Out = "Builds/feel-lab.txt";
        static float W => GameConfig.WorldScale;
        static float Cm(float units) => units / W * 100f;

        [MenuItem("Tuition Takkar/Feel Lab (measure pen motion)")]
        static void Run()
        {
            // Same physics settings as the game (GameManager.ConfigureRuntime), restored afterwards.
            var g = Physics.gravity; int it = Physics.defaultSolverIterations, vit = Physics.defaultSolverVelocityIterations;
            float off = Physics.defaultContactOffset, bt = Physics.bounceThreshold;
            Physics.gravity = GameConfig.Gravity;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.defaultContactOffset = 0.002f;
            Physics.bounceThreshold = 0.6f;
            try { Measure(); }
            finally
            {
                Physics.gravity = g; Physics.defaultSolverIterations = it; Physics.defaultSolverVelocityIterations = vit;
                Physics.defaultContactOffset = off; Physics.bounceThreshold = bt;
            }
        }

        static void Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Feel Lab  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("desk friction " + GameConfig.DeskFriction + ", pen damping " + GameConfig.PenLinearDamping + ", impulse max " + GameConfig.ImpulseMax / W);
            sb.AppendLine();

            var pen = Catalog.Pen(Catalog.StarterId);

            sb.AppendLine("== Solo flick, centre hit (starter pen) ==");
            foreach (float p in new[] { 0.25f, 0.5f, 0.75f, 1f })
                Solo(sb, pen, p, 0f, true);

            sb.AppendLine();
            sb.AppendLine("== Solo flick, near the end of the pen (strike 0.85) ==");
            foreach (float p in new[] { 0.3f, 0.6f })
                Solo(sb, pen, p, 0.85f, false);

            sb.AppendLine();
            sb.AppendLine("== Pen hits pen: shooter 7 cm away, both side-on, centre to centre ==");
            foreach (float p in new[] { 0.35f, 0.6f, 1f })
                Hit(sb, pen, p, 0f);

            sb.AppendLine();
            sb.AppendLine("== Pen hits pen off-centre: target shifted 4.5 cm along its length ==");
            foreach (float p in new[] { 0.35f, 0.6f, 1f })
                Hit(sb, pen, p, 0.045f * W);

            sb.AppendLine();
            sb.AppendLine("== Other pens, solo 60% centre ==");
            foreach (var d in Catalog.Pens) Solo(sb, d, 0.6f, 0f, false);

            Directory.CreateDirectory("Builds");
            File.WriteAllText(Out, sb.ToString());
            Debug.Log("[TuitionTakkar] Feel Lab written to " + Path.GetFullPath(Out));
        }

        // ---------------------------------------------------------------- sweep

        /// <summary>
        /// Tries desk grip x flick strength combinations (desk friction multiplies with each pen's
        /// own grip) and prints, per combo, how far and how long flicks and hits run.
        /// Target from real footage: a flick runs ~0.4-0.6 s, a full one ~30-35 cm.
        /// </summary>
        [MenuItem("Tuition Takkar/Feel Lab Sweep")]
        static void Sweep()
        {
            var g = Physics.gravity; int it = Physics.defaultSolverIterations, vit = Physics.defaultSolverVelocityIterations;
            float off = Physics.defaultContactOffset, bt = Physics.bounceThreshold;
            Physics.gravity = GameConfig.Gravity;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.defaultContactOffset = 0.002f;
            Physics.bounceThreshold = 0.6f;
            var sb = new StringBuilder();
            try
            {
                var pen = Catalog.Pen(Catalog.StarterId);
                foreach (float deskMul in new[] { 0.25f, 0.35f, 0.45f, 0.6f })
                {
                    var mat = new PhysicsMaterial("SweepDesk")
                    {
                        dynamicFriction = deskMul, staticFriction = deskMul * 1.15f, bounciness = GameConfig.DeskBounce,
                        frictionCombine = PhysicsMaterialCombine.Multiply, bounceCombine = PhysicsMaterialCombine.Average
                    };
                    foreach (float imp in new[] { 0.014f, 0.017f, 0.020f })
                    {
                        sb.AppendLine(string.Format("desk x{0:0.00}  impulse {1:0.000}", deskMul, imp));
                        foreach (float p in new[] { 0.25f, 0.5f, 1f })
                        {
                            using (var w = new World(mat))
                            {
                                var rb = w.Pen(pen, Vector3.zero, 0f);
                                for (int i = 0; i < 10; i++) w.Step();
                                rb.AddForce(Vector3.forward * Mathf.Lerp(GameConfig.ImpulseMin, imp * W, p), ForceMode.Impulse);
                                var tr = Follow(w, rb);
                                sb.AppendLine(string.Format("    flick {0,4:0%}: {1,5:0.0} cm in {2:0.00} s (90% by {3:0.00} s), start {4,4:0} cm/s", p, tr.dist, tr.stopT, tr.t90, tr.peakSpeed));
                            }
                        }
                        foreach (float p in new[] { 0.5f, 1f })
                        {
                            using (var w = new World(mat))
                            {
                                var target = w.Pen(pen, new Vector3(0.03f * W, 0f, 0f), 0f);
                                var shooter = w.Pen(pen, new Vector3(0f, 0f, -0.07f * W), 0f);
                                for (int i = 0; i < 10; i++) w.Step();
                                shooter.AddForce(Vector3.forward * Mathf.Lerp(GameConfig.ImpulseMin, imp * W, p), ForceMode.Impulse);
                                Vector3 s0 = shooter.position;
                                var tr = Follow(w, target, 240 * 5, () => shooter.linearVelocity.magnitude > 0.002f * W || shooter.angularVelocity.magnitude > 0.05f);
                                sb.AppendLine(string.Format("    hit 7cm {0,4:0%}: target {1,5:0.0} cm, turns {2,4:0}°, all still {3:0.00} s | shooter total {4,5:0.0} cm",
                                    p, tr.dist, tr.yaw, tr.stopT, Cm(Vector3.Distance(Flat(shooter.position), Flat(s0)))));
                            }
                        }
                    }
                }
            }
            finally
            {
                Physics.gravity = g; Physics.defaultSolverIterations = it; Physics.defaultSolverVelocityIterations = vit;
                Physics.defaultContactOffset = off; Physics.bounceThreshold = bt;
            }
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/feel-sweep.txt", sb.ToString());
        }

        // ---------------------------------------------------------------- hit sweep

        static void Tune(Rigidbody rb, float bounce, float angDamp)
        {
            foreach (var col in rb.GetComponents<Collider>())
            {
                var m = new PhysicsMaterial(col.sharedMaterial.name + "_sweep")
                {
                    dynamicFriction = col.sharedMaterial.dynamicFriction,
                    staticFriction = col.sharedMaterial.staticFriction,
                    bounciness = bounce,
                    frictionCombine = col.sharedMaterial.frictionCombine,
                    bounceCombine = col.sharedMaterial.bounceCombine
                };
                col.sharedMaterial = m;
            }
            rb.angularDamping = angDamp;
        }

        /// <summary>Pen-on-pen bounce x spin damping: how much speed the hit passes on, and how long the hit pen spins.</summary>
        [MenuItem("Tuition Takkar/Feel Lab Hit Sweep")]
        static void HitSweep()
        {
            var g = Physics.gravity; int it = Physics.defaultSolverIterations, vit = Physics.defaultSolverVelocityIterations;
            float off = Physics.defaultContactOffset, bt = Physics.bounceThreshold;
            Physics.gravity = GameConfig.Gravity;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.defaultContactOffset = 0.002f;
            Physics.bounceThreshold = 0.6f;
            var sb = new StringBuilder();
            var pen = Catalog.Pen(Catalog.StarterId);
            try
            {
                foreach (float bounce in new[] { 0.23f, 0.5f, 0.7f, 0.9f })
                foreach (float ad in new[] { 0.8f, 0.4f, 0.15f })
                {
                    sb.AppendLine(string.Format("pen bounce {0:0.00}  spin damping {1:0.00}", bounce, ad));
                    foreach (float offX in new[] { 0f, 0.045f })
                    foreach (float p in new[] { 0.6f, 1f })
                    {
                        using (var w = new World())
                        {
                            var target = w.Pen(pen, new Vector3(offX * W, 0f, 0f), 0f);
                            var shooter = w.Pen(pen, new Vector3(0f, 0f, -0.07f * W), 0f);
                            Tune(target, bounce, ad);
                            Tune(shooter, bounce, ad);
                            for (int i = 0; i < 10; i++) w.Step();
                            Flick.Apply(shooter, pen, new Shot { dir = Vector3.forward, power = p, strikeT = 0f });
                            Vector3 sAt = shooter.position;
                            bool hit = false;
                            float spinT = 0f, lastSpin = 0f;
                            var tr = Follow(w, target, 240 * 5, () =>
                            {
                                if (!hit && target.linearVelocity.magnitude > 0.01f * W) { hit = true; sAt = shooter.position; }
                                if (hit) { spinT += GameConfig.FixedDt; if (target.angularVelocity.magnitude > 0.6f) lastSpin = spinT; }
                                return shooter.linearVelocity.magnitude > 0.002f * W || shooter.angularVelocity.magnitude > 0.05f;
                            });
                            sb.AppendLine(string.Format("    {0} {1,4:0%}: shooter carries on {2,4:0.0} cm | target {3,5:0.0} cm, turns {4,4:0}°, spins for {5:0.00} s, still at {6:0.00} s",
                                offX == 0f ? "straight " : "off-centre", p, Cm(Vector3.Distance(Flat(shooter.position), Flat(sAt))), tr.dist, tr.yaw, lastSpin, tr.stopT));
                        }
                    }
                    using (var w = new World())
                    {
                        var rb = w.Pen(pen, Vector3.zero, 0f);
                        Tune(rb, bounce, ad);
                        for (int i = 0; i < 10; i++) w.Step();
                        Flick.Apply(rb, pen, new Shot { dir = Vector3.forward, power = 0.6f, strikeT = 0.85f });
                        var tr = Follow(w, rb);
                        sb.AppendLine(string.Format("    solo end-flick 60%: slides {0,5:0.0} cm, turns {1,4:0}°, still at {2:0.00} s", tr.dist, tr.yaw, tr.stopT));
                    }
                }
            }
            finally
            {
                Physics.gravity = g; Physics.defaultSolverIterations = it; Physics.defaultSolverVelocityIterations = vit;
                Physics.defaultContactOffset = off; Physics.bounceThreshold = bt;
            }
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/feel-hits.txt", sb.ToString());
        }

        // ---------------------------------------------------------------- world

        class World : IDisposable
        {
            public Scene scene;
            public PhysicsScene physics;
            public World() : this(null) { }

            public World(PhysicsMaterial deskOverride)
            {
                // Edit mode: a preview scene comes with its own physics world (like prefab mode).
                scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                physics = scene.GetPhysicsScene();
                if (!physics.IsValid()) throw new Exception("Feel Lab: preview scene has no physics");
                var deskMat = BenchWorld.MakeDeskMaterial();
                var desk = new GameObject("LabDesk");
                SceneManager.MoveGameObjectToScene(desk, scene);
                desk.transform.position = new Vector3(0f, -GameConfig.DeskThickness * 0.5f, 0f);
                var dc = desk.AddComponent<BoxCollider>();
                dc.size = new Vector3(40f, GameConfig.DeskThickness, 40f); // 4 m: nothing falls off
                dc.sharedMaterial = deskOverride ?? deskMat;
            }

            public Rigidbody Pen(PenDef d, Vector3 pos, float yawDeg)
            {
                var go = new GameObject("LabPen");
                SceneManager.MoveGameObjectToScene(go, scene);
                var rb = PenFactory.BuildPhysics(go, d, false);
                pos.y = PenFactory.RestHeight(d);
                var rot = Quaternion.Euler(0f, yawDeg, 0f);
                go.transform.SetPositionAndRotation(pos, rot);
                rb.position = pos;
                rb.rotation = rot;
                return rb;
            }

            public void Step() => physics.Simulate(GameConfig.FixedDt);

            public void Dispose()
            {
                if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------------------------------------------------------------- tests

        struct Track
        {
            public float stopT, dist, yaw, t90, peakSpeed;
            public List<string> curve;
        }

        static float Yaw(Rigidbody rb) => rb.rotation.eulerAngles.y;

        static Track Follow(World w, Rigidbody rb, int maxSteps = 240 * 5, Func<bool> extraBusy = null)
        {
            var tr = new Track { curve = new List<string>() };
            Vector3 start = rb.position;
            float lastYaw = Yaw(rb), yawSum = 0f;
            var dists = new List<float>();
            int quiet = 0;
            for (int s = 1; s <= maxSteps; s++)
            {
                w.Step();
                float t = s * GameConfig.FixedDt;
                float y = Yaw(rb);
                yawSum += Mathf.Abs(Mathf.DeltaAngle(lastYaw, y));
                lastYaw = y;
                float d = Cm(Vector3.Distance(Flat(rb.position), Flat(start)));
                dists.Add(d);
                float v = Cm(rb.linearVelocity.magnitude);
                tr.peakSpeed = Mathf.Max(tr.peakSpeed, v);
                if (s % 12 == 0 && t <= 1.2f) tr.curve.Add(t.ToString("0.00") + "s " + d.ToString("0.0") + "cm " + v.ToString("0") + "cm/s " + (rb.angularVelocity.magnitude * Mathf.Rad2Deg).ToString("0") + "°/s");
                bool still = rb.linearVelocity.magnitude < 0.002f * W && rb.angularVelocity.magnitude < 0.05f;
                if (extraBusy != null && extraBusy()) still = false;
                quiet = still ? quiet + 1 : 0;
                if (quiet == 1) tr.stopT = t;
                if (quiet > 24) break;
            }
            tr.dist = dists.Count > 0 ? dists[dists.Count - 1] : 0f;
            tr.yaw = yawSum;
            for (int i = 0; i < dists.Count; i++) if (dists[i] >= tr.dist * 0.9f) { tr.t90 = (i + 1) * GameConfig.FixedDt; break; }
            return tr;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void Solo(StringBuilder sb, PenDef d, float power, float strikeT, bool curve)
        {
            using (var w = new World())
            {
                var rb = w.Pen(d, Vector3.zero, 0f); // pen along x
                for (int i = 0; i < 10; i++) w.Step();
                Flick.Apply(rb, d, new Shot { dir = Vector3.forward, power = power, strikeT = strikeT });
                var tr = Follow(w, rb);
                sb.AppendLine(string.Format("{0,-12} power {1,4:0%}  slides {2,5:0.0} cm  stops {3:0.00} s  (90% of the way by {4:0.00} s)  peak {5,4:0} cm/s  turns {6,4:0}°",
                    d.id, power, tr.dist, tr.stopT, tr.t90, tr.peakSpeed, tr.yaw));
                if (curve) sb.AppendLine("    " + string.Join(" | ", tr.curve));
            }
        }

        static void Hit(StringBuilder sb, PenDef d, float power, float offsetX)
        {
            using (var w = new World())
            {
                var target = w.Pen(d, new Vector3(offsetX, 0f, 0f), 0f);
                var shooter = w.Pen(d, new Vector3(0f, 0f, -0.07f * W), 0f);
                for (int i = 0; i < 10; i++) w.Step();
                Vector3 t0 = target.position, s0 = shooter.position;
                Flick.Apply(shooter, d, new Shot { dir = Vector3.forward, power = power, strikeT = 0f });

                // Time of first contact: when the target starts moving.
                float contactT = -1f, spinFor = 0f;
                Vector3 sAtContact = s0;
                var tr = Follow(w, target, 240 * 5, () =>
                {
                    if (contactT < 0f && target.linearVelocity.magnitude > 0.01f * W) { contactT = 0f; sAtContact = shooter.position; }
                    if (contactT >= 0f)
                    {
                        contactT += GameConfig.FixedDt;
                        if (contactT < GameConfig.SpinAssistSeconds)
                        {
                            PenFactory.SpinAssist(target, d, GameConfig.SpinAssist);
                            PenFactory.SpinAssist(shooter, d, GameConfig.SpinAssist);
                        }
                        if (target.angularVelocity.magnitude > 0.6f) spinFor = contactT;
                    }
                    return shooter.linearVelocity.magnitude > 0.002f * W || shooter.angularVelocity.magnitude > 0.05f;
                });
                float shooterAfter = Cm(Vector3.Distance(Flat(shooter.position), Flat(sAtContact)));
                float gap = GapCm(d, shooter, target);
                sb.AppendLine(string.Format("power {0,4:0%}: target moves {1,5:0.0} cm, turns {2,4:0}° (spinning for {6:0.00} s), stops {3:0.00} s | shooter carries on {4,4:0.0} cm after contact | end gap {5,4:0.0} cm",
                    power, tr.dist, tr.yaw, tr.stopT, shooterAfter, gap, spinFor));
            }
        }

        /// <summary>Closest distance between the two pens' axes minus both radii (0 = touching).</summary>
        static float GapCm(PenDef d, Rigidbody a, Rigidbody b)
        {
            float L = PenFactory.ScaledLength(d), r = PenFactory.ScaledRadius(d);
            Vector3 a0 = a.position - a.rotation * Vector3.right * L * 0.5f, a1 = a.position + a.rotation * Vector3.right * L * 0.5f;
            Vector3 b0 = b.position - b.rotation * Vector3.right * L * 0.5f, b1 = b.position + b.rotation * Vector3.right * L * 0.5f;
            float best = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                Vector3 p = Vector3.Lerp(a0, a1, i / 20f);
                for (int j = 0; j <= 20; j++) best = Mathf.Min(best, Vector3.Distance(Flat(p), Flat(Vector3.Lerp(b0, b1, j / 20f))));
            }
            return Mathf.Max(0f, Cm(best - 2f * r));
        }
    }
}
