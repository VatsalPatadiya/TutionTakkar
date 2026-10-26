using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// The ring that follows your finger while aiming, drawn in pencil (K1): a paper disc with a
    /// wobbly graphite outline, a red-pen arc filled to the current power, and the % in the middle.
    /// </summary>
    public class PowerRing : VisualElement
    {
        public const float Size = 72f;
        float value;
        readonly Label label;

        public PowerRing()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = Size;
            style.height = Size;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            generateVisualContent += Draw;

            label = UIKit.Text("0%", UIKit.Marker, 16f, Sketch.Graphite, TextAnchor.MiddleCenter, false);
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }

        public float Value
        {
            get => value;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, this.value)) return;
                this.value = v;
                label.text = Mathf.RoundToInt(v * 100f) + "%";
                MarkDirtyRepaint();
            }
        }

        /// <summary>Centre the ring on a point in panel coordinates.</summary>
        public void MoveTo(Vector2 panelPos)
        {
            style.left = panelPos.x - Size * 0.5f;
            style.top = panelPos.y - Size * 0.5f;
        }

        static Angle Deg(float d) => new Angle(d, AngleUnit.Degree);

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(Size * 0.5f, Size * 0.5f);
            const float r = 29f;

            // Soft pencil shadow, then the paper disc.
            p.fillColor = new Color(0f, 0f, 0f, 0.16f);
            p.BeginPath(); p.Arc(c + new Vector2(2.5f, 3.5f), r, Deg(0f), Deg(360f)); p.ClosePath(); p.Fill();
            p.fillColor = Sketch.PaperC;
            p.BeginPath(); p.Arc(c, r, Deg(0f), Deg(360f)); p.ClosePath(); p.Fill();

            // Wobbly graphite outline: two slightly offset loose circles.
            p.lineJoin = LineJoin.Round;
            p.strokeColor = Sketch.Graphite; p.lineWidth = 2.6f;
            Wobbly(p, c, r, 0, 0.9f);
            p.strokeColor = Sketch.A(Sketch.Graphite, 0.4f); p.lineWidth = 1f;
            Wobbly(p, c + new Vector2(1f, 0.6f), r - 1.5f, 40, 1.1f);

            if (value > 0.005f)
            {
                p.strokeColor = Sketch.RedPen;
                p.lineWidth = 6f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(c, r, Deg(-90f), Deg(-90f + 360f * Mathf.Min(value, 0.999f)));
                p.Stroke();
            }
        }

        static void Wobbly(Painter2D p, Vector2 c, float r, int seed, float wob)
        {
            const int n = 28;
            p.BeginPath();
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float rr = r + Sketch.Wob(seed + (i % n), wob);
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.Stroke();
        }
    }
}
