# EditorPlus 1.6.1 — Release notes

**Update for users of [EditorPlus-Endar728](https://github.com/Endar728/EditorPlus-Endar728/releases) (1.5.2 and later).**

This release builds on **Editor Plus 1.5.2** (free-camera / carrier-deck fix) and **1.5.1** (group copy-paste formation fixes). It adds **Atomic Builder** — save and paste unit layouts as JSON blueprints directly in the mission editor — plus fixes for blueprint capture, paste hotkeys, and UI.

---

## Install / upgrade

1. Install [BepInEx 5 (Mono)](https://docs.bepinex.dev/articles/user_guide/installation/index.html) in your Nuclear Option folder if you have not already.
2. Copy from this release zip into `Nuclear Option\BepInEx\plugins\` (any subfolder is fine):
   - `com.nikkorap.EditorPlus.dll`
   - `Newtonsoft.Json.dll` **(required — include both files)**
3. Start the game once. Blueprints are stored in:

   **`Nuclear Option\Blueprints\`**

   That folder is created automatically when the mod loads.

4. Open the **mission editor**, enable **Builder** on the toolbar, and use the **Atomic Builder** panel.

**Upgrade from 1.5.2:** Replace the old `com.nikkorap.EditorPlus.dll`. Add `Newtonsoft.Json.dll` beside it if it is not already there. Your existing copy-paste workflow (Ctrl+C / Ctrl+V / Ctrl+D) is unchanged except where noted below.

---

## What’s new

### Atomic Builder (in-editor blueprints)

- **Builder** toolbar button opens the Atomic Builder side panel.
- **Blueprint** tab — name, capture radius, **Save blueprint**, **Paste at cursor**.
- **Library** tab — search and pick saved `.json` blueprints.
- **Save** captures units within the radius around the capture center (selected group centroid, or all placed units, or cursor fallback).
- **Paste** spawns the layout at the editor camera / cursor anchor with terrain-aware placement (same ideas as desktop Atomic Builder).
- Compatible with existing desktop Atomic Builder JSON; copy old files into `Nuclear Option\Blueprints\` if needed.

### Hotkeys (important)

| Shortcut | Action |
|----------|--------|
| **Ctrl+C** | Copy selected units (group clipboard) |
| **Ctrl+V** | Paste **copied units** at cursor |
| **Ctrl+Alt+V** | Paste **blueprint** at cursor (name from Blueprint tab or Library) |
| **Ctrl+D** | Duplicate selection in place |

Blueprint paste and unit group paste are **separate** so Ctrl+V no longer conflicts with blueprint workflows.

### UI

- Atomic Builder panel styled to match the mission editor: **dark grey** background, **green** accents (title, primary buttons, active tabs, library selection).

---

## Fixed (since 1.5.2)

- **Blueprint save returned 0 objects** — Capture now uses consistent **global** positions for radius checks and JSON output; always prefers **live editor units** before falling back to an on-disk mission file. Empty blueprints are rejected instead of writing useless files.
- **Paste said “no spawnable units”** — Follows from the save fix; saved blueprints now include units when anything is in capture radius.
- **Blueprint folder** — Defaults to **`<game install>\Blueprints`**, created on plugin load (no Desktop / BepInEx plugins path required).
- **Newtonsoft.Json** — Build deploys `Newtonsoft.Json.dll` next to the plugin to avoid paste `TypeLoadException` when only the main DLL was copied.
- **TerrainHeightMap spam on large paste** — Throttled physics during spawn, formation terrain clamp, and per-unit terrain snap after paste (when terrain collision is enabled).

---

## Unchanged from 1.5.2

- Free-camera collision bypass **only in the mission editor** (carrier deck / multiplayer fix from 1.5.2).
- Group copy-paste formation and rotation fixes from **1.5.1**.
- Hold position, noclip, mass delete, node graph, box selection, and other EditorPlus features from prior Endar728 releases.

---

## Tips

- **Nothing saved in radius?** Select your build before saving, increase **Radius (m)**, or check the log for `Live editor capture: N unit(s)`.
- **Mission not saved to disk yet?** Save still works from **live** units in the editor; you do not need a mission JSON on disk first.
- **Paste blueprint:** Set the name on the Blueprint tab or pick one in Library, then **Ctrl+Alt+V** at the target location.

---

## Credits

- **Original EditorPlus:** [nikkorap/EditorPlus](https://github.com/nikkorap/EditorPlus)
- **1.5.x maintenance & copy-paste:** [Endar728/EditorPlus-Endar728](https://github.com/Endar728/EditorPlus-Endar728)
- **Atomic Builder integration & 1.6.x:** This fork / release

Report issues with log excerpts from `BepInEx\LogOutput.log` (search for `[EditorPlus]` and `[AtomicBuilder]`).
