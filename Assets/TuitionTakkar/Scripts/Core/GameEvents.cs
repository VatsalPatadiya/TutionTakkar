using System;

namespace TuitionTakkar
{
    /// <summary>Small event hub so physics, audio, haptics and camera stay decoupled.</summary>
    public static class GameEvents
    {
        /// <summary>Two pens collided. Strength is impact speed in real m/s.</summary>
        public static event Action<float> PenHitPen;
        /// <summary>A pen bumped the bench top. Strength in real m/s.</summary>
        public static event Action<float> PenHitDesk;
        /// <summary>A pen hit the classroom floor.</summary>
        public static event Action<PenBody, float> PenHitFloor;
        /// <summary>A flick was released. Power 0..1.</summary>
        public static event Action<float> Flicked;
        /// <summary>Aim power crossed a 25% step while pulling.</summary>
        public static event Action<int> AimStep;
        /// <summary>A second ticked in the last 10 seconds of a turn (seconds left).</summary>
        public static event Action<int> TimerTick;

        public static void RaisePenHitPen(float s) => PenHitPen?.Invoke(s);
        public static void RaisePenHitDesk(float s) => PenHitDesk?.Invoke(s);
        public static void RaisePenHitFloor(PenBody p, float s) => PenHitFloor?.Invoke(p, s);
        public static void RaiseFlicked(float power) => Flicked?.Invoke(power);
        public static void RaiseAimStep(int step) => AimStep?.Invoke(step);
        public static void RaiseTimerTick(int secondsLeft) => TimerTick?.Invoke(secondsLeft);

        public static void ClearAll()
        {
            PenHitPen = null;
            PenHitDesk = null;
            PenHitFloor = null;
            Flicked = null;
            AimStep = null;
            TimerTick = null;
        }
    }
}
