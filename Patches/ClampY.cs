using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

namespace EditorPlus.Patches
{
    [HarmonyPatch]
    internal static class EditorHandle_ClampY_Patch
    {
        static MethodBase TargetMethod()
        {
            // This patch is optional - if ClampY method doesn't exist, skip the patch
            try
            {
                var type = Type.GetType("NuclearOption.MissionEditorScripts.EditorHandle, Assembly-CSharp")
                            ?? AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType("NuclearOption.MissionEditorScripts.EditorHandle"))
                                .FirstOrDefault(t => t != null);
                
                if (type == null) return null;
                
                // Try to find ClampY method with different signatures
                var method = AccessTools.Method(type, "ClampY", new[] { typeof(Unit), typeof(GlobalPosition) });
                if (method == null)
                {
                    method = AccessTools.Method(type, "ClampY");
                }
                
                if (method == null)
                {
                    Plugin.Logger?.LogInfo("[EditorPlus] ClampY method not found, skipping patch (this is normal)");
                }
                
                return method;
            }
            catch
            {
                return null;
            }
        }
        
        static void Prefix(GlobalPosition position, out float __state) => __state = position.y;
        static void Postfix(ref GlobalPosition __result, float __state)
        {
            if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
            {
                __result.y = __state;
                return;
            }
            __result.y = Mathf.Max(__state, __result.y);
        }
    }
}