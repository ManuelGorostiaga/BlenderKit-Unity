using System;
using System.IO;
using BlenderKitUnity.Editor.API;

namespace BlenderKitUnity.Editor.Pipeline
{
    public static class BlenderFinder
    {
        public static string ResolveBlenderExecutable()
        {
            string configuredPath = BlenderKitSettings.BlenderExecutablePath;
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            string detectedPath = TryDetectBlender();
            if (!string.IsNullOrEmpty(detectedPath) && File.Exists(detectedPath))
            {
                BlenderKitSettings.BlenderExecutablePath = detectedPath;
                return detectedPath;
            }

            throw new FileNotFoundException(
                "Blender executable was not found. Please install Blender or configure the path to 'blender.exe' in BlenderKit Settings.");
        }

        public static bool IsBlenderAvailable()
        {
            string configuredPath = BlenderKitSettings.BlenderExecutablePath;
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                return true;
            }

            string detected = TryDetectBlender();
            return !string.IsNullOrEmpty(detected) && File.Exists(detected);
        }

        private static string TryDetectBlender()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string blenderFoundationDir = Path.Combine(programFiles, "Blender Foundation");

            if (Directory.Exists(blenderFoundationDir))
            {
                string[] subDirs = Directory.GetDirectories(blenderFoundationDir);
                Array.Sort(subDirs, (a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));

                for (int i = 0; i < subDirs.Length; i++)
                {
                    string candidate = Path.Combine(subDirs[i], "blender.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string steamBlender = Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Blender", "blender.exe");
            if (File.Exists(steamBlender))
            {
                return steamBlender;
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] entries = pathEnv.Split(Path.PathSeparator);
                for (int i = 0; i < entries.Length; i++)
                {
                    string candidate = Path.Combine(entries[i].Trim(), "blender.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
