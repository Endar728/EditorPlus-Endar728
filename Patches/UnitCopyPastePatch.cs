using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using System;
using System.Reflection;
using System.Linq;

namespace EditorPlus.Patches
{
    /// <summary>
    /// Patches UnitCopyPaste.CopyPaste to preserve hold position if it was set
    /// </summary>
    [HarmonyPatch]
    internal static class UnitCopyPaste_CopyPaste_Patch
    {
        static MethodBase TargetMethod()
        {
            try
            {
                var type = Type.GetType("NuclearOption.MissionEditorScripts.UnitCopyPaste, Assembly-CSharp")
                            ?? AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType("NuclearOption.MissionEditorScripts.UnitCopyPaste"))
                                .FirstOrDefault(t => t != null);
                
                if (type == null) return null;
                
                // Find CopyPaste method - it likely has parameters for mission, source SavedUnit, target Unit, and target SavedUnit
                var method = AccessTools.Method(type, "CopyPaste");
                if (method == null)
                {
                    Plugin.Logger?.LogInfo("[EditorPlus] UnitCopyPaste.CopyPaste method not found, skipping patch (this is normal)");
                }
                
                return method;
            }
            catch
            {
                return null;
            }
        }
        
        static void Prefix(object __0, SavedUnit __1, Unit __2, SavedUnit __3, out bool __state)
        {
            // Store the hold position value before CopyPaste runs
            __state = false;
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __3 != null)
            {
                __state = HoldPositionHelper.GetHoldPosition(__3);
                Plugin.Logger?.LogInfo($"[EditorPlus] UnitCopyPaste.CopyPaste: Stored hold position = {__state} for {__3.type}");
            }
        }
        
        static void Postfix(object __0, SavedUnit __1, Unit __2, SavedUnit __3, bool __state)
        {
            // Restore hold position if it was set before CopyPaste
            if (Plugin.Instance != null && Plugin.Instance.holdpos && __3 != null)
            {
                bool currentValue = HoldPositionHelper.GetHoldPosition(__3);
                if (__state || currentValue != true)
                {
                    HoldPositionHelper.ApplyToSavedUnit(__3, true);
                    bool afterValue = HoldPositionHelper.GetHoldPosition(__3);
                    Plugin.Logger?.LogInfo($"[EditorPlus] UnitCopyPaste.CopyPaste: Restored hold position for {__3.type}, before={currentValue}, after={afterValue}");
                }
            }
        }
    }
}
