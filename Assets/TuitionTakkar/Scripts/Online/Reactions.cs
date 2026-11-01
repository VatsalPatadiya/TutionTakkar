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
        /// <summary>Emoji stickers (drawn, not system emojis). Older apps don't know these codes and simply ignore them.</summary>
        public static readonly string[] EmojiCodes = { "e_laugh", "e_wow", "e_cry", "e_angry", "e_thumb", "e_fire" };
        static readonly DoodleIcon.Kind[] EmojiKinds =
            { DoodleIcon.Kind.Laugh, DoodleIcon.Kind.Wow, DoodleIcon.Kind.Cry, DoodleIcon.Kind.Angry, DoodleIcon.Kind.ThumbsUp, DoodleIcon.Kind.Fire };
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
            // Emoji stickers: two rows of three, above the words.
            var grid = UIKit.Col().St(s => { s.backgroundColor = Pal.Paper; UIKit.Border(s, 2f, Pal.Ink); UIKit.Radius(s, 18); UIKit.Pad(s, 6); });
            for (int row = 0; row < 2; row++)
            {
                var r = UIKit.Row();
                for (int k = 0; k < 3; k++)
                {
                    int idx = row * 3 + k;
                    string code = EmojiCodes[idx];
                    var b = UIKit.Btn("", () => Send(code), Color.clear, Pal.Ink, UIKit.BodyBold, 10f, 48f, 12f);
                    b.style.width = 48; UIKit.Pad(b.style, 3);
                    b.Add(new DoodleIcon(EmojiKinds[idx], 42f));
                    r.Add(b);
                }
                grid.Add(r);
            }
            menu.Add(grid);
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
            int emoji = System.Array.IndexOf(EmojiCodes, code);
            string text = TextFor(code);
            if (string.IsNullOrEmpty(text) && emoji < 0) return;
            var bubble = UIKit.Row().St(s =>
            {
                s.marginTop = 6; UIKit.Pad(s, 8, 14, 8, 14); UIKit.Radius(s, 18);
                s.backgroundColor = mine ? Pal.Yellow : Color.white; UIKit.Border(s, 2f, Pal.Ink);
            });
            bubble.pickingMode = PickingMode.Ignore;
            bubble.Add(UIKit.Strong(who + ": ", 14f, Pal.InkSoft));
            bubble.style.alignItems = Align.Center;
            if (emoji >= 0)
            {
                var icon = new DoodleIcon(EmojiKinds[emoji], 56f);
                bubble.Add(icon);
                // A little bounce on top of the pop-in.
                icon.schedule.Execute(() => icon.style.scale = new Scale(new Vector3(1.25f, 1.25f, 1f))).StartingIn(120);
                icon.schedule.Execute(() => icon.style.scale = new Scale(Vector3.one)).StartingIn(260);
                icon.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("scale") };
                icon.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0.14f, TimeUnit.Second) };
            }
            else bubble.Add(UIKit.Strong(text, 16f, Pal.Ink));
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
