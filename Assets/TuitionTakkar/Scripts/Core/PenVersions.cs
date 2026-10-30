using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Classic and shop pens get a V1-&gt;V2-&gt;V3 progression: higher versions perform better and
    /// look different, and unlock as the player's Online Level rises (the same level that already
    /// opens higher desks), paid for in marks at the moment of upgrade. Custom Workshop-built pens
    /// are exempt and always report version 1 here.
    /// </summary>
    public static class PenVersions
    {
        public const int MaxVersion = 3;

        public static int LevelFor(int version) => version == 2 ? 10 : version == 3 ? 30 : 1;
        public static int CostFor(int version) => version == 2 ? 200 : version == 3 ? 600 : 0;

        /// <summary>The highest version this save's Online Level allows for this pen.</summary>
        public static int Unlocked(SaveData save, string id)
        {
            if (PenParts.IsCustom(id)) return 1;
            int level = OnlineLevel.Level(save.onlineXp);
            if (level >= LevelFor(3)) return 3;
            if (level >= LevelFor(2)) return 2;
            return 1;
        }

        /// <summary>The version this pen is actually set to right now (bought/upgraded), clamped to what's unlocked.</summary>
        public static int CurrentVersion(SaveData save, string id)
        {
            if (PenParts.IsCustom(id)) return 1;
            var entry = save.penVersions.Find(v => v.id == id);
            if (entry == null) return 1;
            return Mathf.Clamp(entry.version, 1, Unlocked(save, id));
        }

        public static bool TryUpgrade(SaveData save, string id, out string error)
        {
            error = null;
            if (PenParts.IsCustom(id)) { error = "custom pens can't be upgraded."; return false; }
            int current = CurrentVersion(save, id);
            if (current >= MaxVersion) { error = "already at the top version."; return false; }
            int next = current + 1;
            int needLevel = LevelFor(next);
            if (OnlineLevel.Level(save.onlineXp) < needLevel) { error = "reach online level " + needLevel + " to unlock this."; return false; }
            int cost = CostFor(next);
            if (save.marks < cost) { error = "not enough marks (need " + cost + ")."; return false; }
            save.marks -= cost;
            var entry = save.penVersions.Find(v => v.id == id);
            if (entry == null) save.penVersions.Add(new PenVersion { id = id, version = next });
            else entry.version = next;
            return true;
        }

        /// <summary>Applies a version's stat boost and look to a base pen. V1 (or a custom pen) is returned unchanged.</summary>
        public static PenDef Apply(PenDef baseDef, int version)
        {
            if (version <= 1 || baseDef.custom) return baseDef;
            var d = baseDef.Recolor(baseDef.color, baseDef.capColor); // clones via the same MemberwiseClone PenDef already exposes
            float factor = version == 3 ? 1.12f : 1.06f;
            d.statWeight = Mathf.Clamp01(baseDef.statWeight * factor);
            d.statGrip = Mathf.Clamp01(baseDef.statGrip * factor);
            d.statSpin = Mathf.Clamp01(baseDef.statSpin * factor);
            d.mass = Mathf.Lerp(PenParts.MassMin, PenParts.MassMax, d.statWeight);
            d.friction = Mathf.Lerp(PenParts.FrictionMin, PenParts.FrictionMax, d.statGrip);
            d.angularDamping = Mathf.Lerp(PenParts.DampingHigh, PenParts.DampingLow, d.statSpin);
            d.finish = version == 3 ? PenFinish.Holo : PenFinish.Metal;
            if (version == 3) d.color = Color.Lerp(baseDef.color, Pal.H("#D4A72C"), 0.25f);
            d.name = baseDef.name + " V" + version;
            return d;
        }
    }
}
