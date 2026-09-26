using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Worms.Editor
{
    /// <summary>
    /// Batchmode build entry points. CI calls <see cref="CI"/> through GameCI's
    /// buildMethod; the other methods are for local builds from the menu.
    /// </summary>
    public static class Build
    {
        public const string AndroidId = "com.lazybutts.worms";

        /// <summary>GameCI entry: target from -buildTarget, output from -customBuildPath.</summary>
        public static void CI()
        {
            string path = Arg("-customBuildPath");
            if (string.IsNullOrEmpty(path)) Fail("missing -customBuildPath");
            Run(EditorUserBuildSettings.activeBuildTarget, path);
        }

        [MenuItem("Worms/Build/Web")] public static void Web() { Run(BuildTarget.WebGL, "Builds/Web"); }
        [MenuItem("Worms/Build/Android")] public static void Android() { Run(BuildTarget.Android, "Builds/Android/Worms.apk"); }
        [MenuItem("Worms/Build/Windows")] public static void Windows() { Run(BuildTarget.StandaloneWindows64, "Builds/Windows/Worms.exe"); }
        [MenuItem("Worms/Build/macOS")] public static void MacOS() { Run(BuildTarget.StandaloneOSX, "Builds/macOS/Worms.app"); }
        [MenuItem("Worms/Build/Linux")] public static void Linux() { Run(BuildTarget.StandaloneLinux64, "Builds/Linux/Worms"); }

        public static void Run(BuildTarget target, string path)
        {
            ProjectSetup.Apply();
            Configure(target);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.BootScene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log($"Build {target}: {report.summary.result}, {report.summary.totalSize} bytes, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded) Fail("build failed");
        }

        static void Configure(BuildTarget target)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var named = NamedBuildTarget.FromBuildTargetGroup(group);

            string version = Arg("-buildVersion");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;

            if (target != BuildTarget.WebGL)
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);

            switch (target)
            {
                case BuildTarget.WebGL:
                    // Brotli with the loader's own decompressor: works behind any
                    // proxy or CDN without Content-Encoding headers (server/WebStatic.cs).
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                    PlayerSettings.WebGL.decompressionFallback = true;
                    PlayerSettings.WebGL.nameFilesAsHashes = true;
                    PlayerSettings.WebGL.dataCaching = true;
                    // Assets/WebGLTemplates/Worms: full-screen canvas, PWA manifest, APK banner on Android.
                    PlayerSettings.WebGL.template = "PROJECT:Worms";
                    break;
                case BuildTarget.Android:
                    PlayerSettings.SetApplicationIdentifier(named, AndroidId);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    if (int.TryParse(Arg("-androidVersionCode"), out int code)) PlayerSettings.Android.bundleVersionCode = code;
                    ConfigureKeystore();
                    break;
            }
        }

        // GameCI writes the keystore from the ANDROID_KEYSTORE_BASE64 secret and passes
        // its name and passwords as arguments. Without it the APK gets a debug signature,
        // which cannot install over a previous build signed with another key.
        static void ConfigureKeystore()
        {
            string name = Arg("-androidKeystoreName");
            if (string.IsNullOrEmpty(name) || !System.IO.File.Exists(name))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = System.IO.Path.GetFullPath(name);
            PlayerSettings.Android.keystorePass = Arg("-androidKeystorePass");
            PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName");
            PlayerSettings.Android.keyaliasPass = Arg("-androidKeyaliasPass");
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw new BuildFailedException(message);
        }
    }
}
