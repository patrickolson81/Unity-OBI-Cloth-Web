#nullable enable
namespace WebCloth.Editor
{
    using System;
    using System.IO;
    using Obi;
    using UnityEditor;
    using UnityEditor.Build.Reporting;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Rendering;

    public static class CpuWebClothBuild
    {
        internal const string ComputeFolderKey = "WebCloth.CpuBuild.ComputeFolder";

        public static BuildReport Build(BuildPlayerOptions options)
        {
            Validate(options);
            var computeFolder = FindComputeFolder();
            var previousScope = SessionState.GetString(ComputeFolderKey, string.Empty);
            var automatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            try
            {
                SessionState.SetString(ComputeFolderKey, computeFolder);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
                return BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                SessionState.SetString(ComputeFolderKey, previousScope);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, graphics);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, automatic);
            }
        }

        public static void Validate(BuildPlayerOptions options)
        {
            if (options.target != BuildTarget.WebGL) throw new ArgumentException("CPU cloth web builds require the WebGL target.", nameof(options));
            if (options.scenes == null || options.scenes.Length == 0) throw new ArgumentException("Select at least one saved scene.", nameof(options));
            var count = 0;
            foreach (var path in options.scenes)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new ArgumentException("The build scene does not exist: " + path, nameof(options));
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var solver in root.GetComponentsInChildren<ObiSolver>(true))
                    {
                        count++;
                        if (solver.backendType != ObiSolver.BackendType.Burst)
                            throw new InvalidOperationException("CPU web builds cannot contain Compute solvers: " + solver.name + " in " + path);
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            if (count == 0) throw new InvalidOperationException("No Obi solver was found in the selected scenes.");
        }

        private static string FindComputeFolder()
        {
            foreach (var guid in AssetDatabase.FindAssets("Obi t:AssemblyDefinitionAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/Scripts/Obi.asmdef", StringComparison.Ordinal)) continue;
                var root = Path.GetDirectoryName(Path.GetDirectoryName(path))!.Replace('\\', '/');
                var compute = root + "/Resources/Compute/";
                if (Directory.Exists(compute)) return compute;
            }
            throw new InvalidOperationException("Could not locate the Obi 7 compute resources. Install Obi Cloth 7.2 before building.");
        }
    }
}
