/*
TODO
this is jank, fix it.
add snapping to cursor ghost
make snapping snap to global positions
*/

using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
#if !NO_RUNTIME_TRANSFORM_HANDLE
using RuntimeHandle;
#endif
using System.Globalization;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace EditorPlus.Patches
{
#if !NO_RUNTIME_TRANSFORM_HANDLE
    [HarmonyPatch(typeof(Vector3DataField), "Setup", [typeof(string), typeof(IValueWrapper<Vector3>)])]
    static class Snap_Patch
    {
        static void Postfix(Vector3DataField __instance, string label)
        {
            bool isPos = string.Equals(label, "Position", StringComparison.OrdinalIgnoreCase);
            if (!(isPos || string.Equals(label, "Rotation", StringComparison.OrdinalIgnoreCase))) return;


            TMP_InputField anyInput = __instance.GetComponentInChildren<TMP_InputField>();
            if (!anyInput) return;

            RectTransform row = anyInput.transform.parent as RectTransform;
            if (!row) return;

            string name = isPos ? "SnapInput_Pos" : "SnapInput_Rot";
            _ = row.Find(name)?.GetComponent<TMP_InputField>() ?? CloneSnap(anyInput, row, name, isPos);

            HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg)
            {
                hlg.childControlWidth = true;
                hlg.childForceExpandWidth = true;
            }
        }

#if !NO_RUNTIME_TRANSFORM_HANDLE
        static RuntimeTransformHandle _cachedHandle;
        static RuntimeTransformHandle GetHandle() =>
            _cachedHandle ? _cachedHandle : (_cachedHandle = UnityEngine.Object.FindObjectOfType<RuntimeTransformHandle>());
#else
        static object _cachedHandle;
        static object GetHandle() => null;
#endif


        static TMP_InputField CloneSnap(TMP_InputField template, Transform parent, string name, bool isPos)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = name;

            TMP_InputField snap = go.GetComponent<TMP_InputField>();
            if (!snap) return null;

            snap.onEndEdit.RemoveAllListeners();
            snap.contentType = TMP_InputField.ContentType.DecimalNumber;

            LayoutElement le = snap.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = 55f;

#if !NO_RUNTIME_TRANSFORM_HANDLE
            RuntimeTransformHandle h = GetHandle() as RuntimeTransformHandle;

            float value = h ? isPos ? h.positionSnap.x : h.rotationSnap : 0f;
#else
            float value = 0f;
#endif
            snap.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));

            snap.onEndEdit.AddListener(v =>
            {
                if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float val)) return;
                val = Mathf.Max(0f, val);

#if !NO_RUNTIME_TRANSFORM_HANDLE
                RuntimeTransformHandle handle = GetHandle() as RuntimeTransformHandle;
                if (!handle) return;

                if (isPos) handle.positionSnap = new Vector3(val, val, val);
                else handle.rotationSnap = val;
#endif
            });

            return snap;
        }
    }
#endif
}