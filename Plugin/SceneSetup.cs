
using System.Collections;
using NuclearOption.MissionEditorScripts.Buttons;
using NuclearOption.MissionEditorScripts;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using NuclearOption.SavedMission.ObjectiveV2;
using System.Reflection;
using System.Collections.Generic;

namespace EditorPlus
{
    public sealed partial class Plugin
    {
        Coroutine _sceneSetupCo;
        ObjectiveEditorV2 editormenu;
        ChangeTabButton objectivesBtn;
        private static readonly Dictionary<Type, FieldInfo> _completeObjListField = new();
        private static MethodInfo _miShowEditOutcome;
        private static bool IsInMissionEditor() => SceneSingleton<MissionEditor>.i != null && MissionManager.Objectives != null;

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (s.name != "GameWorld") return;

            if (_sceneSetupCo != null) StopCoroutine(_sceneSetupCo);
            _sceneSetupCo = StartCoroutine(SceneSetupWhenReady(s));
        }

        private IEnumerator SceneSetupWhenReady(Scene s)
        {
            while (s.isLoaded && s.name == "GameWorld" && !IsInMissionEditor())
                yield return null;

            if (!s.isLoaded || s.name != "GameWorld") yield break;

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
        }
        private IEnumerator EnsureEditorMenu()
        {
            if (editormenu && editormenu.gameObject.activeInHierarchy) yield break;

            editormenu = null;
            editormenu = FindObjectOfType<ObjectiveEditorV2>();
            if (editormenu) yield break;

            objectivesBtn ??= FindObjectsOfType<ChangeTabButton>(true).FirstOrDefault(b => b && string.Equals(b.name, "ObjectivesButton", StringComparison.OrdinalIgnoreCase));
            if (!objectivesBtn) yield break;

            objectivesBtn.ToggleTab(true);

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
            MissionObjectives mo = MissionManager.Objectives;
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
            MissionObjectives mo = MissionManager.Objectives;
            int idx = mo.AllOutcomes.FindIndex(oc => oc.SavedOutcome.UniqueName == uniqueName);
            if (idx < 0) yield break;

            _miShowEditOutcome ??= typeof(ObjectiveEditorV2).GetMethod("ShowEditOutcome", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_miShowEditOutcome == null) yield break;
            _miShowEditOutcome?.Invoke(editormenu, [idx]);
        }
    }
}

