using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus.Patches
{
    internal static class HoldPositionHelper
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void ApplyToSavedUnit(SavedUnit su, bool value)
        {
            if (su == null) return;
            
            bool success = false;
            string unitType = su.type ?? "unknown";
            Type t = su.GetType();
            
            // CRITICAL: Always set the field directly using reflection to bypass any property logic
            // This ensures the actual underlying value is set, regardless of property getters/setters
            FieldInfo field = t.GetField("holdPosition", Flags) ?? t.GetField("HoldPosition", Flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                try 
                { 
                    field.SetValue(su, value);
                    // Verify by reading the field directly (bypasses getter patch)
                    bool verified = (bool)field.GetValue(su);
                    success = true;
                    // Only log if verification fails
                    if (verified != value)
                    {
                        Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to apply via field to {t.Name} {unitType} - set to {value} but got {verified}");
                    }
                    // Always return after setting field - this is the most reliable method
                    return;
                } 
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to set field on {t.Name} {unitType}: {ex.Message}");
                }
            }
            
            // Fallback: Try direct property access (for SavedVehicle and SavedShip)
            if (su is SavedVehicle v) 
            { 
                v.holdPosition = value; 
                // Verify by reading field directly
                bool verified = GetHoldPosition(v);
                success = true;
                if (verified != value)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to apply to SavedVehicle {unitType} - set to {value} but got {verified}");
                }
                return; 
            }
            if (su is SavedShip s) 
            { 
                s.holdPosition = value; 
                // Verify by reading field directly
                bool verified = GetHoldPosition(s);
                success = true;
                if (verified != value)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to apply to SavedShip {unitType} - set to {value} but got {verified}");
                }
                return; 
            }
            
            // Last resort: Try property setter via reflection
            PropertyInfo prop = t.GetProperty("holdPosition", Flags) ?? t.GetProperty("HoldPosition", Flags);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
            {
                try 
                { 
                    prop.SetValue(su, value, null);
                    // Verify by reading field directly (bypasses getter)
                    bool verified = GetHoldPosition(su);
                    success = true;
                    if (verified != value)
                    {
                        Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to apply via property to {t.Name} {unitType} - set to {value} but got {verified}");
                    }
                    return; 
                } 
                catch (Exception ex) 
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Failed to set property on {t.Name} {unitType}: {ex.Message}");
                }
            }
            
            if (!success)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionHelper: Could not apply hold position to {t.Name} {unitType} - no property or field found");
            }
        }
        
        public static bool GetHoldPosition(SavedUnit su)
        {
            if (su == null) return false;
            
            // IMPORTANT: We must read the underlying field directly, NOT the property getter,
            // because the getter is patched to always return true when holdpos is enabled.
            // Reading the field directly allows us to check the actual value to see if it needs to be applied.
            
            Type t = su.GetType();
            
            // Try to read the underlying field directly (bypasses getter patch)
            FieldInfo field = t.GetField("holdPosition", Flags) ?? t.GetField("HoldPosition", Flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                try { return (bool)field.GetValue(su); } catch { }
            }
            
            // Fallback: try property getter (but this will be patched, so it may always return true)
            // This is only used if no field is found
            PropertyInfo prop = t.GetProperty("holdPosition", Flags) ?? t.GetProperty("HoldPosition", Flags);
            if (prop != null && prop.CanRead && prop.PropertyType == typeof(bool))
            {
                try { return (bool)prop.GetValue(su, null); } catch { }
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(MissionEditor), nameof(MissionEditor.RegisterNewUnit), new[] { typeof(Unit), typeof(string) })]
    internal static class MissionEditor_RegisterNewUnit_Patch
    {
        static bool Prepare()
        {
            // Verify the method exists
            var method = AccessTools.Method(typeof(MissionEditor), nameof(MissionEditor.RegisterNewUnit), new[] { typeof(Unit), typeof(string) })
                ?? AccessTools.Method(typeof(MissionEditor), "RegisterNewUnit");
            if (method == null)
            {
                Plugin.Logger?.LogError("[EditorPlus] RegisterNewUnit patch: Could not find RegisterNewUnit method!");
                return false;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] RegisterNewUnit patch: Found method {method.Name}, will apply patch");
            return true;
        }

        static void Postfix(ref SavedUnit __result, Unit __0)
        {
            if (__result == null || Plugin.Instance == null) return;
            
            // Apply hold position if enabled
            if (Plugin.Instance.holdpos)
            {
                HoldPositionHelper.ApplyToSavedUnit(__result, true);
                
                // Re-apply hold position after a frame to ensure it sticks
                // This is important for "place more" where units might get their hold position reset
                try
                {
                    Plugin.Instance.StartCoroutine(ReapplyHoldPosition(__result));
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[EditorPlus] RegisterNewUnit: Failed to start coroutine: {ex.Message}");
                }
            }
            
            // If no clip is enabled, check if we have a stored position for this unit
            if (Plugin.Instance.ignoreTerrain && __0 != null && __result != null)
            {
                if (NoClipPositionStore.TryGetPosition(__0, out GlobalPosition storedPos))
                {
                    // Restore the position that was stored before RegisterNewUnit
                    Plugin.Instance.StartCoroutine(ReapplyNoClipPosition(__0, __result, storedPos));
                    // Clear the stored position after use
                    NoClipPositionStore.ClearPosition(__0);
                }
            }
        }
        
        private static IEnumerator ReapplyHoldPosition(SavedUnit savedUnit)
        {
            if (savedUnit == null || Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
            
            // Apply immediately (before waiting)
            HoldPositionHelper.ApplyToSavedUnit(savedUnit, true);
            
            // Wait a frame to ensure any other code that might reset hold position has run
            yield return null;
            
            if (savedUnit == null || Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
            
            // Re-apply hold position after first frame
            HoldPositionHelper.ApplyToSavedUnit(savedUnit, true);
            
            // Apply multiple times over many frames to ensure it sticks
            // Increased to 20 frames for maximum persistence (especially for "place more")
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                
                if (savedUnit == null || Plugin.Instance == null || !Plugin.Instance.holdpos) yield break;
                
                // Check value before applying
                bool beforeValue = HoldPositionHelper.GetHoldPosition(savedUnit);
                
                // Re-apply hold position
                HoldPositionHelper.ApplyToSavedUnit(savedUnit, true);
                
                // Verify it's still set after applying
                bool afterValue = HoldPositionHelper.GetHoldPosition(savedUnit);
                
                // Only log if there's an issue
                if (beforeValue != afterValue || !afterValue)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] ReapplyHoldPosition: Frame {i + 2} - hold position was reset! before={beforeValue}, after={afterValue}");
                }
            }
        }
        
        private static IEnumerator ReapplyNoClipPosition(Unit unit, SavedUnit savedUnit, GlobalPosition desiredGlobalPos)
        {
            // Wait a frame for RegisterNewUnit to complete any clamping
            yield return null;
            
            if (unit == null || savedUnit == null || Plugin.Instance == null || !Plugin.Instance.ignoreTerrain) yield break;
            
            var globalVec = desiredGlobalPos.AsVector3();
            
            // Calculate local position from global
            // Try to get origin offset from camera
            Vector3 originOffset = Vector3.zero;
            try
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camLocalPos = cam.transform.position;
                    GlobalPosition camGlobalPos = camLocalPos.ToGlobalPosition();
                    Vector3 camGlobalVec = camGlobalPos.AsVector3();
                    originOffset = camGlobalVec - camLocalPos;
                }
            }
            catch { }
            
            Vector3 localPos = globalVec - originOffset;
            
            // Re-apply the position via wrapper
            try
            {
                var posWrapper = savedUnit.PositionWrapper;
                if (posWrapper != null)
                {
                    posWrapper.SetValue(desiredGlobalPos, null, true);
                }
            }
            catch
            {
                try
                {
                    var posWrapper = savedUnit.PositionWrapper;
                    if (posWrapper != null)
                    {
                        posWrapper.SetValue(desiredGlobalPos, null, false);
                    }
                }
                catch { }
            }
            
            // Also set transform directly
            unit.transform.position = localPos;
            savedUnit.globalPosition = desiredGlobalPos;
            Physics.SyncTransforms();
            
            Plugin.Logger?.LogInfo($"[EditorPlus] No clip: Re-applied position after RegisterNewUnit - local={localPos}, global={globalVec}");
            
            // Apply multiple times over several frames to ensure it sticks
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                
                if (unit == null || savedUnit == null || Plugin.Instance == null || !Plugin.Instance.ignoreTerrain) yield break;
                
                // Re-calculate origin offset in case it changed
                try
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 camLocalPos = cam.transform.position;
                        GlobalPosition camGlobalPos = camLocalPos.ToGlobalPosition();
                        Vector3 camGlobalVec = camGlobalPos.AsVector3();
                        originOffset = camGlobalVec - camLocalPos;
                    }
                }
                catch { }
                
                localPos = globalVec - originOffset;
                
                // Apply via wrapper
                try
                {
                    var posWrapper = savedUnit.PositionWrapper;
                    if (posWrapper != null)
                    {
                        posWrapper.SetValue(desiredGlobalPos, null, true);
                    }
                }
                catch { }
                
                // Set transform
                unit.transform.position = localPos;
                savedUnit.globalPosition = desiredGlobalPos;
                Physics.SyncTransforms();
                
                Plugin.Logger?.LogInfo($"[EditorPlus] No clip: Applied position again (frame {i + 2})");
            }
        }
    }
}