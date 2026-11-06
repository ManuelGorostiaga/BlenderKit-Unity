using System;
using System.Collections.Generic;

namespace BlenderKitUnity.Editor.API
{
    [Serializable]
    public class SearchResponse
    {
        public int count;
        public string next;
        public string previous;
        public List<AssetData> results = new List<AssetData>();
    }

    [Serializable]
    public class AssetData
    {
        public string id;
        public string name;
        public string displayName;
        public string description;
        public string assetType;
        public string category;
        public bool isFree;
        public string license;
        public string thumbnailSmallUrl;
        public string thumbnailMiddleUrl;
        public string thumbnailLargeUrl;
        public List<AssetFile> files = new List<AssetFile>();
        public List<AssetParam> parameters = new List<AssetParam>();

        public string GetEffectiveName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            throw new InvalidOperationException("Asset has neither displayName nor name.");
        }

        public AssetFile GetTargetFile(string targetResolution)
        {
            if (files == null || files.Count == 0)
            {
                throw new InvalidOperationException($"Asset {id} does not contain any downloadable files.");
            }

            string expectedType = targetResolution switch
            {
                "512" => "resolution_0_5K",
                "1K" => "resolution_1K",
                "2K" => "resolution_2K",
                "4K" => "resolution_4K",
                _ => "blend"
            };

            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i].fileType, expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    return files[i];
                }
            }

            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i].fileType, "blend", StringComparison.OrdinalIgnoreCase))
                {
                    return files[i];
                }
            }

            return files[0];
        }
    }

    [Serializable]
    public class AssetFile
    {
        public string id;
        public string fileType;
        public string downloadUrl;
        public long fileUploadSize;
        public string filename;
    }

    [Serializable]
    public class AssetParam
    {
        public string parameterType;
        public string value;
    }

    [Serializable]
    public class DownloadResponse
    {
        public string fileType;
        public string uuid;
        public string filePath;
        public List<FilePathItem> filePaths = new List<FilePathItem>();

        public string GetResolvedUrl()
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                return filePath;
            }

            if (filePaths != null && filePaths.Count > 0 && !string.IsNullOrEmpty(filePaths[0].url))
            {
                return filePaths[0].url;
            }

            throw new InvalidOperationException("BlenderKit download response does not contain a valid URL.");
        }
    }

    [Serializable]
    public class FilePathItem
    {
        public string url;
    }

    [Serializable]
    public class ExtractedMaterialMeta
    {
        public string materialName;
        public string albedoMap;
        public string normalMap;
        public string roughnessMap;
        public string metallicMap;
        public string occlusionMap;
        public string emissionMap;
        public string heightMap;
        public float[] baseColor = new float[] { 1f, 1f, 1f, 1f };
        public float metallic = 0f;
        public float roughness = 0.5f;
        public float normalStrength = 1.0f;
        public float[] emissionColor = new float[] { 0f, 0f, 0f, 1f };
    }
}
