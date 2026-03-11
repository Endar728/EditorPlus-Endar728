using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using NuclearOption.MissionEditorScripts;

namespace EditorPlus
{
    /// <summary>
    /// Handles copy-paste input (Ctrl+C, Ctrl+V, Ctrl+D) for EditorPlus
    /// </summary>
    internal class CopyPasteInputHandler : MonoBehaviour
    {
        private static CopyPasteInputHandler _instance;
        private float _lastCopyTime = -1f;
        private float _lastPasteTime = -1f;
        private float _lastDuplicateTime = -1f;
        private const float DEBOUNCE_TIME = 0.1f; // 100ms debounce to prevent multiple triggers (reduced from 200ms)
        private static bool _isPasting = false; // Track if paste is in progress

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[EditorPlus_CopyPasteHandler]");
            _instance = go.AddComponent<CopyPasteInputHandler>();
            DontDestroyOnLoad(go);
            // Set execution order to run early (negative = earlier)
            // This helps ensure our input handler runs before other mods
            Plugin.Logger?.LogInfo("[EditorPlus] CopyPasteInputHandler created");
            
            // Disable UnitCopyPaste's handler if it exists to prevent conflicts
            DisableUnitCopyPasteHandler();
        }

        void Awake()
        {
            // Try to ensure this runs early by setting script execution order
            // Unity processes Update() in script load order, so being loaded first helps
        }

        void OnEnable()
        {
            // Disable UnitCopyPaste handler whenever we're enabled
            DisableUnitCopyPasteHandler();
        }

