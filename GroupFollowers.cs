using System;
using System.Collections.Generic;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
using UnityEngine;
namespace EditorPlus
{
    [DisallowMultipleComponent]
    public sealed class GroupFollowers : MonoBehaviour
    {
        private const int TERRAIN_MASK = 1 << 6;
        private const float MOVE_EPS_SQR = 1e-8f;
        private const float ROT_DOT_EPS = 0.99999f;
        private const float MARKER_DIV = 7f;
        private static readonly RaycastHit[] RAY_HITS = new RaycastHit[64];
        private bool _dragging;
        private Coroutine _nullSelectRoutine;
        private string _lastPrimaryFaction;
        private readonly List<Unit> _group = [];
        private Unit _primary;
        private GlobalPosition _primStartPos;
        private Quaternion _primStartRot = Quaternion.identity;
        private readonly HashSet<Unit> _seen = new();
        private struct Follower
        {
            public Unit unit;
            public Vector3 startWorld;
            public Quaternion startRot;
            public IValueWrapper<GlobalPosition> posW;
            public IValueWrapper<Quaternion> rotW;
        }
        private readonly List<Follower> _followers = [];
        private Transform _markerParent, _vanillaMarker;
        private readonly Dictionary<Unit, MarkerInfo> _markers = [];
        private readonly List<Unit> _scratchUnits = [];
        private readonly struct MarkerInfo
        {
            public readonly GameObject go;
            public readonly EditorCursor cursor;
            public MarkerInfo(GameObject go, EditorCursor cursor) { this.go = go; this.cursor = cursor; }
        }
        private static UnitSelection USel => SceneSingleton<UnitSelection>.i;
        private static MissionEditor Editor => SceneSingleton<MissionEditor>.i;
        private void OnEnable()
        {
            if (USel != null) USel.OnSelect += OnVanillaSelect;
            ResolveMarkerRefs();
        }
        private void OnDisable()
        {
            if (USel != null) USel.OnSelect -= OnVanillaSelect;
            CancelNullSelectRoutine();
            ClearAllMarkers();
        }
        public void SetGroup(IEnumerable<Unit> units, Unit primary)
        {
            _group.Clear();
            _seen.Clear();
            if (units != null)
                foreach (var u in units)
                    if (u && _seen.Add(u)) _group.Add(u);
            _primary = (_group.Contains(primary) ? primary : (_group.Count > 0 ? _group[0] : null));
            if (_followers.Capacity < _group.Count) _followers.Capacity = _group.Count;
            if (_scratchUnits.Capacity < _group.Count) _scratchUnits.Capacity = _group.Count;
            RebuildFollowers();
            SnapshotPrimaryBaseline();
            RefreshFollowerMarkers();
        }
        public void TryVanillaSelectPrimary(Unit primary)
        {
            if (!USel || !primary) return;
            IEditorSelectable sel = primary.GetComponentInParent<IEditorSelectable>();
            if (sel != null) USel.SetSelection(sel);
        }
        public void ClearGroupAndTryVanillaDeselect()
        {
            ClearGroup();
            USel?.ClearSelection();
        }
        private void OnVanillaSelect(SelectionDetails sd)
        {
            Unit u = (sd as UnitSelectionDetails)?.Unit;
            if (!u)
            {
                CancelNullSelectRoutine();
                _nullSelectRoutine = StartCoroutine(DeferredNullSelectionClear());
                return;
            }
            CancelNullSelectRoutine();
            if (_group.Count == 0 || !_group.Contains(u))
            {
                ClearGroup();
                return;
            }
            if (u == _primary) return;
            _primary = u;
            RebuildFollowers();
            SnapshotPrimaryBaseline();
            RefreshFollowerMarkers();
        }
        private System.Collections.IEnumerator DeferredNullSelectionClear()
        {
            yield return null;
            _nullSelectRoutine = null;
            ClearGroup();
        }
        private void CancelNullSelectRoutine()
        {
            if (_nullSelectRoutine != null)
            {
                StopCoroutine(_nullSelectRoutine);
                _nullSelectRoutine = null;
            }
        }
        private void Update()
        {
            if (_primary == null || _group.Count <= 1)
            {
                UpdateMarkers();
                return;
            }
            string fac = _primary.NetworkHQ?.faction?.factionName ?? _primary.SavedUnit?.faction;
            if (fac != _lastPrimaryFaction)
            {
                _lastPrimaryFaction = fac;
                if (!string.IsNullOrEmpty(fac)) PropagateFactionToFollowers(fac);
            }
            if (Input.GetMouseButtonDown(0)) { _dragging = true; CaptureDragBaseline(); }
            if (_dragging && Input.GetMouseButton(0)) ApplyDragDelta();
            if (_dragging && Input.GetMouseButtonUp(0)) _dragging = false;
            UpdateMarkers();
        }
        private void RebuildFollowers()
        {
            _followers.Clear();
            if (!_primary) return;
            for (int i = 0; i < _group.Count; i++)
            {
                var u = _group[i];
                if (!u || u == _primary) continue;
                var su = u.SavedUnit;
                _followers.Add(new Follower
                {
                    unit = u,
                    posW = su?.PositionWrapper,
                    rotW = su?.RotationWrapper,
                    startWorld = su?.PositionWrapper != null ? su.PositionWrapper.Value.ToLocalPosition() : u.transform.position,
                    startRot = su?.RotationWrapper != null ? su.RotationWrapper.Value : u.transform.rotation
                });
            }
        }
        private void SnapshotPrimaryBaseline()
        {
            if (!_primary) return;
            NuclearOption.SavedMission.SavedUnit psu = _primary.SavedUnit;
            _primStartPos = psu?.PositionWrapper != null ? psu.PositionWrapper.Value : _primary.transform.position.ToGlobalPosition();
            _primStartRot = psu?.RotationWrapper != null ? psu.RotationWrapper.Value : _primary.transform.rotation;
        }
        private void CaptureDragBaseline()
        {
            SnapshotPrimaryBaseline();
            for (int i = 0; i < _followers.Count; i++)
            {
                Follower f = _followers[i];
                if (!f.unit) continue;
                f.startWorld = f.posW != null ? f.posW.Value.ToLocalPosition() : f.unit.transform.position;
                f.startRot = f.rotW != null ? f.rotW.Value : f.unit.transform.rotation;
                _followers[i] = f;
            }
        }
        private void ApplyDragDelta()
        {
            if (!_primary) return;
            NuclearOption.SavedMission.SavedUnit primSU = _primary.SavedUnit;
            ValueWrapperGlobalPosition primPosW = primSU?.PositionWrapper;
            ValueWrapperQuaternion primRotW = primSU?.RotationWrapper;
            if (primPosW == null && primRotW == null) return;
            Vector3 primStart = _primStartPos.ToLocalPosition();
            Vector3 primNow = primPosW != null ? primPosW.Value.ToLocalPosition() : primStart;
            Quaternion rotNow = primRotW != null ? primRotW.Value : _primStartRot;
            Quaternion dRot = rotNow * Quaternion.Inverse(_primStartRot);
            Vector3 dPos = primNow - primStart;
            if (dPos.sqrMagnitude < MOVE_EPS_SQR && Quaternion.Dot(dRot, Quaternion.identity) > ROT_DOT_EPS)
                return;
            for (int i = 0; i < _followers.Count; i++)
            {
                Follower f = _followers[i];
                if (!f.unit) continue;
                Vector3 rel = f.startWorld - primStart;
                Vector3 newWorld = dRot * rel + primStart + dPos;
                newWorld = ClampYForUnit(f.unit, newWorld);
                if (f.posW != null) f.posW.SetValue(newWorld.ToGlobalPosition(), this, true);
                if (f.rotW != null) f.rotW.SetValue(dRot * f.startRot, this, true);
            }
        }
        public void PropagateFactionToFollowers(string factionName)
        {
            if (string.IsNullOrEmpty(factionName) || _group.Count <= 1 || !_primary) return;
            for (int i = 0; i < _group.Count; i++)
            {
                Unit u = _group[i];
                if (!u || u == _primary) continue;
                NuclearOption.SavedMission.SavedUnit su = u.SavedUnit;
                if (su == null) continue;
                su.faction = factionName;
                u.NetworkHQ = FactionRegistry.HqFromName(factionName);
                if (u.TryGetComponent<Airbase>(out Airbase airbase))
                    airbase.EditorSetFaction(factionName, true);
            }
            USel?.RefreshSelected();
        }
        public void OnPrimaryDeletedCascade(Unit deleted)
        {
            if (Editor != null)
            {
                for (int i = 0; i < _group.Count; i++)
                {
                    var u = _group[i];
                    if (u && u != deleted) Editor.RemoveUnit(u);
                }
            }
            USel?.ClearSelection();
            _group.Clear();
            _followers.Clear();
            _primary = null;
            ClearAllMarkers();
        }
        private void ResolveMarkerRefs()
        {
            if (_markerParent && _vanillaMarker) return;
            GameObject root = GameObject.Find("Scene Markers");
            if (root)
            {
                _markerParent = root.transform;
                Transform child = _markerParent.Find("selectionMarker");
                if (child) _vanillaMarker = child;
            }
            if (_vanillaMarker) return;
            EditorCursor[] cursors = FindObjectsOfType<EditorCursor>(true);
            for (int i = 0; i < cursors.Length && !_vanillaMarker; i++)
            {
                EditorCursor c = cursors[i];
                if (!c) continue;
                if (c.name.IndexOf("selectionMarker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _vanillaMarker = c.transform;
                    _markerParent = c.transform.parent;
                }
            }
        }
        private void RefreshFollowerMarkers()
        {
            ResolveMarkerRefs();
            if (!_markerParent || !_vanillaMarker) { ClearAllMarkers(); return; }
            _scratchUnits.Clear();
            foreach (var kv in _markers)
                if (!_group.Contains(kv.Key) || kv.Key == _primary)
                    _scratchUnits.Add(kv.Key);
            for (int i = 0; i < _scratchUnits.Count; i++)
            {
                var u = _scratchUnits[i];
                if (_markers.TryGetValue(u, out var mi) && mi.go) Destroy(mi.go);
                _markers.Remove(u);
            }
            for (int i = 0; i < _group.Count; i++)
            {
                var u = _group[i];
                if (!u || u == _primary) continue;
                if (!_markers.TryGetValue(u, out var mi) || mi.go == null)
                {
                    var go = Instantiate(_vanillaMarker.gameObject, _markerParent, false);
                    go.name = "selectionMarker(Clone-multi)";
                    go.SetActive(true);
                    _markers[u] = new MarkerInfo(go, go.GetComponent<EditorCursor>());
                }
            }
        }
        private void UpdateMarkers()
        {
            if (_markers.Count == 0) return;
            foreach (KeyValuePair<Unit, MarkerInfo> kv in _markers)
            {
                Unit u = kv.Key;
                MarkerInfo mi = kv.Value;
                if (!u || !mi.go) continue;
                Transform t = mi.go.transform;
                t.localScale = Vector3.one * (u.definition.length / MARKER_DIV);
                t.position = u.transform.position;
                t.rotation = Quaternion.LookRotation(u.transform.forward, Vector3.up);
                Faction fac = u.NetworkHQ != null ? u.NetworkHQ.faction : null;
                mi.cursor?.SetColor(u, fac);
            }
        }

        private static Vector3 ClampYForUnit(Unit unit, Vector3 worldPos)
        {
            if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain) return worldPos;
            UnitDefinition def = unit.definition;
            float terrY = TerrainYAt(worldPos);
            float baseY = terrY + def.spawnOffset.y;
            float minY = baseY + def.minEditorHeight;
            float maxY = baseY + def.maxEditorHeight;
            worldPos.y = Mathf.Clamp(worldPos.y, minY, maxY);
            return worldPos;
        }
        private static float TerrainYAt(Vector3 worldPos)
        {
            int n = Physics.RaycastNonAlloc(worldPos + Vector3.up * 10000f, Vector3.down, RAY_HITS, 20000f, TERRAIN_MASK);
            if (n == 0) return worldPos.y;
            float bestY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = RAY_HITS[i];
                if (!hit.collider) continue;
                bestY = Mathf.Max(bestY, hit.point.y);
            }
            return bestY == float.MinValue ? worldPos.y : bestY;
        }
        private void ClearGroup()
        {
            _group.Clear();
            _primary = null;
            _followers.Clear();
            _dragging = false;
            CancelNullSelectRoutine();
            ClearAllMarkers();
        }
        private void ClearAllMarkers()
        {
            foreach (MarkerInfo mi in _markers.Values) if (mi.go) Destroy(mi.go);
            _markers.Clear();
        }
    }
}