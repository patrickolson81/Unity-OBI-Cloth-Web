#nullable enable
namespace WebCloth.Sample.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEditor.Build.Reporting;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using WebCloth.Editor;

    public static class ClothSample
    {
        public static string ScenePath => AssetDatabase.GUIDToAssetPath("2f37e09bdd5549b48558bf00da13cef2");

        [MenuItem("Web Cloth/Sample/Open interaction scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (string.IsNullOrEmpty(ScenePath)) throw new InvalidOperationException("Import the Cloth Interaction sample in Package Manager first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Web Cloth/Sample/Build WebGL2 development test")]
        public static void BuildWeb()
        {
            var output = Environment.GetEnvironmentVariable("WEB_CLOTH_OUTPUT") ?? "Builds/WebCloth";
            var compression = PlayerSettings.WebGL.compressionFormat;
            var template = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.template = "APPLICATION:Default";
                var report = CpuWebClothBuild.Build(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.WebGL,
                    locationPathName = output, options = BuildOptions.Development
                });
                Directory.CreateDirectory("Reports");
                File.WriteAllText("Reports/web-build.txt", $"{report.summary.result}: {report.summary.totalErrors} errors; {report.summary.totalSize} bytes; {report.summary.totalTime} elapsed.");
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Cloth WebGL build failed; inspect the build log.");
                var index = Path.Combine(output, "index.html");
                var html = File.ReadAllText(index);
                var responsiveStyle = "<style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#111827}#unity-container.unity-desktop{position:fixed;inset:0;width:100%;height:100%;transform:none}#unity-canvas{width:100vw!important;height:100vh!important;display:block}#unity-footer{display:none}</style>";
                File.WriteAllText(index, html.Replace("</head>", responsiveStyle + "</head>"));
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.template = template;
            }
        }
    }
}
