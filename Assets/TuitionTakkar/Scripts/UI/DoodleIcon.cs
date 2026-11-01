using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Hand-drawn style icons drawn in code (look the same on every phone and on the web, unlike
    /// system emojis). Two sets: search stickers for the Quick Match spin, and reaction emojis
    /// for the in-match "Say…" menu.
    /// </summary>
    public class DoodleIcon : VisualElement
    {
        public enum Kind
        {
            // search stickers
            Pen, Star, Question, Smile, Bolt, Eraser,
            // reaction emojis
            Laugh, Wow, Cry, Angry, ThumbsUp, Fire,
        }

        public static readonly Kind[] Stickers = { Kind.Pen, Kind.Star, Kind.Question, Kind.Smile, Kind.Bolt, Kind.Eraser };
        public static readonly Color[] StickerBacks = { Pal.H("#BFE3F0"), Pal.H("#F8C8D4"), Pal.H("#FFE680"), Pal.H("#8FE0B5"), Pal.H("#D9CCF5"), Pal.H("#FFD2A8") };

        static readonly Color Ink = Pal.H("#1F2A44");
        static readonly Color FaceYellow = Pal.H("#FFD84D");
        static readonly Color Tear = Pal.H("#5BB8F0");

        Kind kind;
        readonly Label q;

        public DoodleIcon(Kind kind, float size)
        {
            this.kind = kind;
            style.width = size; style.height = size; style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            q = UIKit.Disp("?", size * 0.7f, Ink, TextAnchor.MiddleCenter);
            q.pickingMode = PickingMode.Ignore;
            UIKit.Fill(q);
            Add(q);
            generateVisualContent += Draw;
            SetKind(kind);
        }

        public void SetKind(Kind k)
        {
            kind = k;
            q.style.display = k == Kind.Question ? DisplayStyle.Flex : DisplayStyle.None;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float w = r.width, h = r.height;
            if (w < 2 || h < 2) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            float lw = Mathf.Max(2f, w * 0.06f);
            p.lineWidth = lw; p.strokeColor = Ink;
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            float R = w * 0.42f;

            switch (kind)
            {
                case Kind.Pen:
                {
                    // Diagonal pen: body, cap, tip.
                    Vector2 a = new Vector2(w * 0.2f, h * 0.8f), b = new Vector2(w * 0.8f, h * 0.2f);
                    Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * w * 0.09f;
                    Vector2 tipStart = a + d * w * 0.16f;
                    Poly(p, Pal.H("#3D7BD9"), tipStart + n, b + n, b - n, tipStart - n);
                    Poly(p, Pal.H("#1F2A44"), b - d * w * 0.18f + n, b + n, b - n, b - d * w * 0.18f - n);
                    Poly(p, Pal.H("#F2ECE2"), a, tipStart + n, tipStart - n);
                    break;
                }
                case Kind.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = (-90f + i * 36f) * Mathf.Deg2Rad;
                        float rr = i % 2 == 0 ? R : R * 0.45f;
                        pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                    }
                    Poly(p, Pal.H("#FFC93C"), pts);
                    break;
                }
                case Kind.Question:
                    break; // the "?" label
                case Kind.Smile:
                    Face(p, c, R, FaceYellow);
                    Dot(p, c + new Vector2(-R * 0.35f, -R * 0.2f), R * 0.1f);
                    Dot(p, c + new Vector2(R * 0.35f, -R * 0.2f), R * 0.1f);
                    ArcStroke(p, c + new Vector2(0, R * 0.05f), R * 0.5f, 20f, 160f);
                    break;
                case Kind.Bolt:
                    Poly(p, Pal.H("#FFC93C"),
                        new Vector2(w * 0.58f, h * 0.08f), new Vector2(w * 0.22f, h * 0.56f), new Vector2(w * 0.48f, h * 0.56f),
                        new Vector2(w * 0.40f, h * 0.92f), new Vector2(w * 0.78f, h * 0.42f), new Vector2(w * 0.52f, h * 0.42f));
                    break;
                case Kind.Eraser:
                {
                    Vector2 d = new Vector2(0.7f, -0.7f), n = new Vector2(0.7f, 0.7f);
                    Vector2 m = c;
                    float L = w * 0.36f, T = w * 0.2f;
                    Poly(p, Pal.H("#F49AB0"), m - d * L - n * T, m - n * T, m + n * T, m - d * L + n * T);
                    Poly(p, Pal.H("#5B8FD9"), m - n * T, m + d * L - n * T, m + d * L + n * T, m + n * T);
                    break;
                }

                case Kind.Laugh:
                    Face(p, c, R, FaceYellow);
                    ArcStroke(p, c + new Vector2(-R * 0.36f, -R * 0.12f), R * 0.16f, 200f, 340f);
                    ArcStroke(p, c + new Vector2(R * 0.36f, -R * 0.12f), R * 0.16f, 200f, 340f);
                    Mouth(p, c + new Vector2(0, R * 0.12f), R * 0.5f);
                    Drop(p, c + new Vector2(-R * 0.82f, -R * 0.05f), R * 0.16f);
                    Drop(p, c + new Vector2(R * 0.82f, -R * 0.05f), R * 0.16f);
                    break;
                case Kind.Wow:
                    Face(p, c, R, FaceYellow);
                    Dot(p, c + new Vector2(-R * 0.34f, -R * 0.22f), R * 0.13f);
                    Dot(p, c + new Vector2(R * 0.34f, -R * 0.22f), R * 0.13f);
                    p.fillColor = Pal.H("#7A2E2E");
                    p.BeginPath(); p.Arc(c + new Vector2(0, R * 0.35f), R * 0.2f, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill(); p.Stroke();
                    break;
                case Kind.Cry:
                    Face(p, c, R, FaceYellow);
                    Dot(p, c + new Vector2(-R * 0.34f, -R * 0.2f), R * 0.1f);
                    Dot(p, c + new Vector2(R * 0.34f, -R * 0.2f), R * 0.1f);
                    ArcStroke(p, c + new Vector2(0, R * 0.72f), R * 0.38f, 210f, 330f);
                    Drop(p, c + new Vector2(-R * 0.36f, R * 0.18f), R * 0.15f);
                    Drop(p, c + new Vector2(R * 0.36f, R * 0.18f), R * 0.15f);
                    break;
                case Kind.Angry:
                    Face(p, c, R, Pal.H("#F07A5A"));
                    Line(p, c + new Vector2(-R * 0.58f, -R * 0.42f), c + new Vector2(-R * 0.14f, -R * 0.24f));
                    Line(p, c + new Vector2(R * 0.58f, -R * 0.42f), c + new Vector2(R * 0.14f, -R * 0.24f));
                    Dot(p, c + new Vector2(-R * 0.32f, -R * 0.06f), R * 0.1f);
                    Dot(p, c + new Vector2(R * 0.32f, -R * 0.06f), R * 0.1f);
                    ArcStroke(p, c + new Vector2(0, R * 0.78f), R * 0.38f, 215f, 325f);
                    break;
                case Kind.ThumbsUp:
                {
                    // Fist with the thumb up.
                    p.fillColor = FaceYellow;
                    RoundRect(p, new Rect(w * 0.22f, h * 0.44f, w * 0.52f, h * 0.42f), w * 0.1f);
                    RoundRect(p, new Rect(w * 0.42f, h * 0.1f, w * 0.18f, h * 0.42f), w * 0.09f);
                    for (int i = 1; i < 3; i++)
                        Line(p, new Vector2(w * 0.26f, h * (0.44f + 0.14f * i)), new Vector2(w * 0.48f, h * (0.44f + 0.14f * i)));
                    p.fillColor = Pal.H("#3D7BD9");
                    RoundRect(p, new Rect(w * 0.74f, h * 0.48f, w * 0.12f, h * 0.36f), w * 0.03f);
                    break;
                }
                case Kind.Fire:
                    Flame(p, c + new Vector2(0, R * 0.1f), R * 0.95f, Pal.H("#F2552C"));
                    Flame(p, c + new Vector2(0, R * 0.35f), R * 0.55f, Pal.H("#FFC93C"));
                    break;
            }
        }

        // ---------- little drawing helpers ----------

        static void Poly(Painter2D p, Color fill, params Vector2[] pts)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        static void Face(Painter2D p, Vector2 c, float r, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill(); p.Stroke();
        }

        static void Dot(Painter2D p, Vector2 c, float r)
        {
            p.fillColor = Ink;
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill();
        }

        static void ArcStroke(Painter2D p, Vector2 c, float r, float from, float to)
        {
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(from), Angle.Degrees(to)); p.Stroke();
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        /// <summary>Big open laughing mouth: a half circle, flat on top.</summary>
        static void Mouth(Painter2D p, Vector2 c, float r)
        {
            p.fillColor = Pal.H("#7A2E2E");
            p.BeginPath(); p.MoveTo(c + new Vector2(-r, 0)); p.Arc(c, r, Angle.Degrees(180), Angle.Degrees(0), ArcDirection.CounterClockwise);
            p.ClosePath(); p.Fill(); p.Stroke();
        }

        static void Drop(Painter2D p, Vector2 c, float r)
        {
            p.fillColor = Tear;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0, -r * 1.6f));
            p.BezierCurveTo(c + new Vector2(r * 1.2f, -r * 0.2f), c + new Vector2(r, r), c + new Vector2(0, r));
            p.BezierCurveTo(c + new Vector2(-r, r), c + new Vector2(-r * 1.2f, -r * 0.2f), c + new Vector2(0, -r * 1.6f));
            p.ClosePath(); p.Fill();
        }

        static void Flame(Painter2D p, Vector2 c, float r, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0, -r));
            p.BezierCurveTo(c + new Vector2(r * 0.35f, -r * 0.45f), c + new Vector2(r * 0.95f, -r * 0.1f), c + new Vector2(r * 0.7f, r * 0.45f));
            p.BezierCurveTo(c + new Vector2(r * 0.5f, r * 0.85f), c + new Vector2(-r * 0.5f, r * 0.85f), c + new Vector2(-r * 0.7f, r * 0.45f));
            p.BezierCurveTo(c + new Vector2(-r * 0.9f, r * 0.0f), c + new Vector2(-r * 0.45f, -r * 0.3f), c + new Vector2(-r * 0.25f, -r * 0.55f));
            p.BezierCurveTo(c + new Vector2(-r * 0.1f, -r * 0.3f), c + new Vector2(0, -r * 0.6f), c + new Vector2(0, -r));
            p.ClosePath(); p.Fill(); p.Stroke();
        }

        static void RoundRect(Painter2D p, Rect r, float rad)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + rad, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), rad);
            p.ClosePath(); p.Fill(); p.Stroke();
        }
    }
}
