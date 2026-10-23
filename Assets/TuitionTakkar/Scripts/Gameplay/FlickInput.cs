using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>Live aim data for the HUD (positions are in UI panel units).</summary>
    public struct AimInfo
    {
        public float power;
        public float strikeT;
        public Vector2 fingerPanel;
        public Vector2 penPanel;
    }

    /// <summary>
    /// Press on your pen, drag back, let go. Where you grab along the pen
    /// sets spin; how far you pull sets power. Reads UI Toolkit pointer events
    /// so it works with either input backend, mouse or touch.
    /// </summary>
    public class FlickInput : MonoBehaviour
    {
        public event Action<AimInfo> AimChanged;
        public event Action AimEnded;

        BenchWorld world;
        AimVisual aim;
        VisualElement surface;

        PenBody pen;
        Action<Shot> onShot;
        int pointerId = -1;
        Vector2 startScreen;
        Vector3 startWorld;
        float strikeT;
        float power;
        Vector3 dir;
        int lastStep;

        public bool Enabled => pen != null;

        /// <summary>The player is holding a pull with real power right now.</summary>
        public bool IsAiming => pen != null && pointerId >= 0 && power >= 0.02f && dir.sqrMagnitude > 0.5f;

        /// <summary>Time ran out mid-pull: fire the aim the player is holding, as if they let go.</summary>
        public bool ReleaseNow()
        {
            if (!IsAiming) return false;
            if (surface.HasPointerCapture(pointerId)) surface.ReleasePointer(pointerId);
            pointerId = -1;
            aim.Hide();
            AimEnded?.Invoke();
            var shot = new Shot { strikeT = strikeT, dir = dir, power = power };
            var cb = onShot;
            cb?.Invoke(shot);
            return true;
        }

        public void Init(BenchWorld w, AimVisual a, VisualElement inputSurface)
        {
            world = w;
            aim = a;
            surface = inputSurface;
            surface.RegisterCallback<PointerDownEvent>(OnDown);
            surface.RegisterCallback<PointerMoveEvent>(OnMove);
            surface.RegisterCallback<PointerUpEvent>(OnUp);
            surface.RegisterCallback<PointerCancelEvent>(OnCancel);
        }

        public void Enable(PenBody p, Action<Shot> callback)
        {
            pen = p;
            onShot = callback;
            pointerId = -1;
        }

        public void Disable()
        {
            if (pointerId >= 0 && surface.HasPointerCapture(pointerId)) surface.ReleasePointer(pointerId);
            pen = null;
            onShot = null;
            pointerId = -1;
            aim.Hide();
            AimEnded?.Invoke();
        }

        float RefScale => Screen.width / 390f;

        Vector2 PanelToScreen(Vector2 p)
        {
            var size = surface.panel.visualTree.layout.size;
            if (size.x <= 0f || size.y <= 0f) return p;
            return new Vector2(p.x * Screen.width / size.x, Screen.height - p.y * Screen.height / size.y);
        }

        bool ScreenToDesk(Vector2 screen, float height, out Vector3 world)
        {
            var ray = this.world.Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            if (plane.Raycast(ray, out float d))
            {
                world = ray.GetPoint(d);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        void OnDown(PointerDownEvent e)
        {
            if (pen == null || pointerId >= 0 || Time.timeScale == 0f) return;
            var screen = PanelToScreen(e.position);

            // Distance from the touch to the pen as drawn on screen.
            var cam = world.Cam;
            Vector2 a = cam.WorldToScreenPoint(pen.TipEnd);
            Vector2 b = cam.WorldToScreenPoint(pen.CapEnd);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-3f ? 0.5f : Mathf.Clamp01(Vector2.Dot(screen - a, ab) / ab.sqrMagnitude);
            float px = Vector2.Distance(screen, a + ab * t);
            float reach = (e.pointerType == UnityEngine.UIElements.PointerType.touch ? GameConfig.GrabRadiusTouchRef : GameConfig.GrabRadiusMouseRef) * RefScale;
            if (px > reach) return;

            pointerId = e.pointerId;
            surface.CapturePointer(pointerId);
            startScreen = screen;
            ScreenToDesk(screen, pen.Centre.y, out startWorld);
            strikeT = t * 2f - 1f;
            power = 0f;
            dir = Vector3.zero;
            lastStep = 0;
            UpdateVisual(screen, e.position);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (pen == null || e.pointerId != pointerId) return;
            var screen = PanelToScreen(e.position);

            float dz = GameConfig.DeadZoneRef * RefScale;
            float full = Mathf.Max(GameConfig.FullPullScreenFraction * Screen.height, GameConfig.MinFullPullRef * RefScale);
            float pull = Vector2.Distance(screen, startScreen);
            float n = Mathf.Clamp01((pull - dz) / Mathf.Max(full - dz, 1f));
            power = Mathf.Pow(n, GameConfig.PowerGamma);

            if (ScreenToDesk(screen, pen.Centre.y, out var cur))
            {
                var d = startWorld - cur;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            }

            int step = Mathf.FloorToInt(power * 4f + 0.0001f);
            if (step != lastStep)
            {
                if (step > lastStep) GameEvents.RaiseAimStep(step);
                lastStep = step;
            }
            UpdateVisual(screen, e.position);
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != pointerId) return;
            if (surface.HasPointerCapture(pointerId)) surface.ReleasePointer(pointerId);
            pointerId = -1;
            aim.Hide();
            AimEnded?.Invoke();

            if (pen == null || power < 0.02f || dir.sqrMagnitude < 0.5f) return; // tiny pull = cancel
            var shot = new Shot { strikeT = strikeT, dir = dir, power = power };
            var cb = onShot;
            cb?.Invoke(shot);
        }

        void OnCancel(PointerCancelEvent e)
        {
            if (e.pointerId != pointerId) return;
            pointerId = -1;
            aim.Hide();
            AimEnded?.Invoke();
        }

        void UpdateVisual(Vector2 fingerScreen, Vector2 fingerPanel)
        {
            Vector3 grab = Flick.StrikePoint(pen.Def, pen.Rb.position, pen.Rb.rotation, strikeT);
            Vector3? finger = null;
            if (ScreenToDesk(fingerScreen, 0f, out var f)) finger = f;
            aim.Show(grab, finger, dir, power, pen.Length * 0.5f);
            AimChanged?.Invoke(new AimInfo
            {
                power = power,
                strikeT = strikeT,
                fingerPanel = fingerPanel,
                penPanel = ScreenToPanel(world.Cam.WorldToScreenPoint(pen.Rb.position))
            });
        }

        Vector2 ScreenToPanel(Vector3 screen)
        {
            var size = surface.panel.visualTree.layout.size;
            return new Vector2(screen.x * size.x / Screen.width, (Screen.height - screen.y) * size.y / Screen.height);
        }
    }
}
