using System;
using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
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

        public bool Owns(string penId) => owned.Contains(penId);
        public bool LadderComplete => ladderIndex >= Catalog.Rivals.Count;

        public RivalDef CurrentRival =>
            Catalog.Rivals[Mathf.Clamp(ladderIndex, 0, Catalog.Rivals.Count - 1)];

        public static SaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                if (data.owned == null) data.owned = new List<string>();
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
