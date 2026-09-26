using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Worms.Editor
{
    /// <summary>
    /// Brings a fresh checkout (CI or a new machine) to the state the game
    /// needs, so ProjectSettings and scenes never have to be edited by hand.
    /// </summary>
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        public const string UrpAsset = "Assets/Settings/URP.asset";

        [MenuItem("Worms/Apply Project Setup")]
        public static void Apply()
        {
            EnsureBootScene();
            EnsureUrp();
            UseLegacyInputManager();
            ConfigureCommonPlayerSettings();
            AssetDatabase.SaveAssets();
        }

        static void EnsureBootScene()
        {
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // With "Automatic" fog stripping Unity keeps fog shader variants only
                // if a scene in the build uses fog; the match turns fog on at runtime.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                EditorSceneManager.SaveScene(scene, BootScene);
                AssetDatabase.ImportAsset(BootScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
        }

        // URP asset and renderer created from code. The settings here are the
        // High tier; UrpSetup lowers them at runtime for Medium and Low.
        static void EnsureUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAsset);
            if (urp == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UrpAsset));
                urp = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(urp, UrpAsset);
                urp.LoadBuiltinRendererData();
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 60f;
            urp.shadowCascadeCount = 2;
            urp.mainLightShadowmapResolution = 2048;
            // No public setter for soft shadows.
            var so = new SerializedObject(urp);
            var soft = so.FindProperty("m_SoftShadowsSupported");
            if (soft != null) soft.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
        }

        // Keyboard and touch are read through UnityEngine.Input, which works the
        // same on every target including Web. There is no public API for this
        // setting, so it is written through the serialized PlayerSettings asset.
        static void UseLegacyInputManager()
        {
            var assets = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 0)
            {
                prop.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureCommonPlayerSettings()
        {
            PlayerSettings.companyName = "lazybutts";
            PlayerSettings.productName = "Worms";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }
    }
}
