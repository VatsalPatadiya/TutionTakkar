using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TuitionTakkar
{
    /// <summary>
    /// The 3D desk of a DeskDef: playable top (exactly W x D, the only collider), a thick slab with
    /// rounded corners and a bevelled edge, a pen groove with a pencil at the far end, steel tube
    /// legs with cross bars and a footrest, a book shelf, and for premium tiers metal trim, corner
    /// caps, a TT crest, a rug and (Diamond) twinkles. Everything but the top is visual only.
    /// </summary>
    public static class DeskModel
    {
        /// <summary>Extra strip at the far end that holds the pen groove (normal desk only).</summary>
        public const float GrooveStrip = 0.30f;

        public static GameObject Build(DeskDef d, Transform root, float W, float D, bool party, PhysicsMaterial physics)
        {
            float t = GameConfig.DeskThickness;
            float rim = BenchWorld.RimSize;
            float hw = W * 0.5f, hd = D * 0.5f;
            float far = party ? 0f : GrooveStrip;
            bool premium = d.Premium;
            var trim = d.Trim;

            var edgeMaps = DeskArt.Edge(d);
            var edgeMat = DeskArt.Make(edgeMaps, Vector2.one, 0.6f);
            var legMat = DeskArt.Metal(d.leg, d.legMetal, 0.3f + d.legMetal * 0.55f);
            Material trimMat = trim.HasValue ? DeskArt.Metal(trim.Value, 0.95f, 0.82f) : null;

            // ---- playable top: collider only, the picture is a separate quad with proper UVs ----
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "DeskTop";
            top.transform.SetParent(root, false);
            top.transform.localPosition = new Vector3(0f, -t * 0.5f, 0f);
            top.transform.localScale = new Vector3(W, t, D);
            top.GetComponent<MeshRenderer>().enabled = false;
            top.GetComponent<Collider>().sharedMaterial = physics;
            top.AddComponent<Surface>().kind = SurfaceKind.Desk;

            int texW = party ? 768 : 512, texH = party ? 768 : 1024;
            var topMaps = DeskArt.Top(d, texW, texH);
            var face = MeshObject(root, "DeskFace", Quad(new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, hd)), DeskArt.Make(topMaps, Vector2.one, 1f));
            face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---- slab: the thick top board with rounded corners and a soft bevel ----
            var slab = RoundedBox(new Vector3(-hw - rim, -t - 0.04f, -hd - rim), new Vector3(hw + rim, -0.003f, hd + rim + far),
                0.09f, 0.04f, 6, 0.55f);
            MeshObject(root, "Slab", slab, edgeMat);

            // ---- the far strip: pen groove, a pencil, an ink well or the crest ----
            if (!party)
            {
                float gz = hd + rim + far * 0.5f;
                float gx0 = -hw * 0.86f, gx1 = hw * 0.30f;
                var groove = MeshObject(root, "Groove", Quad(new Vector3(gx0, -0.0015f, gz - 0.06f), new Vector3(gx1, -0.0015f, gz + 0.06f)),
                    ArtLib.LitTextured(GrooveTex(d.edge), Vector2.one, 0.1f));
                groove.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Pencil(root, new Vector3(gx0 + 0.15f, 0.02f, gz), (gx1 - gx0) * 0.62f);

                if (premium && trim.HasValue)
                {
                    var crest = MeshObject(root, "Crest", Quad(new Vector3(hw * 0.62f - 0.26f, 0.002f, gz - 0.13f), new Vector3(hw * 0.62f + 0.26f, 0.002f, gz + 0.13f)),
                        DeskArt.Make(DeskArt.Crest(trim.Value, d.edge), Vector2.one, 1f));
                    crest.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    // Ink well: a dark hole with a brass ring.
                    var well = Prim(root, PrimitiveType.Cylinder, "InkWell", new Vector3(hw * 0.62f, -0.0012f, gz), new Vector3(0.17f, 0.001f, 0.17f), DeskArt.Metal(Pal.H("#9C7A3C"), 0.7f, 0.6f));
                    Prim(well.transform.parent, PrimitiveType.Cylinder, "Ink", new Vector3(hw * 0.62f, -0.0008f, gz), new Vector3(0.12f, 0.001f, 0.12f), ArtLib.Matte(Pal.H("#14100C")));
                }
            }

            // ---- premium: metal trim round the playing top, corner caps ----
            if (trimMat != null)
            {
                const float tw = 0.035f, th = 0.006f;
                Prim(root, PrimitiveType.Cube, "TrimN", new Vector3(0f, -0.001f, -hd - tw * 0.5f), new Vector3(W + tw * 2f, th, tw), trimMat);
                Prim(root, PrimitiveType.Cube, "TrimF", new Vector3(0f, -0.001f, hd + tw * 0.5f), new Vector3(W + tw * 2f, th, tw), trimMat);
                Prim(root, PrimitiveType.Cube, "TrimL", new Vector3(-hw - tw * 0.5f, -0.001f, 0f), new Vector3(tw, th, D), trimMat);
                Prim(root, PrimitiveType.Cube, "TrimR", new Vector3(hw + tw * 0.5f, -0.001f, 0f), new Vector3(tw, th, D), trimMat);
                float cx = hw + rim, czn = hd + rim, czf = hd + rim + far;
                foreach (var c in new[] { new Vector2(-cx, -czn), new Vector2(cx, -czn), new Vector2(-cx, czf), new Vector2(cx, czf) })
                {
                    const float inward = 0.11f;
                    bool px = c.x > 0f, pz = c.y > 0f;
                    var min = new Vector3(px ? c.x - inward : c.x - 0.012f, -t - 0.05f, pz ? c.y - inward : c.y - 0.012f);
                    var max = new Vector3(px ? c.x + 0.012f : c.x + inward, 0.003f, pz ? c.y + 0.012f : c.y + inward);
                    // Only the outer corner shows: a cap that wraps the slab's corner.
                    MeshObject(root, "CornerCap", RoundedBox(min, max, 0.095f, 0.02f, 5, 2f), trimMat);
                }
            }

            // ---- frame: front apron, legs, side rails, footrest, shelf ----
            float floorY = -GameConfig.DeskHeight;
            float underY = -t - 0.04f;
            float legIn = 0.17f;
            float lx = hw + rim - legIn, lzN = hd + rim - legIn, lzF = hd + rim + far - legIn;
            if (!party)
            {
                var apron = RoundedBox(new Vector3(-lx + 0.08f, underY - 1.1f, -lzN - 0.02f), new Vector3(lx - 0.08f, underY, -lzN + 0.05f), 0.02f, 0.01f, 2, 0.55f);
                MeshObject(root, "Apron", apron, edgeMat);
            }
            float legH = underY - floorY;
            var legPos = new[] { new Vector2(lx, lzF), new Vector2(-lx, lzF), new Vector2(lx, -lzN), new Vector2(-lx, -lzN) };
            foreach (var p in legPos)
            {
                if (d.roundLegs)
                    Prim(root, PrimitiveType.Cylinder, "Leg", new Vector3(p.x, underY - legH * 0.5f, p.y), new Vector3(0.11f, legH * 0.5f, 0.11f), legMat);
                else
                    MeshObject(root, "Leg", RoundedBox(new Vector3(p.x - 0.055f, floorY + 0.04f, p.y - 0.055f), new Vector3(p.x + 0.055f, underY, p.y + 0.055f), 0.015f, 0.005f, 2, 1f), legMat);
                Prim(root, PrimitiveType.Cylinder, "Foot", new Vector3(p.x, floorY + 0.03f, p.y), new Vector3(0.14f, 0.03f, 0.14f), ArtLib.Matte(Pal.H("#151515")));
            }
            float railY = floorY + 0.55f;
            Bar(root, new Vector3(lx, railY, -lzN), new Vector3(lx, railY, lzF), 0.035f, legMat, d.roundLegs);
            Bar(root, new Vector3(-lx, railY, -lzN), new Vector3(-lx, railY, lzF), 0.035f, legMat, d.roundLegs);
            Bar(root, new Vector3(-lx, railY + 0.25f, lzF), new Vector3(lx, railY + 0.25f, lzF), 0.035f, legMat, d.roundLegs);
            Bar(root, new Vector3(-lx, floorY + 0.32f, -lzN), new Vector3(lx, floorY + 0.32f, -lzN), 0.04f, legMat, d.roundLegs); // footrest
            Bar(root, new Vector3(-lx, underY - 0.06f, -lzN), new Vector3(lx, underY - 0.06f, -lzN), 0.03f, legMat, d.roundLegs);
            Bar(root, new Vector3(-lx, underY - 0.06f, lzF), new Vector3(lx, underY - 0.06f, lzF), 0.03f, legMat, d.roundLegs);

            float shelfY = underY - 0.55f;
            MeshObject(root, "Shelf", RoundedBox(new Vector3(-lx + 0.05f, shelfY - 0.05f, -lzN + 0.12f), new Vector3(lx - 0.05f, shelfY, lzF - 0.05f), 0.02f, 0.01f, 2, 0.55f), edgeMat);
            if (!party)
            {
                // A couple of books left on the shelf.
                var books = new[] { "#B23A48", "#2E5E8C", "#E0A526", "#3E7C59" };
                float bx = -lx + 0.25f;
                for (int i = 0; i < books.Length; i++)
                {
                    float bw = 0.12f + (i % 2) * 0.05f, bh = 0.42f + (i * 37 % 10) * 0.02f;
                    var b = Prim(root, PrimitiveType.Cube, "Book", new Vector3(bx, shelfY + bh * 0.5f, -lzN + 0.55f), new Vector3(bw, bh, 0.75f), ArtLib.Lit(Pal.H(books[i])));
                    b.transform.localRotation = Quaternion.Euler(0f, 0f, i == books.Length - 1 ? -12f : 0f);
                    bx += bw + 0.02f;
                }
            }

            // ---- floor dressing ----
            if (d.rug.HasValue)
            {
                var rug = MeshObject(root, "Rug", Quad(new Vector3(-hw - 1.3f, floorY + 0.012f, -hd - 1.4f), new Vector3(hw + 1.3f, floorY + 0.012f, hd + far + 1.2f)),
                    DeskArt.Make(DeskArt.Rug(d), Vector2.one, 0.8f));
                rug.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            if (d.Sparkle)
            {
                var starMat = ArtLib.Transparent(DeskArt.Star, new Color(1f, 1f, 1f, 0.9f));
                var spots = new[]
                {
                    new Vector3(-hw - rim, 0.03f, -hd - rim), new Vector3(hw + rim, 0.03f, hd + rim + far), new Vector3(hw * 0.62f + 0.18f, 0.03f, hd + rim + far * 0.6f),
                    new Vector3(hw + 0.02f, 0.03f, -hd * 0.3f), new Vector3(-hw - 0.02f, 0.03f, hd * 0.5f), new Vector3(0.2f, 0.03f, -hd - 0.03f),
                };
                for (int i = 0; i < spots.Length; i++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    s.name = "Twinkle";
                    Object.Destroy(s.GetComponent<Collider>());
                    s.transform.SetParent(root, false);
                    s.transform.localPosition = spots[i];
                    var r = s.GetComponent<MeshRenderer>();
                    r.sharedMaterial = starMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    s.AddComponent<Twinkle>().phase = i * 0.71f;
                }
            }
            return top;
        }

        // =====================================================================
        // pieces
        // =====================================================================

        static GameObject Prim(Transform root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject MeshObject(Transform root, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return go;
        }

        /// <summary>A steel tube (or square bar) from a to b.</summary>
        static void Bar(Transform root, Vector3 a, Vector3 b, float r, Material m, bool round)
        {
            var go = GameObject.CreatePrimitive(round ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = "Bar";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = (a + b) * 0.5f;
            var dir = b - a;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            go.transform.localScale = round ? new Vector3(r * 2f, dir.magnitude * 0.5f, r * 2f) : new Vector3(r * 2f, dir.magnitude, r * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A yellow pencil lying in the groove (along x).</summary>
        static void Pencil(Transform root, Vector3 start, float length)
        {
            var rot = Quaternion.Euler(0f, 0f, 90f);
            float r = 0.034f;
            void Piece(string n, float x0, float x1, float rad, Color c, float metal = 0f)
            {
                var go = Prim(root, PrimitiveType.Cylinder, n, start + new Vector3((x0 + x1) * 0.5f, 0f, 0f), new Vector3(rad * 2f, (x1 - x0) * 0.5f, rad * 2f),
                    metal > 0f ? DeskArt.Metal(c, metal, 0.7f) : ArtLib.Lit(c));
                go.transform.localRotation = rot;
            }
            float body = length * 0.82f;
            Piece("PencilWood", -0.12f, 0f, r * 0.75f, Pal.H("#E8C79A"));
            Piece("PencilLead", -0.17f, -0.12f, r * 0.3f, Pal.H("#2A2A2A"));
            Piece("PencilBody", 0f, body, r, Pal.H("#F2B705"));
            Piece("PencilBand", body, body + 0.06f, r * 1.05f, Pal.H("#C9CED6"), 0.8f);
            Piece("PencilEraser", body + 0.06f, body + 0.14f, r, Pal.H("#E8798A"));
        }

        static readonly Dictionary<Color, Texture2D> grooveTex = new Dictionary<Color, Texture2D>();

        /// <summary>Across the groove: a dark shadowed hollow with a lit far lip.</summary>
        static Texture2D GrooveTex(Color wood)
        {
            if (grooveTex.TryGetValue(wood, out var t) && t != null) return t;
            const int W = 8, H = 32;
            t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            var dark = Color.Lerp(wood, Color.black, 0.65f);
            var lit = Color.Lerp(wood, Color.white, 0.25f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (H - 1f);                      // 0 near lip … 1 far lip
                float hollow = Mathf.Sin(v * Mathf.PI);
                var c = Color.Lerp(wood, dark, hollow * 0.8f);
                if (v > 0.55f) c = Color.Lerp(c, lit, Mathf.Clamp01((v - 0.55f) / 0.4f) * (1f - Mathf.Clamp01((v - 0.9f) / 0.1f)) * 0.6f);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            grooveTex[wood] = t;
            return t;
        }

        // =====================================================================
        // meshes
        // =====================================================================

        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> tri = new List<int>();

            public int Add(Vector3 p, Vector2 u) { v.Add(p); uv.Add(u); return v.Count - 1; }

            /// <summary>Triangle facing 'outward' whatever order the corners come in.</summary>
            public void Tri(int a, int b, int c, Vector3 outward)
            {
                var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(n, outward) < 0f) { tri.Add(a); tri.Add(c); tri.Add(b); }
                else { tri.Add(a); tri.Add(b); tri.Add(c); }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetTriangles(tri, 0);
                m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Flat quad on y, from min to max (u along x, v along z = far end up).</summary>
        static Mesh Quad(Vector3 min, Vector3 max)
        {
            var b = new Builder();
            float y = min.y;
            int a = b.Add(new Vector3(min.x, y, min.z), new Vector2(0, 0));
            int c = b.Add(new Vector3(max.x, y, min.z), new Vector2(1, 0));
            int d = b.Add(new Vector3(max.x, y, max.z), new Vector2(1, 1));
            int e = b.Add(new Vector3(min.x, y, max.z), new Vector2(0, 1));
            b.Tri(a, c, d, Vector3.up);
            b.Tri(a, d, e, Vector3.up);
            return b.Build("Quad");
        }

        /// <summary>
        /// A box with rounded corners (seen from above) and a rounded top edge (bevel), so light
        /// catches the edge like a real desk. UVs are planar in world units x uvScale.
        /// </summary>
        static Mesh RoundedBox(Vector3 min, Vector3 max, float radius, float bevel, int seg, float uvScale)
        {
            float sx = max.x - min.x, sz = max.z - min.z, sy = max.y - min.y;
            radius = Mathf.Min(radius, sx * 0.5f - 0.001f, sz * 0.5f - 0.001f);
            bevel = Mathf.Min(bevel, radius * 0.95f, sy * 0.45f);
            // Perimeter (counter-clockwise seen from above) with outward normals.
            var P = new List<Vector2>();
            var N = new List<Vector2>();
            var centres = new[]
            {
                new Vector2(max.x - radius, max.z - radius), new Vector2(min.x + radius, max.z - radius),
                new Vector2(min.x + radius, min.z + radius), new Vector2(max.x - radius, min.z + radius),
            };
            for (int c = 0; c < 4; c++)
            for (int k = 0; k <= seg; k++)
            {
                float a = (c * 90f + 90f * k / seg) * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                P.Add(centres[c] + n * radius);
                N.Add(n);
            }
            int count = P.Count;
            var len = new float[count + 1];
            for (int i = 1; i <= count; i++) len[i] = len[i - 1] + Vector2.Distance(P[i - 1], P[i % count]);

            // Rings from the top edge down: bevel arc, then straight down to the bottom.
            var ringInset = new List<float>();
            var ringY = new List<float>();
            var ringUp = new List<float>();
            const int bs = 4;
            for (int k = 0; k <= bs; k++)
            {
                float th = 90f * k / bs * Mathf.Deg2Rad;
                ringInset.Add(bevel * (1f - Mathf.Sin(th)));
                ringY.Add(max.y - bevel * (1f - Mathf.Cos(th)));
                ringUp.Add(Mathf.Cos(th));
            }
            ringInset.Add(0f); ringY.Add(min.y); ringUp.Add(0f);

            var b = new Builder();
            var rings = new int[ringY.Count, count + 1];
            for (int r = 0; r < ringY.Count; r++)
            for (int i = 0; i <= count; i++)
            {
                int ii = i % count;
                var p = P[ii] - N[ii] * ringInset[r];
                rings[r, i] = b.Add(new Vector3(p.x, ringY[r], p.y), new Vector2(len[i] * uvScale, ringY[r] * uvScale));
            }
            for (int r = 0; r + 1 < ringY.Count; r++)
            for (int i = 0; i < count; i++)
            {
                var n2 = N[i];
                var outward = new Vector3(n2.x, (ringUp[r] + ringUp[r + 1]) * 0.5f, n2.y);
                b.Tri(rings[r, i], rings[r, i + 1], rings[r + 1, i + 1], outward);
                b.Tri(rings[r, i], rings[r + 1, i + 1], rings[r + 1, i], outward);
            }
            // Caps (own vertices so their edges stay crisp).
            void Cap(float y, float inset, Vector3 outward)
            {
                var centre = new Vector3((min.x + max.x) * 0.5f, y, (min.z + max.z) * 0.5f);
                int ci = b.Add(centre, new Vector2(centre.x, centre.z) * uvScale);
                var idx = new int[count];
                for (int i = 0; i < count; i++)
                {
                    var p = P[i] - N[i] * inset;
                    idx[i] = b.Add(new Vector3(p.x, y, p.y), new Vector2(p.x, p.y) * uvScale);
                }
                for (int i = 0; i < count; i++) b.Tri(ci, idx[i], idx[(i + 1) % count], outward);
            }
            Cap(max.y, bevel, Vector3.up);
            Cap(min.y, 0f, Vector3.down);
            return b.Build("RoundedBox");
        }
    }

    /// <summary>A star that faces the camera and twinkles now and then (Diamond desks).</summary>
    public class Twinkle : MonoBehaviour
    {
        public float phase;
        Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            float t = Mathf.Repeat(Time.unscaledTime * 0.35f + phase, 1f);
            float s = t < 0.18f ? Mathf.Sin(t / 0.18f * Mathf.PI) : 0f;
            transform.localScale = Vector3.one * (0.02f + s * 0.26f);
        }
    }
}
