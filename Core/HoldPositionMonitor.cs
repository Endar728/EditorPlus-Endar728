using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus
{
    /// <summary>
    /// Continuously monitors all units in the mission and applies hold position
    /// to any that don't have it set when holdpos is enabled.
    /// This is a catch-all approach that works regardless of how units are placed.
    /// Checks in Update, LateUpdate, and FixedUpdate for maximum coverage.
    /// </summary>
    internal class HoldPositionContinuousMonitor : MonoBehaviour
    {
        private static HoldPositionContinuousMonitor _instance;
        private int _frameCounter = 0;
        private HashSet<SavedUnit> _processedUnits = new HashSet<SavedUnit>();

        public static void EnsureExists()
        {
            if (_instance != null)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] HoldPositionContinuousMonitor already exists");
                return;
            }

            var go = new GameObject("[EditorPlus_HoldPositionMonitor]");
            _instance = go.AddComponent<HoldPositionContinuousMonitor>();
            DontDestroyOnLoad(go);
            Plugin.Logger?.LogInfo("[EditorPlus] HoldPositionContinuousMonitor created and will monitor all units");
        }

        void CheckAndApplyHoldPosition()
        {
            // Only monitor when in mission editor
            if (SceneSingleton<MissionEditor>.i == null)
            {
                _processedUnits.Clear(); // Clear when not in editor
                return;
            }
            
            if (Plugin.Instance == null || !Plugin.Instance.holdpos)
            {
                _processedUnits.Clear(); // Clear when holdpos is disabled
                return;
            }

            var mission = MissionManager.CurrentMission;
            if (mission == null)
            {
                _processedUnits.Clear(); // Clear if mission is null
                return;
            }
            
            // Check EVERY frame when holdpos is enabled to catch units immediately
            // This is critical for "place more" where units are placed rapidly
            try
            {
                var savedUnitsField = mission.GetType().GetField("savedUnits", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (savedUnitsField == null) return;

                var savedUnits = savedUnitsField.GetValue(mission);
                if (savedUnits is IEnumerable enumerable)
                {
                    int appliedCount = 0;
                    int checkedCount = 0;
                    int resetCount = 0;
                    
                    foreach (var item in enumerable)
                    {
                        if (item is SavedUnit su && su != null)
                        {
                            checkedCount++;
                            // Read the actual underlying value (bypasses getter patch)
                            bool currentValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                            
                            // ALWAYS apply hold position if it's false
                            // This ensures we catch units that have their hold position reset
                            if (!currentValue)
                            {
                                bool wasProcessed = _processedUnits.Contains(su);
                                Patches.HoldPositionHelper.ApplyToSavedUnit(su, true);
                                // Verify it was applied (read field directly again)
                                bool afterValue = Patches.HoldPositionHelper.GetHoldPosition(su);
                                
                                if (!afterValue)
                                {
                                    // If it still failed, try again more aggressively
                                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionContinuousMonitor: Hold position failed to apply to {su.type}, retrying...");
                                    // Try direct field access
                                    var field = su.GetType().GetField("holdPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (field != null)
                                    {
                                        field.SetValue(su, true);
                                        afterValue = (bool)field.GetValue(su);
                                    }
                                }
                                
                                // Log if this is a new unit or if it was reset after being processed
                                if (!wasProcessed)
                                {
                                    Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionContinuousMonitor: Applied hold position to NEW unit {su.type}, before={currentValue}, after={afterValue}");
                                    appliedCount++;
                                }
                                else if (!afterValue)
                                {
                                    Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionContinuousMonitor: Hold position was RESET on {su.type}! before={currentValue}, after={afterValue}");
                                    resetCount++;
                                    appliedCount++;
                                }
                                
                                // Always track it, even if we've seen it before
                                _processedUnits.Add(su);
                            }
                            else
                            {
                                // Track units that already have hold position set
                                _processedUnits.Add(su);
                            }
                        }
                    }

                    // Log summary every 60 frames (about once per second) to reduce spam
                    _frameCounter++;
                    if (_frameCounter % 60 == 0 && checkedCount > 0)
                    {
                        if (appliedCount > 0 || resetCount > 0)
                        {
                            Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionContinuousMonitor: Checked {checkedCount} unit(s), applied to {appliedCount} ({resetCount} resets), this second. Running.");
                        }
                    }
                    
                    // Log every 300 frames (about every 5 seconds) to confirm monitor is running
                    if (_frameCounter % 300 == 0)
                    {
                        Plugin.Logger?.LogInfo($"[EditorPlus] HoldPositionContinuousMonitor: Running (frame {_frameCounter}), tracking {_processedUnits.Count} unit(s)");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] HoldPositionContinuousMonitor: Error checking units: {ex.Message}");
            }
        }

        void Update()
        {
            CheckAndApplyHoldPosition();
        }

        void LateUpdate()
        {
            // Also check in LateUpdate to catch units that might be placed after Update
            // This is especially important for "place more" where units might be registered late
            CheckAndApplyHoldPosition();
        }

        void FixedUpdate()
        {
            // Also check in FixedUpdate for maximum coverage
            // This ensures we catch units regardless of when they're placed in the frame
            CheckAndApplyHoldPosition();
        }
        
        void OnEnable()
        {
            Plugin.Logger?.LogInfo("[EditorPlus] HoldPositionContinuousMonitor enabled");
        }
        
        void OnDisable()
        {
            Plugin.Logger?.LogWarning("[EditorPlus] HoldPositionContinuousMonitor disabled - this should not happen!");
        }

        void OnDestroy()
        {
            _processedUnits.Clear();
        }
    }
}
