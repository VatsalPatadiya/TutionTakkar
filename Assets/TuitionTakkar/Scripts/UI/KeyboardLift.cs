using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Phone keyboards cover the bottom of the screen and UI Toolkit doesn't move anything out of
    /// the way. While a text box has focus and the keyboard is up, this slides the layer holding
    /// it (a bottom sheet or a screen) up just enough that the box, and the sheet's buttons,
    /// sit above the keyboard. Works for every TextField in the game, nothing to wire per screen.
    /// </summary>
    public static class KeyboardLift
    {
        static VisualElement root, lifted;
        static float current;

        public static void Attach(VisualElement uiRoot)
        {
            root = uiRoot;
            root.schedule.Execute(Tick).Every(16);
        }

        static void Tick()
        {
            if (root == null || root.panel == null) return;
            float panelH = root.layout.height;
            if (float.IsNaN(panelH) || panelH < 10f) return;

            var focused = root.panel.focusController?.focusedElement as VisualElement;
            bool typing = focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
            float kbH = 0f;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (typing && TouchScreenKeyboard.visible)
            {
                float px = TouchScreenKeyboard.area.height;
                kbH = px > 1f ? px / Screen.height * panelH : panelH * 0.42f;
            }
#endif
            float target = 0f;
            VisualElement layer = null;
            if (kbH > 0f)
            {
                layer = LayerOf(focused);
                var box = focused.GetFirstAncestorOfType<TextField>() ?? focused;
                var b = box.worldBound;
                float visibleBottom = panelH - kbH - 12f;
                // Bottom sheet: lift the whole card so its buttons show too, as long as the box stays on screen.
                var card = SheetCard(box);
                float want = card != null ? card.worldBound.yMax - visibleBottom : b.yMax - visibleBottom;
                float maxLift = Mathf.Max(0f, b.yMin + current - 16f); // never push the box off the top
                target = Mathf.Clamp(want + current, 0f, maxLift);
            }

            if (layer != lifted && lifted != null)
            {
                lifted.style.translate = new Translate(0, 0);
                current = 0f;
            }
            if (layer != null) lifted = layer;
            current = Mathf.Lerp(current, target, 0.35f);
            if (Mathf.Abs(current - target) < 0.5f) current = target;
            if (lifted != null) lifted.style.translate = new Translate(0, -current);
            if (current == 0f && target == 0f) lifted = null;
        }

        /// <summary>The direct child of the UI root (a layer) that holds this element.</summary>
        static VisualElement LayerOf(VisualElement e)
        {
            while (e != null && e.parent != root) e = e.parent;
            return e;
        }

        /// <summary>The bottom-sheet card around this element, if it's in one.</summary>
        static VisualElement SheetCard(VisualElement e)
        {
            while (e != null && e.parent != null)
            {
                if (e.parent.ClassListContains(UIRoot.SheetClass)) return e;
                e = e.parent;
            }
            return null;
        }
    }
}
