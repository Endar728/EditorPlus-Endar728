# EditorPlus - Credits and Features

## Original Creator
**nikkorap** - Created the original EditorPlus mod

## Updated By
**Endar728** - Enhanced EditorPlus with copy-paste functionality, bug fixes, and additional features

---

## Version History

### Version 1.5.0 (Current) - Updated by Endar728
- **Release Date**: 2026-03-10
- **Major Features Added**: Copy-paste functionality, enhanced hold position system, no clip mode
- **Critical Bug Fix**: Fixed copy-paste/duplicate name collision bug

### Version 1.4.2 - Original by nikkorap
- Base EditorPlus functionality
- Node graph UI
- Group selection
- Hold position toggle
- Terrain collision toggle

---

## Complete Feature List

### Original Features (by nikkorap)

#### Node Graph UI
- Visual node graph interface for managing mission objectives and outcomes
- Click and drag on ports to change connections
- Hover over a connection or port and press Delete to remove connections
- Ghost lines from nodes to units, airbases, and waypoints
- Add selected units to a node: open Node UI, hover the node, Shift + LMB

#### Group Unit Selection
- Box selection: `Shift` + `LMB` drag to box select multiple units
- Pivot selection: Click any selected unit to set the pivot point
- Mass operations: Remove and Faction settings apply to all selected units simultaneously
- Group movement: Selected units move together maintaining relative positions and rotations

#### Unit Placement
- Rapid placement: Hold `Ctrl` while placing units for rapid placement
- Hold position toggle: Enable/disable hold position when placing new units (toggle in toolbar)
- Grid snapping: Work in progress (WIP)
- Terrain collision toggle: Toggle to ignore terrain collision (labeled "noclip" in toolbar)
- Position/Rotation switch: Hold `Ctrl` to switch between position and rotation modes
- Height limits removed: Units can be placed at any height

#### Other Features
- Extended dropdowns: All dropdown menus have extended options
- Mission objectives cache: Automatic cache management for mission objectives
- Scene lifecycle management: Proper cleanup on scene unload

---

## New Features Added by Endar728 (v1.4.2 → v1.5.0)

### Copy-Paste Functionality
**Completely new feature system integrated into EditorPlus**

#### Keyboard Shortcuts
- **Ctrl+C**: Copy selected units to clipboard
  - Works with single selection, multi-selection, and EditorPlus mass selection
  - Stores unit type, faction, position, rotation, and all properties
  
