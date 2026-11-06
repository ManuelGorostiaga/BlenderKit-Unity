using System;
using System.Diagnostics;
using System.IO;
using BlenderKitUnity.Editor.API;
using UnityEngine;

namespace BlenderKitUnity.Editor.Pipeline
{
    public static class BlenderMaterialExtractor
    {
        public static ExtractedMaterialMeta Extract(string blendFilePath, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(blendFilePath) || !File.Exists(blendFilePath))
            {
                throw new FileNotFoundException($"The .blend file was not found at '{blendFilePath}'");
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
            }

            string blenderExecutable = BlenderFinder.ResolveBlenderExecutable();
            string scriptPath = ResolvePythonScriptPath();

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = blenderExecutable,
                Arguments = $"-b \"{blendFilePath}\" --python \"{scriptPath}\" -- --out \"{outputDirectory}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !output.Contains("[EXTRACT_SUCCESS]"))
            {
                throw new InvalidOperationException(
                    $"Blender extraction failed with exit code {process.ExitCode}.\nOutput: {output}\nError: {error}");
            }

            string metaPath = Path.Combine(outputDirectory, "material_meta.json");
            if (!File.Exists(metaPath))
            {
                throw new FileNotFoundException(
                    $"Extraction succeeded but metadata file was not generated at '{metaPath}'");
            }

            string json = File.ReadAllText(metaPath);
            var meta = JsonUtility.FromJson<ExtractedMaterialMeta>(json);
            if (meta == null)
            {
                throw new InvalidOperationException($"Failed to deserialize material metadata from '{metaPath}'.");
            }

            return meta;
        }

        private static string ResolvePythonScriptPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BlenderMaterialExtractor).Assembly);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                string packagePath = Path.Combine(packageInfo.resolvedPath, "Editor", "Pipeline", "extract_material.py");
                if (File.Exists(packagePath))
                {
                    return packagePath;
                }
            }

            string[] guids = UnityEditor.AssetDatabase.FindAssets("extract_material");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (assetPath.EndsWith("extract_material.py", StringComparison.OrdinalIgnoreCase))
                {
                    string fullPath = Path.GetFullPath(assetPath);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            string directPath = Path.GetFullPath("Packages/com.blenderkit.unity/Editor/Pipeline/extract_material.py");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            string localDevPath = Path.GetFullPath("Assets/BlenderKitUnity/Editor/Pipeline/extract_material.py");
            if (File.Exists(localDevPath))
            {
                return localDevPath;
            }

            throw new FileNotFoundException(
                "Could not locate 'extract_material.py' in the project. Please ensure the BlenderKit package files are intact.");
        }
    }
}
