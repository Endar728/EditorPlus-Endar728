using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EditorPlus;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
using RuntimeHandle;
using UnityEngine;
public sealed class GroupFollowers : MonoBehaviour
{
    readonly List<Unit> _group = [];
    Unit _primary;
    EditorHandle _editorHandle;
    RuntimeTransformHandle _rth;
    UnitSelection _unitSelection;
    GlobalPosition _primStartPos;
    Quaternion _primStartRot;
    static readonly RaycastHit[] _rayHits = new RaycastHit[64];
    const int TERRAIN_MASK = 64;
    struct Follower
    {
        public Unit unit;
        public IValueWrapper<GlobalPosition> posW;
        public IValueWrapper<Quaternion> rotW;
        public GlobalPosition startPos;
        public Quaternion startRot;
    }
    readonly List<Follower> _followers = [];
    Transform _vanillaMarker;
    Transform _markerParent;
    readonly Dictionary<Unit, GameObject> _markerByUnit = [];
    readonly Dictionary<Unit, Faction> _factionByUnit = [];
    void OnEnable() { TryBindVanilla(); }
    void OnDisable() { UnbindVanilla(); }
    void TryBindVanilla()
    {
        _editorHandle = FindObjectOfType<EditorHandle>();
        if (!_editorHandle) return;
        Type t = typeof(EditorHandle);
        _rth = GetPrivate<RuntimeTransformHandle>(_editorHandle, t, "handle");
        if (_rth != null)
        {
            _rth.startedDraggingHandle.AddListener(OnDragBegin);
            _rth.isDraggingHandle.AddListener(OnDragTick);
            _rth.endedDraggingHandle.AddListener(OnDragEnd);
        }
        _unitSelection = FindObjectOfType<UnitSelection>();
        if (_unitSelection != null)
        {
            _unitSelection.OnSelect += OnVanillaSelect;
            _vanillaMarker = GetPrivate<Transform>(_unitSelection, typeof(UnitSelection), "unitSelectionMarker");
            _markerParent = _vanillaMarker ? _vanillaMarker.parent : null;
        }
    }
    void UnbindVanilla()
    {
        if (_rth != null)
        {
            _rth.startedDraggingHandle.RemoveListener(OnDragBegin);
            _rth.isDraggingHandle.RemoveListener(OnDragTick);
            _rth.endedDraggingHandle.RemoveListener(OnDragEnd);
        }
        if (_unitSelection != null)
            _unitSelection.OnSelect -= OnVanillaSelect;
        foreach (KeyValuePair<Unit, GameObject> kv in _markerByUnit) if (kv.Value) Destroy(kv.Value);
        _markerByUnit.Clear();
        _factionByUnit.Clear();
        _rth = null;
        _editorHandle = null;
        _unitSelection = null;
        _vanillaMarker = null;
        _markerParent = null;
    }
    static T GetPrivate<T>(object obj, Type type, string fieldName) where T : class
    {
        FieldInfo fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return fi?.GetValue(obj) as T;
    }
    public void SetGroup(IEnumerable<Unit> units, Unit primary)
    {
        _group.Clear();
        if (units != null) _group.AddRange(units.Where(u => u));
        _primary = (primary && _group.Contains(primary)) ? primary : _group.FirstOrDefault();
        RefreshFollowerMarkers();
    }
    public void ClearGroupAndTryVanillaDeselect()
    {
        _group.Clear(); _primary = null;
        RefreshFollowerMarkers();
        TryVanillaDeselect();
    }
    public void TryVanillaSelectPrimary(Unit primary)
    {
        try
        {
            UnitSelection us = _unitSelection ?? FindObjectOfType<UnitSelection>();
            if (!us) return;
            IEditorSelectable sel = primary ? primary.GetComponentInParent<IEditorSelectable>() : null;
            if (sel != null) us.SetSelection(sel);
        }
        catch { }
    }
    public void TryVanillaDeselect()
    {
        try { (_unitSelection ?? FindObjectOfType<UnitSelection>())?.ClearSelection(); }
        catch { }
    }
    void OnVanillaSelect(SelectionDetails sd)
    {
        if (_group.Count == 0) return;
        if (sd == null) { _group.Clear(); _primary = null; RefreshFollowerMarkers(); return; }
        UnitSelectionDetails usd = sd as UnitSelectionDetails;
        Unit selectedUnit = usd?.Unit;
        if (!selectedUnit) { _group.Clear(); _primary = null; RefreshFollowerMarkers(); return; }
        if (_group.Contains(selectedUnit)) { _primary = selectedUnit; RefreshFollowerMarkers(); return; }
        _group.Clear(); _primary = null; RefreshFollowerMarkers();
    }
    void OnDragBegin()
    {
        if (_group.Count == 0 || _primary == null || _unitSelection?.SelectionDetails == null) return;
        IValueWrapper<GlobalPosition> primPosW = _unitSelection.SelectionDetails.PositionWrapper;
        IValueWrapper<Quaternion> primRotW = _unitSelection.SelectionDetails.RotationWrapper;
        if (primPosW != null) _primStartPos = primPosW.Value;
        if (primRotW != null) _primStartRot = primRotW.Value;
        _followers.Clear();
        foreach (Unit u in _group)
        {
            if (!u || u == _primary) continue;
            IEditorSelectable selectable = u.GetComponentInParent<IEditorSelectable>();
            if (selectable == null) continue;
            SelectionDetails sd = selectable.CreateSelectionDetails();
            if (sd == null) continue;
            IValueWrapper<GlobalPosition> posW = sd.PositionWrapper;
            IValueWrapper<Quaternion> rotW = sd.RotationWrapper;
            if (posW == null) continue;
            _followers.Add(new Follower
            {
                unit = u,
                posW = posW,
                rotW = rotW,
                startPos = posW.Value,
                startRot = rotW != null ? rotW.Value : u.transform.rotation
            });
        }
    }

