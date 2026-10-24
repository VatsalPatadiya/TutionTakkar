using System.Collections.Generic;
using UnityEngine;

namespace TuitionTakkar
{
    public enum PenShape { Round, Triangle }

    public static class Pal
    {
        public static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static readonly Color Ink = H("#1F2A44");
        public static readonly Color InkSoft = H("#3D4B66");
        public static readonly Color Room = H("#26343A");
        public static readonly Color Night = H("#1C2C33");
        public static readonly Color NightSoft = H("#2B3F47");
        public static readonly Color Mist = H("#BFD3CB");
        public static readonly Color MistLight = H("#D9E6E1");
        public static readonly Color Paper = H("#FFFDF4");
        public static readonly Color GraphLine = H("#DDE8F2");
        public static readonly Color Dash = H("#B9C7D6");
        public static readonly Color Tape = H("#F4E8C4");
        public static readonly Color Yellow = H("#FFE45C");
        public static readonly Color Pink = H("#FFB8C9");
        public static readonly Color Sky = H("#A9D8F5");
        public static readonly Color Teal = H("#8ED1C6");
        public static readonly Color Red = H("#B23A26");
        public static readonly Color RedDark = H("#7E2617");
        public static readonly Color Green = H("#1E6B4F");
        public static readonly Color Orange = H("#E8762F");
        public static readonly Color Pouch = H("#2F5D8C");
        public static readonly Color PouchDark = H("#234A70");
        public static readonly Color PouchText = H("#DDE9F5");
        public static readonly Color Laminate = H("#CBDDD0");
        public static readonly Color LaminateEdge = H("#8FB09D");
        public static readonly Color LaminateShadow = H("#5F7F6C");
        public static readonly Color Locked = H("#6F8B7C");
        public static readonly Color LockedText = H("#3E5A4C");
        public static readonly Color BenchText = H("#2C4A3E");
        public static readonly Color White = Color.white;
    }

    public class PenDef
    {
        public string id;
        public string name;
        public string traits;     // short line for cards
        public string blurb;      // flavour line
        public Color color;
        public Color capColor;
        public Color gripColor = Pal.H("#E9E4D8");
        public Color tipColor = Pal.H("#3A3A3A");
        public float length = 0.145f;  // metres (real)
        public float radius = 0.0052f; // metres (real)
        public float mass = 0.0138f;   // kg
        public float friction = 0.30f;
        public float bounce = 0.38f;
        public float angularDamping = 0.8f;
        /// <summary>-1..1 along the pen axis, + toward the cap end. Top-heavy pens wobble.</summary>
        public float comOffset;
        public bool hasClip = true;
        public PenShape shape = PenShape.Round;
        public float statWeight, statGrip, statSpin;

        public PenDef Recolor(Color body, Color cap)
        {
            var p = (PenDef)MemberwiseClone();
            p.color = body;
            p.capColor = cap;
            return p;
        }
    }

    public class BotProfile
    {
        public float aimNoiseDeg;
        public float powerNoise;
        public float caution;       // 0..1, how much it fears its own pen near an edge
        public float aggression;    // 0..1, how much it chases the kill
        public float mistakeChance; // chance to pick a weaker plan

        public BotProfile(float aim, float power, float caution, float aggression, float mistake)
        {
            aimNoiseDeg = aim;
            powerNoise = power;
            this.caution = caution;
            this.aggression = aggression;
            mistakeChance = mistake;
        }
    }

    public class RivalDef
    {
        public string id;
        public string name;
        public string initial;
        public Color avatar;
        public string blurb;
        public string penId;
        public int reward;
        public string quipWhenTheyLose;
        public string quipWhenTheyWin;
        public BotProfile bot;
    }

    public static class Catalog
    {
        public const string StarterId = "starter";

