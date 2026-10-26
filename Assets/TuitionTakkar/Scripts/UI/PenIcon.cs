using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>A flat pen drawing for menus: tip, grip, barrel, cap, clip.</summary>
    public static class PenIcon
    {
        public static VisualElement Build(PenDef d, float width, bool locked = false)
        {
            float h = Mathf.Max(8f, width * (d.shape == PenShape.Triangle ? 0.1f : 0.075f) * (d.radius / 0.0052f));
            Color body = locked ? Pal.Locked : d.color;
            Color cap = locked ? Pal.Locked : d.capColor;
            Color grip = locked ? Pal.Locked : d.gripColor;
            Color tip = locked ? Pal.Locked : d.tipColor;

            var root = UIKit.Row().St(s =>
            {
                s.width = width;
                s.height = h + 6f;
                s.alignItems = Align.Center;
                s.flexShrink = 0;
            });
            root.pickingMode = PickingMode.Ignore;

            float lengthK = d.length / 0.145f;
            float w = width * Mathf.Clamp(lengthK, 0.8f, 1f);

            root.Add(UIKit.El().St(s =>
            {
                s.width = w * 0.07f; s.height = h * 0.55f; s.backgroundColor = tip;
                s.borderTopLeftRadius = s.borderBottomLeftRadius = h;
            }));
            if (d.shape == PenShape.Round)
                root.Add(UIKit.El().St(s => { s.width = w * 0.17f; s.height = h * 0.95f; s.backgroundColor = grip; UIKit.Radius(s, 2f); }));
            var barrel = UIKit.El().St(s => { s.width = w * 0.58f; s.height = h; s.backgroundColor = body; UIKit.Radius(s, d.shape == PenShape.Triangle ? 1f : h * 0.3f); });
            if (!locked && (d.finish != PenFinish.Glossy || d.pattern != PenPattern.None))
            {
                // Same barrel picture as the 3D pen (pattern, glitter, holo...).
                barrel.style.backgroundImage = new StyleBackground(ArtLib.PenBarrel(d.color, d.finish, d.pattern));
                barrel.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                barrel.style.overflow = Overflow.Hidden;
            }
            if (!locked && (d.finish == PenFinish.Metal || d.finish == PenFinish.Gold))
            {
                // Metal: a bright band across the barrel.
                barrel.Add(UIKit.El().St(s =>
                {
                    s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = h * 0.12f; s.height = Mathf.Max(1.5f, h * 0.28f);
                    s.backgroundColor = new Color(1f, 1f, 1f, 0.45f);
                }));
            }
            root.Add(barrel);
            root.Add(UIKit.El().St(s => { s.width = w * 0.16f; s.height = h * 1.15f; s.backgroundColor = cap; UIKit.Radius(s, h * 0.35f); }));

            if (d.hasClip && !locked)
            {
                var clip = UIKit.El().St(s =>
                {
                    s.position = Position.Absolute;
                    s.left = w * 0.55f;
                    s.top = 0f;
                    s.width = w * 0.3f;
                    s.height = Mathf.Max(2f, h * 0.25f);
                    s.backgroundColor = Pal.Ink;
                    UIKit.Radius(s, 2f);
                });
                root.Add(clip);
            }
            // Shine strip.
            if (!locked)
            {
                root.Add(UIKit.El().St(s =>
                {
                    s.position = Position.Absolute;
                    s.left = w * 0.27f;
                    s.top = 3f + h * 0.18f;
                    s.width = w * 0.5f;
                    s.height = Mathf.Max(1f, h * 0.16f);
                    s.backgroundColor = new Color(1f, 1f, 1f, 0.35f);
                    UIKit.Radius(s, 1f);
                }));
            }
            return root;
        }
    }
}
