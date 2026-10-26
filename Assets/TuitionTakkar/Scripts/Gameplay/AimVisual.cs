using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Draws the aim: a dark "rubber band" from the grab point to your finger,
    /// and a white dotted arrow showing where the pen will go (longer = harder).
    /// Also used to telegraph the bot's shot for a moment before it fires.
    /// </summary>
    public class AimVisual : MonoBehaviour
    {
        LineRenderer band, path, head;
        Transform grabDot;

        public static AimVisual Create(Transform parent)
        {
            var go = new GameObject("AimVisual");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AimVisual>();
            a.band = a.MakeLine("Band", Sketch.Graphite, 0.035f, 6f);   // pencil dashes to your finger
            a.path = a.MakeLine("Path", Sketch.PaperC, 0.06f, 5f);      // paper-white dashed aim (K1)
            a.head = a.MakeLine("Head", Sketch.PaperC, 0.06f, 0f);
            a.head.textureMode = LineTextureMode.Stretch;
            a.head.material.mainTexture = null;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "GrabDot";
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(go.transform, false);
            dot.transform.localScale = new Vector3(0.11f, 0.03f, 0.11f);
            dot.GetComponent<MeshRenderer>().sharedMaterial = ArtLib.Lit(Pal.Yellow);
            a.grabDot = dot.transform;
            a.Hide();
            return a;
        }

        LineRenderer MakeLine(string n, Color c, float width, float tiling)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = width;
            lr.material = ArtLib.Line(c);
            lr.textureMode = LineTextureMode.Tile;
            lr.material.mainTextureScale = new Vector2(tiling, 1f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        public void Show(Vector3 grab, Vector3? finger, Vector3 dir, float power, float penHalfLength)
        {
            const float lift = 0.02f;
            grab.y = Mathf.Max(grab.y, 0f) + lift;
            grabDot.gameObject.SetActive(true);
            grabDot.position = grab + Vector3.up * 0.02f;

            if (finger.HasValue)
            {
                var f = finger.Value;
                f.y = lift;
                band.enabled = true;
                band.SetPosition(0, grab);
                band.SetPosition(1, f);
            }
            else band.enabled = false;

            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f || power <= 0.001f)
            {
                path.enabled = head.enabled = false;
                return;
            }
            dir.Normalize();
            float len = penHalfLength + 0.25f + power * 2.2f;
            var end = grab + dir * len;
            path.enabled = true;
            path.SetPosition(0, grab + dir * penHalfLength * 0.6f);
            path.SetPosition(1, end);

            // Arrow head as a short wide stroke.
            var side = Vector3.Cross(Vector3.up, dir) * 0.11f;
            head.enabled = true;
            head.positionCount = 3;
            head.SetPosition(0, end - dir * 0.15f + side);
            head.SetPosition(1, end);
            head.SetPosition(2, end - dir * 0.15f - side);

            var c = Color.Lerp(Color.white, Pal.Yellow, power);
            path.material.color = c;
            head.material.color = c;
        }

        public void Hide()
        {
            if (band) band.enabled = false;
            if (path) path.enabled = false;
            if (head) head.enabled = false;
            if (grabDot) grabDot.gameObject.SetActive(false);
        }
    }
}
