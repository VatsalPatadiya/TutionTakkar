using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Every tuning number in one place. The world runs at 10x real scale
    /// (1 unit = 10 cm) so small pens stay stable in PhysX; gravity and
    /// impulses are scaled to match, so motion behaves like real size.
    /// </summary>
    public static class GameConfig
    {
        public const float WorldScale = 10f;
        /// <summary>Physics step. 240 Hz so a full-power pen moves less than its own thickness per step.</summary>
        public const float FixedDt = 1f / 240f;
        public static readonly Vector3 Gravity = new Vector3(0f, -9.81f * WorldScale, 0f);

        // Tuition desk, long side runs up the screen. Kept small so the
        // real-size pens look big on screen, like the "D · Tuition desk" design.
        public const float DeskWidth = 0.27f * WorldScale;   // x
        public const float DeskDepth = 0.50f * WorldScale;   // z
        public const float DeskThickness = 0.03f * WorldScale;
        public const float DeskHeight = 0.72f * WorldScale;
        // Grippier desk: pens slow down steadily (feels weighty) instead of gliding far.
        public const float DeskFriction = 0.40f;
        public const float DeskBounce = 0.10f;

        /// <summary>A pen is out once its centre drops this far below the desk top.</summary>
        public const float OutDepth = 0.04f * WorldScale;

        // Starting spots for the two pens (distance from centre along z).
        public const float StartOffsetZ = 0.13f * WorldScale;

        // Flick input (pixel values are in the 390-wide reference layout).
        public const float DeadZoneRef = 12f;
        public const float FullPullScreenFraction = 0.30f;
        public const float MinFullPullRef = 120f;
        public const float PowerGamma = 1.45f;
        public const float GrabRadiusTouchRef = 52f;
        public const float GrabRadiusMouseRef = 34f;
        public const float StrikeRange = 0.9f;

        // Impulse range in scaled units (real N*s x WorldScale).
        public const float ImpulseMin = 0.0012f * WorldScale;
        // A full flick slides ~35 cm on its own and slows down gradually. From a short run-up,
        // a full-power hit pushes the other pen ~9 cm while yours stops after ~3 cm, so a pen
        // is worked off the edge in two or three good hits. (0.020 with no bounce left touching
        // pens glued together: 100% moved the pair only 3 cm and rounds never ended.)
        public const float ImpulseMax = 0.024f * WorldScale;
        public const float RefMass = 0.0138f;
        public const float MassAdapt = 0.8f;

        // Pens
        public const float PenLinearDamping = 0.06f;
        /// <summary>
        /// Pens are drawn (and collide) bigger than real life so they read well on a phone:
        /// a bit longer, and noticeably thicker. Mass stays real, so hits feel the same.
        /// Both the visuals and the colliders use these, so what you see is what you hit.
        /// </summary>
        public const float PenLengthScale = 1f;
        public const float PenThicknessScale = 1f;
        /// <summary>
        /// Pen-on-pen hits are soft pushes, not sharp bounces: each pen's bounciness is scaled
        /// to ~0.2. With no bounce at all the push never passed on (pens moved as a glued pair);
        /// springy pens made hits look like jumps. This sits in between.
        /// </summary>
        public const float PenBounceScale = 0.6f;
        /// <summary>
        /// Extra grip added to every pen: pens rubbing against each other turn each other,
        /// so contacts curve and spin naturally instead of sliding apart in straight lines.
        /// </summary>
        public const float PenGripBoost = 0.08f;
        public const float PenMaxAngularVelocity = 40f;

        // Turn settling
        public const float SettleLinear = 0.015f * WorldScale;
        public const float SettleAngular = 0.15f;
        public const float SettleHold = 0.3f;
        /// <summary>Wait longer before ending a turn while a pen hangs over an edge, so a slow tip-off still counts.</summary>
        public const float SettleHoldOverhang = 0.9f;
        public const float SettleTimeout = 6f;

        // Match
        public const int RoundsToWin = 3; // best of 5

        // Turn timer (T1-T3 designs): a human gets this long to shoot, then the game shoots for them.
        public const float TurnTimeLimit = 60f;
        /// <summary>Online turns are shorter: someone is waiting on the other phone.</summary>
        public const float OnlineTurnTimeLimit = 30f;
        /// <summary>Extra seconds to wait for the other phone before it counts as gone.</summary>
        public const float OnlineAbsentGrace = 20f;
        /// <summary>
        /// Online: auto shots each player may get per match when their clock runs out.
        /// One more time-out after that and they lose the match (or their pen is out).
        /// </summary>
        public const int OnlineMaxAutoShots = 3;
        /// <summary>3–4 player online desk: a square two-seater (52 x 52 cm).</summary>
        public const float PartyDeskSize = 0.52f * WorldScale;
        /// <summary>How far from the middle each pen starts on the party desk.</summary>
        public const float PartyStartRadius = 0.15f * WorldScale;
        public const float TurnTimeUrgent = 10f;
        /// <summary>
        /// The automatic shot when your time runs out plays it safe: steady aim, gentle power,
        /// very careful about its own pen, no deliberate mistakes. It must never throw the
        /// idle player's own pen off the desk.
        /// </summary>
        public static readonly BotProfile AutoShotProfile = new BotProfile(3f, 0.03f, 1f, 0.2f, 0f);
        /// <summary>Most power the automatic shot may use.</summary>
        public const float AutoShotMaxPower = 0.55f;

        // Fair play vs computer: each round a bot is ahead by, it gets a bit sloppier
        // (wider aim, shakier power, more mistakes, less careful), so every rival stays beatable.
        public const float BotLeadAimMul = 0.6f;
        public const float BotLeadPowerNoise = 0.08f;
        public const float BotLeadMistake = 0.15f;
        public const float BotLeadCalm = 0.25f;
        /// <summary>A long round wears the bot down too: per shot after this many, it eases a little.</summary>
        public const int BotTiredAfterShots = 6;

        // Bot timing
        public const float BotThinkMin = 0.35f;
        public const float BotTelegraph = 0.45f;
        /// <summary>Most time a bot spends planning (ms); it keeps the best shot found so far.</summary>
        public const float BotThinkMaxMs = 900f;
        /// <summary>Planning time for the automatic shot after the turn timer runs out (ms).</summary>
        public const float AutoShotThinkMs = 250f;
        /// <summary>How long the auto shot shows its aim before firing (s).</summary>
        public const float AutoShotTelegraph = 0.6f;

        /// <summary>
        /// After a shot settles, pens that ended up touching are slid apart to at least this gap
        /// (world units, ~8 mm) so the next flick can't push both pens at once.
        /// </summary>
        public const float MinPenGap = 0.008f * WorldScale;
        /// <summary>
        /// Slide touching pens a few millimetres apart after a shot. On: two pens left pressed
        /// together could only be shoved as a pair (3 cm at full power), which stalled rounds.
        /// With a small gap the next flick reaches the other pen at speed and actually moves it.
        /// </summary>
        public const bool SeparateTouchingPens = true;
        public const float SeparateSeconds = 0.18f;
        /// <summary>Fastest a pen on the desk may move upward (world units/s, ~25 cm/s): enough to ride over another pen, not to fly.</summary>
        public const float MaxRiseSpeed = 0.25f * WorldScale;

        /// <summary>A pen this close to an edge (world units) is "in danger": edge warning + bot scoring.</summary>
        public const float DangerZone = 0.045f * WorldScale;

        // Feel
        public const float TeeterSlowMo = 0.35f;
        public const float TeeterMaxSeconds = 1.2f;
    }
}
