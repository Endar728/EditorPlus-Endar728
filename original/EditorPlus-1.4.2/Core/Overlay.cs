using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;
using UnityEngine;
using NuclearOption.MissionEditorScripts.Buttons;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
using TMPro;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Collections;

namespace EditorPlus
{
    public sealed partial class Plugin
    {
        AssetBundle _bundle;
        Stream _bundleStream;
        GameObject _overlayRoot;
        GraphView _view;
        RectTransform _leftPanelRT;
        Vector2 _leftPanelOriginalAnchored;
        bool _leftPanelOffsetApplied, _nameInputShrunk;
        const float LeftPanelShiftX = -530f;
        Button _overlayToggleButton, _gridToggleButton;
        Toggle _holdPosToggle, _terrainToggle;
        internal bool holdpos;
        public bool ignoreTerrain;

        private bool EnsureOverlayLoaded()
        {
            if (_overlayRoot) return true;

            if (_bundle == null && !TryLoadEmbeddedBundle())
            {
                Logger.LogError("No embedded asset bundle found in the assembly resources.");
                return false;
            }

            GameObject prefab = _bundle.LoadAsset<GameObject>("GraphOverlayPanel");
            if (!prefab) { Logger.LogError("Prefab 'GraphOverlayPanel' not found in bundle."); return false; }

            if (!TryFindHostCanvas(out Canvas hostCanvas))
            {
                Logger.LogError("Could not find a suitable Canvas under 'SceneEssentials/Canvas'.");
                return false;
            }
            if (!FindObjectOfType<GroupFollowers>())
            {
                GameObject go = new("GroupGroup");
                DontDestroyOnLoad(go);
                go.AddComponent<GroupFollowers>();
                go.AddComponent<BoxSelection>();
            }

            _overlayRoot = Instantiate(prefab, hostCanvas.transform, false);
            _overlayRoot.name = "MissionGraph_OverlayPanel";
            RectTransform rt = _overlayRoot.transform as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlayRoot.transform.SetAsFirstSibling();

            _view = _overlayRoot.GetComponent<GraphView>();
            if (!_view)
            {
                base.Logger.LogError("GraphView not found on overlay panel prefab.");
                return false;
            }

            _view.OnLink = (fromId, fromIsObj, toId, toIsObj) =>
            {
                try
                {
                    Logger.LogInfo($"[MissionGraph] LINK request: {fromId}({(fromIsObj ? "OBJ" : "OUT")}) > {toId}({(toIsObj ? "OBJ" : "OUT")})");

                    MissionObjectives mo = MissionManager.Objectives;
                    Objective o = mo.AllObjectives.FirstOrDefault(x => x.SavedObjective.UniqueName == (fromIsObj ? fromId : toId));
                    Outcome oc = mo.AllOutcomes.FirstOrDefault(x => x.SavedOutcome.UniqueName == (fromIsObj ? toId : fromId));

                    if (o == null || oc == null)
                    {
                        Logger.LogError($"[MissionGraph] LINK resolve failed. UI/model out of sync. fromId={fromId} toId={toId} fromIsObj={fromIsObj} toIsObj={toIsObj}");
                        return;
                    }

                    if (fromIsObj && !toIsObj)
                    {
                        int before = o.Outcomes.Count;
                        if (o.Outcomes.Contains(oc)) return;
                        o.Outcomes.Add(oc);
                        int after = o.Outcomes.Count;
                        Logger.LogInfo($"Linked OUT '{oc.SavedOutcome.UniqueName}' to OBJ '{o.SavedObjective.UniqueName}' (count {before}→{after}).");
                        SceneSingleton<MissionEditor>.i.CheckAutoSave();
                    }
                    else if (!fromIsObj && toIsObj)
                    {
                        bool added = TryAddObjectiveReferenceToOutcome(oc, o);
                        if (added)
                        {
                            Logger.LogInfo($"[MissionGraph] Outcome '{oc.SavedOutcome.UniqueName}' now references OBJ '{o.SavedObjective.UniqueName}'.");
                            SceneSingleton<MissionEditor>.i.CheckAutoSave();
                            Logger.LogInfo("[MissionGraph] AutoSave requested (link).");
                        }
                        else
                        {
                            Logger.LogWarning($"[MissionGraph] Link no-op: outcome type unsupported or reference already present. out='{oc.SavedOutcome.UniqueName}' > obj='{o.SavedObjective.UniqueName}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MissionGraph] LINK apply crashed: {ex}");
                }
            };

            _view.OnUnlink = (fromId, fromIsObj, toId, toIsObj) =>
            {
                try
                {
                    Logger.LogInfo($"[MissionGraph] UNLINK request: {fromId}({(fromIsObj ? "OBJ" : "OUT")}) > {toId}({(toIsObj ? "OBJ" : "OUT")})");

                    MissionObjectives mo = MissionManager.Objectives;
                    Objective o = mo.AllObjectives.FirstOrDefault(x => x.SavedObjective.UniqueName == (fromIsObj ? fromId : toId));
                    Outcome oc = mo.AllOutcomes.FirstOrDefault(x => x.SavedOutcome.UniqueName == (fromIsObj ? toId : fromId));
                    if (o == null || oc == null)
                    {
                        Logger.LogError($"[MissionGraph] UNLINK resolve failed. UI/model out of sync. fromId={fromId} toId={toId} fromIsObj={fromIsObj} toIsObj={toIsObj}");
                        return;
                    }

                    bool changed = false;
                    if (fromIsObj && !toIsObj)
                    {
                        if (o.Outcomes.Contains(oc)) { o.Outcomes.Remove(oc); changed = true; }
                    }
                    else if (!fromIsObj && toIsObj)
                    {
                        changed = RemoveObjectiveReferenceFromOutcome(oc, o);
                    }

                    if (changed)
                    {
                        Logger.LogInfo($"[MissionGraph] Unlinked '{fromId}' × '{toId}'.");
                        SceneSingleton<MissionEditor>.i.CheckAutoSave();
                        Logger.LogInfo("[MissionGraph] AutoSave requested (unlink).");
                    }
                    else
                    {
                        Logger.LogWarning($"[MissionGraph] Unlink no-op: relationship not found. fromId={fromId} toId={toId}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MissionGraph] UNLINK apply crashed: {ex}");
                }
            };
            _view.SetWorldCamera(Camera.main);
            _view.OnEditObjective = id => StartCoroutine(OpenAndEditObjective(id));
            _view.OnEditOutcome = id => StartCoroutine(OpenAndEditOutcome(id));
            _view.QueryUnitWorldPositions = (id, isObj) => EnumerateUnitWorldGetters(id, isObj);
            _view.OnRequestAddSelectionToNode = (id, isObj) => TryAddCurrentSelectionToNode(id, isObj);
            _overlayRoot.SetActive(false);
            return true;
        }
        private void TryAddCurrentSelectionToNode(string id, bool isObjective)
        {
            var gf = FindObjectOfType<GroupFollowers>();
            if (!gf) { Logger.LogWarning("[Graph] No GroupFollowers found."); return; }

            var units = gf.CurrentUnits;
            if (units == null || units.Count == 0) { Logger.LogInfo("[Graph] No units currently selected."); return; }

            var mo = MissionManager.Objectives;
            if (mo == null) return;

            // Collect SavedUnit objects, unique by reference
            var sel = new List<NuclearOption.SavedMission.SavedUnit>();
            foreach (var u in units)
            {
                if (u != null) // for UnityEngine.Object, keeps destroyed-object semantics
                {
                    var su = u.SavedUnit;
                    if (su != null && !sel.Contains(su))
                        sel.Add(su);
                }
            }

            if (sel.Count == 0) { Logger.LogInfo("[Graph] Selected units had no SavedUnit."); return; }

            if (isObjective)
            {
                var obj = mo.AllObjectives.FirstOrDefault(o => o.SavedObjective.UniqueName == id);
                if (obj == null) { Logger.LogWarning($"[Graph] Objective '{id}' not found."); return; }

                var fi = ReflectionUtils.FindFieldRecursive(obj.GetType(), "allItems");
                if (fi == null) { Logger.LogWarning($"[Graph] Objective '{id}' has no 'allItems' field."); return; }

                if (fi.GetValue(obj) is System.Collections.IList list)
                {
                    int added = 0;
                    foreach (var su in sel)
                    {
                        if (!ContainsSavedUnit(list, su)) { list.Add(su); added++; }
                    }

                    if (added > 0)
                    {
                        Logger.LogInfo($"[Graph] Added {added} unit(s) to objective '{id}'.");
                        SceneSingleton<MissionEditor>.i?.CheckAutoSave();
                    }
                    else Logger.LogInfo($"[Graph] No new units to add to objective '{id}'.");
                }
                return;
            }
            else
            {
                var oc = mo.AllOutcomes.FirstOrDefault(x => x.SavedOutcome.UniqueName == id);
                if (oc == null) { Logger.LogWarning($"[Graph] Outcome '{id}' not found."); return; }

                // Try find a List<SavedUnit> either on the runtime outcome or its SavedOutcome
                var list = FindSavedUnitList(oc) ?? FindSavedUnitList(ReflectionUtils.GetPropOrFieldValue(oc, "SavedOutcome"));
                if (list == null) { Logger.LogWarning($"[Graph] No editable SavedUnit list found on outcome '{id}'."); return; }

                int added = 0;
                foreach (var su in sel)
                    if (!ContainsSavedUnit(list, su)) { list.Add(su); added++; }

                if (added > 0)
                {
                    Logger.LogInfo($"[Graph] Added {added} unit(s) to outcome '{id}'.");
                    SceneSingleton<MissionEditor>.i?.CheckAutoSave();
                }
                else Logger.LogInfo($"[Graph] No new units to add to outcome '{id}'.");
            }
        }

        private static bool ContainsSavedUnit(IList list, NuclearOption.SavedMission.SavedUnit su)
        {
            foreach (var it in list)
                if (ReferenceEquals(it, su)) return true;
            return false;
        }

        private static IList FindSavedUnitList(object host)
        {
            if (host == null) return null;
            foreach (var f in host.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(IList).IsAssignableFrom(f.FieldType)) continue;
                var elem = f.FieldType.IsGenericType ? f.FieldType.GetGenericArguments()[0] : null;
                if (elem != typeof(NuclearOption.SavedMission.SavedUnit)) continue;

                var list = (IList)f.GetValue(host);
                if (list == null && f.FieldType.IsGenericType)
                {
                    var listType = typeof(List<>).MakeGenericType(elem);
                    list = (IList)Activator.CreateInstance(listType);
                    f.SetValue(host, list);
                }
                return list;
            }
            return null;
        }

        private bool TryLoadEmbeddedBundle()
        {
            string resName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".noep", StringComparison.OrdinalIgnoreCase));
            if (resName == null) return false;

            _bundleStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
            if (_bundleStream == null || !_bundleStream.CanRead || !_bundleStream.CanSeek) return false;

            _bundle = AssetBundle.LoadFromStream(_bundleStream);
            return _bundle != null;
        }
        private static bool TryFindHostCanvas(out Canvas host)
        {
            host = null;
            GameObject container = GameObject.Find("SceneEssentials/Canvas");
            if (container)
            {
                Canvas[] canvases = container.GetComponentsInChildren<Canvas>(true);
                Canvas pick = null;
                foreach (Canvas c in canvases)
                {
                    if (!c.isActiveAndEnabled) continue;
                    if (c.name.Contains("Menu", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pick == null || c.sortingOrder >= pick.sortingOrder) pick = c;
                    }
                }
                if (pick == null)
                {
                    foreach (Canvas c in canvases)
                        if (pick == null || c.sortingOrder >= pick.sortingOrder) pick = c;
                }

                if (pick != null) { host = pick; return true; }
            }
            host = FindObjectOfType<Canvas>();
            return host != null;
        }
        private void ApplyLeftPanelOffset(bool on)
        {
            if (!_leftPanelRT)
            {
                _leftPanelRT = GameObject.Find("LeftPanel")?.GetComponent<RectTransform>();

                if (!_leftPanelRT)
                {
                    ObjectiveEditorV2 editor = editormenu ? editormenu : FindObjectOfType<ObjectiveEditorV2>(true);
                    if (editor)
                        _leftPanelRT = editor.GetComponentsInParent<RectTransform>(true)
                            .FirstOrDefault(rt => string.Equals(rt.name, "LeftPanel", StringComparison.OrdinalIgnoreCase));
                }
                if (_leftPanelRT && !_leftPanelOffsetApplied)
                    _leftPanelOriginalAnchored = _leftPanelRT.anchoredPosition;
            }

            if (!_leftPanelRT) return;

            if (on)
            {
                if (_leftPanelOffsetApplied) return;
                _leftPanelRT.anchoredPosition = _leftPanelOriginalAnchored + new Vector2(LeftPanelShiftX, 0f);
                _leftPanelOffsetApplied = true;
            }
            else
            {
                if (!_leftPanelOffsetApplied) return;
                _leftPanelRT.anchoredPosition = _leftPanelOriginalAnchored;
                _leftPanelOffsetApplied = false;
            }
        }
        private void ShrinkTopbarFirstSibling(Transform parent, float factor)
        {
            if (_nameInputShrunk) return;
            if (parent?.Find("MissionNameInput") is not RectTransform rt) return;

            rt.offsetMax = new(rt.offsetMax.x * factor, rt.offsetMax.y);
            _nameInputShrunk = true;
        }
        private static Button EnsureToolbarButton(Transform parent, Button template, string goName, string hoverText, Action onClick)
        {
            Transform existingTf = parent.Find(goName);
            GameObject go = existingTf ? existingTf.gameObject : Instantiate(template.gameObject, parent);
            go.name = goName;

            Button btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke());

