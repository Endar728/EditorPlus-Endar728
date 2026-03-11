using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

namespace EditorPlus.Patches
{
    /// <summary>
    /// Patches UnitSelectionDetails.ClampPosition to respect no clip toggle
    /// </summary>
    [HarmonyPatch]
    internal static class UnitSelectionDetails_ClampPosition_Patch
    {
        static MethodBase TargetMethod()
        {
            try
            {
                var type = Type.GetType("NuclearOption.MissionEditorScripts.UnitSelectionDetails, Assembly-CSharp")
                            ?? AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType("NuclearOption.MissionEditorScripts.UnitSelectionDetails"))
                                .FirstOrDefault(t => t != null);
                
                if (type == null) return null;
                
                var method = AccessTools.Method(type, "ClampPosition");
                if (method == null)
                {
                    Plugin.Logger?.LogInfo("[EditorPlus] ClampPosition method not found, skipping patch (this is normal)");
                }
                
                return method;
            }
            catch
            {
                return null;
            }
        }
        
        static void Prefix(out GlobalPosition __state, GlobalPosition __0)
        {
            // Store the original position before clamping
            __state = __0;
        }
        
        static void Postfix(ref GlobalPosition __result, GlobalPosition __state)
        {
            // If no clip is enabled, restore the original position
            if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
            {
                __result = __state; // Restore original position
                Plugin.Logger?.LogInfo($"[EditorPlus] No clip: ClampPosition blocked, restored original: {__state.AsVector3()}");
            }
        }
    }
}
