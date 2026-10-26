using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>The 3D scene: tuition bench, floor, light, camera and the two pens.</summary>
    public class BenchWorld : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public CameraRig Rig { get; private set; }
        /// <summary>2 pens normally, 3–4 in an online party match.</summary>
        public PenBody[] Pens { get; private set; } = new PenBody[2];
        public PhysicsMaterial DeskMaterial { get; private set; }
        public PhysicsMaterial FloorMaterial { get; private set; }

        /// <summary>Visual rim width around the laminate (world units).</summary>
        public const float RimSize = 0.06f;
        /// <summary>Height of the desk's front panel under the top (world units).</summary>
        public const float ApronHeight = 1.5f;

        static float deskW = GameConfig.DeskWidth, deskD = GameConfig.DeskDepth;
        public static float DeskW => deskW;
        public static float DeskD => deskD;
        public static float HalfW => deskW * 0.5f;
        public static float HalfD => deskD * 0.5f;
        /// <summary>The square two-seater desk used for 3–4 player online matches.</summary>
        public static bool PartyDesk { get; private set; }

        Transform deskRoot;

        /// <summary>
        /// The desk top. Friction multiplies with the pen's own (Multiply wins over the pens'
        /// Average), so each pen's grip stat changes how far it slides, while pen-on-pen contacts
        /// keep their own grip.
        /// </summary>
        public static PhysicsMaterial MakeDeskMaterial() => new PhysicsMaterial("Desk")
        {
            dynamicFriction = GameConfig.DeskFriction,
            staticFriction = GameConfig.DeskFriction * 1.15f,
            bounciness = GameConfig.DeskBounce,
            frictionCombine = PhysicsMaterialCombine.Multiply,
            bounceCombine = PhysicsMaterialCombine.Average
        };

        public void Build()
        {
            DeskMaterial = MakeDeskMaterial();
            FloorMaterial = new PhysicsMaterial("Floor")
            {
                dynamicFriction = 0.5f, staticFriction = 0.6f, bounciness = 0.2f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average
            };

            BuildDesk();

            // Classroom floor tiles. Fallen pens land here.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -GameConfig.DeskHeight - 0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = ArtLib.LitTextured(ArtLib.FloorTiles, new Vector2(36f, 36f));
            floor.GetComponent<Collider>().sharedMaterial = FloorMaterial;
            floor.AddComponent<Surface>().kind = SurfaceKind.Floor;

            // Tube-light key light, tuned so the laminate reads pale green, not white.
            var lightGo = new GameObject("TubeLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(70f, -25f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = Pal.H("#FFF9F0");
            l.intensity = 0.62f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.45f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Pal.H("#6E675C");
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0f;

            // Camera in front of the desk, looking down at it in perspective.
            var camGo = new GameObject("BenchCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Pal.H("#E6E0D1");
            Cam.fieldOfView = 40f; // a little more perspective: the desk reads as a trapezoid (K1)
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 300f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Rig = camGo.AddComponent<CameraRig>();
        }

        /// <summary>Switches between the normal desk and the square party desk (3–4 players).</summary>
        public void SetPartyDesk(bool party)
        {
            if (deskRoot != null && PartyDesk == party) return;
            PartyDesk = party;
            deskW = party ? GameConfig.PartyDeskSize : GameConfig.DeskWidth;
            deskD = party ? GameConfig.PartyDeskSize : GameConfig.DeskDepth;
            BuildDesk();
            if (Rig != null) Rig.Fit();
        }

        void BuildDesk()
        {
            if (deskRoot != null) Destroy(deskRoot.gameObject);
            deskRoot = new GameObject("Desk").transform;
            deskRoot.SetParent(transform, false);

            float t = GameConfig.DeskThickness;

            // Desk top (the only surface pens play on): an old wooden school desk, carved by past
            // students (mockup K1). One texture covers the whole top.
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "DeskTop";
            top.transform.SetParent(deskRoot, false);
            top.transform.localPosition = new Vector3(0f, -t * 0.5f, 0f);
            top.transform.localScale = new Vector3(deskW, t, deskD);
            int texW = PartyDesk ? 768 : 512, texH = PartyDesk ? 768 : 1024;
            top.GetComponent<MeshRenderer>().sharedMaterial =
                ArtLib.LitTextured(ArtLib.DeskWood(texW, texH), Vector2.one);
            top.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            top.GetComponent<Collider>().sharedMaterial = DeskMaterial;
            top.AddComponent<Surface>().kind = SurfaceKind.Desk;

            // A thin darker wood rim, the thick front edge, black steel legs and a soft shadow on
            // the floor. All visual only: the playable top stays exactly DeskWidth x DeskDepth.
            const float rim = RimSize;
            Visual(PrimitiveType.Cube, "Rim", new Vector3(0f, -t * 0.5f - 0.01f, 0f),
                new Vector3(deskW + rim * 2f, t, deskD + rim * 2f), Pal.H("#7E5F3E"));
            // Front edge: on the near side only for the normal desk, on all four sides for the
            // square party desk (every player looks at it from a different side).
            int sides = PartyDesk ? 4 : 1;
            for (int k = 0; k < sides; k++)
            {
                var turn = Quaternion.Euler(0f, 90f * k, 0f);
                bool across = k % 2 == 1;
                float halfFar = across ? HalfW : HalfD;
                float span = across ? deskD : deskW;
                var edge = Visual(PrimitiveType.Cube, "FrontEdge", turn * new Vector3(0f, -t * 0.5f - 0.01f, -halfFar - rim - 0.005f),
                    new Vector3(span + rim * 2f, t + 0.02f, 0.01f), Pal.H("#4E3620"));
                edge.transform.localRotation = turn;
                // A thicker slab under the top's edge, so the desk reads as a heavy wooden bench.
                var slab = Visual(PrimitiveType.Cube, "Slab", turn * new Vector3(0f, -t - 0.11f, -halfFar - rim + 0.06f),
                    new Vector3(span + rim * 2f, 0.22f, 0.12f), Pal.H("#3F2B19"));
                slab.transform.localRotation = turn;
            }

            float legH = GameConfig.DeskHeight - t;
            float lx = HalfW - 0.16f, lz = HalfD - 0.16f;
            foreach (var p in new[] { new Vector2(lx, lz), new Vector2(-lx, lz), new Vector2(lx, -lz), new Vector2(-lx, -lz) })
                Visual(PrimitiveType.Cube, "Leg", new Vector3(p.x, -t - legH * 0.5f, p.y),
                    new Vector3(0.14f, legH, 0.14f), Pal.H("#1E1E1E"));

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "DeskShadow";
            Destroy(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(deskRoot, false);
            shadow.transform.localPosition = new Vector3(0.25f, -GameConfig.DeskHeight + 0.02f, -0.35f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(deskW * 1.45f, deskD * 1.25f, 1f);
            var sr = shadow.GetComponent<MeshRenderer>();
            sr.sharedMaterial = ArtLib.Transparent(ArtLib.Blob, new Color(0f, 0f, 0f, 0.38f));
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }

        GameObject Visual(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(deskRoot != null ? deskRoot : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = ArtLib.Matte(c);
            return go;
        }

        public void SpawnPens(params PenDef[] defs)
        {
            ClearPens();
            Pens = new PenBody[defs.Length];
            for (int i = 0; i < defs.Length; i++) Pens[i] = PenBody.Create(defs[i], i, transform);
            ResetPens();
        }

        public void ClearPens()
        {
            for (int i = 0; i < Pens.Length; i++)
            {
                if (Pens[i] != null) Destroy(Pens[i].gameObject);
                Pens[i] = null;
            }
        }

        /// <summary>Player 0 near you (bottom of screen), player 1 at the far end.</summary>
        public void ResetPens() => ResetPens(Random.Range(-4f, 4f));

        /// <summary>Online passes the same jitter on every phone so the start is identical.</summary>
        public void ResetPens(float jitter)
        {
            if (Pens.Length == 0 || Pens[0] == null) return;
            if (Pens.Length > 2)
            {
                // Party desk: pens in a ring, player 0 nearest the bottom, each lying across its side.
                int n = Pens.Length;
                for (int i = 0; i < n; i++)
                {
                    float theta = -90f + 360f * i / n;
                    float r = theta * Mathf.Deg2Rad;
                    var pos = new Vector3(Mathf.Cos(r) * GameConfig.PartyStartRadius, PenFactory.RestHeight(Pens[i].Def), Mathf.Sin(r) * GameConfig.PartyStartRadius);
                    Pens[i].Place(pos, Quaternion.Euler(0f, -(theta + 90f) + (i % 2 == 0 ? jitter : -jitter), 0f));
                }
                return;
            }
            Pens[0].Place(new Vector3(0f, PenFactory.RestHeight(Pens[0].Def), -GameConfig.StartOffsetZ),
                Quaternion.Euler(0f, jitter, 0f));
            Pens[1].Place(new Vector3(0f, PenFactory.RestHeight(Pens[1].Def), GameConfig.StartOffsetZ),
                Quaternion.Euler(0f, 180f - jitter, 0f));
        }

        public PenState[] Snapshot() => new[] { Pens[0].Snapshot(), Pens[1].Snapshot() };

        /// <summary>Camera yaw that puts this pen index at the bottom of the screen.</summary>
        public static float YawFor(int index, int count) => count <= 1 ? 0f : -360f * index / count;

        /// <summary>
        /// Out once it really drops: 4 cm below the top, or already falling with its
        /// centre past the edge (it can't come back from there).
        /// </summary>
        public bool IsOut(PenBody pen)
        {
            if (pen == null || pen.HitFloor) return true;
            var c = pen.Rb.worldCenterOfMass;
            if (pen.transform.position.y < -GameConfig.OutDepth) return true;
            if (!OffDesk(c)) return false;
            // Middle past the edge: it is going over. Count it once it is dropping, or once it
            // has gone still there (it can't stay on the desk with its middle in the air).
            return c.y < PenFactory.RestHeight(pen.Def) - 0.15f || pen.IsQuiet;
        }

        public static float EdgeDistance(Vector3 p) =>
            Mathf.Min(HalfW - Mathf.Abs(p.x), HalfD - Mathf.Abs(p.z));

        /// <summary>0 = safe in the middle, 1 = at (or past) the edge.</summary>
        public static float Danger(Vector3 p) => Mathf.Clamp01(1f - EdgeDistance(p) / (GameConfig.DangerZone * 2f));

        public static bool OffDesk(Vector3 p) => Mathf.Abs(p.x) > HalfW || Mathf.Abs(p.z) > HalfD;

        /// <summary>Pen is still on the desk but part of it hangs over the edge.</summary>
        public bool IsOverhanging(PenBody pen)
        {
            if (pen == null || IsOut(pen)) return false;
            return OffDesk(pen.TipEnd) || OffDesk(pen.CapEnd);
        }

        public bool AnyMoving()
        {
            foreach (var p in Pens)
            {
                if (p == null || IsOut(p)) continue;
                if (!p.IsQuiet) return true;
                // A pen tipping slowly over the edge barely moves at first: be stricter with it.
                if (IsOverhanging(p) && (p.Rb.linearVelocity.magnitude > 0.03f || p.Rb.angularVelocity.magnitude > 0.03f))
                    return true;
            }
            return false;
        }

        public bool AnyOverhanging()
        {
            foreach (var p in Pens)
                if (IsOverhanging(p)) return true;
            return false;
        }

        /// <summary>
        /// If the two pens are touching (closer than minGap between their surfaces), returns the
        /// flat moves that open the gap: split between both pens, or all on one pen when the
        /// other would be nudged toward an edge. False when no move is needed or possible.
        /// </summary>
        public bool SeparationNeeded(float minGap, out Vector3 push0, out Vector3 push1) =>
            SeparationNeeded(Pens[0], Pens[1], minGap, out push0, out push1);

        public bool SeparationNeeded(PenBody a, PenBody b, float minGap, out Vector3 push0, out Vector3 push1)
        {
            push0 = push1 = Vector3.zero;
            if (a == null || b == null || IsOut(a) || IsOut(b)) return false;

            ClosestPointsFlat(a.TipEnd, a.CapEnd, b.TipEnd, b.CapEnd, out var pa, out var pb);
            Vector3 d = pb - pa;
            d.y = 0f;
            float dist = d.magnitude;
            float need = a.Radius + b.Radius + minGap - dist;
            if (need <= 0f) return false;

            Vector3 n;
            if (dist > 1e-4f) n = d / dist;
            else
            {
                n = b.Rb.position - a.Rb.position;
                n.y = 0f;
                n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
            }

            Vector3 half0 = -n * need * 0.5f, half1 = n * need * 0.5f;
            bool ok0 = SafeMove(a, half0 * 2f), ok1 = SafeMove(b, half1 * 2f);
            if (ok0 && ok1) { push0 = half0; push1 = half1; }
            else if (ok1) push1 = n * need;          // a is near an edge: move only b
            else if (ok0) push0 = -n * need;         // b is near an edge: move only a
            else return false;                       // both near edges: leave them
            return true;
        }

        /// <summary>True if moving the pen by delta keeps both of its ends on the desk.</summary>
        bool SafeMove(PenBody p, Vector3 delta) => !OffDesk(p.TipEnd + delta) && !OffDesk(p.CapEnd + delta);

        /// <summary>Closest points between two pen axes, ignoring height.</summary>
        static void ClosestPointsFlat(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            p1.y = q1.y = p2.y = q2.y = 0f;
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-8f && e <= 1e-8f) { s = t = 0f; }
            else if (a <= 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float bb = Vector3.Dot(d1, d2);
                    float denom = a * e - bb * bb;
                    s = denom > 1e-8f ? Mathf.Clamp01((bb * f - c * e) / denom) : 0f;
                    t = (bb * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }
    }
}