        public static readonly List<PenDef> Pens = new List<PenDef>
        {
            new PenDef { id = StarterId, name = "Starter Click", traits = "Balanced · can't be lost", blurb = "your first pen. it never leaves the pouch.",
                color = Pal.H("#E8762F"), capColor = Pal.H("#1F2A44"), statWeight = .5f, statGrip = .5f, statSpin = .5f },
            new PenDef { id = "zippy07", name = "Zippy 07", traits = "Light · big spin", blurb = "slim and light. slides far, spins easy.",
                color = Pal.H("#2A9D8F"), capColor = Pal.H("#16324F"), radius = .0046f, mass = .0105f, angularDamping = .55f,
                statWeight = .35f, statGrip = .45f, statSpin = .85f },
            new PenDef { id = "trigrip", name = "Tri-Grip", traits = "Triangle · never rolls", blurb = "three flat sides. it stays where it lands.",
                color = Pal.H("#E2B93B"), capColor = Pal.H("#7A5A12"), radius = .0055f, mass = .0145f, friction = .45f,
                hasClip = false, shape = PenShape.Triangle, statWeight = .55f, statGrip = .8f, statSpin = .3f },
            new PenDef { id = "steel", name = "Steel Streak", traits = "Heavy · hits like a train", blurb = "metal barrel. moves slow, hits hard.",
                color = Pal.H("#8A97A5"), capColor = Pal.H("#3E4A57"), radius = .0050f, mass = .0215f, bounce = .3f,
                statWeight = .9f, statGrip = .5f, statSpin = .35f },
            new PenDef { id = "gelglide", name = "Gel Glide", traits = "Slick · slides far", blurb = "glossy barrel. good luck stopping it.",
                color = Pal.H("#3F7FD8"), capColor = Pal.H("#1D3F73"), radius = .0054f, mass = .0128f, friction = .2f,
                statWeight = .45f, statGrip = .25f, statSpin = .6f },
            new PenDef { id = "fatcap", name = "Fat Cap", traits = "Top-heavy · wobbles", blurb = "that huge cap pulls it sideways.",
                color = Pal.H("#7B5EA7"), capColor = Pal.H("#4A3570"), radius = .0058f, mass = .0155f, comOffset = .35f,
                statWeight = .6f, statGrip = .5f, statSpin = .7f },
            new PenDef { id = "rubber", name = "Rubber Grip", traits = "Grippy · parks hard", blurb = "full rubber body. stops dead.",
                color = Pal.H("#4D4D4D"), capColor = Pal.H("#E2553C"), gripColor = Pal.H("#2B2B2B"), radius = .0056f, mass = .015f,
                friction = .6f, angularDamping = 1.2f, statWeight = .6f, statGrip = .95f, statSpin = .3f },
            new PenDef { id = "twintone", name = "Twin Tone", traits = "Bouncy · aggressive", blurb = "two colours, one mood: attack.",
                color = Pal.H("#C8414B"), capColor = Pal.H("#2F5D8C"), bounce = .45f, statWeight = .5f, statGrip = .5f, statSpin = .55f },
            new PenDef { id = "featherlite", name = "Featherlite", traits = "Ultra light · fast", blurb = "so light it barely stops.",
                color = Pal.H("#F2A7C3"), capColor = Pal.H("#8C3A5E"), radius = .0047f, mass = .009f, friction = .26f,
                statWeight = .2f, statGrip = .35f, statSpin = .75f },
            new PenDef { id = "chunky", name = "Chunky Marker", traits = "Short · immovable", blurb = "short, fat and stubborn.",
                color = Pal.H("#2E8B57"), capColor = Pal.H("#14452A"), length = .13f, radius = .0068f, mass = .023f, friction = .4f,
                hasClip = false, statWeight = 1f, statGrip = .7f, statSpin = .2f },
            new PenDef { id = "golden", name = "Golden Nib", traits = "The ladder prize", blurb = "only the batch champion carries this.",
                color = Pal.H("#D4A72C"), capColor = Pal.H("#6B5216"), radius = .0053f, mass = .0175f, friction = .38f, bounce = .34f,
                statWeight = .75f, statGrip = .75f, statSpin = .65f },
        };