    void OnDragTick()
    {
        if (_followers.Count == 0 || _unitSelection?.SelectionDetails == null || _rth == null) return;
        HandleType ht = _rth.type;
        IValueWrapper<GlobalPosition> primPosW = _unitSelection.SelectionDetails.PositionWrapper;
        IValueWrapper<Quaternion> primRotW = _unitSelection.SelectionDetails.RotationWrapper;
        Vector3 deltaWorld = Vector3.zero;
        if (primPosW != null)
            deltaWorld = primPosW.Value.ToLocalPosition() - _primStartPos.ToLocalPosition();
        Quaternion dRot = Quaternion.identity;
        if (primRotW != null)
            dRot = primRotW.Value * Quaternion.Inverse(_primStartRot);
        if (ht == HandleType.POSITION && primPosW != null)
        {
            for (int i = 0; i < _followers.Count; i++)
            {
                Follower f = _followers[i];
                Vector3 newWorld = f.startPos.ToLocalPosition() + deltaWorld;
                newWorld = ClampYForUnit(f.unit, newWorld);
                f.posW.SetValue(newWorld.ToGlobalPosition(), this, true);
            }
        }
        else if (ht == HandleType.ROTATION && primRotW != null)
        {
            Vector3 pivotWorld = _primStartPos.ToLocalPosition();
            for (int i = 0; i < _followers.Count; i++)
            {
                Follower f = _followers[i];
                Vector3 from = f.startPos.ToLocalPosition() - pivotWorld;
                Vector3 rotated = dRot * from;
                Vector3 newWorld = pivotWorld + rotated;
                newWorld = ClampYForUnit(f.unit, newWorld);
                f.posW.SetValue(newWorld.ToGlobalPosition(), this, true);
                f.rotW?.SetValue(dRot * f.startRot, this, true);
            }
        }
    }
    void OnDragEnd() { _followers.Clear(); }
    static float TerrainYAt(Vector3 worldPos)
    {
        int n = Physics.RaycastNonAlloc(worldPos + Vector3.up * 10000f, Vector3.down, _rayHits, 20000f, TERRAIN_MASK);
        if (n == 0) return -100f;
        float? bestY = null;
        for (int i = 0; i < n; i++)
        {
            RaycastHit hit = _rayHits[i];
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<Unit>() != null) continue;
            float y = hit.point.y;
            if (bestY == null || y > bestY.Value) bestY = y;
        }
        return bestY ?? -100f;
    }
    static Vector3 ClampYForUnit(Unit unit, Vector3 worldPos)
    {
        if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
            return worldPos;

        UnitDefinition def = unit.definition;
        float terrY = TerrainYAt(worldPos);
        float baseY = terrY + def.spawnOffset.y;
        float minY = baseY + def.minEditorHeight;
        float maxY = baseY + def.maxEditorHeight;
        worldPos.y = Mathf.Clamp(worldPos.y, minY, maxY);
        return worldPos;
    }
    void RefreshFollowerMarkers()
    {
        if (!_vanillaMarker || !_markerParent) { ClearAllMarkers(); return; }
        HashSet<Unit> desired = [.. _group.Where(u => u && u != _primary)];
        List<Unit> toRemove = [.. _markerByUnit.Keys.Where(u => !desired.Contains(u))];
        foreach (Unit u in toRemove)
        {
            if (_markerByUnit[u]) Destroy(_markerByUnit[u]);
            _markerByUnit.Remove(u);
            _factionByUnit.Remove(u);
        }
        foreach (Unit u in desired)
        {
            if (_markerByUnit.ContainsKey(u)) continue;
            GameObject go = Instantiate(_vanillaMarker.gameObject, _markerParent, worldPositionStays: false);
            go.name = "selectionMarker(Clone-multi)";
            go.SetActive(true);
            IEditorSelectable sel = u.GetComponentInParent<IEditorSelectable>();
            Faction fac = null;
            if (sel != null)
            {
                SelectionDetails sd = sel.CreateSelectionDetails();
                if (sd != null) fac = sd.Faction;
            }
            _factionByUnit[u] = fac;
            _markerByUnit[u] = go;
        }
    }
    void LateUpdate()
    {
        if (_markerByUnit.Count == 0) return;
        foreach (KeyValuePair<Unit, GameObject> kv in _markerByUnit)
        {
            Unit unit = kv.Key;
            GameObject go = kv.Value;
            if (!unit || !go) continue;
            Transform t = go.transform;
            t.localScale = Vector3.one * (unit.definition.length / 7f);
            t.position = unit.transform.position;
            t.rotation = Quaternion.LookRotation(unit.transform.forward, Vector3.up);
            EditorCursor cursor = go.GetComponent<EditorCursor>();
            if (cursor != null)
            {
                _factionByUnit.TryGetValue(unit, out Faction fac);
                cursor.SetColor(unit, fac);
            }
        }
    }
    void ClearAllMarkers()
    {
        foreach (KeyValuePair<Unit, GameObject> kv in _markerByUnit) if (kv.Value) Destroy(kv.Value);
        _markerByUnit.Clear();
        _factionByUnit.Clear();
    }
}
