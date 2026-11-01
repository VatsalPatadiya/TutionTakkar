using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Face avatars, built from layered parts (ArtSource/avatar_parts, turned into colour layers in
    /// Resources/Avatar by the art pipeline). A face is a short code like
    /// "face=01;eyes=02;...;skin=2;hairc=0;cloth=3;iris=1;bgc=4", saved in SaveData.face, shown
    /// everywhere through <see cref="Faces"/>.
    /// </summary>
    public static class AvatarParts
    {
        [Serializable] public class LayerFile { public string f; public string t; }

        [Serializable]
        public class Part
        {
            public string id, layer, name, tier, unlock;
            public int price;
            public bool hidesHair, hasBack;
            public LayerFile[] layers;
            public string Num => id.Substring(id.LastIndexOf('_') + 1);
            public bool Free => tier == "free" || price <= 0 && tier != "earned";
        }

        [Serializable] class Index { public int size; public Part[] parts; }

        /// <summary>Stacking order, back to front.</summary>
        public static readonly string[] Order =
            { "bg", "hairback", "top", "face", "cheeks", "eyes", "brows", "nose", "mouth", "beard", "glasses", "earrings", "hair", "hat" };

        /// <summary>Layers you can leave empty in the editor ("none").</summary>
        public static readonly HashSet<string> Optional = new HashSet<string> { "cheeks", "beard", "glasses", "earrings", "hair", "hat" };

        /// <summary>Editor tabs, in the order kids look for them.</summary>
        public static readonly string[] Tabs = { "face", "hair", "eyes", "brows", "mouth", "nose", "cheeks", "beard", "glasses", "earrings", "hat", "top", "bg" };

        public static string TabName(string layer) => layer switch
        {
            "face" => "FACE", "hair" => "HAIR", "eyes" => "EYES", "brows" => "BROWS", "mouth" => "MOUTH", "nose" => "NOSE",
            "cheeks" => "CHEEKS", "beard" => "BEARD", "glasses" => "GLASSES", "earrings" => "EARRINGS", "hat" => "HAT",
            "top" => "TOP", "bg" => "BACK", _ => layer.ToUpperInvariant(),
        };

        // ---------- colours (index 0.. in each list) ----------
        public static readonly string[] Skin = { "#FBE3D0", "#F2C9A5", "#E0A97E", "#C68863", "#9C6644", "#6B4430" };
        public static readonly string[] Hair = { "#1B1B1B", "#4A2F23", "#8A5A3B", "#D9A441", "#B5482F", "#8E8E8E", "#3D7BD9", "#D94F9C", "#3FAF6C" };
        public static readonly string[] Cloth = { "#3D7BD9", "#E5483A", "#3FAF6C", "#F2C14E", "#D94F9C", "#5B5BD6", "#F28C28", "#8E8E8E", "#1B1B1B", "#FFFFFF" };
        public static readonly string[] Iris = { "#6B4430", "#2E1E14", "#B07D2B", "#3D7BD9", "#3FAF6C", "#8E8E8E" };
        public static readonly string[] Back = { "#3D7BD9", "#E5483A", "#3FAF6C", "#F2C14E", "#D94F9C", "#5B5BD6", "#F28C28", "#2F4A3A", "#F2ECE2" };
        /// <summary>Fun hair colours cost marks; every other colour is free.</summary>
        public const int FunHairFrom = 6, FunHairPrice = 200;

        public static readonly string[] ColorKeys = { "skin", "hairc", "cloth", "iris", "bgc" };
        public static string[] Palette(string key) => key switch
        {
            "skin" => Skin, "hairc" => Hair, "cloth" => Cloth, "iris" => Iris, _ => Back,
        };

        /// <summary>Which colour row the editor shows on a tab.</summary>
        public static string ColorFor(string layer) => layer switch
        {
            "face" => "skin", "hair" => "hairc", "brows" => "hairc", "beard" => "hairc", "eyes" => "iris",
            "top" => "cloth", "hat" => "cloth", "bg" => "bgc", _ => null,
        };

        static Index index;
        static Dictionary<string, Part> byId;
        static Dictionary<string, List<Part>> byLayer;

        public static int Size { get { Load(); return index != null ? index.size : 256; } }

        public static bool Ready { get { Load(); return byId != null && byId.Count > 0; } }

        static void Load()
        {
            if (index != null) return;
            var ta = Resources.Load<TextAsset>("Avatar/avatar_index");
            index = ta != null ? JsonUtility.FromJson<Index>(ta.text) : new Index { size = 256, parts = new Part[0] };
            byId = new Dictionary<string, Part>();
            byLayer = new Dictionary<string, List<Part>>();
            foreach (var p in index.parts)
            {
                byId[p.id] = p;
                if (!byLayer.TryGetValue(p.layer, out var list)) byLayer[p.layer] = list = new List<Part>();
                list.Add(p);
            }
            foreach (var l in byLayer.Values) l.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        }

        public static Part Get(string id) { Load(); return id != null && byId.TryGetValue(id, out var p) ? p : null; }

        public static List<Part> Of(string layer) { Load(); return byLayer.TryGetValue(layer, out var l) ? l : new List<Part>(); }

        // ---------- the face code ----------

        /// <summary>A parsed face: part number per layer ("" = none) and colour index per colour key.</summary>
        public class Face
        {
            public readonly Dictionary<string, string> parts = new Dictionary<string, string>();
            public readonly Dictionary<string, int> colors = new Dictionary<string, int>();

            public string PartId(string layer)
            {
                if (!parts.TryGetValue(layer, out var n) || string.IsNullOrEmpty(n)) return null;
                return layer + "_" + n;
            }

            public int Color(string key) => colors.TryGetValue(key, out var v) ? v : 0;

            public Face Copy()
            {
                var f = new Face();
                foreach (var kv in parts) f.parts[kv.Key] = kv.Value;
                foreach (var kv in colors) f.colors[kv.Key] = kv.Value;
                return f;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var layer in Order)
                {
                    if (layer == "hairback") continue;
                    sb.Append(layer).Append('=').Append(parts.TryGetValue(layer, out var n) ? n : "").Append(';');
                }
                foreach (var k in ColorKeys) sb.Append(k).Append('=').Append(Color(k)).Append(';');
                return sb.ToString(0, sb.Length - 1);
            }
        }

        public static Face Parse(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var f = new Face();
            foreach (var pair in code.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string k = pair.Substring(0, eq), v = pair.Substring(eq + 1);
                if (Array.IndexOf(ColorKeys, k) >= 0) { if (int.TryParse(v, out int c)) f.colors[k] = Mathf.Clamp(c, 0, Palette(k).Length - 1); }
                else if (Array.IndexOf(Order, k) >= 0 && (v == "" || Get(k + "_" + v) != null)) f.parts[k] = v;
            }
            if (f.PartId("face") == null) return null; // not a face
            return f;
        }

        // ---------- random faces (default for every player, and the dice button) ----------

        static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char ch in s ?? "") h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        /// <summary>A random face from free parts only. Same seed, same face (a player's default comes from their ID).</summary>
        public static Face Random(string seed) => Random(new System.Random(Hash(seed)));

        public static Face Random(System.Random r)
        {
            Load();
            var f = new Face();
            string Pick(string layer, double chanceOfNone = 0)
            {
                var free = Of(layer).FindAll(p => p.Free);
                if (free.Count == 0 || r.NextDouble() < chanceOfNone) return "";
                return free[r.Next(free.Count)].Num;
            }
            f.parts["bg"] = Pick("bg");
            f.parts["top"] = Pick("top");
            f.parts["face"] = Pick("face");
            f.parts["eyes"] = Pick("eyes");
            f.parts["brows"] = Pick("brows");
            f.parts["nose"] = Pick("nose");
            f.parts["mouth"] = Pick("mouth");
            f.parts["hair"] = Pick("hair", 0.08);
            f.parts["cheeks"] = Pick("cheeks", 0.6);
            f.parts["beard"] = Pick("beard", 0.8);
            f.parts["glasses"] = Pick("glasses", 0.8);
            f.parts["earrings"] = Pick("earrings", 0.75);
            f.parts["hat"] = Pick("hat", 0.85);
            f.colors["skin"] = r.Next(Skin.Length);
            f.colors["hairc"] = r.Next(FunHairFrom);
            f.colors["cloth"] = r.Next(Cloth.Length);
            f.colors["iris"] = r.Next(Iris.Length);
            f.colors["bgc"] = r.Next(Back.Length);
            return f;
        }

        /// <summary>A batch-ladder rival's face: a fixed random face made from their id.</summary>
        public static string RivalCode(RivalDef r) => r == null ? "" : Random("rival-" + r.id).ToString();

        // ---------- owning things ----------

        public static string ColorItem(string key, int i) => "color:" + key + ":" + i;

        public static int ColorPrice(string key, int i) => key == "hairc" && i >= FunHairFrom ? FunHairPrice : 0;

        public static bool Owns(SaveData s, Part p) => p == null || p.Free || s.faceOwned.Contains(p.id);

        public static bool OwnsColor(SaveData s, string key, int i) => ColorPrice(key, i) <= 0 || s.faceOwned.Contains(ColorItem(key, i));

        /// <summary>The face this phone shows for me: the saved one, or my default random face.</summary>
        public static string MyCode(SaveData s)
        {
            if (Parse(s.face) != null) return s.face;
            string seed = !string.IsNullOrEmpty(OnlineServices.PlayerId) ? OnlineServices.PlayerId : SystemInfo.deviceUniqueIdentifier;
            return Random(seed).ToString();
        }
    }
}
