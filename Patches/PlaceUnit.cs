using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using static EditorPlus.Plugin;
using System.Reflection;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace EditorPlus.Patches
{
    [HarmonyPatch]
    static class UnitMenu_PlaceUnit_Patch
    {
        static Type _unitMenuType;
        static MethodInfo _startPlaceUnitMethod;
        static MethodInfo _placeUnitMethod;
        static bool _patchApplied = false;
        
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
        
        static bool Prepare()
        {
            Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit patch: Prepare() called");
            // Always return true - we'll handle type lookup in TargetMethod()
            // This allows the patch to be registered even if UnitMenu isn't loaded yet
            return true;
        }
        
        static MethodBase TargetMethod()
        {
            Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit patch: TargetMethod() called");
            var type = GetUnitMenuType();
            if (type == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: Could not find UnitMenu type in TargetMethod!");
                return null;
            }
            _placeUnitMethod = AccessTools.Method(type, "PlaceUnit", Type.EmptyTypes);
            if (_placeUnitMethod == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: Could not find PlaceUnit method in TargetMethod!");
                return null;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit patch: Found method {_placeUnitMethod.Name}, will apply patch");
            return _placeUnitMethod;
        }

        static void Prefix(object __instance, out HashSet<SavedUnit> __state)
        {
            // Capture units BEFORE placement in Prefix
            __state = new HashSet<SavedUnit>();
            
            if (Plugin.Instance != null && Plugin.Instance.holdpos)
            {
                try
                {
                    var mission = MissionManager.CurrentMission;
                    if (mission != null)
                    {
                        var savedUnitsField = mission.GetType().GetField("savedUnits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (savedUnitsField != null)
                        {
                            var savedUnits = savedUnitsField.GetValue(mission);
                            if (savedUnits is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var item in enumerable)
                                {
                                    if (item is SavedUnit su && su != null)
                                    {
                                        __state.Add(su);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] PlaceUnit: Prefix - Failed to get units before: {ex.Message}");
                }
            }
        }

        static void Postfix(object __instance, HashSet<SavedUnit> __state)
        {
            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Postfix called, holdpos={Plugin.Instance?.holdpos}, ctrl={Input.GetKey(KeyCode.LeftControl)}, unitsBefore={__state?.Count ?? 0}");
            
            // Apply hold position IMMEDIATELY to all units that don't have it
            // This is critical for "place more" - don't wait for frames
            if (Plugin.Instance != null && Plugin.Instance.holdpos)
            {
                Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Applying hold position immediately (tracking {__state?.Count ?? 0} existing units)");
                try
                {
                    ApplyHoldPositionImmediate();
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[EditorPlus] PlaceUnit: Failed to apply hold position immediately: {ex.Message}");
                }
                
                // Also start the coroutine for continuous monitoring, passing the unit set
                Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit: Starting ApplyHoldPositionToPlacedUnit coroutine");
                try
                {
                    if (Plugin.Instance != null)
                    {
                        Plugin.Instance.StartCoroutine(ApplyHoldPositionToPlacedUnit(__state ?? new HashSet<SavedUnit>()));
                    }
                    else
                    {
                        Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit: Plugin.Instance is null, cannot start coroutine");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[EditorPlus] PlaceUnit: Failed to start coroutine: {ex.Message}");
                }
            }
            
            if (Input.GetKey(KeyCode.LeftControl))
            {
                var type = GetUnitMenuType();
                if (type == null) return;
                
                if (_startPlaceUnitMethod == null)
                {
                    _startPlaceUnitMethod = AccessTools.Method(type, "StartPlaceUnit", Type.EmptyTypes);
                }
                
                if (_startPlaceUnitMethod != null && Plugin.Instance != null)
                {
                    Plugin.Instance.StartCoroutine(CallAtEndOfFrame(__instance));
                }
            }
        }
        
        static void ApplyHoldPositionImmediate()
        {
            if (Plugin.Instance == null || !Plugin.Instance.holdpos) return;

            var mission = MissionManager.CurrentMission;
            if (mission == null) return;

            try
            {
                var savedUnitsField = mission.GetType().GetField("savedUnits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (savedUnitsField == null)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit: IMMEDIATE - savedUnits field not found");
                    return;
                }

                var savedUnits = savedUnitsField.GetValue(mission);
                if (savedUnits is System.Collections.IEnumerable enumerable)
                {
                    int appliedCount = 0;
                    int checkedCount = 0;
                    foreach (var item in enumerable)
                    {
                        if (item is SavedUnit su && su != null)
                        {
                            checkedCount++;
                            bool currentValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                            if (!currentValue)
                            {
                                Patches.HoldPositionHelper.ApplyToSavedUnit(su, true);
                                // Verify it was applied
                                bool afterValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                if (afterValue)
                                {
                                    Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: IMMEDIATE - Applied hold position to {su.type}, verified={afterValue}");
                                    appliedCount++;
                                }
                                else
                                {
                                    Plugin.Logger?.LogWarning($"[EditorPlus] PlaceUnit: IMMEDIATE - Failed to apply hold position to {su.type}! Retrying...");
                                    // Retry with direct field access
                                    var field = su.GetType().GetField("holdPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                    if (field != null)
                                    {
                                        field.SetValue(su, true);
                                        afterValue = (bool)field.GetValue(su);
                                        if (afterValue)
                                        {
                                            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: IMMEDIATE - Retry succeeded for {su.type}");
                                            appliedCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (appliedCount > 0)
                    {
                        Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: IMMEDIATE - Applied hold position to {appliedCount} of {checkedCount} unit(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[EditorPlus] PlaceUnit: IMMEDIATE - Error applying hold position: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        static IEnumerator ApplyHoldPositionToPlacedUnit(HashSet<SavedUnit> unitsBefore)
        {
            int unitCountBefore = unitsBefore?.Count ?? 0;
            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: ApplyHoldPositionToPlacedUnit coroutine started (monitoring for 60 frames, tracking {unitCountBefore} existing units)");
            
            // Apply immediately (don't wait)
            ApplyHoldPositionImmediate();
            
            // Then continue monitoring for 60 frames to catch all "place more" units
            // Increased from 30 to 60 to catch units that might be registered late
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                
                if (Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
                
                var mission = MissionManager.CurrentMission;
                if (mission == null) yield break;
                
                // Check for NEW units that weren't there before placement
                // This is critical for "place more" - we need to target the newly placed units
                try
                {
                    var savedUnitsField = mission.GetType().GetField("savedUnits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (savedUnitsField != null)
                    {
                        var savedUnits = savedUnitsField.GetValue(mission);
                        if (savedUnits is System.Collections.IEnumerable enumerable)
                        {
                            int appliedCount = 0;
                            int newUnitCount = 0;
                            foreach (var item in enumerable)
                            {
                                if (item is SavedUnit su && su != null)
                                {
                                    // Check if this is a NEW unit (wasn't in the list before placement)
                                    bool isNewUnit = unitsBefore == null || !unitsBefore.Contains(su);
                                    
                                    if (isNewUnit)
                                    {
                                        newUnitCount++;
                                        // For new units, ALWAYS apply hold position aggressively
                                        // Check current value first
                                        bool currentValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                        
                                        // Apply hold position multiple times to ensure it sticks
                                        for (int retry = 0; retry < 3; retry++)
                                        {
                                            Patches.HoldPositionHelper.ApplyToSavedUnit(su, true);
                                            bool afterValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                            
                                            if (afterValue)
                                            {
                                                if (retry > 0 || !currentValue)
                                                {
                                                    Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Applied hold position to NEW unit {su.type} on frame {frame} (retry {retry}), before={currentValue}, after={afterValue}");
                                                    appliedCount++;
                                                }
                                                break; // Success, exit retry loop
                                            }
                                            
                                            // If still not set, try direct field access
                                            if (retry == 2)
                                            {
                                                var field = su.GetType().GetField("holdPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                                if (field != null)
                                                {
                                                    field.SetValue(su, true);
                                                    afterValue = (bool)field.GetValue(su);
                                                    if (afterValue)
                                                    {
                                                        Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Applied hold position to NEW unit {su.type} via direct field access on frame {frame}");
                                                        appliedCount++;
                                                    }
                                                }
                                            }
                                        }
                                        
                                        // Add to tracking set so we don't process it again in this coroutine
                                        if (unitsBefore != null)
                                        {
                                            unitsBefore.Add(su);
                                        }
                                    }
                                    else
                                    {
                                        // For existing units, only apply if it's not set
                                        bool currentValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                        if (!currentValue)
                                        {
                                            Patches.HoldPositionHelper.ApplyToSavedUnit(su, true);
                                            bool afterValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                            if (afterValue)
                                            {
                                                Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Applied hold position to existing unit {su.type} on frame {frame}, before={currentValue}, after={afterValue}");
                                                appliedCount++;
                                            }
                                        }
                                    }
                                }
                            }
                            
                            if (appliedCount > 0 || (newUnitCount > 0 && frame % 5 == 0))
                            {
                                Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit: Frame {frame} - Found {newUnitCount} new unit(s), applied hold position to {appliedCount} unit(s)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] PlaceUnit: Error applying hold position on frame {frame}: {ex.Message}");
                }
            }
            
            Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit: ApplyHoldPositionToPlacedUnit coroutine completed");
        }
        
        static IEnumerator CallAtEndOfFrame(object instance)
        {
            yield return new WaitForEndOfFrame();
            _startPlaceUnitMethod?.Invoke(instance, null);
        }
        
        public static void TryApplyPatchDelayed()
        {
            if (_patchApplied)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit patch: Already applied, skipping");
                return;
            }
            
            Plugin.Logger?.LogInfo("[EditorPlus] PlaceUnit patch: TryApplyPatchDelayed() called");
            var type = GetUnitMenuType();
            if (type == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: UnitMenu type still not found in TryApplyPatchDelayed");
                return;
            }
            
            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit patch: Found UnitMenu type: {type.FullName}");
            
            _placeUnitMethod = AccessTools.Method(type, "PlaceUnit", Type.EmptyTypes);
            if (_placeUnitMethod == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: PlaceUnit method still not found in TryApplyPatchDelayed");
                return;
            }
            
            Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit patch: Found PlaceUnit method: {_placeUnitMethod.Name}");
            
            try
            {
                // Use the existing Harmony instance from Plugin to avoid conflicts
                Harmony harmony = Plugin.Instance?._harmony;
                
                // Fallback to creating a new instance if we can't access the existing one
                if (harmony == null)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: Plugin.Instance._harmony is null, creating new instance");
                    harmony = new Harmony("com.nikkorap.EditorPlus");
                }
                
                var postfixMethod = typeof(UnitMenu_PlaceUnit_Patch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    Plugin.Logger?.LogError("[EditorPlus] PlaceUnit patch: Could not find Postfix method!");
                    return;
                }
                
                harmony.Patch(_placeUnitMethod, postfix: new HarmonyMethod(postfixMethod));
                _patchApplied = true;
                Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit patch: Successfully applied patch to {_placeUnitMethod.Name} (Postfix: {postfixMethod.Name})");
                
                // Verify the patch was applied
                var patches = Harmony.GetPatchInfo(_placeUnitMethod);
                if (patches != null && patches.Postfixes != null && patches.Postfixes.Count > 0)
                {
                    Plugin.Logger?.LogInfo($"[EditorPlus] PlaceUnit patch: Verified - {patches.Postfixes.Count} postfix patch(es) active");
                }
                else
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] PlaceUnit patch: WARNING - Patch may not have been applied correctly!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[EditorPlus] PlaceUnit patch: Failed to apply patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}