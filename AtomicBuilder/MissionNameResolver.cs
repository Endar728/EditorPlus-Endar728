using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NuclearOption;
using NuclearOption.MissionEditorScripts;

namespace EditorPlus.AtomicBuilder
{
    internal static class MissionNameResolver
    {
        internal static string GetCurrentMissionName()
        {
            try
            {
                var mission = MissionManager.CurrentMission;
                if (mission == null) return null;

                foreach (string prop in new[] { "missionName", "MissionName", "name", "Name", "fileName", "FileName" })
                {
                    var pi = mission.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pi?.GetValue(mission) is string s && !string.IsNullOrWhiteSpace(s))
                        return Path.GetFileNameWithoutExtension(s);
                }

                foreach (string field in new[] { "missionName", "MissionName", "name", "Name" })
                {
                    var fi = mission.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi?.GetValue(mission) is string s && !string.IsNullOrWhiteSpace(s))
                        return Path.GetFileNameWithoutExtension(s);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[AtomicBuilder] Mission name resolve failed: {ex.Message}");
            }

            return null;
        }
    }
}
