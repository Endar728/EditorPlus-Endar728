using System;
using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Linq;

namespace EditorPlus.AtomicBuilder
{
    internal static class AtomicBuilderPaths
    {
        internal static readonly string[] UnitCategories =
        {
            "vehicles", "ships", "airbases", "buildings", "aircraft",
            "scenery", "missiles", "containers", "pilots"
        };

        static string _blueprintsRoot;

        /// <summary>
        /// &lt;game install&gt;/Blueprints — created on first access and at plugin startup.
        /// </summary>
        internal static string BlueprintsRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_blueprintsRoot))
                    _blueprintsRoot = Path.Combine(BepInEx.Paths.GameRootPath, "Blueprints");
                return _blueprintsRoot;
            }
        }

        internal static void EnsureBlueprintsFolder()
        {
            Directory.CreateDirectory(BlueprintsRoot);
        }

        internal static string MissionsRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Low", "Shockfront", "NuclearOption", "Missions");

        internal static string BackupsRoot =>
            Path.Combine(BepInEx.Paths.PluginPath, "EditorPlus", "Backups");

        internal static string BlueprintFile(string name)
        {
            string safe = SanitizeFileName(name);
            return Path.Combine(BlueprintsRoot, safe + ".json");
        }

        internal static List<string> ListBlueprintNames()
        {
            if (!Directory.Exists(BlueprintsRoot)) return new List<string>();
            return Directory.GetFiles(BlueprintsRoot, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "blueprint";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        internal static string TryGetCurrentMissionJsonPath()
        {
            string missionName = MissionNameResolver.GetCurrentMissionName();
            if (string.IsNullOrEmpty(missionName)) return null;
            string path = Path.Combine(MissionsRoot, missionName, missionName + ".json");
            return File.Exists(path) ? path : null;
        }
    }
}
