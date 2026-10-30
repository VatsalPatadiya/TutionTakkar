using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>One bot seat assigned to fill an empty Quick Match seat after the search window closes.</summary>
    public struct BotSeat
    {
        public string name;
        public string penId;
        public BotProfile skill;
    }

    /// <summary>
    /// Bot seats for Quick Match: a synthetic seat id scheme that never collides with a real
    /// Lobby player id, a fake-name pool so a bot's card never shows a Batch Ladder rival's name,
    /// and a desk-tier -> rival-skill mapping that reuses the game's existing bot skill curve.
    /// </summary>
    public static class OnlineBot
    {
        const string Prefix = "bot:";

        public static string BotId(int index) => Prefix + index;
        public static bool IsBotId(string id) => id != null && id.StartsWith(Prefix);
        public static int IndexFromId(string id) => int.Parse(id.Substring(Prefix.Length));

        static readonly string[] Names =
        {
            "RiyaFlicks", "SahilSpins", "AnjuAims", "KunalKnocks", "PriyaPens", "RohanRolls",
            "NehaNudges", "VivekVanish", "TaraTakes", "AmanArcs", "DivyaDarts", "KaranClacks",
            "SnehaSwipes", "ManishMarks", "PoojaPulls", "RahulRicochet", "IshaInk", "YashYeets",
            "KritiKicks", "SumitSnaps", "AartiAces", "DevRebounds",
        };

        /// <summary>Indices into Catalog.Rivals (weakest..strongest) for each desk tier.</summary>
        static readonly (int from, int to)[] TierRivalRange =
        {
            (0, 1), // Classic
            (2, 3), // Silver
            (4, 5), // Gold
            (6, 7), // Platinum
            (8, 9), // Diamond
        };

        static RivalDef PickRival(DeskTier tier)
        {
            var range = TierRivalRange[Mathf.Clamp((int)tier, 0, TierRivalRange.Length - 1)];
            int i = UnityEngine.Random.Range(range.from, range.to + 1);
            return Catalog.Rivals[i];
        }

        /// <summary>Picks `count` bot seats for this desk tier, each with a distinct fake name.</summary>
        public static List<BotSeat> PickSeats(int count, DeskTier tier)
        {
            var seats = new List<BotSeat>(count);
            var used = new HashSet<string>();
            while (seats.Count < count)
            {
                string name = Names[UnityEngine.Random.Range(0, Names.Length)];
                if (!used.Add(name)) continue;
                var rival = PickRival(tier);
                seats.Add(new BotSeat { name = name, penId = rival.penId, skill = rival.bot });
            }
            return seats;
        }

        // ---------- wire format: "name,penId,aim,power,caution,aggression,mistake;..." ----------

        public static string EncodeAll(List<BotSeat> seats)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < seats.Count; i++)
            {
                if (i > 0) sb.Append(';');
                var s = seats[i];
                sb.Append(s.name).Append(',').Append(s.penId).Append(',')
                  .Append(F(s.skill.aimNoiseDeg)).Append(',').Append(F(s.skill.powerNoise)).Append(',')
                  .Append(F(s.skill.caution)).Append(',').Append(F(s.skill.aggression)).Append(',').Append(F(s.skill.mistakeChance));
            }
            return sb.ToString();
        }

        public static List<BotSeat> DecodeAll(string data)
        {
            var seats = new List<BotSeat>();
            if (string.IsNullOrEmpty(data)) return seats;
            foreach (var record in data.Split(';'))
            {
                var v = record.Split(',');
                if (v.Length < 7) continue;
                seats.Add(new BotSeat
                {
                    name = v[0],
                    penId = v[1],
                    skill = new BotProfile(P(v[2]), P(v[3]), P(v[4]), P(v[5]), P(v[6])),
                });
            }
            return seats;
        }

        static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static float P(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
