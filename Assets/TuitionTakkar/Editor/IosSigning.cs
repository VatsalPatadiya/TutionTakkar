using UnityEditor;
using UnityEngine;

namespace TuitionTakkar.EditorTools
{
    /// <summary>iPhone signing: Xcode signs builds automatically with the Techiebutler Apple team.</summary>
    static class IosSigning
    {
        public const string TeamId = "87CTM2H52V";

        [MenuItem("Tuition Takkar/Set iPhone signing team")]
        static void Apply()
        {
            PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.unitygame.tuitiontakkar");
            AssetDatabase.SaveAssets();
            Debug.Log("[TuitionTakkar] iOS signing: team " + TeamId + ", automatic");
        }
    }
}
