using System;
using Newtonsoft.Json.Linq;
using NuclearOption;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus.AtomicBuilder
{
    internal static class BlueprintSpawner
    {
        static int _spawnCounter;

        internal static bool TrySpawnEntry(
            MissionEditor editor,
            Spawner spawner,
            JObject entry,
            GlobalPosition pasteAnchorGlobal,
            out Unit unit,
            bool syncPhysics = true)
        {
            unit = null;
            string type = entry.Value<string>("type");
            if (string.IsNullOrEmpty(type)) return false;

            if (!Encyclopedia.Lookup.TryGetValue(type, out UnitDefinition definition))
            {
                Plugin.Logger?.LogWarning($"[AtomicBuilder] Unknown type: {type}");
                return false;
            }

            Vector3 rel = AtomicBuilderMath.GetCoords(entry);
            GlobalPosition gpos;
            try
            {
                gpos = pasteAnchorGlobal + rel;
            }
            catch
            {
                gpos = new GlobalPosition(pasteAnchorGlobal.AsVector3() + rel);
            }

            // Ship elevation only when spawning via blueprint spawner path (formation paste uses terrain clamp, not +35)

            Quaternion rot = AtomicBuilderMath.GetRotation(entry);
            string faction = entry.Value<string>("faction") ?? "";
            FactionHQ hq = !string.IsNullOrEmpty(faction) ? FactionRegistry.HqFromName(faction) : null;

            string uniqueName = (entry.Value<string>("UniqueName") ?? definition.unitPrefab.name)
                + "_AB" + (++_spawnCounter);

            unit = spawner.SpawnFromUnitDefinitionInEditor(definition, gpos, rot, hq, uniqueName);
            if (unit == null) return false;

            if (syncPhysics)
                Physics.SyncTransforms();
            SavedUnit saved = editor.RegisterNewUnit(unit, uniqueName);
            if (saved != null)
            {
                saved.globalPosition = gpos;
                saved.rotation = rot;
                if (!string.IsNullOrEmpty(faction))
                    saved.faction = faction;

                if (Plugin.Instance != null && Plugin.Instance.holdpos)
                    Patches.HoldPositionHelper.ApplyToSavedUnit(saved, true);

                if (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain)
                    GroupCopyPaste.ClampSpawnedUnit(unit, saved, definition);
            }

            return true;
        }
    }
}
