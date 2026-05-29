using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;

namespace EditorPlus.Patches
{
    [HarmonyPatch(typeof(MissionEditor), nameof(MissionEditor.RegisterNewUnit), [typeof(Unit), typeof(string)])]
    internal static class MissionEditor_RegisterNewUnit_Patch
    {
        static void Postfix(ref SavedUnit __result)
        {
            if (__result is SavedVehicle v)
            {
                v.holdPosition = Plugin.Instance.holdpos;
            }
            if (__result is SavedShip s)
            {
                s.holdPosition = Plugin.Instance.holdpos;
            }
        }
    }
}