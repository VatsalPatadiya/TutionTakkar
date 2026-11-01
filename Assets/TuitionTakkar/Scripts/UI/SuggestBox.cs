using System;
using System.Collections.Generic;
using Unity.Services.CloudCode;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// "Send us an idea" (Settings), for signed-in players only. Pick idea / bug / pen idea, write up
    /// to 300 letters, send. Saved online by the send_suggestion Cloud Code script (max 3 a day),
    /// readable in the Unity dashboard under Cloud Save > Custom items > suggestions_YYYY_MM.
    /// </summary>
    public static class SuggestBox
    {
        public const int MaxLength = 300;
        static readonly string[] Kinds = { "idea", "bug", "pen" };
        static readonly string[] KindNames = { "Idea", "Bug", "Pen idea" };

        [Serializable] class Result { public bool ok; public int left; }

        public static bool Allowed(SaveData s) => OnlineServices.OnLeaderboard(s); // signed in with Google (Apple later)

        /// <summary>The Settings row: opens the box, or says to sign in first.</summary>
        public static VisualElement SettingsRow(GameManager gm)
        {
            bool ok = Allowed(gm.Save);
            var b = UIKit.Btn(ok ? "Send us an idea" : "Sign in to send ideas", () =>
            {
                if (ok) gm.UI.ShowOverlay(Box(gm));
                else { gm.UI.HideOverlay(); OnlineScreens.Enter(gm); }
            }, Color.white, Pal.Ink, UIKit.BodyBold, 16f, 54f, 12f, null, Pal.Dash);
            b.style.marginTop = 10;
            if (!ok) b.style.color = Pal.InkSoft;
            return b;
        }

        static VisualElement Box(GameManager gm, int kind = 0, string draft = "")
        {
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp("SEND US AN IDEA", 28f, Pal.Ink));
            c.Add(UIKit.HandText("we read every one. thank you!", 17f, Pal.InkSoft));

            var chips = UIKit.Row().St(s => s.marginTop = 12);
            var chipBtns = new List<Button>();
            void PaintChips()
            {
                for (int i = 0; i < chipBtns.Count; i++)
                {
                    bool on = i == kind;
                    chipBtns[i].style.backgroundColor = on ? Pal.Ink : Color.white;
                    chipBtns[i].style.color = on ? Color.white : Pal.Ink;
                }
            }
            for (int i = 0; i < Kinds.Length; i++)
            {
                int ii = i;
                var chip = UIKit.Btn(KindNames[i], () => { kind = ii; PaintChips(); }, Color.white, Pal.Ink, UIKit.BodyBold, 14f, 40f, 20f, null, Pal.Ink);
                chip.style.marginRight = 8; UIKit.Pad(chip.style, 0, 14, 0, 14);
                chipBtns.Add(chip);
                chips.Add(chip);
            }
            PaintChips();
            c.Add(chips);

            var field = new TextField { maxLength = MaxLength, multiline = true, value = draft ?? "" };
            field.style.marginTop = 12; field.style.marginLeft = field.style.marginRight = 0;
            field.style.unityFontDefinition = FontDefinition.FromFont(UIKit.BodyBold);
            field.style.fontSize = 17;
            field.textEdition.placeholder = "what would make the game better?";
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Color.white; input.style.minHeight = 120; input.style.color = Pal.Ink;
                UIKit.Pad(input.style, 10, 12, 10, 12); UIKit.Radius(input.style, 12); UIKit.Border(input.style, 2f, Pal.Ink);
                input.style.unityTextAlign = TextAnchor.UpperLeft; input.style.whiteSpace = WhiteSpace.Normal;
            }
            c.Add(field);
            var count = UIKit.Plain("0 / " + MaxLength, 12f, Pal.InkSoft, TextAnchor.MiddleRight).St(s => s.marginTop = 4);
            c.Add(count);

            var send = UIKit.PrimaryBtn("SEND", null, 54f);
            send.style.marginTop = 10;
            void PaintSend()
            {
                int n = (field.value ?? "").Trim().Length;
                count.text = n + " / " + MaxLength;
                send.SetEnabled(n >= 3);
                send.style.opacity = n >= 3 ? 1f : 0.5f;
            }
            field.RegisterValueChangedCallback(_ => PaintSend());
            PaintSend();
            send.clicked += () => Send(gm, Kinds[kind], kind, field.value);
            c.Add(send);
            c.Add(UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 44f, 12f).St(s => s.marginTop = 4));
            return o;
        }

        static async void Send(GameManager gm, string kind, int kindIndex, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length < 3) return;
            if (!NetPopups.IsOnline) { gm.UI.ShowOverlay(NetPopups.NoInternet(gm)); return; }
            gm.UI.ShowOverlay(OnlineScreens.Busy("sending…"));
            try
            {
                await OnlineServices.InitAsync();
                var r = await CloudCodeService.Instance.CallEndpointAsync<Result>("send_suggestion", new Dictionary<string, object>
                {
                    { "text", text }, { "kind", kind }, { "name", OnlineServices.DisplayName(gm.Save) },
                    { "version", Application.version }, { "platform", Application.platform.ToString() },
                });
                if (r == null || !r.ok)
                {
                    gm.UI.ShowOverlay(OnlineScreens.Error(gm, "THAT'S 3 FOR TODAY", "thanks for all the ideas! you can send more tomorrow."));
                    return;
                }
                gm.Audio.Stamp();
                Haptics.Win();
                gm.UI.ShowOverlay(OnlineScreens.Error(gm, "THANK YOU!", "got it. we read every idea" + (r.left > 0 ? " (you can send " + r.left + " more today)." : ".")));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Suggestion: " + e);
                var sheet = OnlineScreens.Error(gm, "DIDN'T SEND", "check your internet and try again. your message is kept.");
                // Keep what they wrote: OK goes back to the box with the text still in it.
                var ok = sheet.Q<Button>();
                if (ok != null) { ok.clickable = new Clickable(() => gm.UI.ShowOverlay(Box(gm, kindIndex, text))); ok.text = "BACK"; }
                gm.UI.ShowOverlay(sheet);
            }
        }
    }
}
