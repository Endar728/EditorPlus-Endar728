using System;
using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Linq;
using UnityEngine;

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
        static bool _loggedReady;

        /// <summary>
        /// &lt;game install&gt;/Blueprints — created on plugin load, scene load, and before any blueprint file access.
        /// </summary>
        internal static string BlueprintsRoot
        {
            get
            {
                EnsureBlueprintsFolder();
                return _blueprintsRoot ?? ResolveBlueprintsRoot();
            }
        }

        internal static bool EnsureBlueprintsFolder()
        {
            try
            {
                string root = ResolveBlueprintsRoot();
                _blueprintsRoot = root;
                Directory.CreateDirectory(root);

                if (!Directory.Exists(root))
                {
                    Plugin.Logger?.LogError($"[EditorPlus] Blueprints folder could not be created: {root}");
                    _loggedReady = false;
                    return false;
                }

                if (!_loggedReady)
                {
                    Plugin.Logger?.LogInfo($"[EditorPlus] Blueprints folder ready: {root}");
                    _loggedReady = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[EditorPlus] Blueprints folder creation failed: {ex.Message}");
                _loggedReady = false;
                return false;
            }
        }

        static string ResolveBlueprintsRoot()
        {
            string gameRoot = BepInEx.Paths.GameRootPath;
            if (string.IsNullOrWhiteSpace(gameRoot))
                gameRoot = Path.GetDirectoryName(Application.dataPath);

            if (string.IsNullOrWhiteSpace(gameRoot))
                throw new InvalidOperationException("Could not resolve Nuclear Option game root for Blueprints folder.");

            return Path.Combine(gameRoot, "Blueprints");
        }

        internal static string MissionsRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Low", "Shockfront", "NuclearOption", "Missions");

        internal static string BackupsRoot =>
            Path.Combine(BepInEx.Paths.PluginPath, "EditorPlus", "Backups");

        internal static string BlueprintFile(string name)
        {
            EnsureBlueprintsFolder();
            string safe = SanitizeFileName(name);
            return Path.Combine(BlueprintsRoot, safe + ".json");
        }

        internal static List<string> ListBlueprintNames()
        {
            if (!EnsureBlueprintsFolder() || !Directory.Exists(BlueprintsRoot))
                return new List<string>();

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
