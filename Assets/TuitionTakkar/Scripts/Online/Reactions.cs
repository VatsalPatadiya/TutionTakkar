using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Quick reactions during an online match (no free chat): a "Say…" button above the
    /// power ruler opens four stickers. Everyone else sees a speech bubble with your name.
    /// </summary>
    public class Reactions
    {
        public static readonly string[] Codes = { "nice", "close", "oops", "gg" };
        public static readonly string[] Texts = { "Nice shot!", "So close!", "Oops", "GG" };
        const float Cooldown = 3f;

        public readonly VisualElement Root;
        readonly GameManager gm;
        readonly OnlineLink link;
        readonly VisualElement menu;
        readonly VisualElement bubbles;
        readonly Button sayBtn;
        float lastSent = -99f;

        public static string TextFor(string code)
        {
            int i = System.Array.IndexOf(Codes, code);
            return i >= 0 ? Texts[i] : "";
        }

        public Reactions(GameManager gm, OnlineLink link)
        {
            this.gm = gm;
            this.link = link;
            Root = UIKit.El("Reactions");
            UIKit.Fill(Root);
            Root.pickingMode = PickingMode.Ignore;

            // Incoming bubbles, just under the classroom wall.
            bubbles = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.left = 0; s.right = 0; s.top = gm.UI.Hud.TopClearance + 64f; s.alignItems = Align.Center;
            });
            bubbles.pickingMode = PickingMode.Ignore;
            Root.Add(bubbles);

            float bottom = gm.UI.Hud.BottomClearance - 6f;
            menu = UIKit.Col().St(s =>
            {
                s.position = Position.Absolute; s.right = 12; s.bottom = bottom + 52f; s.alignItems = Align.FlexEnd;
                s.display = DisplayStyle.None;
            });
            for (int i = 0; i < Codes.Length; i++)
            {
                string code = Codes[i];
                var chip = UIKit.Btn(Texts[i], () => Send(code), Pal.Paper, Pal.Ink, UIKit.BodyBold, 15f, 42f, 21f, null, Pal.Ink);
                chip.style.marginTop = 6;
                UIKit.Rot(chip, i % 2 == 0 ? -2f : 2f);
                menu.Add(chip);
            }
            Root.Add(menu);

            sayBtn = UIKit.Btn("Say…", Toggle, Pal.Ink, Color.white, UIKit.BodyBold, 14f, 40f, 20f, null, Color.white);
            sayBtn.style.position = Position.Absolute;
            sayBtn.style.right = 12;
            sayBtn.style.bottom = bottom;
            sayBtn.style.width = 76;
            Root.Add(sayBtn);

            link.Reacted += OnReacted;
            UIKit.PassThrough(bubbles);
            menu.pickingMode = PickingMode.Ignore; // only its chip buttons take taps
        }

        public void Detach()
        {
            link.Reacted -= OnReacted;
            Root.RemoveFromHierarchy();
        }

        void Toggle()
        {
            bool open = menu.style.display == DisplayStyle.None;
            menu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) UIRoot.PopIn(menu);
        }

        void Send(string code)
        {
            menu.style.display = DisplayStyle.None;
            if (Time.realtimeSinceStartup - lastSent < Cooldown) return;
            lastSent = Time.realtimeSinceStartup;
            link.SendReaction(code);
            Show("You", code, true);
            // Grey the button out while it cools down.
            sayBtn.SetEnabled(false);
            sayBtn.style.opacity = 0.5f;
            sayBtn.schedule.Execute(() => { sayBtn.SetEnabled(true); sayBtn.style.opacity = 1f; }).StartingIn((long)(Cooldown * 1000));
        }

        void OnReacted(int index, string code)
        {
            var setup = gm.Match.Setup;
            string name = setup != null && index < setup.names.Length ? setup.names[index] : "Player";
            Show(name, code, false);
        }

        void Show(string who, string code, bool mine)
        {
            string text = TextFor(code);
            if (string.IsNullOrEmpty(text)) return;
            var bubble = UIKit.Row().St(s =>
            {
                s.marginTop = 6; UIKit.Pad(s, 8, 14, 8, 14); UIKit.Radius(s, 18);
                s.backgroundColor = mine ? Pal.Yellow : Color.white; UIKit.Border(s, 2f, Pal.Ink);
            });
            bubble.pickingMode = PickingMode.Ignore;
            bubble.Add(UIKit.Strong(who + ": ", 14f, Pal.InkSoft));
            bubble.Add(UIKit.Strong(text, 16f, Pal.Ink));
            UIKit.Rot(bubble, Random.Range(-3f, 3f));
            UIKit.PassThrough(bubble);
            while (bubbles.childCount >= 3) bubbles.RemoveAt(0);
            bubbles.Add(bubble);
            UIRoot.PopIn(bubble);
            if (!mine) gm.Audio.Ui();
            bubble.schedule.Execute(() => bubble.RemoveFromHierarchy()).StartingIn(2600);
        }
    }
}