        public static readonly List<RivalDef> Rivals = new List<RivalDef>
        {
            R("monty", "Monty", "#8ED1C6", "zippy07", 25, "Sits next to the water cooler. Hits hard, panics near the edge.",
                "the bench is slippery today, I swear.", "too easy. do your homework next time.", new BotProfile(14f, .24f, .2f, .8f, .25f)),
            R("pinky", "Pinky", "#FFB8C9", "trigrip", 30, "Never misses a short shot. Plays safe.",
                "okay, that one was lucky.", "slow and steady, yaar.", new BotProfile(13f, .2f, .55f, .3f, .25f)),
            R("babloo", "Babloo", "#F3C98B", "steel", 35, "Heavy hands. Heavier pen.",
                "my pen is too heavy for this bench.", "boom. off the bench.", new BotProfile(10f, .18f, .3f, .9f, .18f)),
            R("sana", "Sana", "#A9D8F5", "gelglide", 40, "Glides in, glides out. Never where you aim.",
                "hmm. noted.", "you should see your face.", new BotProfile(9f, .15f, .5f, .6f, .15f)),
            R("kabir", "Kabir", "#C6B4E8", "fatcap", 45, "Wobbly pen, steady nerves.",
                "it wobbled. not my fault.", "wobble wobble, bye bye.", new BotProfile(8f, .14f, .4f, .7f, .12f)),
            R("ritu", "Ritu", "#F5B7A1", "rubber", 50, "Parks her pen like a scooter. It never moves.",
                "fine. take it.", "parked. and you're out.", new BotProfile(7f, .12f, .85f, .35f, .1f)),
            R("tanmay", "Tanmay", "#BFE3B4", "twintone", 55, "Class topper. Calculates every flick.",
                "statistically, that shouldn't happen.", "as calculated.", new BotProfile(5.5f, .1f, .6f, .6f, .08f)),
            R("zoya", "Zoya", "#FFE0A3", "featherlite", 60, "Fast, light and very annoying.",
                "rematch. now.", "blink and you'll miss it.", new BotProfile(4.5f, .09f, .5f, .75f, .06f)),
            R("arjun", "Arjun", "#B9D3E8", "chunky", 70, "Back-bench legend. Shoulders like a wall.",
                "respect. you earned it.", "back bench rules.", new BotProfile(3.5f, .07f, .7f, .6f, .04f)),
            R("vikram", "Vikram", "#E8D49A", "golden", 100, "The tuition sir's nephew. Has never lost. Allegedly.",
                "...don't tell my uncle.", "uncle said I'd win.", new BotProfile(2.5f, .05f, .65f, .75f, .02f)),
        };

        public static readonly string[] PracticeNames = { "Easy", "Medium", "Hard" };

        public static readonly BotProfile[] PracticeBots =
        {
            new BotProfile(14f, .25f, .3f, .6f, .3f),
            new BotProfile(8f, .14f, .5f, .6f, .12f),
            new BotProfile(3f, .06f, .65f, .7f, .03f),
        };

        static RivalDef R(string id, string name, string avatar, string pen, int reward, string blurb,
            string quipLose, string quipWin, BotProfile bot)
        {
            return new RivalDef
            {
                id = id, name = name, initial = name.Substring(0, 1), avatar = Pal.H(avatar), penId = pen,
                reward = reward, blurb = blurb, quipWhenTheyLose = quipLose, quipWhenTheyWin = quipWin, bot = bot
            };
        }

        public static PenDef Pen(string id)
        {
            foreach (var p in Pens) if (p.id == id) return p;
            return Pens[0];
        }

        public static RivalDef RivalForPen(string penId)
        {
            foreach (var r in Rivals) if (r.penId == penId) return r;
            return null;
        }
    }
}
