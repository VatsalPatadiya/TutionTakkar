using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Light motion blur for a fast pen, like a phone camera catches in a real pen fight:
    /// a short see-through smear behind the pen while it moves fast. A pen moving sideways
    /// smears its whole length; one moving along its length leaves a thin streak.
    /// Purely visual: it has no collider and doesn't touch the physics.
    /// </summary>
    [RequireComponent(typeof(PenBody))]
    public class PenStreak : MonoBehaviour
    {
        /// <summary>Speed (real cm/s) where the smear starts to show, and where it is full.</summary>
        const float FromCmS = 45f, FullCmS = 110f;
        const float MaxAlpha = 0.28f;
        const float Life = 0.07f;

        PenBody pen;
        TrailRenderer trail;
        Color tint;

        void Awake()
        {
            pen = GetComponent<PenBody>();
            var go = new GameObject("Streak");
            go.transform.SetParent(transform, false);
            trail = go.AddComponent<TrailRenderer>();
            trail.time = Life;
            trail.minVertexDistance = 0.02f;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            tint = pen.Def != null ? pen.Def.color : Color.white;
            var m = ArtLib.Line(tint);
            m.mainTexture = Texture2D.whiteTexture;
            trail.sharedMaterial = m;
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        void LateUpdate()
        {
            var rb = pen.Rb;
            if (rb == null || !pen.gameObject.activeInHierarchy || rb.isKinematic)
            {
                trail.emitting = false;
                return;
            }
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            float cms = v.magnitude / GameConfig.WorldScale * 100f;
            float k = Mathf.InverseLerp(FromCmS, FullCmS, cms);
            bool on = k > 0f && pen.IsFlat;
            if (on != trail.emitting)
            {
                if (on) trail.Clear();
                trail.emitting = on;
            }
            if (!on) return;

            // Sideways motion smears the whole pen; lengthwise only its thickness.
            float along = v.sqrMagnitude > 1e-6f ? Mathf.Abs(Vector3.Dot(v.normalized, pen.Axis)) : 1f;
            trail.widthMultiplier = Mathf.Lerp(pen.Length * 0.85f, pen.Radius * 2f, along);
            trail.transform.position = pen.Centre + Vector3.up * pen.Radius;
            SetAlpha(MaxAlpha * k);
        }
    }
}
