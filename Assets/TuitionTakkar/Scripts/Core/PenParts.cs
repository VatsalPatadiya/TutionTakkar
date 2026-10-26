using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TuitionTakkar
{
    public enum PenFinish { Glossy, Matte, Frost, Metal, Glitter, Holo, Gold }
    public enum PenPattern { None, Stripes, Dots, Checks, Stars, Camo }
    public enum PenRarity { Common, Rare, Epic, Legendary }

    /// <summary>
    /// Pens built from parts. Build parts (type, barrel, grip, cap) decide how a pen plays;
    /// looks (colours, finish, pattern) only change how it looks. Every pen gets the same
    /// kind of stats (weight, grip, spin) from its parts, capped by a power budget so no pen
    /// is simply the best: more of one stat means less of another.
    ///
    /// The same parts make the 600 shop pens (<see cref="Generated"/>) and the pens players
    /// design in the workshop. A custom pen's id is a short code of its parts ("c.…"), so every
    /// phone in an online match rebuilds exactly the same pen and physics from the id alone.
    /// </summary>
    public static class PenParts
    {
        // ---------- part tables ----------

        public class Part
        {
            public string name, note;
            public float w, g, s;   // stat changes (weight, grip, spin)
            public int price;
            public float comOffset;
        }

        public class BodyType : Part
        {
            public string key;      // short word used in names ("Gel", "Click", ...)
            public float length, radius, bounce = .38f;
            public bool hasClip = true;
            public PenShape shape = PenShape.Round;
        }

        public static readonly BodyType[] Types =
        {
            new BodyType { key = "Click", name = "Click pen", note = "balanced all-rounder", length = .145f, radius = .0052f, w = .5f, g = .5f, s = .5f, price = 120 },
            new BodyType { key = "Ballpoint", name = "Ballpoint", note = "light, spins well", length = .148f, radius = .0050f, w = .42f, g = .45f, s = .62f, price = 100 },
            new BodyType { key = "Gel", name = "Gel pen", note = "fat and grippy", length = .142f, radius = .0058f, w = .55f, g = .62f, s = .4f, price = 160 },
            new BodyType { key = "Slim", name = "Use-and-throw", note = "featherweight, slides far", length = .150f, radius = .0045f, w = .28f, g = .38f, s = .8f, price = 80, hasClip = false },
            new BodyType { key = "Fountain", name = "Fountain pen", note = "heavy and steady", length = .138f, radius = .0060f, w = .72f, g = .5f, s = .32f, price = 260, bounce = .32f },
            new BodyType { key = "Marker", name = "Marker", note = "short, fat, hard to move", length = .130f, radius = .0068f, w = .85f, g = .66f, s = .2f, price = 220, hasClip = false },
            new BodyType { key = "Tri", name = "Triangle pen", note = "flat sides, never rolls", length = .145f, radius = .0055f, w = .55f, g = .8f, s = .28f, price = 180, hasClip = false, shape = PenShape.Triangle },
        };

        public static readonly Part[] Barrels =
        {
            new Part { name = "Plastic", note = "standard" },
            new Part { name = "Metal", note = "+weight, −spin", w = .18f, g = -.05f, s = -.1f, price = 120 },
            new Part { name = "Light alloy", note = "+spin, −weight", w = -.1f, s = .14f, price = 100 },
            new Part { name = "Rubber coat", note = "+grip, −spin", w = .04f, g = .18f, s = -.1f, price = 90 },
        };

        public static readonly Part[] Grips =
        {
            new Part { name = "Smooth", note = "slides more", g = -.05f, s = .05f },
            new Part { name = "Ribbed", note = "a little grip", g = .08f, price = 30 },
            new Part { name = "Rubber", note = "grips, spins less", g = .14f, s = -.07f, price = 60 },
        };

        public static readonly Part[] Caps =
        {
            new Part { name = "Plastic", note = "standard" },
            new Part { name = "Metal", note = "+weight, −spin", w = .1f, s = -.05f, price = 70, comOffset = .15f },
            new Part { name = "Big cap", note = "top-heavy wobble", w = .05f, s = .12f, g = -.03f, price = 50, comOffset = .35f },
        };

        public class Look { public string name; public int price; }

        public static readonly Look[] Finishes =
        {
            new Look { name = "Glossy" }, new Look { name = "Matte", price = 20 }, new Look { name = "Frosted", price = 60 },
            new Look { name = "Metallic", price = 150 }, new Look { name = "Glitter", price = 200 }, new Look { name = "Holo", price = 300 },
            new Look { name = "Gold", price = 500 },
        };

        public static readonly Look[] Patterns =
        {
            new Look { name = "Plain" }, new Look { name = "Stripes", price = 40 }, new Look { name = "Dots", price = 40 },
            new Look { name = "Checks", price = 60 }, new Look { name = "Stars", price = 80 }, new Look { name = "Camo", price = 80 },
        };

        public static readonly Color[] Colours =
        {
            Pal.H("#E8762F"), Pal.H("#1F4FA8"), Pal.H("#C8242E"), Pal.H("#1F8F86"), Pal.H("#2E7D4F"), Pal.H("#5B2A86"),
            Pal.H("#E05AA8"), Pal.H("#F2C230"), Pal.H("#7FC4F0"), Pal.H("#F2A7C3"), Pal.H("#8A97A5"), Pal.H("#2B2B2B"),
            Pal.H("#F2F2EE"), Pal.H("#8C5A2B"), Pal.H("#B5E3A1"), Pal.H("#16324F"),
        };

        /// <summary>Most a pen's three stats may add up to, by rarity (custom pens: <see cref="CustomBudget"/>).</summary>
        public static float Budget(PenRarity r) => r == PenRarity.Common ? 1.6f : r == PenRarity.Rare ? 1.72f : r == PenRarity.Epic ? 1.84f : 1.96f;
        public const float CustomBudget = 1.84f;
        /// <summary>The power bar's full scale.</summary>
        public const float MaxPower = 2f;

        // ---------- one pen's parts ----------

        [Serializable]
        public struct Spec
        {
            public int type, barrel, grip, cap, body, capColour, finish, pattern;
            public string name;

            public PenFinish Finish => (PenFinish)finish;
            public PenPattern Pattern => (PenPattern)pattern;
        }

        public struct Stats
        {
            public float weight, grip, spin;
            public bool capped;     // the budget cut the stats back
            public float Power => weight + grip + spin;
        }

        public static Stats StatsOf(Spec p, float budget)
        {
            var t = Types[Mathf.Clamp(p.type, 0, Types.Length - 1)];
            var b = Barrels[Mathf.Clamp(p.barrel, 0, Barrels.Length - 1)];
            var gr = Grips[Mathf.Clamp(p.grip, 0, Grips.Length - 1)];
            var c = Caps[Mathf.Clamp(p.cap, 0, Caps.Length - 1)];
            var st = new Stats
            {
                weight = Mathf.Clamp(t.w + b.w + gr.w + c.w, .1f, 1f),
                grip = Mathf.Clamp(t.g + b.g + gr.g + c.g, .1f, 1f),
                spin = Mathf.Clamp(t.s + b.s + gr.s + c.s, .1f, 1f),
            };
            float sum = st.Power;
            if (sum > budget)
            {
                // Over budget: shave every stat by the same share, keeping the pen's character.
                float k = budget / sum;
                st.weight *= k; st.grip *= k; st.spin *= k;
                st.capped = true;
            }
            return st;
        }

        /// <summary>Coins to make this pen in the workshop: parts + looks + a premium for power.</summary>
        public static int Cost(Spec p, out int parts, out int looks, out int power)
        {
            parts = Types[p.type].price + Barrels[p.barrel].price + Grips[p.grip].price + Caps[p.cap].price;
            looks = Finishes[p.finish].price + Patterns[p.pattern].price;
            var st = StatsOf(p, CustomBudget);
            power = Mathf.Max(0, Mathf.RoundToInt((st.Power - 1.4f) * 900f / 10f) * 10);
            return parts + looks + power;
        }

        public static int Cost(Spec p) => Cost(p, out _, out _, out _);

        public static PenDef Build(Spec p, string id, PenRarity rarity, float budget)
        {
            var t = Types[Mathf.Clamp(p.type, 0, Types.Length - 1)];
            var st = StatsOf(p, budget);
            var finish = p.Finish;
            Color body = Colours[Mathf.Clamp(p.body, 0, Colours.Length - 1)];
            Color cap = Colours[Mathf.Clamp(p.capColour, 0, Colours.Length - 1)];
            if (p.cap == 1) cap = Pal.H("#B7C0C7");   // metal cap
            if (finish == PenFinish.Gold) { body = Pal.H("#D4A72C"); }
            if (finish == PenFinish.Frost) body = Color.Lerp(body, Color.white, 0.55f);

            var d = new PenDef
            {
                id = id,
                name = string.IsNullOrEmpty(p.name) ? t.name : p.name,
                color = body,
                capColor = cap,
                gripColor = p.grip == 2 ? Pal.H("#2B2B2B") : p.grip == 1 ? Color.Lerp(body, Color.black, .25f) : Pal.H("#E9E4D8"),
                length = t.length,
                radius = t.radius,
                shape = t.shape,
                hasClip = t.hasClip && p.cap != 2,
                bounce = t.bounce,
                // The same mapping the classic pens follow: weight -> mass, grip -> friction, spin -> less damping.
                mass = Mathf.Lerp(.0085f, .0235f, st.weight),
                friction = Mathf.Lerp(.18f, .62f, st.grip),
                angularDamping = Mathf.Lerp(1.25f, .5f, st.spin),
                comOffset = Caps[Mathf.Clamp(p.cap, 0, Caps.Length - 1)].comOffset,
                statWeight = st.weight,
                statGrip = st.grip,
                statSpin = st.spin,
                finish = finish,
                pattern = p.Pattern,
                rarity = rarity,
                typeName = t.name,
                spec = p,
            };
            d.traits = Trait(st) + " · " + t.name.ToLowerInvariant();
            d.blurb = t.note + (finish != PenFinish.Glossy ? ", " + Finishes[p.finish].name.ToLowerInvariant() + " finish" : "")
                + (p.pattern != 0 ? ", " + Patterns[p.pattern].name.ToLowerInvariant() : "") + ".";
            return d;
        }

        static string Trait(Stats s)
        {
            if (s.weight >= s.grip && s.weight >= s.spin) return s.weight > .7f ? "Heavy hitter" : "Solid";
            if (s.grip >= s.spin) return s.grip > .7f ? "Parks hard" : "Grippy";
            return s.spin > .7f ? "Big spin" : "Quick";
        }

        // ---------- custom pens: id = code ----------

        public const string CustomPrefix = "c.";

        public static bool IsCustom(string id) => id != null && id.StartsWith(CustomPrefix);

        /// <summary>"c.TBGCbcFP.Name" with one base-36 digit per part (colours: two).</summary>
        public static string Encode(Spec p)
        {
            var sb = new StringBuilder(CustomPrefix);
            sb.Append(D(p.type)).Append(D(p.barrel)).Append(D(p.grip)).Append(D(p.cap))
              .Append(D(p.body / 36)).Append(D(p.body % 36)).Append(D(p.capColour / 36)).Append(D(p.capColour % 36))
              .Append(D(p.finish)).Append(D(p.pattern)).Append('.').Append(CleanName(p.name));
            return sb.ToString();
        }

        public static bool TryDecode(string id, out Spec p)
        {
            p = default;
            if (!IsCustom(id) || id.Length < CustomPrefix.Length + 10) return false;
            string c = id.Substring(CustomPrefix.Length);
            int[] v = new int[10];
            for (int i = 0; i < 10; i++) { v[i] = U(c[i]); if (v[i] < 0) return false; }
            p.type = v[0]; p.barrel = v[1]; p.grip = v[2]; p.cap = v[3];
            p.body = v[4] * 36 + v[5]; p.capColour = v[6] * 36 + v[7]; p.finish = v[8]; p.pattern = v[9];
            p.name = c.Length > 11 ? CleanName(c.Substring(11)) : "";
            return p.type < Types.Length && p.barrel < Barrels.Length && p.grip < Grips.Length && p.cap < Caps.Length
                && p.body < Colours.Length && p.capColour < Colours.Length && p.finish < Finishes.Length && p.pattern < Patterns.Length;
        }

        static char D(int v) => "0123456789abcdefghijklmnopqrstuvwxyz"[Mathf.Clamp(v, 0, 35)];
        static int U(char ch) => "0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(ch);

        static readonly string[] Blocked = { "fuck", "shit", "bitch", "bastard", "chutiya", "madarchod", "bhenchod", "behenchod", "gandu", "randi", "lund", "sex", "porn", "dick", "pussy", "nigg", "slut", "whore" };

        /// <summary>Letters, digits and spaces, at most 14 characters, and no swear words.</summary>
        public static string CleanName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
                else if (ch == ' ' && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                if (sb.Length >= 14) break;
            }
            string clean = sb.ToString().Trim();
            string lower = clean.ToLowerInvariant().Replace(" ", "");
            foreach (var b in Blocked) if (lower.Contains(b)) return "";
            return clean;
        }

        public static PenDef Custom(string id)
        {
            if (!TryDecode(id, out var p)) return null;
            var d = Build(p, id, PenRarity.Epic, CustomBudget);
            d.custom = true;
            if (string.IsNullOrEmpty(p.name)) d.name = "My " + Types[p.type].key;
            return d;
        }

        // ---------- the 600 shop pens ----------

        public const int GeneratedCount = 600;
        public const string GeneratedPrefix = "g";

        static List<PenDef> generated;

        static readonly string[] Words =
        {
            "Monsoon", "Galaxy", "Mango", "Rocket", "Thunder", "Chai", "Lassi", "Cricket", "Kite", "Diwali", "Holi", "Topper",
            "Backbench", "Recess", "Tiffin", "Jalebi", "Masala", "Neon", "Turbo", "Ninja", "Comet", "Pepper", "Mint", "Coconut",
            "Zebra", "Tiger", "Peacock", "Lotus", "Sunset", "Ocean", "Storm", "Pixel", "Laser", "Bolt", "Crystal", "Shadow",
            "Rainbow", "Candy", "Exam", "Homework", "Monitor", "Bench", "Chalk", "Inkwell", "Ruler", "Compass", "Lunchbox",
            "Saffron", "Indigo", "Emerald", "Ruby", "Pearl", "Cobalt", "Copper", "Silver", "Midnight", "Sparky", "Zoom",
            "Rapid", "Jumbo", "Mini", "Desi", "Filmy", "Bindaas", "Jugaad", "Dhamaka", "Toofan", "Badshah", "Rani", "Raja",
        };

        public static IReadOnlyList<PenDef> Generated
        {
            get
            {
                if (generated != null) return generated;
                generated = new List<PenDef>(GeneratedCount);
                var rng = new System.Random(20261026);
                var used = new HashSet<string>();
                for (int i = 0; i < GeneratedCount; i++)
                {
                    double r = rng.NextDouble();
                    var rarity = r < .55 ? PenRarity.Common : r < .83 ? PenRarity.Rare : r < .96 ? PenRarity.Epic : PenRarity.Legendary;
                    var p = new Spec
                    {
                        type = rng.Next(Types.Length),
                        barrel = rng.Next(Barrels.Length),
                        grip = rng.Next(Grips.Length),
                        cap = rng.Next(Caps.Length),
                        body = rng.Next(Colours.Length),
                        capColour = rng.Next(Colours.Length),
                        finish = FinishFor(rarity, rng),
                        pattern = rng.NextDouble() < (rarity == PenRarity.Common ? .25 : .5) ? 1 + rng.Next(Patterns.Length - 1) : 0,
                    };
                    if (p.capColour == p.body) p.capColour = (p.body + 5) % Colours.Length;
                    // "Monsoon Gel"; once those run out, two words ("Chai Rocket Gel").
                    string name;
                    int guard = 0;
                    do
                    {
                        string key = Types[p.type].key;
                        name = guard++ < 12 ? Words[rng.Next(Words.Length)] + " " + key
                            : Words[rng.Next(Words.Length)] + " " + Words[rng.Next(Words.Length)] + " " + key;
                    }
                    while (!used.Add(name));
                    p.name = name;

                    string id = GeneratedPrefix + (i + 1).ToString("000");
                    var d = Build(p, id, rarity, Budget(rarity));
                    // Shop prices: common ~150–350, rare ~300–700, epic ~600–1,300, legendary ~1,200–2,500 marks.
                    float mult = rarity == PenRarity.Common ? .45f : rarity == PenRarity.Rare ? .7f : rarity == PenRarity.Epic ? 1f : 1.45f;
                    d.price = Mathf.Max(100, Mathf.RoundToInt(Cost(p) * mult / 10f) * 10);
                    generated.Add(d);
                }
                return generated;
            }
        }

        static int FinishFor(PenRarity r, System.Random rng)
        {
            switch (r)
            {
                case PenRarity.Common: return rng.Next(3);                       // glossy, matte, frosted
                case PenRarity.Rare: return rng.NextDouble() < .6 ? rng.Next(3) : 3; // or metallic
                case PenRarity.Epic: return 3 + rng.Next(3);                     // metallic, glitter, holo
                default: return rng.NextDouble() < .5 ? 6 : 4 + rng.Next(2);     // gold, glitter or holo
            }
        }

        public static PenDef GeneratedPen(string id)
        {
            if (id == null || !id.StartsWith(GeneratedPrefix) || id.Length < 2) return null;
            if (!int.TryParse(id.Substring(GeneratedPrefix.Length), out int n)) return null;
            return n >= 1 && n <= GeneratedCount ? Generated[n - 1] : null;
        }

        /// <summary>Six shop pens that change every day (the same for everyone that day).</summary>
        public static List<PenDef> TodaysPens()
        {
            int day = (int)(DateTime.Now.Date - new DateTime(2026, 1, 1)).TotalDays;
            var rng = new System.Random(day * 7919);
            var list = new List<PenDef>();
            var all = Generated;
            // One of each rarity tier at least, then any.
            foreach (var want in new[] { PenRarity.Common, PenRarity.Rare, PenRarity.Epic, PenRarity.Legendary })
            {
                for (int tries = 0; tries < 200; tries++)
                {
                    var p = all[rng.Next(all.Count)];
                    if (p.rarity == want && !list.Contains(p)) { list.Add(p); break; }
                }
            }
            while (list.Count < 6)
            {
                var p = all[rng.Next(all.Count)];
                if (!list.Contains(p)) list.Add(p);
            }
            return list;
        }

        public static Color RarityColour(PenRarity r) =>
            r == PenRarity.Common ? Pal.H("#7D8790") : r == PenRarity.Rare ? Pal.H("#2B6FD6") : r == PenRarity.Epic ? Pal.H("#8A3FD1") : Pal.H("#C9921A");

        public static string RarityName(PenRarity r) =>
            r == PenRarity.Common ? "common" : r == PenRarity.Rare ? "rare" : r == PenRarity.Epic ? "epic" : "legendary";
    }
}
