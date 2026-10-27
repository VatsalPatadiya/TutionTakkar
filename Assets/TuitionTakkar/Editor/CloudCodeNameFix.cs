using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Works around a Cloud Code package bug: after a domain reload the loaded Cloud Code scripts lose
    /// their name, so the Deployment window reports "same name" duplicates and uploads 404.
    /// This puts each script's name back from its file path. Run it, then Deploy again.
    /// </summary>
    static class CloudCodeNameFix
    {
        [MenuItem("Tuition Takkar/Fix Cloud Code Script Names")]
        public static void Fix()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetType("Unity.Services.CloudCode.Authoring.Editor.Scripts.CloudCodeScript") != null);
            if (asm == null) { Debug.LogError("[CC fix] Cloud Code editor assembly not found."); return; }
            var assetType = asm.GetType("Unity.Services.CloudCode.Authoring.Editor.Scripts.CloudCodeScript");
            var nameType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Services.CloudCode.Authoring.Editor.Core.Model.ScriptName")).FirstOrDefault(t => t != null);
            if (nameType == null) { Debug.LogError("[CC fix] ScriptName type not found."); return; }
            var fromPath = nameType.GetMethod("FromPath", BindingFlags.Public | BindingFlags.Static);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            int fixedCount = 0, seen = 0;
            foreach (var obj in Resources.FindObjectsOfTypeAll(assetType))
            {
                var model = assetType.GetProperty("Model", all).GetValue(obj);
                if (model == null) continue;
                seen++;
                var mt = model.GetType();
                var path = (string)mt.GetProperty("Path", all).GetValue(model);
                if (string.IsNullOrEmpty(path)) path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[CC fix] script with no path skipped"); continue; }
                var before = mt.GetProperty("Name", all).GetValue(model)?.ToString();
                var name = fromPath.Invoke(null, new object[] { path });
                var t = mt; FieldInfo fName = null, fPath = null;
                while (t != null && (fName == null || fPath == null))
                { fName ??= t.GetField("m_Name", all); fPath ??= t.GetField("m_Path", all); t = t.BaseType; }
                fName?.SetValue(model, name);
                if (fPath != null && fPath.GetValue(model) == null) fPath.SetValue(model, path);
                Debug.Log($"[CC fix] {path}: name '{before}' -> '{name}'");
                fixedCount++;
            }
            Debug.Log($"[CC fix] done: {fixedCount} of {seen} Cloud Code scripts named.");
        }
    }
}
