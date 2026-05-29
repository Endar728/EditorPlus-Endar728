# EditorPlus

A [BepInEx](https://docs.bepinex.dev/) mod that enhances the **Nuclear Option** mission editor with a node graph, multi-unit workflows, blueprint save/paste, and quality-of-life tools.

**Current version:** 1.6.2  
**Maintained by:** [Endar728](https://github.com/Endar728) — [EditorPlus-Endar728](https://github.com/Endar728/EditorPlus-Endar728)  
**Original author:** [nikkorap](https://github.com/nikkorap/EditorPlus) (v1.4.2)

![EditorPlus mission editor screenshot](https://github.com/user-attachments/assets/6489d11f-7bdb-4868-85cf-6edbeec75d87)

## Install

### 1. BepInEx 5 (Mono)

1. Install [BepInEx 5 Mono](https://github.com/BepInEx/BepInEx) into your Nuclear Option game folder (where `NuclearOption.exe` lives).
2. Launch the game once so BepInEx generates its config files.
3. In `Nuclear Option\BepInEx\config\BepInEx.cfg`, set:

   ```ini
   [Chainloader]
   HideGameManagerObject = true
   ```

See the [BepInEx installation guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for details.

### 2. EditorPlus

From the [latest release](https://github.com/Endar728/EditorPlus-Endar728/releases), copy into `Nuclear Option\BepInEx\plugins\` (any subfolder is fine):

- `com.nikkorap.EditorPlus.dll`
- `Newtonsoft.Json.dll` **(required)**

Blueprints are saved to `<game install>\Blueprints\` — that folder is created automatically when the mod loads.

## Mission editor toolbar

| Button | What it does |
|--------|----------------|
| **Graph** | Open the objective/outcome node graph overlay |
| **Builder** | Open Atomic Builder (blueprint save/paste panel) |
| **Tools** | Duplicate, batch rename, and graph grid |
| **Hold Pos** | Keep newly placed / pasted units from drifting |
| **noclip** | Ignore terrain clamping while editing |

## Hotkeys

| Shortcut | Action |
|----------|--------|
| **Ctrl+C** | Copy selected units |
| **Ctrl+V** | Paste copied units at cursor |
| **Ctrl+D** | Duplicate selection in place (small offset) |
| **Ctrl+Alt+V** | Paste blueprint at cursor |
| **Delete** | Remove all selected units |
| **Shift + drag** | Box-select multiple units |

Unit hotkeys are disabled while you are typing in a text field.

## Features

### Copy, paste, and duplicate

- Copy multi-unit groups with relative positions, rotations, and properties.
- Paste at the cursor while keeping formation shape.
- Duplicate in place via **Ctrl+D** or **Tools → Duplicate**.
- Each pasted/duplicated unit gets a unique name to avoid mission JSON corruption.
- Automatically disables the conflicting **UnitCopyPaste** mod handler when present.

### Batch rename *(v1.6.2)*

- **Tools → Batch rename…** opens a panel in the bottom-right of the editor.
- Select multiple units, enter a **name prefix** and **start number**, then apply.
- Names are assigned in order (`Squad_A_1`, `Squad_A_2`, …) with uniqueness checks across the mission.

### Atomic Builder *(v1.6.x)*

In-editor blueprint save and paste, compatible with desktop Atomic Builder JSON files.

- **Builder** toolbar button → side panel with blueprint name, capture radius, **Save blueprint**, and **Paste at cursor**.
- **Ctrl+Alt+V** pastes a blueprint at the editor camera / cursor.
- **Ctrl+V** is reserved for copied unit groups only.

### Node graph UI

- Drag connections between objective and outcome ports.
- Hover a connection or port and press **Delete** to remove it.
- Ghost lines from nodes to units, airbases, and waypoints.
- **Shift + LMB** on a node to add the current unit selection to it.

### Group selection

- **Shift + LMB drag** to box-select units.
- Click any selected unit to set the group pivot.
- Faction changes and delete apply to the whole selection.

### Placement and camera

- Hold **Ctrl** for rapid unit placement.
- Hold **Ctrl** to switch between move and rotate handles.
- **Hold Pos** and **noclip** toggles on the toolbar.
- Free-camera collision disabled in the mission editor only.
- Extended dropdown lists and removed height limits.

## Credits and lineage

- **nikkorap** — original EditorPlus (node graph, group selection, placement tools)
- **Endar728** — copy/paste, mass delete, hold position, noclip, free camera fixes, Atomic Builder integration, duplicate button, batch rename, and toolbar improvements

Copy-paste, mass delete, and related multi-unit workflow features were added in the Endar728 release line and were not part of the original v1.4.2.

## Feedback

Found a bug or have a suggestion? Open an issue on [GitHub](https://github.com/Endar728/EditorPlus-Endar728/issues).
