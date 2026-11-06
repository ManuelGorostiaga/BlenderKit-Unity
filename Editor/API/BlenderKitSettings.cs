using UnityEditor;
using UnityEngine;

namespace BlenderKitUnity.Editor.API
{
    public static class BlenderKitSettings
    {
        private const string BlenderPathKey = "BlenderKit_BlenderExecutablePath";
        private const string ApiKeyKey = "BlenderKit_ApiKey";
        private const string ResolutionKey = "BlenderKit_DefaultResolution";
        private const string TargetFolderKey = "BlenderKit_TargetFolder";
        private const string PurePbrOnlyKey = "BlenderKit_PurePbrOnly";

        public static string BlenderExecutablePath
        {
            get => EditorPrefs.GetString(BlenderPathKey, string.Empty);
            set => EditorPrefs.SetString(BlenderPathKey, value);
        }

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyKey, string.Empty);
            set => EditorPrefs.SetString(ApiKeyKey, value);
        }

        public static string DefaultResolution
        {
            get => EditorPrefs.GetString(ResolutionKey, "1K");
            set => EditorPrefs.SetString(ResolutionKey, value);
        }

        public static string TargetFolder
        {
            get => EditorPrefs.GetString(TargetFolderKey, "Assets/BlenderKit/Materials");
            set => EditorPrefs.SetString(TargetFolderKey, value);
        }

        public static bool PurePbrOnly
        {
            get => EditorPrefs.GetBool(PurePbrOnlyKey, true);
            set => EditorPrefs.SetBool(PurePbrOnlyKey, value);
        }
    }
}
