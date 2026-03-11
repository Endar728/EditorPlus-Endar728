using System;
using System.Collections.Generic;
using System.Reflection;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission.ObjectiveV2;
using UnityEngine;
using EditorPlus.Patches;

namespace EditorPlus
{
    [DisallowMultipleComponent]
    public sealed class GroupFollowers : MonoBehaviour
    {
        private const int TERRAIN_MASK = 1 << 6;
        private const float MOVE_EPS_SQR = 1e-8f;
        private const float ROT_DOT_EPS = 0.99999f;
        private const float MARKER_DIV = 7f;
        private const int MAX_MARKERS = 64;
        private const int LARGE_GROUP_THROTTLE = 4;
        private static readonly RaycastHit[] RAY_HITS = new RaycastHit[64];
        private static MethodInfo _setColorMethod;
        private int _markerUpdateSkip;
        private bool _dragging;
        private Coroutine _nullSelectRoutine;
        private string _lastPrimaryFaction;
        private readonly List<Unit> _group = [];
        private readonly HashSet<Unit> _groupSet = [];
        public IReadOnlyList<Unit> CurrentUnits => _group;
        private Unit _primary;
        private GlobalPosition _primStartPos;
        private Vector3 _primStartWorld;
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
        private readonly List<Collider> _dragDisabledColliders = new List<Collider>();
        private readonly List<(Rigidbody rb, bool wasKinematic)> _dragRigidbodies = new List<(Rigidbody, bool)>();
        private readonly List<(Transform t, int layer)> _dragLayerBackup = new List<(Transform, int)>();
        private const int LAYER_IGNORE_DURING_DRAG = 2;
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
            RestoreCollisionAfterDrag();
            ClearAllMarkers();
        }
        public void SetGroup(IEnumerable<Unit> units, Unit primary)
        {
            _group.Clear();
            _groupSet.Clear();
            _seen.Clear();
            if (units != null)
                foreach (var u in units)
                    if (u && _seen.Add(u)) { _group.Add(u); _groupSet.Add(u); }
            _primary = (_groupSet.Contains(primary) ? primary : (_group.Count > 0 ? _group[0] : null));
            if (_followers.Capacity < _group.Count) _followers.Capacity = _group.Count;
            if (_scratchUnits.Capacity < _group.Count) _scratchUnits.Capacity = _group.Count;
            RebuildFollowers();
            SnapshotPrimaryBaseline();
            RefreshFollowerMarkers();
            if (Plugin.Instance != null && Plugin.Instance.holdpos)
                for (int i = 0; i < _group.Count; i++) { var su = _group[i]?.SavedUnit; if (su != null) HoldPositionHelper.ApplyToSavedUnit(su, true); }
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
            if (Plugin.Instance != null && Plugin.Instance.holdpos && u.SavedUnit != null)
                HoldPositionHelper.ApplyToSavedUnit(u.SavedUnit, true);
            CancelNullSelectRoutine();
            if (_group.Count == 0 || !_groupSet.Contains(u))
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
            if (_primary == null || !_primary)
            {
                if (_group.Count > 0) ClearGroup();
                UpdateMarkers();
                return;
            }
            PruneDestroyedUnits();
            if (_group.Count <= 1)
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
            if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftShift))
            {
                _dragging = true;
                DisableCollisionForDrag();
                CaptureDragBaseline();
            }
            if (_dragging && Input.GetMouseButtonUp(0))
            {
                ApplyDragDelta();
                RestoreCollisionAfterDrag();
                _dragging = false;
            }
            UpdateMarkers();
        }

        private void PruneDestroyedUnits()
        {
            if (_group.Count == 0) return;
            bool pruned = false;
            for (int i = _group.Count - 1; i >= 0; i--)
            {
                var u = _group[i];
                if (!u) { _groupSet.Remove(u); _group.RemoveAt(i); pruned = true; }
            }
            if (_primary != null && !_primary) { _primary = null; pruned = true; }
            if (_primary == null || !_groupSet.Contains(_primary)) { _primary = _group.Count > 0 ? _group[0] : null; pruned = true; }
            if (pruned && _primary != null && _group.Count > 1) RebuildFollowers();
        }

        private void LateUpdate()
        {
            if (_primary == null || !_primary || _group.Count <= 1) return;
            if (_dragging && Input.GetMouseButton(0))
                ApplyDragDelta();
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
                IValueWrapper<GlobalPosition> posW = su?.PositionWrapper;
                IValueWrapper<Quaternion> rotW = su?.RotationWrapper;
                if (su != null && (posW == null || rotW == null))
                {
                    var t = su.GetType();
                    if (posW == null)
                    {
                        var prop = t.GetProperty("PositionWrapper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        posW = prop?.GetValue(su) as IValueWrapper<GlobalPosition>;
                    }
                    if (rotW == null)
                    {
                        var prop = t.GetProperty("RotationWrapper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        rotW = prop?.GetValue(su) as IValueWrapper<Quaternion>;
                    }
                }
                _followers.Add(new Follower
                {
                    unit = u,
                    posW = posW,
                    rotW = rotW,
                    startWorld = posW != null ? posW.Value.ToLocalPosition() : u.transform.position,
                    startRot = rotW != null ? rotW.Value : u.transform.rotation
                });
            }
        }
        private void SnapshotPrimaryBaseline()
        {
            if (!_primary) return;
            NuclearOption.SavedMission.SavedUnit psu = _primary.SavedUnit;
            _primStartPos = psu?.PositionWrapper != null ? psu.PositionWrapper.Value : _primary.transform.position.ToGlobalPosition();
            _primStartWorld = _primary.transform.position;
            _primStartRot = _primary.transform.rotation;
        }
        private void DisableCollisionForDrag()
        {
            RestoreCollisionAfterDrag();
            for (int i = 0; i < _group.Count; i++)
            {
                Unit u = _group[i];
                if (!u) continue;
                Transform[] transforms = u.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    Transform tr = transforms[t];
                    if (!tr) continue;
                    GameObject go = tr.gameObject;
                    _dragLayerBackup.Add((tr, go.layer));
                    go.layer = LAYER_IGNORE_DURING_DRAG;
                }
                Collider[] colliders = u.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider col = colliders[c];
                    if (col && col.enabled) { col.enabled = false; _dragDisabledColliders.Add(col); }
                }
                Rigidbody[] rbs = u.GetComponentsInChildren<Rigidbody>(true);
                for (int r = 0; r < rbs.Length; r++)
                {
                    Rigidbody rb = rbs[r];
                    if (rb) { _dragRigidbodies.Add((rb, rb.isKinematic)); rb.isKinematic = true; }
                }
            }
        }

        private void RestoreCollisionAfterDrag()
        {
            for (int i = 0; i < _dragLayerBackup.Count; i++)
            {
                var (t, layer) = _dragLayerBackup[i];
                if (t && t.gameObject) t.gameObject.layer = layer;
            }
            _dragLayerBackup.Clear();
            for (int i = 0; i < _dragDisabledColliders.Count; i++)
            {
                if (_dragDisabledColliders[i]) _dragDisabledColliders[i].enabled = true;
            }
            _dragDisabledColliders.Clear();
            for (int i = 0; i < _dragRigidbodies.Count; i++)
            {
                var (rb, wasKinematic) = _dragRigidbodies[i];
                if (rb) rb.isKinematic = wasKinematic;
            }
            _dragRigidbodies.Clear();
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
            Vector3 primStart = _primStartWorld;
            Vector3 primNow = _primary.transform.position;
            Quaternion rotNow = _primary.transform.rotation;
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
                if (!_dragging && _followers.Count <= 100) newWorld = ClampYForUnit(f.unit, newWorld);
                Quaternion followerRot = dRot * f.startRot;
                if (f.posW != null) SafeSetPosition(f.posW, newWorld.ToGlobalPosition());
                if (f.rotW != null) SafeSetRotation(f.rotW, followerRot);
                f.unit.transform.position = newWorld;
                f.unit.transform.rotation = followerRot;
            }
        }


        private void SafeSetPosition(IValueWrapper<GlobalPosition> posW, GlobalPosition value)
        {
            try { posW.SetValue(value, this, true); }
            catch { try { posW.SetValue(value, this, false); } catch { } }
        }

        private void SafeSetRotation(IValueWrapper<Quaternion> rotW, Quaternion value)
        {
            try { rotW.SetValue(value, this, true); }
            catch { try { rotW.SetValue(value, this, false); } catch { } }
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
            _groupSet.Clear();
            _followers.Clear();
            _primary = null;
            ClearAllMarkers();
        }
        private void ResolveMarkerRefs()
        {
            if (_markerParent && _vanillaMarker) return;
            // Prefer working-perfect hierarchy: "Scene Markers" / "selectionMarker"
            GameObject root = GameObject.Find("Scene Markers");
            if (root)
            {
                _markerParent = root.transform;
                Transform child = _markerParent.Find("selectionMarker");
                if (child) _vanillaMarker = child;
            }
            if (_vanillaMarker) return;
            // Fallback: "EditorMarkers" / "UnitMarker" (alternate scene layout)
            root = GameObject.Find("EditorMarkers");
            if (root)
            {
                _markerParent = root.transform;
                Transform child = _markerParent.Find("UnitMarker");
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
                else if (c.name.IndexOf("UnitMarker", StringComparison.OrdinalIgnoreCase) >= 0)
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
                if (!_groupSet.Contains(kv.Key) || kv.Key == _primary)
                    _scratchUnits.Add(kv.Key);
            for (int i = 0; i < _scratchUnits.Count; i++)
            {
                var u = _scratchUnits[i];
                if (_markers.TryGetValue(u, out var mi) && mi.go) Destroy(mi.go);
                _markers.Remove(u);
            }
            int markerCount = 0;
            for (int i = 0; i < _group.Count; i++)
            {
                var u = _group[i];
                if (!u || u == _primary) continue;
                if (markerCount >= MAX_MARKERS)
                {
                    if (_markers.TryGetValue(u, out var excess) && excess.go) { Destroy(excess.go); _markers.Remove(u); }
                    continue;
                }
                if (!_markers.TryGetValue(u, out var mi) || mi.go == null)
                {
                    var go = Instantiate(_vanillaMarker.gameObject, _markerParent, false);
                    go.name = "selectionMarker(Clone-multi)";
                    go.SetActive(true);
                    _markers[u] = new MarkerInfo(go, go.GetComponent<EditorCursor>());
                }
                markerCount++;
            }
        }
        private void UpdateMarkers()
        {
            if (_markers.Count == 0) return;
            if (_markers.Count > 32 && (++_markerUpdateSkip % LARGE_GROUP_THROTTLE) != 0) return;
            if (_setColorMethod == null)
                _setColorMethod = typeof(EditorCursor).GetMethod("SetColor", new[] { typeof(Unit), typeof(Faction) });
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
                if (mi.cursor != null && _setColorMethod != null)
                    _setColorMethod.Invoke(mi.cursor, new object[] { u, fac });
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
            RestoreCollisionAfterDrag();
            _group.Clear();
            _groupSet.Clear();
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
