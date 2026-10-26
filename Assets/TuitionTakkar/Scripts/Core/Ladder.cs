using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// The endless Batch Ladder, in batches of 10 rivals. Batch 1 is the hand-made one
    /// (Monty … Vikram). From Batch 2 on, rivals are made from a seed, so every player meets the
    /// same rivals in the same batch: a name, a look, a play style and a pen from the 600 shop pens.
    /// Rival 10 of every batch is the boss. Play goes forward only: the next rival opens once
    /// the current one is beaten, and the next batch once all 10 are.
    /// </summary>
    public static class Ladder
    {
        public const int PerBatch = 10;

        /// <summary>0-based batch of a ladder position (0 = Batch 1).</summary>
        public static int BatchOf(int index) => Mathf.Max(0, index) / PerBatch;
        public static int PosInBatch(int index) => Mathf.Max(0, index) % PerBatch;
        public static bool IsBoss(int index) => PosInBatch(index) == PerBatch - 1;

        static readonly string[] Subtitles =
        {
            "Morning batch", "Evening batch", "Weekend batch", "Crash course", "Board exam batch",
            "Topper's batch", "Night owls", "Summer camp", "Olympiad batch", "Hall of fame",
        };

        /// <summary>"Batch 3"</summary>
        public static string BatchName(int batch) => "Batch " + (batch + 1);

        /// <summary>"Weekend batch" (repeats with II, III… after ten batches).</summary>
        public static string Subtitle(int batch)
        {
            string s = Subtitles[batch % Subtitles.Length];
            int round = batch / Subtitles.Length;
            return round == 0 ? s : s + " " + Roman(round + 1);
        }

        static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };

        static readonly Dictionary<int, RivalDef> cache = new Dictionary<int, RivalDef>();

        public static RivalDef RivalAt(int index)
        {
            index = Mathf.Max(0, index);
            if (index < Catalog.Rivals.Count) return Catalog.Rivals[index];
            if (cache.TryGetValue(index, out var r)) return r;
            r = Make(index);
            cache[index] = r;
            return r;
        }

        /// <summary>The ladder position of a rival (−1 if it isn't one).</summary>
        public static int IndexOf(RivalDef rival)
        {
            int i = Catalog.Rivals.IndexOf(rival);
            if (i >= 0) return i;
            foreach (var kv in cache) if (kv.Value == rival) return kv.Key;
            return -1;
        }

        // ---------- making rivals ----------

        static readonly string[] Names =
        {
            "Aarav", "Ishaan", "Diya", "Kavya", "Rohan", "Ananya", "Vihaan", "Meera", "Aditya", "Priya", "Kunal", "Neha",
            "Siddharth", "Riya", "Harsh", "Pooja", "Yash", "Tanvi", "Dev", "Nisha", "Rahul", "Sneha", "Aman", "Isha",
            "Varun", "Kriti", "Nikhil", "Aditi", "Rehan", "Simran", "Kabeer", "Anika", "Parth", "Mahi", "Shaurya", "Avni",
            "Om", "Jiya", "Rudra", "Saanvi", "Arnav", "Myra", "Laksh", "Navya", "Reyansh", "Kiara", "Atharv", "Pari",
            "Ayaan", "Ira", "Dhruv", "Trisha", "Krish", "Shreya", "Veer", "Aisha", "Manav", "Gauri", "Raghav", "Tara",
            "Faizan", "Mehak", "Sahil", "Bhavya", "Ritvik", "Jhanvi", "Pranav", "Ishita", "Chirag", "Divya", "Gautam", "Hina",
            "Jatin", "Kajal", "Lalit", "Mansi", "Naveen", "Ojas", "Payal", "Rishi", "Sakshi", "Tushar", "Urvi", "Vedant",
        };

        static readonly string[] Avatars =
        {
            "#8ED1C6", "#FFB8C9", "#F3C98B", "#A9D8F5", "#C6B4E8", "#F5B7A1", "#BFE3B4", "#FFE0A3", "#B9D3E8", "#E8D49A",
            "#F7C6E0", "#C9E4A6", "#FFD1A9", "#B5D8F0",
        };

        enum Style { Attacker, Wall, Spinner, AllRounder }

        static readonly string[][] Blurbs =
        {
            new[] { "Goes for the kill every single flick.", "Hits first, asks questions never.", "Loves sending pens to the floor.", "Never plays it safe. Ever." },
            new[] { "Parks the pen in the middle and waits.", "You won't push this one off easily.", "Plays slow, plays safe, plays to win.", "Defends like it's the board exam." },
            new[] { "Grabs near the cap. Everything spins.", "Curving shots you won't see coming.", "Spins your pen like a top.", "Loves the trick shot." },
            new[] { "Good at everything. Annoyingly.", "Reads the desk like a textbook.", "Calm, careful, and very good.", "Never panics near the edge." },
        };

        static readonly string[] Lose =
        {
            "rematch… oh wait, there are no rematches.", "okay okay, you're good.", "the desk was tilted. definitely.", "I'll tell everyone I let you win.",
            "fine. take the pen.", "not bad, new kid.", "my hand slipped!", "hmm. respect.",
        };

        static readonly string[] Win =
        {
            "back to the last bench with you.", "too slow, yaar.", "do your homework first.", "off the desk, off the batch.",
            "is that all you've got?", "come back when you've practised.", "easy, like a Monday test.", "next!",
        };

        static RivalDef Make(int index)
        {
            int batch = BatchOf(index), pos = PosInBatch(index);
            bool boss = pos == PerBatch - 1;

            // Ten different names per batch: shuffle the list with the batch's seed.
            var order = new List<string>(Names);
            var shuffle = new System.Random(7000 + batch * 131);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = shuffle.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            string name = order[pos];

            var rng = new System.Random(9000 + index * 7919);
            var style = (Style)rng.Next(4);

            // Skill: better each rival, up to a fair best (bots never get perfect). Batch 1 ends near aim 2.5°.
            float t = Mathf.Clamp01((batch - 1) / 6f + pos / 60f);
            float aim = Mathf.Lerp(3.8f, 2.4f, t) - (boss ? 0.4f : 0f);
            float power = Mathf.Lerp(.08f, .05f, t) - (boss ? .01f : 0f);
            float mistake = Mathf.Lerp(.06f, .02f, t) - (boss ? .01f : 0f);
            float caution, aggression;
            switch (style)
            {
                case Style.Attacker: caution = .3f; aggression = .9f; break;
                case Style.Wall: caution = .85f; aggression = .35f; break;
                case Style.Spinner: caution = .5f; aggression = .7f; break;
                default: caution = .65f; aggression = .65f; break;
            }

            // Their pen: a shop pen whose rarity rises with the batch (boss: epic or legendary).
            PenRarity want = boss ? (batch >= 3 ? PenRarity.Legendary : PenRarity.Epic)
                : rng.NextDouble() < Mathf.Min(.25f + batch * .08f, .7f) ? PenRarity.Rare : PenRarity.Common;
            if (!boss && batch >= 4 && rng.NextDouble() < .3) want = PenRarity.Epic;
            var pens = PenParts.Generated;
            PenDef pen = null;
            int start = rng.Next(pens.Count);
            for (int k = 0; k < pens.Count && pen == null; k++)
            {
                var p = pens[(start + k) % pens.Count];
                if (p.rarity == want) pen = p;
            }
            pen ??= pens[start];

            int reward = (60 + batch * 12 + pos * 4) * (boss ? 2 : 1);

            return new RivalDef
            {
                id = "lad" + index,
                name = name,
                initial = name.Substring(0, 1),
                avatar = Pal.H(Avatars[rng.Next(Avatars.Length)]),
                blurb = boss ? "The " + Subtitle(batch).ToLowerInvariant() + " boss. " + Blurbs[(int)style][rng.Next(4)]
                             : Blurbs[(int)style][rng.Next(4)],
                penId = pen.id,
                reward = reward,
                quipWhenTheyLose = Lose[rng.Next(Lose.Length)],
                quipWhenTheyWin = Win[rng.Next(Win.Length)],
                bot = new BotProfile(Mathf.Max(2.2f, aim), Mathf.Max(.045f, power), caution, aggression, Mathf.Max(.015f, mistake)),
            };
        }

        /// <summary>Marks for clearing a whole batch (paid with the boss win).</summary>
        public static int BatchBonus(int batch) => 100 + batch * 50;
    }
}
