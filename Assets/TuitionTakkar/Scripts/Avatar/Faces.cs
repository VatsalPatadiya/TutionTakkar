using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Turns a face code into a picture and shows it. Layers are white masks (tinted with the chosen
    /// skin / hair / cloth / eye / background colour) or fixed-colour art, stacked in order.
    /// Faces are made one per frame (no stutter in lists) and cached.
    /// </summary>
    public static class Faces
    {
        // ---------- composing ----------

        static readonly Dictionary<string, Texture2D> faceCache = new Dictionary<string, Texture2D>();
        static readonly List<string> faceOrder = new List<string>();
        const int MaxFaces = 48;

        static readonly Dictionary<string, Color32[]> layerCache = new Dictionary<string, Color32[]>();
        static readonly List<string> layerOrder = new List<string>();
        const int MaxLayers = 60;

        static readonly Queue<(string code, Action<Texture2D> done)> queue = new Queue<(string, Action<Texture2D>)>();
        static readonly Dictionary<string, List<Action<Texture2D>>> waiting = new Dictionary<string, List<Action<Texture2D>>>();
        static IVisualElementScheduledItem pump;

        static Color32[] LayerPixels(string file, int size)
        {
            if (layerCache.TryGetValue(file, out var px)) return px;
            var ta = Resources.Load<TextAsset>("Avatar/" + file);
            if (ta == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ta.bytes);
            px = tex.width == size && tex.height == size ? tex.GetPixels32() : null;
            UnityEngine.Object.Destroy(tex);
            Resources.UnloadAsset(ta);
            if (px == null) return null;
            layerCache[file] = px;
            layerOrder.Add(file);
            if (layerOrder.Count > MaxLayers) { layerCache.Remove(layerOrder[0]); layerOrder.RemoveAt(0); }
            return px;
        }

        static Color32 Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? (Color32)c : new Color32(255, 0, 255, 255);

        static Color32 Mul(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);

        /// <summary>Builds the picture now (slow-ish: use <see cref="Get"/> from UI code).</summary>
        public static Texture2D Compose(string code)
        {
            if (faceCache.TryGetValue(code, out var cached) && cached != null) return cached;
            var f = AvatarParts.Parse(code);
            int n = AvatarParts.Size;
            var outPx = new Color32[n * n];
            if (f != null)
            {
                var skin = Hex(AvatarParts.Skin[f.Color("skin")]);
                var hair = Hex(AvatarParts.Hair[f.Color("hairc")]);
                var cloth = Hex(AvatarParts.Cloth[f.Color("cloth")]);
                var iris = Hex(AvatarParts.Iris[f.Color("iris")]);
                var back = Hex(AvatarParts.Back[f.Color("bgc")]);
                bool blackHair = f.Color("hairc") == 0;
                var hairShade = blackHair ? new Color32(0x3A, 0x3A, 0x3A, 255) : Mul(hair, 0.70f);
                var hat = AvatarParts.Get(f.PartId("hat"));
                bool hideHair = hat != null && hat.hidesHair;

                foreach (var layer in AvatarParts.Order)
                {
                    string id = layer == "hairback" ? (f.PartId("hair") ?? "").Replace("hair_", "hairback_") : f.PartId(layer);
                    var part = AvatarParts.Get(id);
                    if (part == null) continue;
                    if (hideHair && (layer == "hair" || layer == "hairback")) continue;
                    bool isBg = layer == "bg";
                    foreach (var lf in part.layers)
                    {
                        var px = LayerPixels(lf.f, n);
                        if (px == null) continue;
                        bool tinted = lf.t != "FIX";
                        Color32 tint = default;
                        if (tinted)
                        {
                            switch (lf.t)
                            {
                                case "SKIN": tint = skin; break;
                                case "SKIN_SHADE": tint = Mul(skin, 0.84f); break;
                                case "HAIR": tint = hair; break;
                                case "HAIR_SHADE": tint = hairShade; break;
                                case "CLOTH": tint = isBg ? back : cloth; break;
                                case "CLOTH_SHADE": tint = Mul(isBg ? back : cloth, 0.80f); break;
                                case "IRIS": tint = iris; break;
                                default: tint = new Color32(255, 255, 255, 255); break;
                            }
                        }
                        Blend(outPx, px, tinted, tint);
                    }
                }
            }
            // PNG rows start at the top; Unity textures start at the bottom. LoadImage already flips, so no flip needed.
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, name = "face" };
            tex.SetPixels32(outPx);
            tex.Apply(true, true);
            faceCache[code] = tex;
            faceOrder.Add(code);
            if (faceOrder.Count > MaxFaces)
            {
                var old = faceOrder[0];
                faceOrder.RemoveAt(0);
                if (faceCache.TryGetValue(old, out var t) && t != null) UnityEngine.Object.Destroy(t);
                faceCache.Remove(old);
            }
            return tex;
        }

        /// <summary>"src over dst", straight alpha. Tinted layers are white masks: colour = tint, alpha = mask.</summary>
        static void Blend(Color32[] dst, Color32[] src, bool tinted, Color32 tint)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                var s = src[i];
                int sa = s.a;
                if (sa == 0) continue;
                int sr = tinted ? tint.r : s.r, sg = tinted ? tint.g : s.g, sb = tinted ? tint.b : s.b;
                if (sa == 255) { dst[i] = new Color32((byte)sr, (byte)sg, (byte)sb, 255); continue; }
                var d = dst[i];
                int da = d.a * (255 - sa) / 255;
                int oa = sa + da;
                if (oa == 0) continue;
                dst[i] = new Color32((byte)((sr * sa + d.r * da) / oa), (byte)((sg * sa + d.g * da) / oa), (byte)((sb * sa + d.b * da) / oa), (byte)oa);
            }
        }

        /// <summary>Gives the picture now if cached, otherwise makes it on a later frame (one face per frame).</summary>
        public static void Get(VisualElement anyElement, string code, Action<Texture2D> done)
        {
            if (string.IsNullOrEmpty(code)) return;
            if (faceCache.TryGetValue(code, out var t) && t != null) { done(t); return; }
            if (waiting.TryGetValue(code, out var list)) { list.Add(done); return; }
            waiting[code] = new List<Action<Texture2D>> { done };
            queue.Enqueue((code, null));
            // Run on a layer that always stays on screen, so a closed list can't stall the queue.
            var host = GameManager.I != null && GameManager.I.UI != null ? GameManager.I.UI.Root : anyElement;
            if (pump == null && host != null)
                pump = host.schedule.Execute(Pump).Every(16);
        }

        static void Pump()
        {
            if (queue.Count == 0) { pump?.Pause(); pump = null; return; }
            var (code, _) = queue.Dequeue();
            Texture2D tex = null;
            try { tex = Compose(code); } catch (Exception e) { Debug.LogWarning("Face: " + e.Message); }
            if (waiting.TryGetValue(code, out var list))
            {
                waiting.Remove(code);
                if (tex != null) foreach (var cb in list) { try { cb(tex); } catch (Exception e) { Debug.LogWarning("Face cb: " + e.Message); } }
            }
        }

        // ---------- showing ----------

        /// <summary>
        /// A round avatar: the face for this code, or the old letter disc while it loads (or if there's no face).
        /// Same size and border as <see cref="UIKit.Avatar"/>, so it drops into the same places.
        /// </summary>
        public static VisualElement Avatar(string code, string initial, Color bg, float size = 64f)
        {
            var a = UIKit.Avatar(initial, bg, size);
            Fill(a, code);
            return a;
        }

        /// <summary>Puts the face for this code on an existing avatar disc (or replaces the old face).</summary>
        public static void Fill(VisualElement disc, string code)
        {
            if (disc == null || AvatarParts.Parse(code) == null) return;
            void Apply(Texture2D t)
            {
                disc.style.backgroundImage = new StyleBackground(t);
                disc.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                foreach (var c in disc.Children()) c.style.display = DisplayStyle.None;
            }
            if (faceCache.TryGetValue(code, out var tex) && tex != null) { Apply(tex); return; }
            if (disc.panel != null) Get(disc, code, Apply);
            else disc.RegisterCallback<AttachToPanelEvent>(_ => Get(disc, code, Apply));
        }

        static readonly Dictionary<string, string> playerFaces = new Dictionary<string, string>();

        /// <summary>
        /// Another player's face from their public profile (loaded once per session). Guests have no
        /// public profile, so they keep the letter disc.
        /// </summary>
        public static async void ForPlayer(VisualElement disc, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (playerId == OnlineServices.PlayerId && GameManager.I != null) { Fill(disc, AvatarParts.MyCode(GameManager.I.Save)); return; }
            if (!playerFaces.TryGetValue(playerId, out var code))
            {
                try { code = (await ProfileCloud.LoadAsync(playerId))?.face ?? ""; } catch (Exception) { code = ""; }
                playerFaces[playerId] = code;
            }
            Fill(disc, code);
        }

        /// <summary>My own face, on any avatar disc.</summary>
        public static VisualElement Mine(SaveData s, string initial, float size) => Avatar(AvatarParts.MyCode(s), initial, Pal.Yellow, size);
    }
}
