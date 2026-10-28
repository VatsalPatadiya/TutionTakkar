using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Opening splash (design SP3 · Chalkboard), about 2.8 s:
    /// chalk writes TUITION then TAKKAR, your pen drops onto the desk with a bounce,
    /// and a red FLICK! stamp lands on the board. A tap skips it.
    /// </summary>
    public static class SplashScreen
    {
        const float Total = 2.9f;
        const float Write1From = 0.16f, Write1To = 1.44f;
        const float Write2From = 0.80f, Write2To = 1.84f;
        const float DropFrom = 1.84f, DropTo = 2.24f, BounceTo = 2.48f;
        const float StampFrom = 2.32f, StampTo = 2.56f;
        const float TextWidth = 300f;

        public static void Show(GameManager gm, Action done)
        {
            var root = UIKit.El().St(s => s.backgroundColor = Pal.H("#E8E0CB"));
            root.name = BackNav.SplashName;
            root.pickingMode = PickingMode.Position;

            // Chalkboard in its wooden frame, with the chalk ledge under it.
            var board = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 18; s.right = 18; s.top = 150; s.height = 360;
                s.backgroundColor = Pal.H("#2F4A3E");
                UIKit.Border(s, 12f, Pal.H("#6B4A2B")); UIKit.Radius(s, 6);
            });
            board.Add(UIKit.El().St(s => { s.position = Position.Absolute; s.left = -12; s.right = -12; s.bottom = -26; s.height = 14; s.backgroundColor = Pal.H("#8A6440"); }));

            var line1 = ChalkLine("TUITION", Pal.H("#F4F1E8"), 60f, out var clip1);
            var line2 = ChalkLine("TAKKAR", Pal.Yellow, 134f, out var clip2);
            board.Add(line1, line2);

            var chalk = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.width = 26; s.height = 9; UIKit.Radius(s, 3); s.backgroundColor = Color.white;
            });
            board.Add(chalk);

            var stamp = UIKit.Disp("FLICK!", 30f, Pal.H("#E8563A"), TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.right = 26; s.top = 200;
                UIKit.Border(s, 5f, Pal.H("#E8563A")); UIKit.Radius(s, 8); UIKit.Pad(s, 4, 12, 4, 12);
                s.letterSpacing = 2f; s.opacity = 0f;
            });
            board.Add(stamp);
            root.Add(board);

            // Your own pen drops onto the desk.
            var pen = PenIcon.Build(Catalog.Pen(gm.Save.equipped), 210f);
            pen.style.position = Position.Absolute;
            pen.style.left = 90;
            pen.style.top = 574;
            root.Add(pen);

            root.Add(UIKit.HandText("flick their pen off the bench. keep it.", 21f, Pal.BenchText, TextAnchor.MiddleCenter).St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = 650;
            }));

            var dots = UIKit.Row().St(s => { s.position = Position.Absolute; s.left = 0; s.right = 0; s.bottom = 48; s.justifyContent = Justify.Center; });
            var dot = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                dot[i] = UIKit.El().St(s => { s.width = 10; s.height = 10; UIKit.Radius(s, 5); s.backgroundColor = Pal.Ink; s.marginLeft = 5; s.marginRight = 5; });
                dots.Add(dot[i]);
            }
            root.Add(dots);
            UIKit.PassThrough(root);
            root.pickingMode = PickingMode.Position; // the whole screen takes the skip tap

            float start = Time.realtimeSinceStartup;
            bool finished = false, stamped = false, landed = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                done?.Invoke();
            }
            root.RegisterCallback<PointerDownEvent>(_ => { if (Time.realtimeSinceStartup - start > 0.4f) Finish(); });

            root.schedule.Execute(() =>
            {
                if (finished) return;
                float t = Time.realtimeSinceStartup - start;

                // Chalk writing: reveal each line left to right, the chalk riding its edge.
                float w1 = Ease(Mathf.InverseLerp(Write1From, Write1To, t)) * TextWidth;
                float w2 = Ease(Mathf.InverseLerp(Write2From, Write2To, t)) * TextWidth;
                clip1.style.width = w1;
                clip2.style.width = w2;
                bool onLine2 = t >= Write2From;
                float cx = 22f + (onLine2 ? w2 : w1);
                chalk.style.left = cx - 6f;
                chalk.style.top = onLine2 ? 168f : 94f;
                chalk.style.opacity = t < Write2To + 0.1f ? 1f : Mathf.Clamp01(1f - (t - Write2To - 0.1f) * 5f);

                // Pen drop with one small bounce.
                float y;
                if (t < DropFrom) y = -800f;
                else if (t < DropTo) { float k = (t - DropFrom) / (DropTo - DropFrom); y = -800f * (1f - k * k); }
                else if (t < BounceTo) { float k = (t - DropTo) / (BounceTo - DropTo); y = -18f * Mathf.Sin(k * Mathf.PI); }
                else y = 0f;
                pen.style.translate = new Translate(0, y);
                pen.style.rotate = new Rotate(new Angle(Mathf.Lerp(-40f, -12f, Mathf.InverseLerp(DropFrom, DropTo, t)), AngleUnit.Degree));
                if (!landed && t >= DropTo)
                {
                    landed = true;
                    gm.Audio.DeskTap(0.8f);
                    Haptics.Pulse(14, 160);
                }

                // FLICK! stamp slams onto the board.
                float sk = Mathf.InverseLerp(StampFrom, StampTo, t);
                stamp.style.opacity = t < StampFrom ? 0f : Mathf.Clamp01(sk * 3f);
                float sc = sk < 0.75f ? Mathf.Lerp(2.6f, 0.94f, sk / 0.75f) : Mathf.Lerp(0.94f, 1f, (sk - 0.75f) / 0.25f);
                stamp.style.scale = new Scale(new Vector3(sc, sc, 1f));
                stamp.style.rotate = new Rotate(new Angle(-10f, AngleUnit.Degree));
                if (!stamped && t >= StampTo - 0.06f)
                {
                    stamped = true;
                    gm.Audio.Stamp();
                }

                // Loading dots pulse one after another.
                for (int i = 0; i < 3; i++)
                    dot[i].style.opacity = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin((t * 3.5f) - i * 0.9f));

                if (t >= Total) Finish();
            }).Every(16);

            gm.UI.ShowScreen(root);
        }

        /// <summary>One line of chalk text inside a box whose width is animated to reveal it.</summary>
        static VisualElement ChalkLine(string text, Color c, float top, out VisualElement clip)
        {
            clip = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.left = 22; s.top = top; s.height = 72; s.width = 0; s.overflow = Overflow.Hidden;
            });
            var label = UIKit.Disp(text, 62f, c).St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.top = 0; s.width = TextWidth; s.whiteSpace = WhiteSpace.NoWrap;
            });
            clip.Add(label);
            return clip;
        }

        static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
