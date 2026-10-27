using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Procedural PBR maps for the desks: colour, normal (relief of grain, carvings, veins,
    /// leather) and a metal/gloss map, plus floor, rug, crest and a room reflection cubemap.
    /// Photo textures can replace the top: Resources/TT/Desks/&lt;id&gt;/albedo (+ normal).
    /// </summary>
    public static class DeskArt
    {
        public class Maps
        {
            public Texture2D albedo, normal, metalGloss;
            public void Destroy()
            {
                if (albedo != null) Object.Destroy(albedo);
                if (normal != null) Object.Destroy(normal);
                if (metalGloss != null) Object.Destroy(metalGloss);
            }
        }

        static Material deskBase;

        /// <summary>Standard shader with normal + metal/gloss maps (variant kept by Resources/TT/Materials/TTDesk).</summary>
        static Material DeskBase
        {
            get
            {
                if (deskBase != null) return deskBase;
                deskBase = Resources.Load<Material>("TT/Materials/TTDesk");
                if (deskBase == null)
                {
                    deskBase = new Material(ArtLib.LitBase);
                    deskBase.EnableKeyword("_NORMALMAP");
                    deskBase.EnableKeyword("_METALLICGLOSSMAP");
                }
                return deskBase;
            }
        }

        public static Material Make(Maps m, Vector2 tiling, float normalScale = 1f)
        {
            var mat = new Material(DeskBase) { color = Color.white };
            Set(mat, "_MainTex", m.albedo, tiling);
            Set(mat, "_BumpMap", m.normal, tiling);
            Set(mat, "_MetallicGlossMap", m.metalGloss, tiling);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICGLOSSMAP");
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", normalScale);
            if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1f);
            if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
            return mat;
        }

        static void Set(Material m, string prop, Texture tex, Vector2 tiling)
        {
            if (tex == null || !m.HasProperty(prop)) return;
            m.SetTexture(prop, tex);
            m.SetTextureScale(prop, tiling);
        }

        /// <summary>Shiny metal or painted steel (no texture).</summary>
        public static Material Metal(Color c, float metal, float smooth)
        {
            var m = new Material(ArtLib.LitBase) { color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        // =====================================================================
        // building blocks
        // =====================================================================

        /// <summary>Colour, height (0..1) and gloss per pixel, then packed into the three maps.</summary>
        class Canvas
        {
            public readonly int w, h;
            public readonly Color[] col;
            public readonly float[] hgt, gloss;
            public float metal;
            /// <summary>Optional metal per pixel (overrides metal).</summary>
            public float[] mtl;

            public Canvas(int w, int h)
            {
                this.w = w; this.h = h;
                col = new Color[w * h];
                hgt = new float[w * h];
                gloss = new float[w * h];
            }

            public Maps Pack(float bump, bool wrap, string name)
            {
                var mode = wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var a = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = mode, name = name, anisoLevel = 4 };
                a.SetPixels(col);
                a.Apply(true);

                var n = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = mode, name = name + "_n", anisoLevel = 4 };
                var np = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int xl = wrap ? (x - 1 + w) % w : Mathf.Max(0, x - 1), xr = wrap ? (x + 1) % w : Mathf.Min(w - 1, x + 1);
                    int yd = wrap ? (y - 1 + h) % h : Mathf.Max(0, y - 1), yu = wrap ? (y + 1) % h : Mathf.Min(h - 1, y + 1);
                    float dx = (hgt[y * w + xr] - hgt[y * w + xl]) * bump;
                    float dy = (hgt[yu * w + x] - hgt[yd * w + x]) * bump;
                    var v = new Vector3(-dx, -dy, 1f).normalized;
                    np[y * w + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
                n.SetPixels(np);
                n.Apply(true);

                var g = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = mode, name = name + "_mg" };
                var gp = new Color[w * h];
                for (int i = 0; i < gp.Length; i++) gp[i] = new Color(mtl != null ? mtl[i] : metal, 0f, 0f, Mathf.Clamp01(gloss[i]));
                g.SetPixels(gp);
                g.Apply(true);
                return new Maps { albedo = a, normal = n, metalGloss = g };
            }
        }

        static float Fbm(float x, float y, int oct = 4)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++)
            {
                v += a * Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 9.1f);
                f *= 2f; a *= 0.5f;
            }
            return v / 0.9375f;
        }

        /// <summary>Wood grain along v: rings that wander, with figure. Returns (tone 0..1, groove 0..1).</summary>
        static Vector2 Grain(float gx, float gy, float spacing, float wander, int seed)
        {
            float xw = gx + wander * (Mathf.Sin(gy * 0.011f + gx * 0.006f + seed) * 0.6f + Fbm(gx * 0.012f + seed, gy * 0.003f, 3) * 1.4f);
            float ring = Mathf.Repeat(xw / spacing, 1f);
            float late = Mathf.SmoothStep(0f, 1f, Mathf.Abs(ring - 0.5f) * 2f);    // latewood band
            float fine = Mathf.PerlinNoise(gx * 0.9f, gy * 0.02f + seed);
            return new Vector2(late * 0.7f + fine * 0.3f, late);
        }

        // =====================================================================
        // desk tops
        // =====================================================================

        static readonly Dictionary<string, Maps> topCache = new Dictionary<string, Maps>();

        /// <summary>The playing top for a desk (cached; w x h covers the whole top, v = far end up).</summary>
        public static Maps Top(DeskDef d, int w, int h)
        {
            string key = d.id + w + "x" + h;
            if (topCache.TryGetValue(key, out var hit) && hit.albedo != null) return hit;
            // Drop other desks' tops: only one is on screen.
            foreach (var kv in topCache) if (kv.Key != key) kv.Value.Destroy();
            topCache.Clear();

            var photo = Resources.Load<Texture2D>("TT/Desks/" + d.id + "/albedo");
            Maps maps;
            if (photo != null)
            {
                var c = Paint(d, 4, 4);
                maps = c.Pack(1f, false, d.id);
                maps.albedo = photo;
                var pn = Resources.Load<Texture2D>("TT/Desks/" + d.id + "/normal");
                if (pn != null) maps.normal = pn;
            }
            else maps = Paint(d, w, h).Pack(Bump(d.surface), false, d.id);
            topCache[key] = maps;
            return maps;
        }

        static readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();

        /// <summary>A small picture of the top for menus (colour only).</summary>
        public static Texture2D Preview(DeskDef d)
        {
            if (previews.TryGetValue(d.id, out var t) && t != null) return t;
            var photo = Resources.Load<Texture2D>("TT/Desks/" + d.id + "/albedo");
            if (photo != null) { previews[d.id] = photo; return photo; }
            var c = Paint(d, 120, 216);
            t = new Texture2D(c.w, c.h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = d.id + "_preview" };
            // UI images show row 0 at the bottom, like the desk's near end: no flip needed.
            t.SetPixels(c.col);
            t.Apply(false);
            previews[d.id] = t;
            return t;
        }

        static float Bump(DeskSurface s) => s switch
        {
            DeskSurface.OldWood => 5f,
            DeskSurface.Plywood => 4f,
            DeskSurface.Paint => 7f,
            DeskSurface.Leather => 10f,
            DeskSurface.Steel => 3f,
            DeskSurface.Marble => 2f,
            DeskSurface.Glass => 1f,
            DeskSurface.Laminate => 2.5f,
            DeskSurface.Court => 4f,
            _ => 4f,
        };

        static Canvas Paint(DeskDef d, int w, int h)
        {
            var c = new Canvas(w, h);
            var rng = new System.Random(d.index * 131 + 7);
            float k2 = w / 256f;
            switch (d.surface)
            {
                case DeskSurface.OldWood:
                case DeskSurface.Plywood:
                case DeskSurface.Paint:
                case DeskSurface.Court:
                    Wood(c, d.top, d.topDark, Color.Lerp(d.top, Color.white, 0.35f), k2, d.surface == DeskSurface.Plywood ? 26f : 19f,
                        d.surface == DeskSurface.Plywood ? 4f : 9f, d.index, 0.12f, 0.2f);
                    Wear(c, d.topDark, 0.12f);
                    if (d.surface != DeskSurface.Court) Scratches(c, rng, Color.Lerp(d.top, Color.white, 0.55f), d.topDark, k2, d.surface == DeskSurface.OldWood ? 90 : 50);
                    Sheen(c, 0.08f);
                    if (d.surface == DeskSurface.Paint) Splatter(c, rng);
                    if (d.surface == DeskSurface.Court) CourtLines(c);
                    if (d.carvings)
                    {
                        float s = w / 342f;
                        Ring(c, new Vector2(0.62f * w, 0.84f * h), 19f * s, 12f * s, 1.6f * s, 0.28f);
                        Ring(c, new Vector2(0.74f * w, 0.33f * h), 2.5f * s, 3f * s, 2.5f * s, 0.55f);
                        Carve(c, "#", new Vector2(0.20f, 0.86f), 15f * s, -4f);
                        Carve(c, "LAST BENCH", new Vector2(0.86f, 0.70f), 10f * s, -84f);
                        Carve(c, d.index == 0 ? "RAJU" : d.index == 14 ? "HOSTEL" : "GOLU", new Vector2(0.14f, 0.62f), 8f * s, 80f);
                        Carve(c, d.index == 19 ? "STARS" : "RAJU", new Vector2(0.24f, 0.32f), 12f * s, 78f);
                        Carve(c, "AJ", new Vector2(0.80f, 0.16f), 22f * s, 24f);
                    }
                    break;
                case DeskSurface.Polished:
                    Wood(c, d.top, d.topDark, Color.Lerp(d.top, Color.white, 0.25f), k2, 14f, 6f, d.index, 0.55f, 0.08f);
                    Scratches(c, rng, Color.Lerp(d.top, Color.white, 0.3f), d.topDark, k2, 18);
                    Sheen(c, 0.10f);
                    break;
                case DeskSurface.Laminate:
                    Laminate(c, d.top, d.topDark, rng, 0.42f);
                    Wear(c, d.topDark, 0.06f);
                    Scratches(c, rng, Color.Lerp(d.top, Color.white, 0.35f), d.topDark, k2, 25);
                    Sheen(c, 0.08f);
                    break;
                case DeskSurface.Steel:
                    Steel(c, d.top, d.topDark, rng);
                    Scratches(c, rng, Color.Lerp(d.top, Color.white, 0.5f), d.topDark, k2, 60);
                    c.metal = 0.85f;
                    break;
                case DeskSurface.Marble:
                    Marble(c, d.top, d.topDark, d.veins ?? d.topDark, d.index);
                    Sheen(c, 0.05f);
                    break;
                case DeskSurface.Glass:
                    // A sheet of glass over polished wood (or black glass with flecks).
                    var under = d.inset ?? d.top;
                    if (under.grayscale < 0.12f || d.index >= 24) Laminate(c, under, d.topDark, rng, 0.78f, flecks: d.index == 24);
                    else Wood(c, under, d.topDark, Color.Lerp(under, Color.white, 0.25f), k2, 14f, 6f, d.index, 0.72f, 0.05f);
                    for (int i = 0; i < c.col.Length; i++) c.hgt[i] *= 0.2f;
                    GlassEdge(c);
                    Sheen(c, 0.06f);
                    break;
                case DeskSurface.Leather:
                    Wood(c, d.top, d.topDark, Color.Lerp(d.top, Color.white, 0.25f), k2, 14f, 6f, d.index, 0.55f, 0.08f);
                    Leather(c, d.inset ?? Pal.H("#2F5D46"), rng, d.Trim ?? Pal.H("#C9A54A"));
                    Sheen(c, 0.08f);
                    break;
            }
            return c;
        }

        static void Wood(Canvas c, Color baseC, Color dark, Color light, float k2, float spacing, float wander, int seed, float gloss, float speckAmt)
        {
            var rng = new System.Random(seed * 7 + 3);
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float gx = x / k2, gy = y / k2;
                var g = Grain(gx, gy, spacing, wander, seed);
                var col = Color.Lerp(baseC, dark, g.y * 0.12f);
                col = Color.Lerp(col, light, (1f - g.x) * 0.05f);
                float tone = (Fbm(gx * 0.006f + seed, gy * 0.002f, 3) - 0.5f) * 0.28f;
                col = Color.Lerp(col, tone > 0 ? light : dark, Mathf.Abs(tone));
                float sp = (float)(rng.NextDouble() - 0.5) * speckAmt * 0.12f;
                int i = y * c.w + x;
                c.col[i] = new Color(col.r + sp, col.g + sp, col.b + sp, 1f);
                c.hgt[i] = 0.5f - g.y * 0.05f + (Mathf.PerlinNoise(gx * 1.3f, gy * 0.05f) - 0.5f) * 0.03f;
                c.gloss[i] = gloss * (1f - g.y * 0.25f);
            }
        }

        /// <summary>Darker, rougher edges where hands rest; shade at the near end.</summary>
        static void Wear(Canvas c, Color dark, float amt)
        {
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float u = x / (float)c.w, v = y / (float)c.h;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float grime = Mathf.Clamp01(1f - edge / 0.06f) * amt;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], dark, grime);
                c.gloss[i] *= 1f - grime;
            }
        }

        static void Sheen(Canvas c, float amt)
        {
            // A soft window reflection band towards the far end.
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float u = x / (float)c.w, v = y / (float)c.h;
                float hl = Mathf.Exp(-((u - 0.55f) * (u - 0.55f) / 0.12f + (v - 0.72f) * (v - 0.72f) / 0.05f)) * amt;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], Color.white, hl);
            }
        }

        static void Laminate(Canvas c, Color baseC, Color dark, System.Random rng, float gloss, bool flecks = false)
        {
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float n = Fbm(x * 0.02f, y * 0.02f, 3);
                float sp = (float)rng.NextDouble();
                var col = Color.Lerp(baseC, dark, (n - 0.5f) * 0.25f + 0.05f);
                if (sp > 0.985f) col = Color.Lerp(col, dark, 0.25f);
                else if (sp < 0.012f) col = Color.Lerp(col, Color.white, 0.15f);
                if (flecks && sp > 0.9975f) col = Pal.H("#D9B44A");
                int i = y * c.w + x;
                c.col[i] = col;
                c.hgt[i] = 0.5f + (n - 0.5f) * 0.1f + (sp - 0.5f) * 0.04f;
                c.gloss[i] = gloss * (0.92f + n * 0.08f);
            }
        }

        static void Steel(Canvas c, Color baseC, Color dark, System.Random rng)
        {
            var rows = new float[c.h];
            for (int y = 0; y < c.h; y++) rows[y] = (float)rng.NextDouble();
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                // Brushed across the desk: streaks run along x.
                float streak = rows[y] * 0.6f + Mathf.PerlinNoise(x * 0.004f, y * 0.9f) * 0.4f;
                float cloud = Fbm(x * 0.01f, y * 0.006f, 3);
                var col = Color.Lerp(dark, baseC, 0.55f + streak * 0.3f + (cloud - 0.5f) * 0.3f);
                int i = y * c.w + x;
                c.col[i] = col;
                c.hgt[i] = 0.5f + (streak - 0.5f) * 0.12f;
                c.gloss[i] = 0.55f + streak * 0.15f;
            }
        }

        static void Marble(Canvas c, Color baseC, Color dark, Color vein, int seed)
        {
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float u = x / (float)c.w * 2f, v = y / (float)c.h * 4f;
                float warp = Fbm(u * 1.4f + seed, v * 1.4f, 5) * 4.5f;
                float s = Mathf.Abs(Mathf.Sin((u * 1.6f + v * 0.9f + warp) * 2.2f));
                float main = Mathf.Pow(1f - s, 9f);                      // thin main veins
                float s2 = Mathf.Abs(Mathf.Sin((u * 3.1f - v * 1.7f + warp * 1.3f) * 3.1f));
                float fine = Mathf.Pow(1f - s2, 40f) * 0.25f;
                float cloud = Fbm(u * 3f + 5f, v * 3f, 4);
                var col = Color.Lerp(baseC, dark, Mathf.Clamp01((cloud - 0.35f) * 0.9f));
                float soft = Mathf.Pow(1f - s, 3f) * 0.45f + Mathf.Pow(1f - s2, 6f) * 0.2f;   // broad grey drifts around the veins
                col = Color.Lerp(col, dark, soft);
                col = Color.Lerp(col, vein, Mathf.Clamp01(main * 1.1f + fine * 0.7f));
                int i = y * c.w + x;
                c.col[i] = col;
                c.hgt[i] = 0.5f - main * 0.08f;
                c.gloss[i] = 0.62f - main * 0.15f;
            }
        }

        static void GlassEdge(Canvas c)
        {
            // The glass sheet's polished bevel catches the light just inside the edge.
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                float u = x / (float)c.w, v = y / (float)c.h;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u) * c.w, Mathf.Min(v, 1f - v) * c.h);
                float line = Mathf.Clamp01(1f - Mathf.Abs(edge - 6f) / 2.5f) * 0.35f;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], Pal.H("#D8F0EC"), line);
                if (edge < 6f) c.hgt[i] = 0.5f + (6f - edge) * 0.02f;
            }
        }

        static void Leather(Canvas c, Color leather, System.Random rng, Color tooling)
        {
            int bx = Mathf.RoundToInt(c.w * 0.09f), by = Mathf.RoundToInt(c.w * 0.09f);
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                int i = y * c.w + x;
                int ex = Mathf.Min(x - bx, c.w - 1 - bx - x), ey = Mathf.Min(y - by, c.h - 1 - by - y);
                int e = Mathf.Min(ex, ey);
                if (e < 0) continue;
                float pebble = Mathf.PerlinNoise(x * 0.45f, y * 0.45f) * 0.6f + Mathf.PerlinNoise(x * 0.11f + 9f, y * 0.11f) * 0.4f;
                var col = Color.Lerp(leather, Color.black, (0.5f - pebble) * 0.35f + 0.05f);
                float cloud = Fbm(x * 0.01f, y * 0.01f, 3);
                col = Color.Lerp(col, Color.Lerp(leather, Color.white, 0.2f), (cloud - 0.5f) * 0.4f);
                // Gold tooled line a little inside the leather.
                float tool = Mathf.Clamp01(1f - Mathf.Abs(e - c.w * 0.025f) / 1.4f);
                col = Color.Lerp(col, tooling, tool * 0.85f);
                c.col[i] = col;
                c.hgt[i] = 0.42f + pebble * 0.12f - (e < 3 ? 0.08f : 0f) - tool * 0.05f;
                c.gloss[i] = 0.32f + tool * 0.4f;
            }
        }

        static void Scratches(Canvas c, System.Random rng, Color lightC, Color dark, float k2, int baseCount)
        {
            int count = Mathf.RoundToInt(baseCount * (c.w * (float)c.h) / (512f * 1024f));
            for (int i = 0; i < count; i++)
            {
                var a0 = new Vector2((float)rng.NextDouble() * c.w, (float)rng.NextDouble() * c.h);
                float ang = (rng.NextDouble() < 0.8 ? 70f + (float)(rng.NextDouble() - 0.5) * 50f : (float)rng.NextDouble() * 180f) * Mathf.Deg2Rad;
                float len = (12f + (float)rng.NextDouble() * 80f) * k2;
                var b0 = a0 + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                bool darkLine = rng.NextDouble() < 0.12;
                Segment(c, a0, b0, 0.9f + (float)rng.NextDouble() * 0.7f, darkLine ? dark : lightC, darkLine ? 0.12f : 0.06f + (float)rng.NextDouble() * 0.1f, -0.03f);
            }
        }

        static void Splatter(Canvas c, System.Random rng)
        {
            var paints = new[] { "#D7263D", "#1B98E0", "#F4D35E", "#2E933C", "#7B2CBF", "#F78C2B", "#FFFFFF" };
            for (int i = 0; i < 70; i++)
            {
                var col = Pal.H(paints[rng.Next(paints.Length)]);
                var p = new Vector2((float)rng.NextDouble() * c.w, (float)rng.NextDouble() * c.h);
                float r = 2f + (float)rng.NextDouble() * (rng.NextDouble() < 0.15 ? 18f : 6f);
                int drops = 1 + rng.Next(5);
                for (int k = 0; k < drops; k++)
                {
                    var q = p + new Vector2((float)(rng.NextDouble() - 0.5) * r * 4f, (float)(rng.NextDouble() - 0.5) * r * 4f);
                    float rr = k == 0 ? r : r * 0.3f;
                    Blob(c, q, rr, col, 0.9f, 0.08f, 0.55f);
                }
            }
        }

        static void CourtLines(Canvas c)
        {
            var white = Pal.H("#F4F1EA");
            var red = Pal.H("#B3261E");
            float w = c.w, h = c.h;
            void Line(Vector2 a, Vector2 b, Color col) => Segment(c, a, b, w * 0.012f, col, 0.9f, 0.02f, 0.5f);
            float m = w * 0.06f;
            Line(new Vector2(m, m), new Vector2(w - m, m), white);
            Line(new Vector2(w - m, m), new Vector2(w - m, h - m), white);
            Line(new Vector2(w - m, h - m), new Vector2(m, h - m), white);
            Line(new Vector2(m, h - m), new Vector2(m, m), white);
            Line(new Vector2(m, h * 0.5f), new Vector2(w - m, h * 0.5f), white);
            Ring(c, new Vector2(w * 0.5f, h * 0.5f), w * 0.18f, w * 0.18f, w * 0.012f, 0.9f, red);
        }

        static void Ring(Canvas c, Vector2 centre, float rx, float ry, float thick, float alpha, Color? colour = null)
        {
            var groove = colour ?? Pal.H("#46200A");
            int x0 = Mathf.FloorToInt(centre.x - rx - thick - 1), x1 = Mathf.CeilToInt(centre.x + rx + thick + 1);
            int y0 = Mathf.FloorToInt(centre.y - ry - thick - 1), y1 = Mathf.CeilToInt(centre.y + ry + thick + 1);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(c.h, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(c.w, x1); x++)
            {
                float dx = (x - centre.x) / rx, dy = (y - centre.y) / ry;
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                float a = Mathf.Clamp01(1f - d / thick) * alpha;
                if (a <= 0f) continue;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], groove, a);
                if (colour == null) c.hgt[i] -= a * 0.05f;
            }
        }

        static void Blob(Canvas c, Vector2 p, float r, Color col, float alpha, float raise, float gloss)
        {
            for (int y = Mathf.Max(0, (int)(p.y - r - 2)); y < Mathf.Min(c.h, (int)(p.y + r + 2)); y++)
            for (int x = Mathf.Max(0, (int)(p.x - r - 2)); x < Mathf.Min(c.w, (int)(p.x + r + 2)); x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), p);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0f) continue;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], col, a * alpha);
                c.hgt[i] += a * raise * Mathf.Clamp01((r - d) / Mathf.Max(1f, r * 0.5f));
                c.gloss[i] = Mathf.Lerp(c.gloss[i], gloss, a);
            }
        }

        static void Segment(Canvas c, Vector2 a, Vector2 b, float thick, Color col, float alpha, float depth, float gloss = -1f)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - thick - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + thick + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - thick - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + thick + 1);
            var ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(c.h, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(c.w, x1); x++)
            {
                var p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t);
                float m = Mathf.Clamp01(1f - (d - thick * 0.5f));
                if (m <= 0f) continue;
                int i = y * c.w + x;
                c.col[i] = Color.Lerp(c.col[i], col, m * alpha);
                c.hgt[i] += depth * m;
                if (gloss >= 0f) c.gloss[i] = Mathf.Lerp(c.gloss[i], gloss, m);
            }
        }

        /// <summary>Knife-carved text: a groove (deep in the normal map) with a light lip.</summary>
        static void Carve(Canvas c, string text, Vector2 uv, float height, float rotDeg)
        {
            float k = height / 6f;
            float advance = 5.6f, total = 0f;
            foreach (char ch in text) total += ch == ' ' ? 3f : advance;
            total -= 1.6f;
            var centre = new Vector2(uv.x * c.w, uv.y * c.h);
            float rad = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            Vector2 Map(float gx, float gy)
            {
                float lx = (gx - total * 0.5f) * k, ly = (gy - 3f) * k;
                return centre + new Vector2(lx * cs - ly * sn, lx * sn + ly * cs);
            }
            var groove = Pal.H("#46200A");
            var lip = Pal.H("#FFE2B8");
            float thick = Mathf.Max(1.1f, k * 0.55f);
            float pen = 0f;
            foreach (char ch in text)
            {
                if (ch == ' ') { pen += 3f; continue; }
                if (ArtLib.Glyph(ch, out var strokes))
                    foreach (var st in strokes)
                        for (int i = 0; i + 3 < st.Length; i += 2)
                        {
                            var a = Map(pen + st[i], st[i + 1]);
                            var b = Map(pen + st[i + 2], st[i + 3]);
                            Segment(c, a + new Vector2(0.8f, -0.8f), b + new Vector2(0.8f, -0.8f), thick, lip, 0.18f, 0f);
                            Segment(c, a, b, thick, groove, 0.38f, -0.35f, 0.05f);
                        }
                pen += advance;
            }
        }

        // =====================================================================
        // edges, floors, rugs, crest
        // =====================================================================

        static readonly Dictionary<string, Maps> tiledCache = new Dictionary<string, Maps>();

        /// <summary>Tiling grain for the desk's edges and frame (polished, along u).</summary>
        public static Maps Edge(DeskDef d)
        {
            string key = "edge" + d.id;
            if (tiledCache.TryGetValue(key, out var m) && m.albedo != null) return m;
            var c = new Canvas(128, 128);
            bool metalEdge = d.surface == DeskSurface.Steel;
            for (int y = 0; y < c.h; y++)
            for (int x = 0; x < c.w; x++)
            {
                int i = y * c.w + x;
                float n = Mathf.PerlinNoise(x * 0.04f, y * 0.6f) * 0.7f + Mathf.PerlinNoise(x * 0.4f, y * 2.1f) * 0.3f;
                if (metalEdge)
                {
                    c.col[i] = Color.Lerp(d.edge, Color.white, n * 0.15f);
                    c.gloss[i] = 0.6f;
                }
                else
                {
                    float ring = Mathf.Repeat(y / 9f + Mathf.Sin(x * 0.05f) * 0.4f, 1f);
                    float late = Mathf.SmoothStep(0f, 1f, Mathf.Abs(ring - 0.5f) * 2f);
                    c.col[i] = Color.Lerp(d.edge, Color.Lerp(d.edge, Color.black, 0.4f), late * 0.3f + n * 0.1f);
                    c.gloss[i] = d.surface == DeskSurface.OldWood || d.surface == DeskSurface.Plywood ? 0.15f : 0.5f;
                }
                c.hgt[i] = n * 0.3f;
            }
            c.metal = metalEdge ? 0.8f : 0f;
            m = c.Pack(2f, true, key);
            tiledCache[key] = m;
            return m;
        }

        public static Maps Floor(DeskDef d)
        {
            string key = "floor" + d.floor + ColorUtility.ToHtmlStringRGB(d.floorA) + ColorUtility.ToHtmlStringRGB(d.floorB);
            if (tiledCache.TryGetValue(key, out var m) && m.albedo != null) return m;
            const int S = 128;
            var c = new Canvas(S, S);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                float n = Fbm(x * 0.05f, y * 0.05f, 3);
                Color col; float h = 0.5f, g = 0.2f;
                switch (d.floor)
                {
                    case FloorKind.Planks:
                    {
                        int plank = y / 16;
                        int off = (plank * 37) % S;
                        bool seam = y % 16 == 0 || (x + off) % 96 == 0;
                        float tone = Mathf.PerlinNoise(plank * 3.1f, 0.5f) * 0.25f;
                        float grain = Mathf.PerlinNoise(x * 0.06f, y * 0.8f);
                        col = Color.Lerp(d.floorA, d.floorB, tone + grain * 0.25f);
                        if (seam) { col = d.floorB * 0.7f; h = 0.2f; }
                        g = seam ? 0.05f : 0.35f;
                        break;
                    }
                    case FloorKind.Carpet:
                        col = Color.Lerp(d.floorA, d.floorB, Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.5f + n * 0.3f);
                        h = Mathf.PerlinNoise(x * 0.9f, y * 0.9f); g = 0.02f;
                        break;
                    case FloorKind.Checker:
                    {
                        bool a = ((x / 32) + (y / 32)) % 2 == 0;
                        bool grout = x % 32 == 0 || y % 32 == 0;
                        col = a ? d.floorA : d.floorB;
                        col = Color.Lerp(col, Color.black, (n - 0.5f) * 0.12f);
                        if (grout) { col = Color.Lerp(d.floorB, Color.gray, 0.5f); h = 0.3f; }
                        g = grout ? 0.05f : 0.55f;
                        break;
                    }
                    case FloorKind.Concrete:
                        col = Color.Lerp(d.floorA, d.floorB, n * 0.8f + (Mathf.PerlinNoise(x * 0.5f, y * 0.5f) - 0.5f) * 0.2f);
                        h = n; g = 0.12f;
                        break;
                    case FloorKind.Stage:
                        col = Color.Lerp(d.floorA, d.floorB, Mathf.Clamp01(n * 0.4f));
                        if (x % 64 == 0 || y % 64 == 0) col = Color.Lerp(col, d.floorB, 0.8f);
                        g = 0.75f;
                        break;
                    default:
                    {
                        bool grout = x % 32 <= 1 || y % 32 <= 1;
                        col = Color.Lerp(d.floorA, Color.Lerp(d.floorA, Color.black, 0.2f), n * 0.5f);
                        if (grout) { col = d.floorB; h = 0.2f; }
                        g = grout ? 0.05f : 0.4f;
                        break;
                    }
                }
                c.col[i] = col; c.hgt[i] = h; c.gloss[i] = g;
            }
            m = c.Pack(3f, true, key);
            tiledCache[key] = m;
            return m;
        }

        public static Maps Rug(DeskDef d)
        {
            string key = "rug" + d.id;
            if (tiledCache.TryGetValue(key, out var m) && m.albedo != null) return m;
            const int W = 256, H = 256;
            var c = new Canvas(W, H);
            var main = d.rug ?? Pal.H("#7A1E22");
            var trim = d.Trim ?? Pal.H("#C9A54A");
            var dark = Color.Lerp(main, Color.black, 0.45f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                int e = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                float pile = Mathf.PerlinNoise(x * 0.8f, y * 0.8f);
                var col = main;
                if (e < 6) col = dark;
                else if (e < 10) col = Color.Lerp(trim, main, 0.35f);
                else if (e < 26)
                {
                    // Border: a row of diamonds.
                    float t = Mathf.Repeat((x + y) / 16f, 1f);
                    float t2 = Mathf.Repeat((x - y) / 16f, 1f);
                    bool dia = Mathf.Abs(t - 0.5f) + Mathf.Abs(t2 - 0.5f) < 0.35f;
                    col = dia ? Color.Lerp(trim, main, 0.3f) : dark;
                }
                else if (e < 29) col = Color.Lerp(trim, main, 0.35f);
                else
                {
                    float cx = (x - W * 0.5f) / (W * 0.5f), cy = (y - H * 0.5f) / (H * 0.5f);
                    float r = Mathf.Abs(cx) + Mathf.Abs(cy);
                    if (Mathf.Abs(r - 0.42f) < 0.03f || r < 0.12f) col = Color.Lerp(trim, main, 0.4f);
                }
                col = Color.Lerp(col, Color.black, (0.5f - pile) * 0.15f);
                c.col[i] = col; c.hgt[i] = pile * 0.5f; c.gloss[i] = 0.03f;
            }
            m = c.Pack(2f, false, key);
            tiledCache[key] = m;
            return m;
        }

        /// <summary>Oval metal plate with "TT" engraved (premium desks).</summary>
        public static Maps Crest(Color metal, Color behind)
        {
            string key = "crest" + ColorUtility.ToHtmlStringRGB(metal) + ColorUtility.ToHtmlStringRGB(behind);
            if (tiledCache.TryGetValue(key, out var m) && m.albedo != null) return m;
            const int W = 256, H = 128;
            var c = new Canvas(W, H) { mtl = new float[W * H] };
            var dark = Color.Lerp(metal, Color.black, 0.5f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float dx = (x - W * 0.5f) / (W * 0.48f), dy = (y - H * 0.5f) / (H * 0.46f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 1f) { c.col[i] = behind; c.hgt[i] = 0.2f; c.gloss[i] = 0.4f; continue; }
                float rim = Mathf.Clamp01((r - 0.82f) / 0.18f);
                var col = Color.Lerp(Color.Lerp(metal, Color.white, 0.25f * (1f - dy)), dark, rim * 0.5f);
                if (Mathf.Abs(r - 0.78f) < 0.025f) col = dark;
                c.col[i] = col; c.hgt[i] = 0.6f - rim * 0.4f; c.gloss[i] = 0.8f; c.mtl[i] = 0.9f;
            }
            Carve(c, "TT", new Vector2(0.5f, 0.5f), 62f, 0f);
            m = c.Pack(4f, false, key);
            tiledCache[key] = m;
            return m;
        }

        static Texture2D star;

        /// <summary>A soft four-point twinkle (Diamond desks).</summary>
        public static Texture2D Star
        {
            get
            {
                if (star != null) return star;
                const int S = 64;
                star = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S * 0.5f + 0.5f) / (S * 0.5f), dy = (y - S * 0.5f + 0.5f) / (S * 0.5f);
                    float core = Mathf.Exp(-(dx * dx + dy * dy) * 30f);
                    float rays = Mathf.Exp(-Mathf.Abs(dx) * 40f) * Mathf.Clamp01(1f - Mathf.Abs(dy)) + Mathf.Exp(-Mathf.Abs(dy) * 40f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                    float a = Mathf.Clamp01(core + rays * 0.8f);
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
                star.SetPixels(px);
                star.Apply();
                return star;
            }
        }

        // =====================================================================
        // reflections
        // =====================================================================

        static Cubemap room;
        static string roomKey;

        /// <summary>A small cubemap of the room (tube light above, walls, floor) for metal and gloss to reflect.</summary>
        public static Cubemap Room(DeskDef d)
        {
            string key = ColorUtility.ToHtmlStringRGB(d.wall) + ColorUtility.ToHtmlStringRGB(d.floorA) + ColorUtility.ToHtmlStringRGB(d.light);
            if (room != null && roomKey == key) return room;
            if (room != null) Object.Destroy(room);
            const int S = 64;
            room = new Cubemap(S, TextureFormat.RGBA32, true) { name = "Room" };
            roomKey = key;
            var wall = d.wall;
            var floor = Color.Lerp(d.floorA, d.floorB, 0.3f);
            var ceiling = Color.Lerp(d.wall, Color.white, 0.5f);
            var px = new Color[S * S];
            foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
            {
                if (face == CubemapFace.Unknown) continue;
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    Vector3 dir = face switch
                    {
                        CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                        CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                        CubemapFace.PositiveY => new Vector3(u, 1f, v),
                        CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                        CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                        _ => new Vector3(-u, -v, -1f),
                    };
                    dir.Normalize();
                    Color col;
                    if (dir.y > 0.25f)
                    {
                        col = ceiling;
                        // Two tube lights across the ceiling.
                        float lx = dir.x / dir.y, lz = dir.z / dir.y;
                        if (Mathf.Abs(lx) < 0.9f && (Mathf.Abs(lz - 0.35f) < 0.06f || Mathf.Abs(lz + 0.35f) < 0.06f)) col = d.light * 1.5f;
                    }
                    else if (dir.y < -0.15f) col = floor * 0.8f;
                    else
                    {
                        col = Color.Lerp(wall * 0.85f, wall, (dir.y + 0.15f) / 0.4f);
                        // A bright window on one side.
                        if (dir.x > 0.6f && dir.y > -0.05f && dir.y < 0.22f && Mathf.Abs(dir.z) < 0.35f) col = Color.Lerp(Color.white, d.light, 0.3f) * 1.6f;
                    }
                    col.a = 1f;
                    px[y * S + x] = col;
                }
                room.SetPixels(px, face);
            }
            room.Apply(true);
            return room;
        }
    }
}
