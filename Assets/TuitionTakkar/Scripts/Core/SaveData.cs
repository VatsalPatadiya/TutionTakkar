using System;
using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Ink left in one pen (JsonUtility can't save dictionaries).</summary>
    [Serializable]
    public class PenInk
    {
        public string id;
        public int ink;
    }

    [Serializable]
    public class SaveData
    {
        const string Key = "tuition_takkar_save_v1";

        public int marks = 100;
        public int ladderIndex;
        public List<string> owned = new List<string> { Catalog.StarterId };
        public string equipped = Catalog.StarterId;
        public int practiceDifficulty = 1;
        public bool soundOn = true;
        public bool hapticsOn = true;
        /// <summary>Ink per pen. A pen with no entry has a full tank (older saves, new pens).</summary>
        public List<PenInk> inks = new List<PenInk>();
        /// <summary>Local date (yyyy-MM-dd) the daily double was last used.</summary>
        public string lastDoubleDay = "";

        // ---------- online ----------
        /// <summary>Signed in with Google on this phone (online play unlocked).</summary>
        public bool googleLinked;
        /// <summary>Editor / dev builds: online without Google, for testing.</summary>
        public bool devGuest;
        /// <summary>Plays online as "Guest_xxxxxx" without Google. Guest wins never go on the leaderboard.</summary>
        public bool onlineGuest;
        /// <summary>Public name without Unity's "#1234" tag.</summary>
        public string onlineName = "";
        /// <summary>Last known online wins (shown before the leaderboard answers).</summary>
        public int onlineWins;
        /// <summary>Online XP: sets the online level that opens the higher desks.</summary>
        public int onlineXp;
        /// <summary>Local date (yyyy-MM-dd) the daily pocket money was last taken.</summary>
        public string lastPocketDay = "";
        /// <summary>The desk last chosen for Quick Match.</summary>
        public string lastDesk = "tuition";
        /// <summary>Profile stats and badges.</summary>
        public PlayerStats stats = new PlayerStats();
        /// <summary>Permanent player ID for friends (TT-XXXXXX), from Cloud Code. Google players only.</summary>
        public string friendId = "";

        // ---------- reminders ----------
        /// <summary>Settings switch for the 7 PM reminders.</summary>
        public bool remindersOn = true;
        /// <summary>The "want a reminder?" question has been shown (after the first win).</summary>
        public bool remindersAsked;
        /// <summary>Local date (yyyy-MM-dd) the game was last opened, for the comeback bonus.</summary>
        public string lastPlayDay = "";

        public int Ink(string penId)
        {
            if (!Economy.UsesInk(penId)) return Economy.InkTank;
            foreach (var p in inks) if (p.id == penId) return p.ink;
            return Economy.InkTank;
        }

        public void SetInk(string penId, int ink)
        {
            ink = Mathf.Clamp(ink, 0, Economy.InkTank);
            foreach (var p in inks)
            {
                if (p.id != penId) continue;
                p.ink = ink;
                return;
            }
            inks.Add(new PenInk { id = penId, ink = ink });
        }

        /// <summary>Uses one match of ink. False when the pen is already dry.</summary>
        public bool UseInk(string penId)
        {
            if (!Economy.UsesInk(penId)) return true;
            int left = Ink(penId);
            if (left <= 0) return false;
            SetInk(penId, left - 1);
            return true;
        }

        /// <summary>Adds a pen to the pouch with a full tank (bought or won).</summary>
        public void AddPen(string penId)
        {
            if (!owned.Contains(penId)) owned.Add(penId);
            SetInk(penId, Economy.InkTank);
        }

        public bool Owns(string penId) => owned.Contains(penId);
        /// <summary>The ladder never ends now (new batches keep coming).</summary>
        public bool LadderComplete => false;

        /// <summary>The rival to beat next: forward only, beaten rivals can't be replayed.</summary>
        public RivalDef CurrentRival => Ladder.RivalAt(ladderIndex);

        public static SaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                if (data.owned == null) data.owned = new List<string>();
                if (data.inks == null) data.inks = new List<PenInk>();
                if (data.lastDoubleDay == null) data.lastDoubleDay = "";
                if (data.onlineName == null) data.onlineName = "";
                if (data.lastPlayDay == null) data.lastPlayDay = "";
                if (data.lastPocketDay == null) data.lastPocketDay = "";
                if (string.IsNullOrEmpty(data.lastDesk)) data.lastDesk = "tuition";
                if (data.stats == null) data.stats = new PlayerStats();
                if (data.friendId == null) data.friendId = "";
                data.stats.Fix();
                if (!data.owned.Contains(Catalog.StarterId)) data.owned.Insert(0, Catalog.StarterId);
                if (!data.Owns(data.equipped)) data.equipped = Catalog.StarterId;
                return data;
            }
            catch (Exception)
            {
                return new SaveData();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
