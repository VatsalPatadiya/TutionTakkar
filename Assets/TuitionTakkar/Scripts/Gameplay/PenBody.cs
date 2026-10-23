using UnityEngine;

namespace TuitionTakkar
{
    public enum SurfaceKind { Desk, Floor }

    /// <summary>Marks a collider as desk or floor so pens know what they hit.</summary>
    public class Surface : MonoBehaviour
    {
        public SurfaceKind kind;
    }

    public struct Shot
    {
        public float strikeT;   // -1 tip .. +1 cap
        public Vector3 dir;     // flat, normalised
        public float power;     // 0..1
    }

    public struct PenState
    {
        public Vector3 pos;
        public Quaternion rot;
    }

    /// <summary>Shared flick maths so the real game and the bot's simulation match.</summary>
    public static class Flick
    {
        public static float Impulse(PenDef def, float power)
        {
            float j = Mathf.Lerp(GameConfig.ImpulseMin, GameConfig.ImpulseMax, Mathf.Clamp01(power));
            return j * Mathf.Pow(def.mass / GameConfig.RefMass, GameConfig.MassAdapt);
        }

        public static Vector3 StrikePoint(PenDef def, Vector3 centre, Quaternion rot, float strikeT)
        {
            Vector3 axis = rot * Vector3.right;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
            axis.Normalize();
            float half = PenFactory.ScaledLength(def) * 0.5f;
            return centre + axis * (Mathf.Clamp(strikeT, -1f, 1f) * half * GameConfig.StrikeRange);
        }

        public static void Apply(Rigidbody rb, PenDef def, Shot s)
        {
            Vector3 centre = rb.worldCenterOfMass;
            Vector3 point = StrikePoint(def, rb.position, rb.rotation, s.strikeT);
            point.y = centre.y; // push horizontally through the centre height: slides and spins, no flip
            Vector3 dir = s.dir;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            rb.WakeUp();
            rb.AddForceAtPosition(dir * Impulse(def, s.power), point, ForceMode.Impulse);
        }
    }

    public class PenBody : MonoBehaviour
    {
        public PenDef Def { get; private set; }
        public int Index { get; private set; }
        public Rigidbody Rb { get; private set; }
        public float Length { get; private set; }
        public float Radius { get; private set; }
        public bool HitFloor { get; private set; }

        public Vector3 Axis
        {
            get
            {
                var a = transform.right;
                a.y = 0f;
                return a.sqrMagnitude < 1e-6f ? Vector3.right : a.normalized;
            }
        }

        public Vector3 Centre => Rb.worldCenterOfMass;
        public Vector3 TipEnd => transform.position - transform.right * Length * 0.5f;
        public Vector3 CapEnd => transform.position + transform.right * Length * 0.5f;

        public bool IsQuiet =>
            Rb.IsSleeping() ||
            (Rb.linearVelocity.magnitude < GameConfig.SettleLinear &&
             Rb.angularVelocity.magnitude < GameConfig.SettleAngular);

        public static PenBody Create(PenDef def, int index, Transform parent)
        {
            var go = new GameObject("Pen_" + index + "_" + def.id);
            go.transform.SetParent(parent, false);
            var pen = go.AddComponent<PenBody>();
            pen.Def = def;
            pen.Index = index;
            pen.Length = PenFactory.ScaledLength(def);
            pen.Radius = PenFactory.ScaledRadius(def);
            PenFactory.BuildVisual(go.transform, def);
            pen.Rb = PenFactory.BuildPhysics(go, def, true);
            return pen;
        }

        public void Place(Vector3 pos, Quaternion rot)
        {
            gameObject.SetActive(true);
            Rb.isKinematic = false;
            Rb.position = pos;
            Rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            PenFactory.Flatten(Rb);
            HitFloor = false;
        }

        void FixedUpdate()
        {
            PenFactory.ReleaseIfOverEdge(Rb, Def);
            PenFactory.KeepOnDesk(Rb, Def);
        }

        public PenState Snapshot() => new PenState { pos = Rb.position, rot = Rb.rotation };

        void OnCollisionEnter(Collision c)
        {
            float s = c.relativeVelocity.magnitude / GameConfig.WorldScale;
            var other = c.collider.GetComponentInParent<PenBody>();
            if (other != null)
            {
                if (Index < other.Index) GameEvents.RaisePenHitPen(s); // report once per pair
                return;
            }

            var surf = c.collider.GetComponent<Surface>();
            if (surf == null) return;
            if (surf.kind == SurfaceKind.Floor)
            {
                if (!HitFloor)
                {
                    HitFloor = true;
                    GameEvents.RaisePenHitFloor(this, s);
                }
            }
            else
            {
                GameEvents.RaisePenHitDesk(s);
            }
        }
    }
}