- **Ctrl+V**: Paste units at cursor position
  - Raycasts from camera to find paste location
  - Pastes units maintaining their relative positions
  - Automatically selects pasted units for immediate manipulation
  - Preserves original height (units don't sink into ground)
  
- **Ctrl+D**: Duplicate selected units in place
  - Copies selection and pastes nearby (10m offset)
  - Useful for quick duplication

#### Copy-Paste Features
- **Mass selection support**: Works seamlessly with EditorPlus GroupFollowers mass selection
- **Height preservation**: Units paste at their original height, not at ground level
- **Property preservation**: All unit properties are copied including:
  - Unit type and definition
  - Faction assignment
  - Rotation/orientation
  - Aircraft livery (for aircraft)
  - All saved unit data
  
- **Automatic selection**: Pasted units are automatically added to GroupFollowers for mass movement
- **Clipboard management**: Clipboard clears after paste to prevent accidental multiple pastes
- **Terrain handling**: 
  - Ships are raised 35m above paste point to drop onto water surface
  - Terrain clamping respects "noclip" toggle
  - Falls back to water plane if no terrain is found
- **Async paste**: Large groups are pasted asynchronously across multiple frames to maintain FPS
- **Smart input detection**: Unit copy-paste is automatically disabled when typing in text input fields
- **Alternative hotkeys**: Ctrl+Shift+C/V/D as alternative hotkeys that always work
- **Unique name generation**: Each pasted/duplicated unit receives a unique name to prevent mission corruption

### Enhanced Hold Position System
**Significant improvements to existing hold position feature**

- **Continuous monitoring**: Background monitor ensures hold position is maintained even if other code tries to reset it
- **Multiple protection layers**: 
  - Immediate application during unit registration
  - Re-application via coroutines over multiple frames
  - Setter/getter patches that prevent resetting
  - Continuous background monitoring
- **Copy-paste integration**: Hold position is preserved during copy-paste operations
- **Place More support**: Hold position works for units placed via "Place More" (Ctrl+click)
- **Aggressive enforcement**: Multiple layers of protection prevent hold position from being cleared

### No Clip Functionality
**Enhanced terrain ignore mode**

- **Terrain ignore mode**: Units can be placed and moved without terrain clamping
- **Position preservation**: No clip positions are maintained through unit registration and copy-paste operations
- **Multi-frame application**: Positions are re-applied over multiple frames to ensure they stick
- **Clamp prevention**: Patches prevent the game's clamping methods from affecting units when no clip is active
- **Copy-paste support**: No clip positions are preserved when copying and pasting units

### Free Camera Enhancement
**New feature for better camera control**

- **Collision disabled**: Free camera movement without collision constraints
- **Automatic detection**: Automatically finds and disables camera collision
- **Scene-aware**: Re-initializes when GameWorld scene loads

---

## Technical Improvements by Endar728

### Code Architecture
- **Modular design**: Separated concerns into dedicated classes
  - `GroupCopyPaste.cs` - Core copy-paste logic
  - `GroupClipboard.cs` - Clipboard data storage
  - `CopyPasteInputHandler.cs` - Input handling
  - `NoClipPositionStore.cs` - No clip position storage
  - `HoldPositionMonitor.cs` - Hold position monitoring
- **Error handling**: Enhanced error handling throughout with try-catch blocks
- **Logging**: Comprehensive logging for debugging and troubleshooting
- **Reflection utils**: Enhanced reflection utilities for compatibility with different game versions

### Harmony Patches Added
- **RegisterNewUnit Patch**: Applies hold position and no clip positions immediately when units are registered
- **PlaceUnit Patch**: Monitors and applies hold position to "Place More" units
- **StartPlaceUnit Patch**: Additional monitoring for units placed through the placement system
- **UnitCopyPaste Patch**: Preserves hold position during copy-paste operations
- **HoldPositionMonitor Patches**: Prevent hold position from being reset on SavedVehicle and SavedShip
- **ClampPosition Patches**: Prevent terrain clamping when no clip is enabled
- **FreeCameraCollision Patch**: Disables camera collision

### Input Handling
- **Early initialization**: Input handler is created early in plugin lifecycle to ensure priority over other mods
- **Input consumption**: Input is consumed after handling to prevent conflicts with other mods
- **Input field detection**: Copy-paste shortcuts are disabled when typing in UI input fields
- **Debouncing**: Prevents rapid-fire operations with debounce timers

### Compatibility
- **UnitCopyPaste Mod**: Automatically disables conflicting handler to prevent conflicts
- **Reflection-based**: Uses reflection for MissionObjectives access for better compatibility
- **BepInEx 5.x**: Requires BepInEx 5.x or later

---

## Bug Fixes by Endar728

### Critical Bug Fixes
1. **Copy-Paste/Duplicate Name Collision Bug (v1.5.0)**
   - **Problem**: All pasted/duplicated units received the same name, causing mission corruption
   - **Solution**: Added unique name preservation system that restores names after property copying
   - **Impact**: Prevents mission files from becoming corrupted

### Copy-Paste Bug Fixes
2. **Units pasting far from cursor position**
   - Solution: Calculate origin offset using camera's global position instead of source unit

3. **Units pasting into the ground**
   - Solution: Preserve original Y-coordinate height from copied group center

4. **Clipboard not clearing after paste**
   - Solution: Clear clipboard immediately after successful paste operation

5. **Unit copy-paste interfering with text copy-paste in input fields**
   - Solution: Enhanced input field detection with multiple methods (raycast, focus detection, parent traversal)

### Hold Position Bug Fixes
6. **Hold position not working for "Place More" units**
   - Solution: Multiple layers of monitoring and application (immediate, coroutine, continuous)

7. **Hold position being reset after application**
   - Solution: Direct field access via reflection, multiple re-application attempts over multiple frames

### No Clip Bug Fixes
8. **No clip not working for pasted units**
   - Solution: Store desired position before RegisterNewUnit, then re-apply via coroutine

9. **Units being clamped to terrain despite no clip being enabled**
   - Solution: Patch ClampPosition and ClampY methods to respect no clip setting

---

## Files Added by Endar728

### New Source Files (10 files)
1. `GroupCopyPaste.cs` - Core copy-paste logic with support for mass operations
2. `GroupClipboard.cs` - Centralized clipboard system for storing copied unit data
3. `CopyPasteInputHandler.cs` - Handles keyboard input for copy-paste operations
4. `NoClipPositionStore.cs` - Temporary storage for desired positions during no clip operations
5. `Patches/HoldPositionMonitor.cs` - Monitors and prevents hold position from being reset
6. `Core/HoldPositionMonitor.cs` - Continuous background monitor for hold position
7. `Patches/ClampPositionPatch.cs` - Prevents terrain clamping when no clip is enabled
8. `Patches/FreeCameraCollision.cs` - Disables collision for free camera movement
9. `Patches/UnitCopyPastePatch.cs` - Preserves hold position during copy-paste operations
10. `Patches/StartPlaceUnit.cs` - Additional monitoring for units placed through the placement system

### Files Modified by Endar728 (7 files)
1. `Plugin.cs` - Added initialization for new systems, enhanced error handling
2. `Core/SceneSetup.cs` - Enhanced with copy-paste support, patch initialization
3. `Core/ReflectionUtils.cs` - Added `GetMissionObjectives()` method for compatibility
4. `Core/Overlay.cs` - Enhanced hold position integration
5. `Patches/RegisterNewUnit.cs` - Enhanced hold position and no clip support
6. `Patches/PlaceUnit.cs` - Added hold position monitoring
7. `Patches/ClampY.cs` - Enhanced no clip support

---

## Statistics

### Code Changes
- **New Files**: 10
- **Modified Files**: 7
- **Lines of Code Added**: ~2000+ (estimated)
- **New Features**: 4 major feature systems
- **Bug Fixes**: 9 bugs fixed
- **Harmony Patches Added**: 7 new patches

### Feature Breakdown
- **Copy-Paste System**: Complete new feature system with 3 keyboard shortcuts
- **Hold Position Enhancement**: Significant improvements to existing feature
- **No Clip System**: Enhanced terrain ignore functionality
- **Free Camera**: New camera collision disabling feature

---

## Installation

1. Place `com.nikkorap.EditorPlus.dll` in `BepInEx/plugins/`
2. Ensure `editorplus_ui.noep` is in the same folder as the DLL
3. Requires BepInEx 5.x or later
4. Compatible with latest version of Nuclear Option

---

## Usage

### Copy-Paste
- **Copy**: Select units, then press `Ctrl+C`
- **Paste**: Press `Ctrl+V` at desired location (cursor position)
- **Duplicate**: Select units, then press `Ctrl+D`

### Mass Operations
- Use **Shift+LMB** drag for box selection
- All selected units can be moved together
- Faction changes apply to all selected units
- Copy-paste works with mass selections

### Hold Position
- Toggle "Hold Pos" in toolbar before placing new units
- Applies to newly placed units automatically
- Also applies when pasting if toggle is enabled

### No Clip
- Toggle "noclip" in toolbar
- Units can be placed and moved without terrain constraints when enabled
- Works with copy-paste operations

---

## Acknowledgments

- **nikkorap**: Original creator of EditorPlus - thank you for the excellent base mod


## License

This mod builds upon the original EditorPlus by nikkorap. All new features and improvements added by Endar728 are provided as enhancements to the original work.

---

## Support

For issues, bug reports, or feature requests related to the updates by Endar728, please provide:
- BepInEx log file
- Steps to reproduce
- Description of the problem
- Version information (should be 1.5.0)

---

**Last Updated**: 2026-03-07  
**Current Version**: 1.5.0  
**Updated by**: Endar728  
**Original Creator**: nikkorap
