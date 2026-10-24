using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace TuitionTakkar
{
    /// <summary>
    /// Reminders the phone schedules for itself (no server). All at 7 PM, at most one a day,
    /// rebuilt every time the game opens or goes to the background, and none after 7 days away:
    ///   day 0  ink low on the equipped pen, else "double-marks win is waiting" (if not used today)
    ///   day 1  "&lt;next rival&gt; is waiting for a rematch"
    ///   day 2  double-marks win
    ///   day 3  "your pens are gathering dust"
    ///   day 5  double-marks win
    ///   day 7  "50 bonus marks if you come back today" (paid on return after 7+ days)
    /// Permission is asked after the player's first win, never at first launch.
    /// </summary>
    public static class Reminders
    {
        public const int Hour = 19;
        public const int ComebackBonus = 50;
        const string Channel = "reminders";

        struct Item { public int day; public string title, body; }

        // ---------- start-up ----------

        public static void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.Initialize();
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                    Channel, "Reminders", "Daily double, rematches and ink reminders", Importance.Default));
                AndroidNotificationCenter.CancelAllDisplayedNotifications();
            }
            catch (Exception e) { Debug.LogWarning("Reminders init: " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                iOSNotificationCenter.RemoveAllDeliveredNotifications();
                iOSNotificationCenter.ApplicationBadge = 0;
            }
            catch (Exception e) { Debug.LogWarning("Reminders init: " + e.Message); }
#endif
        }

        /// <summary>
        /// Call once when the game opens. Pays the comeback bonus after 7+ days away and
        /// returns true if it did (the home screen shows "Welcome back").
        /// </summary>
        public static bool CheckComeback(SaveData save)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            bool paid = false;
            if (!string.IsNullOrEmpty(save.lastPlayDay) &&
                DateTime.TryParse(save.lastPlayDay, out var last) &&
                (DateTime.Now.Date - last.Date).TotalDays >= 7)
            {
                save.marks += ComebackBonus;
                paid = true;
            }
            save.lastPlayDay = today;
            save.Save();
            return paid;
        }

        // ---------- scheduling ----------

        /// <summary>Clears every pending reminder and books the next ones (if allowed and switched on).</summary>
        public static void Reschedule(SaveData save)
        {
            CancelAll();
            if (!save.remindersOn || !Allowed()) return;
            foreach (var it in Plan(save)) Schedule(it);
        }

        static List<Item> Plan(SaveData save)
        {
            var list = new List<Item>();
            var now = DateTime.Now;
            bool todayStillOpen = now.Hour < Hour; // 7 PM hasn't passed yet

            // Day 0: ink first (it is about a pen they care about), else the daily double.
            var pen = Catalog.Pen(save.equipped);
            int ink = save.Ink(pen.id);
            if (todayStillOpen)
            {
                if (Economy.UsesInk(pen.id) && ink > 0 && ink <= 2)
                    list.Add(new Item { day = 0, title = "Running low on ink ✏️", body = "Your " + pen.name + " has " + ink + (ink == 1 ? " match" : " matches") + " of ink left. Refill it in the pouch." });
                else if (Economy.DoubleAvailable(save))
                    list.Add(new Item { day = 0, title = "Double marks are waiting", body = "Your first win today pays 2× marks. One quick match?" });
            }

            string rival = save.LadderComplete ? "The whole batch" : save.CurrentRival.name;
            list.Add(new Item { day = 1, title = rival + " wants a rematch", body = save.LadderComplete ? "You're the champion. Come defend your title!" : rival + " is waiting at the desk. Flick them off!" });
            list.Add(new Item { day = 2, title = "Double marks are waiting", body = "Your first win today pays 2× marks." });
            list.Add(new Item { day = 3, title = "Your pens are gathering dust", body = "Win today for 2× marks!" });
            list.Add(new Item { day = 5, title = "Double marks are waiting", body = "One win today pays double. The desk misses you." });
            list.Add(new Item { day = 7, title = ComebackBonus + " bonus marks for you 🎁", body = "Come back today and " + ComebackBonus + " marks are yours." });
            return list;
        }

        static DateTime At(int day) => DateTime.Now.Date.AddDays(day).AddHours(Hour);

        static void Schedule(Item it)
        {
            var when = At(it.day);
            if (when <= DateTime.Now.AddMinutes(1)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var n = new AndroidNotification
                {
                    Title = it.title,
                    Text = it.body,
                    FireTime = when,
                };
                AndroidNotificationCenter.SendNotification(n, Channel);
            }
            catch (Exception e) { Debug.LogWarning("Reminder: " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = "tt_day" + it.day,
                    Title = it.title,
                    Body = it.body,
                    ShowInForeground = false,
                    Trigger = new iOSNotificationCalendarTrigger
                    {
                        Year = when.Year, Month = when.Month, Day = when.Day, Hour = when.Hour, Minute = 0, Repeats = false,
                    },
                });
            }
            catch (Exception e) { Debug.LogWarning("Reminder: " + e.Message); }
#else
            Debug.Log("[TuitionTakkar] reminder " + when.ToString("ddd dd MMM HH:mm") + ": " + it.title + " | " + it.body);
#endif
        }

        static void CancelAll()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { AndroidNotificationCenter.CancelAllScheduledNotifications(); } catch (Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            try { iOSNotificationCenter.RemoveAllScheduledNotifications(); } catch (Exception) { }
#endif
        }

        // ---------- permission ----------

        static bool Allowed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed; }
            catch (Exception) { return false; }