            ShowHoverText label = go.GetComponentInChildren<ShowHoverText>(true);
            if (label) label.SetText(hoverText);

            go.transform.SetAsLastSibling();
            return btn;
        }
        private static Toggle EnsureToolbarToggle(Transform parent, Toggle template, string goName, string hoverText, bool initialValue, Action<bool> onValueChanged)
        {
            if (!template) return null;

            Transform existingTf = parent.Find(goName);
            GameObject go = existingTf ? existingTf.gameObject : Instantiate(template.gameObject, parent);
            go.name = goName;
            go.SetActive(true);

            Toggle t = go.GetComponent<Toggle>();
            if (!t) t = go.AddComponent<Toggle>();

            t.onValueChanged.RemoveAllListeners();
            t.SetIsOnWithoutNotify(initialValue);
            t.onValueChanged.AddListener(v => onValueChanged?.Invoke(v));
            t.interactable = true;

            ShowHoverText hover = go.GetComponentInChildren<ShowHoverText>(true);
            if (hover) hover.SetText(hoverText);
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = hoverText;

            go.transform.SetAsLastSibling();
            return t;
        }
        private bool TryEnsureTopbarToggleButton()
        {
            if (!IsInMissionEditor()) return false;

            if (_overlayToggleButton && _gridToggleButton && _holdPosToggle) return true;

            objectivesBtn = FindObjectsOfType<ChangeTabButton>(true)
                .FirstOrDefault(b => b && string.Equals(b.name, "ObjectivesButton", StringComparison.OrdinalIgnoreCase));
            if (!objectivesBtn) return false;

            Button template = objectivesBtn.GetComponent<Button>();
            if (!template) return false;

            Transform parent = template.transform?.parent;
            if (!parent) return false;
            ShrinkTopbarFirstSibling(parent, 0.5f); //shrink name box for more buttons
            _overlayToggleButton ??= EnsureToolbarButton(
                parent, template, "EditorPlusToggleButton", "Graph",
                () =>
                {
                    if (!IsInMissionEditor() || !EnsureOverlayLoaded()) return;
                    bool show = !_overlayRoot.activeSelf;
                    if (!show && _view) _view.ClearUnitGhosts();
                    _overlayRoot.SetActive(show);
                    ApplyLeftPanelOffset(show);
                    if (show) RebuildGraph();
                });

            _gridToggleButton ??= EnsureToolbarButton(
                parent, template, "EditorPlusGridButton", "Grid",
                () =>
                {
                    if (!IsInMissionEditor() || !EnsureOverlayLoaded()) return;
                    _view?.ToggleBackgroundAndGrid();
                });

            Toggle autoSaveTemplate = parent.GetComponentsInChildren<Toggle>(true)
                .FirstOrDefault(t => t && string.Equals(t.name, "AutoSaveToggle", StringComparison.OrdinalIgnoreCase));
            _holdPosToggle ??= EnsureToolbarToggle(
                parent,
                autoSaveTemplate,
                "HoldPosToggle",
                "Hold Pos",
                Instance.holdpos,
                v => { Instance.holdpos = v; }
            );
            _terrainToggle ??= EnsureToolbarToggle(
                parent,
                autoSaveTemplate,
                "IgnoreTerrainToggle",
                "noclip",
                Instance.ignoreTerrain,
                v => { Instance.ignoreTerrain = v; }
            );
            return _overlayToggleButton && _gridToggleButton && _holdPosToggle;
        }

    }
}
