using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// One-time project setup, run automatically when the project opens:
    /// materials that survive builds, the Main scene, build settings, portrait mode.
    /// Re-run from the menu: Tuition Takkar > Run Project Setup.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MatFolder = "Assets/Resources/TT/Materials";
        const string UIFolder = "Assets/Resources/TT/UI";
        public const string PanelSettingsPath = UIFolder + "/TTPanel.asset";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("TT_SetupDone", false))
                {
                    SessionState.SetBool("TT_SetupDone", true);
                    Run();
                }
            };
        }

        [MenuItem("Tuition Takkar/Run Project Setup")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EnsureFolder("Assets/Resources/TT", "Materials");
            CreateMaterial(MatFolder + "/TTLit.mat", LitShader());
            CreateMaterial(MatFolder + "/TTLine.mat", Shader.Find("Sprites/Default"));

            CreatePanelSettings();

            PlayerSettings.productName = "Tuition Takkar";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            EnsureScene();
            AssetDatabase.SaveAssets();
        }

        static Shader LitShader()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultShader != null) return rp.defaultShader;
            return Shader.Find("Standard");
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                var p = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                EnsureFolder(p, Path.GetFileName(parent));
            }
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        static void CreateMaterial(string path, Shader shader)
        {
            if (shader == null) return;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != shader) existing.shader = shader;
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
        }

        static void CreatePanelSettings()
        {
            // Must exist as a project asset (not just created at runtime) so Unity's
            // build pipeline assigns it the ICU text-shaping data it needs for layout.
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) != null) return;

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UIFolder + "/TTTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(390, 844);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0f;
            panel.sortingOrder = 10;
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("TuitionTakkar");
                go.AddComponent<GameManager>();
                SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
