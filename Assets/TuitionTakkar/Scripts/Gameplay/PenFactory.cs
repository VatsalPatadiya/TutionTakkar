using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Builds pen physics and visuals. Pens lie along local +X; +X is the cap end.</summary>
    public static class PenFactory
    {
        public static float ScaledLength(PenDef d) => d.length * GameConfig.PenLengthScale * GameConfig.WorldScale;
        public static float ScaledRadius(PenDef d) => d.radius * GameConfig.PenThicknessScale * GameConfig.WorldScale;

        /// <summary>Height of the pen centre above the desk when resting.</summary>
        public static float RestHeight(PenDef d)
        {
            float r = ScaledRadius(d);
            return d.shape == PenShape.Triangle ? r * 1.2f * 0.5f + 0.002f : r + 0.002f;
        }

        public static Rigidbody BuildPhysics(GameObject go, PenDef d, bool interpolate)
        {
            float L = ScaledLength(d);
            float r = ScaledRadius(d);

            var mat = new PhysicsMaterial(d.id)
            {
                // Grip: pens rubbing together turn each other (smooth, curving contacts).
                dynamicFriction = d.friction + GameConfig.PenGripBoost,
                staticFriction = (d.friction + GameConfig.PenGripBoost) * 1.15f,
                // Soft but lively contact (~0.2 bounce): the hit pen takes most of the push and the
                // shooter slows to a stop, without the sharp one-frame "jump" of a springy hit.
                bounciness = Mathf.Clamp01(d.bounce * GameConfig.PenBounceScale),
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            if (d.shape == PenShape.Triangle)
            {
                // Looks like a triangle, but collides as a flat-sided block of the same
                // footprint: sloped sides act like a ramp, so a round pen hitting them was
                // deflected upward (riding over the pencil) and lost most of its push.
                // Upright sides make every hit a sideways push, like round pens.
                float R = r * 1.2f;
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(L, R * 1.5f, R * 1.7320508f);
                box.center = new Vector3(0f, R * 0.25f, 0f); // bottom flush with the prism's flat face
                box.sharedMaterial = mat;
            }
            else
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 0;
                cap.height = L;
                cap.radius = r;
                cap.sharedMaterial = mat;
                if (d.hasClip)
                {
                    // The clip sticks up and stops the pen rolling forever, like a real one.
                    var clip = go.AddComponent<BoxCollider>();
                    clip.center = new Vector3(L * 0.18f, r * 1.05f, 0f);
                    clip.size = new Vector3(L * 0.32f, r * 0.35f, r * 0.5f);
                    clip.sharedMaterial = mat;
                }
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = d.mass;
            rb.linearDamping = GameConfig.PenLinearDamping;
            rb.angularDamping = d.angularDamping;
            rb.maxAngularVelocity = GameConfig.PenMaxAngularVelocity;
            // Speculative CCD keeps momentum in fast pen-on-pen hits. ContinuousDynamic stopped a
            // fast pen at the moment of contact and threw most of its push away, so a 100% flick
            // could move the other pen only ~2 cm (and sometimes, by timing luck, 80 cm).
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = interpolate ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            rb.centerOfMass = new Vector3(d.comOffset * L * 0.5f, 0f, 0f);
            rb.constraints = Flat;
            // Never let a pen fall asleep: a pen tipping slowly over the edge could otherwise
            // freeze mid-fall, end the turn, and drop later during someone else's turn.
            rb.sleepThreshold = 0f;
            return rb;
        }

        /// <summary>
        /// On the desk a pen slides and spins flat (like a real pen fight flick) instead of
        /// rolling: a rolling round pen would stop dead as soon as its clip hit the desk.
        /// </summary>
        public const RigidbodyConstraints Flat = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        public static void Flatten(Rigidbody rb) => rb.constraints = Flat;

        /// <summary>Once the pen's middle passes the edge (or it starts dropping), let it tip and fall for real.</summary>
        public static void ReleaseIfOverEdge(Rigidbody rb, PenDef d)
        {
            if (rb.constraints == RigidbodyConstraints.None) return;
            var c = rb.worldCenterOfMass;
            if (BenchWorld.OffDesk(c) || c.y < RestHeight(d) - 0.1f)
            {
                rb.constraints = RigidbodyConstraints.None;
                rb.WakeUp();
                // Tumble over the edge like a real pen (penfight.xyz): roll forward about the
                // axis across its travel, so the fall reads clearly from above.
                var v = rb.linearVelocity;
                v.y = 0f;
                if (v.sqrMagnitude > 1e-4f)
                {
                    var across = Vector3.Cross(Vector3.up, v.normalized);
                    rb.angularVelocity += across * Mathf.Clamp(v.magnitude * GameConfig.EdgeTumble, 0f, 14f);
                }
            }
        }

        /// <summary>
        /// A pen that was just hit keeps spinning longer while it slides (real footage: the hit pen
        /// turns smoothly as it slows). Pushes back part of the desk's turning friction
        /// (about mu*m*g*L/4 for a rod spinning flat) for a short while after the hit.
        /// </summary>
        public static void SpinAssist(Rigidbody rb, PenDef d, float strength)
        {
            if (strength <= 0f || rb.constraints != Flat) return;
            float w = rb.angularVelocity.y;
            if (Mathf.Abs(w) < 0.4f) return;
            float mu = GameConfig.DeskFriction * (d.friction + GameConfig.PenGripBoost) * 2f; // measured: contacts brake ~2x nominal
            float torque = strength * mu * d.mass * -Physics.gravity.y * ScaledLength(d) * 0.25f;
            rb.AddTorque(Vector3.up * Mathf.Sign(w) * torque, ForceMode.Force);
        }

        /// <summary>
        /// Pens may ride over and lie across each other (like real pens, and it looks natural).
        /// Forcing them to stay down made one pen sink into the other and get wedged there.
        /// The only limit: a pen flat on the desk may not pop upward fast, so a hard hit
        /// slides one pen over the other instead of launching it into the air.
        /// </summary>
        public static void KeepOnDesk(Rigidbody rb, PenDef d)
        {
            if (rb.constraints == RigidbodyConstraints.None) return;
            var v = rb.linearVelocity;
            if (v.y > GameConfig.MaxRiseSpeed)
            {
                v.y = GameConfig.MaxRiseSpeed;
                rb.linearVelocity = v;
            }
        }

        public static void BuildVisual(Transform root, PenDef d)
        {
            float L = ScaledLength(d);
            float r = ScaledRadius(d);
            float x0 = -L * 0.5f;
            var metal = Pal.H("#B7C0C7");

            if (d.shape == PenShape.Triangle)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(root, false);
                body.AddComponent<MeshFilter>().sharedMesh = PrismMesh(L * 0.9f, r * 1.2f);
                body.AddComponent<MeshRenderer>().sharedMaterial = ArtLib.Lit(d.color);
                body.transform.localPosition = new Vector3(L * 0.05f, 0f, 0f);
                // Sharpened end: a smooth cone instead of a squashed sphere.
                Lathe(root, "Tip", d.tipColor, new[] { P(x0, 0f), P(x0 + L * 0.035f, r * 0.22f), P(x0 + L * 0.05f, r * 0.3f) });
                Lathe(root, "Wood", Pal.H("#E8C79A"), new[] { P(x0 + L * 0.05f, r * 0.3f), P(x0 + L * 0.1f, r * 0.95f) });
                Part(root, PrimitiveType.Cube, new Vector3(L * 0.44f, 0f, 0f), new Vector3(L * 0.1f, r * 1.25f, r * 1.4f), d.capColor, false);
                return;
            }

            // One smooth turned shape per section (no stretched spheres): ink tip, metal cone,
            // ridged grip, barrel, cap with a rounded end, click button.
            Lathe(root, "Tip", d.tipColor, new[] { P(x0, 0f), P(x0 + L * 0.012f, r * 0.14f), P(x0 + L * 0.03f, r * 0.2f) });
            Lathe(root, "Cone", metal, new[] { P(x0 + L * 0.03f, r * 0.2f), P(x0 + L * 0.07f, r * 0.62f), P(x0 + L * 0.085f, r * 0.9f), P(x0 + L * 0.09f, r * 0.98f) });
            Lathe(root, "Grip", d.gripColor, Ridged(x0 + L * 0.09f, x0 + L * 0.27f, r * 1.0f, r * 1.07f, 7));
            Lathe(root, "Barrel", d.color, new[] { P(x0 + L * 0.27f, r * 0.96f), P(x0 + L * 0.275f, r), P(x0 + L * 0.835f, r), P(x0 + L * 0.84f, r * 0.97f) });

            var capPts = new List<Vector2> { P(x0 + L * 0.84f, r * 1.04f), P(x0 + L * 0.845f, r * 1.12f), P(x0 + L * 0.95f, r * 1.12f) };
            for (int i = 1; i <= 6; i++)
            {
                // Quarter-round shoulder at the cap end.
                float a = i / 6f * Mathf.PI * 0.5f;
                capPts.Add(P(x0 + L * 0.95f + Mathf.Sin(a) * L * 0.025f, Mathf.Lerp(r * 0.55f, r * 1.12f, Mathf.Cos(a))));
            }
            Lathe(root, "Cap", d.capColor, capPts.ToArray());
            Lathe(root, "Click", d.capColor, new[] { P(L * 0.475f, r * 0.5f), P(L * 0.49f, r * 0.5f), P(L * 0.5f, r * 0.35f), P(L * 0.5f, 0f) });

            if (d.hasClip)
            {
                Part(root, PrimitiveType.Cube, new Vector3(L * 0.2f, r * 1.2f, 0f), new Vector3(L * 0.3f, r * 0.14f, r * 0.42f), metal, false);
                Part(root, PrimitiveType.Cube, new Vector3(L * 0.345f, r * 1.12f, 0f), new Vector3(L * 0.02f, r * 0.3f, r * 0.42f), metal, false);
            }
            // A thin highlight strip sells the plastic.
            Part(root, PrimitiveType.Cube, new Vector3(x0 + L * 0.55f, r * 0.7f, -r * 0.55f), new Vector3(L * 0.5f, r * 0.1f, r * 0.1f),
                Color.Lerp(d.color, Color.white, 0.45f), false);
        }

        static Vector2 P(float x, float radius) => new Vector2(x, radius);

        /// <summary>Profile with small rings, like a rubber grip.</summary>
        static Vector2[] Ridged(float from, float to, float rLow, float rHigh, int rings)
        {
            var pts = new List<Vector2>();
            int n = rings * 2;
            for (int i = 0; i <= n; i++)
                pts.Add(P(Mathf.Lerp(from, to, i / (float)n), i % 2 == 0 ? rLow : rHigh));
            return pts.ToArray();
        }

        static readonly Dictionary<string, Mesh> latheCache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Spins a side profile (x along the pen, radius) around the pen's axis into a smooth
        /// mesh with proper normals. Open ends get a flat disc so nothing looks hollow.
        /// </summary>
        static void Lathe(Transform root, string name, Color c, Vector2[] profile)
        {
            const int seg = 32;
            var sb = new System.Text.StringBuilder(name);
            foreach (var p in profile) sb.Append('|').Append(p.x.ToString("0.0000")).Append(':').Append(p.y.ToString("0.0000"));
            string key = sb.ToString();

            if (!latheCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                var v = new List<Vector3>();
                var nrm = new List<Vector3>();
                var tri = new List<int>();
                int rows = profile.Length;
                for (int i = 0; i < rows; i++)
                {
                    // Profile normal from the neighbouring points: smooth shading along the pen.
                    Vector2 t = profile[Mathf.Min(rows - 1, i + 1)] - profile[Mathf.Max(0, i - 1)];
                    if (t.sqrMagnitude < 1e-10f) t = Vector2.right;
                    t.Normalize();
                    var pn = new Vector2(-t.y, t.x); // (along, outward)
                    if (pn.y < 0f) pn = -pn;
                    for (int k = 0; k <= seg; k++)
                    {
                        float a = k / (float)seg * Mathf.PI * 2f;
                        float cy = Mathf.Cos(a), cz = Mathf.Sin(a);
                        v.Add(new Vector3(profile[i].x, cy * profile[i].y, cz * profile[i].y));
                        nrm.Add(new Vector3(pn.x, cy * pn.y, cz * pn.y).normalized);
                    }
                }
                for (int i = 0; i < rows - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int a0 = i * (seg + 1) + k, a1 = a0 + 1, b0 = a0 + seg + 1, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
                EndDisc(v, nrm, tri, profile[0], -1f, seg);
                EndDisc(v, nrm, tri, profile[rows - 1], 1f, seg);

                mesh = new Mesh { name = "Lathe_" + name };
                mesh.SetVertices(v);
                mesh.SetNormals(nrm);
                mesh.SetTriangles(tri, 0);
                FaceOutward(mesh);
                mesh.RecalculateBounds();
                latheCache[key] = mesh;
            }

            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ArtLib.Lit(c);
        }

        static void EndDisc(List<Vector3> v, List<Vector3> n, List<int> t, Vector2 p, float dir, int seg)
        {
            if (p.y < 1e-4f) return; // already closes to a point
            int centre = v.Count;
            v.Add(new Vector3(p.x, 0f, 0f));
            n.Add(new Vector3(dir, 0f, 0f));
            for (int k = 0; k <= seg; k++)
            {
                float a = k / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(p.x, Mathf.Cos(a) * p.y, Mathf.Sin(a) * p.y));
                n.Add(new Vector3(dir, 0f, 0f));
            }
            for (int k = 0; k < seg; k++) t.AddRange(new[] { centre, centre + 1 + k, centre + 2 + k });
        }

        /// <summary>Flip any triangle whose winding disagrees with its normals, so every face is visible from outside.</summary>
        static void FaceOutward(Mesh m)
        {
            var verts = m.vertices;
            var norms = m.normals;
            var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = verts[tris[i]];
                var b = verts[tris[i + 1]];
                var c = verts[tris[i + 2]];
                var face = Vector3.Cross(b - a, c - a);
                var want = norms[tris[i]] + norms[tris[i + 1]] + norms[tris[i + 2]];
                if (Vector3.Dot(face, want) < 0f)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
            }
            m.triangles = tris;
        }

        static void Part(Transform root, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = ArtLib.Lit(c);
        }

        /// <summary>Triangular prism along X with a flat face down.</summary>
        public static Mesh PrismMesh(float length, float circumR)
        {
            float h = length * 0.5f;
            var tri = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.Deg2Rad * (90f + 120f * i);
                tri[i] = new Vector2(Mathf.Cos(a) * circumR, Mathf.Sin(a) * circumR);
            }

            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            // Side faces (separate verts for flat shading).
            for (int i = 0; i < 3; i++)
            {
                var a = tri[i];
                var b = tri[(i + 1) % 3];
                int s = v.Count;
                v.Add(new Vector3(-h, a.y, a.x));
                v.Add(new Vector3(h, a.y, a.x));
                v.Add(new Vector3(h, b.y, b.x));
                v.Add(new Vector3(-h, b.y, b.x));
                t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            // End caps.
            int e0 = v.Count;
            for (int i = 0; i < 3; i++) v.Add(new Vector3(-h, tri[i].y, tri[i].x));
            t.AddRange(new[] { e0, e0 + 2, e0 + 1 });
            int e1 = v.Count;
            for (int i = 0; i < 3; i++) v.Add(new Vector3(h, tri[i].y, tri[i].x));
            t.AddRange(new[] { e1, e1 + 1, e1 + 2 });

            var mesh = new Mesh { name = "PenPrism" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Make sure faces point outward whatever winding we produced.
            FixWinding(mesh);
            return mesh;
        }

        static void FixWinding(Mesh m)
        {
            var verts = m.vertices;
            var tris = m.triangles;
            // The prism is convex and centred on the origin, so a face points
            // outward when its normal agrees with the direction to its centre.
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = verts[tris[i]];
                var b = verts[tris[i + 1]];
                var c = verts[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                var centre = (a + b + c) / 3f;
                if (Vector3.Dot(n, centre) < 0f)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
            }
            m.triangles = tris;
            m.RecalculateNormals();
        }
    }
}
