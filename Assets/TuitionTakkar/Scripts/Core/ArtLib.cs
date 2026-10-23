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
                var baseC = Pal.H("#C9BFA8");
                var grout = Pal.H("#A39679");
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
    }
}
