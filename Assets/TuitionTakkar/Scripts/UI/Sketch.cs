using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Pencil-sketch / classroom drawing pieces for the game screen (mockups K1 / K2):
    /// chalk colours, the torn paper chit, a sketched paper button, the chalk circle timer,
    /// chalk arrows and doodles. All drawn with Painter2D, no imported art.
    /// </summary>
    public static class Sketch
    {
        public static readonly Color Graphite = Pal.H("#2B2B2B");
        public static readonly Color PaperC = Pal.H("#FBF7EC");
        public static readonly Color RedPen = Pal.H("#D63B2F");
        public static readonly Color Ballpoint = Pal.H("#2B4C9B");
        public static readonly Color ChalkWhite = Pal.H("#F4F1E8");
        public static readonly Color ChalkYellow = Pal.H("#FFE07A");
        public static readonly Color ChalkPink = Pal.H("#FFB3C7");
        public static readonly Color ChalkBlue = Pal.H("#9FD3F5");
        public static readonly Color ChalkGreen = Pal.H("#B5E3A1");
        public static readonly Color ChalkRed = Pal.H("#FF8F7E");

        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Label Chalk(string s, float size, Color c, TextAnchor a = TextAnchor.MiddleLeft)
        {
            var l = UIKit.Text(s, UIKit.Chalk, size, c, a, false);
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Small deterministic wobble so lines look hand drawn but don't shimmer.</summary>
        public static float Wob(int i, float amount) => (Mathf.PerlinNoise(i * 0.73f, 0.31f) - 0.5f) * 2f * amount;

        public static Angle Deg(float d) => new Angle(d, AngleUnit.Degree);
    }

    /// <summary>A torn paper slip ("your turn"), like a note passed in class. Sizes to its label.</summary>
    public class PaperChit : VisualElement
    {
        public readonly Label label;

        // Top and bottom torn edges, as (x fraction, y fraction) like the mockup's clip-path.
        static readonly Vector2[] Top =
        {
            new Vector2(0f, .06f), new Vector2(.06f, 0f), new Vector2(.13f, .07f), new Vector2(.21f, .01f), new Vector2(.30f, .06f),
            new Vector2(.40f, 0f), new Vector2(.50f, .06f), new Vector2(.61f, .01f), new Vector2(.71f, .07f), new Vector2(.82f, 0f),
            new Vector2(.92f, .06f), new Vector2(1f, .02f)
        };
        static readonly Vector2[] Bottom =
        {
            new Vector2(1f, .94f), new Vector2(.93f, 1f), new Vector2(.84f, .93f), new Vector2(.74f, .99f), new Vector2(.63f, .93f),
            new Vector2(.52f, 1f), new Vector2(.41f, .94f), new Vector2(.30f, .99f), new Vector2(.20f, .93f), new Vector2(.09f, 1f),
            new Vector2(0f, .95f)
        };

        public PaperChit(string text, float size = 20f)
        {
            pickingMode = PickingMode.Ignore;
            UIKit.Pad(style, 9, 18, 12, 18);
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            generateVisualContent += Draw;
            label = UIKit.Text(text, UIKit.HandBold, size, Sketch.Ballpoint, TextAnchor.MiddleCenter, false);
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }

        void Path(Painter2D p, Rect r, Vector2 off)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + Top[0].x * r.width, r.y + Top[0].y * r.height) + off);
            foreach (var v in Top) p.LineTo(new Vector2(r.x + v.x * r.width, r.y + v.y * r.height) + off);
            foreach (var v in Bottom) p.LineTo(new Vector2(r.x + v.x * r.width, r.y + v.y * r.height) + off);
            p.ClosePath();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = new Rect(0, 0, layout.width, layout.height);
            if (r.width < 2f) return;
            p.fillColor = new Color(0f, 0f, 0f, 0.13f);
            Path(p, r, new Vector2(0f, 5f)); p.Fill();
            p.fillColor = new Color(0f, 0f, 0f, 0.1f);
            Path(p, r, new Vector2(1.5f, 3f)); p.Fill();
            p.fillColor = Sketch.PaperC;
            Path(p, r, Vector2.zero); p.Fill();
            // One faint blue rule near the bottom, like notebook paper.
            p.strokeColor = new Color(80f / 255f, 120f / 255f, 190f / 255f, 0.35f);
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(2f, r.height - 14f));
            p.LineTo(new Vector2(r.width - 2f, r.height - 14f));
            p.Stroke();
        }
    }

    /// <summary>Sketched paper square button: hatched pencil shadow, wobbly double outline.</summary>
    public class SketchSquare : VisualElement
    {
        readonly float size;
        public Color fill = Sketch.PaperC;

        public SketchSquare(float size)
        {
            this.size = size;
            style.width = size; style.height = size;
            style.alignItems = Align.Center; style.justifyContent = Justify.Center;
            generateVisualContent += Draw;
        }

        static Vector2[] Quad(float x0, float y0, float x1, float y1, int seed, float wob)
        {
            return new[]
            {
                new Vector2(x0 + Sketch.Wob(seed, wob), y0 + Sketch.Wob(seed + 1, wob)),
                new Vector2(x1 + Sketch.Wob(seed + 2, wob), y0 + Sketch.Wob(seed + 3, wob)),
                new Vector2(x1 + Sketch.Wob(seed + 4, wob), y1 + Sketch.Wob(seed + 5, wob)),
                new Vector2(x0 + Sketch.Wob(seed + 6, wob), y1 + Sketch.Wob(seed + 7, wob)),
            };
        }

        static void Poly(Painter2D p, Vector2[] q)
        {
            p.BeginPath();
            p.MoveTo(q[0]);
            for (int i = 1; i < q.Length; i++) p.LineTo(q[i]);
            p.ClosePath();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float s = size, o = 5f;
            // Hatched shadow: diagonal pencil strokes in a square offset down-right.
            p.strokeColor = Sketch.A(Sketch.Graphite, 0.55f);
            p.lineWidth = 1.3f;
            for (float k = -s; k < s; k += 4.5f)
            {
                // Line x - y = k, clipped to [o, s+o] x [o, s+o].
                float xa = Mathf.Max(o, o + k), xb = Mathf.Min(s + o, s + o + k);
                if (xb <= xa) continue;
                p.BeginPath();
                p.MoveTo(new Vector2(xa, xa - k));
                p.LineTo(new Vector2(xb, xb - k));
                p.Stroke();
            }
            p.fillColor = fill;
            Poly(p, Quad(1f, 1f, s - 1f, s - 1f, 3, 0.8f)); p.Fill();
            p.strokeColor = Sketch.Graphite; p.lineWidth = 2.6f; p.lineJoin = LineJoin.Round;
            Poly(p, Quad(1f, 1f, s - 1f, s - 1f, 11, 1.1f)); p.Stroke();
            p.strokeColor = Sketch.A(Sketch.Graphite, 0.45f); p.lineWidth = 1f;
            Poly(p, Quad(3f, 2.5f, s - 2.5f, s - 0.5f, 23, 1.2f)); p.Stroke();
        }
    }

    /// <summary>Two pencil strokes: the pause sign.</summary>
    public class PencilPause : VisualElement
    {
        public PencilPause()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 16; style.height = 16;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.strokeColor = Sketch.Graphite; p.lineWidth = 3.2f; p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(new Vector2(4.6f, 2f)); p.LineTo(new Vector2(4.8f, 14f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(11.2f, 1.9f)); p.LineTo(new Vector2(11.3f, 14.1f)); p.Stroke();
            };
        }
    }

    /// <summary>Chalk circle timer on the blackboard: faint ring, arc draining clockwise, seconds inside.</summary>
    public class ChalkTimer : VisualElement
    {
        readonly float Size;
        float value = 1f;
        Color color = Sketch.ChalkYellow;
        public readonly Label secs;

        public ChalkTimer(float size = 30f)
        {
            Size = size;
            pickingMode = PickingMode.Ignore;
            style.width = Size; style.height = Size; style.flexShrink = 0;
            style.alignItems = Align.Center; style.justifyContent = Justify.Center;
            generateVisualContent += Draw;
            secs = Sketch.Chalk("", size * 0.42f, Sketch.ChalkYellow, TextAnchor.MiddleCenter);
            Add(secs);
        }

        public void Set(float left01, int seconds, Color c)
        {
            secs.text = seconds.ToString();
            secs.style.color = c;
            if (Mathf.Abs(left01 - value) < 0.002f && c == color) return;
            value = Mathf.Clamp01(left01);
            color = c;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(Size * 0.5f, Size * 0.5f);
            float r = Size * 0.4f;
            p.lineWidth = Size * 0.1f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.25f);
            p.BeginPath(); p.Arc(c, r, Sketch.Deg(0f), Sketch.Deg(360f)); p.Stroke();
            if (value <= 0.005f) return;
            p.strokeColor = color;
            p.lineCap = LineCap.Round;
            p.BeginPath(); p.Arc(c, r, Sketch.Deg(-90f), Sketch.Deg(-90f + 360f * Mathf.Min(value, 0.999f))); p.Stroke();
        }
    }

    /// <summary>A chalk (or pen) arrow pointing right.</summary>
    public class SketchArrow : VisualElement
    {
        public Color color;

        public SketchArrow(Color c, float w = 18f, float h = 14f, float width = 2.6f)
        {
            color = c;
            pickingMode = PickingMode.Ignore;
            style.width = w; style.height = h; style.flexShrink = 0;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.strokeColor = color; p.lineWidth = width; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                float y = h * 0.52f;
                p.BeginPath(); p.MoveTo(new Vector2(1.5f, y + 0.6f)); p.LineTo(new Vector2(w - 2f, y)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(w - 7f, y - 5f)); p.LineTo(new Vector2(w - 2f, y)); p.LineTo(new Vector2(w - 7.5f, y + 5f)); p.Stroke();
            };
        }
    }

    /// <summary>Chalk round box: empty, or filled yellow for a won round.</summary>
    public class ChalkBox : VisualElement
    {
        bool won;

        public ChalkBox()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 15; style.height = 15; style.flexShrink = 0;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                if (won)
                {
                    p.fillColor = Sketch.A(Sketch.ChalkYellow, 0.85f);
                    p.BeginPath(); p.MoveTo(new Vector2(1, 1)); p.LineTo(new Vector2(14, 1)); p.LineTo(new Vector2(14, 14)); p.LineTo(new Vector2(1, 14)); p.ClosePath(); p.Fill();
                }
                p.strokeColor = new Color(1f, 1f, 1f, won ? 0.8f : 0.55f); p.lineWidth = 2f;
                p.BeginPath(); p.MoveTo(new Vector2(1, 1.3f)); p.LineTo(new Vector2(14.2f, 1)); p.LineTo(new Vector2(14, 14.2f)); p.LineTo(new Vector2(0.8f, 14)); p.ClosePath(); p.Stroke();
            };
        }

        public bool Won
        {
            get => won;
            set { if (won == value) return; won = value; MarkDirtyRepaint(); }
        }
    }

    /// <summary>Faint chalk geometry doodle (triangle with its height dashed), like last class's maths.</summary>
    public class ChalkTriangle : VisualElement
    {
        public ChalkTriangle()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 90; style.height = 60;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.strokeColor = new Color(1f, 1f, 1f, 0.42f); p.lineWidth = 2f; p.lineJoin = LineJoin.Round;
                p.BeginPath(); p.MoveTo(new Vector2(5, 50)); p.LineTo(new Vector2(45, 8)); p.LineTo(new Vector2(85, 50)); p.ClosePath(); p.Stroke();
                p.lineWidth = 1.5f;
                for (float y = 8; y < 50; y += 6)
                {
                    p.BeginPath(); p.MoveTo(new Vector2(45, y)); p.LineTo(new Vector2(45, Mathf.Min(50, y + 3))); p.Stroke();
                }
            };
        }
    }
}
