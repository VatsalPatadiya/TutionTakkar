using System;
using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>One line on the "marks earned" card after a match.</summary>
    public struct RewardLine
    {
        public string label;
        public string note;
        public int amount;          // marks added (for a multiplier line: the marks it added)
        public bool isMultiplier;   // shown as "×2" instead of "+N"
    }

    public class RewardResult
    {
        public readonly List<RewardLine> lines = new List<RewardLine>();
        public int total;
        public bool doubled;
    }

    /// <summary>
    /// Marks, pen prices and ink. Every number for the pen economy lives here.
    ///
    /// Earning: practice win 10/20/35 by difficulty, ladder win = the rival's reward
    /// (half on a rematch) plus their pen, +5 per rival pen knocked off, the first win
    /// of each day pays double, a loss pays 2 for trying. Pass &amp; Play pays nothing
    /// (two players on one phone could farm marks).
    ///
    /// Ink: every pen except the Starter Click holds ink for 20 offline matches. Each
    /// offline match uses 1. An empty pen still plays, but with Starter Click stats,
    /// until it is refilled with marks (a full tank costs about a third of its price).
    /// </summary>
    public static class Economy
    {
        public const int InkTank = 20;
        public const int InkLowAt = 3;
        public const int LossConsolation = 2;
        public const int KnockOffBonus = 5;
        public const int OnlineWin = 30;
        /// <summary>Second place in a 3–4 player online match.</summary>
        public const int OnlineSecond = 10;

        static readonly int[] PracticeWin = { 10, 20, 35 };

        static readonly Dictionary<string, int> Prices = new Dictionary<string, int>
        {
            { "zippy07", 150 },
            { "trigrip", 200 },
            { "gelglide", 300 },
            { "fatcap", 300 },
            { "steel", 350 },
            { "rubber", 400 },
            { "twintone", 450 },
            { "featherlite", 500 },
            { "chunky", 600 },
        };

        /// <summary>The Golden Nib is never sold; this is only used to price its refills.</summary>
        const int GoldenValue = 750;

        /// <summary>Classic pens from the price list, plus the 600 generated shop pens.</summary>
        public static bool ForSale(string penId) => Prices.ContainsKey(penId) || PenParts.GeneratedPen(penId) != null;

        public static int Price(string penId)
        {
            if (Prices.TryGetValue(penId, out var p)) return p;
            var g = PenParts.GeneratedPen(penId);
            if (g != null) return g.price;
            if (PenParts.TryDecode(penId, out var spec)) return PenParts.Cost(spec);
            return 0;
        }

        public static int PracticeReward(int level) => PracticeWin[Mathf.Clamp(level, 0, PracticeWin.Length - 1)];

        // ---------- ink ----------

        public static bool UsesInk(string penId) => penId != Catalog.StarterId;

        static int Value(string penId)
        {
            if (penId == "golden") return GoldenValue;
            int p = Price(penId);
            return p > 0 ? p : 300;
        }

        /// <summary>Cost of a full tank, rounded to 5 marks.</summary>
        public static int FullRefillCost(string penId) => Mathf.Max(5, Mathf.RoundToInt(Value(penId) / 3f / 5f) * 5);

        /// <summary>Cost to top a pen up from its current ink to a full tank (0 when full).</summary>
        public static int RefillCost(string penId, int inkLeft)
        {
            int missing = Mathf.Clamp(InkTank - inkLeft, 0, InkTank);
            if (missing == 0) return 0;
            return Mathf.Max(5, Mathf.CeilToInt(FullRefillCost(penId) * missing / (float)InkTank / 5f) * 5);
        }

        // ---------- rewards ----------

        static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        public static bool DoubleAvailable(SaveData save) => save.lastDoubleDay != Today;

        /// <summary>
        /// Works out the marks for a finished offline match and marks the daily double as
        /// used when it applies. Does not add the marks to the save; the caller does.
        /// </summary>
        public static RewardResult ForMatch(MatchSetup s, bool humanWon, int knockOffs, bool firstLadderWin, SaveData save,
            int batchBonus = 0, int ownedPenBonus = 0)
        {
            var r = new RewardResult();
            if (s.mode == MatchMode.PassAndPlay) return r;

            if (!humanWon)
            {
                r.lines.Add(new RewardLine { label = "For trying", note = "lost the match", amount = LossConsolation });
                r.total = LossConsolation;
                return r;
            }

            int win;
            string note;
            if (s.mode == MatchMode.Ladder && s.rival != null)
            {
                win = firstLadderWin ? s.rival.reward : s.rival.reward / 2;
                note = firstLadderWin ? "beat " + s.rival.name : s.rival.name + " rematch";
            }
            else
            {
                win = PracticeReward(s.practiceLevel);
                note = Catalog.PracticeNames[Mathf.Clamp(s.practiceLevel, 0, Catalog.PracticeNames.Length - 1)] + " bot";
            }
            r.lines.Add(new RewardLine { label = "Match win", note = note, amount = win });

            int sub = win;
            if (ownedPenBonus > 0)
            {
                r.lines.Add(new RewardLine { label = "Pen bonus", note = "you already have their pen", amount = ownedPenBonus });
                sub += ownedPenBonus;
            }
            if (batchBonus > 0)
            {
                r.lines.Add(new RewardLine { label = "Batch complete", note = "all 10 rivals beaten", amount = batchBonus });
                sub += batchBonus;
            }
            if (knockOffs > 0)
            {
                int k = knockOffs * KnockOffBonus;
                r.lines.Add(new RewardLine { label = "Knock-offs", note = knockOffs + (knockOffs == 1 ? " pen" : " pens") + " off × " + KnockOffBonus, amount = k });
                sub += k;
            }

            if (DoubleAvailable(save))
            {
                r.doubled = true;
                r.lines.Add(new RewardLine { label = "First win today", note = "resets at midnight", amount = sub, isMultiplier = true });
                sub *= 2;
                save.lastDoubleDay = Today;
            }

            r.total = sub;
            return r;
        }

        /// <summary>The cheapest pen in the shop the player doesn't own yet (null when they own them all).</summary>
        public static PenDef NextPenGoal(SaveData save)
        {
            PenDef best = null;
            foreach (var p in Catalog.Pens)
            {
                if (!Prices.ContainsKey(p.id) || save.Owns(p.id)) continue;
                if (best == null || Price(p.id) < Price(best.id)) best = p;
            }
            return best;
        }
    }
}