        private static void DisableUnitCopyPasteHandler()
        {
            try
            {
                // Find UnitCopyPaste's handler GameObject
                var ucpHandler = GameObject.Find("[UnitCopyPasteHandler]");
                if (ucpHandler != null)
                {
                    // Disable the GameObject to prevent it from handling input
                    ucpHandler.SetActive(false);
                    Plugin.Logger?.LogInfo("[EditorPlus] Disabled UnitCopyPaste handler to prevent conflicts");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger?.LogWarning($"[EditorPlus] Failed to disable UnitCopyPaste handler: {ex.Message}");
            }
        }

        void Update()
        {
            // Only handle input when in mission editor
            if (SceneSingleton<MissionEditor>.i == null) return;
            
            // Periodically check and disable UnitCopyPaste handler (in case it gets re-enabled)
            // Do this every 60 frames to avoid performance impact
            if (Time.frameCount % 60 == 0)
            {
                DisableUnitCopyPasteHandler();
            }

            // Check if we're in an input field - do this FIRST before any other checks
            // This prevents unit copy-paste from interfering with text copy-paste
            if (IsInsideInputField())
            {
                // Don't handle any unit copy-paste when user is typing
                return;
            }

            // Handle Delete key for mass deletion (no Ctrl needed)
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                DeleteSelectedUnits();
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            float currentTime = Time.time;

            // Use EarlyUpdate to handle input before other mods
            // Check for key presses and handle immediately
            // Add debouncing to prevent multiple rapid triggers
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (currentTime - _lastCopyTime < DEBOUNCE_TIME)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] Copy debounced - too soon after last copy");
                    return;
                }
                _lastCopyTime = currentTime;
                Plugin.Logger?.LogInfo("[EditorPlus] Ctrl+C pressed - Copying selection");
                GroupCopyPaste.CopySelectedGroup();
                // Reset input to prevent other mods from handling it
                Input.ResetInputAxes();
                return; // Exit early to prevent other handlers
            }
            
            if (Input.GetKeyDown(KeyCode.V))
            {
                // Don't allow paste if one is already in progress
                if (_isPasting)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] Paste already in progress, ignoring");
                    return;
                }
                if (currentTime - _lastPasteTime < DEBOUNCE_TIME)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] Paste debounced - too soon after last paste");
                    return;
                }
                _lastPasteTime = currentTime;
                Plugin.Logger?.LogInfo("[EditorPlus] Ctrl+V pressed - Pasting at cursor");
                GroupCopyPaste.PasteGroupAtCursor();
                Input.ResetInputAxes();
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (currentTime - _lastDuplicateTime < DEBOUNCE_TIME)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] Duplicate debounced - too soon after last duplicate");
                    return;
                }
                _lastDuplicateTime = currentTime;
                Plugin.Logger?.LogInfo("[EditorPlus] Ctrl+D pressed - Duplicating in place");
                GroupCopyPaste.DuplicateInPlace();
                Input.ResetInputAxes();
                return;
            }
        }

        private static void DeleteSelectedUnits()
        {
            var editor = SceneSingleton<MissionEditor>.i;
            if (editor == null) return;

            // Try to get selection from GroupFollowers first (mass selection)
            var groupFollowers = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
            var unitsToDelete = new List<Unit>();

            if (groupFollowers != null && groupFollowers.CurrentUnits != null && groupFollowers.CurrentUnits.Count > 0)
            {
                // Use mass selection from EditorPlus
                unitsToDelete.AddRange(groupFollowers.CurrentUnits);
                Plugin.Logger?.LogInfo($"[EditorPlus] Delete: Using GroupFollowers selection: {unitsToDelete.Count} units");
            }
            else
            {
                // Fallback to vanilla selection
                var selection = SceneSingleton<UnitSelection>.i;
                if (selection?.SelectionDetails != null)
                {
                    var details = selection.SelectionDetails;
                    if (details is MultiSelectSelectionDetails multi)
                    {
                        foreach (var item in multi.Items)
                        {
                            if (item is UnitSelectionDetails usd && usd.Unit != null)
                                unitsToDelete.Add(usd.Unit);
                        }
                    }
                    else if (details is UnitSelectionDetails single && single.Unit != null)
                    {
                        unitsToDelete.Add(single.Unit);
                    }
                }
            }

            if (unitsToDelete.Count == 0)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] Delete: No units selected");
                return;
            }

            // Delete all selected units
            int deletedCount = 0;
            foreach (var unit in unitsToDelete)
            {
                if (unit != null)
                {
                    editor.RemoveUnit(unit);
                    deletedCount++;
                }
            }

            // Clear selection after deletion
            if (groupFollowers != null)
            {
                groupFollowers.ClearGroupAndTryVanillaDeselect();
            }
            else
            {
                SceneSingleton<UnitSelection>.i?.ClearSelection();
            }

            Plugin.Logger?.LogInfo($"[EditorPlus] Deleted {deletedCount} unit(s)");
        }

        private static bool IsInsideInputField()
        {
            // More robust check for input fields
            // This prevents unit copy-paste from interfering with text copy-paste operations
            
            // Method 1: Check if mouse is over an input field
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // Use raycast to find what UI element the mouse is over
                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                
                foreach (var result in results)
                {
                    if (result.gameObject != null)
                    {
                        // Check for InputField components
                        var inputField = result.gameObject.GetComponent<UnityEngine.UI.InputField>();
                        if (inputField != null) return true;

                        var tmpInput = result.gameObject.GetComponent<TMPro.TMP_InputField>();
                        if (tmpInput != null) return true;
                        
                        // Also check parent objects (input fields are often nested)
                        var parent = result.gameObject.transform.parent;
                        while (parent != null)
                        {
                            inputField = parent.GetComponent<UnityEngine.UI.InputField>();
                            if (inputField != null) return true;
                            
                            tmpInput = parent.GetComponent<TMPro.TMP_InputField>();
                            if (tmpInput != null) return true;
                            
                            parent = parent.parent;
                        }
                    }
                }
            }
            
            // Method 2: Check if any input field is currently focused
            if (IsAnyInputFieldFocused())
            {
                return true;
            }
            
            return false;
        }

        private static bool IsAnyInputFieldFocused()
        {
            // Check the currently selected GameObject
            if (EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null)
                {
                    var inputField = selected.GetComponent<UnityEngine.UI.InputField>();
                    if (inputField != null && inputField.isFocused) return true;

                    var tmpInput = selected.GetComponent<TMPro.TMP_InputField>();
                    if (tmpInput != null && tmpInput.isFocused) return true;
                }
            }
            
            // Also check all InputFields in the scene to see if any are focused
            // This is a fallback in case EventSystem doesn't track it properly
            try
            {
                var allInputFields = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.InputField>();
                foreach (var field in allInputFields)
                {
                    if (field != null && field.isFocused) return true;
                }
                
                var allTMPInputs = UnityEngine.Object.FindObjectsOfType<TMPro.TMP_InputField>();
                foreach (var field in allTMPInputs)
                {
                    if (field != null && field.isFocused) return true;
                }
            }
            catch
            {
                // If FindObjectsOfType fails, just continue
            }
            
            return false;
        }

        /// <summary>
        /// Set the paste-in-progress flag (called from GroupCopyPaste)
        /// </summary>
        public static void SetPasting(bool isPasting)
        {
            _isPasting = isPasting;
        }
    }
}
