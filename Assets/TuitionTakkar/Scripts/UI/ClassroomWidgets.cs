using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// The classroom wall clock. Shows the phone's real time (hour, minute and a red
    /// second hand) with two small pause bars on the face, because tapping it pauses.
    /// </summary>
    public class ClockFace : VisualElement
    {
        readonly float size;

        public ClockFace(float size)
        {
            this.size = size;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            generateVisualContent += Draw;
            // Tick once a second so the hands stay live.
            schedule.Execute(MarkDirtyRepaint).Every(1000);
        }

        static Angle Deg(float d) => new Angle(d, AngleUnit.Degree);

        static Vector2 Polar(Vector2 c, float r, float degFrom12)
        {
            float a = (degFrom12 - 90f) * Mathf.Deg2Rad;
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        void Hand(Painter2D p, Vector2 c, float len, float tail, float deg, float width, Color col)
        {
            p.strokeColor = col;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(Polar(c, -tail, deg));
            p.LineTo(Polar(c, len, deg));
            p.Stroke();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(size * 0.5f, size * 0.5f);
            float r = size * 0.5f;
            float rim = size * 0.07f;

            // Rim + face
            p.fillColor = Pal.NightSoft;
            p.BeginPath(); p.Arc(c, r, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
            p.fillColor = Pal.H("#FBF8EF");
            p.BeginPath(); p.Arc(c, r - rim, Deg(0), Deg(360)); p.ClosePath(); p.Fill();

            // Hour ticks
            p.strokeColor = Pal.Ink;
            p.lineCap = LineCap.Round;
            for (int i = 0; i < 12; i++)
            {
                bool major = i % 3 == 0;
                p.lineWidth = major ? 2.2f : 1.2f;
                p.BeginPath();
                p.MoveTo(Polar(c, r - rim - (major ? size * 0.11f : size * 0.06f), i * 30f));
                p.LineTo(Polar(c, r - rim - size * 0.02f, i * 30f));
                p.Stroke();
            }

            // Pause bars (tap the clock to pause)
            float bw = size * 0.065f, bh = size * 0.17f, gap = size * 0.05f, by = c.y + size * 0.13f;
            p.fillColor = Pal.Red;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - gap - bw, by)); p.LineTo(new Vector2(c.x - gap, by));
            p.LineTo(new Vector2(c.x - gap, by + bh)); p.LineTo(new Vector2(c.x - gap - bw, by + bh));
            p.ClosePath(); p.Fill();
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + gap, by)); p.LineTo(new Vector2(c.x + gap + bw, by));
            p.LineTo(new Vector2(c.x + gap + bw, by + bh)); p.LineTo(new Vector2(c.x + gap, by + bh));
            p.ClosePath(); p.Fill();

            // Real time
            var now = DateTime.Now;
            float sec = now.Second;
            float min = now.Minute + sec / 60f;
            float hour = (now.Hour % 12) + min / 60f;
            Hand(p, c, r * 0.45f, r * 0.06f, hour * 30f, size * 0.055f, Pal.Ink);
            Hand(p, c, r * 0.68f, r * 0.08f, min * 6f, size * 0.04f, Pal.Ink);
            Hand(p, c, r * 0.72f, r * 0.14f, sec * 6f, Mathf.Max(1f, size * 0.016f), Pal.H("#D9472F"));
            p.fillColor = Pal.Ink;
            p.BeginPath(); p.Arc(c, size * 0.035f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
        }
    }

    /// <summary>Small stopwatch (turn timer) or robot (auto shot) icon for the turn tape.</summary>
    public class TimerIcon : VisualElement
    {
        bool robot;
        Color color = Pal.Ink;

        public TimerIcon()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 20;
            style.height = 22;
            generateVisualContent += Draw;
        }

        public void Set(bool showRobot, Color c)
        {
            if (robot == showRobot && color == c) return;
            robot = showRobot;
            color = c;
            MarkDirtyRepaint();
        }

        static Angle Deg(float d) => new Angle(d, AngleUnit.Degree);

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineCap = LineCap.Round;
            if (!robot)
            {
                p.lineWidth = 2.2f;
                p.BeginPath(); p.Arc(new Vector2(10, 13), 7.5f, Deg(0), Deg(360)); p.ClosePath(); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(10, 13)); p.LineTo(new Vector2(10, 8.5f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(7.5f, 1)); p.LineTo(new Vector2(12.5f, 1)); p.LineTo(new Vector2(12.5f, 4));
                p.LineTo(new Vector2(7.5f, 4)); p.ClosePath(); p.Fill();
                p.lineWidth = 2f;
                p.BeginPath(); p.MoveTo(new Vector2(16, 5.5f)); p.LineTo(new Vector2(17.5f, 4)); p.Stroke();
            }
            else
            {
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(4, 8)); p.LineTo(new Vector2(16, 8)); p.LineTo(new Vector2(16, 19)); p.LineTo(new Vector2(4, 19));
                p.ClosePath(); p.Stroke();
                p.BeginPath(); p.Arc(new Vector2(8, 13.5f), 1.6f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
                p.BeginPath(); p.Arc(new Vector2(12, 13.5f), 1.6f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
                p.BeginPath(); p.MoveTo(new Vector2(10, 8)); p.LineTo(new Vector2(10, 4)); p.Stroke();
                p.BeginPath(); p.Arc(new Vector2(10, 3), 1.4f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
            }
        }
    }

    /// <summary>Millimetre and centimetre marks for the ruler power bar.</summary>
    public class RulerTicks : VisualElement
    {
        public RulerTicks()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width;
            if (w <= 1f) return;
            const int steps = 60; // 15 cm x 4 marks per cm
            p.strokeColor = Pal.H("#5B3818");
            for (int i = 0; i <= steps; i++)
            {
                float x = Mathf.Lerp(1f, w - 1f, i / (float)steps);
                bool cm = i % 4 == 0;
                bool half = i % 2 == 0;
                p.lineWidth = cm ? 1.5f : 1f;
                p.BeginPath();
                p.MoveTo(new Vector2(x, 0f));
                p.LineTo(new Vector2(x, cm ? 13f : half ? 9f : 6f));
                p.Stroke();
            }
        }
    }

    /// <summary>Small navy arrow above the power ruler pointing at the current power.</summary>
    public class PowerArrow : VisualElement
    {
        public PowerArrow()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 16;
            style.height = 10;
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.fillColor = Pal.Ink;
                p.BeginPath();
                p.MoveTo(new Vector2(0f, 0f));
                p.LineTo(new Vector2(16f, 0f));
                p.LineTo(new Vector2(8f, 10f));
                p.ClosePath();
                p.Fill();
            };
        }
    }
}
