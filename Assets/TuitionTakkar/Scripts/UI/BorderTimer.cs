using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// The turn timer drawn as a card's border (mockups TE / TP): a faint full outline plus a
    /// dark line that drains clockwise from the pin at the top centre as time runs out.
    /// Sits just outside its card (absolute, inset -Pad on every side).
    /// </summary>
    public class BorderTimer : VisualElement
    {
        public const float Pad = 5f;
        const float Width = 5f;

        float fraction = 1f;
        Color color = Pal.Ink;

        public BorderTimer()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = -Pad; style.top = -Pad; style.right = -Pad; style.bottom = -Pad;
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        /// <summary>Time left (0..1) and the colour of the remaining line.</summary>
        public void Set(float timeLeft01, Color c)
        {
            timeLeft01 = Mathf.Clamp01(timeLeft01);
            if (Mathf.Abs(timeLeft01 - fraction) < 0.001f && c == color) return;
            fraction = timeLeft01;
            color = c;
            MarkDirtyRepaint();
        }

        public bool Shown
        {
            get => style.display == DisplayStyle.Flex;
            set => style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 4f || r.height < 4f) return;
            float h = Width * 0.5f;
            float x0 = h, y0 = h, x1 = r.width - h, y1 = r.height - h, mx = r.width * 0.5f;
            // Clockwise from the top centre: right half of the top, right side, bottom, left side, left half of the top.
            var pts = new[]
            {
                new Vector2(mx, y0), new Vector2(x1, y0), new Vector2(x1, y1),
                new Vector2(x0, y1), new Vector2(x0, y0), new Vector2(mx, y0)
            };
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Vector2.Distance(pts[i - 1], pts[i]);

            var p = ctx.painter2D;
            p.lineWidth = Width;
            p.lineJoin = LineJoin.Miter;

            p.strokeColor = new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.14f);
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.Stroke();

            float left = total * fraction;
            if (left <= 0.5f) return;
            p.strokeColor = color;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length && left > 0f; i++)
            {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (left >= seg) { p.LineTo(pts[i]); left -= seg; }
                else { p.LineTo(Vector2.Lerp(pts[i - 1], pts[i], left / seg)); left = 0f; }
            }
            p.Stroke();
        }
    }
}
