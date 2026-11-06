using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlenderKitUnity.Editor.API;
using UnityEditor;
using UnityEngine;

namespace BlenderKitUnity.Editor.Pipeline
{
    public sealed class BlenderKitMaterialService
    {
        private readonly BlenderKitApiClient apiClient;

        public BlenderKitMaterialService(BlenderKitApiClient client)
        {
            apiClient = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool TryGetExistingMaterial(AssetData asset, out Material material)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            string sanitizedName = SanitizeName(asset.GetEffectiveName());
            string targetFolder = BlenderKitSettings.TargetFolder;
            string materialPath = Path.Combine(targetFolder, sanitizedName, $"{sanitizedName}.mat").Replace('\\', '/');

            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            return material != null;
        }

        public async Task<Material> DownloadAndCreateMaterialAsync(
            AssetData asset,
            string resolution,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (TryGetExistingMaterial(asset, out Material existing))
            {
                return existing;
            }

            AssetFile targetFile = asset.GetTargetFile(resolution);
            string directUrl = await apiClient.ResolveDownloadUrlAsync(targetFile.downloadUrl, cancellationToken);

            string tempDir = Path.Combine(Application.temporaryCachePath, "BlenderKitDownloads", asset.id);
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            string tempBlendPath = Path.Combine(tempDir, "source.blend");
            await apiClient.DownloadFileAsync(directUrl, tempBlendPath, progress, cancellationToken);

            string sanitizedName = SanitizeName(asset.GetEffectiveName());
            string projectFolder = BlenderKitSettings.TargetFolder;
            string assetDirectory = Path.Combine(projectFolder, sanitizedName).Replace('\\', '/');
            string physicalOutputDir = Path.GetFullPath(assetDirectory);

            if (!Directory.Exists(physicalOutputDir))
            {
                Directory.CreateDirectory(physicalOutputDir);
            }

            ExtractedMaterialMeta meta = BlenderMaterialExtractor.Extract(tempBlendPath, physicalOutputDir);
            Material material = MaterialBuilder.BuildAndSave(meta, physicalOutputDir);

            if (File.Exists(tempBlendPath))
            {
                File.Delete(tempBlendPath);
            }

            return material;
        }

        public void ApplyMaterialToGameObject(GameObject targetObject, Material material)
        {
            if (targetObject == null)
            {
                throw new ArgumentNullException(nameof(targetObject));
            }

            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            var renderer = targetObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException($"Selected GameObject '{targetObject.name}' does not have a Renderer component.");
            }

            Undo.RecordObject(renderer, "Apply BlenderKit Material");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        public void ApplyMaterialToSelection(Material material)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                throw new InvalidOperationException("No GameObjects selected in the Scene to apply material.");
            }

            int appliedCount = 0;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                var renderer = selectedObjects[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Apply BlenderKit Material");
                    renderer.sharedMaterial = material;
                    EditorUtility.SetDirty(renderer);
                    appliedCount++;
                }
            }

            if (appliedCount == 0)
            {
                throw new InvalidOperationException("None of the selected GameObjects contain a Renderer component.");
            }
        }

        private static string SanitizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "Material";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string clean = rawName;
            for (int i = 0; i < invalidChars.Length; i++)
            {
                clean = clean.Replace(invalidChars[i], '_');
            }

            return clean.Trim();
        }
    }
}
