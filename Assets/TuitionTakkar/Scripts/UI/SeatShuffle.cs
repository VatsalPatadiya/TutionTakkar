using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// A quick slot-machine card shuffle for a Quick Match seat that just got filled by a bot:
    /// a handful of random pen cards flicker past, slowing down, before settling on the real
    /// (disguised) seat — so a bot's reveal reads like a classmate being found, not an instant swap.
    /// </summary>
    public static class SeatShuffle
    {
        const int Frames = 5;

        public static void Play(VisualElement container, string name, PenDef pen, bool dark)
        {
            var filler = Catalog.Pens;
            int frame = 0;

            void Step()
            {
                container.Clear();
                if (frame >= Frames)
                {
                    container.Add(OnlineScreens.SeatRow(name, pen, dark, name));
                    return;
                }
                var p = filler[Random.Range(0, filler.Count)];
                container.Add(OnlineScreens.SeatRow(p.name, p, dark, p.id));
                frame++;
                container.schedule.Execute(Step).StartingIn(80 + frame * 60); // slows down toward the end
            }
            Step();
        }
    }
}
