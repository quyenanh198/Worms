using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Worms.Editor
{
    /// <summary>
    /// Brings a fresh checkout (CI or a new machine) to the state the game
    /// needs, so ProjectSettings and scenes never have to be edited by hand.
    /// </summary>
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";

        [MenuItem("Worms/Apply Project Setup")]
        public static void Apply()
        {
            EnsureBootScene();
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
                EditorSceneManager.SaveScene(scene, BootScene);
                AssetDatabase.ImportAsset(BootScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
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
