using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// One UI Toolkit panel with four layers, back to front:
    /// game input surface, HUD, full screens, overlays (round result, pause).
    /// Layout units are the 390 x 844 reference phone used in the design.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public VisualElement Root { get; private set; }
        public VisualElement GameLayer { get; private set; }
        public VisualElement HudLayer { get; private set; }
        public VisualElement ScreenLayer { get; private set; }
        public VisualElement OverlayLayer { get; private set; }
        public HudView Hud { get; private set; }

        PanelSettings panelSettings;

        public void Build(GameManager gm)
        {
            UIKit.LoadFonts();

            // Instantiated from a project asset (Assets/Resources/TT/UI/TTPanel.asset,
            // created by ProjectSetup) rather than ScriptableObject.CreateInstance, so it
            // carries the ICU text-shaping data Unity only assigns to PanelSettings assets
            // present at build time.
            var source = Resources.Load<PanelSettings>("TT/UI/TTPanel");
            panelSettings = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "TT Panel";

            var go = new GameObject("UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            go.SetActive(true);

            Root = doc.rootVisualElement;
            Root.style.flexGrow = 1;
            UIKit.Fill(Root);
            Root.style.unityFontDefinition = FontDefinition.FromFont(UIKit.Body);
            Root.pickingMode = PickingMode.Ignore;

            GameLayer = Layer("GameLayer", PickingMode.Position);
            HudLayer = Layer("HudLayer", PickingMode.Ignore);
            ScreenLayer = Layer("ScreenLayer", PickingMode.Ignore);
            OverlayLayer = Layer("OverlayLayer", PickingMode.Ignore);

            Hud = new HudView(gm);
            HudLayer.Add(Hud.Root);
            Hud.Root.style.display = DisplayStyle.None;

            this.gm = gm;
            Root.RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
        }

        GameManager gm;
        Rect lastSafe;
        Vector2Int lastScreen;

        /// <summary>
        /// Keeps the HUD clear of notches and tells the camera exactly where the
        /// free space between the top HUD and the bottom bar is.
        /// </summary>
        void ApplyLayout()
        {
            var size = Root.layout.size;
            if (size.x <= 1f || size.y <= 1f || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            float k = size.y / Screen.height; // screen px -> panel units
            float topInset = Mathf.Max(0f, (Screen.height - Screen.safeArea.yMax) * k);
            float bottomInset = Mathf.Max(0f, Screen.safeArea.yMin * k);
            Hud.ApplyInsets(topInset, bottomInset);

            float top = Hud.TopClearance;       // panel units covered at the top
            float bottom = Hud.BottomClearance; // panel units covered at the bottom
            const float side = 22f; // a strip of floor either side of the desk (reference look)
            var viewport = new Rect(side / size.x, bottom / size.y, 1f - 2f * side / size.x, 1f - (top + bottom) / size.y);
            if (gm != null && gm.World != null) gm.Rig.SetSafeArea(viewport);
        }

        VisualElement Layer(string n, PickingMode mode)
        {
            var e = new VisualElement { name = n, pickingMode = mode };
            UIKit.Fill(e);
            Root.Add(e);
            return e;
        }

        void Update()
        {
            if (panelSettings == null) return;
            // Portrait phones match width; squat windows (editor, tablets) match height.
            float aspect = Screen.height / (float)Mathf.Max(1, Screen.width);
            panelSettings.match = aspect >= 1.75f ? 0f : 1f;

            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
                ApplyLayout();
        }

        public void ShowScreen(VisualElement screen)
        {
            // Rebuilding the screen already showing (pouch tabs, shop filters): swap it in place.
            // Fading it in from transparent let the light 3D room behind flash through.
            bool same = screen != null && !string.IsNullOrEmpty(screen.name) && ScreenLayer.childCount > 0
                        && ScreenLayer[ScreenLayer.childCount - 1].name == screen.name;
            OverlayLayer.Clear();
            Hud.Root.style.display = DisplayStyle.None;
            if (screen == null || same || ScreenLayer.childCount == 0)
            {
                ScreenLayer.Clear();
                if (screen == null) return;
                UIKit.Fill(screen);
                ScreenLayer.Add(screen);
                if (!same) FadeIn(screen);
                return;
            }
            // A different screen: fade it in over the old one (not over the empty room), then drop the old one.
            var old = new List<VisualElement>(ScreenLayer.Children());
            foreach (var o in old) o.pickingMode = PickingMode.Ignore;
            UIKit.Fill(screen);
            ScreenLayer.Add(screen);
            FadeIn(screen);
            screen.schedule.Execute(() => { foreach (var o in old) o.RemoveFromHierarchy(); }).StartingIn(200);
        }

        public void ShowHud()
        {
            OverlayLayer.Clear();
            ScreenLayer.Clear();
            Hud.Root.style.display = DisplayStyle.Flex;
        }

        public void ShowOverlay(VisualElement overlay)
        {
            OverlayLayer.Clear();
            UIKit.Fill(overlay);
            OverlayLayer.Add(overlay);
        }

        public void HideOverlay() => OverlayLayer.Clear();

        public static void FadeIn(VisualElement e, int ms = 160)
        {
            e.style.opacity = 0f;
            e.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            e.style.transitionDuration = new List<TimeValue> { new TimeValue(ms, TimeUnit.Millisecond) };
            e.schedule.Execute(() => e.style.opacity = 1f).StartingIn(16);
        }

        /// <summary>Card pops in with a slight overshoot.</summary>
        public static void PopIn(VisualElement e, int delayMs = 0)
        {
            e.style.opacity = 0f;
            e.style.scale = new Scale(new Vector3(0.86f, 0.86f, 1f));
            e.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity"), new StylePropertyName("scale") };
            e.style.transitionDuration = new List<TimeValue> { new TimeValue(180, TimeUnit.Millisecond), new TimeValue(260, TimeUnit.Millisecond) };
            e.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOutBack) };
            e.schedule.Execute(() =>
            {
                e.style.opacity = 1f;
                e.style.scale = new Scale(Vector3.one);
            }).StartingIn(16 + delayMs);
        }
    }
}
