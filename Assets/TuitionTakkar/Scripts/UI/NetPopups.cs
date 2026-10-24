using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Internet popups (mockups N1, N2): "No internet" when an online feature is tapped
    /// offline, and "Connection lost" for a phone that drops out of an online match.
    /// Until online play ships, tapping an online feature with internet shows "coming soon".
    /// </summary>
    public static class NetPopups
    {
        public static bool IsOnline => Application.internetReachability != NetworkReachability.NotReachable;

        /// <summary>Dimmed backdrop with a taped notebook card in the middle.</summary>
        static VisualElement Card(GameManager gm, out VisualElement card, float rot, bool pop = true)
        {
            var o = UIKit.El().St(s => s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.62f));
            o.pickingMode = PickingMode.Position;
            var wrap = UIKit.Col().St(s => { s.flexGrow = 1; s.justifyContent = Justify.Center; UIKit.Pad(s, 0, 26, 0, 26); });
            card = UIKit.Col().St(s =>
            {
                UIKit.Pad(s, 26, 22, 22, 22); s.alignItems = Align.Stretch;
                s.borderLeftWidth = 3; s.borderLeftColor = new Color(0.85f, 0.28f, 0.18f, 0.5f);
                s.borderBottomWidth = 6; s.borderBottomColor = new Color(0f, 0f, 0f, 0.18f);
            });
            UIKit.GraphPaper(card);
            UIKit.Rot(card, rot);
            var tape = UIKit.El().St(s =>
            {
                s.position = Position.Absolute; s.top = -12; s.left = Length.Percent(50); s.marginLeft = -44; s.width = 88; s.height = 22;
                s.backgroundColor = new Color(Pal.Tape.r, Pal.Tape.g, Pal.Tape.b, 0.92f);
            });
            UIKit.Rot(tape, -3f);
            card.Add(tape);
            wrap.Add(card);
            o.Add(wrap);
            if (pop) UIRoot.PopIn(card);
            return o;
        }

        static Button Secondary(string text, System.Action go, Color c)
        {
            var b = UIKit.Btn(text, go, Color.clear, c, UIKit.Display, 20f, 56f, 16f, null, c);
            b.style.marginTop = 10;
            return b;
        }

        /// <summary>N1: no internet. Try again re-checks; Play offline closes the popup.</summary>
        public static VisualElement NoInternet(GameManager gm)
        {
            var o = Card(gm, out var c, -1f);
            var icon = new LineIcon(LineIcon.Kind.NoWifi, 110f, 88f);
            icon.style.alignSelf = Align.Center;
            c.Add(icon);
            c.Add(UIKit.Disp("NO INTERNET", 32f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            c.Add(UIKit.HandText("online play needs internet. turn on wi-fi or mobile data and try again.", 18f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));

            var still = UIKit.Col().St(s =>
            {
                s.marginTop = 14; UIKit.Pad(s, 10, 12, 10, 12); s.backgroundColor = Color.white; UIKit.Radius(s, 10);
                UIKit.Border(s, 2f, Pal.Dash);
            });
            still.Add(UIKit.Strong("Still works offline", 13f, Pal.Ink));
            still.Add(UIKit.Plain("Batch Ladder · Practice · Pass & Play · Pencil pouch", 12f, Pal.InkSoft).St(s => s.marginTop = 2));
            c.Add(still);

            var retry = UIKit.PrimaryBtn("TRY AGAIN", () =>
            {
                if (IsOnline) { gm.UI.HideOverlay(); OnlineScreens.Enter(gm); }
                else gm.UI.ShowOverlay(NoInternet(gm)); // pops again: a clear "still offline"
            }, 56f);
            retry.style.marginTop = 18;
            c.Add(retry);
            c.Add(Secondary("PLAY OFFLINE", () => gm.UI.HideOverlay(), Pal.Ink));
            return o;
        }

        /// <summary>Shown for online features until online play ships.</summary>
        public static VisualElement ComingSoon(GameManager gm)
        {
            var o = Card(gm, out var c, 0.8f);
            var icon = new LineIcon(LineIcon.Kind.Trophy, 70f, 70f);
            icon.style.alignSelf = Align.Center;
            c.Add(icon);
            c.Add(UIKit.Disp("ONLINE IS COMING", 30f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            c.Add(UIKit.HandText("2, 3 or 4 classmates on one desk, Google sign-in and a class leaderboard. it's in the next update.", 17f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            var ok = UIKit.PrimaryBtn("GOT IT", () => gm.UI.HideOverlay(), 56f);
            ok.style.marginTop = 18;
            c.Add(ok);
            return o;
        }

        /// <summary>
        /// N2: a phone dropped out of an online match. Shows the seconds left to come back;
        /// onLeave quits the match. The online match code calls this and closes it when the
        /// connection returns.
        /// </summary>
        public static VisualElement ConnectionLost(GameManager gm, int secondsLeft, System.Action onLeave, bool pop = true)
        {
            var o = Card(gm, out var c, 0.8f, pop);
            var ring = UIKit.Disp("0:" + secondsLeft.ToString("00"), 30f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
            {
                s.alignSelf = Align.Center; s.width = 96; s.height = 96; UIKit.Radius(s, 48); UIKit.Border(s, 8f, Pal.Orange);
            });
            c.Add(ring);
            c.Add(UIKit.Disp("CONNECTION LOST", 30f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 12));
            c.Add(UIKit.HandText("trying to get you back on the desk. your pen stays where it is.", 18f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            c.Add(UIKit.Strong("if you're not back in 30 seconds, your pen is out of this match", 12f, Pal.H("#7A2A1C")).St(s =>
            {
                s.marginTop = 12; UIKit.Pad(s, 10, 12, 10, 12); s.backgroundColor = Pal.H("#FFF4E5"); UIKit.Radius(s, 10);
            }));
            var wait = UIKit.Btn("RECONNECTING…", null, Pal.H("#E9E2D0"), Pal.H("#8A7A5A"), UIKit.Display, 20f, 56f, 16f, Pal.H("#C9BFA8"));
            wait.style.marginTop = 18;
            c.Add(wait);
            c.Add(Secondary("LEAVE MATCH", onLeave, Pal.Red));
            return o;
        }
    }

    /// <summary>Small hand-drawn line icons: no-wifi, trophy, gear, people, robot.</summary>
    public class LineIcon : VisualElement
    {
        public enum Kind { NoWifi, Trophy, Gear, People, Robot }

        readonly Kind kind;
        readonly Color color;

        public LineIcon(Kind kind, float w, float h, Color? color = null)
        {
            this.kind = kind;
            this.color = color ?? Pal.Ink;
            pickingMode = PickingMode.Ignore;
            style.width = w;
            style.height = h;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        static Angle Deg(float d) => new Angle(d, AngleUnit.Degree);

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f) return;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            switch (kind)
            {
                case Kind.NoWifi:
                {
                    var c = new Vector2(w * 0.5f, h * 0.88f);
                    p.lineWidth = w * 0.05f;
                    foreach (var k in new[] { 0.28f, 0.52f, 0.76f })
                    {
                        p.BeginPath();
                        p.Arc(c, w * 0.5f * k * 1.1f, Deg(-140f), Deg(-40f));
                        p.Stroke();
                    }
                    p.BeginPath(); p.Arc(c, w * 0.05f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
                    p.strokeColor = Pal.H("#D9472F");
                    p.lineWidth = w * 0.06f;
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.18f, h * 0.12f)); p.LineTo(new Vector2(w * 0.82f, h * 0.92f)); p.Stroke();
                    break;
                }
                case Kind.Trophy:
                {
                    p.lineWidth = w * 0.07f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(w * 0.28f, h * 0.15f)); p.LineTo(new Vector2(w * 0.72f, h * 0.15f));
                    p.LineTo(new Vector2(w * 0.72f, h * 0.38f));
                    p.Arc(new Vector2(w * 0.5f, h * 0.38f), w * 0.22f, Deg(0), Deg(180));
                    p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.5f, h * 0.6f)); p.LineTo(new Vector2(w * 0.5f, h * 0.8f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.32f, h * 0.85f)); p.LineTo(new Vector2(w * 0.68f, h * 0.85f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.28f, h * 0.2f)); p.LineTo(new Vector2(w * 0.14f, h * 0.2f)); p.LineTo(new Vector2(w * 0.16f, h * 0.34f)); p.LineTo(new Vector2(w * 0.3f, h * 0.42f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.72f, h * 0.2f)); p.LineTo(new Vector2(w * 0.86f, h * 0.2f)); p.LineTo(new Vector2(w * 0.84f, h * 0.34f)); p.LineTo(new Vector2(w * 0.7f, h * 0.42f)); p.Stroke();
                    break;
                }
                case Kind.Gear:
                {
                    var c = new Vector2(w * 0.5f, h * 0.5f);
                    p.lineWidth = w * 0.09f;
                    p.BeginPath(); p.Arc(c, w * 0.16f, Deg(0), Deg(360)); p.ClosePath(); p.Stroke();
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45f * Mathf.Deg2Rad;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.BeginPath(); p.MoveTo(c + d * w * 0.28f); p.LineTo(c + d * w * 0.42f); p.Stroke();
                    }
                    break;
                }
                case Kind.People:
                {
                    p.lineWidth = w * 0.08f;
                    p.BeginPath(); p.Arc(new Vector2(w * 0.37f, h * 0.33f), w * 0.14f, Deg(0), Deg(360)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(w * 0.37f, h * 0.9f), w * 0.26f, Deg(180), Deg(360)); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(w * 0.7f, h * 0.37f), w * 0.11f, Deg(0), Deg(360)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(w * 0.74f, h * 0.88f), w * 0.2f, Deg(200), Deg(360)); p.Stroke();
                    break;
                }
                case Kind.Robot:
                {
                    p.lineWidth = w * 0.08f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(w * 0.2f, h * 0.32f)); p.LineTo(new Vector2(w * 0.8f, h * 0.32f));
                    p.LineTo(new Vector2(w * 0.8f, h * 0.8f)); p.LineTo(new Vector2(w * 0.2f, h * 0.8f)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.5f, h * 0.32f)); p.LineTo(new Vector2(w * 0.5f, h * 0.14f)); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(w * 0.38f, h * 0.52f), w * 0.06f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.Arc(new Vector2(w * 0.62f, h * 0.52f), w * 0.06f, Deg(0), Deg(360)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(new Vector2(w * 0.4f, h * 0.67f)); p.LineTo(new Vector2(w * 0.6f, h * 0.67f)); p.Stroke();
                    break;
                }
            }
        }
    }
}
