using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EditorPlus.Patches
{
    /// <summary>
    /// Patches UnitMenu.StartPlaceUnit to ensure hold position is applied for "place more" units
    /// This patch works in conjunction with RegisterNewUnit_Patch to catch units placed via "place more"
    /// </summary>
    [HarmonyPatch]
    internal static class UnitMenu_StartPlaceUnit_Patch
    {
        static Type _unitMenuType;
        
        static Type GetUnitMenuType()
        {
            if (_unitMenuType == null)
            {
                _unitMenuType = Type.GetType("NuclearOption.MissionEditorScripts.UnitMenu, Assembly-CSharp") 
                             ?? AppDomain.CurrentDomain.GetAssemblies()
                                 .Select(a => a.GetType("NuclearOption.MissionEditorScripts.UnitMenu"))
                                 .FirstOrDefault(t => t != null);
            }
            return _unitMenuType;
        }
        
        static MethodBase TargetMethod()
        {
            var type = GetUnitMenuType();
            if (type == null) return null;
            
            var method = AccessTools.Method(type, "StartPlaceUnit", Type.EmptyTypes);
            if (method == null)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] StartPlaceUnit method not found, skipping patch (this is normal)");
            }
            else
            {
                Plugin.Logger?.LogInfo("[EditorPlus] StartPlaceUnit patch: Found method, will apply patch");
            }
            return method;
        }
        
        static void Postfix(object __instance)
        {
            if (Plugin.Instance == null || !Plugin.Instance.holdpos) return;
            
            Plugin.Logger?.LogInfo("[EditorPlus] StartPlaceUnit: Called, will monitor for newly registered unit");
            
            // Start a coroutine to check for and apply hold position to the most recently registered unit
            // This is needed for "place more" where StartPlaceUnit is called repeatedly
            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(CheckAndApplyHoldPosition());
            }
        }
        
        private static IEnumerator CheckAndApplyHoldPosition()
        {
            // Wait a few frames for the unit to be registered via RegisterNewUnit
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            
            if (Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
            
            // Get the mission to access saved units
            var mission = MissionManager.CurrentMission;
            if (mission == null) yield break;
            
            // Check all saved units and apply hold position to any that don't have it
            try
            {
                // Use reflection to get savedUnits if available
                var savedUnitsField = mission.GetType().GetField("savedUnits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (savedUnitsField != null)
                {
                    var savedUnits = savedUnitsField.GetValue(mission);
                    if (savedUnits is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is SavedUnit su && su != null)
                            {
                                // Try to get registration time or use a timestamp
                                // For now, we'll check all units and apply hold position to any that don't have it
                                bool currentValue = HoldPositionHelper.GetHoldPosition(su);
                                if (!currentValue)
                                {
                                    HoldPositionHelper.ApplyToSavedUnit(su, true);
                                    bool afterValue = HoldPositionHelper.GetHoldPosition(su);
                                    Plugin.Logger?.LogInfo($"[EditorPlus] StartPlaceUnit: Applied hold position to {su.type}, before={currentValue}, after={afterValue}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] StartPlaceUnit: Error checking units: {ex.Message}");
            }
            
            // Continue monitoring for a few more frames to catch any late registrations
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
                
                if (Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
                if (mission == null) yield break;
                
                // Re-check and re-apply if needed
                try
                {
                    var savedUnitsField = mission.GetType().GetField("savedUnits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (savedUnitsField != null)
                    {
                        var savedUnits = savedUnitsField.GetValue(mission);
                        if (savedUnits is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item is SavedUnit su && su != null)
                                {
                                    bool currentValue = HoldPositionHelper.GetHoldPosition(su);
                                    if (!currentValue)
                                    {
                                        HoldPositionHelper.ApplyToSavedUnit(su, true);
                                        bool afterValue = HoldPositionHelper.GetHoldPosition(su);
                                        Plugin.Logger?.LogWarning($"[EditorPlus] StartPlaceUnit: Re-applied hold position to {su.type} on frame {frame} (was reset!), before={currentValue}, after={afterValue}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] StartPlaceUnit: Error re-checking units: {ex.Message}");
                }
            }
        }
    }
}
