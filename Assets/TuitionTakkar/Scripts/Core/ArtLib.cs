using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TuitionTakkar
{
    /// <summary>Materials and procedural textures. No imported art is needed.</summary>
    public static class ArtLib
    {
        static Material litBase;
        static Material lineBase;
        static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();
        static Texture2D laminate, graph, dash;

        public static Material LitBase
        {
            get
            {
                if (litBase != null) return litBase;
                litBase = Resources.Load<Material>("TT/Materials/TTLit");
                if (litBase == null)
                {
                    Shader s = null;
                    var rp = GraphicsSettings.currentRenderPipeline;
                    if (rp != null) s = rp.defaultShader;
                    if (s == null) s = Shader.Find("Standard");
                    if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
                    litBase = new Material(s);
                }
                return litBase;
            }
        }

        public static Material Lit(Color c)
        {
            if (litCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(LitBase) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            litCache[c] = m;
            return m;
        }

        /// <summary>Matte surface with a tiled texture. Sets both Built-in and URP property names.</summary>
        public static Material LitTextured(Texture2D tex, Vector2 tiling, float smoothness = 0.08f)
        {
            var m = new Material(LitBase) { color = Color.white };
            SetTex(m, "_MainTex", tex, tiling);
            SetTex(m, "_BaseMap", tex, tiling);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            return m;
        }

        static void SetTex(Material m, string prop, Texture2D tex, Vector2 tiling)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, tex);
            m.SetTextureScale(prop, tiling);
        }

        /// <summary>Flat matte colour (no shine), for the bench rim, shadow and props.</summary>
        public static Material Matte(Color c)
        {
            var m = new Material(LitBase) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return m;
        }

        public static Material Line(Color c)
        {
            if (lineBase == null)
            {
                lineBase = Resources.Load<Material>("TT/Materials/TTLine");
                if (lineBase == null) lineBase = new Material(Shader.Find("Sprites/Default"));
            }
            var m = new Material(lineBase) { color = c, mainTexture = Dash };
            return m;
        }

        /// <summary>Pale green sunmica laminate, like an old tuition bench.</summary>
        public static Texture2D Laminate
        {
            get
            {
                if (laminate != null) return laminate;
                const int n = 256;
                laminate = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Laminate" };
                var baseC = Pal.Laminate;
                var rng = new System.Random(7);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Fine diagonal streaks + speckle, all tileable.
                    float d1 = Mathf.Repeat(x * 0.47f + y * 0.88f, 11f);
                    float d2 = Mathf.Repeat(x * 0.88f - y * 0.47f, 17f);
                    float streak = d1 < 2f ? 0.05f : 0f;
                    float faint = d2 < 1f ? -0.02f : 0f;
                    float speck = (float)(rng.NextDouble() - 0.5) * 0.035f;
                    float k = 1f + streak + faint + speck;
                    px[y * n + x] = new Color(baseC.r * k, baseC.g * k, baseC.b * k, 1f);
                }
                laminate.SetPixels(px);
                laminate.Apply(true);
                return laminate;
            }
        }

        /// <summary>One cell of graph paper; tile it at 18 px.</summary>
        public static Texture2D Graph
        {
            get
            {
                if (graph != null) return graph;
                const int n = 18;
                graph = new Texture2D(n, n, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Graph" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x == 0 || y == n - 1) ? Pal.GraphLine : Pal.Paper;
                graph.SetPixels(px);
                graph.Apply();
                return graph;
            }
        }

        static Texture2D floorTiles, cork;

        /// <summary>Beige classroom floor tile with grout lines (one tile, repeat it).</summary>
        public static Texture2D FloorTiles
        {
            get
            {
                if (floorTiles != null) return floorTiles;
                const int n = 128;
                floorTiles = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "FloorTiles" };
                var baseC = Pal.H("#D8D2C2");
                var grout = Pal.H("#9C9282");
                var rng = new System.Random(21);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = x < 3 || y < 3;
                    float speck = (float)(rng.NextDouble() - 0.5) * 0.04f;
                    var c = line ? grout : baseC;
                    px[y * n + x] = new Color(c.r + speck, c.g + speck, c.b + speck, 1f);
                }
                floorTiles.SetPixels(px);
                floorTiles.Apply(true);
                return floorTiles;
            }
        }

        /// <summary>Cork notice-board texture (tile it at 32 px).</summary>
        public static Texture2D Cork
        {
            get
            {
                if (cork != null) return cork;
                const int n = 32;
                cork = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "Cork" };
                var baseC = Pal.H("#C99A61");
                var dark = Pal.H("#8A5F2E");
                var rng = new System.Random(33);
                var px = new Color[n * n];
                for (int i = 0; i < px.Length; i++)
                {
                    double r = rng.NextDouble();
                    px[i] = r < 0.12 ? Color.Lerp(baseC, dark, 0.7f) : r < 0.2 ? Color.Lerp(baseC, Color.white, 0.12f) : baseC;
                }
                cork.SetPixels(px);
                cork.Apply();
                return cork;
            }
        }

        /// <summary>Dash pattern for aim lines (tiled along the line).</summary>
        public static Texture2D Dash
        {
            get
            {
                if (dash != null) return dash;
                dash = new Texture2D(16, 4, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Dash" };
                var px = new Color[16 * 4];
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 16; x++)
                    px[y * 16 + x] = x < 9 ? Color.white : new Color(1, 1, 1, 0);
                dash.SetPixels(px);
                dash.Apply();
                return dash;
            }
        }

        /// <summary>See-through textured quad material (shadow blob), from the line shader.</summary>
        public static Material Transparent(Texture2D tex, Color tint)
        {
            if (lineBase == null)
            {
                lineBase = Resources.Load<Material>("TT/Materials/TTLine");
                if (lineBase == null) lineBase = new Material(Shader.Find("Sprites/Default"));
            }
            return new Material(lineBase) { color = tint, mainTexture = tex };
        }

        /// <summary>Vertical gradient (top colour first), for UI backgrounds.</summary>
        public static Texture2D Gradient(Color top, Color bottom, int h = 64)
        {
            var t = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Gradient" };
            var px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                // Texture row 0 is drawn at the bottom.
                var c = Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = px[y * 2 + 1] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D blob, board;

        /// <summary>Soft round shadow (white, alpha falls off to the edge); tint it dark.</summary>
        public static Texture2D Blob
        {
            get
            {
                if (blob != null) return blob;
                const int n = 64;
                blob = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Blob" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1f, d));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
                blob.SetPixels(px);
                blob.Apply();
                return blob;
            }
        }

        /// <summary>Old green blackboard: chalk dust speckle and a faint rubbed-out smudge.</summary>
        public static Texture2D Chalkboard
        {
            get
            {
                if (board != null) return board;
                const int w = 256, h = 160;
                board = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Chalkboard" };
                var baseC = Pal.H("#26392F");
                var rng = new System.Random(5);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = 1f - y / (float)h;
                    float smudge = Mathf.Exp(-((u - 0.3f) * (u - 0.3f) / 0.09f + (v - 0.4f) * (v - 0.4f) / 0.12f)) * 0.07f;
                    float rub = Mathf.PerlinNoise(x * 0.012f, y * 0.09f) * 0.05f;
                    float speck = rng.NextDouble() < 0.06 ? 0.05f : 0f;
                    float k = smudge + rub + speck;
                    px[y * w + x] = Color.Lerp(baseC, Color.white, k);
                }
                board.SetPixels(px);
                board.Apply(true);
                return board;
            }
        }

        static readonly Dictionary<Vector2Int, Texture2D> woodCache = new Dictionary<Vector2Int, Texture2D>();

        /// <summary>
        /// The old wooden school desk top: grain along its length, a cup ring and things carved by
        /// past students (#, LAST BENCH, Raju, AJ). One texture covers the whole top (u across, v along).
        /// </summary>
        public static Texture2D DeskWood(int w, int h)
        {
            var key = new Vector2Int(w, h);
            if (woodCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "DeskWood" };
            var baseC = Pal.H("#B57A43");
            var dark = Pal.H("#50280A");
            var light = Pal.H("#FFDCAA");
            var rng = new System.Random(11);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                // Grain runs along v; warp it gently so the lines wander like real planks.
                float xw = x + 7f * Mathf.Sin(y * 0.011f + x * 0.006f) + 9f * Mathf.PerlinNoise(x * 0.015f, y * 0.0035f);
                float f1 = Mathf.Repeat(xw, 10f), f2 = Mathf.Repeat(xw * 0.97f + 4f, 21f);
                var c = baseC;
                if (f1 < 2f) c = Color.Lerp(c, dark, 0.12f);
                if (f2 < 1f) c = Color.Lerp(c, light, 0.10f);
                float tone = (Mathf.PerlinNoise(x * 0.008f + 3f, y * 0.0025f) - 0.5f) * 0.12f;
                c = Color.Lerp(c, tone > 0 ? light : dark, Mathf.Abs(tone));
                float hl = Mathf.Exp(-((u - 0.45f) * (u - 0.45f) / 0.12f + (v - 0.72f) * (v - 0.72f) / 0.08f)) * 0.30f;
                float sh = Mathf.Exp(-((u - 0.7f) * (u - 0.7f) / 0.1f + (v - 0.12f) * (v - 0.12f) / 0.06f)) * 0.22f;
                c = Color.Lerp(Color.Lerp(c, light, hl), dark, sh);
                float speck = (float)(rng.NextDouble() - 0.5) * 0.03f;
                px[y * w + x] = new Color(c.r + speck, c.g + speck, c.b + speck, 1f);
            }

            // Cup ring, slightly squashed.
            float s = w / 342f; // mockup px -> texture px
            Ring(px, w, h, new Vector2(0.38f * w, 0.84f * h), 19f * s, 12f * s, 1.6f * s, 0.25f);
            // Carvings, like the mockup: positions as (u, v) with v = 1 the far end.
            Carve(px, w, h, "#", new Vector2(0.18f, 0.80f), 15f * s, -4f);
            Carve(px, w, h, "LAST BENCH", new Vector2(0.86f, 0.62f), 10f * s, -84f);
            Carve(px, w, h, "RAJU", new Vector2(0.10f, 0.45f), 11f * s, 80f);
            Carve(px, w, h, "AJ", new Vector2(0.80f, 0.18f), 22f * s, 24f);

            // The cube's top face shows the texture turned half way round, so turn it back
            // (far end of the desk = top of the picture, carvings read the right way up).
            System.Array.Reverse(px);
            tex.SetPixels(px);
            tex.Apply(true);
            woodCache[key] = tex;
            return tex;
        }

        static void Ring(Color[] px, int w, int h, Vector2 c, float rx, float ry, float thick, float alpha)
        {
            var groove = Pal.H("#46200A");
            int x0 = Mathf.FloorToInt(c.x - rx - thick - 1), x1 = Mathf.CeilToInt(c.x + rx + thick + 1);
            int y0 = Mathf.FloorToInt(c.y - ry - thick - 1), y1 = Mathf.CeilToInt(c.y + ry + thick + 1);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
            {
                float dx = (x - c.x) / rx, dy = (y - c.y) / ry;
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                float a = Mathf.Clamp01(1f - d / thick) * alpha;
                if (a > 0f) px[y * w + x] = Color.Lerp(px[y * w + x], groove, a);
            }
        }

        // A tiny knife-carved stroke font (straight cuts), 4 x 6 grid, y up.
        static readonly Dictionary<char, float[][]> Glyphs = new Dictionary<char, float[][]>
        {
            ['A'] = new[] { new float[] { 0, 0, 2, 6, 4, 0 }, new float[] { 1, 3, 3, 3 } },
            ['B'] = new[] { new float[] { 0, 0, 0, 6, 3, 6, 4, 5, 3, 3, 0, 3 }, new float[] { 3, 3, 4, 1.5f, 3, 0, 0, 0 } },
            ['C'] = new[] { new float[] { 4, 6, 1, 6, 0, 4.5f, 0, 1.5f, 1, 0, 4, 0 } },
            ['E'] = new[] { new float[] { 4, 6, 0, 6, 0, 0, 4, 0 }, new float[] { 0, 3, 3, 3 } },
            ['H'] = new[] { new float[] { 0, 0, 0, 6 }, new float[] { 4, 0, 4, 6 }, new float[] { 0, 3, 4, 3 } },
            ['J'] = new[] { new float[] { 1, 6, 4, 6 }, new float[] { 3, 6, 3, 1, 2, 0, 1, 0, 0, 1 } },
            ['L'] = new[] { new float[] { 0, 6, 0, 0, 4, 0 } },
            ['N'] = new[] { new float[] { 0, 0, 0, 6, 4, 0, 4, 6 } },
            ['R'] = new[] { new float[] { 0, 0, 0, 6, 3, 6, 4, 5, 4, 4, 3, 3, 0, 3 }, new float[] { 2, 3, 4, 0 } },
            ['S'] = new[] { new float[] { 4, 5.5f, 3, 6, 1, 6, 0, 5, 0, 4, 1, 3, 3, 3, 4, 2, 4, 1, 3, 0, 1, 0, 0, 0.5f } },
            ['T'] = new[] { new float[] { 0, 6, 4, 6 }, new float[] { 2, 6, 2, 0 } },
            ['U'] = new[] { new float[] { 0, 6, 0, 1, 1, 0, 3, 0, 4, 1, 4, 6 } },
            ['#'] = new[] { new float[] { 1.3f, 0, 1.8f, 6 }, new float[] { 2.8f, 0, 3.3f, 6 }, new float[] { 0, 2, 4.6f, 2.2f }, new float[] { 0, 4, 4.6f, 4.2f } },
        };

        /// <summary>Scratch text into the wood: a dark groove with a light lip beside it.</summary>
        static void Carve(Color[] px, int w, int h, string text, Vector2 uv, float height, float rotDeg)
        {
            float k = height / 6f;
            float advance = 5.6f, total = 0f;
            foreach (char ch in text) total += ch == ' ' ? 3f : advance;
            total -= 1.6f;
            var centre = new Vector2(uv.x * w, uv.y * h);
            float rad = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            System.Func<float, float, Vector2> map = (gx, gy) =>
            {
                float lx = (gx - total * 0.5f) * k, ly = (gy - 3f) * k;
                return centre + new Vector2(lx * cs - ly * sn, lx * sn + ly * cs);
            };
            var groove = Pal.H("#46200A");
            var lip = Pal.H("#FFE2B8");
            float thick = Mathf.Max(1.1f, k * 0.55f);
            float pen = 0f;
            foreach (char ch in text)
            {
                if (ch == ' ') { pen += 3f; continue; }
                if (Glyphs.TryGetValue(ch, out var strokes))
                    foreach (var st in strokes)
                        for (int i = 0; i + 3 < st.Length; i += 2)
                        {
                            var a = map(pen + st[i], st[i + 1]);
                            var b = map(pen + st[i + 2], st[i + 3]);
                            Segment(px, w, h, a + new Vector2(0.8f, -0.8f), b + new Vector2(0.8f, -0.8f), thick, lip, 0.18f);
                            Segment(px, w, h, a, b, thick, groove, 0.38f);
                        }
                pen += advance;
            }
        }

        static void Segment(Color[] px, int w, int h, Vector2 a, Vector2 b, float thick, Color c, float alpha)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - thick - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + thick + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - thick - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + thick + 1);
            var ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
            {
                var p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t);
                float m = Mathf.Clamp01(1f - (d - thick * 0.5f));
                if (m > 0f) px[y * w + x] = Color.Lerp(px[y * w + x], c, m * alpha);
            }
        }
    }
}
