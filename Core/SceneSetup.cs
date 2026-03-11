using System.Collections;
using NuclearOption.MissionEditorScripts;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using NuclearOption.SavedMission.ObjectiveV2;
using System.Reflection;
using System.Collections.Generic;
using EditorPlus.Patches;

namespace EditorPlus
{
    public sealed partial class Plugin
    {
        Coroutine _sceneSetupCo;
        ObjectiveEditorV2 editormenu;
        object objectivesBtn;
        private static readonly Dictionary<Type, FieldInfo> _completeObjListField = new();
        private static MethodInfo _miShowEditOutcome;
        private static bool IsInMissionEditor() => SceneSingleton<MissionEditor>.i != null && ReflectionUtils.GetMissionObjectives() != null;

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (s.name != "GameWorld") return;

            // Ensure copy-paste input handler exists immediately
            // This ensures it's created before other mods' handlers
            CopyPasteInputHandler.EnsureExists();
            
            // Re-initialize free camera monitor when GameWorld scene loads
            // This ensures it can find the camera which is created during scene load
            try
            {
                Patches.FreeCameraCollisionPatch.Initialize();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"[EditorPlus] Failed to initialize free camera monitor on scene load: {ex.Message}");
            }
            
            // Try to apply PlaceUnit patch if it wasn't applied during Awake
            // This is needed because UnitMenu type might not be loaded at startup
            try
            {
                Patches.UnitMenu_PlaceUnit_Patch.TryApplyPatchDelayed();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"[EditorPlus] Failed to apply PlaceUnit patch on scene load: {ex.Message}");
            }

            if (_sceneSetupCo != null) StopCoroutine(_sceneSetupCo);
            _sceneSetupCo = StartCoroutine(SceneSetupWhenReady(s));
        }

        private IEnumerator SceneSetupWhenReady(Scene s)
        {
            while (s.isLoaded && s.name == "GameWorld" && !IsInMissionEditor())
                yield return null;

            if (!s.isLoaded || s.name != "GameWorld") yield break;

            // Try to apply PlaceUnit patch again now that mission editor is ready
            // This gives us another chance if it wasn't applied on scene load
            try
            {
                Patches.UnitMenu_PlaceUnit_Patch.TryApplyPatchDelayed();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"[EditorPlus] Failed to apply PlaceUnit patch when mission editor ready: {ex.Message}");
            }

            EnsureOverlayLoaded();

            while (s.isLoaded && s.name == "GameWorld" && !TryEnsureTopbarToggleButton())
                yield return null;

            if (_overlayRoot && _overlayRoot.activeSelf)
                RebuildGraph();

            _sceneSetupCo = null;
        }

        private void OnSceneUnloaded(Scene s)
        {
            if (_sceneSetupCo != null) { StopCoroutine(_sceneSetupCo); _sceneSetupCo = null; }

            _overlayToggleButton = null;
            _gridToggleButton = null;
            _holdPosToggle = null;
            _terrainToggle = null;
            objectivesBtn = null;
            editormenu = null;
            _view = null;
            _nameInputShrunk = false;
            if (_overlayRoot)
            {
                Destroy(_overlayRoot);
                _overlayRoot = null;
            }
            ReflectionUtils.ClearMissionObjectivesCache();
        }
        private IEnumerator EnsureEditorMenu()
        {
            if (editormenu && editormenu.gameObject.activeInHierarchy) yield break;

            editormenu = null;
            editormenu = FindObjectOfType<ObjectiveEditorV2>();
            if (editormenu) yield break;

            if (objectivesBtn == null)
            {
                var allButtons = FindObjectsOfType<MonoBehaviour>(true);
                objectivesBtn = allButtons.FirstOrDefault(b => b && string.Equals(b.name, "ObjectivesButton", StringComparison.OrdinalIgnoreCase));
            }
            if (objectivesBtn == null) yield break;

            var toggleMethod = objectivesBtn.GetType().GetMethod("ToggleTab", new[] { typeof(bool) });
            toggleMethod?.Invoke(objectivesBtn, new object[] { true });

            const float timeout = 2f;
            float start = Time.realtimeSinceStartup;
            while (!(editormenu = FindObjectOfType<ObjectiveEditorV2>()) &&
                   Time.realtimeSinceStartup - start < timeout)
            {
                yield return null;
            }
        }
        private IEnumerator OpenAndEditObjective(string uniqueName)
        {
            yield return EnsureEditorMenu();
            if (!editormenu) yield break;

            editormenu.ShowObjectiveList();
            yield return null;
            MissionObjectives mo = ReflectionUtils.GetMissionObjectives();
            if (mo == null) yield break;
            Objective obj = mo.AllObjectives.FirstOrDefault(o => o.SavedObjective.UniqueName == uniqueName);
            if (obj != null)
                editormenu.ShowEditObjective(obj);
        }

        private IEnumerator OpenAndEditOutcome(string uniqueName)
        {
            yield return EnsureEditorMenu();
            if (!editormenu) yield break;

            editormenu.ShowOutcomeList();
            yield return null;
            MissionObjectives mo = ReflectionUtils.GetMissionObjectives();
            if (mo == null) yield break;
            int idx = mo.AllOutcomes.FindIndex(oc => oc.SavedOutcome.UniqueName == uniqueName);
            if (idx < 0) yield break;

            _miShowEditOutcome ??= typeof(ObjectiveEditorV2).GetMethod("ShowEditOutcome", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_miShowEditOutcome == null) yield break;
            _miShowEditOutcome?.Invoke(editormenu, [idx]);
        }
    }
}
