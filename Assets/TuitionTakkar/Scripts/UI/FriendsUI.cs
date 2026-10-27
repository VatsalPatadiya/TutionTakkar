using System;
using System.Collections.Generic;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace TuitionTakkar
{
    /// <summary>
    /// Friends screens (mockups F1–F5, R1–R3): friends list, friend card, requests, add by player
    /// ID, the locked screen for guests, the room's invite sheet and the incoming invite pop-up.
    /// </summary>
    public static class FriendsUI
    {
        static readonly Color Green = Pal.H("#1E6B4F"), Red = Pal.H("#B23A26"), Soft = Pal.H("#5B6B78");
        static readonly Color Online = Pal.H("#2EB872"), Playing = Pal.H("#F2A23A"), Offline = Pal.H("#9AA5B1");
        static int tab;

        // =====================================================================
        // entry
        // =====================================================================

        public static async void Show(GameManager gm, int startTab = -1)
        {
            if (startTab >= 0) tab = startTab;
            if (!Friends.Allowed(gm.Save)) { gm.UI.ShowScreen(Locked(gm)); return; }
            if (!Friends.Ready)
            {
                gm.UI.ShowOverlay(OnlineScreens.Busy("loading friends…"));
                try { await Friends.InitAsync(gm); }
                catch (Exception e)
                {
                    gm.UI.ShowOverlay(OnlineScreens.Error(gm, "CAN'T LOAD FRIENDS", OnlineServices.Explain(e)));
                    return;
                }
                gm.UI.HideOverlay();
            }
            gm.UI.ShowScreen(Screen(gm));
        }

        static VisualElement Screen(GameManager gm)
        {
            var root = OnlineScreens.Screen();
            root.Add(OnlineScreens.Header("FRIENDS", "sign-in players only", () => OnlineScreens.ShowPlay(gm)));
            var tabs = UIKit.Row(Align.Stretch).St(s =>
            {
                s.marginLeft = 16; s.marginRight = 16; s.marginTop = 12; s.backgroundColor = Pal.Night; UIKit.Radius(s, 14); UIKit.Pad(s, 4); s.flexShrink = 0;
            });
            root.Add(tabs);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var body = UIKit.Col().St(s => s.paddingBottom = 26);
            scroll.Add(body);
            root.Add(scroll);

            void Paint()
            {
                tabs.Clear();
                string[] names = { "Friends", "Requests", "Add friend" };
                for (int i = 0; i < 3; i++)
                {
                    int k = i;
                    bool on = i == tab;
                    var t = UIKit.Row().St(s =>
                    {
                        s.flexGrow = 1; s.flexBasis = 0; s.justifyContent = Justify.Center; s.height = 38; UIKit.Radius(s, 11);
                        s.backgroundColor = on ? Pal.Paper : Color.clear;
                    });
                    t.Add(UIKit.Strong(names[i], 14f, on ? Pal.Ink : Pal.H("#DDE9F5")));
                    if (i == 0) t.Add(UIKit.Strong(Friends.List.Count + "/" + Friends.MaxFriends, 11f, on ? Pal.InkSoft : Pal.H("#8FA3AE")).St(s => s.marginLeft = 4));
                    if (i == 1 && Friends.Incoming.Count > 0)
                        t.Add(UIKit.Strong(Friends.Incoming.Count.ToString(), 11f, Color.white, TextAnchor.MiddleCenter).St(s =>
                        {
                            s.marginLeft = 4; s.backgroundColor = Red; UIKit.Radius(s, 9); UIKit.Pad(s, 0, 6, 0, 6);
                        }));
                    t.RegisterCallback<ClickEvent>(_ => { if (tab == k) return; gm.Audio.Ui(); tab = k; Paint(); });
                    tabs.Add(t);
                }
                body.Clear();
                if (tab == 0) PaintFriends(gm, body);
                else if (tab == 1) PaintRequests(gm, body);
                else PaintAdd(gm, body);
            }

            Action changed = () => { if (root.panel != null && !typing) Paint(); };
            Friends.Changed += changed;
            root.RegisterCallback<DetachFromPanelEvent>(_ => Friends.Changed -= changed);
            Paint();
            return root;
        }

        static bool typing;

        static VisualElement Head(string text, string right = null)
        {
            var r = UIKit.Row().St(s => { s.justifyContent = Justify.SpaceBetween; s.marginTop = 16; s.marginLeft = 18; s.marginRight = 18; s.marginBottom = 6; });
            r.Add(UIKit.Strong(text, 12f, Pal.BenchText).St(s => s.letterSpacing = 0.6f));
            if (right != null) r.Add(UIKit.Strong(right, 12f, Pal.BenchText));
            return r;
        }

        static VisualElement AvatarStatus(string name, string playerId, Friends.State? st, float size = 42f)
        {
            var holder = UIKit.El().St(s => { s.width = size; s.height = size; s.flexShrink = 0; });
            holder.Add(UIKit.Avatar(ProfileScreens.Initial(name), OnlineScreens.AvatarColor(playerId), size));
            if (st.HasValue)
                holder.Add(UIKit.El().St(s =>
                {
                    s.position = Position.Absolute; s.right = -1; s.bottom = -1; s.width = 13; s.height = 13; UIKit.Radius(s, 7);
                    s.backgroundColor = st == Friends.State.Online ? Online : st == Friends.State.Playing ? Playing : Offline;
                    UIKit.Border(s, 2.5f, Pal.Paper);
                }));
            return holder;
        }

        static VisualElement LevelChip(int lv) => lv <= 0 ? UIKit.El() :
            UIKit.Strong("Lv " + lv, 10f, Color.white, TextAnchor.MiddleCenter).St(s => { s.marginLeft = 6; s.backgroundColor = Pal.Pouch; UIKit.Radius(s, 8); UIKit.Pad(s, 1, 6, 1, 6); });

        static string StatusText(Relationship r, out Color c)
        {
            var st = Friends.StateOf(r);
            var a = Friends.ActivityOf(r);
            if (st == Friends.State.Playing) { c = Pal.H("#C25A10"); return "in a match" + (string.IsNullOrEmpty(a.desk) ? "" : " · " + a.desk); }
            if (st == Friends.State.Online) { c = Green; return "online"; }
            c = Soft;
            var seen = Friends.LastSeen(r);
            if (seen <= DateTime.MinValue.AddDays(1)) return "offline";
            return "last seen " + StatsRecorder.Ago(new DateTimeOffset(seen.ToUniversalTime()).ToUnixTimeSeconds());
        }

        static VisualElement FriendRow(Relationship r, VisualElement action)
        {
            var row = UIKit.Row().St(s => { s.backgroundColor = Pal.Paper; UIKit.Radius(s, 14); UIKit.Pad(s, 10, 12, 10, 12); s.marginTop = 8; s.marginLeft = 16; s.marginRight = 16; });
            string name = Friends.NameOf(r);
            row.Add(AvatarStatus(name, r.Member.Id, Friends.StateOf(r)));
            var c = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; s.marginLeft = 12; });
            var nr = UIKit.Row();
            nr.Add(UIKit.Strong(name, 15f, Pal.Ink));
            nr.Add(LevelChip(Friends.ActivityOf(r).level));
            c.Add(nr);
            c.Add(UIKit.Strong(StatusText(r, out var col), 12f, col));
            row.Add(c);
            if (action != null) row.Add(action);
            return row;
        }

        // =====================================================================
        // F1 friends + F2 card
        // =====================================================================

        static void PaintFriends(GameManager gm, VisualElement body)
        {
            var list = new List<Relationship>(Friends.List);
            list.Sort((a, b) =>
            {
                int sa = (int)Friends.StateOf(a), sb = (int)Friends.StateOf(b);
                if (sa != sb) return sa.CompareTo(sb);
                return Friends.LastSeen(b).CompareTo(Friends.LastSeen(a));
            });
            int on = 0;
            foreach (var r in list) if (Friends.StateOf(r) != Friends.State.Offline) on++;
            if (list.Count == 0)
            {
                body.Add(UIKit.HandText("no friends yet. add a classmate by their player ID, or tap a player's name in a match.", 18f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => { s.marginTop = 40; s.marginLeft = 30; s.marginRight = 30; }));
                body.Add(UIKit.PrimaryBtn("ADD A FRIEND", () => { tab = 2; Show(gm); }).St(s => { s.marginTop = 20; s.marginLeft = 40; s.marginRight = 40; }));
                return;
            }
            body.Add(Head("ONLINE · " + on, list.Count + " of " + Friends.MaxFriends + " friends"));
            bool offHead = false;
            foreach (var r in list)
            {
                if (!offHead && Friends.StateOf(r) == Friends.State.Offline)
                {
                    body.Add(Head("OFFLINE · " + (list.Count - on)));
                    offHead = true;
                }
                var rr = r;
                var dots = UIKit.Strong("•••", 16f, Pal.InkSoft);
                var row = FriendRow(r, dots);
                row.RegisterCallback<ClickEvent>(_ => { gm.Audio.Ui(); gm.UI.ShowOverlay(Card(gm, rr)); });
                body.Add(row);
            }
        }

        static VisualElement Card(GameManager gm, Relationship r)
        {
            var o = OnlineScreens.Sheet(out var c);
            string name = Friends.NameOf(r);
            var a = Friends.ActivityOf(r);
            var top = UIKit.Row();
            top.Add(AvatarStatus(name, r.Member.Id, Friends.StateOf(r), 60f));
            var who = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
            who.Add(UIKit.Disp(name.ToUpperInvariant(), 24f, Pal.Ink));
            var chips = UIKit.Row().St(s => s.marginTop = 6);
            if (!string.IsNullOrEmpty(a.friendId)) chips.Add(ProfileScreens.IdChip(a.friendId, false));
            chips.Add(LevelChip(a.level));
            who.Add(chips);
            who.Add(UIKit.Strong("● " + StatusText(r, out var col), 12f, col).St(s => s.marginTop = 5));
            top.Add(who);
            c.Add(top);

            var room = OnlineRoom.Current;
            if (room != null && !room.Closed && !room.IsQuick && !room.Playing)
            {
                var inv = UIKit.PrimaryBtn("INVITE TO MY ROOM", null, 56f);
                inv.clicked += async () =>
                {
                    inv.SetEnabled(false);
                    string err = await Friends.InviteAsync(gm, r, room);
                    inv.text = err == null ? "INVITED ✓" : "TRY AGAIN";
                    inv.SetEnabled(err != null);
                    if (err != null) c.Add(UIKit.Strong(err, 12f, Red, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
                };
                inv.style.marginTop = 16;
                c.Add(inv);
            }
            var row = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 12; s.justifyContent = Justify.SpaceBetween; });
            var remove = UIKit.Btn("Remove friend", null, Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 48f, 14f, null, Pal.Ink);
            remove.style.width = Length.Percent(48);
            remove.clicked += () => gm.UI.ShowOverlay(Confirm(gm, "REMOVE " + name.ToUpperInvariant() + "?", "you'll stop seeing each other in your friend lists.", "REMOVE",
                async () => { await Friends.RemoveAsync(r.Member.Id); }));
            var block = UIKit.Btn("Block", null, Color.clear, Red, UIKit.BodyBold, 15f, 48f, 14f, null, Red);
            block.style.width = Length.Percent(48);
            block.clicked += () => gm.UI.ShowOverlay(Confirm(gm, "BLOCK " + name.ToUpperInvariant() + "?", "they'll be removed and can't send you requests.", "BLOCK",
                async () => { string e = await Friends.BlockAsync(r.Member.Id); if (e != null) gm.UI.ShowOverlay(OnlineScreens.Error(gm, "COULDN'T BLOCK", e)); }));
            row.Add(remove, block);
            c.Add(row);
            c.Add(UIKit.Strong("friends see this small card only. full profiles show to players in the same match.", 11f, Soft, TextAnchor.MiddleCenter).St(s => { s.marginTop = 12; s.whiteSpace = WhiteSpace.Normal; }));
            c.Add(UIKit.Btn("Close", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 44f, 12f).St(s => s.marginTop = 4));
            return o;
        }

        static VisualElement Confirm(GameManager gm, string title, string text, string yes, Func<System.Threading.Tasks.Task> act)
        {
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp(title, 26f, Pal.Ink));
            c.Add(UIKit.HandText(text, 17f, Pal.InkSoft).St(s => s.marginTop = 4));
            var go = UIKit.Btn(yes, null, Red, Color.white, UIKit.Display, 20f, 54f, 16f, Pal.RedDark);
            go.style.marginTop = 16;
            go.clicked += async () => { go.SetEnabled(false); await act(); gm.UI.HideOverlay(); };
            c.Add(go);
            c.Add(UIKit.Btn("Cancel", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f).St(s => s.marginTop = 6));
            return o;
        }

        // =====================================================================
        // F3 requests
        // =====================================================================

        static void PaintRequests(GameManager gm, VisualElement body)
        {
            body.Add(Head("WANT TO BE YOUR FRIEND · " + Friends.Incoming.Count));
            if (Friends.Incoming.Count == 0)
                body.Add(UIKit.HandText("no new requests.", 17f, Pal.BenchText).St(s => s.marginLeft = 18));
            foreach (var r in Friends.Incoming)
            {
                var rr = r;
                var actions = UIKit.Row();
                var accept = UIKit.Btn("Accept", null, Green, Color.white, UIKit.BodyBold, 13f, 36f, 11f);
                accept.clicked += async () =>
                {
                    accept.SetEnabled(false);
                    string e = await Friends.AcceptAsync(rr.Member.Id);
                    if (e != null) gm.UI.ShowOverlay(OnlineScreens.Error(gm, "COULDN'T ACCEPT", e));
                };
                var decline = UIKit.Btn("✕", null, Color.clear, Soft, UIKit.BodyBold, 15f, 36f, 11f, null, Pal.H("#C9D1D9"));
                decline.style.width = 36; decline.style.marginLeft = 6; UIKit.Pad(decline.style, 0);
                decline.clicked += async () => { decline.SetEnabled(false); await Friends.DeclineAsync(rr.Member.Id); };
                actions.Add(accept, decline);
                body.Add(RequestRow(r, actions));
            }
            body.Add(Head("YOU SENT · " + Friends.Outgoing.Count, Friends.Outgoing.Count + " of " + Friends.MaxPending + " waiting"));
            foreach (var r in Friends.Outgoing)
            {
                var rr = r;
                var cancel = UIKit.Btn("Cancel", null, Color.clear, Pal.Ink, UIKit.BodyBold, 13f, 34f, 11f, null, Pal.Ink);
                cancel.clicked += async () => { cancel.SetEnabled(false); await Friends.CancelAsync(rr.Member.Id); };
                body.Add(RequestRow(r, cancel, true));
            }
            body.Add(UIKit.Strong("you're not friends until they accept. you can have up to " + Friends.MaxPending + " requests waiting at once.", 12f, Pal.H("#6B5216")).St(s =>
            {
                s.marginTop = 16; s.marginLeft = 16; s.marginRight = 16; s.backgroundColor = Pal.H("#FFF4C2"); UIKit.Radius(s, 12); UIKit.Pad(s, 10, 12, 10, 12); s.whiteSpace = WhiteSpace.Normal;
            }));
        }

        static VisualElement RequestRow(Relationship r, VisualElement action, bool sent = false)
        {
            var row = UIKit.Row().St(s =>
            {
                s.backgroundColor = sent ? new Color(1, 0.99f, 0.96f, 0.75f) : Pal.Paper; UIKit.Radius(s, 14); UIKit.Pad(s, 10, 12, 10, 12);
                s.marginTop = 8; s.marginLeft = 16; s.marginRight = 16;
            });
            string name = Friends.NameOf(r);
            row.Add(AvatarStatus(name, r.Member.Id, null));
            var c = UIKit.Col().St(s => { s.flexGrow = 1; s.flexShrink = 1; s.marginLeft = 12; });
            var nr = UIKit.Row();
            nr.Add(UIKit.Strong(name, 15f, Pal.Ink));
            nr.Add(LevelChip(Friends.ActivityOf(r).level));
            c.Add(nr);
            var a = Friends.ActivityOf(r);
            c.Add(UIKit.Strong((string.IsNullOrEmpty(a.friendId) ? "" : a.friendId + " · ") + (sent ? "waiting" : "wants to be friends"), 11f, Soft));
            row.Add(c);
            row.Add(action);
            return row;
        }

        // =====================================================================
        // F4 add by player ID
        // =====================================================================

        static void PaintAdd(GameManager gm, VisualElement body)
        {
            var save = gm.Save;
            var idCard = UIKit.Col().St(s => { s.marginTop = 14; s.marginLeft = 16; s.marginRight = 16; s.backgroundColor = Pal.Night; UIKit.Radius(s, 16); UIKit.Pad(s, 14); });
            idCard.Add(UIKit.Strong("YOUR PLAYER ID · NEVER CHANGES", 11f, Pal.Mist).St(s => s.letterSpacing = 0.8f));
            var idRow = UIKit.Row().St(s => { s.marginTop = 8; s.justifyContent = Justify.SpaceBetween; });
            string myId = string.IsNullOrEmpty(save.friendId) ? "TT-······" : save.friendId;
            idRow.Add(UIKit.Disp(myId, 24f, Pal.Yellow).St(s => s.letterSpacing = 2f));
            var acts = UIKit.Row();
            var copy = UIKit.Btn("Copy", null, Pal.NightSoft, Color.white, UIKit.BodyBold, 13f, 36f, 11f);
            copy.clicked += () => { if (!string.IsNullOrEmpty(save.friendId)) { GUIUtility.systemCopyBuffer = save.friendId; copy.text = "Copied!"; } };
            var share = UIKit.Btn("Share", null, Pal.Yellow, Pal.Ink, UIKit.BodyBold, 13f, 36f, 11f);
            share.style.marginLeft = 8;
            share.clicked += () =>
            {
                if (string.IsNullOrEmpty(save.friendId)) return;
                NativeShare.Share("Add me on Tuition Takkar! ✏️ My player ID: " + save.friendId + "\nPlay Online → Friends → Add friend.", "My Tuition Takkar player ID");
            };
            acts.Add(copy, share);
            idRow.Add(acts);
            idCard.Add(idRow);
            if (string.IsNullOrEmpty(save.friendId))
            {
                idCard.Add(UIKit.Strong("getting your ID…", 12f, Pal.Mist).St(s => s.marginTop = 6));
                Get();
                async void Get() { await Friends.EnsureFriendIdAsync(save); if (!string.IsNullOrEmpty(save.friendId) && body.panel != null) { body.Clear(); PaintAdd(gm, body); } }
            }
            body.Add(idCard);

            body.Add(Head("ADD BY PLAYER ID"));
            var row = UIKit.Row().St(s => { s.marginLeft = 16; s.marginRight = 16; });
            var field = new TextField { maxLength = 9, value = "" };
            field.style.flexGrow = 1;
            field.style.marginLeft = field.style.marginRight = 0;
            field.style.unityFontDefinition = FontDefinition.FromFont(UIKit.BodyBold);
            field.style.fontSize = 22;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Color.white; input.style.height = 54; UIKit.Pad(input.style, 0, 14, 0, 14); UIKit.Radius(input.style, 14);
                UIKit.Border(input.style, 2f, Pal.Ink); input.style.color = Pal.Ink; input.style.letterSpacing = 3;
            }
            field.textEdition.placeholder = "TT-XXXXXX";
            field.RegisterCallback<FocusInEvent>(_ => typing = true);
            field.RegisterCallback<FocusOutEvent>(_ => typing = false);
            var find = UIKit.Btn("Find", null, Green, Color.white, UIKit.BodyBold, 15f, 58f, 14f);
            find.style.width = 70; find.style.marginLeft = 8;
            row.Add(field, find);
            body.Add(row);
            var result = UIKit.Col().St(s => { s.marginLeft = 16; s.marginRight = 16; });
            body.Add(result);

            find.clicked += async () =>
            {
                result.Clear();
                string code = Friends.CleanId(field.value);
                if (code == "") { result.Add(Note("a player ID looks like TT-7K3P9Q (6 letters and numbers).", Pal.H("#C25A10"))); return; }
                if (code == save.friendId) { result.Add(Note("that's your own ID!", Pal.H("#C25A10"))); return; }
                find.SetEnabled(false);
                result.Add(Note("looking for " + code + "…", Soft));
                string pid = null;
                try { pid = await Friends.FindAsync(code); }
                catch (Exception e) { result.Clear(); result.Add(Note(OnlineServices.Explain(e), Red)); find.SetEnabled(true); return; }
                find.SetEnabled(true);
                result.Clear();
                if (pid == null) { result.Add(Note("no player with that ID. check the letters.", Pal.H("#C25A10"))); return; }
                var p = await ProfileCloud.LoadAsync(pid);
                string name = p != null ? p.name : "Player";
                var card = UIKit.Row().St(s => { s.marginTop = 14; s.backgroundColor = Pal.Paper; UIKit.Radius(s, 16); UIKit.Pad(s, 14); });
                card.Add(UIKit.Avatar(ProfileScreens.Initial(name), OnlineScreens.AvatarColor(pid), 54f));
                var c = UIKit.Col().St(s => { s.marginLeft = 12; s.flexShrink = 1; });
                c.Add(UIKit.Disp(name.ToUpperInvariant(), 20f, Pal.Ink));
                var cr = UIKit.Row().St(s => s.marginTop = 4);
                cr.Add(UIKit.Strong(code, 12f, Soft));
                if (p != null) cr.Add(LevelChip(p.level));
                c.Add(cr);
                card.Add(c);
                result.Add(card);
                var send = AddFriendButton(gm, pid, name);
                if (send != null) result.Add(send.St(s => s.marginTop = 12));
            };
        }

        static VisualElement Note(string text, Color c) =>
            UIKit.Strong(text, 13f, c).St(s => { s.marginTop = 12; s.whiteSpace = WhiteSpace.Normal; });

        /// <summary>Add friend / Request sent / Friends ✓ for a player (null for guests).</summary>
        public static VisualElement AddFriendButton(GameManager gm, string playerId, string name)
        {
            if (!Friends.Allowed(gm.Save) || string.IsNullOrEmpty(playerId) || playerId == OnlineServices.PlayerId) return null;
            var holder = UIKit.Col();
            void Paint(string msg = null)
            {
                holder.Clear();
                if (!Friends.Ready)
                {
                    holder.Add(UIKit.Btn("ADD FRIEND", async () => { await Friends.InitAsync(gm); Paint(); }, Pal.Yellow, Pal.Ink, UIKit.Display, 20f, 54f, 16f, Pal.H("#C7A928")));
                    return;
                }
                if (Friends.IsFriend(playerId)) { holder.Add(UIKit.Strong("friends ✓", 15f, Green, TextAnchor.MiddleCenter)); return; }
                if (Friends.IsPending(playerId)) { holder.Add(UIKit.Strong("request sent · waiting for " + name, 14f, Soft, TextAnchor.MiddleCenter)); return; }
                var b = UIKit.Btn("SEND FRIEND REQUEST", null, Pal.Yellow, Pal.Ink, UIKit.Display, 20f, 54f, 16f, Pal.H("#C7A928"));
                b.clicked += async () =>
                {
                    b.SetEnabled(false);
                    string e = await Friends.RequestAsync(playerId);
                    if (e == null) gm.Save.Save();
                    Paint(e);
                };
                holder.Add(b);
                if (msg != null) holder.Add(UIKit.Strong(msg, 12f, Red, TextAnchor.MiddleCenter).St(s => { s.marginTop = 6; s.whiteSpace = WhiteSpace.Normal; }));
            }
            Paint();
            if (!Friends.Ready) { Load(); async void Load() { try { await Friends.InitAsync(gm); } catch (Exception) { } if (holder.panel != null) Paint(); } }
            return holder;
        }

        // =====================================================================
        // F5 guests
        // =====================================================================

        static VisualElement Locked(GameManager gm)
        {
            var root = OnlineScreens.Screen();
            root.Add(OnlineScreens.Header("FRIENDS", null, () => OnlineScreens.ShowPlay(gm)));
            var col = UIKit.Col().St(s => { s.alignItems = Align.Center; UIKit.Pad(s, 50, 30, 0, 30); s.flexGrow = 1; });
            var icon = UIKit.El().St(s => { s.width = 110; s.height = 110; UIKit.Radius(s, 55); s.backgroundColor = Pal.Night; s.alignItems = Align.Center; s.justifyContent = Justify.Center; });
            icon.Add(UIKit.Disp("?", 54f, Pal.Yellow, TextAnchor.MiddleCenter));
            col.Add(icon);
            col.Add(UIKit.Disp("FRIENDS NEED\nA SIGNED-IN ACCOUNT", 28f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 18));
            col.Add(UIKit.HandText("you're playing as " + OnlineServices.DisplayName(gm.Save) + ". sign in to get your own player ID, add friends and invite them to rooms.", 18f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            col.Add(UIKit.Strong("your marks, pens and level come with you.", 13f, Pal.InkSoft, TextAnchor.MiddleCenter).St(s => s.marginTop = 10));
            col.Add(UIKit.Grow());
            var g = UIKit.Btn("Sign in with Google", () => OnlineScreens.SignInFromDesk(gm), Color.white, Pal.Ink, UIKit.BodyBold, 17f, 58f, 16f, Pal.H("#C9D3DE"), Pal.Ink);
            g.style.alignSelf = Align.Stretch;
            col.Add(g);
            col.Add(UIKit.Strong("Sign in with Apple comes with the iPhone version", 12f, Pal.BenchText, TextAnchor.MiddleCenter).St(s => { s.marginTop = 10; s.marginBottom = 40; }));
            root.Add(col);
            return root;
        }

        // =====================================================================
        // R2 invite sheet (private room)
        // =====================================================================

        public static async void ShowInviteSheet(GameManager gm, OnlineRoom room)
        {
            if (!Friends.Allowed(gm.Save)) { gm.UI.ShowScreen(Locked(gm)); return; }
            if (!Friends.Ready)
            {
                gm.UI.ShowOverlay(OnlineScreens.Busy("loading friends…"));
                try { await Friends.InitAsync(gm); }
                catch (Exception e) { gm.UI.ShowOverlay(OnlineScreens.Error(gm, "CAN'T LOAD FRIENDS", OnlineServices.Explain(e))); return; }
            }
            var o = OnlineScreens.Sheet(out var c);
            c.Add(UIKit.Disp("INVITE FRIENDS", 26f, Pal.Ink));
            var d = room.Desk;
            c.Add(UIKit.Strong(d.name + (d.Free ? " · free" : " · entry " + Desks.Money(d.fee) + " · they need the marks"), 13f, Pal.BenchText));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 460; scroll.style.marginTop = 8;
            var list = new List<Relationship>(Friends.List);
            list.Sort((a, b) => ((int)Friends.StateOf(a)).CompareTo((int)Friends.StateOf(b)));
            if (list.Count == 0)
                scroll.Add(UIKit.HandText("no friends yet. add classmates from Play Online → Friends.", 17f, Pal.BenchText).St(s => s.marginTop = 10));
            foreach (var r in list)
            {
                var rr = r;
                bool inRoom = room.Find(r.Member.Id) != null;
                VisualElement action;
                if (inRoom) action = UIKit.Strong("in room ✓", 12f, Green);
                else if (Friends.StateOf(r) == Friends.State.Playing) action = UIKit.Strong("busy", 13f, Pal.H("#C25A10")).St(s => { UIKit.Border(s, 1.5f, Pal.H("#F2A23A")); UIKit.Radius(s, 10); UIKit.Pad(s, 6, 10, 6, 10); });
                else
                {
                    var b = UIKit.Btn("Invite", null, Pal.Night, Color.white, UIKit.BodyBold, 13f, 36f, 11f);
                    b.clicked += async () =>
                    {
                        b.SetEnabled(false);
                        b.text = "…";
                        string e = await Friends.InviteAsync(gm, rr, room);
                        b.text = e == null ? "Invited ✓" : "Retry";
                        b.style.backgroundColor = e == null ? Pal.H("#DFF0E5") : Red;
                        b.style.color = e == null ? Green : Color.white;
                        b.SetEnabled(e != null);
                        if (e != null) Debug.LogWarning("Invite: " + e);
                    };
                    action = b;
                }
                var row = FriendRow(r, action);
                row.style.marginLeft = row.style.marginRight = 0;
                scroll.Add(row);
            }
            c.Add(scroll);
            c.Add(UIKit.Strong("online friends get a pop-up in the game. offline friends get a phone notification. invites last 5 minutes.", 12f, Pal.H("#6B5216")).St(s =>
            {
                s.marginTop = 10; s.backgroundColor = Pal.H("#FFF4C2"); UIKit.Radius(s, 12); UIKit.Pad(s, 10, 12, 10, 12); s.whiteSpace = WhiteSpace.Normal;
            }));
            c.Add(UIKit.Btn("Done", () => gm.UI.HideOverlay(), Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f).St(s => s.marginTop = 4));
            gm.UI.ShowOverlay(o);
        }

        // =====================================================================
        // R3 incoming invite
        // =====================================================================

        static bool hooked;

        /// <summary>Listen for invites from friends while the game is open.</summary>
        public static void Hook(GameManager gm)
        {
            if (hooked) return;
            hooked = true;
            Friends.InviteReceived += inv =>
            {
                if (gm.Match.Running) return;   // they show as busy; never interrupt a match
                long age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inv.at;
                if (age > Friends.InviteSeconds) return;
                gm.UI.ShowOverlay(InvitePopup(gm, inv));
                gm.Audio.Stamp();
            };
        }

        static VisualElement InvitePopup(GameManager gm, RoomInvite inv)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.62f); s.justifyContent = Justify.Center; UIKit.Pad(s, 0, 22, 60, 22); });
            o.pickingMode = PickingMode.Position;
            var card = UIKit.Col().St(s => { s.backgroundColor = Pal.Paper; UIKit.Radius(s, 20); UIKit.Pad(s, 18); });
            UIKit.Rot(card, -1f);
            card.Add(UIKit.Strong("ROOM INVITE", 12f, Pal.Ink, TextAnchor.MiddleCenter).St(s =>
            {
                s.alignSelf = Align.Center; s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 4, 14, 4, 14); s.marginTop = -30; s.letterSpacing = 1f;
            }));
            var top = UIKit.Row().St(s => s.marginTop = 8);
            top.Add(UIKit.Avatar(ProfileScreens.Initial(inv.from), OnlineScreens.AvatarColor(inv.fromId), 54f));
            var who = UIKit.Col().St(s => s.marginLeft = 12);
            who.Add(UIKit.Disp(inv.from.ToUpperInvariant(), 22f, Pal.Ink));
            who.Add(UIKit.HandText("saved you a seat!", 17f, Pal.BenchText));
            top.Add(who);
            card.Add(top);
            var d = Desks.ById(inv.deskId) ?? Desks.All[0];
            var desk = UIKit.Row().St(s =>
            {
                s.marginTop = 14; s.justifyContent = Justify.SpaceBetween; UIKit.Radius(s, 14); UIKit.Pad(s, 12, 14, 12, 14);
                s.backgroundColor = Color.Lerp(d.top, Color.black, 0.35f);
            });
            var dl = UIKit.Col();
            dl.Add(UIKit.Strong(d.Number + " · " + Desks.TierName(d.tier) + " DESK", 10f, Pal.H("#F2E2C8")));
            dl.Add(UIKit.Disp(d.name.ToUpperInvariant(), 19f, Color.white));
            dl.Add(UIKit.Strong(inv.seated + " of " + inv.seats + " players · " + (d.Free ? "free" : "best of " + d.BestOf), 12f, Pal.H("#F2E2C8")));
            desk.Add(dl);
            var fee = UIKit.Col().St(s => s.alignItems = Align.FlexEnd);
            fee.Add(UIKit.Strong("ENTRY", 10f, Pal.H("#F2E2C8")));
            var fr = UIKit.Row();
            if (!d.Free) fr.Add(new CoinIcon(18f));
            fr.Add(UIKit.Disp(d.Free ? "FREE" : Desks.Money(d.fee), 20f, Color.white).St(s => s.marginLeft = 4));
            fee.Add(fr);
            desk.Add(fee);
            card.Add(desk);
            var buttons = UIKit.Row(Align.Stretch).St(s => { s.marginTop = 14; s.justifyContent = Justify.SpaceBetween; });
            var join = UIKit.Btn("JOIN", null, Green, Color.white, UIKit.Display, 22f, 56f, 16f, Pal.H("#134A37"));
            join.style.width = Length.Percent(62);
            join.clicked += () => { gm.UI.HideOverlay(); OnlineScreens.JoinDirect(gm, inv.code); };
            var ignore = UIKit.Btn("IGNORE", () => gm.UI.HideOverlay(), Pal.H("#E1E6EA"), Pal.Ink, UIKit.Display, 18f, 56f, 16f, Pal.H("#B9C2C9"));
            ignore.style.width = Length.Percent(34);
            buttons.Add(join, ignore);
            card.Add(buttons);
            var left = UIKit.Strong("", 12f, Soft, TextAnchor.MiddleCenter).St(s => s.marginTop = 10);
            card.Add(left);
            o.Add(card);
            UIRoot.PopIn(card);
            void Tick()
            {
                long rem = Friends.InviteSeconds - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inv.at);
                if (rem <= 0) { left.text = "this invite has expired"; join.SetEnabled(false); return; }
                left.text = "expires in " + (rem / 60) + ":" + (rem % 60).ToString("00") + " · you have " + Desks.Money(gm.Save.marks) + " marks";
            }
            Tick();
            o.schedule.Execute(Tick).Every(1000);
            return o;
        }
    }
}
