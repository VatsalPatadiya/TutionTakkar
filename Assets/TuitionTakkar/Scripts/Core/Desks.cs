using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    public enum DeskTier { Classic, Silver, Gold, Platinum, Diamond }

    /// <summary>What the playing top is made of (only the look: every desk plays the same).</summary>
    public enum DeskSurface { OldWood, Plywood, Laminate, Polished, Marble, Glass, Leather, Steel, Paint, Court }

    public enum FloorKind { Tiles, Planks, Carpet, Checker, Concrete, Stage }

    /// <summary>
    /// One online desk (8 Ball Pool style tables): everyone at the desk pays the entry fee, the
    /// winner takes the pot. Higher desks cost more, open at a higher online level, play faster
    /// and look fancier. The physics is the same at every desk, so it stays fair.
    /// </summary>
    public class DeskDef
    {
        public int index;              // 0-based (D1 = 0)
        public string id;
        public string name;
        public string where;           // one line under the name
        public DeskTier tier;
        public int fee;                // marks each player pays; 0 = friendly
        public int level;              // online level needed
        public float turnSeconds;
        public int roundsToWin;        // 2 = best of 3, 3 = best of 5 (3–4 players: always one round)
        public float size = 1f;        // top desks are smaller: less room to hide

        // ---------- look ----------
        public DeskSurface surface;
        public Color top, topDark, edge;
        public Color? inset;           // leather / glass under-colour
        public Color? veins;           // marble
        public bool carvings;
        public Color leg;
        public bool roundLegs;
        public float legMetal;         // 0 painted steel … 1 chrome / gold
        public Color wall, floorA, floorB;
        public FloorKind floor;
        public Color? rug;
        public Color light = Pal.H("#FFF9F0");

        public bool Free => fee <= 0;
        public bool Premium => tier != DeskTier.Classic;
        public string Number => "D" + (index + 1);
        public int BestOf => roundsToWin * 2 - 1;

        /// <summary>The metal of the tier: none for Classic.</summary>
        public Color? Trim => tier switch
        {
            DeskTier.Silver => Pal.H("#C9CED6"),
            DeskTier.Gold => Pal.H("#D9B44A"),
            DeskTier.Platinum => Pal.H("#E4E6EA"),
            DeskTier.Diamond => Pal.H("#DDEBFF"),
            _ => (Color?)null,
        };

        public bool Sparkle => tier == DeskTier.Diamond;

        /// <summary>What the winner takes home with this many players at the desk.</summary>
        public int Pot(int players) => fee * Mathf.Max(2, players);

        /// <summary>Prize for a place: 2 players winner takes all; 3–4 players 70% / 30%.</summary>
        public int Prize(int players, int place)
        {
            int pot = Pot(players);
            if (players <= 2) return place == 1 ? pot : 0;
            if (place == 1) return Mathf.RoundToInt(pot * 0.7f);
            if (place == 2) return pot - Mathf.RoundToInt(pot * 0.7f);
            return 0;
        }
    }

    public static class Desks
    {
        /// <summary>Guests can sit at the first two desks only.</summary>
        public const int GuestDesks = 2;
        /// <summary>Daily pocket money when you're short of marks.</summary>
        public const int PocketMoney = 100;

        /// <summary>The desk of the match being played (null offline: the old tuition bench).</summary>
        public static DeskDef Active { get; set; }

        /// <summary>The look used offline (ladder, practice, pass and play).</summary>
        public static DeskDef Home => All[1];

        public static DeskDef ActiveOrHome => Active ?? Home;

        public static int RoundsToWin => Active != null ? Active.roundsToWin : 3;
        public static float OnlineTurnSeconds => Active != null ? Active.turnSeconds : 30f;

        static List<DeskDef> all;
        public static List<DeskDef> All => all ??= Make();

        public static DeskDef ById(string id)
        {
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        public static string TierName(DeskTier t) => t switch
        {
            DeskTier.Silver => "SILVER",
            DeskTier.Gold => "GOLD",
            DeskTier.Platinum => "PLATINUM",
            DeskTier.Diamond => "DIAMOND",
            _ => "CLASSIC",
        };

        public static Color TierColour(DeskTier t) => t switch
        {
            DeskTier.Silver => Pal.H("#9AA3AE"),
            DeskTier.Gold => Pal.H("#C99A2E"),
            DeskTier.Platinum => Pal.H("#7F8C9A"),
            DeskTier.Diamond => Pal.H("#5B8DEF"),
            _ => Pal.H("#8F7656"),
        };

        /// <summary>1,20,000 style (Indian grouping).</summary>
        public static string Money(int v)
        {
            if (v < 1000) return v.ToString();
            string s = v.ToString();
            string last3 = s.Substring(s.Length - 3);
            string rest = s.Substring(0, s.Length - 3);
            var parts = new List<string>();
            while (rest.Length > 2) { parts.Insert(0, rest.Substring(rest.Length - 2)); rest = rest.Substring(0, rest.Length - 2); }
            if (rest.Length > 0) parts.Insert(0, rest);
            return string.Join(",", parts) + "," + last3;
        }

        // ---------- who can sit where ----------

        public enum Block { None, Level, Marks, Guest }

        /// <summary>Why this player can't sit at the desk (None = they can).</summary>
        public static Block Check(SaveData s, DeskDef d, bool forRoom = false)
        {
            if (OnlineServices.IsGuest(s) && d.index >= GuestDesks) return Block.Guest;
            if (!forRoom && OnlineLevel.Level(s.onlineXp) < d.level) return Block.Level;
            if (s.marks < d.fee) return Block.Marks;
            return Block.None;
        }

        /// <summary>The best desk this player can afford and has opened (for "try a cheaper desk").</summary>
        public static DeskDef BestAffordable(SaveData s)
        {
            DeskDef best = All[0];
            foreach (var d in All) if (Check(s, d) == Block.None) best = d;
            return best;
        }

        public static bool PocketMoneyReady(SaveData s) => s.lastPocketDay != System.DateTime.Now.ToString("yyyy-MM-dd");

        public static void TakePocketMoney(SaveData s)
        {
            if (!PocketMoneyReady(s)) return;
            s.lastPocketDay = System.DateTime.Now.ToString("yyyy-MM-dd");
            s.marks += PocketMoney;
            s.Save();
        }

        // ---------- the 28 desks ----------

        static readonly int[] Fees =
        {
            0, 50, 100, 250, 500, 1000, 2500, 5000,
            7500, 10000, 12500, 15000, 20000, 25000,
            30000, 35000, 40000, 50000, 60000, 70000,
            80000, 90000, 100000, 120000,
            140000, 160000, 180000, 200000,
        };

        static readonly int[] Levels = { 0, 0, 3, 6, 10, 15, 22, 30 };

        static List<DeskDef> Make()
        {
            var list = new List<DeskDef>();
            void Add(string id, string name, string where, DeskTier tier, DeskSurface surf, string top, string dark, string edge,
                FloorKind floor, string floorA, string floorB, string wall, string rug = null, string leg = "#2A2A2A",
                float legMetal = 0f, bool round = false, bool carvings = false, string inset = null, string veins = null, string light = null)
            {
                int i = list.Count;
                var d = new DeskDef
                {
                    index = i, id = id, name = name, where = where, tier = tier, fee = Fees[i],
                    level = i < Levels.Length ? Levels[i] : 34 + 4 * (i - Levels.Length),
                    turnSeconds = i < 4 ? 60f : i < 10 ? 45f : i < 18 ? 30f : 20f,
                    roundsToWin = i < 6 ? 2 : 3,
                    size = i < 18 ? 1f : i < 24 ? 0.93f : 0.86f,
                    surface = surf, top = Pal.H(top), topDark = Pal.H(dark), edge = Pal.H(edge),
                    floor = floor, floorA = Pal.H(floorA), floorB = Pal.H(floorB), wall = Pal.H(wall),
                    rug = rug != null ? Pal.H(rug) : (Color?)null, leg = Pal.H(leg), legMetal = legMetal, roundLegs = round,
                    carvings = carvings, inset = inset != null ? Pal.H(inset) : (Color?)null, veins = veins != null ? Pal.H(veins) : (Color?)null,
                };
                if (light != null) d.light = Pal.H(light);
                list.Add(d);
            }

            // Classic: school and tuition desks.
            Add("primary", "Primary School", "free friendly desk · no marks at stake", DeskTier.Classic, DeskSurface.OldWood, "#A88B62", "#6A4A2A", "#7E5F3E",
                FloorKind.Tiles, "#D9D3C4", "#8D8778", "#E6E0D1", carvings: true);
            Add("tuition", "Tuition Centre", "the old bench at Sharma Sir's", DeskTier.Classic, DeskSurface.OldWood, "#8F7656", "#5A3A1C", "#6E5034",
                FloorKind.Tiles, "#D9D3C4", "#5F5A4F", "#E6E0D1", carvings: true);
            Add("coaching", "Coaching Class", "formica top, steel frame", DeskTier.Classic, DeskSurface.Laminate, "#A9B8A2", "#7C8C77", "#2F3431",
                FloorKind.Tiles, "#CFCBC0", "#7D796E", "#DCE3DA");
            Add("library", "School Library", "shh! polished oak", DeskTier.Classic, DeskSurface.Polished, "#B07A44", "#6E4422", "#5C3A1E",
                FloorKind.Planks, "#9C7752", "#6B4B30", "#E2D6BC");
            Add("staff", "Staff Room", "where the teachers sit", DeskTier.Classic, DeskSurface.Laminate, "#D6C8A6", "#A89A78", "#5C4330",
                FloorKind.Tiles, "#C9C6BE", "#8A877F", "#E8E1CF");
            Add("principal", "Principal's Office", "mahogany under glass", DeskTier.Classic, DeskSurface.Glass, "#6E2E1C", "#3C160C", "#3E1A0E",
                FloorKind.Planks, "#6E4A2E", "#4A2F1C", "#E9DFC9", rug: "#7A1E22", inset: "#6E2E1C", leg: "#3A2014");
            Add("exam", "Board Exam Hall", "silence. one desk each.", DeskTier.Classic, DeskSurface.Plywood, "#BE9F70", "#8C6D44", "#5E4630",
                FloorKind.Tiles, "#D4D2CB", "#9A978F", "#EEEAE0");
            Add("topper", "Topper's Hall of Fame", "walnut and green leather", DeskTier.Classic, DeskSurface.Leather, "#5A3A22", "#2E1C10", "#3A2414",
                FloorKind.Planks, "#7A5636", "#4E3420", "#E4D9C2", rug: "#2F4F3A", inset: "#2F5D46", leg: "#2A1A10");

            // Silver: the school's special rooms.
            Add("science", "Science Lab", "black epoxy bench", DeskTier.Silver, DeskSurface.Laminate, "#2A2E30", "#1A1D1F", "#1E2224",
                FloorKind.Tiles, "#E6E8E8", "#A8ADAE", "#DDE6E8", rug: "#3D5A6C", leg: "#C9CED6", legMetal: .8f, round: true);
            Add("computer", "Computer Lab", "light grey, cable cut-out", DeskTier.Silver, DeskSurface.Laminate, "#CDD1D5", "#9EA4AA", "#55606A",
                FloorKind.Tiles, "#BFD0DE", "#7F93A5", "#E3EAF0", rug: "#2E4C6E", leg: "#C9CED6", legMetal: .8f, round: true);
            Add("art", "Art Room", "paint everywhere", DeskTier.Silver, DeskSurface.Paint, "#C9AE82", "#8E7350", "#6A5038",
                FloorKind.Concrete, "#BDB8AE", "#8F8A80", "#F1E9DA", rug: "#A0522D", leg: "#C9CED6", legMetal: .7f, round: true);
            Add("music", "Music Room", "rosewood, like the harmonium", DeskTier.Silver, DeskSurface.Polished, "#5A2A1A", "#2C120A", "#2E140A",
                FloorKind.Carpet, "#6E2632", "#4E1622", "#E8D8C8", rug: "#C9A227", leg: "#C9CED6", legMetal: .8f, round: true);
            Add("canteen", "School Canteen", "steel top, samosa crumbs", DeskTier.Silver, DeskSurface.Steel, "#B9BFC5", "#8D949B", "#7C838A",
                FloorKind.Checker, "#D8D2C2", "#8C3A2E", "#F0E6CF", rug: "#2F6B4F", leg: "#C9CED6", legMetal: .9f, round: true);
            Add("sports", "Sports Room", "maple, court lines", DeskTier.Silver, DeskSurface.Court, "#D3A46A", "#A87A44", "#7A532C",
                FloorKind.Concrete, "#4E7A5A", "#3A5E44", "#E5ECE2", rug: "#1F5FA8", leg: "#C9CED6", legMetal: .8f, round: true);

            // Gold: hostel and college.
            Add("hostel", "Hostel Study Room", "late nights, older carvings", DeskTier.Gold, DeskSurface.OldWood, "#6F5437", "#3E2814", "#4A3420",
                FloorKind.Tiles, "#BDB4A2", "#6E6656", "#D8CDB6", rug: "#6B2737", leg: "#D9B44A", legMetal: .75f, round: true, carvings: true, light: "#FFF1DA");
            Add("railway", "Railway Waiting Room", "teak bench, platform no. 1", DeskTier.Gold, DeskSurface.Polished, "#8A5A2B", "#55331A", "#4A2C14",
                FloorKind.Checker, "#E2D6BC", "#7E2E22", "#EADBB8", rug: "#1F4E5F", leg: "#D9B44A", legMetal: .75f, round: true, light: "#FFF1DA");
            Add("kota", "Kota Coaching Hub", "white laminate, 500 students", DeskTier.Gold, DeskSurface.Laminate, "#E8E4DA", "#C2BCAE", "#3C4148",
                FloorKind.Tiles, "#D6D6D2", "#9C9C96", "#EEF0EC", rug: "#8A1F2B", leg: "#D9B44A", legMetal: .75f, round: true);
            Add("collegecanteen", "College Canteen", "red top, chai rings", DeskTier.Gold, DeskSurface.Laminate, "#A2392F", "#6E2219", "#3A1A14",
                FloorKind.Checker, "#E8DFC8", "#2E2A26", "#F2E3C4", rug: "#2D4E3C", leg: "#D9B44A", legMetal: .75f, round: true);
            Add("collegelibrary", "College Library", "walnut, red leather", DeskTier.Gold, DeskSurface.Leather, "#4E321E", "#24160C", "#2E1C10",
                FloorKind.Carpet, "#2E4A3A", "#1E3428", "#E2D4B8", rug: "#7A1E22", inset: "#7A2A24", leg: "#D9B44A", legMetal: .8f, round: true, light: "#FFF1DA");
            Add("rooftop", "Hostel Rooftop", "sun-bleached, under the stars", DeskTier.Gold, DeskSurface.OldWood, "#9A9284", "#625A4C", "#5A5246",
                FloorKind.Concrete, "#8E8A82", "#6A665E", "#2B3550", rug: "#B5651D", leg: "#D9B44A", legMetal: .75f, round: true, carvings: true, light: "#E9EEFF");

            // Platinum: engineering college.
            Add("workshop", "Engineering Workshop", "brushed steel, oil marks", DeskTier.Platinum, DeskSurface.Steel, "#7A8086", "#4E545A", "#3E4348",
                FloorKind.Concrete, "#9A9A94", "#6E6E68", "#D6D8D4", rug: "#C9A227", leg: "#E4E6EA", legMetal: .95f, round: true);
            Add("debate", "Debate Hall", "mahogany and burgundy leather", DeskTier.Platinum, DeskSurface.Leather, "#5E2414", "#2E0E06", "#341208",
                FloorKind.Planks, "#5E3E26", "#3E2616", "#E6DACA", rug: "#24324F", inset: "#6E1E2A", leg: "#E4E6EA", legMetal: .95f, round: true);
            Add("convocation", "Convocation Hall", "dark walnut, blue carpet", DeskTier.Platinum, DeskSurface.Polished, "#3E2616", "#1C0F06", "#22130A",
                FloorKind.Carpet, "#1F2E5A", "#141F40", "#E8E2D2", rug: "#B08D2E", leg: "#E4E6EA", legMetal: .95f, round: true);
            Add("iit", "IIT Lecture Theatre", "graphite laminate, wood edge", DeskTier.Platinum, DeskSurface.Laminate, "#3A3F45", "#24282C", "#7A4E2A",
                FloorKind.Planks, "#8A6440", "#5E4024", "#DAD8D0", rug: "#8A1F2B", leg: "#E4E6EA", legMetal: .95f, round: true);

            // Diamond: the big stages.
            Add("olympiad", "Olympiad Arena", "black glass, gold flecks", DeskTier.Diamond, DeskSurface.Glass, "#14161A", "#08090B", "#101215",
                FloorKind.Stage, "#1A1C22", "#2A2E38", "#1E2230", rug: "#3B2A6E", inset: "#14161A", leg: "#DDEBFF", legMetal: 1f, round: true, light: "#F2F6FF");
            Add("quizshow", "TV Quiz Show", "studio blue, bright lights", DeskTier.Diamond, DeskSurface.Glass, "#1E3A8A", "#0F1E4A", "#0E1A3C",
                FloorKind.Stage, "#14182A", "#2A3460", "#141A36", rug: "#C9A227", inset: "#1E3A8A", leg: "#DDEBFF", legMetal: 1f, round: true, light: "#F2F6FF");
            Add("pressbox", "Stadium Press Box", "black marble, gold veins", DeskTier.Diamond, DeskSurface.Marble, "#1C1C1F", "#0C0C0E", "#121214",
                FloorKind.Carpet, "#26262C", "#18181C", "#2C3140", rug: "#7A1E22", veins: "#C9A54A", leg: "#DDEBFF", legMetal: 1f, round: true, light: "#F2F6FF");
            Add("palace", "Champions' Palace", "white marble, gold everywhere", DeskTier.Diamond, DeskSurface.Marble, "#CFC8BA", "#857C6C", "#C9C1B2",
                FloorKind.Checker, "#EFEAE0", "#2A2622", "#F3EAD6", rug: "#8E1B2A", veins: "#7E6024", leg: "#D9B44A", legMetal: 1f, round: true, light: "#FFF6E6");

            return list;
        }
    }

    /// <summary>Online level from XP. Each level needs a little more than the last.</summary>
    public static class OnlineLevel
    {
        public static int Need(int level) => 60 + 15 * level;

        public static int Level(int xp)
        {
            int lv = 1;
            while (xp >= Need(lv)) { xp -= Need(lv); lv++; if (lv > 999) break; }
            return lv;
        }

        /// <summary>XP into the current level and the XP that level needs.</summary>
        public static (int into, int need) Progress(int xp)
        {
            int lv = 1;
            while (xp >= Need(lv)) { xp -= Need(lv); lv++; if (lv > 999) break; }
            return (xp, Need(lv));
        }

        /// <summary>XP for one online match at a desk: more for winning and for higher desks.</summary>
        public static int ForMatch(DeskDef d, int place, int players)
        {
            int baseXp = place == 1 ? 60 : place == 2 && players > 2 ? 35 : 20;
            float mul = 1f + (d != null ? d.index : 0) * 0.15f;
            return Mathf.RoundToInt(baseXp * mul);
        }
    }
}
