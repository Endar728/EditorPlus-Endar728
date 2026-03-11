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

            // Try to get selection from GroupFollowers first (mass selection)
            var groupFollowers = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
            
            if (groupFollowers != null && groupFollowers.CurrentUnits != null && groupFollowers.CurrentUnits.Count > 0)
            {
                // Use mass selection from EditorPlus
                var unitsToCopy = groupFollowers.CurrentUnits;
                Log.LogInfo($"[EditorPlus] Using GroupFollowers selection: {unitsToCopy.Count} units");

                // Calculate group center using GlobalPosition (not local transform)
                Vector3 center = Vector3.zero;
                int validCount = 0;
                foreach (var unit in unitsToCopy)
                {
                    if (unit?.SavedUnit != null)
                    {
                        center += unit.SavedUnit.globalPosition.AsVector3();
                        validCount++;
                    }
                }
                if (validCount == 0)
                {
                    Log.LogWarning("[EditorPlus] No valid units with SavedUnit");
                    return;
                }
                center /= validCount;

                // Store the original center Y for height preservation
                GroupClipboard.OriginalCenterY = center.y;

                // Store clipboard data with global position offsets - clear first to prevent accumulation
                GroupClipboard.Clear();
                GroupClipboard.OriginalCenterY = center.y; // Restore after clear
                foreach (var unit in unitsToCopy)
                {
                    if (unit?.SavedUnit == null) continue;
                    
                    GroupClipboard.Items.Add(new CopiedUnitData
                    {
                        Type = unit.SavedUnit.type,
                        Faction = unit.SavedUnit.faction,
                        RelativeOffset = unit.SavedUnit.globalPosition.AsVector3() - center,
                        Rotation = unit.SavedUnit.rotation,
                        SourceSaved = unit.SavedUnit
                    });
                }
            }
            else
            {
                // Fallback to vanilla selection
                var details = selection.SelectionDetails;
                if (details == null)
                {
                    Log.LogWarning("[EditorPlus] Nothing selected");
                    return;
                }

                var unitDetails = new List<UnitSelectionDetails>();

                if (details is MultiSelectSelectionDetails multi)
                {
                    foreach (var item in multi.Items)
                    {
                        if (item is UnitSelectionDetails usd)
                            unitDetails.Add(usd);
                    }
                }
                else if (details is UnitSelectionDetails single)
                {
                    unitDetails.Add(single);
                }

                if (unitDetails.Count == 0)
                {
                    Log.LogWarning("[EditorPlus] No units in selection");
                    return;
                }

                // Calculate group center using GlobalPosition (not local transform)
                Vector3 center = Vector3.zero;
                foreach (var ud in unitDetails)
                {
                    if (ud?.SavedUnit != null)
                        center += ud.SavedUnit.globalPosition.AsVector3();
                }
                center /= unitDetails.Count;

                // Store the original center Y for height preservation
                GroupClipboard.OriginalCenterY = center.y;

                // Store clipboard data with global position offsets
                GroupClipboard.Clear();
                GroupClipboard.OriginalCenterY = center.y; // Restore after clear
                foreach (var ud in unitDetails)
                {
                    if (ud?.SavedUnit == null) continue;
                    
                    GroupClipboard.Items.Add(new CopiedUnitData
                    {
                        Type = ud.SavedUnit.type,
                        Faction = ud.SavedUnit.faction,
                        RelativeOffset = ud.SavedUnit.globalPosition.AsVector3() - center,
                        Rotation = ud.SavedUnit.rotation,
                        SourceSaved = ud.SavedUnit
                    });
                }
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

            Vector3 hitPointLocal;
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                hitPointLocal = hit.point;
            }
            else
            {
                // No terrain hit — try water plane for over-sea pasting
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

            // Calculate paste center in global coordinates
            // Use X and Z from hit point, but preserve original Y height
            Vector3 pasteCenterGlobal = hitPointLocal + originOffset;
            
            // Preserve the original center Y height instead of using ground level
            // This ensures units paste at their original height, not sunk into the ground
            // OriginalCenterY is already in global coordinates, so use it directly
            if (GroupClipboard.OriginalCenterY != 0f)
            {
                // Use X and Z from the hit point, but Y from the original center (already global)
                pasteCenterGlobal.x = hitPointLocal.x + originOffset.x;
                pasteCenterGlobal.z = hitPointLocal.z + originOffset.z;
                pasteCenterGlobal.y = GroupClipboard.OriginalCenterY; // Already in global coordinates
                Log.LogInfo($"[EditorPlus] Preserving original Y height: originalCenterY={GroupClipboard.OriginalCenterY}, pasteY={pasteCenterGlobal.y}");
            }
            
            Log.LogInfo($"[EditorPlus] Paste: hitLocal={hitPointLocal} offset={originOffset} pasteCenter={pasteCenterGlobal}");
            
            // Start async paste to avoid freezing with large groups
            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(SpawnGroupAsync(pasteCenterGlobal, originOffset));
            }
            else
            {
                // Fallback to synchronous if no plugin instance
                SpawnGroup(pasteCenterGlobal, originOffset);
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

            Vector3 originalCenter = globalPos - first.RelativeOffset;
            Vector3 offset = new Vector3(10f, 0f, 10f);
            
            Log.LogInfo($"[EditorPlus] DuplicateInPlace: Spawning {GroupClipboard.Items.Count} unit(s) at offset {offset}");
            SpawnGroup(originalCenter + offset, originOffset);
        }

        private static void SpawnGroup(Vector3 pasteCenter, Vector3 originOffset)
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

            var spawnedUnits = new List<Unit>();

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

                    Vector3 worldPos = pasteCenter + data.RelativeOffset;

                    // Ships: raise 35m above so they drop onto water surface
                    if (definition is ShipDefinition)
                    {
                        worldPos.y += 35f;
                        Log.LogInfo("[EditorPlus] Ship detected, raised +35m for water drop");
                    }

                    GlobalPosition gpos = new GlobalPosition(worldPos);

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
                        
                        // If no clip, also update the unit transform immediately
                        if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
                        {
                            Vector3 localPos = (gpos.AsVector3() - originOffset);
                            unit.transform.position = localPos;
                            unit.transform.rotation = data.Rotation;
                            Physics.SyncTransforms();
                            Log.LogInfo($"[EditorPlus] No clip: Position re-applied after CopyPaste - local={localPos}, global={gpos.AsVector3()}");
                        }

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

                        Log.LogInfo($"[EditorPlus] Properties copied, pos re-set to {worldPos}");
                    }

                    // Clamp position to terrain — same as game's ClampPosition
                    // Uses layer 64 (terrain only) so structures aren't detected
                    // Only clamp if ignoreTerrain is false
                    if (Plugin.Instance == null || !Plugin.Instance.ignoreTerrain)
                    {
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

                // Clear clipboard after successful paste to prevent accidental multiple pastes
                GroupClipboard.Clear();
                Log.LogInfo("[EditorPlus] Clipboard cleared after paste");
            }

            Log.LogInfo($"[EditorPlus] Pasted {spawnedUnits.Count} units");
        }

        /// <summary>
        /// Async version of SpawnGroup that spreads spawning across multiple frames to maintain FPS
        /// </summary>
        private static IEnumerator SpawnGroupAsync(Vector3 pasteCenter, Vector3 originOffset)
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

                var spawnedUnits = new List<Unit>();
                var itemsToSpawn = new List<CopiedUnitData>(GroupClipboard.Items); // Copy list to avoid modification during iteration
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

                        Vector3 worldPos = pasteCenter + data.RelativeOffset;

                        if (definition is ShipDefinition)
                        {
                            worldPos.y += 35f;
                        }

                        GlobalPosition gpos = new GlobalPosition(worldPos);
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
                            
                            if (Plugin.Instance != null && Plugin.Instance.ignoreTerrain)
                            {
                                Vector3 localPos = (gpos.AsVector3() - originOffset);
                                unit.transform.position = localPos;
                                unit.transform.rotation = data.Rotation;
                                Physics.SyncTransforms();
                            }

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

                    // Clear clipboard after successful paste
                    GroupClipboard.Clear();
                    Log.LogInfo("[EditorPlus] Clipboard cleared after async paste");
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
        private static void ClampUnitToTerrain(Unit unit, SavedUnit savedUnit,
            UnitDefinition definition, Vector3 originOffset)
        {
            if (unit == null || savedUnit == null || definition == null) return;

            Vector3 localPos = unit.transform.position;

            // FindHighestTerrainPoint: raycast down with layer 64, exclude Unit colliders
            const int terrainLayer = 64;
            RaycastHit[] hits = Physics.RaycastAll(
                localPos + Vector3.up * 10000f, Vector3.down, 20000f, terrainLayer);

            float highestY = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.GetComponentInParent<Unit>() == null
                    && hits[i].point.y > highestY)
                {
                    highestY = hits[i].point.y;
                }
            }

            if (highestY <= float.MinValue) return;

            // ClampPosition formula
            float terrainY = highestY + definition.spawnOffset.y;
            float minY = terrainY + definition.minEditorHeight;
            float maxY = terrainY + definition.maxEditorHeight;
            float clampedY = Mathf.Clamp(localPos.y, minY, maxY);

            if (Mathf.Abs(clampedY - localPos.y) > 0.01f)
            {
                unit.transform.position = new Vector3(localPos.x, clampedY, localPos.z);
                Physics.SyncTransforms();

                // Update saved global position
                Vector3 globalVec = savedUnit.globalPosition.AsVector3();
                globalVec.y = clampedY + originOffset.y;
                savedUnit.globalPosition = new GlobalPosition(globalVec);

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

            // Convert global position to local for transform
            Vector3 globalVec = gpos.AsVector3();
            Vector3 localPos = globalVec - originOffset;
            
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
