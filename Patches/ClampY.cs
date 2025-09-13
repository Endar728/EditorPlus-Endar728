using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace EditorPlus.Patches
{

    [HarmonyPatch(typeof(EditorHandle), "ClampY", [typeof(Unit), typeof(GlobalPosition)])]
    internal static class EditorHandle_ClampY_Patch
    {
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