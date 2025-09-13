using HarmonyLib;
using System.Reflection;
using UnityEngine.UI;
using UnityEngine;

namespace EditorPlus.Patches
{
    [HarmonyPatch(typeof(Dropdown), "Show")]
    static class Dropdown_Patch
    {
        static readonly FieldInfo F_List = AccessTools.Field(typeof(Dropdown), "m_Dropdown");

        static void Postfix(Dropdown __instance)
        {
            if (F_List?.GetValue(__instance) is not GameObject listGo) return;
            if (!listGo.TryGetComponent(out RectTransform listRt)) return;

            int count = Mathf.Max(1, __instance.options?.Count ?? 0);
            float desired = count * 20f + 8f;

            Canvas canvas = __instance.GetComponentInParent<Canvas>()?.rootCanvas;
            float scale = canvas ? canvas.scaleFactor : 1f;
            float maxHeight = (Screen.safeArea.height * 0.5f) / scale;
            listRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(desired, maxHeight));
        }
    }
}