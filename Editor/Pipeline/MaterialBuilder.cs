using System;
using System.IO;
using BlenderKitUnity.Editor.API;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlenderKitUnity.Editor.Pipeline
{
    public static class MaterialBuilder
    {
        public static Material BuildAndSave(ExtractedMaterialMeta meta, string outputDirectory)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
            }

            string relativeDir = MakeRelativeAssetPath(outputDirectory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture2D albedoTexture = ConfigureAndLoadTexture(relativeDir, meta.albedoMap, TextureImporterType.Default, true);
            Texture2D normalTexture = ConfigureAndLoadTexture(relativeDir, meta.normalMap, TextureImporterType.NormalMap, false);
            Texture2D roughnessTexture = ConfigureAndLoadTexture(relativeDir, meta.roughnessMap, TextureImporterType.Default, false);
            Texture2D metallicTexture = ConfigureAndLoadTexture(relativeDir, meta.metallicMap, TextureImporterType.Default, false);
            Texture2D occlusionTexture = ConfigureAndLoadTexture(relativeDir, meta.occlusionMap, TextureImporterType.Default, false);
            Texture2D emissionTexture = ConfigureAndLoadTexture(relativeDir, meta.emissionMap, TextureImporterType.Default, true);

            Texture2D packedMetallicGloss = TryCreatePackedMetallicGloss(
                relativeDir,
                metallicTexture,
                roughnessTexture,
                meta.metallic,
                meta.roughness);

            Shader targetShader = ResolveTargetShader();
            var material = new Material(targetShader)
            {
                name = SanitizeMaterialName(meta.materialName)
            };

            bool isUrp = targetShader.name.Contains("Universal") || targetShader.name.Contains("URP");
            bool isHdrp = targetShader.name.Contains("HDRP");

            Color baseColor = new Color(meta.baseColor[0], meta.baseColor[1], meta.baseColor[2], meta.baseColor[3]);
            Color emissionColor = new Color(meta.emissionColor[0], meta.emissionColor[1], meta.emissionColor[2], meta.emissionColor[3]);

            if (isUrp)
            {
                ApplyUrpProperties(material, baseColor, albedoTexture, normalTexture, meta.normalStrength,
                    packedMetallicGloss, meta.metallic, 1f - meta.roughness, occlusionTexture, emissionTexture, emissionColor);
            }
            else if (isHdrp)
            {
                ApplyHdrpProperties(material, baseColor, albedoTexture, normalTexture, meta.normalStrength,
                    packedMetallicGloss, meta.metallic, 1f - meta.roughness, occlusionTexture, emissionTexture, emissionColor);
            }
            else
            {
                ApplyStandardProperties(material, baseColor, albedoTexture, normalTexture, meta.normalStrength,
                    packedMetallicGloss, meta.metallic, 1f - meta.roughness, occlusionTexture, emissionTexture, emissionColor);
            }

            string materialPath = Path.Combine(relativeDir, $"{material.name}.mat").Replace('\\', '/');
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material loaded = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (loaded == null)
            {
                throw new InvalidOperationException($"Created material could not be reloaded at '{materialPath}'.");
            }

            return loaded;
        }

        private static Shader ResolveTargetShader()
        {
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;
            if (currentPipeline != null)
            {
                string pipelineName = currentPipeline.GetType().Name;
                if (pipelineName.Contains("Universal") || pipelineName.Contains("URP"))
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null) return urpShader;
                }
                else if (pipelineName.Contains("HDRP") || pipelineName.Contains("HighDefinition"))
                {
                    Shader hdrpShader = Shader.Find("HDRP/Lit");
                    if (hdrpShader != null) return hdrpShader;
                }
            }

            Shader fallback = Shader.Find("Universal Render Pipeline/Lit")
                              ?? Shader.Find("Standard")
                              ?? Shader.Find("Diffuse");

            if (fallback == null)
            {
                throw new InvalidOperationException("No suitable PBR shader found in the current Unity project.");
            }

            return fallback;
        }

        private static void ApplyUrpProperties(
            Material mat,
            Color baseColor,
            Texture2D albedo,
            Texture2D normal,
            float normalScale,
            Texture2D metallicGloss,
            float metallic,
            float smoothness,
            Texture2D occlusion,
            Texture2D emission,
            Color emissionColor)
        {
            mat.SetColor("_BaseColor", baseColor);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);

            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", normalScale);
            }

            if (metallicGloss != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", metallicGloss);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
            }
            else
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (occlusion != null)
            {
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTexture("_OcclusionMap", occlusion);
            }

            if (emission != null || emissionColor.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                if (emission != null) mat.SetTexture("_EmissionMap", emission);
            }
        }

        private static void ApplyStandardProperties(
            Material mat,
            Color baseColor,
            Texture2D albedo,
            Texture2D normal,
            float normalScale,
            Texture2D metallicGloss,
            float metallic,
            float smoothness,
            Texture2D occlusion,
            Texture2D emission,
            Color emissionColor)
        {
            mat.SetColor("_Color", baseColor);
            if (albedo != null) mat.SetTexture("_MainTex", albedo);

            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", normalScale);
            }

            if (metallicGloss != null)
            {
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", metallicGloss);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Glossiness", 1f);
            }
            else
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Glossiness", smoothness);
            }

            if (occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", occlusion);
            }

            if (emission != null || emissionColor.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                if (emission != null) mat.SetTexture("_EmissionMap", emission);
            }
        }

        private static void ApplyHdrpProperties(
            Material mat,
            Color baseColor,
            Texture2D albedo,
            Texture2D normal,
            float normalScale,
            Texture2D metallicGloss,
            float metallic,
            float smoothness,
            Texture2D occlusion,
            Texture2D emission,
            Color emissionColor)
        {
            mat.SetColor("_BaseColor", baseColor);
            if (albedo != null) mat.SetTexture("_BaseColorMap", albedo);

            if (normal != null)
            {
                mat.SetTexture("_NormalMap", normal);
                mat.SetFloat("_NormalScale", normalScale);
            }

            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (emission != null || emissionColor.maxColorComponent > 0.01f)
            {
                mat.SetColor("_EmissiveColor", emissionColor);
                if (emission != null) mat.SetTexture("_EmissiveColorMap", emission);
            }
        }

        private static Texture2D ConfigureAndLoadTexture(
            string relativeDirectory,
            string filename,
            TextureImporterType importerType,
            bool isSRgb)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            string relativePath = Path.Combine(relativeDirectory, filename).Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != importerType)
                {
                    importer.textureType = importerType;
                    dirty = true;
                }

                if (importer.sRGBTexture != isSRgb)
                {
                    importer.sRGBTexture = isSRgb;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        }

        private static Texture2D TryCreatePackedMetallicGloss(
            string relativeDirectory,
            Texture2D metallicTex,
            Texture2D roughnessTex,
            float defaultMetallic,
            float defaultRoughness)
        {
            if (metallicTex == null && roughnessTex == null)
            {
                return null;
            }

            int width = metallicTex != null ? metallicTex.width : roughnessTex.width;
            int height = metallicTex != null ? metallicTex.height : roughnessTex.height;

            var readableMetallic = EnsureReadableTexture(metallicTex);
            var readableRoughness = EnsureReadableTexture(roughnessTex);

            var packed = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    float m = readableMetallic != null
                        ? readableMetallic.GetPixelBilinear(u, v).r
                        : defaultMetallic;

                    float r = readableRoughness != null
                        ? readableRoughness.GetPixelBilinear(u, v).r
                        : defaultRoughness;

                    float smoothness = Mathf.Clamp01(1f - r);
                    pixels[y * width + x] = new Color(m, 0f, 0f, smoothness);
                }
            }

            packed.SetPixels(pixels);
            packed.Apply();

            string diskPath = Path.Combine(relativeDirectory, "Packed_MetallicGloss.png").Replace('\\', '/');
            string physicalPath = Path.GetFullPath(diskPath);
            File.WriteAllBytes(physicalPath, packed.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(packed);
            if (readableMetallic != null && readableMetallic != metallicTex) UnityEngine.Object.DestroyImmediate(readableMetallic);
            if (readableRoughness != null && readableRoughness != roughnessTex) UnityEngine.Object.DestroyImmediate(readableRoughness);

            AssetDatabase.ImportAsset(diskPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(diskPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(diskPath);
        }

        private static Texture2D EnsureReadableTexture(Texture2D source)
        {
            if (source == null) return null;

            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path)) return source;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return source;
        }

        private static string MakeRelativeAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int assetsIndex = normalized.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return normalized.Substring(assetsIndex);
            }

            throw new ArgumentException($"Path '{fullPath}' is not within the Unity project Assets folder.");
        }

        private static string SanitizeMaterialName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "BlenderKit_Material";
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
