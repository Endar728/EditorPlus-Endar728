using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus
{
    public static class GroupCopyPaste
    {
        private static ManualLogSource Log => Plugin.Logger;

        private static int _spawnCounter = 0;

        public static void CopySelectedGroup()
        {
            var selection = SceneSingleton<UnitSelection>.i;
            if (selection == null)
            {
                Log.LogWarning("[EditorPlus] UnitSelection not found");
                return;
            }

            // Prefer whichever source has MORE units so vanilla multi-select wins over a stale
            // single-unit GroupFollowers group; box-select still wins when vanilla only reports primary.
            var groupFollowers = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
            List<Unit> unitsToCopy = EditorSelectionHelper.ResolveSelectedUnits(selection, groupFollowers, out string sourceLabel);

            if (unitsToCopy == null || unitsToCopy.Count == 0)
            {
                Log.LogWarning("[EditorPlus] Nothing selected");
                return;
            }

            Log.LogInfo($"[EditorPlus] Copy selection source: {sourceLabel}, {unitsToCopy.Count} unit(s)");

            // Centroid in Unity world space so offsets match what you see; mixing mission AsVector3() with
            // paste ToGlobalPosition() was stacking every unit on the cursor.
            Vector3 center = Vector3.zero;
            int validCount = 0;
            foreach (var unit in unitsToCopy)
            {
                if (unit?.SavedUnit != null)
                {
                    center += unit.transform.position;
                    validCount++;
                }
            }
            if (validCount == 0)
            {
                Log.LogWarning("[EditorPlus] No valid units with SavedUnit");
                return;
            }
            center /= validCount;

            GroupClipboard.Clear();
            foreach (var unit in unitsToCopy)
            {
                if (unit?.SavedUnit == null) continue;

                GroupClipboard.Items.Add(new CopiedUnitData
                {
                    Type = unit.SavedUnit.type,
                    Faction = unit.SavedUnit.faction,
                    RelativeOffset = unit.transform.position - center,
                    // Match world pose: offsets use transform.position; SavedUnit.rotation can lag behind
                    // gizmo / GroupFollowers edits until the mission re-saves, so pasted units looked wrong yaw.
                    Rotation = unit.transform.rotation,
                    SourceSaved = unit.SavedUnit
                });
            }

            if (GroupClipboard.Items.Count == 0)
            {
                Log.LogWarning("[EditorPlus] No units copied to clipboard");
                return;
            }

            Log.LogInfo($"[EditorPlus] Copied {GroupClipboard.Items.Count} units");
        }

        public static void PasteGroupAtCursor()
        {
            Log.LogInfo($"[EditorPlus] PasteGroupAtCursor called, hasData={GroupClipboard.HasData}");

            if (!GroupClipboard.HasData)
            {
                Log.LogWarning("[EditorPlus] Clipboard empty");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Log.LogWarning("[EditorPlus] Camera.main is null");
                return;
            }

            // Raycast to find paste position
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Log.LogInfo($"[EditorPlus] Raycast from {ray.origin} dir {ray.direction}");

            // Terrain uses Unity layer mask 64 (= 1<<6), NOT layer index 64. Do not use 1<<64 (wraps to layer 0 in C#).
            const int TerrainOnlyLayerMask = 64;
            Vector3 hitPointLocal;
            if (Physics.Raycast(ray, out RaycastHit terrainHit, 100000f, TerrainOnlyLayerMask))
            {
                hitPointLocal = terrainHit.point;
                Log.LogInfo($"[EditorPlus] Paste ray hit terrain layer at {hitPointLocal}");
            }
            else if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                hitPointLocal = hit.point;
                Log.LogInfo($"[EditorPlus] Paste ray hit non-terrain collider at {hitPointLocal} (no terrain under cursor)");
                // Snap anchor Y to terrain under XZ so formation isn't anchored to a roof/wall depth
                if (TrySnapLocalYToTerrain(hitPointLocal, TerrainOnlyLayerMask, out Vector3 snapped))
                {
                    hitPointLocal = snapped;
                    Log.LogInfo($"[EditorPlus] Snapped paste anchor to terrain under cursor: {hitPointLocal}");
                }
            }
            else
            {
                // No hit — try water plane for over-sea pasting
                Plane waterPlane = new Plane(Vector3.up, new Vector3(0, Datum.LocalSeaY, 0));
                if (waterPlane.Raycast(ray, out float enter))
                {
                    hitPointLocal = ray.GetPoint(enter);
                    Log.LogInfo($"[EditorPlus] No terrain, using water plane at {hitPointLocal}");
                }
                else
                {
                    Log.LogWarning("[EditorPlus] Raycast missed — no terrain or water under cursor");
                    return;
                }
            }

            // Convert local hit point to global position
            // Use camera position to calculate origin offset (most reliable for paste location)
            Vector3 originOffset = Vector3.zero;
            try
            {
                Vector3 camLocalPos = cam.transform.position;
                GlobalPosition camGlobalPos = camLocalPos.ToGlobalPosition();
                Vector3 camGlobalVec = camGlobalPos.AsVector3();
                originOffset = camGlobalVec - camLocalPos;
                Log.LogInfo($"[EditorPlus] Using camera for offset: local={camLocalPos} global={camGlobalVec} offset={originOffset}");
            }
            catch
            {
                // Fallback: try to get offset from any nearby unit
                Unit[] nearbyUnits = UnityEngine.Object.FindObjectsOfType<Unit>();
                foreach (var unit in nearbyUnits)
                {
                    if (unit?.SavedUnit != null)
                    {
                        Vector3 localPos = unit.transform.position;
                        Vector3 globalPos = unit.SavedUnit.globalPosition.AsVector3();
                        originOffset = globalPos - localPos;
                        Log.LogInfo("[EditorPlus] Using nearby unit for offset calculation");
                        break;
                    }
                }
            }

            // Anchor is Unity world; we convert the anchor to GlobalPosition once, then add each unit's RelativeOffset
            // in global space (see OffsetFromPasteAnchor). Per-unit (anchor+offset).ToGlobalPosition() is not translation-
            // invariant with the game's floating origin and scatters large formations.
            Log.LogInfo($"[EditorPlus] Paste: hitWorld={hitPointLocal} offset={originOffset} (formation from anchor global + deltas)");
            
            // Start async paste to avoid freezing with large groups
            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(SpawnGroupAsync(hitPointLocal, originOffset));
            }
            else
            {
                // Fallback to synchronous if no plugin instance
                SpawnGroup(hitPointLocal, originOffset);
            }
        }

        public static void DuplicateInPlace()
        {
            // Clear clipboard first to prevent accumulation from previous operations
            GroupClipboard.Clear();
            Log.LogInfo("[EditorPlus] DuplicateInPlace: Cleared clipboard before copying");
            
            CopySelectedGroup();
            if (!GroupClipboard.HasData)
            {
                Log.LogWarning("[EditorPlus] DuplicateInPlace: No data copied to clipboard");
                return;
            }

            var first = GroupClipboard.Items[0];
            if (first.SourceSaved == null || first.SourceSaved.Unit == null)
            {
                Log.LogWarning("[EditorPlus] Source unit no longer exists");
                GroupClipboard.Clear(); // Clear on error
                return;
            }

            Vector3 localPos = first.SourceSaved.Unit.transform.position;
            Vector3 globalPos = first.SourceSaved.globalPosition.AsVector3();
            Vector3 originOffset = globalPos - localPos;

            Vector3 dupNudge = new Vector3(10f, 0f, 10f);
            // RelativeOffset = transform.position - centroid(world); centroid = first.pos - first.RelativeOffset
            Vector3 centerWorld = localPos - first.RelativeOffset;
            Vector3 dupAnchorWorld = centerWorld + dupNudge;
            
            Log.LogInfo($"[EditorPlus] DuplicateInPlace: Spawning {GroupClipboard.Items.Count} unit(s) at offset {dupNudge}");
            SpawnGroup(dupAnchorWorld, originOffset);
        }

        /// <summary>
        /// Convert paste anchor (Unity world hit) to <see cref="GlobalPosition"/> once per paste operation.
        /// </summary>
        private static GlobalPosition GetPasteAnchorGlobal(Vector3 pasteAnchorWorld, Vector3 originOffset)
        {
            try
            {
                return pasteAnchorWorld.ToGlobalPosition();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[EditorPlus] ToGlobalPosition(paste anchor) failed: {ex.Message}; using world+originOffset fallback");
                return new GlobalPosition(pasteAnchorWorld + originOffset);
            }
        }

        /// <summary>
        /// <paramref name="relativeOffsetWorld"/> is world-space delta from copy centroid.
        /// Adds it to <paramref name="anchorGlobal"/> (single anchor conversion). Do not call
        /// <c>(anchorWorld + offset).ToGlobalPosition()</c> per unit — it breaks formation layout on large maps.
        /// </summary>
        private static GlobalPosition OffsetFromPasteAnchor(GlobalPosition anchorGlobal, Vector3 relativeOffsetWorld, bool shipLift)
        {
            Vector3 delta = relativeOffsetWorld;
            if (shipLift)
                delta.y += 35f;
            return anchorGlobal + delta;
        }

        private static void SpawnGroup(Vector3 pasteAnchorWorld, Vector3 originOffset)
        {
            Log.LogInfo("[EditorPlus] SpawnGroup entered");

            var editor = SceneSingleton<MissionEditor>.i;
            var spawner = NetworkSceneSingleton<Spawner>.i;
            Log.LogInfo($"[EditorPlus] editor={editor != null} spawner={spawner != null}");

            if (editor == null || spawner == null)
            {
                Log.LogWarning("[EditorPlus] Editor or Spawner not available");
                return;
            }

            var mission = MissionManager.CurrentMission;
            Log.LogInfo($"[EditorPlus] mission={mission != null}");

            if (mission == null)
            {
                Log.LogWarning("[EditorPlus] No current mission");
                return;
            }

            GlobalPosition pasteAnchorGlobal = GetPasteAnchorGlobal(pasteAnchorWorld, originOffset);

            var spawnedUnits = new List<Unit>();
            bool deferFormationTerrain =
                GroupClipboard.Items.Count > 1
                && (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain);
            var formationClampEntries = deferFormationTerrain
                ? new List<(Unit unit, SavedUnit savedUnit, UnitDefinition def)>()
                : null;

            foreach (var data in GroupClipboard.Items)
            {
                try
                {
                    Log.LogInfo($"[EditorPlus] Spawning type={data.Type} faction={data.Faction}");

                    UnitDefinition definition = null;
                    if (data.SourceSaved != null && data.SourceSaved.Unit != null)
                    {
                        definition = data.SourceSaved.Unit.definition;
                        Log.LogInfo($"[EditorPlus] Got definition from source: {definition?.name}");
                    }

                    if (definition == null)
                    {
                        Log.LogInfo("[EditorPlus] Trying Encyclopedia lookup...");
                        if (!Encyclopedia.Lookup.TryGetValue(data.Type, out definition))
                        {
                            Log.LogWarning($"[EditorPlus] Unknown unit type: {data.Type}, skipping");
                            continue;
                        }
                    }

                    Log.LogInfo($"[EditorPlus] Looking up faction: {data.Faction}");
                    FactionHQ factionHQ = null;
                    // Try name lookup first (reliable for named factions)
                    if (!string.IsNullOrEmpty(data.Faction))
                    {
                        factionHQ = FactionRegistry.HqFromName(data.Faction);
                    }
                    // Fallback: get from source unit directly (for neutral/scenery with no faction name)
                    if (factionHQ == null && data.SourceSaved?.Unit != null)
                    {
                        try { factionHQ = data.SourceSaved.Unit.NetworkHQ; }
                        catch { Log.LogWarning("[EditorPlus] Failed to read NetworkHQ from source unit"); }
                    }
                    Log.LogInfo($"[EditorPlus] FactionHQ={factionHQ?.name ?? "null"}");

                    GlobalPosition gpos = OffsetFromPasteAnchor(pasteAnchorGlobal, data.RelativeOffset, definition is ShipDefinition);
                    if (definition is ShipDefinition)
                        Log.LogInfo("[EditorPlus] Ship detected, raised +35m for water drop");

                    // Generate a unique name with counter and random component to ensure uniqueness
                    string uniqueName = definition.unitPrefab.name + "_EP" + (++_spawnCounter) + "_" + UnityEngine.Random.Range(1000, 9999);

                    Log.LogInfo($"[EditorPlus] Calling SpawnFromUnitDefinitionInEditor name={uniqueName}");
                    Unit unit = spawner.SpawnFromUnitDefinitionInEditor(
                        definition, gpos, data.Rotation, factionHQ, uniqueName);

                    if (unit == null)
                    {
                        Log.LogWarning($"[EditorPlus] Failed to spawn {data.Type}");
                        continue;
                    }

                    Log.LogInfo("[EditorPlus] Unit spawned, registering...");
                    Physics.SyncTransforms();

                    // Store desired position for no clip before RegisterNewUnit
                    if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
                    {
                        NoClipPositionStore.StorePosition(unit, gpos);
                    }

                    SavedUnit savedUnit = editor.RegisterNewUnit(unit, uniqueName);
                    Log.LogInfo($"[EditorPlus] Registered, savedUnit={savedUnit != null}");

                    if (data.SourceSaved != null && savedUnit != null)
                    {
                        NuclearOption.MissionEditorScripts.UnitCopyPaste.CopyPaste(
                            mission, data.SourceSaved, unit, savedUnit);
                        
                        // Preserve unique name after CopyPaste (it may have copied the source name)
                        PreserveUniqueName(savedUnit, uniqueName);
                        
                        // Re-set position after CopyPaste to prevent any side effects
                        // This is critical for no clip - CopyPaste might have clamped it
                        savedUnit.globalPosition = gpos;
                        savedUnit.rotation = data.Rotation;
                        
                        // Always move the scene object to the pasted pose before terrain clamp (not only no-clip).
                        ApplyTransformFromGlobalPose(unit, gpos, data.Rotation, originOffset);
                        Log.LogInfo($"[EditorPlus] Pose after CopyPaste: global={gpos.AsVector3()} local={unit.transform.position}");

                        // Apply aircraft livery visually (CopyPaste only sets SavedAircraft data)
                        if (unit is Aircraft aircraft
                            && data.SourceSaved is SavedAircraft srcAc
                            && savedUnit is SavedAircraft dstAc)
                        {
                            dstAc.liveryKey = srcAc.liveryKey;
                            aircraft.SetLiveryKey(dstAc.liveryKey);
                            Log.LogInfo($"[EditorPlus] Livery applied: {dstAc.liveryKey}");
                        }

                        // Apply hold position if enabled
                        if (Plugin.Instance != null && Plugin.Instance.holdpos)
                        {
                            Patches.HoldPositionHelper.ApplyToSavedUnit(savedUnit, true);
                        }

                        Log.LogInfo($"[EditorPlus] Properties copied, pos re-set to {gpos.AsVector3()}");
                    }

                    // Clamp position to terrain — same as game's ClampPosition
                    // Uses layer 64 (terrain only) so structures aren't detected
                    // Only clamp if ignoreTerrain is false
                    if (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain)
                    {
                        if (formationClampEntries != null && unit != null && savedUnit != null)
                            formationClampEntries.Add((unit, savedUnit, definition));
                        else
                            ClampUnitToTerrain(unit, savedUnit, definition, originOffset);
                    }
                    else
                    {
                        // No clip enabled: ensure position is set correctly and not clamped
                        // The RegisterNewUnit patch will handle re-applying position after registration
                        // But we also apply it immediately here and via coroutine for extra safety
                        if (savedUnit != null && unit != null)
                        {
                            // Apply immediately
                            ApplyNoClipPositionImmediate(unit, savedUnit, gpos, data.Rotation, originOffset);
                            
                            // Also apply via coroutine after a frame (double safety)
                            if (Plugin.Instance != null)
                            {
                                Plugin.Instance.StartCoroutine(ApplyNoClipPosition(unit, savedUnit, gpos, data.Rotation, originOffset));
                            }
                        }
                    }

                    spawnedUnits.Add(unit);
                }
                catch (Exception ex)
                {
                    Log.LogError($"[EditorPlus] Error spawning {data.Type}: {ex}");
                }
            }

            if (formationClampEntries != null && formationClampEntries.Count > 0)
            {
                if (formationClampEntries.Count > 1)
                    ApplyFormationTerrainClamp(formationClampEntries, originOffset);
                else
                {
                    var e = formationClampEntries[0];
                    ClampUnitToTerrain(e.unit, e.savedUnit, e.def, originOffset);
                }
            }

            if (spawnedUnits.Count > 0)
            {
                // Use GroupFollowers to set mass selection
                var groupFollowers = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
                if (groupFollowers != null)
                {
                    Unit primary = spawnedUnits[0];
                    groupFollowers.SetGroup(spawnedUnits, primary);
                    groupFollowers.TryVanillaSelectPrimary(primary);
                    Log.LogInfo($"[EditorPlus] Pasted {spawnedUnits.Count} units, added to GroupFollowers");
                }
                else
                {
                    // Fallback to vanilla selection
                    var selectables = new List<IEditorSelectable>();
                    foreach (var u in spawnedUnits)
                        selectables.Add(u);

                    try
                    {
                        SceneSingleton<UnitSelection>.i?.ReplaceSelection(selectables);
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"[EditorPlus] Error selecting: {ex.Message}");
                    }
                }

                // Clipboard is kept so Ctrl+V can paste again (standard behavior; debounce + _isPasting limit spam)
            }

            Log.LogInfo($"[EditorPlus] Pasted {spawnedUnits.Count} units");
        }

        /// <summary>
        /// Async version of SpawnGroup that spreads spawning across multiple frames to maintain FPS
        /// </summary>
        private static IEnumerator SpawnGroupAsync(Vector3 pasteAnchorWorld, Vector3 originOffset)
        {
            // Set paste flag to prevent multiple simultaneous pastes
            CopyPasteInputHandler.SetPasting(true);
            
            try
            {
                Log.LogInfo("[EditorPlus] SpawnGroupAsync started - spawning units across multiple frames");

                var editor = SceneSingleton<MissionEditor>.i;
                var spawner = NetworkSceneSingleton<Spawner>.i;

                if (editor == null || spawner == null)
                {
                    Log.LogWarning("[EditorPlus] Editor or Spawner not available");
                    yield break;
                }

                var mission = MissionManager.CurrentMission;
                if (mission == null)
                {
                    Log.LogWarning("[EditorPlus] No current mission");
                    yield break;
                }

                GlobalPosition pasteAnchorGlobal = GetPasteAnchorGlobal(pasteAnchorWorld, originOffset);

                var spawnedUnits = new List<Unit>();
                var itemsToSpawn = new List<CopiedUnitData>(GroupClipboard.Items); // Copy list to avoid modification during iteration
                bool deferFormationTerrain =
                    itemsToSpawn.Count > 1
                    && (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain);
                var formationClampEntries = deferFormationTerrain
                    ? new List<(Unit unit, SavedUnit savedUnit, UnitDefinition def)>()
                    : null;
                const int UNITS_PER_FRAME = 15; // Spawn 15 units per frame to maintain FPS
                int spawnedCount = 0;

                foreach (var data in itemsToSpawn)
                {
                    Unit unit = null;
                    SavedUnit savedUnit = null;
                    
                    try
                    {
                        UnitDefinition definition = null;
                        if (data.SourceSaved != null && data.SourceSaved.Unit != null)
                        {
                            definition = data.SourceSaved.Unit.definition;
                        }

                        if (definition == null)
                        {
                            if (!Encyclopedia.Lookup.TryGetValue(data.Type, out definition))
                            {
                                Log.LogWarning($"[EditorPlus] Unknown unit type: {data.Type}, skipping");
                                continue;
                            }
                        }

                        FactionHQ factionHQ = null;
                        if (!string.IsNullOrEmpty(data.Faction))
                        {
                            factionHQ = FactionRegistry.HqFromName(data.Faction);
                        }
                        if (factionHQ == null && data.SourceSaved?.Unit != null)
                        {
                            try { factionHQ = data.SourceSaved.Unit.NetworkHQ; }
                            catch { }
                        }

                        GlobalPosition gpos = OffsetFromPasteAnchor(pasteAnchorGlobal, data.RelativeOffset, definition is ShipDefinition);
                        // Generate a unique name with counter and random component to ensure uniqueness
                        string uniqueName = definition.unitPrefab.name + "_EP" + (++_spawnCounter) + "_" + UnityEngine.Random.Range(1000, 9999);

                        unit = spawner.SpawnFromUnitDefinitionInEditor(
                            definition, gpos, data.Rotation, factionHQ, uniqueName);

                        if (unit == null)
                        {
                            Log.LogWarning($"[EditorPlus] Failed to spawn {data.Type}");
                            continue;
                        }

                        Physics.SyncTransforms();

                        if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
                        {
                            NoClipPositionStore.StorePosition(unit, gpos);
                        }

                        savedUnit = editor.RegisterNewUnit(unit, uniqueName);

                        if (data.SourceSaved != null && savedUnit != null)
                        {
                            NuclearOption.MissionEditorScripts.UnitCopyPaste.CopyPaste(
                                mission, data.SourceSaved, unit, savedUnit);
                            
                            // Preserve unique name after CopyPaste (it may have copied the source name)
                            PreserveUniqueName(savedUnit, uniqueName);
                            
                            savedUnit.globalPosition = gpos;
                            savedUnit.rotation = data.Rotation;
                            
                            ApplyTransformFromGlobalPose(unit, gpos, data.Rotation, originOffset);

                            if (unit is Aircraft aircraft
                                && data.SourceSaved is SavedAircraft srcAc
                                && savedUnit is SavedAircraft dstAc)
                            {
                                dstAc.liveryKey = srcAc.liveryKey;
                                aircraft.SetLiveryKey(dstAc.liveryKey);
                            }

                            if (Plugin.Instance != null && Plugin.Instance.holdpos)
                            {
                                Patches.HoldPositionHelper.ApplyToSavedUnit(savedUnit, true);
                            }
                        }

                        if (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain)
                        {
                            if (formationClampEntries != null && unit != null && savedUnit != null)
                                formationClampEntries.Add((unit, savedUnit, definition));
                            else
                                ClampUnitToTerrain(unit, savedUnit, definition, originOffset);
                        }
                        else
                        {
                            if (savedUnit != null && unit != null)
                            {
                                ApplyNoClipPositionImmediate(unit, savedUnit, gpos, data.Rotation, originOffset);
                                if (Plugin.Instance != null)
                                {
                                    Plugin.Instance.StartCoroutine(ApplyNoClipPosition(unit, savedUnit, gpos, data.Rotation, originOffset));
                                }
                            }
                        }

                        spawnedUnits.Add(unit);
                        spawnedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"[EditorPlus] Error spawning {data.Type}: {ex}");
                    }
                    
                    // Yield every N units to maintain FPS (outside try-catch)
                    if (spawnedCount % UNITS_PER_FRAME == 0)
                    {
                        yield return null; // Wait one frame
                    }
                }

                if (formationClampEntries != null && formationClampEntries.Count > 0)
                {
                    if (formationClampEntries.Count > 1)
                        ApplyFormationTerrainClamp(formationClampEntries, originOffset);
                    else
                    {
                        var e = formationClampEntries[0];
                        ClampUnitToTerrain(e.unit, e.savedUnit, e.def, originOffset);
                    }
                }

                // Final selection and cleanup
                if (spawnedUnits.Count > 0)
                {
                    var groupFollowers = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
                    if (groupFollowers != null)
                    {
                        Unit primary = spawnedUnits[0];
                        groupFollowers.SetGroup(spawnedUnits, primary);
                        groupFollowers.TryVanillaSelectPrimary(primary);
                        Log.LogInfo($"[EditorPlus] Pasted {spawnedUnits.Count} units, added to GroupFollowers");
                    }
                    else
                    {
                        var selectables = new List<IEditorSelectable>();
                        foreach (var u in spawnedUnits)
                            selectables.Add(u);

                        try
                        {
                            SceneSingleton<UnitSelection>.i?.ReplaceSelection(selectables);
                        }
                        catch (Exception ex)
                        {
                            Log.LogError($"[EditorPlus] Error selecting: {ex.Message}");
                        }
                    }

                    // Clipboard kept for repeat paste (see synchronous SpawnGroup)
                }

                Log.LogInfo($"[EditorPlus] Async paste completed: {spawnedUnits.Count} units");
            }
            finally
            {
                // Always clear the paste flag
                CopyPasteInputHandler.SetPasting(false);
            }
        }

        /// <summary>
        /// Replicates the game's UnitSelectionDetails.ClampPosition logic:
        /// Raycast with layer 64 (terrain only, ignores structures/units),
        /// then clamp Y using definition.spawnOffset, minEditorHeight, maxEditorHeight.
        /// </summary>
        /// <summary>
        /// Sync Unity transform from intended global pose. CopyPaste copies source unit state and can leave
        /// the transform at the old location while savedUnit.globalPosition is correct — ClampUnitToTerrain
        /// must run on the pasted pose or the group scatters / keeps source positions.
        /// </summary>
        private static void ApplyTransformFromGlobalPose(Unit unit, GlobalPosition gpos, Quaternion rotation, Vector3 originOffset)
        {
            if (unit == null) return;
            // Match vanilla / GroupFollowers: GlobalPosition -> Unity world is ToLocalPosition(), not AsVector3()-offset
            Vector3 localPos;
            try
            {
                localPos = gpos.ToLocalPosition();
            }
            catch
            {
                localPos = gpos.AsVector3() - originOffset;
            }
            unit.transform.position = localPos;
            unit.transform.rotation = rotation;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Raycast straight down from above a point to terrain only; used when the mouse ray hit a building/unit.
        /// </summary>
        private static bool TrySnapLocalYToTerrain(Vector3 worldPoint, int terrainLayerMask, out Vector3 snapped)
        {
            snapped = worldPoint;
            float startY = Mathf.Max(worldPoint.y, Datum.LocalSeaY) + 10000f;
            var origin = new Vector3(worldPoint.x, startY, worldPoint.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit tHit, 25000f, terrainLayerMask)
                && tHit.collider != null
                && tHit.collider.GetComponentInParent<Unit>() == null)
            {
                snapped = new Vector3(worldPoint.x, tHit.point.y, worldPoint.z);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Multi-unit paste: per-unit terrain clamp uses a different ground reference under each footprint,
        /// which warps relative Y on slopes. Apply one vertical shift to the whole group so every unit stays
        /// inside its editor height band while preserving relative heights (same delta Y between units).
        /// </summary>
        private static void ApplyFormationTerrainClamp(
            List<(Unit unit, SavedUnit savedUnit, UnitDefinition def)> entries,
            Vector3 originOffset)
        {
            if (entries == null || entries.Count < 2) return;

            var yIntended = new List<float>(entries.Count);
            var lo = new List<float>(entries.Count);
            var hi = new List<float>(entries.Count);

            foreach (var (unit, savedUnit, def) in entries)
            {
                if (unit == null || savedUnit == null || def == null)
                {
                    foreach (var e in entries)
                        ClampUnitToTerrain(e.unit, e.savedUnit, e.def, originOffset);
                    return;
                }

                Vector3 p = unit.transform.position;
                if (!TryGetHighestTerrainY(p, out float rawTerrainY))
                {
                    foreach (var e in entries)
                        ClampUnitToTerrain(e.unit, e.savedUnit, e.def, originOffset);
                    return;
                }

                float terrainY = rawTerrainY + def.spawnOffset.y;
                yIntended.Add(p.y);
                lo.Add(terrainY + def.minEditorHeight);
                hi.Add(terrainY + def.maxEditorHeight);
            }

            float sMin = lo[0] - yIntended[0];
            float sMax = hi[0] - yIntended[0];
            for (int i = 1; i < entries.Count; i++)
            {
                sMin = Mathf.Max(sMin, lo[i] - yIntended[i]);
                sMax = Mathf.Min(sMax, hi[i] - yIntended[i]);
            }

            if (sMin > sMax)
            {
                Log.LogInfo("[EditorPlus] Formation terrain: no single vertical shift fits all units; using per-unit clamp");
                foreach (var e in entries)
                    ClampUnitToTerrain(e.unit, e.savedUnit, e.def, originOffset);
                return;
            }

            float s = Mathf.Clamp(0f, sMin, sMax);
            bool moved = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var (unit, savedUnit, _) = entries[i];
                float newY = yIntended[i] + s;
                Vector3 lp = unit.transform.position;
                if (Mathf.Abs(newY - lp.y) <= 0.01f)
                    continue;

                unit.transform.position = new Vector3(lp.x, newY, lp.z);
                moved = true;

                try
                {
                    savedUnit.globalPosition = unit.transform.position.ToGlobalPosition();
                }
                catch
                {
                    Vector3 gv = savedUnit.globalPosition.AsVector3();
                    gv.y = newY + originOffset.y;
                    savedUnit.globalPosition = new GlobalPosition(gv);
                }
            }

            if (moved)
                Physics.SyncTransforms();

            Log.LogInfo($"[EditorPlus] Formation terrain clamp: uniform shift {s:F2}m for {entries.Count} units (relative heights preserved)");
        }

        /// <summary>Layer 64 terrain raycast; highest hit excluding Unit colliders.</summary>
        private static bool TryGetHighestTerrainY(Vector3 localPos, out float highestY)
        {
            highestY = float.MinValue;
            const int TerrainOnlyLayerMask = 64;
            RaycastHit[] hits = Physics.RaycastAll(
                localPos + Vector3.up * 10000f, Vector3.down, 20000f, TerrainOnlyLayerMask);

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.GetComponentInParent<Unit>() == null
                    && hits[i].point.y > highestY)
                {
                    highestY = hits[i].point.y;
                }
            }

            return highestY > float.MinValue;
        }

        private static void ClampUnitToTerrain(Unit unit, SavedUnit savedUnit,
            UnitDefinition definition, Vector3 originOffset)
        {
            if (unit == null || savedUnit == null || definition == null) return;

            Vector3 localPos = unit.transform.position;

            if (!TryGetHighestTerrainY(localPos, out float highestY))
                return;

            // ClampPosition formula
            float terrainY = highestY + definition.spawnOffset.y;
            float minY = terrainY + definition.minEditorHeight;
            float maxY = terrainY + definition.maxEditorHeight;
            float clampedY = Mathf.Clamp(localPos.y, minY, maxY);

            if (Mathf.Abs(clampedY - localPos.y) > 0.01f)
            {
                unit.transform.position = new Vector3(localPos.x, clampedY, localPos.z);
                Physics.SyncTransforms();

                try
                {
                    savedUnit.globalPosition = unit.transform.position.ToGlobalPosition();
                }
                catch
                {
                    Vector3 globalVec = savedUnit.globalPosition.AsVector3();
                    globalVec.y = clampedY + originOffset.y;
                    savedUnit.globalPosition = new GlobalPosition(globalVec);
                }

                Log.LogInfo($"[EditorPlus] Terrain clamped localY: {localPos.y:F1} -> {clampedY:F1}");
            }
        }

        private static IEnumerator ApplyNoClipPosition(Unit unit, SavedUnit savedUnit, GlobalPosition gpos, Quaternion rotation, Vector3 originOffset)
        {
            // Wait a frame to ensure registration and any clamping has completed
            yield return null;
            
            if (unit == null || savedUnit == null || Plugin.Instance == null || !Plugin.Instance.ignoreTerrain) yield break;
            
            // Apply position
            ApplyNoClipPositionImmediate(unit, savedUnit, gpos, rotation, originOffset);
            
            // Wait another frame and apply again to ensure it sticks
            yield return null;
            
            if (unit == null || savedUnit == null || Plugin.Instance == null || !Plugin.Instance.ignoreTerrain) yield break;
            
            ApplyNoClipPositionImmediate(unit, savedUnit, gpos, rotation, originOffset);
            Log.LogInfo("[EditorPlus] No clip: Applied position again after second frame");
        }

        private static void ApplyNoClipPositionImmediate(Unit unit, SavedUnit savedUnit, GlobalPosition gpos, Quaternion rotation, Vector3 originOffset)
        {
            if (unit == null || savedUnit == null) return;

            Vector3 globalVec = gpos.AsVector3();
            Vector3 localPos;
            try
            {
                localPos = gpos.ToLocalPosition();
            }
            catch
            {
                localPos = globalVec - originOffset;
            }
            
            // Set position via wrapper if available (more reliable)
            try
            {
                var posWrapper = savedUnit.PositionWrapper;
                if (posWrapper != null)
                {
                    posWrapper.SetValue(gpos, null, true);
                }
            }
            catch
            {
                try
                {
                    var posWrapper = savedUnit.PositionWrapper;
                    if (posWrapper != null)
                    {
                        posWrapper.SetValue(gpos, null, false);
                    }
                }
                catch { }
            }
            
            // Also set transform directly to ensure it's applied
            unit.transform.position = localPos;
            unit.transform.rotation = rotation;
            savedUnit.globalPosition = gpos;
            savedUnit.rotation = rotation;
            Physics.SyncTransforms();
            
            Log.LogInfo($"[EditorPlus] No clip: position set to local={localPos}, global={globalVec}");
        }

        /// <summary>
        /// Preserves the unique name on a SavedUnit after CopyPaste may have overwritten it.
        /// Uses reflection to set the uniqueName property/field.
        /// </summary>
        private static void PreserveUniqueName(SavedUnit savedUnit, string uniqueName)
        {
            if (savedUnit == null || string.IsNullOrEmpty(uniqueName))
                return;

            try
            {
                Type savedUnitType = savedUnit.GetType();
                
                // Try property first (most common)
                PropertyInfo prop = savedUnitType.GetProperty("uniqueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(savedUnit, uniqueName);
                    Log.LogInfo($"[EditorPlus] Preserved unique name via property: {uniqueName}");
                    return;
                }
                
                // Try field if property not found
                FieldInfo field = savedUnitType.GetField("uniqueName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    field.SetValue(savedUnit, uniqueName);
                    Log.LogInfo($"[EditorPlus] Preserved unique name via field: {uniqueName}");
                    return;
                }
                
                // Try with capital U (UniqueName)
                prop = savedUnitType.GetProperty("UniqueName", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(savedUnit, uniqueName);
                    Log.LogInfo($"[EditorPlus] Preserved unique name via UniqueName property: {uniqueName}");
                    return;
                }
                
                field = savedUnitType.GetField("UniqueName", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(savedUnit, uniqueName);
                    Log.LogInfo($"[EditorPlus] Preserved unique name via UniqueName field: {uniqueName}");
                    return;
                }
                
                Log.LogWarning($"[EditorPlus] Could not find uniqueName property/field on SavedUnit type {savedUnitType.Name}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[EditorPlus] Error preserving unique name: {ex.Message}");
            }
        }
    }
}
