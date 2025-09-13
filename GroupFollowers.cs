using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
using RuntimeHandle;
using UnityEngine;

namespace EditorPlus
{
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
        readonly List<Unit> _toRemove = new();
        void OnEnable() { TryBindVanilla(); }
        void OnDisable() { UnbindVanilla(); }
        static FieldInfo _editorHandleField;
        static FieldInfo _unitSelectionMarkerField;

        static T GetPrivateCached<T>(object obj, ref FieldInfo cache, Type type, string fieldName) where T : class
        {
            cache ??= type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return cache?.GetValue(obj) as T;
        }

        void TryBindVanilla()
        {
            _editorHandle = FindObjectOfType<EditorHandle>();
            if (!_editorHandle) return;

            _rth = GetPrivateCached<RuntimeTransformHandle>(_editorHandle, ref _editorHandleField, typeof(EditorHandle), "handle");
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
                _vanillaMarker = GetPrivateCached<Transform>(_unitSelection, ref _unitSelectionMarkerField, typeof(UnitSelection), "unitSelectionMarker");
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

            ClearAllMarkers();

            _rth = null;
            _editorHandle = null;
            _unitSelection = null;
            _vanillaMarker = null;
            _markerParent = null;
        }
        public void SetGroup(IEnumerable<Unit> units, Unit primary)
        {
            _group.Clear();
            if (units != null)
            {
                var seen = new HashSet<Unit>();
                foreach (var u in units)
                    if (u && seen.Add(u)) _group.Add(u);
            }
            _primary = (primary && _group.Contains(primary)) ? primary : _group.FirstOrDefault();
            RefreshFollowerMarkers();
        }

        public void ClearGroupAndTryVanillaDeselect()
        {
            ClearGroup();
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

            Unit unit = (sd as UnitSelectionDetails)?.Unit;
            if (!unit || !_group.Contains(unit))
            {
                ClearGroup();
                return;
            }

            _primary = unit;
            RefreshFollowerMarkers();
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
            if (_followers.Count == 0 || _rth == null) return;
            SelectionDetails sdPrim = _unitSelection?.SelectionDetails;
            if (sdPrim == null) return;

            IValueWrapper<GlobalPosition> primPosW = sdPrim.PositionWrapper;
            IValueWrapper<Quaternion> primRotW = sdPrim.RotationWrapper;

            Vector3 deltaWorld = primPosW != null
                ? primPosW.Value.ToLocalPosition() - _primStartPos.ToLocalPosition()
                : Vector3.zero;

            Quaternion dRot = primRotW != null
                ? primRotW.Value * Quaternion.Inverse(_primStartRot)
                : Quaternion.identity;

            switch (_rth.type)
            {
                case HandleType.POSITION when primPosW != null:
                    for (int i = 0; i < _followers.Count; i++)
                    {
                        Follower f = _followers[i];
                        Vector3 newWorld = ClampYForUnit(f.unit, f.startPos.ToLocalPosition() + deltaWorld);
                        f.posW.SetValue(newWorld.ToGlobalPosition(), this, true);
                    }
                    break;

                case HandleType.ROTATION when primRotW != null:
                    Vector3 pivotWorld = _primStartPos.ToLocalPosition();
                    for (int i = 0; i < _followers.Count; i++)
                    {
                        Follower f = _followers[i];
                        Vector3 from = f.startPos.ToLocalPosition() - pivotWorld;
                        Vector3 rotated = dRot * from;
                        Vector3 newWorld = ClampYForUnit(f.unit, pivotWorld + rotated);
                        f.posW.SetValue(newWorld.ToGlobalPosition(), this, true);
                        f.rotW?.SetValue(dRot * f.startRot, this, true);
                    }
                    break;
            }
        }

        void OnDragEnd() { _followers.Clear(); }
        static float TerrainYAt(Vector3 worldPos)
        {
            int n = Physics.RaycastNonAlloc(worldPos + Vector3.up * 10000f, Vector3.down, _rayHits, 20000f, TERRAIN_MASK);
            if (n == 0) return -100;

            bool found = false;
            float bestY = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = _rayHits[i];
                if (!hit.collider) continue;
                if (hit.collider.GetComponentInParent<Unit>() != null) continue;

                float y = hit.point.y;
                if (!found || y > bestY) { bestY = y; found = true; }
            }

            return found ? bestY : -100;
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
        void ClearGroup()
        {
            _group.Clear();
            _primary = null;
            RefreshFollowerMarkers();
        }
        readonly struct MarkerInfo(GameObject go, EditorCursor cursor, Faction faction)
        {
            public readonly GameObject go = go;
            public readonly EditorCursor cursor = cursor;
            public readonly Faction faction = faction;
        }
        readonly Dictionary<Unit, MarkerInfo> _marker = [];

        void RefreshFollowerMarkers()
        {
            if (!_vanillaMarker || !_markerParent) { ClearAllMarkers(); return; }

            _toRemove.Clear();
            foreach (var u in _marker.Keys)
                if (!_group.Contains(u) || u == _primary)
                    _toRemove.Add(u);

            foreach (var u in _toRemove)
            {
                var mi = _marker[u];
                if (mi.go) Destroy(mi.go);
                _marker.Remove(u);
            }

            foreach (var u in _group)
            {
                if (!u || u == _primary || _marker.ContainsKey(u)) continue;

                var go = Instantiate(_vanillaMarker.gameObject, _markerParent, false);
                go.name = "selectionMarker(Clone-multi)";
                go.SetActive(true);

                Faction fac = null;
                var sd = u.GetComponentInParent<IEditorSelectable>()?.CreateSelectionDetails();
                if (sd != null) fac = sd.Faction;

                var cursor = go.GetComponent<EditorCursor>();
                _marker[u] = new MarkerInfo(go, cursor, fac);
            }
        }

        void LateUpdate()
        {
            if (_marker.Count == 0) return;
            foreach (KeyValuePair<Unit, MarkerInfo> kv in _marker)
            {
                Unit unit = kv.Key;
                MarkerInfo mi = kv.Value;
                if (!unit || !mi.go) continue;

                Transform t = mi.go.transform;
                t.localScale = Vector3.one * (unit.definition.length / 7f);
                t.position = unit.transform.position;
                t.rotation = Quaternion.LookRotation(unit.transform.forward, Vector3.up);
                mi.cursor?.SetColor(unit, mi.faction);
            }
        }


        void ClearAllMarkers()
        {
            foreach (MarkerInfo mi in _marker.Values) if (mi.go) Destroy(mi.go);
            _marker.Clear();
        }

    }
}