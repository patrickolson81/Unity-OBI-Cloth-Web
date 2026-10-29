#nullable enable
namespace WebCloth.Editor
{
    using System;
    using System.Collections.Generic;
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

        public static BuildReport Build(BuildPlayerOptions options) => Build(options, Array.Empty<GameObject>());

        public static BuildReport Build(BuildPlayerOptions options, IReadOnlyList<GameObject> runtimeSolverPrefabs)
        {
            Validate(options, runtimeSolverPrefabs);
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

        public static void Validate(BuildPlayerOptions options) => Validate(options, Array.Empty<GameObject>());

        public static void Validate(BuildPlayerOptions options, IReadOnlyList<GameObject> runtimeSolverPrefabs)
        {
            if (options.target != BuildTarget.WebGL) throw new ArgumentException("CPU cloth web builds require the WebGL target.", nameof(options));
            if (options.scenes == null || options.scenes.Length == 0) throw new ArgumentException("Select at least one saved scene.", nameof(options));
            if (runtimeSolverPrefabs == null) throw new ArgumentNullException(nameof(runtimeSolverPrefabs));
            var count = 0;
            foreach (var path in options.scenes)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new ArgumentException("The build scene does not exist: " + path, nameof(options));
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        count += ValidateSolvers(root, path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            foreach (var prefab in runtimeSolverPrefabs)
            {
                if (prefab == null || !AssetDatabase.IsMainAsset(prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                    throw new ArgumentException("Declare saved root prefab assets for runtime solvers.", nameof(runtimeSolverPrefabs));
                var path = AssetDatabase.GetAssetPath(prefab);
                var prefabCount = ValidateSolvers(prefab, path);
                if (prefabCount == 0) throw new InvalidOperationException("The declared runtime prefab contains no Obi solver: " + path);
                count += prefabCount;
            }
            if (count == 0) throw new InvalidOperationException("No Obi solver was found in the selected scenes or declared runtime prefabs.");
        }

        private static int ValidateSolvers(GameObject root, string source)
        {
            var solvers = root.GetComponentsInChildren<ObiSolver>(true);
            foreach (var solver in solvers)
                if (solver.backendType != ObiSolver.BackendType.Burst)
                    throw new InvalidOperationException("CPU web builds cannot contain Compute solvers: " + solver.name + " in " + source);
            return solvers.Length;
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