#elif UNITY_IOS && !UNITY_EDITOR
            try { return iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized; }
            catch (Exception) { return false; }
#else
            return true;
#endif
        }

        /// <summary>
        /// After the first win: our own friendly question first, and only on "Yes" the phone's
        /// permission prompt (people say yes far more often when they know why).
        /// </summary>
        public static void AskAfterWin(GameManager gm)
        {
            var save = gm.Save;
            if (save.remindersAsked) return;
            save.remindersAsked = true;
            save.Save();
            if (Allowed() && !Application.isEditor) { Reschedule(save); return; }
            gm.UI.ScreenLayer.schedule.Execute(() => gm.UI.ShowOverlay(AskSheet(gm))).StartingIn(1400);
        }

        static VisualElement AskSheet(GameManager gm)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f); s.justifyContent = Justify.FlexEnd; });
            o.pickingMode = PickingMode.Position;
            var c = UIKit.Col().St(s =>
            {
                s.backgroundColor = Pal.Paper; UIKit.Pad(s, 22, 20, 30, 20);
                s.borderTopLeftRadius = s.borderTopRightRadius = 22;
            });
            c.Add(UIKit.Disp("NICE WIN!", 30f, Pal.Ink));
            c.Add(UIKit.HandText("want a reminder at 7 pm when your double-marks win is ready? at most one a day.", 18f, Pal.Ink).St(s => s.marginTop = 6));
            var yes = UIKit.PrimaryBtn("YES, REMIND ME", () =>
            {
                gm.UI.HideOverlay();
                RequestPermission(gm.Save);
            }, 56f);
            yes.style.marginTop = 16;
            c.Add(yes);
            var no = UIKit.Btn("No thanks", () =>
            {
                gm.Save.remindersOn = false;
                gm.Save.Save();
                gm.UI.HideOverlay();
            }, Color.clear, Pal.Ink, UIKit.BodyBold, 15f, 46f, 12f);
            no.style.marginTop = 6;
            c.Add(no);
            o.Add(c);
            UIRoot.PopIn(c);
            return o;
        }

        /// <summary>Settings switch. Turning it on asks the phone for permission if needed.</summary>
        public static void SetOn(SaveData save, bool on)
        {
            save.remindersOn = on;
            save.remindersAsked = true;
            save.Save();
            if (on && !Allowed()) RequestPermission(save);
            else Reschedule(save);
        }

        public static void RequestPermission(SaveData save)
        {
            save.remindersOn = true;
            save.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var req = new PermissionRequest();
                MainThread.Run(() => WaitAndroid(req, save, 0));
            }
            catch (Exception e) { Debug.LogWarning("Reminder permission: " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false);
                MainThread.Run(() => WaitIOS(req, save, 0));
            }
            catch (Exception e) { Debug.LogWarning("Reminder permission: " + e.Message); }
#else
            Reschedule(save);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void WaitAndroid(PermissionRequest req, SaveData save, int tries)
        {
            if (req.Status == PermissionStatus.RequestPending && tries < 600)
            {
                GameManager.I.UI.Root.schedule.Execute(() => WaitAndroid(req, save, tries + 1)).StartingIn(100);
                return;
            }
            Reschedule(save);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        static void WaitIOS(AuthorizationRequest req, SaveData save, int tries)
        {
            if (!req.IsFinished && tries < 600)
            {
                GameManager.I.UI.Root.schedule.Execute(() => WaitIOS(req, save, tries + 1)).StartingIn(100);
                return;
            }
            req.Dispose();
            Reschedule(save);
        }
#endif

        /// <summary>"Welcome back! +50 marks" after 7+ days away.</summary>
        public static VisualElement WelcomeBack(GameManager gm)
        {
            var o = UIKit.Col().St(s => { s.backgroundColor = new Color(0.063f, 0.102f, 0.118f, 0.6f); s.justifyContent = Justify.Center; UIKit.Pad(s, 0, 28, 0, 28); });
            o.pickingMode = PickingMode.Position;
            var c = UIKit.Col().St(s => { s.backgroundColor = Pal.Yellow; UIKit.Pad(s, 24, 20, 22, 20); s.alignItems = Align.Center; s.borderBottomWidth = 6; s.borderBottomColor = new Color(0, 0, 0, 0.18f); });
            UIKit.Rot(c, -2f);
            c.Add(UIKit.Disp("WELCOME BACK!", 32f, Pal.Ink, TextAnchor.MiddleCenter));
            var r = UIKit.Row().St(s => s.marginTop = 10);
            r.Add(new CoinIcon(30f));
            r.Add(UIKit.Disp("+" + ComebackBonus, 40f, Pal.Ink).St(s => s.marginLeft = 8));
            c.Add(r);
            c.Add(UIKit.HandText("bonus marks for coming back. the desk missed you.", 18f, Pal.Ink, TextAnchor.MiddleCenter).St(s => s.marginTop = 6));
            var ok = UIKit.PrimaryBtn("LET'S PLAY", () => { gm.UI.HideOverlay(); gm.ShowHome(); }, 54f);
            ok.style.marginTop = 16;
            ok.style.alignSelf = Align.Stretch;
            c.Add(ok);
            o.Add(c);
            UIRoot.PopIn(c);
            gm.Audio.Stamp();
            return o;
        }
    }
}
