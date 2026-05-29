using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus
{
    internal static class UnitNaming
    {
        internal static string GetUniqueName(SavedUnit savedUnit)
        {
            if (savedUnit == null) return null;
            try
            {
                return savedUnit.UniqueName;
            }
            catch
            {
                return TryGetUniqueNameViaReflection(savedUnit);
            }
        }

        internal static bool TrySetUniqueName(SavedUnit savedUnit, string uniqueName)
        {
            if (savedUnit == null || string.IsNullOrEmpty(uniqueName))
                return false;

            try
            {
                savedUnit.UniqueName = uniqueName;
                return true;
            }
            catch
            {
                return TrySetUniqueNameViaReflection(savedUnit, uniqueName);
            }
        }

        internal static HashSet<string> CollectExistingUniqueNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var mission = MissionManager.CurrentMission;
                if (mission != null)
                {
                    FieldInfo savedUnitsField = mission.GetType().GetField(
                        "savedUnits",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (savedUnitsField?.GetValue(mission) is IEnumerable savedUnits)
                    {
                        foreach (object item in savedUnits)
                        {
                            if (item is SavedUnit su)
                            {
                                string n = GetUniqueName(su);
                                if (!string.IsNullOrEmpty(n))
                                    names.Add(n);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] CollectExistingUniqueNames mission scan failed: {ex.Message}");
            }

            foreach (Unit unit in UnityEngine.Object.FindObjectsOfType<Unit>())
            {
                if (unit?.SavedUnit == null) continue;
                string n = GetUniqueName(unit.SavedUnit);
                if (!string.IsNullOrEmpty(n))
                    names.Add(n);
            }

            return names;
        }

        internal static string EnsureUnique(string baseName, HashSet<string> usedNames)
        {
            if (string.IsNullOrEmpty(baseName))
                baseName = "Unit";

            if (usedNames == null || !usedNames.Contains(baseName))
                return baseName;

            for (int i = 2; i < 10000; i++)
            {
                string candidate = $"{baseName}_{i}";
                if (!usedNames.Contains(candidate))
                    return candidate;
            }

            return baseName + "_" + UnityEngine.Random.Range(10000, 99999);
        }

        static string TryGetUniqueNameViaReflection(SavedUnit savedUnit)
        {
            Type t = savedUnit.GetType();
            foreach (string name in new[] { "UniqueName", "uniqueName" })
            {
                PropertyInfo prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    try { return prop.GetValue(savedUnit) as string; }
                    catch { }
                }

                FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    try { return field.GetValue(savedUnit) as string; }
                    catch { }
                }
            }

            return null;
        }

        static bool TrySetUniqueNameViaReflection(SavedUnit savedUnit, string uniqueName)
        {
            try
            {
                Type savedUnitType = savedUnit.GetType();

                PropertyInfo prop = savedUnitType.GetProperty("uniqueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(savedUnit, uniqueName);
                    return true;
                }

                FieldInfo field = savedUnitType.GetField("uniqueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    field.SetValue(savedUnit, uniqueName);
                    return true;
                }

                prop = savedUnitType.GetProperty("UniqueName", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(savedUnit, uniqueName);
                    return true;
                }

                field = savedUnitType.GetField("UniqueName", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(savedUnit, uniqueName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[EditorPlus] Error setting unique name: {ex.Message}");
            }

            return false;
        }
    }
}
