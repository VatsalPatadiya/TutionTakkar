using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Frames the whole desk top (with its rim and front edge) in the free area between
    /// the classroom wall strip and the ruler, on any phone shape. Seen from the front
    /// in gentle perspective, like "D · Tuition desk, front view".
    /// Also adds impact shake and a small push-in during edge slow-motion.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        /// <summary>Viewport rect (0..1, y up) the bench must fit inside. Set by the UI.</summary>
        public Rect safeArea = new Rect(0.036f, 0.12f, 0.928f, 0.70f);
        public float pitch = 68f;
        /// <summary>0 = player 0 at the bottom of the screen. Online player 1 looks from the other side (180).</summary>
        public float yaw;

        Camera cam;
        Vector3 basePos;
        Quaternion baseRot;
        float shake;
        float pushIn;
        int lastW, lastH;
        Vector3[] corners;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (Screen.width != lastW || Screen.height != lastH) Fit();

            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
            Vector3 offset = Vector3.zero;
            if (shake > 0f)
            {
                float t = Time.unscaledTime * 55f;
                offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * shake * 0.25f;
            }
            transform.SetPositionAndRotation(basePos + transform.forward * pushIn + offset, baseRot);
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, Mathf.Clamp01(amount));

        public void SetPushIn(float amount) => pushIn = Mathf.Lerp(pushIn, amount, 0.15f);

        public void SetSafeArea(Rect viewport)
        {
            if (viewport.width <= 0.05f || viewport.height <= 0.05f) return;
            safeArea = viewport;
            Fit();
        }

        public void Fit()
        {
            if (cam == null) cam = GetComponent<Camera>();
            lastW = Screen.width;
            lastH = Screen.height;

            float hw = BenchWorld.HalfW + BenchWorld.RimSize;
            float hd = BenchWorld.HalfD + BenchWorld.RimSize;
            float t = GameConfig.DeskThickness;
            var turn = Quaternion.Euler(0f, yaw, 0f);
            corners = new[]
            {
                turn * new Vector3(-hw, 0f, -hd), turn * new Vector3(hw, 0f, -hd),
                turn * new Vector3(-hw, -t, -hd), turn * new Vector3(hw, -t, -hd), // keep the front edge in view
                turn * new Vector3(-hw, 0f, hd), turn * new Vector3(hw, 0f, hd)
            };

            baseRot = turn * Quaternion.Euler(pitch, 0f, 0f);
            Vector3 fwd = baseRot * Vector3.forward;

            // For each look-at point along z, find the closest distance where the whole
            // bench fits. Keep the closest (biggest bench); among near-ties (width-limited
            // phones) pick the one whose bench sits most centred in the free area.
            const int n = 161;
            var dists = new float[n];
            var errs = new float[n];
            float minD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float z = -4f + i * 0.05f;
                var target = turn * new Vector3(0f, 0f, z);
                dists[i] = MinFitDistance(target, fwd);
                transform.SetPositionAndRotation(target - fwd * dists[i], baseRot);
                errs[i] = Mathf.Abs(VerticalCentre() - safeArea.center.y);
                minD = Mathf.Min(minD, dists[i]);
            }

            int best = 0;
            float bestErr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                // A desk limited by the screen width (the square party desk) may sit a touch smaller
                // so it can be centred in the free area instead of hugging the top.
                if (dists[i] > minD * 1.05f) continue;
                if (errs[i] < bestErr) { bestErr = errs[i]; best = i; }
            }

            var bestTarget = turn * new Vector3(0f, 0f, -4f + best * 0.05f);
            basePos = bestTarget - fwd * dists[best];
            transform.SetPositionAndRotation(basePos, baseRot);
        }

        float VerticalCentre()
        {
            float lo = 1f, hi = 0f;
            foreach (var c in corners)
            {
                var v = cam.WorldToViewportPoint(c);
                lo = Mathf.Min(lo, v.y);
                hi = Mathf.Max(hi, v.y);
            }
            return (lo + hi) * 0.5f;
        }

        float MinFitDistance(Vector3 target, Vector3 fwd)
        {
            float lo = 2f, hi = 150f;
            for (int i = 0; i < 26; i++)
            {
                float mid = (lo + hi) * 0.5f;
                transform.SetPositionAndRotation(target - fwd * mid, baseRot);
                if (Fits()) hi = mid; else lo = mid;
            }
            return hi;
        }

        bool Fits()
        {
            foreach (var c in corners)
            {
                var v = cam.WorldToViewportPoint(c);
                if (v.z <= 0f) return false;
                if (v.x < safeArea.xMin || v.x > safeArea.xMax || v.y < safeArea.yMin || v.y > safeArea.yMax) return false;
            }
            return true;
        }
    }
}
