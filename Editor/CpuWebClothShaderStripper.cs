#nullable enable
namespace WebCloth.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Rendering;
    using UnityEngine;

    public sealed class CpuWebClothShaderStripper : IPreprocessComputeShaders
    {
        public int callbackOrder => int.MaxValue;
        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            var folder = SessionState.GetString(CpuWebClothBuild.ComputeFolderKey, string.Empty);
            if (folder.Length > 0 && AssetDatabase.GetAssetPath(shader).StartsWith(folder, StringComparison.Ordinal)) data.Clear();
        }
    }
}
