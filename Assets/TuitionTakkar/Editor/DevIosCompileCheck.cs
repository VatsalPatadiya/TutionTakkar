using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>Compiles the game scripts as an iPhone build would (UNITY_IOS), without switching platform.</summary>
    static class DevIosCompileCheck
    {
        [MenuItem("Tuition Takkar/Dev: Check iPhone code compiles")]
        static void Run()
        {
            var files = Directory.GetFiles("Assets/TuitionTakkar/Scripts", "*.cs", SearchOption.AllDirectories);
            Directory.CreateDirectory("Temp/ioscheck");
            var b = new AssemblyBuilder("Temp/ioscheck/TTiOS.dll", files)
            {
                buildTarget = BuildTarget.iOS,
                buildTargetGroup = BuildTargetGroup.iOS,
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                    .Where(a => a.name != "Assembly-CSharp").Select(a => a.outputPath).Where(File.Exists).ToArray(),
                flags = AssemblyBuilderFlags.None,
            };
            b.buildFinished += (path, msgs) =>
            {
                int errors = 0;
                foreach (var m in msgs.Where(m => m.type == CompilerMessageType.Error)) { errors++; Debug.LogError("[iOS check] " + m.message); }
                Debug.Log("[iOS check] done, errors " + errors);
            };
            if (!b.Build()) Debug.LogError("[iOS check] could not start");
        }
    }
}
