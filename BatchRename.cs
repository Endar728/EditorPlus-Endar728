using System.Collections.Generic;
using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace EditorPlus
{
    internal static class BatchRename
    {
        internal static int CountSelectedUnits()
        {
            var selection = SceneSingleton<UnitSelection>.i;
            var groupFollowers = Object.FindObjectOfType<GroupFollowers>();
            return EditorSelectionHelper.ResolveSelectedUnits(selection, groupFollowers, out _).Count;
        }

        internal static bool TryRenameSelected(string namePrefix, int startIndex, out string message)
        {
            var selection = SceneSingleton<UnitSelection>.i;
            var groupFollowers = Object.FindObjectOfType<GroupFollowers>();
            List<Unit> units = EditorSelectionHelper.ResolveSelectedUnits(selection, groupFollowers, out string sourceLabel);

            if (units == null || units.Count == 0)
            {
                message = "No units selected.";
                return false;
            }

            namePrefix = namePrefix?.Trim();
            if (string.IsNullOrEmpty(namePrefix))
            {
                message = "Enter a name prefix.";
                return false;
            }

            if (startIndex < 0)
                startIndex = 0;

            units.Sort(CompareUnitsForRename);

            var usedNames = UnitNaming.CollectExistingUniqueNames();
            foreach (Unit unit in units)
            {
                if (unit?.SavedUnit == null) continue;
                string existing = UnitNaming.GetUniqueName(unit.SavedUnit);
                if (!string.IsNullOrEmpty(existing))
                    usedNames.Remove(existing);
            }

            int index = startIndex;
            int renamed = 0;
            foreach (Unit unit in units)
            {
                if (unit?.SavedUnit == null) continue;

                string newName = UnitNaming.EnsureUnique($"{namePrefix}_{index}", usedNames);
                if (UnitNaming.TrySetUniqueName(unit.SavedUnit, newName))
                {
                    unit.name = newName;
                    usedNames.Add(newName);
                    renamed++;
                }

                index++;
            }

            if (renamed == 0)
            {
                message = "Could not rename any selected units.";
                return false;
            }

            SceneSingleton<MissionEditor>.i?.CheckAutoSave();
            Plugin.Logger?.LogInfo($"[EditorPlus] Batch rename ({sourceLabel}): renamed {renamed} unit(s) with prefix '{namePrefix}'");
            message = $"Renamed {renamed} unit(s) to {namePrefix}_{{n}}.";
            return true;
        }

        static int CompareUnitsForRename(Unit a, Unit b)
        {
            if (!a) return b ? 1 : 0;
            if (!b) return -1;

            Vector3 pa = a.transform.position;
            Vector3 pb = b.transform.position;
            int cmp = pa.x.CompareTo(pb.x);
            if (cmp != 0) return cmp;
            cmp = pa.z.CompareTo(pb.z);
            if (cmp != 0) return cmp;
            return pa.y.CompareTo(pb.y);
        }
    }
}
