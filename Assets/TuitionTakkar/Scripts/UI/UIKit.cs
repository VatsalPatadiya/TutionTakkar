using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>Tiny helpers to build UI Toolkit screens in code, styled like the design.</summary>
    public static class UIKit
    {
        public static Font Display, Hand, HandBold, Body, BodyBold;

        public static void LoadFonts()
        {
            Display = Resources.Load<Font>("TT/Fonts/LilitaOne-Regular");
            Hand = Resources.Load<Font>("TT/Fonts/Kalam-Regular");
            HandBold = Resources.Load<Font>("TT/Fonts/Kalam-Bold");
            Body = Resources.Load<Font>("TT/Fonts/Nunito-Bold");
            BodyBold = Resources.Load<Font>("TT/Fonts/Nunito-ExtraBold");
        }

        // ---------- generic ----------

        public static T St<T>(this T e, Action<IStyle> f) where T : VisualElement
        {
            f(e.style);
            return e;
        }

        public static T Add<T>(this T parent, params VisualElement[] kids) where T : VisualElement
        {
            foreach (var k in kids) if (k != null) parent.Add(k);
            return parent;
        }

        public static VisualElement El(string name = null) => new VisualElement { name = name };

        public static VisualElement Row(Align align = Align.Center)
        {
            var e = El();
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = align;
            return e;
        }

        public static VisualElement Col()
        {
            var e = El();
            e.style.flexDirection = FlexDirection.Column;
            return e;
        }

        /// <summary>UI Toolkit has no CSS gap, so space the children with margins.</summary>
        public static T Gap<T>(this T parent, float gap) where T : VisualElement
        {
            bool row = parent.style.flexDirection.value == FlexDirection.Row;
            int i = 0;
            foreach (var c in parent.Children())
            {
                if (i++ == 0) continue;
                if (row) c.style.marginLeft = gap; else c.style.marginTop = gap;
            }
            return parent;
        }

        public static Label Text(string s, Font f, float size, Color c, TextAnchor align = TextAnchor.UpperLeft, bool wrap = true)
        {
            var l = new Label(s);
            l.style.unityFontDefinition = FontDefinition.FromFont(f);
            l.style.fontSize = size;
            l.style.color = c;
            l.style.unityTextAlign = align;
            l.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        public static Label Disp(string s, float size, Color c, TextAnchor a = TextAnchor.UpperLeft) => Text(s, Display, size, c, a);
        public static Label HandText(string s, float size, Color c, TextAnchor a = TextAnchor.UpperLeft) => Text(s, Hand, size, c, a);
        public static Label Strong(string s, float size, Color c, TextAnchor a = TextAnchor.UpperLeft) => Text(s, BodyBold, size, c, a);
        public static Label Plain(string s, float size, Color c, TextAnchor a = TextAnchor.UpperLeft) => Text(s, Body, size, c, a);

        public static void Radius(IStyle s, float r)
        {
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
        }

        public static void Pad(IStyle s, float t, float r, float b, float l)
        {
            s.paddingTop = t;
            s.paddingRight = r;
            s.paddingBottom = b;
            s.paddingLeft = l;
        }

        public static void Pad(IStyle s, float all) => Pad(s, all, all, all, all);

        public static void Border(IStyle s, float w, Color c)
        {
            s.borderTopWidth = s.borderRightWidth = s.borderBottomWidth = s.borderLeftWidth = w;
            s.borderTopColor = s.borderRightColor = s.borderBottomColor = s.borderLeftColor = c;
        }

        public static void Rot(VisualElement e, float deg) => e.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));

        /// <summary>
        /// Lets touches go straight through this element and everything inside it, except
        /// real buttons. Text labels catch touches by default in UI Toolkit, which made the
        /// timer tape, the power ruler and the debug bar "dead" spots when grabbing a pen.
        /// </summary>
        public static void PassThrough(VisualElement root)
        {
            if (root == null) return;
            if (!(root is Button)) root.pickingMode = PickingMode.Ignore;
            root.Query<VisualElement>().ForEach(e =>
            {
                if (e is Button) return;
                // Children of a button stay as they are so the button still gets its taps.
                for (var p = e.parent; p != null; p = p.parent)
                    if (p is Button) return;
                e.pickingMode = PickingMode.Ignore;
            });
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.top = 0;
            e.style.right = 0;
            e.style.bottom = 0;
        }

        public static void Abs(VisualElement e, float? left = null, float? top = null, float? right = null, float? bottom = null)
        {
            e.style.position = Position.Absolute;
            if (left.HasValue) e.style.left = left.Value;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
        }

        public static VisualElement Spacer(float h)
        {
            var e = El();
            e.style.height = h;
            e.style.flexShrink = 0;
            return e;
        }

        public static VisualElement Grow()
        {
            var e = El();
            e.style.flexGrow = 1;
            return e;
        }

        // ---------- textures ----------

        public static void Laminate(VisualElement e)
        {
            e.style.backgroundColor = Pal.Laminate;
            e.style.backgroundImage = new StyleBackground(ArtLib.Laminate);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            e.style.backgroundSize = new BackgroundSize(new Length(256f), new Length(256f));
        }

        public static void GraphPaper(VisualElement e)
        {
            e.style.backgroundColor = Pal.Paper;
            e.style.backgroundImage = new StyleBackground(ArtLib.Graph);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            e.style.backgroundSize = new BackgroundSize(new Length(18f), new Length(18f));
        }

        // ---------- widgets ----------

        public static Button Btn(string text, Action onClick, Color bg, Color fg, Font font, float size, float height,
            float radius = 14f, Color? shadow = null, Color? border = null)
        {
            var b = new Button(() => { GameManager.I?.Audio.Ui(); onClick?.Invoke(); }) { text = text };
            var s = b.style;
            s.unityFontDefinition = FontDefinition.FromFont(font);
            s.fontSize = size;
            s.color = fg;
            s.backgroundColor = bg;
            s.height = height;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            Pad(s, 0, 12, 0, 12);
            Radius(s, radius);
            Border(s, 0f, Color.clear);
            if (border.HasValue) Border(s, 2f, border.Value);
            if (shadow.HasValue)
            {
                s.borderBottomWidth = 5f;
                s.borderBottomColor = shadow.Value;
            }
            return b;
        }

        public static Button PrimaryBtn(string text, Action onClick, float height = 58f) =>
            Btn(text, onClick, Pal.Red, Color.white, Display, 22f, height, 16f, Pal.RedDark);

        public static Button IconBtn(Action onClick, VisualElement icon, Color bg, string label)
        {
            var b = Btn("", onClick, bg, Color.white, Body, 12f, 44f, 12f);
            b.style.width = 44f;
            Pad(b.style, 0);
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.tooltip = label;
            icon.pickingMode = PickingMode.Ignore;
            b.Add(icon);
            return b;
        }

        /// <summary>Simple vector-free icons drawn from boxes.</summary>
        public static VisualElement PauseIcon()
        {
            var r = Row();
            r.Add(El().St(s => { s.width = 4; s.height = 14; s.backgroundColor = Color.white; Radius(s, 1); }));
            r.Add(El().St(s => { s.width = 4; s.height = 14; s.backgroundColor = Color.white; Radius(s, 1); s.marginLeft = 4; }));
            return r;
        }

        public static VisualElement BackIcon()
        {
            var e = El().St(s =>
            {
                s.width = 12; s.height = 12;
                s.borderLeftWidth = 3; s.borderBottomWidth = 3;
                s.borderLeftColor = Color.white; s.borderBottomColor = Color.white;
                s.marginLeft = 5;
            });
            Rot(e, 45f);
            return e;
        }

        public static VisualElement Chevron(Color c)
        {
            var e = El().St(s =>
            {
                s.width = 11; s.height = 11;
                s.borderRightWidth = 3; s.borderTopWidth = 3;
                s.borderRightColor = c; s.borderTopColor = c;
                s.marginRight = 4;
            });
            Rot(e, 45f);
            return e;
        }

        public static VisualElement Dot(float size, Color fill, bool filled, Color ring)
        {
            return El().St(s =>
            {
                s.width = size;
                s.height = size;
                Radius(s, size * 0.5f);
                if (filled) s.backgroundColor = fill;
                else Border(s, 2f, ring);
                s.flexShrink = 0;
            });
        }

        public static VisualElement Avatar(string initial, Color bg, float size = 64f)
        {
            var a = El().St(s =>
            {
                s.width = size; s.height = size;
                Radius(s, size * 0.5f);
                s.backgroundColor = bg;
                Border(s, 3f, Pal.Ink);
                s.alignItems = Align.Center;
                s.justifyContent = Justify.Center;
            });
            a.Add(Disp(initial, size * 0.42f, Pal.Ink, TextAnchor.MiddleCenter));
            return a;
        }

        public static VisualElement Tape(string text, float width, float rotate)
        {
            var t = El().St(s =>
            {
                s.width = width;
                s.height = 34f;
                s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.95f);
                s.alignItems = Align.Center;
                s.justifyContent = Justify.Center;
            });
            Rot(t, rotate);
            if (!string.IsNullOrEmpty(text)) t.Add(Text(text, HandBold, 18f, Pal.Ink, TextAnchor.MiddleCenter, false));
            return t;
        }

        public static VisualElement StatBar(string label, float value, Color fg, Color track)
        {
            var row = Row();
            row.Add(Strong(label, 14f, Pal.Ink).St(s => s.width = 70f));
            var bar = El().St(s => { s.flexGrow = 1; s.height = 10f; s.backgroundColor = track; Radius(s, 5f); s.overflow = Overflow.Hidden; });
            bar.Add(El().St(s => { s.width = Length.Percent(Mathf.Clamp01(value) * 100f); s.height = 10f; s.backgroundColor = fg; }));
            row.Add(bar);
            return row;
        }

        public static VisualElement DashedLine(Color c)
        {
            // Dashes as a row of short bars.
            var r = Row().St(s => { s.height = 2f; s.overflow = Overflow.Hidden; });
            for (int i = 0; i < 40; i++)
                r.Add(El().St(s => { s.width = 6f; s.height = 2f; s.backgroundColor = c; s.marginRight = 4f; s.flexShrink = 0; }));
            return r;
        }

        public static VisualElement KeyValue(string k, string v, Color vColor)
        {
            var r = Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.paddingBottom = 8f; });
            r.Add(Plain(k, 15f, Pal.Ink));
            r.Add(Strong(v, 15f, vColor, TextAnchor.UpperRight).St(s => { s.flexShrink = 1; s.marginLeft = 10f; }));
            return r;
        }
    }
}
