using HarmonyLib;
using NuclearOption.MissionEditorScripts;
#if !NO_RUNTIME_TRANSFORM_HANDLE
using RuntimeHandle;
#endif
using System;
using System.Reflection;
using UnityEngine;

namespace EditorPlus.Patches
{
#if !NO_RUNTIME_TRANSFORM_HANDLE
    [HarmonyPatch(typeof(EditorHandle), "Update")]
    static class EditorHandle_Fast_Rotate
    {
        private static Action<EditorHandle, HandleType, bool> _setMode;
        private static FieldInfo _fiHandleType;
        private static readonly KeyCode FAST_ROTATE_KEY = KeyCode.LeftControl;

        static void Postfix(EditorHandle __instance)
        {
            if (__instance == null || !__instance.isActiveAndEnabled) return;

            if (_fiHandleType == null)
            {
                _fiHandleType = AccessTools.Field(typeof(EditorHandle), "type");
                if (_fiHandleType == null) return;
            }

            if (_setMode == null)
            {
                MethodInfo mi = AccessTools.Method(typeof(EditorHandle), "SetMode", [typeof(HandleType), typeof(bool)]);
                if (mi == null) return;
                _setMode = AccessTools.MethodDelegate<Action<EditorHandle, HandleType, bool>>(mi);
            }

            HandleType _handleType = (HandleType)_fiHandleType.GetValue(__instance);
            if (_handleType == HandleType.NONE) return;

            bool _keyHeld = Input.GetKey(FAST_ROTATE_KEY);
            HandleType _overrideType = _keyHeld ? HandleType.ROTATION : HandleType.POSITION;

            if (_handleType != _overrideType)
                _setMode(__instance, _overrideType, false);
        }
    }
#endif
}