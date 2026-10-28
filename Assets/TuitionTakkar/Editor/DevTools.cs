using UnityEditor;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Editor-only test helpers (not in builds).</summary>
    public static class DevTools
    {
        /// <summary>Play mode: a 3 or 4 pen desk on one screen (hot-seat), to check the party desk.</summary>
        [MenuItem("Tuition Takkar/Dev: 4-player desk (hot-seat)")]
        static void Party4() => Party(4);

        [MenuItem("Tuition Takkar/Dev: 3-player desk (hot-seat)")]
        static void Party3() => Party(3);

        [MenuItem("Tuition Takkar/Dev: 2-player desk (hot-seat)")]
        static void Party2() => Party(2);

        [MenuItem("Tuition Takkar/Dev: Skip turn clock to 8 s")]
        static void Skip() => MatchController.DevSetSeconds = 8f;

        /// <summary>Game view at a phone's portrait resolution (1170 x 2532), so layouts look like on a phone.</summary>
        [MenuItem("Tuition Takkar/Dev: Phone-size game view")]
        static void PhoneView()
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = single.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Android });
            var gType = group.GetType();
            int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var gs = gType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)gs.GetType().GetProperty("baseText").GetValue(gs) == "TT phone") { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kind = asm.GetType("UnityEditor.GameViewSizeType");
                var size = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new object[] { System.Enum.ToObject(kind, 1), 1170, 2532, "TT phone" });
                gType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = total;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(gv, new object[] { index, null });
        }

        /// <summary>Play mode: a 2-player desk with two fancy shop pens (finishes, patterns) to check how they look.</summary>
        [MenuItem("Tuition Takkar/Dev: 2-player desk, fancy pens")]
        static void FancyPens()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            PenDef a = null, b = null;
            foreach (var p in PenParts.Generated)
            {
                if (a == null && p.finish == PenFinish.Glitter && p.pattern == PenPattern.Stars) a = p;
                if (b == null && p.finish == PenFinish.Gold && p.shape == PenShape.Round) b = p;
            }
            var s = new MatchSetup(2) { mode = MatchMode.PassAndPlay };
            s.pens[0] = a ?? PenParts.Generated[0];
            s.pens[1] = b ?? PenParts.Generated[1];
            s.names[0] = "Player 1"; s.names[1] = "Player 2";
            gm.StartMatch(s);
        }

        [MenuItem("Tuition Takkar/Dev: Ladder to Batch 2, rival 1")]
        static void Ladder2() => JumpLadder(10);

        [MenuItem("Tuition Takkar/Dev: Ladder to Batch 2 boss")]
        static void Ladder2Boss() => JumpLadder(19);

        static void JumpLadder(int index)
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            gm.Save.ladderIndex = index;
            gm.Save.Save();
            gm.ShowRival();
        }

        [MenuItem("Tuition Takkar/Dev: Online, signed out (choice screen)")]
        static void OnlineChoice()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            OnlineServices.SignOut(gm.Save);
            gm.ShowOnline();
        }

        [MenuItem("Tuition Takkar/Dev: Online, continue as guest")]
        static async void OnlineGuest()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            await OnlineServices.SignInGuestAsync(gm.Save);
            Debug.Log("[TuitionTakkar] guest name " + gm.Save.onlineName + " / server name " + await OnlineServices.Auth.GetPlayerNameAsync(false));
            gm.ShowOnline();
        }

        [MenuItem("Tuition Takkar/Dev: Open tuck shop")]
        static void Shop() { if (!Application.isPlaying || GameManager.I == null) return; Application.runInBackground = true; GameManager.I.ShowPouch(1); }

        [MenuItem("Tuition Takkar/Dev: Open pen workshop")]
        static void Workshop() { if (!Application.isPlaying || GameManager.I == null) return; Application.runInBackground = true; GameManager.I.ShowPouch(2); }

        [MenuItem("Tuition Takkar/Dev: Open my pens")]
        static void MyPens() { if (!Application.isPlaying || GameManager.I == null) return; Application.runInBackground = true; GameManager.I.ShowPouch(0); }

        /// <summary>Editor only: 5000 test marks, to try buying and making pens.</summary>
        [MenuItem("Tuition Takkar/Dev: Add 5000 test marks")]
        static void Marks()
        {
            if (!Application.isPlaying || GameManager.I == null) return;
            GameManager.I.Save.marks += 5000;
            GameManager.I.Save.Save();
        }

        // ---------- desks ----------

        static int deskShown = 1;

        [MenuItem("Tuition Takkar/Dev: Desk look, next")]
        static void DeskNext() => ShowDesk(deskShown + 1);

        [MenuItem("Tuition Takkar/Dev: Desk look, previous")]
        static void DeskPrev() => ShowDesk(deskShown - 1);

        [MenuItem("Tuition Takkar/Dev: Desk look D2 Tuition Centre")]
        static void DeskD2() => ShowDesk(1);

        [MenuItem("Tuition Takkar/Dev: Desk look D6 Principal's Office")]
        static void DeskD6() => ShowDesk(5);

        [MenuItem("Tuition Takkar/Dev: Desk look D28 Champions' Palace")]
        static void DeskD28() => ShowDesk(27);

        /// <summary>Play mode: a 2-player pass-and-play match at desk i, with the HUD as online shows it.</summary>
        static void ShowDesk(int i)
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            deskShown = (i % Desks.All.Count + Desks.All.Count) % Desks.All.Count;
            var d = Desks.All[deskShown];
            var s = new MatchSetup(2) { mode = MatchMode.PassAndPlay };
            s.pens[0] = Catalog.Pen(gm.Save.equipped);
            s.pens[1] = PenParts.Generated[7];
            s.names[0] = "Player 1"; s.names[1] = "Player 2";
            gm.StartMatch(s);
            Desks.Active = d;
            gm.World.SetDesk(d, false);
            gm.World.ResetPens();
            gm.UI.Hud.SetDesk(d, d.Pot(2));
            Debug.Log("[TuitionTakkar] desk " + d.Number + " " + d.name);
        }

        [MenuItem("Tuition Takkar/Dev: Open desk chooser")]
        static void DeskChooser()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            DeskScreens.ShowChooser(gm, DeskScreens.Mode.Quick);
        }

        [MenuItem("Tuition Takkar/Dev: Add 5000 online XP")]
        static void Xp()
        {
            if (!Application.isPlaying || GameManager.I == null) return;
            GameManager.I.Save.onlineXp += 5000;
            GameManager.I.Save.Save();
            Debug.Log("[TuitionTakkar] online level " + OnlineLevel.Level(GameManager.I.Save.onlineXp));
        }

        [MenuItem("Tuition Takkar/Dev: Open my profile (sample stats)")]
        static void Profile()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            var st = gm.Save.stats;
            if (st.played == 0)
            {
                st.played = 214; st.won = 131; st.lost = 83; st.streak = 4; st.bestStreak = 11;
                st.knockOffs = 486; st.ownFalls = 172; st.flawless = 38; st.comebacks = 9; st.oneFlick = 27; st.survivor = 3;
                st.partyPlayed = 46; st.party1st = 17; st.party2nd = 12; st.potWon = 248600; st.biggestPot = 50000; st.highestDesk = 16;
                st.tierWins = new[] { 74, 38, 19, 0, 0 }; st.since = "Oct 2026"; st.daysPlayed = 19; st.bosses = 5; st.finished = 204; st.roomsHosted = 2; st.friends = 3;
                st.deskIds.Add("tuition"); st.deskPlays.Add(61); st.deskIds.Add("kota"); st.deskPlays.Add(12);
                st.recent.Add(new MatchRecord { won = true, vs = "RiyaFlicks", desk = "Kota Coaching Hub", score = "3–1", net = 40000, at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 700 });
                st.recent.Add(new MatchRecord { won = false, vs = "Arjun_07", desk = "Principal's Office", score = "1–3", net = -1000, at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2400 });
                st.recent.Add(new MatchRecord { won = true, vs = "Meera.T", desk = "Primary School", score = "2–0", free = true, at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 90000 });
                gm.Save.onlineXp += 3000;
                gm.Save.Save();
            }
            ProfileScreens.ShowProfile(gm, () => gm.ShowHome());
        }

        [MenuItem("Tuition Takkar/Dev: Open badges")]
        static void BadgesScreen()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            ProfileScreens.ShowBadges(gm, () => gm.ShowHome());
        }

        [MenuItem("Tuition Takkar/Dev: Scroll screen down")]
        static void ScrollDown()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.ScrollView>(gm.UI.ScreenLayer).ForEach(sv => sv.scrollOffset += new Vector2(0, 700));
        }

        [MenuItem("Tuition Takkar/Dev: Find player TT-B7ZE7G")]
        static async void FindTest()
        {
            if (!Application.isPlaying) { Debug.Log("[find test] enter Play mode first"); return; }
            try
            {
                await OnlineServices.InitAsync();
                var r = await Unity.Services.CloudCode.CloudCodeService.Instance.CallEndpointAsync<System.Collections.Generic.Dictionary<string, object>>("find_player",
                    new System.Collections.Generic.Dictionary<string, object> { { "code", "TT-B7ZE7G" } });
                foreach (var kv in r) Debug.Log("[find test] " + kv.Key + " = " + kv.Value);
            }
            catch (System.Exception e) { Debug.Log("[find test] failed: " + e); }
        }

        [MenuItem("Tuition Takkar/Dev: Test friend push to TT-B7ZE7G")]
        static async void PushTest()
        {
            if (!Application.isPlaying) { Debug.Log("[push test] enter Play mode first"); return; }
            try
            {
                await OnlineServices.InitAsync();
                var cc = Unity.Services.CloudCode.CloudCodeService.Instance;
                var f = await cc.CallEndpointAsync<System.Collections.Generic.Dictionary<string, object>>("find_player",
                    new System.Collections.Generic.Dictionary<string, object> { { "code", "TT-B7ZE7G" } });
                string pid = f != null && f.TryGetValue("playerId", out var v) ? v as string : null;
                Debug.Log("[push test] player " + pid);
                if (string.IsNullOrEmpty(pid)) return;
                var r = await cc.CallEndpointAsync<System.Collections.Generic.Dictionary<string, object>>("friend_push",
                    new System.Collections.Generic.Dictionary<string, object> { { "to", pid }, { "kind", "request" }, { "from", "Push test" } });
                foreach (var kv in r) Debug.Log("[push test] " + kv.Key + " = " + kv.Value);
            }
            catch (System.Exception e) { Debug.Log("[push test] failed: " + e.Message); }
        }

        [MenuItem("Tuition Takkar/Dev: Back gesture")]
        static void Back()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            BackNav.Handle(gm);
        }

        [MenuItem("Tuition Takkar/Dev: Friend request pop-up")]
        static void FakeRequest()
        {
            if (!Application.isPlaying || GameManager.I == null) return;
            FriendsUI.Hook(GameManager.I);
            Friends.FromPush(new System.Collections.Generic.Dictionary<string, string> { { "kind", "friend_request" }, { "fromName", "Riya" }, { "fromId", "dev-" + Random.Range(0, 99999) } });
        }

        [MenuItem("Tuition Takkar/Dev: Friend accepted pop-up")]
        static void FakeAccepted()
        {
            if (!Application.isPlaying || GameManager.I == null) return;
            FriendsUI.Hook(GameManager.I);
            Friends.FromPush(new System.Collections.Generic.Dictionary<string, string> { { "kind", "friend_accepted" }, { "fromName", "Riya" }, { "fromId", "dev-" + Random.Range(0, 99999) } });
        }

        [MenuItem("Tuition Takkar/Dev: Room invite pop-up")]
        static void FakeInvite()
        {
            if (!Application.isPlaying || GameManager.I == null) return;
            FriendsUI.Hook(GameManager.I);
            Friends.FromPush(new System.Collections.Generic.Dictionary<string, string> { { "kind", "invite" }, { "fromName", "Riya" }, { "fromId", "dev-1" }, { "code", "ABC" + Random.Range(100, 999) }, { "deskId", Desks.All[3].id }, { "seated", "1" }, { "seats", "2" } });
        }

        [MenuItem("Tuition Takkar/Dev: Save game view screenshot")]
        static void Shot()
        {
            System.IO.Directory.CreateDirectory("Builds");
            ScreenCapture.CaptureScreenshot("Builds/editor-shot.png");
            Debug.Log("[TuitionTakkar] screenshot saved to Builds/editor-shot.png");
        }

        // ---------- screenshots for docs/PROJECT_GUIDE.md ----------

        [MenuItem("Tuition Takkar/Dev: Home shots (guest + signed in)")]
        static async void HomeShots()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            gm.ShowHome();
            await System.Threading.Tasks.Task.Delay(1500);
            ScreenCapture.CaptureScreenshot("Builds/home-guest.png");
            await System.Threading.Tasks.Task.Delay(700);
            bool was = gm.Save.googleLinked;
            gm.Save.googleLinked = true;   // only for the picture
            gm.ShowHome();
            gm.Save.googleLinked = was;
            await System.Threading.Tasks.Task.Delay(1500);
            ScreenCapture.CaptureScreenshot("Builds/home-signed.png");
            await System.Threading.Tasks.Task.Delay(700);
            gm.ShowHome();
            Debug.Log("[home shots] done");
        }

        static string shotDir = "docs/screenshots";

        [MenuItem("Tuition Takkar/Dev: Save improved-UI screenshots")]
        static async void ImprovedShots()
        {
            shotDir = "Builds/ui-improved";
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first (phone-size game view)."); return; }
            System.IO.Directory.CreateDirectory(shotDir);
            bool was = gm.Save.googleLinked;
            gm.Save.googleLinked = true;   // only for the picture
            gm.ShowHome();
            gm.Save.googleLinked = was;
            await System.Threading.Tasks.Task.Delay(1500);
            ScreenCapture.CaptureScreenshot(shotDir + "/00-home-signed-in.png");
            await System.Threading.Tasks.Task.Delay(700);
            await EndShot(true, "21-result-win");
            await EndShot(false, "22-result-lose");
            DocShots();
        }

        static async System.Threading.Tasks.Task EndShot(bool win, string name)
        {
            var gm = GameManager.I;
            var s = new MatchSetup(2) { mode = MatchMode.Ladder, rival = gm.Save.CurrentRival };
            s.isBot[1] = true; s.pens[0] = Catalog.Pen("starter"); s.pens[1] = Catalog.Pen(gm.Save.CurrentRival != null ? gm.Save.CurrentRival.penId : "zippy07"); s.names[0] = "You"; s.names[1] = gm.Save.CurrentRival != null ? gm.Save.CurrentRival.name : "Rival";
            var o = new MatchOutcome { setup = s, winner = win ? 0 : 1, score = win ? new[] { 3, 1 } : new[] { 1, 3 }, marksBefore = gm.Save.marks, marksAfter = gm.Save.marks + (win ? 120 : 2), nextRival = gm.Save.CurrentRival, rewards = new RewardResult() };
            o.rewards.lines.Add(win ? new RewardLine { label = "Match win", note = "beat the rival", amount = 100 } : new RewardLine { label = "For trying", note = "lost the match", amount = 2 });
            if (win) o.rewards.lines.Add(new RewardLine { label = "Knock-offs", note = "3 pens off", amount = 20 });
            o.rewards.total = win ? 120 : 2;
            try { gm.UI.ShowScreen(MatchEndScreen.Build(gm, o)); } catch (System.Exception e) { Debug.LogWarning("[shots] " + name + ": " + e); }
            await System.Threading.Tasks.Task.Delay(1500);
            ScreenCapture.CaptureScreenshot(shotDir + "/" + name + ".png");
            await System.Threading.Tasks.Task.Delay(700);
        }

        [MenuItem("Tuition Takkar/Dev: Save result + signed-in home shots")]
        static async void ResultShots()
        {
            shotDir = "Builds/ui-improved";
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) return;
            Application.runInBackground = true;
            bool was = gm.Save.googleLinked;
            gm.Save.googleLinked = true;
            gm.ShowHome();
            gm.Save.googleLinked = was;
            await System.Threading.Tasks.Task.Delay(1500);
            ScreenCapture.CaptureScreenshot(shotDir + "/00-home-signed-in.png");
            await System.Threading.Tasks.Task.Delay(800);
            await EndShot(true, "21-result-win");
            await EndShot(false, "22-result-lose");
            gm.ShowHome();
            Debug.Log("[result shots] done");
        }

        [MenuItem("Tuition Takkar/Dev: Save doc screenshots")]
        static void DocShotsMenu() { shotDir = "docs/screenshots"; DocShots(); }

        static async void DocShots()
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null) { Debug.LogWarning("Press Play first (phone-size game view)."); return; }
            Application.runInBackground = true;
            System.IO.Directory.CreateDirectory(shotDir);
            async System.Threading.Tasks.Task Shot(string name, System.Action show, int waitMs = 1600)
            {
                try { show?.Invoke(); } catch (System.Exception e) { Debug.LogWarning("[doc shots] " + name + ": " + e); }
                await System.Threading.Tasks.Task.Delay(waitMs);
                ScreenCapture.CaptureScreenshot(shotDir + "/" + name + ".png");
                await System.Threading.Tasks.Task.Delay(700);
                Debug.Log("[doc shots] " + name);
            }
            void Pop(string kind, string extraKey = null, string extraVal = null)
            {
                FriendsUI.Hook(gm);
                var d = new System.Collections.Generic.Dictionary<string, string> { { "kind", kind }, { "fromName", "Riya" }, { "fromId", "doc-" + Random.Range(0, 99999) } };
                if (kind == "invite") { d["code"] = "ABC" + Random.Range(100, 999); d["deskId"] = Desks.All[3].id; d["seated"] = "1"; d["seats"] = "2"; }
                Friends.FromPush(d);
            }

            await Shot("01-home", () => gm.ShowHome());
            await Shot("02-exit-prompt", () => { gm.ShowHome(); BackNav.Handle(gm); });
            await Shot("03-batch-ladder-rival", () => gm.ShowRival());
            await Shot("04-pouch-my-pens", () => gm.ShowPouch(0));
            await Shot("05-pouch-tuck-shop", () => gm.ShowPouch(1));
            await Shot("06-pouch-workshop", () => gm.ShowPouch(2));
            await Shot("07-online-sign-in", () => { OnlineServices.SignOut(gm.Save); gm.ShowOnline(); }, 2500);
            await OnlineServices.SignInGuestAsync(gm.Save);
            await Shot("08-play-online", () => gm.ShowOnline(), 2500);
            await Shot("09-desk-chooser", () => DeskScreens.ShowChooser(gm, DeskScreens.Mode.Quick));
            await Shot("10-leaderboard", () => gm.ShowLeaderboard(), 3500);
            await Shot("11-friends-guest-locked", () => FriendsUI.Show(gm, 0));
            await Shot("12-my-profile", () =>
            {
                bool was = gm.Save.googleLinked;
                gm.Save.googleLinked = true;   // the profile is for signed-in players; only for this picture
                Profile();
                gm.Save.googleLinked = was;
            });
            await Shot("13-badges", () => ProfileScreens.ShowBadges(gm, () => gm.ShowHome()));
            await Shot("14-friend-request-popup", () => { gm.ShowHome(); Pop("friend_request"); });
            await Shot("15-friend-accepted-popup", () => { gm.ShowHome(); Pop("friend_accepted"); });
            await Shot("16-room-invite-popup", () => { gm.ShowHome(); Pop("invite"); });
            await Shot("17-match-2-players", () => Party(2), 3500);
            await Shot("18-match-paused", () => gm.Pause());
            await Shot("19-match-4-players", () => { gm.Resume(); Party(4); }, 3500);
            await Shot("20-desk-champions-palace", () => ShowDesk(27), 2500);
            gm.Resume();
            gm.ShowHome();
            Debug.Log("[doc shots] done");
        }

        static void Party(int n)
        {
            var gm = GameManager.I;
            if (!Application.isPlaying || gm == null)
            {
                Debug.LogWarning("Press Play first.");
                return;
            }
            Application.runInBackground = true; // keep playing while the editor isn't the front window
            var s = new MatchSetup(n) { mode = MatchMode.PassAndPlay };
            var ids = new[] { "starter", "zippy07", "trigrip", "gelglide" };
            for (int i = 0; i < n; i++)
            {
                s.pens[i] = Catalog.Pen(ids[i]);
                s.names[i] = "Player " + (i + 1);
            }
            gm.StartMatch(s);
        }
    }
}
