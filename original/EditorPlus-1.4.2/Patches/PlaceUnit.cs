using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using static EditorPlus.Plugin;
using System.Reflection;
using UnityEngine;
using System.Collections;
using System;

namespace EditorPlus.Patches
{
    [HarmonyPatch(typeof(UnitMenu), "PlaceUnit", [])]
    static class UnitMenu_PlaceUnit_Patch
    {
        static readonly MethodInfo mi = AccessTools.Method(typeof(UnitMenu), "StartPlaceUnit", Type.EmptyTypes);

        static void Postfix(UnitMenu __instance)
        {
            if (Input.GetKey(KeyCode.LeftControl))
                HarmonyCoroutineRunner.Instance.StartCoroutine(CallAtEndOfFrame(__instance));
        }
        public class HarmonyCoroutineRunner : MonoBehaviour
        {
            static HarmonyCoroutineRunner _instance;
            public static HarmonyCoroutineRunner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        GameObject go = new("HarmonyCoroutineRunner");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<HarmonyCoroutineRunner>();
                    }
                    return _instance;
                }
            }
        }
        static IEnumerator CallAtEndOfFrame(UnitMenu instance)
        {
            yield return new WaitForEndOfFrame();
            mi.Invoke(instance, null);
        }
    }
}