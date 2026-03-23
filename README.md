# EditorPlus

A bepinex mod to enhance the Nuclear Option mission editor experience.

### New UI
* Nodegraph-based objective/outcome UI, click and drag on ports to change connections, hover over a connection or port and press delete to remove connections.
* Ghost lines from nodes to units, airbases, and waypoints.
* Add selected units to a node: open Node UI, hover the node, Shift + LMB.

### Group unit selection
* Group selection: `Shift` + `LMB` drag to box select; click any selected unit to set the pivot.
* Remove and Faction settings apply to all selected units.

### Unit placement
* Rapid placement of units while holding `Ctrl`.
* Toggle to enable hold position when placing new units.
* Grid snapping (WIP).
* Toggle terrain collision.
* hold `Ctrl` to switch between position and rotation.
* Removed height limits.

### Copy-Paste Functionality ⚠️ NEW IN v1.5.0
**This feature was added by Endar728 in v1.5.0 and did NOT exist in the original v1.4.2 by nikkorap.**

* **Mass Copy (Ctrl+C)**: Copy all selected units with their relative positions, rotations, and properties
* **Mass Paste (Ctrl+V)**: Paste copied units at cursor (raycast hit); formation shape kept via relative offsets
* **Duplicate in Place (Ctrl+D)**: Duplicate selected units at their current location with a small offset
* **Paste height**: Anchor uses the surface under the cursor (full global position from hit), not the copied group’s global Y (avoids wrong placement when center Y is map datum / 0)
* **Multi-Unit Support**: Copy and paste entire groups of units while maintaining their spatial relationships
* **Smart Input Detection**: Unit copy-paste is automatically disabled when typing in text input fields, preventing conflicts with text copy-paste operations
* **Unique Name Generation**: Each pasted/duplicated unit receives a unique name to prevent mission corruption (Fixed in v1.5.0)

### Mass Delete ⚠️ NEW IN v1.5.0
**This feature was added by Endar728 in v1.5.0 and did NOT exist in the original v1.4.2 by nikkorap.**

* **Delete Key**: Press Delete to remove all selected units at once
* Works seamlessly with group selection system

### other
* Extended all dropdowns.
* Automatic conflict resolution with UnitCopyPaste mod (disables conflicting handler) ⚠️ NEW IN v1.5.0

---

## Version Information

**Current Version**: 1.5.1  
**Updated by**: Endar728  
**Original Creator**: nikkorap (v1.4.2)

### What's New in v1.5.0 (Updated by Endar728)
- ⚠️ **Copy-Paste Functionality** - Complete new feature system (Ctrl+C/V/D) - **DID NOT EXIST in v1.4.2**
- ⚠️ **Mass Delete** - Delete key support - **DID NOT EXIST in v1.4.2**
- Enhanced hold position system (improvements to existing feature)
- Enhanced no clip mode (improvements to existing feature)
- Free camera collision disabled (new feature)
- Critical bug fix: Copy-paste name collision bug

### Original Features (v1.4.2 by nikkorap)
- Node graph UI
- Group unit selection
- Unit placement features
- Hold position toggle (basic)
- Terrain collision toggle (basic)
- Extended dropdowns

If you encounter any bugs then please report them. Feedback is appreciated!
<img width="960" height="540" alt="image" src="https://github.com/user-attachments/assets/6489d11f-7bdb-4868-85cf-6edbeec75d87" />


## How to install BepInEx (5 mono) guide [https://docs.bepinex.dev/articles/user_guide/installation/index.html]

TLDR:
1. Download the correct version of BepInEx (bepinex 5 mono) [https://github.com/BepInEx/BepInEx]
2. Extract the contents into the game root (where [NuclearOption.exe] lives)
3. Start the game once to generate configuration files.
4. Open [Nuclear Option\BepInEx\config\BepInEx.cfg] and make sure that the setting 
   [Chainloader]
   HideGameManagerObject = true.

5. (optional) also edit 
   [Logging.Console]
   Enabled = true.

(you can also change bepinex settings ingame using the Mod Configuration manager)


## How to install mods for BepInEx?

- in the downloaded zip file there is a folder, place it in [Nuclear Option\BepInEx\plugins\ (optional folder)]
- the mod .dll and .nobp file must be together in the same folder, they can be placed under any subfolder of plugins
