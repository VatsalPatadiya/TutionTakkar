using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Mockup N2 during an online match: if this phone loses the connection, a popup counts
    /// down while sends keep retrying. Back in time: the match carries on where it was
    /// (shots wait in the room). Not back: the match is lost and we go home.
    /// </summary>
    public class ConnectionWatch
    {
        public const float GiveUpAfter = 30f;
        const float ShowAfter = 2f;

        public readonly VisualElement Root;
        readonly GameManager gm;
        readonly OnlineLink link;
        readonly System.Action onGiveUp;
        VisualElement popup;
        float lostSince = -1f;
        int shownSeconds = -1;
        bool done;

        public ConnectionWatch(GameManager gm, OnlineLink link, System.Action onGiveUp)
        {
            this.gm = gm;
            this.link = link;
            this.onGiveUp = onGiveUp;
            Root = UIKit.El("ConnectionWatch");
            UIKit.Fill(Root);
            Root.pickingMode = PickingMode.Ignore;
            Root.schedule.Execute(Tick).Every(250);
        }

        public void Detach()
        {
            done = true;
            Root.RemoveFromHierarchy();
        }

        void Tick()
        {
            if (done) return;
            float now = Time.realtimeSinceStartup;
            bool bad = !NetPopups.IsOnline || link.NetFailing;
            if (bad)
            {
                if (lostSince < 0f) lostSince = now;
                float lost = now - lostSince;
                float left = GiveUpAfter - lost;
                if (left <= 0f)
                {
                    done = true;
                    Hide();
                    onGiveUp?.Invoke();
                    return;
                }
                if (lost >= ShowAfter) Show(Mathf.CeilToInt(left));
                link.Nudge(99f); // keep knocking: a successful refresh clears NetFailing
            }
            else if (lostSince >= 0f)
            {
                lostSince = -1f;
                Hide();
                link.Nudge(99f);
            }
        }

        void Show(int seconds)
        {
            if (seconds == shownSeconds && popup != null) return;
            bool first = popup == null;
            shownSeconds = seconds;
            popup?.RemoveFromHierarchy();
            popup = NetPopups.ConnectionLost(gm, seconds, () =>
            {
                done = true;
                Hide();
                onGiveUp?.Invoke();
            }, first);
            Root.Add(popup);
        }

        void Hide()
        {
            popup?.RemoveFromHierarchy();
            popup = null;
            shownSeconds = -1;
        }
    }
}
