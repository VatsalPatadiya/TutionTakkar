using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// The ring that follows your finger while aiming: translucent disc, grey track,
    /// orange arc filled to the current power, and the % in the middle.
    /// </summary>
    public class PowerRing : VisualElement
    {
        public const float Size = 76f;
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

            label = UIKit.Disp("0%", 19f, Pal.Ink, TextAnchor.MiddleCenter);
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
            const float disc = 29f, ring = 34f;

            p.fillColor = new Color(1f, 1f, 1f, 0.6f);
            p.BeginPath();
            p.Arc(c, disc, Deg(0f), Deg(360f));
            p.ClosePath();
            p.Fill();

            p.lineWidth = 2f;
            p.strokeColor = Pal.Ink;
            p.BeginPath();
            p.Arc(c, disc, Deg(0f), Deg(360f));
            p.ClosePath();
            p.Stroke();

            p.lineWidth = 7f;
            p.strokeColor = new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.18f);
            p.BeginPath();
            p.Arc(c, ring, Deg(0f), Deg(360f));
            p.Stroke();

            if (value > 0.005f)
            {
                p.strokeColor = Pal.Orange;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(c, ring, Deg(-90f), Deg(-90f + 360f * Mathf.Min(value, 0.999f)));
                p.Stroke();
            }
        }
    }
}
