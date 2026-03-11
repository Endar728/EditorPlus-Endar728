using HarmonyLib;
using NuclearOption.SavedMission;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace EditorPlus.Patches
{
    /// <summary>
    /// Monitors and prevents hold position from being reset after it's been set
    /// </summary>
    [HarmonyPatch]
    internal static class HoldPositionMonitor
    {
        static bool Prepare()
        {
            var method = TargetMethod();
            if (method == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] HoldPositionMonitor: Could not find SavedVehicle.holdPosition setter, skipping patch");
                return false;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionMonitor: Found SavedVehicle.holdPosition setter {method.Name}, will apply patch");
            return true;
        }
        
        private static MethodBase TargetMethod()
        {
            // Try to find SavedVehicle.holdPosition setter
            try
            {
                var savedVehicleType = Type.GetType("NuclearOption.SavedMission.SavedVehicle, Assembly-CSharp")
                                    ?? AppDomain.CurrentDomain.GetAssemblies()
                                        .Select(a => a.GetType("NuclearOption.SavedMission.SavedVehicle"))
                                        .FirstOrDefault(t => t != null);
                
                if (savedVehicleType != null)
                {
                    var prop = savedVehicleType.GetProperty("holdPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite)
                    {
                        var setter = prop.GetSetMethod(true);
                        if (setter != null)
                        {
                            return setter;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: Error finding setter: {ex.Message}");
            }
            
            return null;
        }
        
        static void Prefix(SavedVehicle __instance, ref bool value, out bool __state)
        {
            __state = false;
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                // Store the current value by reading the field directly (bypasses getter patch)
                __state = HoldPositionHelper.GetHoldPosition(__instance);
                
                // If we're trying to set it to false, but holdpos is enabled, force it to true
                if (!value)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: Attempted to set holdPosition=false on {__instance.type}, forcing to true");
                    value = true;
                }
            }
        }
        
        static void Postfix(SavedVehicle __instance, bool __state)
        {
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                // Double-check it's still set after the setter ran
                // Read the field directly to bypass the getter patch
                bool actualValue = HoldPositionHelper.GetHoldPosition(__instance);
                if (!actualValue)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: holdPosition was reset to false on {__instance.type}, re-applying");
                    __instance.holdPosition = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Monitors SavedShip.holdPosition setter
    /// </summary>
    [HarmonyPatch]
    internal static class HoldPositionMonitor_Ship
    {
        static bool Prepare()
        {
            var method = TargetMethod();
            if (method == null)
            {
                Plugin.Logger?.LogWarning("[EditorPlus] HoldPositionMonitor_Ship: Could not find SavedShip.holdPosition setter, skipping patch");
                return false;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionMonitor_Ship: Found SavedShip.holdPosition setter {method.Name}, will apply patch");
            return true;
        }
        
        private static MethodBase TargetMethod()
        {
            try
            {
                var savedShipType = Type.GetType("NuclearOption.SavedMission.SavedShip, Assembly-CSharp")
                                ?? AppDomain.CurrentDomain.GetAssemblies()
                                    .Select(a => a.GetType("NuclearOption.SavedMission.SavedShip"))
                                    .FirstOrDefault(t => t != null);
                
                if (savedShipType != null)
                {
                    var prop = savedShipType.GetProperty("holdPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite)
                    {
                        var setter = prop.GetSetMethod(true);
                        if (setter != null)
                        {
                            return setter;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: Error finding SavedShip setter: {ex.Message}");
            }
            
            return null;
        }
        
        static void Prefix(SavedShip __instance, ref bool value, out bool __state)
        {
            __state = false;
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                // Store the current value by reading the field directly (bypasses getter patch)
                __state = HoldPositionHelper.GetHoldPosition(__instance);
                if (!value)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: Attempted to set holdPosition=false on {__instance.type}, forcing to true");
                    value = true;
                }
            }
        }
        
        static void Postfix(SavedShip __instance, bool __state)
        {
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                // Read the field directly to bypass the getter patch
                bool actualValue = HoldPositionHelper.GetHoldPosition(__instance);
                if (!actualValue)
                {
                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionMonitor: holdPosition was reset to false on SavedShip '{__instance.type}', re-applying.");
                    __instance.holdPosition = true;
                }
            }
        }
    }

    /// <summary>
    /// Patches the getter for SavedVehicle.holdPosition to always return true when holdpos is enabled
    /// This ensures that even if something sets it to false, the game will always see it as true
    /// </summary>
    [HarmonyPatch]
    internal static class HoldPositionGetterMonitor_Vehicle
    {
        static bool Prepare()
        {
            var method = TargetMethod();
            if (method == null)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] HoldPositionGetterMonitor_Vehicle: Could not find SavedVehicle.holdPosition getter, skipping patch (this is normal)");
                return false;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionGetterMonitor_Vehicle: Found SavedVehicle.holdPosition getter {method.Name}, will apply patch");
            return true;
        }
        
        private static MethodBase TargetMethod()
        {
            try
            {
                var savedVehicleType = Type.GetType("NuclearOption.SavedMission.SavedVehicle, Assembly-CSharp")
                                    ?? AppDomain.CurrentDomain.GetAssemblies()
                                        .Select(a => a.GetType("NuclearOption.SavedMission.SavedVehicle"))
                                        .FirstOrDefault(t => t != null);
                
                if (savedVehicleType != null)
                {
                    var prop = savedVehicleType.GetProperty("holdPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanRead)
                    {
                        var getter = prop.GetGetMethod(true);
                        if (getter != null)
                        {
                            return getter;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionGetterMonitor_Vehicle: Error finding getter: {ex.Message}");
            }
            
            return null;
        }
        
        static void Postfix(SavedVehicle __instance, ref bool __result)
        {
            // If holdpos is enabled, always return true regardless of the actual value
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                if (!__result)
                {
                    Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionGetterMonitor_Vehicle: Forcing holdPosition getter to return true for {__instance.type}");
                }
                __result = true;
            }
        }
    }

    /// <summary>
    /// Patches the getter for SavedShip.holdPosition to always return true when holdpos is enabled
    /// </summary>
    [HarmonyPatch]
    internal static class HoldPositionGetterMonitor_Ship
    {
        static bool Prepare()
        {
            var method = TargetMethod();
            if (method == null)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] HoldPositionGetterMonitor_Ship: Could not find SavedShip.holdPosition getter, skipping patch (this is normal)");
                return false;
            }
            Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionGetterMonitor_Ship: Found SavedShip.holdPosition getter {method.Name}, will apply patch");
            return true;
        }
        
        private static MethodBase TargetMethod()
        {
            try
            {
                var savedShipType = Type.GetType("NuclearOption.SavedMission.SavedShip, Assembly-CSharp")
                                ?? AppDomain.CurrentDomain.GetAssemblies()
                                    .Select(a => a.GetType("NuclearOption.SavedMission.SavedShip"))
                                    .FirstOrDefault(t => t != null);
                
                if (savedShipType != null)
                {
                    var prop = savedShipType.GetProperty("holdPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanRead)
                    {
                        var getter = prop.GetGetMethod(true);
                        if (getter != null)
                        {
                            return getter;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionGetterMonitor_Ship: Error finding getter: {ex.Message}");
            }
            
            return null;
        }
        
        static void Postfix(SavedShip __instance, ref bool __result)
        {
            // If holdpos is enabled, always return true regardless of the actual value
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __instance != null)
            {
                if (!__result)
                {
                    Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionGetterMonitor_Ship: Forcing holdPosition getter to return true for {__instance.type}");
                }
                __result = true;
            }
        }
    }
}
