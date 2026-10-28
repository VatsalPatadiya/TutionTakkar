using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Declares each Cloud Code script's parameters in its import settings. The Cloud Code package
    /// reads module.exports.params with Node.js, which this Mac doesn't have; without declared
    /// parameters the server drops every argument the game sends (the scripts received {}).
    /// Run it, then deploy again from the Deployment window.
    /// </summary>
    static class CloudCodeParams
    {
        const int Str = 0, Num = 1;

        static readonly (string script, (string name, int type, bool required)[] ps)[] All =
        {
            ("Assets/CloudCode/find_player.js", new[] { ("code", Str, true) }),
            ("Assets/CloudCode/friend_id.js", new (string, int, bool)[0]),
            ("Assets/CloudCode/invite_push.js", new[]
            {
                ("to", Str, true), ("code", Str, true), ("from", Str, false), ("desk", Str, false), ("deskId", Str, false),
                ("fee", Num, false), ("seated", Num, false), ("seats", Num, false),
            }),
            ("Assets/CloudCode/friend_push.js", new[] { ("to", Str, true), ("kind", Str, true), ("from", Str, false) }),
        };

        [MenuItem("Tuition Takkar/Set Cloud Code Parameters")]
        static void Set()
        {
            var paramType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Services.CloudCode.Authoring.Editor.Core.Model.CloudCodeParameter")).FirstOrDefault(t => t != null);
            var enumType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Services.CloudCode.Authoring.Editor.Core.Model.ParameterType")).FirstOrDefault(t => t != null);
            if (paramType == null || enumType == null) { Debug.LogError("[CC params] Cloud Code types not found"); return; }
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(paramType);

            foreach (var (script, ps) in All)
            {
                var imp = AssetImporter.GetAtPath(script);
                if (imp == null) { Debug.LogWarning("[CC params] no importer for " + script); continue; }
                var field = imp.GetType().GetField("Parameters", BindingFlags.Public | BindingFlags.Instance);
                if (field == null) { Debug.LogWarning("[CC params] " + script + " uses " + imp.GetType().Name + " (no Parameters)"); continue; }
                var list = (IList)Activator.CreateInstance(listType);
                foreach (var (name, type, required) in ps)
                {
                    var p = Activator.CreateInstance(paramType);
                    paramType.GetField("Name").SetValue(p, name);
                    paramType.GetField("ParameterType").SetValue(p, Enum.ToObject(enumType, type));
                    paramType.GetField("Required").SetValue(p, required);
                    list.Add(p);
                }
                field.SetValue(imp, list);
                EditorUtility.SetDirty(imp);
                imp.SaveAndReimport();
                Debug.Log("[CC params] " + script + ": " + ps.Length + " parameters");
            }
            CloudCodeNameFix.Fix();
        }
    }
}
